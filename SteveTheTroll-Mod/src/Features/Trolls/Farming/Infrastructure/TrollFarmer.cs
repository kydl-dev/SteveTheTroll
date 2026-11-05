using System;
using System.Collections.Generic;
using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Core;
using SteveTheTroll.Features.Trolls.Farming.Application;
using SteveTheTroll.Features.Trolls.Farming.Domain;
using SteveTheTroll.Features.Trolls.Farming.Execution;
using SteveTheTroll.Features.Trolls.Recovery.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Farming.Infrastructure;

/// <summary>
///     The farming state of one tamed troll (owner client; T-008d, D-048..D-051). Search (D-051, user): quick check within
///     QuickRange of the troll - the focus item first, then
///     the other allowed items ("Farming now" changes); none -> FarmRange, then MaxRange, then the long-range search
///     (D-163); nothing -> it waits and searches again (D-149).
///     Hitting (D-051): a try = up to 2 swings; no damage -> step forward and try again, 4 tries, then give the source up
///     (skipped BadSeconds). A follow order or a dead troll
///     ends farming; a fight (target creature) hands the AI back for that frame.
///     D-170 (Architecture V2, farming slice): the Infrastructure adapter. It reads the facts into a FarmGateInput,
///     FarmingPipeline decides (owner check, then the priority order
///     of FarmGateRules), FarmingExecutor performs (owner checked again); the approach (ApproachRules / ApproachMeter),
///     the dry-swing ladder (SwingLadder) and the search order
///     (TargetSelection) are game-free domain code. Movement goes through TrollContext's capability; reservations, aim,
///     friendly-troll detour, spot memory and the home trip
///     are helpers in this folder.
/// </summary>
internal sealed partial class TrollFarmer : MonoBehaviour
{
    private const float SearchSeconds = 3f,
        LogSeconds = 5f,
        LookAngle = 25f,
        MinAttackGap = 0.3f,
        BadSeconds = 120f,
        StepMetres = 2f,
        StepSeconds = 2.5f,
        FullCheckSeconds = 2f;

    private const float TravelArriveMetres = 12f, TravelMaxSeconds = 420f, NothingWaitSeconds = 20f;
    internal const float FarWalkMetres = 80f;

    /// D-163: the remembered farm spot (ZDO key; FarmSpotMemory reads / writes it).
    internal const string SpotKey = FarmSpotMemory.SpotKey;

    /// D-150 (test run 014829: the game destroyed and re-created far trolls and the troll wandered for the 90 s of the idle wait): the ZDO remembers that this troll was farming.
    internal const string FarmingKey = "stt_farming";

    // D-175 (run 181331: walking back to its remembered spot the troll crossed into a reloaded zone, its object was re-created, the new TrollFarmer knew nothing of the
    // walk and farmed where it stood, 60 m short): the walk is flagged in the troll's ZDO (owner write); a new farm job with the flag set resumes the walk.
    internal const string SpotWalkKey = "stt_spot_walk";
    private readonly ApproachMeter _approach = new();

    private readonly Dictionary<GameObject, float> _bad = new();
    private readonly List<TrollFarming.Candidate> _cands = new();
    private readonly FarmFriendDetour _friend = new();
    private readonly SwingLadder _ladder = new();
    private MonsterAI _ai;
    private Vector3 _centre, _stepTo, _travelTo;
    private TrollContext _ctx;

    private float
        _dt, _aimDy; // the frame being executed (the bound actions read it); height of the aim point above the troll (m)

    private FarmingExecutor _executor;
    private string _focus = "";
    private Collider _hit;
    private bool _home, _announce, _saidNothing, _toSpot;
    private Humanoid _me;
    private float _nextSearch, _nextAttack, _nextLog, _stepUntil, _nextFullCheck, _travelStart, _nextNothingLog;
    private FarmingPipeline _pipeline;
    private GameObject _target;
    private GameObject _ordered; // the source an order named (Force) - kept across fights (Retarget)

    /// The source this farmer works on now (null = none). D-177: read by the stt.farmersOn test hook.
    internal GameObject Target => _target;

    /// The farmer walks home / waits there (the home order or slippers off). D-180: read by the stt.goingHome test hook.
    internal bool GoingHome => _home;
    private FarmTool _tool;
    private FarmHomeTrip _trip;
    private ItemDrop.ItemData _weapon;

    internal FarmSpotMemory Spots { get; private set; }
    internal ITrollMovement Movement => _ctx.Capabilities.Movement;

    /// Full backpack: on its way to unload (D-062).
    internal bool IsFull { get; set; }

    /// Went farming on its own (D-058 / D-062: no "nothing found" / "home" messages).
    internal bool Auto { get; set; }

    /// Walking to a far source or back to the remembered spot (test hooks and the status text).
    internal bool Travelling { get; private set; }

    /// D-159: on its way to unload (hub / bed -> depot).
    internal bool Unloading => _home && IsFull;

    internal string CurrentItem { get; private set; } = "";

    /// Swings started since this farmer exists (TestLab hook stt.farmAttacks).
    internal int Attacks { get; private set; }

    private int Me => GetInstanceID();

    private void Awake()
    {
        _ctx = new TrollContext(GetComponent<Character>());
        Spots = new FarmSpotMemory(this);
        _trip = new FarmHomeTrip(this);
        _tool = new FarmTool(this);
        _pipeline = new FarmingPipeline();
        // the executor owns the actions: bound once here (method groups), never per frame
        _executor = new FarmingExecutor(_ctx, new FarmingActions
        {
            Stop = OnStop, StopFollow = OnStopFollow, YieldToFight = OnYieldToFight, RecoveryDrives = OnRecovery,
            CheckFull = Full, GoHome = OnGoHome, NoWeapon = OnNoWeapon, Work = Work,
            Swing = OnSwing, StepForward = OnStepForward, GiveUp = OnGiveUpDry
        });
        ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(this) +
                       " set up (Architecture V2: farming pipeline + executor, capabilities complete " +
                       _ctx.Capabilities.Complete + ")");
    }

    private void OnDestroy()
    {
        CurrentItem = "";
        Release();
        TrollRecovery rc = GetComponent<TrollRecovery>();
        if (rc != null)
        {
            rc.Forget();
        }
    }

    internal void StartTravel(Vector3 to, bool toRememberedSpot = false)
    {
        Travelling = true;
        _travelTo = to;
        _travelStart = Time.time;
        _toSpot = toRememberedSpot;
        if (toRememberedSpot)
        {
            SetSpotWalk(true); // D-175: survives the troll's object being re-created on the way (zone reload)
        }

        _nextSearch =
            Time.time + SearchSeconds; // the normal search runs every SearchSeconds on the way: the zone loads, the near sources show up
    }

    private void SetSpotWalk(bool on)
    {
        ZNetView v = GetComponent<ZNetView>();
        if (v != null && v.IsValid() && v.IsOwner() && v.GetZDO().GetBool(SpotWalkKey) != on)
        {
            v.GetZDO().Set(SpotWalkKey, on);
        }
    }

    /// D-175: a walk back to the remembered spot was running when this farmer was made (the object was re-created): walk on. Expected outcome: true when it resumed.
    internal bool ResumeSpotWalk()
    {
        ZNetView v = GetComponent<ZNetView>();
        if (v == null || !v.IsValid() || !v.GetZDO().GetBool(SpotWalkKey))
        {
            return false;
        }

        Vector3 spot;
        if (!Spots.Spot(out spot) || HubGeometry.Flat(spot - transform.position) <= TravelArriveMetres)
        {
            SetSpotWalk(false);
            return false;
        }

        StartTravel(spot, true);
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " walks on to its remembered spot " +
                    spot.ToString("F0") + " (" + HubGeometry.Flat(spot - transform.position).ToString("F0") +
                    " m, the walk survived a reload)");
        return true;
    }

    private void EndTravel(bool failed)
    {
        if (_toSpot)
        {
            SetSpotWalk(false);
        }

        if (failed)
        {
            Spots.GiveUp(_travelTo);
        }

        Travelling = false;
        _nextSearch = failed ? Time.time + NothingWaitSeconds : 0f;
    }

    // Walking to a far source / back to the farm spot. Expected outcome: true = the walk drove the troll this frame.
    private bool Travel(float dt)
    {
        Vector3 flat = _travelTo - transform.position;
        flat.y = 0f;
        float d = flat.magnitude;
        if (d <= TravelArriveMetres)
        {
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " arrived at the far farm place " +
                        _travelTo.ToString("F0") + " (" + (Time.time - _travelStart).ToString("F0") +
                        " s) - searches there");
            TrollRecovery rr = GetComponent<TrollRecovery>();
            if (rr != null)
            {
                rr.Reached();
            }

            EndTravel(false);
            return d <= TravelArriveMetres;
        }

        if (Time.time - _travelStart > TravelMaxSeconds)
        {
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " does not get to " + _travelTo.ToString("F0") +
                        " (" + d.ToString("F0") + " m left, walked too long) - gives that place up for " +
                        FarmSpotMemory.FarBadSeconds.ToString("0") + " s");
            EndTravel(true);
            return !Travelling;
        }

        // D-167: stuck detection, rage and detours are TrollRecovery; a far place that fails two complete cycles is given up (spec 3: 120 s)
        long ka, kb;
        TrollRecovery.KeyOfPlace(_travelTo, out ka, out kb);
        var rec = TrollRecovery.Of(this);
        bool drove = rec.Tick(dt, _travelTo, ka, kb, true, true, null);
        if (drove)
        {
            return drove;
        }

        if (rec.TakeAbandon())
        {
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " does not get to " + _travelTo.ToString("F0") +
                        " (" + d.ToString("F0") +
                        " m left, two complete recovery cycles failed) - gives that place up for " +
                        FarmSpotMemory.FarBadSeconds.ToString("0") + " s");
            EndTravel(true);
            return !Travelling;
        }

        Movement.MoveTo(dt, d > FarWalkMetres ? FarmHomeTrip.Waypoint(transform.position, _travelTo) : _travelTo,
            TravelArriveMetres * 0.5f, true);
        return Travelling;
    }

    /// `announce` (D-058): the troll starts on its own after waiting without orders - its owner gets a message with the item it found.
    internal void Setup(string focus, Vector3 centre, bool announce = false)
    {
        if (!announce)
        {
            SetSpotWalk(
                false); // D-175: an order (or ResumeAtSpot, which starts a new walk right after) ends an old walk to the spot
        }

        _focus = focus ?? "";
        _centre = centre;
        _target = null;
        _ordered = null; // a new order / setup ends the old ordered source
        _hit = null;
        _nextSearch = 0f;
        CurrentItem = "";
        _home = false;
        _announce = announce;
        Auto = announce;
        Travelling = false;
        ResetTries();
        MarkFarming(true);
    }

    private void MarkFarming(bool on)
    {
        ZNetView view = GetComponent<ZNetView>();
        if (view != null && view.IsValid() && view.IsOwner())
        {
            view.GetZDO().Set(FarmingKey, on);
        }
    }

    /// D-084: after a fight - look for the nearest source again from where the troll stands now.
    /// 2026-11-05 (gate run 1, recovery-walled-tree: a Greyling attacked the owner mid-order, the guard chase ended and the troll farmed random trees for 6 minutes):
    /// a troll that had been ORDERED to break a source goes back to it when it still exists and the backpack is not full. Expected outcome: true = back to the ordered source.
    internal bool Retarget()
    {
        GameObject ordered = _ordered;
        _target = null;
        _hit = null;
        _nextSearch = 0f;
        _home = IsFull;
        ResetTries();
        _centre = transform.position;
        return ordered != null && !IsFull && Force(ordered); // Unity null: a source broken meanwhile is gone -> the normal search
    }

    /// D-143 (user 2026-10-31: after the logout Steve 1 ran around the base until he got an order instead of emptying his backpack): to the hub / bed and into the depot, the
    /// same way as a full backpack, then it is done (the next idle check sends it farming). Expected outcome: none.
    internal void SetupUnload()
    {
        Setup("", transform.position, true);
        MarkFarming(false);
        _home = true;
        IsFull = true;
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
        Collider col = source != null ? TrollFarming.Biggest(source) : null;
        if (col == null)
        {
            ModLog.Warn("TrollFarmer: " + TrollTaming.Tag(this) + " cannot farm " +
                        (source != null ? source.name : "?") + " (no collider)");
            return col != null;
        }

        string item = Utils.GetPrefabName(source); // shown as is when it yields none of the known items
        foreach (TrollFocus.Goal g in TrollFocus.DefaultOrder)
        {
            if (TrollFarming.Yields(source, g.Item))
            {
                item = g.Item;
                break;
            }
        }

        _target = source;
        _ordered = source;
        _hit = col;
        CurrentItem = item;
        _home = false;
        Travelling = false;
        Spots.Remember(source.transform.position);
        ResetTries();
        _nextSearch = Time.time + SearchSeconds;
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " ordered to break " + source.name + " (" +
                    TrollFarming.KindOf(source) + ", " + item + ")");
        return _target == source;
    }

    /// One AI step. Expected outcome: returns whether farming drove the troll this frame (true = the game's AI is skipped: no idling, no feeding animation, D-048);
    /// false = fighting / done -> the game's AI runs.
    internal bool Step(MonsterAI ai, float dt)
    {
        _ai = ai;
        _dt = dt;
        _me = _ctx.Humanoid;
        TrollRecovery rec = GetComponent<TrollRecovery>();
        bool alive = _me != null && !_me.IsDead();
        bool home = _home,
            follow = alive && ai.GetFollowTarget() != null,
            fighting = alive && ai.GetTargetCreature() != null;
        // the weapon is read only when the gate can get that far (as before D-170: not while dead, following, fighting or going home)
        _weapon = alive && !home && !follow && !fighting ? _tool.For(_me, _target) : null;
        var input = new FarmGateInput
        {
            Alive = alive, IsOwner = _ctx.IsOwner, FollowOrder = follow, Unloading = _home && IsFull, Fighting = fighting,
            RecoveryBusy = rec != null && rec.Busy, FullCheckDue = Time.time >= _nextFullCheck && !IsFull, Home = home, HasWeapon = _weapon != null
        };
        // D-170: decide, then perform; RecoveryDrives / CheckFull that did not drive fall through to the next priority (as the old code did)
        for (int pass = 0; pass < 3; pass++)
        {
            OperationResponse<FarmAction> decision = _pipeline.Evaluate(input);
            if (!decision.IsSuccess)
            {
                ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(this) + " farming " + decision.Status + " (" +
                               decision.Reason + ") - the game's AI runs");
                return decision.IsSuccess;
            }

            OperationResponse<bool> done = _executor.Execute(decision.Value);
            if (done.Status == ResponseStatus.Rejected)
            {
                return done.IsSuccess;
            }

            if (done.Value || !FarmGateRules.MayFallThrough(decision.Value))
            {
                return done.Value;
            }

            input = input.Without(decision.Value); // Full() that found the backpack not full changed nothing else
        }

        return _home;
    }

    // ---- the gate's actions (D-170: bound once in Awake) ----

    private bool OnStop()
    {
        Destroy(this);
        return _me != null && !_me.IsDead();
    }

    private bool OnStopFollow()
    {
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " got a follow order - stops farming");
        Destroy(this);
        return _ai.GetFollowTarget() == null;
    }

    private bool OnYieldToFight()
    {
        return _ai.GetTargetCreature() == null;
    } // fighting (D-042 defend): the game's AI drives

    private bool OnRecovery()
    {
        TrollRecovery rec = GetComponent<TrollRecovery>();
        return rec != null && rec.Continue(_dt);
    } // D-167: a running rage / detour of the shared recovery drives first

    private bool OnGoHome()
    {
        return _trip.GoHome(_ai, _dt);
    }
}
