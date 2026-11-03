using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Backpack.Application;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Backpack.Execution;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Backpack.Infrastructure;

/// <summary>
///     T-008c (D-062, user spec): every second, on the client that owns the troll, items lying within PickupRange (50 m)
///     of a tamed troll go
///     into its backpack when they are wood, ores, scrap iron or black metal scrap (TrollFocus.PickupItems). Not while one
///     of its chests is open. No room in
///     any chest -> the backpack is full (TrollFarmer walks home, D-062).
///     D-173 (Architecture V2, P6): this class reads the game (trolls, drops, budget) and acts; PickupPipeline decides per
///     drop, PickupExecutor performs (troll-owner gated).
/// </summary>
internal static class BackpackPickup
{
    // D-142 (test run 235053: BackpackPickup took 64 - 102 ms in ONE frame, every few seconds): it walked every dropped item in the world once per troll, built each item's
    // prefab name as a string, and every pickup read and rewrote the troll's chest data. Now: one pass over the drops for all trolls, the prefab check cached per item,
    // at most BudgetMs of work per tick (the rest follows a second later; the trolls take turns).
    private const double BudgetMs = 6.0;
    private static readonly HashSet<string> Allowed = new(TrollFocus.PickupItems);

    private static readonly FieldInfo
        InstancesField =
            AccessTools.Field(typeof(ItemDrop), "s_instances"); // static: FieldInfo, not a FieldRef (MISTAKES)

    private static readonly List<Character> Trolls = new();
    private static readonly List<Job> Jobs = new();

    private static readonly Dictionary<int, bool>
        AllowedDrop = new(); // per ItemDrop instance: is its prefab one of Allowed (the name needs string work)

    private static readonly Stopwatch Clock = new();
    private static float _next;
    private static int _rotate;

    // D-173: built once; the executor's delegates act on the drop / troll being worked on (no closure per item)
    private static readonly PickupPipeline Pipeline = new();
    private static readonly PickupExecutor Executor = new(RequestCurrent, TakeCurrent);
    private static readonly ViewOwnership TrollOwner = new();
    private static ItemDrop _drop;
    private static ZDO _zdo;
    private static Container _store;
    private static int _chest;

    private static void RequestCurrent()
    {
        if (_drop != null)
        {
            _drop.RequestOwn();
        }
    }

    private static bool TakeCurrent()
    {
        _chest = BackpackStore.TryAdd(_zdo, _drop.m_itemData, _store);
        if (_chest < 0)
        {
            return _chest >= 0;
        }

        ZNetScene.instance.Destroy(_drop.gameObject);
        return _chest >= 0;
    }

    /// Plugin.Update. Expected outcome: how many items went into backpacks this time.
    internal static int Tick()
    {
        if (Time.time < _next || !ModSettings.On)
        {
            return 0;
        }

        _next = Time.time + 1f;
        Clock.Reset();
        Clock.Start();
        Trolls.Clear();
        Jobs.Clear();
        foreach (Character c in Character.GetAllCharacters())
        {
            if (c != null && !c.IsDead() && c.IsTamed() && TrollTaming.IsTroll(c))
            {
                Trolls.Add(c);
            }
        }

        foreach (Character t in Trolls)
        {
            ZNetView view = t.GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                continue;
            }

            // D-173: the troll-level checks are the pipeline's validation (owner / chest open / full); a drop-less probe answers whether the troll collects at all
            OperationResponse<PickupAction> probe = Pipeline.Evaluate(new PickupInput(view.IsOwner(),
                view.IsOwner() && BackpackStore.Busy(BackpackStore.Store(t)),
                view.IsOwner() && BackpackStore.IsFullCached(view.GetZDO()), false, false, false));
            if (probe.Status != ResponseStatus.Success)
            {
                continue; // D-144: a full backpack takes nothing
            }

            Jobs.Add(new Job { Troll = t, At = t.transform.position });
        }

        if (Jobs.Count == 0)
        {
            return 0;
        }

        List<ItemDrop> all = InstancesField != null ? InstancesField.GetValue(null) as List<ItemDrop> : null;
        if (all == null)
        {
            return 0;
        }

        float r2 = ModSettings.PickupRange.Value * ModSettings.PickupRange.Value;
        if (AllowedDrop.Count > 6000)
        {
            AllowedDrop.Clear();
        }

        foreach (ItemDrop d in all)
        {
            if (d == null || d.m_itemData == null)
            {
                continue;
            }

            Vector3 pos = d.transform.position;
            bool? ok = null;
            foreach (Job j in Jobs)
            {
                if (!BackpackRules.InRange((pos - j.At).sqrMagnitude, r2))
                {
                    continue;
                }

                if (ok == null)
                {
                    ok = IsAllowed(d);
                }

                if (!ok.Value)
                {
                    break;
                }

                j.Near.Add(d);
            }
        }

        int n = 0;
        for (int k = 0; k < Jobs.Count; k++)
        {
            Job j = Jobs[(k + _rotate) % Jobs.Count];
            if (Clock.Elapsed.TotalMilliseconds >= BudgetMs)
            {
                break;
            }

            n += Collect(j);
        }

        _rotate++;
        return n;
    }

    private static bool IsAllowed(ItemDrop d)
    {
        bool ok;
        int id = d.GetInstanceID();
        if (AllowedDrop.TryGetValue(id, out ok))
        {
            return ok;
        }

        ok = Allowed.Contains(Utils.GetPrefabName(d.gameObject));
        AllowedDrop[id] = ok;
        return ok;
    }

    private static int Collect(Job job)
    {
        Character troll = job.Troll;
        if (troll == null)
        {
            return 0;
        }

        ZNetView view = troll.GetComponent<ZNetView>();
        TrollOwner.View = view;
        _store = BackpackStore.Store(troll);
        _zdo = view != null && view.IsValid() ? view.GetZDO() : null;
        int picked = 0;
        foreach (ItemDrop d in job.Near)
        {
            ZNetView nv = d != null ? d.GetComponent<ZNetView>() : null;
            bool valid = nv != null && nv.IsValid();
            var input = new PickupInput(TrollOwner.IsOwner, BackpackStore.Busy(_store), false,
                Clock.Elapsed.TotalMilliseconds >= BudgetMs, valid, valid && nv.IsOwner());
            OperationResponse<PickupAction> decision = Pipeline.Evaluate(input);
            if (decision.Status != ResponseStatus.Success)
            {
                break; // not the owner any more / chest opened / budget used: the rest waits for the next tick
            }

            string what = valid ? d.m_itemData.m_shared.m_name + " x" + d.m_itemData.m_stack : "";
            _drop = d;
            OperationResponse<PickupAction> done = Executor.Execute(TrollOwner, decision.Value);
            _drop = null;
            if (decision.Value != PickupAction.Take)
            {
                continue;
            }

            if (!done.IsSuccess)
            {
                ModLog.Verbose("BackpackPickup: " + TrollTaming.Tag(troll) + " backpack has no room for " + what);
                break;
            }

            picked++;
            ModLog.Verbose("BackpackPickup: " + TrollTaming.Tag(troll) + " picked up " + what + " into chest " +
                           (_chest + 1));
        }

        TrollOwner.View = null;
        _zdo = null;
        _store = null;
        return picked;
    }

    /// ITrollOwnership over the ZNetView currently worked on (reused - no allocation per troll).
    private sealed class ViewOwnership : ITrollOwnership
    {
        internal ZNetView View;
        public bool IsValid => View != null && View.IsValid();
        public bool IsOwner => IsValid && View.IsOwner();
    }

    private sealed class Job
    {
        internal readonly List<ItemDrop> Near = new();
        internal Vector3 At;
        internal Character Troll;
    }
}
