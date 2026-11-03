using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Backpack.Infrastructure;
using Kydl.Core.Results;
using SteveTheTroll.Hubs.Domain;
using SteveTheTroll.Hubs.Infrastructure;
using SteveTheTroll.Trolls.Contracts;
using SteveTheTroll.Trolls.Core;
using SteveTheTroll.Trolls.Farming.Application;
using SteveTheTroll.Trolls.Farming.Domain;
using SteveTheTroll.Trolls.Farming.Execution;
using SteveTheTroll.Trolls.Recovery.Infrastructure;
using SteveTheTroll.Trolls.Taming.Domain;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Trolls.Farming.Infrastructure
{
    /// <summary>
    /// The farming state of one tamed troll (owner client; T-008d, D-048..D-051). Search (D-051, user): quick check within QuickRange of the troll - the focus item first, then
    /// the other allowed items ("Farming now" changes); none -> FarmRange, then MaxRange, then the long-range search (D-163); nothing -> it waits and searches again (D-149).
    /// Hitting (D-051): a try = up to 2 swings; no damage -> step forward and try again, 4 tries, then give the source up (skipped BadSeconds). A follow order or a dead troll
    /// ends farming; a fight (target creature) hands the AI back for that frame.
    /// D-170 (Architecture V2, farming slice): the Infrastructure adapter. It reads the facts into a FarmGateInput, FarmingPipeline decides (owner check, then the priority order
    /// of FarmGateRules), FarmingExecutor performs (owner checked again); the approach (ApproachRules / ApproachMeter), the dry-swing ladder (SwingLadder) and the search order
    /// (TargetSelection) are game-free domain code. Movement goes through TrollContext's capability; reservations, aim, friendly-troll detour, spot memory and the home trip
    /// are helpers in this folder.
    /// </summary>
    internal sealed class TrollFarmer : MonoBehaviour
    {
        const float SearchSeconds = 3f, LogSeconds = 5f, LookAngle = 25f, MinAttackGap = 0.3f, BadSeconds = 120f, StepMetres = 2f, StepSeconds = 2.5f, FullCheckSeconds = 2f;
        const float TravelArriveMetres = 12f, TravelMaxSeconds = 420f, NothingWaitSeconds = 20f;
        internal const float FarWalkMetres = 80f;
        /// D-163: the remembered farm spot (ZDO key; FarmSpotMemory reads / writes it).
        internal const string SpotKey = FarmSpotMemory.SpotKey;
        /// D-150 (test run 014829: the game destroyed and re-created far trolls and the troll wandered for the 90 s of the idle wait): the ZDO remembers that this troll was farming.
        internal const string FarmingKey = "stt_farming";

        readonly Dictionary<GameObject, float> _bad = new Dictionary<GameObject, float>();
        readonly List<TrollFarming.Candidate> _cands = new List<TrollFarming.Candidate>();
        readonly SwingLadder _ladder = new SwingLadder();
        readonly ApproachMeter _approach = new ApproachMeter();
        readonly FarmFriendDetour _friend = new FarmFriendDetour();
        TrollContext _ctx;
        FarmingPipeline _pipeline;
        FarmingExecutor _executor;
        FarmHomeTrip _trip;
        string _focus = "";
        Vector3 _centre, _stepTo, _travelTo;
        GameObject _target;
        Collider _hit;
        float _nextSearch, _nextAttack, _nextLog, _stepUntil, _nextFullCheck, _travelStart, _nextNothingLog;
        int _attacks;
        bool _home, _announce, _travelling, _saidNothing, _toSpot;
        ItemDrop.ItemData _weapon;
        FarmTool _tool;
        Humanoid _me;
        MonsterAI _ai;
        float _dt, _aimDy;             // the frame being executed (the bound actions read it); height of the aim point above the troll (m)

        internal FarmSpotMemory Spots { get; private set; }
        internal ITrollMovement Movement { get { return _ctx.Capabilities.Movement; } }
        /// Full backpack: on its way to unload (D-062).
        internal bool IsFull { get; set; }
        /// Went farming on its own (D-058 / D-062: no "nothing found" / "home" messages).
        internal bool Auto { get; set; }
        /// Walking to a far source or back to the remembered spot (test hooks and the status text).
        internal bool Travelling { get { return _travelling; } }
        /// D-159: on its way to unload (hub / bed -> depot).
        internal bool Unloading { get { return _home && IsFull; } }
        internal string CurrentItem { get; private set; } = "";
        /// Swings started since this farmer exists (TestLab hook stt.farmAttacks).
        internal int Attacks { get { return _attacks; } }

        void Awake()
        {
            _ctx = new TrollContext(GetComponent<Character>());
            Spots = new FarmSpotMemory(this);
            _trip = new FarmHomeTrip(this);
            _tool = new FarmTool(this);
            _pipeline = new FarmingPipeline();
            // the executor owns the actions: bound once here (method groups), never per frame
            _executor = new FarmingExecutor(_ctx, new FarmingActions
            {
                Stop = OnStop, StopFollow = OnStopFollow, YieldToFight = OnYieldToFight, RecoveryDrives = OnRecovery, CheckFull = Full, GoHome = OnGoHome, NoWeapon = OnNoWeapon, Work = Work,
                Swing = OnSwing, StepForward = OnStepForward, GiveUp = OnGiveUpDry,
            });
            ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(this) + " set up (Architecture V2: farming pipeline + executor, capabilities complete " + _ctx.Capabilities.Complete + ")");
        }

        internal void StartTravel(Vector3 to, bool toRememberedSpot = false)
        {
            _travelling = true; _travelTo = to; _travelStart = Time.time; _toSpot = toRememberedSpot;
            if (toRememberedSpot) SetSpotWalk(true);      // D-175: survives the troll's object being re-created on the way (zone reload)
            _nextSearch = Time.time + SearchSeconds;      // the normal search runs every SearchSeconds on the way: the zone loads, the near sources show up
        }

        // D-175 (run 181331: walking back to its remembered spot the troll crossed into a reloaded zone, its object was re-created, the new TrollFarmer knew nothing of the
        // walk and farmed where it stood, 60 m short): the walk is flagged in the troll's ZDO (owner write); a new farm job with the flag set resumes the walk.
        internal const string SpotWalkKey = "stt_spot_walk";
        void SetSpotWalk(bool on)
        {
            var v = GetComponent<ZNetView>();
            if (v != null && v.IsValid() && v.IsOwner() && v.GetZDO().GetBool(SpotWalkKey, false) != on) v.GetZDO().Set(SpotWalkKey, on);
        }

        /// D-175: a walk back to the remembered spot was running when this farmer was made (the object was re-created): walk on. Expected outcome: true when it resumed.
        internal bool ResumeSpotWalk()
        {
            var v = GetComponent<ZNetView>();
            if (v == null || !v.IsValid() || !v.GetZDO().GetBool(SpotWalkKey, false)) return false;
            Vector3 spot;
            if (!Spots.Spot(out spot) || HubGeometry.Flat(spot - transform.position) <= TravelArriveMetres) { SetSpotWalk(false); return false; }
            StartTravel(spot, true);
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " walks on to its remembered spot " + spot.ToString("F0") + " (" + HubGeometry.Flat(spot - transform.position).ToString("F0") + " m, the walk survived a reload)");
            return true;
        }

        void EndTravel(bool failed)
        {
            if (_toSpot) SetSpotWalk(false);
            if (failed) Spots.GiveUp(_travelTo);
            _travelling = false;
            _nextSearch = failed ? Time.time + NothingWaitSeconds : 0f;
        }

        // Walking to a far source / back to the farm spot. Expected outcome: true = the walk drove the troll this frame.
        bool Travel(float dt)
        {
            var flat = _travelTo - transform.position; flat.y = 0f;
            float d = flat.magnitude;
            if (d <= TravelArriveMetres)
            {
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " arrived at the far farm place " + _travelTo.ToString("F0") + " (" + (Time.time - _travelStart).ToString("F0") + " s) - searches there");
                var rr = GetComponent<TrollRecovery>(); if (rr != null) rr.Reached();
                EndTravel(false);
                return d <= TravelArriveMetres;
            }
            if (Time.time - _travelStart > TravelMaxSeconds)
            {
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " does not get to " + _travelTo.ToString("F0") + " (" + d.ToString("F0") + " m left, walked too long) - gives that place up for " + FarmSpotMemory.FarBadSeconds.ToString("0") + " s");
                EndTravel(true);
                return !_travelling;
            }
            // D-167: stuck detection, rage and detours are TrollRecovery; a far place that fails two complete cycles is given up (spec 3: 120 s)
            long ka, kb;
            TrollRecovery.KeyOfPlace(_travelTo, out ka, out kb);
            var rec = TrollRecovery.Of(this);
            bool drove = rec.Tick(dt, _travelTo, ka, kb, true, true, null);
            if (drove) return drove;
            if (rec.TakeAbandon())
            {
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " does not get to " + _travelTo.ToString("F0") + " (" + d.ToString("F0") + " m left, two complete recovery cycles failed) - gives that place up for " + FarmSpotMemory.FarBadSeconds.ToString("0") + " s");
                EndTravel(true);
                return !_travelling;
            }
            Movement.MoveTo(dt, d > FarWalkMetres ? FarmHomeTrip.Waypoint(transform.position, _travelTo) : _travelTo, TravelArriveMetres * 0.5f, true);
            return _travelling;
        }

        /// `announce` (D-058): the troll starts on its own after waiting without orders - its owner gets a message with the item it found.
        internal void Setup(string focus, Vector3 centre, bool announce = false)
        {
            if (!announce) SetSpotWalk(false);      // D-175: an order (or ResumeAtSpot, which starts a new walk right after) ends an old walk to the spot
            _focus = focus ?? ""; _centre = centre; _target = null; _hit = null; _nextSearch = 0f; CurrentItem = ""; _home = false; _announce = announce; Auto = announce; _travelling = false;
            ResetTries();
            MarkFarming(true);
        }

        void MarkFarming(bool on)
        {
            var view = GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.IsOwner()) view.GetZDO().Set(FarmingKey, on);
        }

        /// D-084: after a fight - look for the nearest source again from where the troll stands now. Expected outcome: none (the next step searches).
        internal void Retarget()
        {
            _target = null; _hit = null; _nextSearch = 0f; _home = IsFull;
            ResetTries();
            _centre = transform.position;
        }

        /// D-143 (user 2026-10-31: after the logout Steve 1 ran around the base until he got an order instead of emptying his backpack): to the hub / bed and into the depot, the
        /// same way as a full backpack, then it is done (the next idle check sends it farming). Expected outcome: none.
        internal void SetupUnload()
        {
            Setup("", transform.position, true);
            MarkFarming(false);
            _home = true; IsFull = true;
        }

        /// T-008e: no farming, straight to the bed (silent).
        internal void SetupHome()
        {
            Setup("", transform.position, true);
            MarkFarming(false);
            _home = true;
        }

        /// D-065: the attack key named this source. Expected outcome: whether it became the target (it must be breakable for the troll).
        internal bool Force(GameObject source)
        {
            var col = source != null ? TrollFarming.Biggest(source) : null;
            if (col == null) { ModLog.Warn("TrollFarmer: " + TrollTaming.Tag(this) + " cannot farm " + (source != null ? source.name : "?") + " (no collider)"); return col != null; }
            string item = Utils.GetPrefabName(source);      // shown as is when it yields none of the known items
            foreach (var g in Ui.TrollFocus.DefaultOrder) if (TrollFarming.Yields(source, g.Item)) { item = g.Item; break; }
            _target = source; _hit = col; CurrentItem = item; _home = false; _travelling = false;
            Spots.Remember(source.transform.position);
            ResetTries();
            _nextSearch = Time.time + SearchSeconds;
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " ordered to break " + source.name + " (" + TrollFarming.KindOf(source) + ", " + item + ")");
            return _target == source;
        }

        /// One AI step. Expected outcome: returns whether farming drove the troll this frame (true = the game's AI is skipped: no idling, no feeding animation, D-048);
        /// false = fighting / done -> the game's AI runs.
        internal bool Step(MonsterAI ai, float dt)
        {
            _ai = ai; _dt = dt;
            _me = _ctx.Humanoid;
            var rec = GetComponent<TrollRecovery>();
            bool alive = _me != null && !_me.IsDead();
            bool home = _home, follow = alive && ai.GetFollowTarget() != null, fighting = alive && ai.GetTargetCreature() != null;
            // the weapon is read only when the gate can get that far (as before D-170: not while dead, following, fighting or going home)
            _weapon = alive && !home && !follow && !fighting ? _tool.For(_me, _target) : null;
            var input = new FarmGateInput(alive, _ctx.IsOwner, follow, _home && IsFull, fighting, rec != null && rec.Busy, Time.time >= _nextFullCheck && !IsFull, home, _weapon != null);
            // D-170: decide, then perform; RecoveryDrives / CheckFull that did not drive fall through to the next priority (as the old code did)
            for (int pass = 0; pass < 3; pass++)
            {
                var decision = _pipeline.Evaluate(input);
                if (!decision.IsSuccess) { ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(this) + " farming " + decision.Status + " (" + decision.Reason + ") - the game's AI runs"); return decision.IsSuccess; }
                var done = _executor.Execute(decision.Value);
                if (done.Status == ResponseStatus.Rejected) return done.IsSuccess;
                if (done.Value || !FarmGateRules.MayFallThrough(decision.Value)) return done.Value;
                input = input.Without(decision.Value);      // Full() that found the backpack not full changed nothing else
            }
            return _home;
        }

        // ---- the gate's actions (D-170: bound once in Awake) ----

        bool OnStop() { Destroy(this); return _me != null && !_me.IsDead(); }

        bool OnStopFollow()
        {
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " got a follow order - stops farming");
            Destroy(this);
            return _ai.GetFollowTarget() == null;
        }

        bool OnYieldToFight() { return _ai.GetTargetCreature() == null; }      // fighting (D-042 defend): the game's AI drives

        bool OnRecovery() { var rec = GetComponent<TrollRecovery>(); return rec != null && rec.Continue(_dt); }       // D-167: a running rage / detour of the shared recovery drives first

        bool OnGoHome() { return _trip.GoHome(_ai, _dt); }

        bool OnNoWeapon()
        {
            bool keep = _tool.Wait(_me);
            if (!keep) Destroy(this);
            return keep;
        }

        // Search / travel / walk / swing (the work part of the old Step).
        bool Work()
        {
            _tool.Found();
            float dt = _dt;
            if ((_target == null || _hit == null) && Time.time >= _nextSearch) Search();
            if (_home) return _home;
            if (_target == null || _hit == null) return _target != null || (_travelling && Travel(dt));

            FarmReservations.Hold(_target, Me);
            var point = FarmAim.Point(_target, ref _hit, transform.position);
            var flat = point - transform.position; flat.y = 0f;
            float d = flat.magnitude;
            bool tree = _target.GetComponent<TreeBase>() != null;
            float reach = tree ? ModSettings.FarmReach.Value : Mathf.Min(Mathf.Max(1.5f, _weapon.m_shared.m_aiAttackRange), ModSettings.FarmReach.Value + 1f);
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + LogSeconds;
                ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(this) + " -> " + _target.name + " (" + TrollFarming.KindOf(_target) + ", " + CurrentItem + ") " + d.ToString("F1") + " m, reach " + reach.ToString("F1")
                    + " m, height diff " + (point.y - transform.position.y).ToString("F1") + " m, " + _ladder + ", attacks " + _attacks + ", stuck " + _approach.StuckSeconds.ToString("F1") + " s");
            }
            if (d > reach && Time.time >= _stepUntil) _approach.Update(d, dt);
            var how = ApproachRules.Decide(Time.time < _stepUntil, d, reach, _approach.StuckSeconds);
            if (how == ApproachAction.Stepping) { Movement.MoveTo(dt, _stepTo, 0.3f, false); return how == ApproachAction.Stepping; }       // a step forward after a dry try (D-051)
            var rec = GetComponent<TrollRecovery>();
            if (how == ApproachAction.Walk)
            {
                // D-167 (user spec): stuck detection, rage, detours and the 120 s blacklist per troll + resource are TrollRecovery (replaces the 8 s give-up of D-063)
                long ka, kb;
                TrollRecovery.KeyOf(_target, out ka, out kb);
                rec = TrollRecovery.Of(this);
                if (rec.Tick(dt, point, ka, kb, true, true, _target)) return how == ApproachAction.Walk;
                if (rec.TakeAbandon()) { GiveUp("cannot reach it (" + d.ToString("F1") + " m, two complete recovery cycles failed)"); return _target == null; }
                Vector3 around;
                if (_friend.Check(gameObject, point, _target.name, out around)) { Movement.MoveTo(dt, around, 0.6f, false); return how == ApproachAction.Walk; }     // D-151: around it, not into it
                Movement.MoveTo(dt, point, reach * 0.7f, false);
                return how == ApproachAction.Walk;
            }
            // D-062 (TEST-026): right at it and not getting closer for 0.5 s -> swings from here
            _approach.Reset();
            if (rec != null) rec.Reached();          // D-167: at the resource: its failure counter starts again
            Movement.Stop();
            Movement.LookAt(point);
            if (_ctx.Capabilities.Combat.InAttack || Time.time < _nextAttack || !Movement.IsLookingAt(point, LookAngle)) return how == ApproachAction.AtTarget;
            _stepTo = transform.position + flat.normalized * StepMetres;
            _aimDy = point.y - transform.position.y;
            var swing = _executor.Execute(_ladder.Next(Revision()));
            return swing.IsSuccess ? swing.Value : swing.IsSuccess;
        }

        // ---- the swing ladder's actions ----

        bool OnSwing()
        {
            if (!_me.IsItemEquiped(_weapon)) _me.EquipItem(_weapon, false);
            if (_me.StartAttack(null, false))
            {
                _attacks++; _ladder.Swung();
                _nextAttack = Time.time + MinAttackGap;       // D-048: keep swinging
            }
            return _target != null;
        }

        bool OnStepForward()
        {
            _stepUntil = Time.time + StepSeconds;
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " no damage on " + _target.name + " - steps " + StepMetres + " m forward, " + _ladder);
            return _stepUntil > Time.time;
        }

        bool OnGiveUpDry()
        {
            GiveUp(SwingLadder.Tries + " tries of " + SwingLadder.SwingsPerTry + " swings without damage (height diff " + _aimDy.ToString("F1") + " m)");
            return _target == null;
        }

        uint Revision()
        {
            var v = _target != null ? _target.GetComponent<ZNetView>() : null;
            var z = v != null && v.IsValid() ? v.GetZDO() : null;
            return z != null ? z.DataRevision : 0u;
        }

        void ResetTries() { _ladder.Reset(); _approach.Reset(); _stepUntil = 0f; }

        void GiveUp(string why)
        {
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " gives up " + (_target != null ? _target.name : "?") + ": " + why + " - tries another source");
            if (_target != null) _bad[_target] = Time.time + BadSeconds;
            Release();
            _target = null; _hit = null;
            ResetTries();
        }

        bool IsBad(GameObject g)
        {
            float until;
            if (g != null && !IdleRules.Reachable(g.transform.position.y - transform.position.y)) return g != null;      // D-149: not across cliffs
            if (g != null && FarmReservations.HeldByOther(g, Me)) return g != null;                                     // D-151: another troll already works on it
            if (g != null && TrollRecovery.IsBlacklisted(GetComponent<Character>(), g)) return g != null;               // D-167: two failed recovery cycles for this troll + resource: 120 s
            return g != null && _bad.TryGetValue(g, out until) && Time.time < until;
        }

        int Me { get { return GetInstanceID(); } }

        void Release() { FarmReservations.Release(_target, Me); }

        // D-051: focus first, then the other allowed items, within QuickRange -> FarmRange -> MaxRange of the troll (TargetSelection decides the order); then the long range.
        void Search()
        {
            _nextSearch = Time.time + SearchSeconds;
            if (_travelling && !TargetRules.NearSearchWhileTravelling(_toSpot, HubGeometry.Flat(_travelTo - transform.position))) return;      // D-174: on the way to the remembered spot
            ResetTries();
            int tier = TamingRules.TamedToolTier;      // D-059: tamed trolls break any tree / rock (TrollFarmHits)
            var goals = Ui.TrollFocus.SearchOrder(_focus);         // D-058: focus item first, then the default priority
            TrollFarming.Gather(transform.position, ModSettings.FarmMaxRange.Value, _cands);   // D-084: one pass over the scene per search
            // D-159: a troll with a hub does not farm further than HubRules.FarmLeash (950 m) from it
            var hub = HubRegistry.For(GetComponent<Character>());
            System.Func<GameObject, bool> skip = hub == null ? (System.Func<GameObject, bool>)IsBad : (g => IsBad(g) || !HubRules.FarmAllowed(true, HubGeometry.Flat(g.transform.position - hub.Pos)));
            var ranges = new[] { ModSettings.FarmQuickRange.Value, ModSettings.FarmRange.Value, ModSettings.FarmMaxRange.Value };
            var hit = TargetSelection.FirstHit(ranges, goals, (range, goal) =>
            {
                _target = TrollFarming.FindIn(_cands, transform.position, transform.position, goal, tier, range, out _hit, skip);
                return _target != null;
            });
            if (hit.IsSuccess)
            {
                string item = goals[hit.Value.GoalIndex].Item;
                float range = ranges[hit.Value.RangeIndex];
                string was = CurrentItem;
                CurrentItem = item;
                if (_travelling && _toSpot) SetSpotWalk(false);
                _travelling = false;
                Spots.Remember(_target.transform.position);
                FarmReservations.Hold(_target, Me);
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " farms " + item + (_focus.Length > 0 && item != _focus ? " (no " + _focus + " found, farming something else)" : "") + " from "
                    + _target.name + " (" + TrollFarming.KindOf(_target) + ") within " + range + " m" + (was != item && was.Length > 0 ? ", was " + was : ""));
                if (_announce && IsMine()) Ui.Notify.Show(Loc.T("stevethetroll_farm_auto", Shown(), Loc.T(Ui.TrollFocus.KeyOf(item))), Ui.Notify.Kind.Farm);
                _announce = false;
                return;
            }
            ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(this) + " near search: " + hit.Reason);
            // User 2026-10-31: nothing within FarmMaxRange -> the long-range search (FarmLongRange, 500 m) walks to the nearest source the game knows of; on the way the near search
            // above runs again every SearchSeconds (the zone loads around the troll) and takes over as soon as a source is in range.
            if (_travelling) { _target = null; _hit = null; return; }
            if (SearchFar(tier, goals, hub)) return;
            CurrentItem = "";
            _announce = false;
            _nextSearch = Time.time + NothingWaitSeconds;
            // D-149 (user: "when no suitable resource exists, wait and search again" - not home, and not to the depot): stays where it is
            if (Time.time >= _nextNothingLog)
            {
                _nextNothingLog = Time.time + 60f;
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " finds nothing to farm within " + ModSettings.FarmMaxRange.Value + " m - waits here and searches again in " + NothingWaitSeconds.ToString("0") + " s");
            }
            if (IsMine() && !Auto && !_saidNothing)      // D-062: a troll that went farming on its own does not report "nothing found" every search; an ordered one says it once
            {
                _saidNothing = true;
                Ui.Notify.Show(Loc.T("stevethetroll_farm_nothing", Shown(), Mathf.Max(ModSettings.FarmMaxRange.Value, ModSettings.FarmLongRange.Value).ToString("0")), Ui.Notify.Kind.Farm);
            }
        }

        // D-163: the long-range search. Expected outcome: true = the troll now walks to a far source.
        bool SearchFar(int tier, IList<Ui.TrollFocus.Goal> goals, HubInfo hub)
        {
            float longRange = ModSettings.FarmLongRange.Value;
            if (longRange <= ModSettings.FarmMaxRange.Value) return longRange > ModSettings.FarmMaxRange.Value;
            Vector3 farPos; string farItem;
            bool found = Spots.FindFar(transform.position, tier, goals, hub, out farPos, out farItem);
            if (!found) return found;
            CurrentItem = farItem;
            var away = farPos - transform.position; away.y = 0f;
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " finds nothing within " + ModSettings.FarmMaxRange.Value + " m - walks " + away.magnitude.ToString("F0") + " m to a " + farItem + " source at " + farPos.ToString("F0"));
            if (_announce && IsMine()) Ui.Notify.Show(Loc.T("stevethetroll_farm_auto", Shown(), Loc.T(Ui.TrollFocus.KeyOf(farItem))), Ui.Notify.Kind.Farm);
            _announce = false;
            StartTravel(farPos);
            return _travelling;
        }

        // D-062: a full backpack ends farming - the troll walks to the owner's bed (T-008e). Expected outcome: true = it is full.
        bool Full()
        {
            _nextFullCheck = Time.time + FullCheckSeconds;
            var view = GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !BackpackStore.IsFull(view.GetZDO())) return IsFull;
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " backpack is full - goes to " + HubRegistry.DestinationName(GetComponent<Character>()));
            _home = true; Auto = true; IsFull = true; _target = null; _hit = null; CurrentItem = ""; _travelling = false;
            if (IsMine()) Ui.Notify.Show(Loc.T("stevethetroll_backpack_full", Shown(), HubRegistry.DestinationName(GetComponent<Character>())), Ui.Notify.Kind.Farm);
            return IsFull;
        }

        internal bool IsMine() { return TrollTaming.Owner(GetComponent<Character>()) == (Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : -1L); }

        internal string Shown() { return TrollTaming.ShownName(this); }

        /// The farmer removes itself (the game's AI runs again).
        internal void Remove() { Destroy(this); }

        void OnDestroy() { CurrentItem = ""; Release(); var rc = GetComponent<TrollRecovery>(); if (rc != null) rc.Forget(); }
    }
}
