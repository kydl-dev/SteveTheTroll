using System.Collections.Generic;
using System.Text;
using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Core;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Movement.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Domain;
using SteveTheTroll.Features.Trolls.Recovery.Application;
using SteveTheTroll.Features.Trolls.Recovery.Domain;
using SteveTheTroll.Features.Trolls.Recovery.Execution;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Recovery.Infrastructure;

/// <summary>
///     D-167 (user spec "Troll Movement - Stuck Detection and Recovery System"): ONE stuck detection and recovery per
///     troll, shared by the farming walk (TrollFarmer) and the walk to the hub
///     (TrollReturn, TrollFarmer.GoHome). The caller hands over the goal, the identity of what it walks to and whether
///     that may be abandoned (a farm target yes, a hub never); this
///     component measures real progress (ProgressMeter), and when the troll is blocked for 3 s it runs the ladder of
///     RecoveryMachine: rage against a destructible obstruction, a
///     detour of 90 degrees and up to 10 m, a second L-shaped detour, then the cycle has failed; two failed cycles for the
///     same troll + resource blacklist that resource for that troll
///     for 120 s (RecoveryBook, static: it outlives this component and the farmer). The decisions are game-free
///     (StuckRules, RecoveryMachine, RecoveryBook; unit-tested); this class
///     reads the game (what blocks, which ground is valid) and acts. It lives as long as the troll.
///     Architecture V2 (D-169): the adapter of the recovery slice - reads the facts into a RecoveryInput, RecoveryPipeline
///     decides (alive / owner checks, then the machine),
///     RecoveryExecutor performs the decision (ownership checked again at the execution boundary), movement and swings go
///     through the troll's capabilities (TrollContext).
/// </summary>
internal sealed partial class TrollRecovery : MonoBehaviour
{
    private const float ProbeRadius = 0.6f,
        ProbeDistance = 3f,
        ChestHeight = 1f,
        StaleSeconds = 2f,
        LogSeconds = 3f,
        SwingGap = 0.3f,
        RageReach = 2.8f,
        PruneSeconds = 60f;

    // D-175 (run 175732: still "height change 13.4 m at 1 m" in 6 of 10 directions next to trees - the highest hit was a tree crown / something overhead): the ground a
    // troll can step on is the highest hit at most WalkUp metres above `refY` (the height of the previous step); anything higher is overhead and the chest-height line
    // test decides whether it blocks. Only when nothing is that low the lowest hit counts.
    private const float WalkUp = 2.5f;

    /// Per troll + resource failure counters and blacklists.
    internal static readonly RecoveryBook Book = new();

    private static readonly HashSet<int>
        LoggedPieces = new(); // D-172: the Destructible-piece line once per piece, not every probe

    private static int _mask, _blockMask;
    private static float _nextPrune;

    // D-174 (run 170316: "R70 0 m (height change 18.2 m at 1 m)" with nothing in the way - ZoneSystem.GetSolidHeight hits the troll's OWN colliders and backpack chests above a
    // point within its body): the ground under `p` is the highest solid hit that is not part of this troll; no hit -> the game's solid height, else p.y.
    private static readonly RaycastHit[] DownHits = new RaycastHit[16];

    private static int _groundMask;

    private static readonly List<float> CandMetres = new();
    private static readonly List<Vector3> CandDirs = new();
    private static readonly List<string> CandNames = new();

    private readonly ProgressMeter _meter = new();
    private TrollContext _ctx;
    private float _d; // distance to the goal in the frame being executed (m)
    private RecoveryExecutor _executor;
    private Vector3 _goal, _intended;
    private GameObject _goalObject, _rageTarget;
    private bool _hasCtx, _canAbandon, _abandon;
    private long _ka, _kb;
    private Obstruction _kind;
    private ResponseStatus _lastStatus = ResponseStatus.Success;
    private float _lastTick, _nextLog, _nextSwing, _rageEnd;

    private float _legPlanned, _legBegan, _legStalled;

    // the running leg
    private Vector3 _legStart, _legTarget, _legLast;

    private string _obstacleName = "";
    private Character _other;
    private RecoveryPipeline _pipeline;
    private string _troll = "";

    private RecoveryMachine M => _pipeline.Machine;

    /// A manoeuvre (rage / detour leg) is running right now: the caller must hand the frame to Continue.
    internal bool Busy => M.Stage != RecoveryStage.Travel && Time.time - _lastTick < StaleSeconds;

    internal RecoveryStage Stage => M.Stage;

    private void Awake()
    {
        _ctx = new TrollContext(GetComponent<Character>());
        _pipeline = new RecoveryPipeline();
        // the executor owns the delegates: bound once here (method groups), never per frame
        _executor = new RecoveryExecutor(_ctx, OnStartRage, OnEndRage, OnStartLeg, OnEndLeg, OnCycleFailed,
            OnCycleReset);
        ModLog.Verbose("TrollRecovery: " + TrollTaming.Tag(this) +
                       " set up (Architecture V2: pipeline + executor, capabilities complete " +
                       _ctx.Capabilities.Complete + ")");
    }

    internal static TrollRecovery Of(Component c)
    {
        TrollRecovery r = c.GetComponent<TrollRecovery>();
        return r != null ? r : c.gameObject.AddComponent<TrollRecovery>();
    }

    /// The last Tick ended a cycle with the resource blacklisted: the caller gives it up (read once). Expected outcome: true once per blacklisting.
    internal bool TakeAbandon()
    {
        bool a = _abandon;
        _abandon = false;
        return a;
    }

    /// The resource is blacklisted for this troll (farm search skips it). Expected outcome: false for a resource the troll never failed on.
    internal static bool IsBlacklisted(Character troll, GameObject resource)
    {
        if (troll == null || resource == null || Book.Count == 0)
        {
            return false;
        }

        long a, b;
        KeyOf(resource, out a, out b);
        return Book.IsBlacklisted(TrollRegistry.IdOf(troll), a, b, Time.time);
    }

    /// The network identity of a resource (its ZDO id), never its prefab: failing on one beech tree must not block the next one. Objects without a ZDO use their instance id.
    internal static void KeyOf(GameObject g, out long a, out long b)
    {
        ZNetView v = g != null ? g.GetComponentInParent<ZNetView>() : null;
        ZDO z = v != null && v.IsValid() ? v.GetZDO() : null;
        if (z != null)
        {
            a = z.m_uid.UserID;
            b = z.m_uid.ID;
            return;
        }

        a = -1L;
        b = g != null ? g.GetInstanceID() : 0L;
    }

    /// A far place (no object yet): kind -3, 10 m cell.
    internal static void KeyOfPlace(Vector3 p, out long a, out long b)
    {
        a = -3L;
        b = Mathf.RoundToInt(p.x / 10f) * 100000L + Mathf.RoundToInt(p.z / 10f);
    }

    /// The hub: kind -2, 10 m cell. A hub is never abandoned.
    internal static void KeyOfHub(Vector3 p, out long a, out long b)
    {
        a = -2L;
        b = Mathf.RoundToInt(p.x / 10f) * 100000L + Mathf.RoundToInt(p.z / 10f);
    }

    /// The troll reached what it walked to: its failure counter starts again (spec). Expected outcome: 0 failed cycles.
    internal int Reached()
    {
        if (!_hasCtx || (!M.Dirty && Book.Failed(_troll, _ka, _kb) == 0))
        {
            return 0;
        }

        Book.Recovered(_troll, _ka, _kb);
        _pipeline.BeginCycle(0);
        return Book.Failed(_troll, _ka, _kb);
    }

    /// The walk is over (the caller destroyed its target / switched jobs): forget the context so the next walk starts clean. A running manoeuvre is cut short.
    internal void Forget()
    {
        _hasCtx = false;
        _pipeline.Abort();
    }

    /// One frame of a walk. `goal` = where the troll walks (flat distance = progress), (ka, kb) = what it walks to (KeyOf*), `canAbandon` = a farm target (true) / a hub (false),
    /// `expectMovement` = false for an intentional pause, `goalObject` = the resource itself (never an obstruction). Expected outcome: true = a manoeuvre drove the troll this frame
    /// (the caller issues no movement of its own); false = the caller walks as usual.
    internal bool Tick(float dt, Vector3 goal, long ka, long kb, bool canAbandon, bool expectMovement,
        GameObject goalObject)
    {
        ITrollCombat combat = _ctx.Capabilities.Combat;
        float now = Time.time;
        bool alive = combat != null && !combat.IsDead;
        float d = 0f, stuck = 0f, sustained = 0f;
        bool legDone = false, rageable = false, headOn = false;
        if (alive)
        {
            if (M.Stage != RecoveryStage.Travel && now - _lastTick > StaleSeconds)
            {
                ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " manoeuvre cut short (" +
                            (now - _lastTick).ToString("F1") + " s without a step) - walks on");
                _pipeline.Abort();
                _meter.Reset(Flat(goal));
            }

            _lastTick = now;
            _goal = goal;
            _goalObject = goalObject;
            _canAbandon = canAbandon;
            d = Flat(goal);
            if (!_hasCtx || ka != _ka || kb != _kb)
            {
                EnterContext(ka, kb, d);
            }

            if (now >= _nextPrune)
            {
                _nextPrune = now + PruneSeconds;
                Book.Prune(now);
            }

            if (M.Stage == RecoveryStage.Leg)
            {
                legDone = LegDone(dt);
            }

            if (M.Stage == RecoveryStage.Travel)
            {
                _meter.Update(d, dt, expectMovement && !combat.InAttack && !combat.IsStaggering);
            }

            if (M.Stage == RecoveryStage.Travel && _meter.StuckSeconds >= StuckRules.StuckTrigger)
            {
                Probe(out rageable, out headOn);
            }

            stuck = _meter.StuckSeconds;
            sustained = _meter.SustainedSeconds;
        }

        // D-169: decide (pipeline: alive / owner checks, then the machine), then perform (executor: ownership checked again at the boundary)
        OperationResponse<RecoveryDecision> response =
            _pipeline.Evaluate(new RecoveryInput
            {
                Now = now, StuckSeconds = stuck, SustainedSeconds = sustained, Rageable = rageable, HeadOn = headOn,
                LegDone = legDone, IsAlive = alive, IsOwner = _ctx.IsOwner
            });
        if (!response.IsSuccess)
        {
            if (response.Status != _lastStatus)
            {
                ModLog.Verbose("TrollRecovery: " + TrollTaming.Tag(this) + " recovery " + response.Status + " (" +
                               response.Reason + ") - nothing driven, a running manoeuvre is cut short");
            }

            _lastStatus = response.Status;
            _pipeline.Abort();
            return M.Stage != RecoveryStage.Travel; // Abort leaves Travel, so false: the caller does what it does
        }

        _lastStatus = response.Status;
        if (!response.Value.IsNone)
        {
            _d = d;
            OperationResponse<RecoveryCmd> done = _executor.Execute(response.Value);
            ModLog.Verbose("TrollRecovery: " + TrollTaming.Tag(this) + " decided " + response.Value + " -> " + done);
        }

        if (now >= _nextSnapshot && M.Stage == RecoveryStage.Travel && _meter.SustainedSeconds < 1f && _meter.StuckSeconds > 0.5f)
        {
            _nextSnapshot = now + 5f;
            ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " not getting anywhere - " + Snapshot(d));
        }

        if (now >= _nextLog && (M.Stage != RecoveryStage.Travel || _meter.StuckSeconds > 1f))
        {
            _nextLog = now + LogSeconds;
            ModLog.Verbose("TrollRecovery: " + TrollTaming.Tag(this) + " " + M.Stage + " goal " + d.ToString("F1") +
                           " m, stuck " + _meter.StuckSeconds.ToString("F1") + " s, progress " +
                           _meter.SustainedSeconds.ToString("F1")
                           + " s, cycle failures " + Book.Failed(_troll, _ka, _kb) + ", raged " + M.Raged +
                           ", detours " + M.DetoursDone);
        }

        return M.Stage != RecoveryStage.Travel && Drive(dt, combat);
    }

    /// Hands the frame to a running manoeuvre (the caller checks Busy first). Expected outcome: true = it drove the troll.
    internal bool Continue(float dt)
    {
        return _hasCtx && Tick(dt, _goal, _ka, _kb, _canAbandon, false, _goalObject);
    }

    private void EnterContext(long ka, long kb, float d)
    {
        _troll = _ctx.EnsureId(); // the one explicit id write (owner only), as TrollRegistry.IdOf did here before D-169
        _ka = ka;
        _kb = kb;
        _hasCtx = true;
        _abandon = false;
        _pipeline.BeginCycle(Book.Failed(_troll, ka, kb));
        _meter.NewAttempt(d);
        ModLog.Verbose("TrollRecovery: " + TrollTaming.Tag(this) + " walks to " +
                       (ka == -2L ? "the hub" : ka == -3L ? "a far place" : "a resource") + " " + ka + "/" + kb + ", " +
                       d.ToString("F1") + " m, failed cycles so far "
                       + Book.Failed(_troll, ka, kb) + ", " + (_canAbandon ? "may be abandoned" : "never abandoned"));
    }

    private float Flat(Vector3 goal)
    {
        Vector3 f = goal - transform.position;
        f.y = 0f;
        return f.magnitude;
    }

    // ---- the executor's actions (D-169: bound once in Awake; _d = the distance of the frame being executed) ----

    private void OnStartRage()
    {
        TrollPath.RecordBadSpot(transform.position, TrollTaming.Tag(this) + " recovery rage"); // D-179 (4)
        _rageEnd = Time.time + StuckRules.RageSeconds;
        ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " is blocked " + _meter.StuckSeconds.ToString("F1") +
                    " s by " + _obstacleName + " (" + _kind + ") " + _d.ToString("F1") +
                    " m from its goal - rage for " + StuckRules.RageSeconds.ToString("0") + " s");
    }

    private void OnEndRage()
    {
        ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " rage over - " +
                    (_rageTarget == null ? "the obstruction is gone" : _obstacleName + " is still there") + ", " +
                    _d.ToString("F1") + " m from its goal");
        _rageTarget = null;
        _meter.Reset(_d);
    }

    private void OnStartLeg(RecoveryDecision decision)
    {
        StartLeg(decision);
    }

    private void OnEndLeg()
    {
        ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " detour " + M.DetoursDone + " done after " +
                    Flat2(_legStart).ToString("F1") + " m - recalculates the route to its goal (" + _d.ToString("F1") +
                    " m)");
        _meter.Reset(_d);
    }

    private void OnCycleFailed()
    {
        CycleFailed(_d);
    }

    private void OnCycleReset()
    {
        ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " made progress for " +
                    StuckRules.SustainedReset.ToString("0") + " s - failure counter of this goal reset (was " +
                    Book.Failed(_troll, _ka, _kb) + ")");
        Book.Recovered(_troll, _ka, _kb);
        _pipeline.BeginCycle(0);
    }

    private float Flat2(Vector3 from)
    {
        Vector3 f = transform.position - from;
        f.y = 0f;
        return f.magnitude;
    }

    /// D-176 (diagnostics): everything about a troll that is not getting anywhere, in one line - where it is and where it goes, how fast it really moves, the stage, the last obstruction
    /// the probe found, detours done, whether it is in an attack / staggered, and whether TrollHold is stopping it. Expected outcome: the text.
    private string Snapshot(float d)
    {
        Character body = GetComponent<Character>();
        Vector3 p = transform.position;
        return "at " + p.ToString("F0") + ", goal " + _goal.ToString("F0") + ", " + d.ToString("F1") + " m to go, real speed " +
               (body != null ? body.GetVelocity().magnitude.ToString("F1") : "?") + " m/s, stage " + M.Stage + ", detours " + M.DetoursDone + ", raged " + M.Raged +
               ", last obstruction " + _obstacleName + " (" + _kind + "), in attack " + (body is Humanoid h && h.InAttack()) + ", staggered " + (body != null && body.IsStaggering()) +
               ", " + TrollHold.Describe(body) + ", " + TrollMoveDiag.Describe(body, _goal);
    }

    private float _nextSnapshot;

    private void CycleFailed(float d)
    {
        string tag = TrollTaming.Tag(this);
        TrollPath.RecordBadSpot(transform.position, tag + " recovery cycle failed"); // D-179 (4)
        bool blacklisted = Book.CycleFailed(_troll, _ka, _kb, Time.time, _canAbandon);
        int failed = Book.Failed(_troll, _ka, _kb);
        _pipeline.BeginCycle(blacklisted ? 0 : failed);
        _meter.Reset(d);
        if (blacklisted)
        {
            _abandon = true;
            ModLog.Warn("TrollRecovery: " + tag + " could not reach its resource " + _ka + "/" + _kb + " in " +
                        StuckRules.MaxFailedCycles + " complete recovery cycles - abandons it for " +
                        StuckRules.BlacklistSeconds.ToString("0") + " s (other resources stay available)");
        }
        else
        {
            ModLog.Info("TrollRecovery: " + tag + " cycle state - " + Snapshot(d));
            ModLog.Info("TrollRecovery: " + tag + " complete recovery cycle failed (" + failed + " so far, " +
                        d.ToString("F1") + " m from its goal) - " + (_canAbandon
                            ? "one more cycle, turning " + (M.RightFirst ? "right" : "left") + " first"
                            : "a hub is never abandoned, tries again turning " + (M.RightFirst ? "right" : "left") +
                              " first"));
        }
    }

    // ---- what blocks the way ----

}
