using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Farming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Backpack
{
    /// <summary>
    /// T-008c (D-062, user spec): every second, on the client that owns the troll, items lying within PickupRange (50 m) of a tamed troll go
    /// into its backpack when they are wood, ores, scrap iron or black metal scrap (TrollFocus.PickupItems). Not while one of its chests is open. No room in
    /// any chest -> the backpack is full (TrollFarmer walks home, D-062).
    /// </summary>
    internal static class BackpackPickup
    {
        static readonly HashSet<string> Allowed = new HashSet<string>(Ui.TrollFocus.PickupItems);
        static readonly System.Reflection.FieldInfo InstancesField = AccessTools.Field(typeof(ItemDrop), "s_instances");      // static: FieldInfo, not a FieldRef (MISTAKES)
        static readonly List<Character> Trolls = new List<Character>();
        static readonly List<Job> Jobs = new List<Job>();
        static readonly Dictionary<int, bool> AllowedDrop = new Dictionary<int, bool>();       // per ItemDrop instance: is its prefab one of Allowed (the name needs string work)
        static readonly System.Diagnostics.Stopwatch Clock = new System.Diagnostics.Stopwatch();
        static float _next;
        static int _rotate;

        // D-142 (test run 235053: BackpackPickup took 64 - 102 ms in ONE frame, every few seconds): it walked every dropped item in the world once per troll, built each item's
        // prefab name as a string, and every pickup read and rewrote the troll's chest data. Now: one pass over the drops for all trolls, the prefab check cached per item,
        // at most BudgetMs of work per tick (the rest follows a second later; the trolls take turns).
        const double BudgetMs = 6.0;

        sealed class Job
        {
            internal Character Troll;
            internal Vector3 At;
            internal readonly List<ItemDrop> Near = new List<ItemDrop>();
        }

        /// Plugin.Update. Expected outcome: how many items went into backpacks this time.
        internal static int Tick()
        {
            if (Time.time < _next || !ModSettings.On) return 0;
            _next = Time.time + 1f;
            Clock.Reset(); Clock.Start();
            Trolls.Clear(); Jobs.Clear();
            foreach (var c in Character.GetAllCharacters()) if (c != null && !c.IsDead() && c.IsTamed() && Taming.TrollTaming.IsTroll(c)) Trolls.Add(c);
            foreach (var t in Trolls)
            {
                var view = t.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner() || BackpackStore.Busy(BackpackStore.Store(t)) || BackpackStore.IsFullCached(view.GetZDO())) continue;       // D-144: a full backpack takes nothing
                Jobs.Add(new Job { Troll = t, At = t.transform.position });
            }
            if (Jobs.Count == 0) return 0;
            var all = InstancesField != null ? InstancesField.GetValue(null) as List<ItemDrop> : null;
            if (all == null) return 0;
            float r2 = ModSettings.PickupRange.Value * ModSettings.PickupRange.Value;
            if (AllowedDrop.Count > 6000) AllowedDrop.Clear();
            foreach (var d in all)
            {
                if (d == null || d.m_itemData == null) continue;
                var pos = d.transform.position;
                bool? ok = null;
                foreach (var j in Jobs)
                {
                    if ((pos - j.At).sqrMagnitude > r2) continue;
                    if (ok == null) ok = IsAllowed(d);
                    if (!ok.Value) break;
                    j.Near.Add(d);
                }
            }
            int n = 0;
            for (int k = 0; k < Jobs.Count; k++)
            {
                var j = Jobs[(k + _rotate) % Jobs.Count];
                if (Clock.Elapsed.TotalMilliseconds >= BudgetMs) break;
                n += Collect(j);
            }
            _rotate++;
            return n;
        }

        static bool IsAllowed(ItemDrop d)
        {
            bool ok;
            int id = d.GetInstanceID();
            if (AllowedDrop.TryGetValue(id, out ok)) return ok;
            ok = Allowed.Contains(Utils.GetPrefabName(d.gameObject));
            AllowedDrop[id] = ok;
            return ok;
        }

        static int Collect(Job job)
        {
            var troll = job.Troll;
            if (troll == null) return 0;
            var view = troll.GetComponent<ZNetView>();
            var store = BackpackStore.Store(troll);
            int picked = 0;
            var zdo = view.GetZDO();
            foreach (var d in job.Near)
            {
                if (d == null) continue;
                if (Clock.Elapsed.TotalMilliseconds >= BudgetMs) break;
                var nv = d.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                if (!nv.IsOwner()) { d.RequestOwn(); continue; }
                string what = d.m_itemData.m_shared.m_name + " x" + d.m_itemData.m_stack;
                int chest = BackpackStore.TryAdd(zdo, d.m_itemData, store);
                if (chest < 0) { ModLog.Verbose("BackpackPickup: " + Taming.TrollTaming.Tag(troll) + " backpack has no room for " + what); break; }
                ZNetScene.instance.Destroy(d.gameObject);
                picked++;
                ModLog.Verbose("BackpackPickup: " + Taming.TrollTaming.Tag(troll) + " picked up " + what + " into chest " + (chest + 1));
            }
            return picked;
        }
    }
}
