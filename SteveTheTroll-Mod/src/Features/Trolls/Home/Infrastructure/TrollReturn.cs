using System.Collections.Generic;
using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Application;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Core;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Orders.Commands;
using SteveTheTroll.Features.Trolls.Recovery.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Home.Infrastructure;

/// <summary>
///     D-162 (user 2026-10-31): a tamed troll that ends up further than 1000 m from its hub with no follow order (the
///     player let it go far away) walks back to the hub until it is
///     inside the 950 m farm zone, then farms again. Only two things stop the walk: the troll or the player (in range) is
///     attacked - the combat / rage code drives (at the 50 %
///     speed of HubSpeed when the hub is out of reach) and the walk goes on afterwards - and a new follow order. The
///     decisions are HubRules.ShouldReturn / ReturnNext.
/// </summary>
internal sealed class TrollReturn : MonoBehaviour
{
    private const float WaypointMetres = 30f;
    private static readonly Dictionary<int, float> Said = new();

    /// Called once a second for each own troll (TrollIdle). Expected outcome: true = the troll walks back (already, or it starts now) and must not be given another job.
    private static readonly HubReturnPipeline Returns = new();

    /// One AI step (MonsterAI.UpdateAI prefix). Expected outcome: true = the walk drove the troll this frame (the game's AI is skipped).
    // D-175 (Architecture V2, P3 leftover "TrollReturn still owns its walking"): the walk goes through the troll's movement capability (TrollContext), built once
    private TrollContext _ctx;

    private HubInfo _hub;
    private float _nextLog, _start;

    /// The hub the troll should walk back to: the one chosen by hand if it still exists, else its owner's nearest (any distance). `distance` = flat metres (-1 = none).
    internal static HubInfo Target(Character c, out float distance)
    {
        distance = -1f;
        ZDO z = TrollTaming.Zdo(c);
        if (z == null || !ModSettings.HubsOn)
        {
            return null;
        }

        long owner = TrollTaming.Owner(c);
        if (owner == 0L)
        {
            return null;
        }

        HubInfo h = null;
        int manual = z.GetInt(HubRegistry.AssignKey);
        if (manual > 0)
        {
            h = HubRegistry.Find(owner, manual);
        }

        if (h == null)
        {
            h = HubRegistry.Nearest(owner, c.transform.position, out distance);
        }

        if (h != null)
        {
            distance = HubGeometry.Flat(h.Pos - c.transform.position);
        }

        return h;
    }

    internal static bool Check(Character c, MonsterAI ai)
    {
        if (c == null || ai == null)
        {
            return false;
        }

        TrollReturn running = c.GetComponent<TrollReturn>();
        float d;
        HubInfo hub = Target(c, out d);
        if (running != null)
        {
            running._hub = hub;
            return true;
        }

        // D-173 (Architecture V2, P7): HubReturnPipeline.Start decides (owner, then HubRules.ShouldStartReturn); the cheap distance test first keeps the component reads off most trolls
        if (!HubRules.ShouldReturn(hub != null, d, false))
        {
            return false;
        }

        TrollFarmer farmer = c.GetComponent<TrollFarmer>();
        ZNetView view = c.GetComponent<ZNetView>();
        OperationResponse<bool> start = Returns.Start(new HubReturnInput
        {
            TrollOwner = view != null && view.IsValid() && view.IsOwner(), HasHub = hub != null, Distance = d,
            FollowOrder = ai.GetFollowTarget() != null, Arriving = c.GetComponent<TrollArrival>() != null,
            OwnerWears = TrollHome.OwnerWears(c), Fighting = ai.GetTargetCreature() != null || c.GetComponent<TrollRage>() != null,
            Unloading = farmer != null && farmer.Unloading
        });
        if (!start.IsSuccess || !start.Value)
        {
            return false;
        }

        if (farmer != null)
        {
            Destroy(farmer);
        }

        TrollReturn r = c.gameObject.AddComponent<TrollReturn>();
        r._hub = hub;
        r._start = Time.time;
        ai.SetPatrolPoint();
        ModLog.Info("TrollReturn: " + TrollTaming.Tag(c) + " is " + d.ToString("F0") + " m from " + hub.Name +
                    " (more than " + HubRules.AutoReach.ToString("0") +
                    " m) and has no follow order - walks back to the " + HubRules.FarmLeash.ToString("0") +
                    " m farm zone");
        if (Player.m_localPlayer != null && TrollTaming.Owner(c) == Player.m_localPlayer.GetPlayerID())
        {
            Notify.Show(
                Loc.T("stevethetroll_hub_return", TrollTaming.ShownName(r), d.ToString("0"), hub.Name,
                    HubRules.FarmLeash.ToString("0")), Notify.Kind.Farm);
        }

        return true;
    }

    internal bool Step(MonsterAI ai, float dt)
    {
        Character c = GetComponent<Character>();
        if (c == null || c.IsDead())
        {
            Destroy(this);
            return false;
        }

        var rec = TrollRecovery.Of(this); // D-167: a running rage / detour drives first
        if (rec.Busy && rec.Continue(dt))
        {
            return true;
        }

        float d = _hub != null ? HubGeometry.Flat(_hub.Pos - transform.position) : -1f;
        bool fighting = ai.GetTargetCreature() != null || GetComponent<TrollRage>() != null ||
                        GetComponent<TrollChase>() != null || GetComponent<TrollSlam>() != null;
        ZNetView view = GetComponent<ZNetView>();
        // D-173 (Architecture V2, P7): HubReturnPipeline.Step decides (owner, hub, then HubRules.ReturnNext)
        OperationResponse<HubRules.ReturnStep> step = Returns.Step(new HubReturnInput
        {
            TrollOwner = view != null && view.IsValid() && view.IsOwner(), HasHub = _hub != null, Distance = d,
            FollowOrder = ai.GetFollowTarget() != null, Arriving = false, OwnerWears = true, Fighting = fighting, Unloading = false
        });
        if (step.Status == ResponseStatus.Rejected)
        {
            return false; // another client owns the troll: its AI runs there
        }

        if (!step.IsSuccess)
        {
            ModLog.Info("TrollReturn: " + TrollTaming.Tag(c) + " lost its hub - stops walking back");
            Destroy(this);
            return false;
        }

        switch (step.Value)
        {
            case HubRules.ReturnStep.Cancel:
                ModLog.Info("TrollReturn: " + TrollTaming.Tag(c) + " got a follow order - stops walking back");
                Destroy(this);
                return false;
            case HubRules.ReturnStep.Yield:
                return false;
            case HubRules.ReturnStep.Done:
                Arrived(c, ai, d);
                return true;
        }

        if (Time.time >= _nextLog)
        {
            _nextLog = Time.time + 30f;
            ModLog.Verbose("TrollReturn: " + TrollTaming.Tag(c) + " " + d.ToString("F0") + " m from " + _hub.Name +
                           ", walking back for " + (Time.time - _start).ToString("F0") + " s");
        }

        // A hub 1 km away is far outside the loaded area (the game's path search fails for it): walk to a point a short way ahead, a new one every step.
        Vector3 dir = _hub.Pos - transform.position;
        dir.y = 0f;
        Vector3 ahead = transform.position + dir.normalized * Mathf.Min(WaypointMetres, dir.magnitude);
        // D-167: the walk back had no stuck detection at all; a hub is never abandoned, so a failed cycle only starts the next one (turning the other way first)
        long ka, kb;
        TrollRecovery.KeyOfHub(_hub.Pos, out ka, out kb);
        if (rec.Tick(dt, _hub.Pos, ka, kb, false, true, null))
        {
            return true;
        }

        if (_ctx == null)
        {
            _ctx = new TrollContext(c);
        }

        ITrollMovement mv = _ctx.Capabilities.Movement;
        if (mv != null)
        {
            mv.MoveTo(dt, ahead, 0.5f, true);
        }

        return true;
    }

    private void Arrived(Character c, MonsterAI ai, float d)
    {
        ai.StopMoving();
        ai.SetPatrolPoint();
        {
            TrollRecovery rr = GetComponent<TrollRecovery>();
            if (rr != null)
            {
                rr.Reached();
            }
        }
        ModLog.Info("TrollReturn: " + TrollTaming.Tag(c) + " is back inside the farm zone of " + _hub.Name + " (" +
                    d.ToString("F0") + " m, " + (Time.time - _start).ToString("F0") + " s) - farms again");
        string name = _hub.Name;
        if (Player.m_localPlayer != null && TrollTaming.Owner(c) == Player.m_localPlayer.GetPlayerID())
        {
            Notify.Show(Loc.T("stevethetroll_hub_return_done", TrollTaming.ShownName(this), name), Notify.Kind.Farm);
        }

        ZDO z = TrollTaming.Zdo(c);
        string focus = z != null ? TrollFocus.Normalize(z.GetString(TrollRegistry.FocusKey)) : "";
        bool full = z != null && BackpackStore.IsFullCached(z);
        Destroy(this);
        if (full)
        {
            TrollOrderService.Send(new UnloadCommand(c));
        }
        else
        {
            TrollOrderService.Send(new FarmFocusCommand(c, focus, true));
        }
    }
}
