using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
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
        static readonly List<ItemDrop> Near = new List<ItemDrop>();
        static readonly List<Character> Trolls = new List<Character>();
        static float _next;

        /// Plugin.Update. Expected outcome: how many items went into backpacks this time.
        internal static int Tick()
        {
            if (Time.time < _next || !ModSettings.On) return 0;
            _next = Time.time + 1f;
            Trolls.Clear();
            foreach (var c in Character.GetAllCharacters()) if (c != null && !c.IsDead() && c.IsTamed() && Taming.TrollTaming.IsTroll(c)) Trolls.Add(c);
            int n = 0;
            foreach (var t in Trolls) n += Collect(t);
            return n;
        }

        static int Collect(Character troll)
        {
            var view = troll.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner()) return 0;
            var store = BackpackStore.Store(troll);
            if (BackpackStore.Busy(store)) return 0;
            var all = InstancesField != null ? InstancesField.GetValue(null) as List<ItemDrop> : null;
            if (all == null) return 0;
            float r2 = ModSettings.PickupRange.Value * ModSettings.PickupRange.Value;
            var at = troll.transform.position;
            Near.Clear();
            foreach (var d in all)
            {
                if (d == null || d.m_itemData == null) continue;
                if ((d.transform.position - at).sqrMagnitude > r2) continue;
                if (!Allowed.Contains(Utils.GetPrefabName(d.gameObject))) continue;
                Near.Add(d);
            }
            int picked = 0;
            var zdo = view.GetZDO();
            foreach (var d in Near)
            {
                var nv = d.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                if (!nv.IsOwner()) { d.RequestOwn(); continue; }
                string what = d.m_itemData.m_shared.m_name + " x" + d.m_itemData.m_stack;
                int chest = BackpackStore.TryAdd(zdo, d.m_itemData, store);
                if (chest < 0) { ModLog.Verbose("BackpackPickup: " + troll.name + " backpack has no room for " + what); break; }
                ZNetScene.instance.Destroy(d.gameObject);
                picked++;
                ModLog.Verbose("BackpackPickup: " + troll.name + " picked up " + what + " into chest " + (chest + 1));
            }
            return picked;
        }
    }
}
