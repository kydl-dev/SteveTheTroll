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

/// <summary>Part 2 of TrollFarmer (split out so the class stays under the size limit).</summary>
internal sealed partial class TrollFarmer
{
    private bool OnNoWeapon()
    {
        bool keep = _tool.Wait(_me);
        if (!keep)
        {
            Destroy(this);
        }

        return keep;
    }

    // Search / travel / walk / swing (the work part of the old Step).
    private bool Work()
    {
        _tool.Found();
        float dt = _dt;
        if ((_target == null || _hit == null) && Time.time >= _nextSearch)
        {
            Search();
        }

        if (_home)
        {
            return _home;
        }

        if (_target == null || _hit == null)
        {
            return _target != null || (Travelling && Travel(dt));
        }

        FarmReservations.Hold(_target, Me);
        Vector3 point = FarmAim.Point(_target, ref _hit, transform.position);
        Vector3 flat = point - transform.position;
        flat.y = 0f;
        float d = flat.magnitude;
        bool tree = _target.GetComponent<TreeBase>() != null;
        float reach = tree
            ? ModSettings.FarmReach.Value
            : Mathf.Min(Mathf.Max(1.5f, _weapon.m_shared.m_aiAttackRange), ModSettings.FarmReach.Value + 1f);
        if (Time.time >= _nextLog)
        {
            _nextLog = Time.time + LogSeconds;
            ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(this) + " -> " + _target.name + " (" +
                           TrollFarming.KindOf(_target) + ", " + CurrentItem + ") " + d.ToString("F1") + " m, reach " +
                           reach.ToString("F1")
                           + " m, height diff " + (point.y - transform.position.y).ToString("F1") + " m, " + _ladder +
                           ", attacks " + Attacks + ", stuck " + _approach.StuckSeconds.ToString("F1") + " s");
        }

        if (d > reach && Time.time >= _stepUntil)
        {
            _approach.Update(d, dt);
        }

        ApproachAction how = ApproachRules.Decide(Time.time < _stepUntil, d, reach, _approach.StuckSeconds);
        if (how == ApproachAction.Stepping)
        {
            Movement.MoveTo(dt, _stepTo, 0.3f, false);
            return how == ApproachAction.Stepping;
        } // a step forward after a dry try (D-051)

        TrollRecovery rec = GetComponent<TrollRecovery>();
        if (how == ApproachAction.Walk)
        {
            // D-167 (user spec): stuck detection, rage, detours and the 120 s blacklist per troll + resource are TrollRecovery (replaces the 8 s give-up of D-063)
            long ka, kb;
            TrollRecovery.KeyOf(_target, out ka, out kb);
            rec = TrollRecovery.Of(this);
            if (rec.Tick(dt, point, ka, kb, true, true, _target))
            {
                return how == ApproachAction.Walk;
            }

            if (rec.TakeAbandon())
            {
                GiveUp("cannot reach it (" + d.ToString("F1") + " m, two complete recovery cycles failed)");
                return _target == null;
            }

            Vector3 around;
            if (_friend.Check(gameObject, point, _target.name, out around))
            {
                Movement.MoveTo(dt, around, 0.6f, false);
                return how == ApproachAction.Walk;
            } // D-151: around it, not into it

            Movement.MoveTo(dt, point, reach * 0.7f, false);
            return how == ApproachAction.Walk;
        }

        // D-062 (TEST-026): right at it and not getting closer for 0.5 s -> swings from here
        _approach.Reset();
        if (rec != null)
        {
            rec.Reached(); // D-167: at the resource: its failure counter starts again
        }

        Movement.Stop();
        Movement.LookAt(point);
        if (_ctx.Capabilities.Combat.InAttack || Time.time < _nextAttack || !Movement.IsLookingAt(point, LookAngle))
        {
            return how == ApproachAction.AtTarget;
        }

        _stepTo = transform.position + flat.normalized * StepMetres;
        _aimDy = point.y - transform.position.y;
        OperationResponse<bool> swing = _executor.Execute(_ladder.Next(Revision()));
        return swing.IsSuccess ? swing.Value : swing.IsSuccess;
    }

    // ---- the swing ladder's actions ----

    private bool OnSwing()
    {
        if (!_me.IsItemEquiped(_weapon))
        {
            _me.EquipItem(_weapon, false);
        }

        if (_me.StartAttack(null, false))
        {
            Attacks++;
            _ladder.Swung();
            _nextAttack = Time.time + MinAttackGap; // D-048: keep swinging
        }

        return _target != null;
    }

    private bool OnStepForward()
    {
        _stepUntil = Time.time + StepSeconds;
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " no damage on " + _target.name + " - steps " +
                    StepMetres + " m forward, " + _ladder);
        return _stepUntil > Time.time;
    }

    private bool OnGiveUpDry()
    {
        GiveUp(SwingLadder.Tries + " tries of " + SwingLadder.SwingsPerTry + " swings without damage (height diff " +
               _aimDy.ToString("F1") + " m)");
        return _target == null;
    }

    private uint Revision()
    {
        ZNetView v = _target != null ? _target.GetComponent<ZNetView>() : null;
        ZDO z = v != null && v.IsValid() ? v.GetZDO() : null;
        return z != null ? z.DataRevision : 0u;
    }

    private void ResetTries()
    {
        _ladder.Reset();
        _approach.Reset();
        _stepUntil = 0f;
    }

    private void GiveUp(string why)
    {
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " gives up " + (_target != null ? _target.name : "?") +
                    ": " + why + " - tries another source");
        if (_target != null)
        {
            _bad[_target] = Time.time + BadSeconds;
        }

        Release();
        _target = null;
        _hit = null;
        ResetTries();
    }

    private bool IsBad(GameObject g)
    {
        float until;
        if (g != null && !IdleRules.Reachable(g.transform.position.y - transform.position.y))
        {
            return g != null; // D-149: not across cliffs
        }

        if (g != null && FarmReservations.HeldByOther(g, Me))
        {
            return g != null; // D-151: another troll already works on it
        }

        if (g != null && TrollRecovery.IsBlacklisted(GetComponent<Character>(), g))
        {
            return g != null; // D-167: two failed recovery cycles for this troll + resource: 120 s
        }

        return g != null && _bad.TryGetValue(g, out until) && Time.time < until;
    }

    private void Release()
    {
        FarmReservations.Release(_target, Me);
    }

    // D-051: focus first, then the other allowed items, within QuickRange -> FarmRange -> MaxRange of the troll (TargetSelection decides the order); then the long range.
    private void Search()
    {
        _nextSearch = Time.time + SearchSeconds;
        if (Travelling &&
            !TargetRules.NearSearchWhileTravelling(_toSpot, HubGeometry.Flat(_travelTo - transform.position)))
        {
            return; // D-174: on the way to the remembered spot
        }

        ResetTries();
        int tier = TamingRules.TamedToolTier; // D-059: tamed trolls break any tree / rock (TrollFarmHits)
        List<TrollFocus.Goal>
            goals = TrollFocus.SearchOrder(_focus); // D-058: focus item first, then the default priority
        TrollFarming.Gather(transform.position, ModSettings.FarmMaxRange.Value,
            _cands); // D-084: one pass over the scene per search
        // D-159: a troll with a hub does not farm further than HubRules.FarmLeash (950 m) from it
        HubInfo hub = HubRegistry.For(GetComponent<Character>());
        Func<GameObject, bool> skip = hub == null
            ? (Func<GameObject, bool>)IsBad
            : g => IsBad(g) || !HubRules.FarmAllowed(true, HubGeometry.Flat(g.transform.position - hub.Pos));
        float[] ranges = new[]
            { ModSettings.FarmQuickRange.Value, ModSettings.FarmRange.Value, ModSettings.FarmMaxRange.Value };
        OperationResponse<SelectionHit> hit = TargetSelection.FirstHit(ranges, goals, (range, goal) =>
        {
            _target = TrollFarming.FindIn(_cands, new TrollFarming.SearchSphere(transform.position, range), transform.position, goal, tier, out _hit,
                skip);
            return _target != null;
        });
        if (hit.IsSuccess)
        {
            string item = goals[hit.Value.GoalIndex].Item;
            float range = ranges[hit.Value.RangeIndex];
            string was = CurrentItem;
            CurrentItem = item;
            if (Travelling && _toSpot)
            {
                SetSpotWalk(false);
            }

            Travelling = false;
            Spots.Remember(_target.transform.position);
            FarmReservations.Hold(_target, Me);
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " farms " + item +
                        (_focus.Length > 0 && item != _focus
                            ? " (no " + _focus + " found, farming something else)"
                            : "") + " from "
                        + _target.name + " (" + TrollFarming.KindOf(_target) + ") within " + range + " m" +
                        (was != item && was.Length > 0 ? ", was " + was : ""));
            if (_announce && IsMine())
            {
                Notify.Show(Loc.T("stevethetroll_farm_auto", Shown(), Loc.T(TrollFocus.KeyOf(item))), Notify.Kind.Farm);
            }

            _announce = false;
            return;
        }

        ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(this) + " near search: " + hit.Reason);
        // User 2026-10-31: nothing within FarmMaxRange -> the long-range search (FarmLongRange, 500 m) walks to the nearest source the game knows of; on the way the near search
        // above runs again every SearchSeconds (the zone loads around the troll) and takes over as soon as a source is in range.
        if (Travelling)
        {
            _target = null;
            _hit = null;
            return;
        }

        if (SearchFar(tier, goals, hub))
        {
            return;
        }

        CurrentItem = "";
        _announce = false;
        _nextSearch = Time.time + NothingWaitSeconds;
        // D-149 (user: "when no suitable resource exists, wait and search again" - not home, and not to the depot): stays where it is
        if (Time.time >= _nextNothingLog)
        {
            _nextNothingLog = Time.time + 60f;
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " finds nothing to farm within " +
                        ModSettings.FarmMaxRange.Value + " m - waits here and searches again in " +
                        NothingWaitSeconds.ToString("0") + " s");
        }

        if (IsMine() && !Auto &&
            !_saidNothing) // D-062: a troll that went farming on its own does not report "nothing found" every search; an ordered one says it once
        {
            _saidNothing = true;
            Notify.Show(
                Loc.T("stevethetroll_farm_nothing", Shown(),
                    Mathf.Max(ModSettings.FarmMaxRange.Value, ModSettings.FarmLongRange.Value).ToString("0")),
                Notify.Kind.Farm);
        }
    }

    // D-163: the long-range search. Expected outcome: true = the troll now walks to a far source.
    private bool SearchFar(int tier, IList<TrollFocus.Goal> goals, HubInfo hub)
    {
        float longRange = ModSettings.FarmLongRange.Value;
        if (longRange <= ModSettings.FarmMaxRange.Value)
        {
            return longRange > ModSettings.FarmMaxRange.Value;
        }

        Vector3 farPos;
        string farItem;
        bool found = Spots.FindFar(transform.position, tier, goals, hub, out farPos, out farItem);
        if (!found)
        {
            return found;
        }

        CurrentItem = farItem;
        Vector3 away = farPos - transform.position;
        away.y = 0f;
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " finds nothing within " +
                    ModSettings.FarmMaxRange.Value + " m - walks " + away.magnitude.ToString("F0") + " m to a " +
                    farItem + " source at " + farPos.ToString("F0"));
        if (_announce && IsMine())
        {
            Notify.Show(Loc.T("stevethetroll_farm_auto", Shown(), Loc.T(TrollFocus.KeyOf(farItem))), Notify.Kind.Farm);
        }

        _announce = false;
        StartTravel(farPos);
        return Travelling;
    }

    // D-062: a full backpack ends farming - the troll walks to the owner's bed (T-008e). Expected outcome: true = it is full.
    private bool Full()
    {
        _nextFullCheck = Time.time + FullCheckSeconds;
        ZNetView view = GetComponent<ZNetView>();
        if (view == null || !view.IsValid() || !BackpackStore.IsFull(view.GetZDO()))
        {
            return IsFull;
        }

        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " backpack is full - goes to " +
                    HubRegistry.DestinationName(GetComponent<Character>()));
        _home = true;
        Auto = true;
        IsFull = true;
        _target = null;
        _hit = null;
        CurrentItem = "";
        Travelling = false;
        if (IsMine())
        {
            Notify.Show(
                Loc.T("stevethetroll_backpack_full", Shown(), HubRegistry.DestinationName(GetComponent<Character>())),
                Notify.Kind.Farm);
        }

        return IsFull;
    }

    internal bool IsMine()
    {
        return TrollTaming.Owner(GetComponent<Character>()) ==
               (Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : -1L);
    }

    internal string Shown()
    {
        return TrollTaming.ShownName(this);
    }

    /// The farmer removes itself (the game's AI runs again).
    internal void Remove()
    {
        Destroy(this);
    }
}
