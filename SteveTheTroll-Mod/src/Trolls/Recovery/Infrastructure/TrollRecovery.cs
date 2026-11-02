using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Core.Results;
using SteveTheTroll.Taming;
using SteveTheTroll.Trolls.Contracts;
using SteveTheTroll.Trolls.Core;
using SteveTheTroll.Trolls.Recovery.Application;
using SteveTheTroll.Trolls.Recovery.Domain;
using SteveTheTroll.Trolls.Recovery.Execution;
using UnityEngine;

namespace SteveTheTroll.Trolls.Recovery.Infrastructure
{
    /// <summary>
    /// D-167 (user spec "Troll Movement - Stuck Detection and Recovery System"): ONE stuck detection and recovery per troll, shared by the farming walk (TrollFarmer) and the walk to the hub
    /// (TrollReturn, TrollFarmer.GoHome). The caller hands over the goal, the identity of what it walks to and whether that may be abandoned (a farm target yes, a hub never); this
    /// component measures real progress (ProgressMeter), and when the troll is blocked for 3 s it runs the ladder of RecoveryMachine: rage against a destructible obstruction, a
    /// detour of 90 degrees and up to 10 m, a second L-shaped detour, then the cycle has failed; two failed cycles for the same troll + resource blacklist that resource for that troll
    /// for 120 s (RecoveryBook, static: it outlives this component and the farmer). The decisions are game-free (StuckRules, RecoveryMachine, RecoveryBook; unit-tested); this class
    /// reads the game (what blocks, which ground is valid) and acts. It lives as long as the troll.
    /// Architecture V2 (D-169): the adapter of the recovery slice - reads the facts into a RecoveryInput, RecoveryPipeline decides (alive / owner checks, then the machine),
    /// RecoveryExecutor performs the decision (ownership checked again at the execution boundary), movement and swings go through the troll's capabilities (TrollContext).
    /// </summary>
    internal sealed class TrollRecovery : MonoBehaviour
    {
        /// Per troll + resource failure counters and blacklists.
        internal static readonly RecoveryBook Book = new RecoveryBook();
        const float ProbeRadius = 0.6f, ProbeDistance = 3f, ChestHeight = 1f, StaleSeconds = 2f, LogSeconds = 3f, SwingGap = 0.3f, RageReach = 2.8f, PruneSeconds = 60f;
        static int _mask, _blockMask;
        static float _nextPrune;

        readonly ProgressMeter _meter = new ProgressMeter();
        RecoveryPipeline _pipeline;
        RecoveryExecutor _executor;
        TrollContext _ctx;
        ResponseStatus _lastStatus = ResponseStatus.Success;
        float _d;                      // distance to the goal in the frame being executed (m)
        string _troll = "";
        long _ka, _kb;
        bool _hasCtx, _canAbandon, _abandon;
        float _lastTick, _nextLog, _nextSwing, _rageEnd;
        Vector3 _goal, _intended;
        GameObject _goalObject, _rageTarget;
        Obstruction _kind;
        string _obstacleName = "";
        // the running leg
        Vector3 _legStart, _legTarget, _legLast;
        float _legPlanned, _legBegan, _legStalled;
        Character _other;

        internal static TrollRecovery Of(Component c)
        {
            var r = c.GetComponent<TrollRecovery>();
            return r != null ? r : c.gameObject.AddComponent<TrollRecovery>();
        }

        void Awake()
        {
            _ctx = new TrollContext(GetComponent<Character>());
            _pipeline = new RecoveryPipeline();
            // the executor owns the delegates: bound once here (method groups), never per frame
            _executor = new RecoveryExecutor(_ctx, OnStartRage, OnEndRage, OnStartLeg, OnEndLeg, OnCycleFailed, OnCycleReset);
            ModLog.Verbose("TrollRecovery: " + TrollTaming.Tag(this) + " set up (Architecture V2: pipeline + executor, capabilities complete " + _ctx.Capabilities.Complete + ")");
        }

        RecoveryMachine M { get { return _pipeline.Machine; } }

        /// A manoeuvre (rage / detour leg) is running right now: the caller must hand the frame to Continue.
        internal bool Busy { get { return M.Stage != RecoveryStage.Travel && Time.time - _lastTick < StaleSeconds; } }

        internal RecoveryStage Stage { get { return M.Stage; } }

        /// The last Tick ended a cycle with the resource blacklisted: the caller gives it up (read once). Expected outcome: true once per blacklisting.
        internal bool TakeAbandon() { bool a = _abandon; _abandon = false; return a; }

        /// The resource is blacklisted for this troll (farm search skips it). Expected outcome: false for a resource the troll never failed on.
        internal static bool IsBlacklisted(Character troll, GameObject resource)
        {
            if (troll == null || resource == null || Book.Count == 0) return false;
            long a, b;
            KeyOf(resource, out a, out b);
            return Book.IsBlacklisted(TrollRegistry.IdOf(troll), a, b, Time.time);
        }

        /// The network identity of a resource (its ZDO id), never its prefab: failing on one beech tree must not block the next one. Objects without a ZDO use their instance id.
        internal static void KeyOf(GameObject g, out long a, out long b)
        {
            var v = g != null ? g.GetComponentInParent<ZNetView>() : null;
            var z = v != null && v.IsValid() ? v.GetZDO() : null;
            if (z != null) { a = z.m_uid.UserID; b = z.m_uid.ID; return; }
            a = -1L; b = g != null ? g.GetInstanceID() : 0L;
        }

        /// A far place (no object yet): kind -3, 10 m cell.
        internal static void KeyOfPlace(Vector3 p, out long a, out long b) { a = -3L; b = (long)Mathf.RoundToInt(p.x / 10f) * 100000L + Mathf.RoundToInt(p.z / 10f); }

        /// The hub: kind -2, 10 m cell. A hub is never abandoned.
        internal static void KeyOfHub(Vector3 p, out long a, out long b) { a = -2L; b = (long)Mathf.RoundToInt(p.x / 10f) * 100000L + Mathf.RoundToInt(p.z / 10f); }

        /// The troll reached what it walked to: its failure counter starts again (spec). Expected outcome: 0 failed cycles.
        internal int Reached()
        {
            if (!_hasCtx || (!M.Dirty && Book.Failed(_troll, _ka, _kb) == 0)) return 0;
            Book.Recovered(_troll, _ka, _kb);
            _pipeline.BeginCycle(0);
            return Book.Failed(_troll, _ka, _kb);
        }

        /// The walk is over (the caller destroyed its target / switched jobs): forget the context so the next walk starts clean. A running manoeuvre is cut short.
        internal void Forget() { _hasCtx = false; _pipeline.Abort(); }

        /// One frame of a walk. `goal` = where the troll walks (flat distance = progress), (ka, kb) = what it walks to (KeyOf*), `canAbandon` = a farm target (true) / a hub (false),
        /// `expectMovement` = false for an intentional pause, `goalObject` = the resource itself (never an obstruction). Expected outcome: true = a manoeuvre drove the troll this frame
        /// (the caller issues no movement of its own); false = the caller walks as usual.
        internal bool Tick(float dt, Vector3 goal, long ka, long kb, bool canAbandon, bool expectMovement, GameObject goalObject)
        {
            var combat = _ctx.Capabilities.Combat;
            float now = Time.time;
            bool alive = combat != null && !combat.IsDead;
            float d = 0f, stuck = 0f, sustained = 0f;
            bool legDone = false, rageable = false, headOn = false;
            if (alive)
            {
                if (M.Stage != RecoveryStage.Travel && now - _lastTick > StaleSeconds)
                {
                    ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " manoeuvre cut short (" + (now - _lastTick).ToString("F1") + " s without a step) - walks on");
                    _pipeline.Abort();
                    _meter.Reset(Flat(goal));
                }
                _lastTick = now;
                _goal = goal; _goalObject = goalObject; _canAbandon = canAbandon;
                d = Flat(goal);
                if (!_hasCtx || ka != _ka || kb != _kb) EnterContext(ka, kb, d);
                if (now >= _nextPrune) { _nextPrune = now + PruneSeconds; Book.Prune(now); }

                if (M.Stage == RecoveryStage.Leg) legDone = LegDone(dt);
                if (M.Stage == RecoveryStage.Travel) _meter.Update(d, dt, expectMovement && !combat.InAttack && !combat.IsStaggering);
                if (M.Stage == RecoveryStage.Travel && _meter.StuckSeconds >= StuckRules.StuckTrigger) Probe(out rageable, out headOn);
                stuck = _meter.StuckSeconds; sustained = _meter.SustainedSeconds;
            }

            // D-169: decide (pipeline: alive / owner checks, then the machine), then perform (executor: ownership checked again at the boundary)
            var response = _pipeline.Evaluate(new RecoveryInput(now, stuck, sustained, rageable, headOn, legDone, alive, _ctx.IsOwner));
            if (!response.IsSuccess)
            {
                if (response.Status != _lastStatus) ModLog.Verbose("TrollRecovery: " + TrollTaming.Tag(this) + " recovery " + response.Status + " (" + response.Reason + ") - nothing driven, a running manoeuvre is cut short");
                _lastStatus = response.Status;
                _pipeline.Abort();
                return M.Stage != RecoveryStage.Travel;          // Abort leaves Travel, so false: the caller does what it does
            }
            _lastStatus = response.Status;
            if (!response.Value.IsNone)
            {
                _d = d;
                var done = _executor.Execute(response.Value);
                ModLog.Verbose("TrollRecovery: " + TrollTaming.Tag(this) + " decided " + response.Value + " -> " + done);
            }
            if (now >= _nextLog && (M.Stage != RecoveryStage.Travel || _meter.StuckSeconds > 1f))
            {
                _nextLog = now + LogSeconds;
                ModLog.Verbose("TrollRecovery: " + TrollTaming.Tag(this) + " " + M.Stage + " goal " + d.ToString("F1") + " m, stuck " + _meter.StuckSeconds.ToString("F1") + " s, progress " + _meter.SustainedSeconds.ToString("F1")
                    + " s, cycle failures " + Book.Failed(_troll, _ka, _kb) + ", raged " + M.Raged + ", detours " + M.DetoursDone);
            }
            return M.Stage != RecoveryStage.Travel && Drive(dt, combat);
        }

        /// Hands the frame to a running manoeuvre (the caller checks Busy first). Expected outcome: true = it drove the troll.
        internal bool Continue(float dt)
        {
            return _hasCtx && Tick(dt, _goal, _ka, _kb, _canAbandon, false, _goalObject);
        }

        void EnterContext(long ka, long kb, float d)
        {
            _troll = _ctx.EnsureId();       // the one explicit id write (owner only), as TrollRegistry.IdOf did here before D-169
            _ka = ka; _kb = kb; _hasCtx = true; _abandon = false;
            _pipeline.BeginCycle(Book.Failed(_troll, ka, kb));
            _meter.Reset(d);
            ModLog.Verbose("TrollRecovery: " + TrollTaming.Tag(this) + " walks to " + (ka == -2L ? "the hub" : ka == -3L ? "a far place" : "a resource") + " " + ka + "/" + kb + ", " + d.ToString("F1") + " m, failed cycles so far "
                + Book.Failed(_troll, ka, kb) + ", " + (_canAbandon ? "may be abandoned" : "never abandoned"));
        }

        float Flat(Vector3 goal) { var f = goal - transform.position; f.y = 0f; return f.magnitude; }

        // ---- the executor's actions (D-169: bound once in Awake; _d = the distance of the frame being executed) ----

        void OnStartRage()
        {
            _rageEnd = Time.time + StuckRules.RageSeconds;
            ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " is blocked " + _meter.StuckSeconds.ToString("F1") + " s by " + _obstacleName + " (" + _kind + ") " + _d.ToString("F1") + " m from its goal - rage for " + StuckRules.RageSeconds.ToString("0") + " s");
        }

        void OnEndRage()
        {
            ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " rage over - " + (_rageTarget == null ? "the obstruction is gone" : _obstacleName + " is still there") + ", " + _d.ToString("F1") + " m from its goal");
            _rageTarget = null;
            _meter.Reset(_d);
        }

        void OnStartLeg(RecoveryDecision decision) { StartLeg(decision); }

        void OnEndLeg()
        {
            ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " detour " + M.DetoursDone + " done after " + Flat2(_legStart).ToString("F1") + " m - recalculates the route to its goal (" + _d.ToString("F1") + " m)");
            _meter.Reset(_d);
        }

        void OnCycleFailed() { CycleFailed(_d); }

        void OnCycleReset()
        {
            ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " made progress for " + StuckRules.SustainedReset.ToString("0") + " s - failure counter of this goal reset (was " + Book.Failed(_troll, _ka, _kb) + ")");
            Book.Recovered(_troll, _ka, _kb);
            _pipeline.BeginCycle(0);
        }

        float Flat2(Vector3 from) { var f = transform.position - from; f.y = 0f; return f.magnitude; }

        void CycleFailed(float d)
        {
            string tag = TrollTaming.Tag(this);
            bool blacklisted = Book.CycleFailed(_troll, _ka, _kb, Time.time, _canAbandon);
            int failed = Book.Failed(_troll, _ka, _kb);
            _pipeline.BeginCycle(blacklisted ? 0 : failed);
            _meter.Reset(d);
            if (blacklisted)
            {
                _abandon = true;
                ModLog.Warn("TrollRecovery: " + tag + " could not reach its resource " + _ka + "/" + _kb + " in " + StuckRules.MaxFailedCycles + " complete recovery cycles - abandons it for " + StuckRules.BlacklistSeconds.ToString("0") + " s (other resources stay available)");
            }
            else
            {
                ModLog.Info("TrollRecovery: " + tag + " complete recovery cycle failed (" + failed + " so far, " + d.ToString("F1") + " m from its goal) - " + (_canAbandon ? "one more cycle, turning " + (M.RightFirst ? "right" : "left") + " first" : "a hub is never abandoned, tries again turning " + (M.RightFirst ? "right" : "left") + " first"));
            }
        }

        // ---- what blocks the way ----

        // A sphere cast along the line to the goal, chest height: the nearest thing that is not the troll itself and not the resource it walks to.
        void Probe(out bool rageable, out bool headOn)
        {
            rageable = false; headOn = false;
            _kind = Obstruction.None; _obstacleName = "nothing found"; _rageTarget = null; _other = null;
            if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "character", "character_net", "vehicle");
            var dir = _goal - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();
            var origin = transform.position + Vector3.up * ChestHeight;
            RaycastHit best = default(RaycastHit);
            float bestD = float.MaxValue;
            foreach (var h in Physics.SphereCastAll(origin, ProbeRadius, dir, ProbeDistance, _mask, QueryTriggerInteraction.Ignore))
            {
                if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
                if (_goalObject != null && h.collider.transform.IsChildOf(_goalObject.transform)) continue;
                if (h.distance < bestD) { bestD = h.distance; best = h; }
            }
            if (best.collider == null) return;
            var c = best.collider;
            var ch = c.GetComponentInParent<Character>();
            var piece = c.GetComponentInParent<Piece>();
            bool playerBuilt = piece != null && piece.IsPlacedByPlayer();
            bool breakable = c.GetComponentInParent<IDestructible>() != null;
            _kind = StuckRules.Classify(ch != null, ch != null && ch.IsPlayer(), ch != null && ch.IsTamed(), playerBuilt, breakable);
            var root = ch != null ? ch.gameObject : (piece != null ? piece.gameObject : c.gameObject);
            _obstacleName = root.name + " (" + c.name + ", " + bestD.ToString("F1") + " m)";
            rageable = StuckRules.Rageable(_kind);
            if (rageable) _rageTarget = ch != null ? ch.gameObject : c.gameObject;
            if (ch != null && ch.IsTamed() && TrollTaming.IsTroll(ch))
            {
                var mf = transform.forward; var of = ch.transform.forward;
                headOn = StuckRules.HeadOn(transform.position.x, transform.position.z, mf.x, mf.z, ch.transform.position.x, ch.transform.position.z, of.x, of.z);
                if (headOn) _other = ch;
            }
        }

        // ---- detour legs ----

        void StartLeg(RecoveryDecision decision)
        {
            string tag = TrollTaming.Tag(this);
            var from = transform.position;
            var flat = _goal - from; flat.y = 0f;
            if (decision.Leg == 0) _intended = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
            Vector3 dir; float metres; Side side = decision.LegSide;
            if (decision.Leg == 0)
            {
                float rightM = PlanMetres(from, LegDir(Side.Right)), leftM = PlanMetres(from, LegDir(Side.Left));
                float prefM = side == Side.Right ? rightM : leftM, otherM = side == Side.Right ? leftM : rightM;
                side = StuckRules.ChooseSide(side, prefM, otherM, out metres);
                dir = LegDir(side);
                ModLog.Info("TrollRecovery: " + tag + " detour " + decision.DetourNumber + (decision.HeadOn ? " (head on with " + (_other != null ? TrollTaming.Tag(_other) : "a troll") + ")" : "") + ", leg 1: turns " + side + " of its intended direction, valid ground right " + rightM.ToString("F0") + " m / left " + leftM.ToString("F0") + " m, walks " + metres.ToString("F0") + " m");
            }
            else
            {
                dir = _intended;       // the second leg of the L: turn back (90 degrees the other way) to the intended direction
                metres = PlanMetres(from, dir);
                ModLog.Info("TrollRecovery: " + tag + " detour 2, leg 2: turns back to its intended direction, valid ground " + metres.ToString("F0") + " m, walks it");
            }
            _legStart = from; _legLast = from; _legPlanned = metres; _legBegan = Time.time; _legStalled = 0f;
            _legTarget = from + dir * metres;
        }

        Vector3 LegDir(Side s)
        {
            float dx, dz;
            StuckRules.Direction(_intended.x, _intended.z, s, out dx, out dz);
            return new Vector3(dx, 0f, dz);
        }

        float GroundY(Vector3 p)
        {
            float h;
            return ZoneSystem.instance != null && ZoneSystem.instance.GetSolidHeight(p, out h) ? h : p.y;
        }

        // The valid metres of a leg from `from` along `dir`: every metre needs ground that does not jump, shallow liquid and a free line (StuckRules.StepOk), and the game's path search
        // must find a way to the end (a point it cannot reach is shortened).
        float PlanMetres(Vector3 from, Vector3 dir)
        {
            if (_blockMask == 0) _blockMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "vehicle");
            var steps = new List<bool>();
            Vector3 prev = from;
            float prevH = GroundY(from);
            for (int i = 1; i <= (int)StuckRules.LegMetres; i++)
            {
                var p = from + dir * i;
                float h = GroundY(p);
                float depth = Mathf.Max(0f, Floating.GetLiquidLevel(new Vector3(p.x, h, p.z)) - h);
                bool blocked = Physics.Linecast(prev + Vector3.up * ChestHeight, p + Vector3.up * ChestHeight, _blockMask, QueryTriggerInteraction.Ignore);
                steps.Add(StuckRules.StepOk(h - prevH, depth, blocked));
                prev = new Vector3(p.x, h, p.z); prevH = h;
            }
            float metres = StuckRules.PlanMetres(steps, StuckRules.LegMetres);
            var ai = GetComponent<BaseAI>();
            var path = new List<Vector3>();
            while (metres >= StuckRules.MinLegMetres && Pathfinding.instance != null && ai != null
                && !Pathfinding.instance.GetPath(from, from + dir * metres, path, ai.m_pathAgentType, false))
                metres -= 2f;
            return Mathf.Max(0f, metres);
        }

        bool LegDone(float dt)
        {
            var pos = transform.position;
            float moved = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(_legLast.x, 0f, _legLast.z));
            _legStalled = moved < StuckRules.LegStallMetres * dt / StuckRules.LegStallSeconds ? _legStalled + dt : 0f;
            _legLast = pos;
            float walked = Flat2(_legStart);
            bool clear = M.HeadOn && _other != null && StuckRules.HeadOnClear(Vector3.Distance(pos, _other.transform.position));
            return clear || StuckRules.LegDone(walked, _legPlanned, Time.time - _legBegan, _legStalled);
        }

        // ---- driving ----

        bool Drive(float dt, ITrollCombat combat)
        {
            var mv = _ctx.Capabilities.Movement;
            if (mv == null) return mv != null;
            if (M.Stage == RecoveryStage.Leg) { mv.MoveTo(dt, _legTarget, 0.4f, true); return M.Stage == RecoveryStage.Leg; }
            if (M.Stage != RecoveryStage.Rage) return M.Stage == RecoveryStage.Rage;
            if (_rageTarget == null) { mv.MoveTo(dt, _goal, 1f, false); return _rageTarget == null; }       // gone already: walks on to its goal while the 5 s run out
            var c = _rageTarget.GetComponentInChildren<Collider>();
            var point = c != null ? c.ClosestPoint(transform.position + Vector3.up * ChestHeight) : _rageTarget.transform.position;
            var flat = point - transform.position; flat.y = 0f;
            if (flat.magnitude > RageReach) mv.MoveTo(dt, point, 1.5f, false); else mv.Stop();
            mv.LookAt(point);
            if (combat.InAttack || Time.time < _nextSwing || !mv.IsLookingAt(point, 40f)) return M.Stage == RecoveryStage.Rage;
            bool low = c != null && c.bounds.max.y < transform.position.y + 1f;
            if (combat.SwingAt(_rageTarget, low)) _nextSwing = Time.time + SwingGap;
            return M.Stage == RecoveryStage.Rage;
        }
    }
}
