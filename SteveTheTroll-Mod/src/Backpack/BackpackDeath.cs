using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Backpack
{
    /// <summary>
    /// T-008c (D-062, user spec): when a tamed troll dies, everything in its backpack drops in the game's cargo crates (CargoCrate, as from
    /// a sunk ship) next to it - as many crates as needed; without that prefab the items drop on the ground. Runs on the troll's owner.
    /// </summary>
    [HarmonyPatch(typeof(Tameable), "OnDeath")]
    internal static class BackpackDeath
    {
        const string CratePrefab = "CargoCrate";

        static void Prefix(Tameable __instance)
        {
            var troll = __instance != null ? __instance.GetComponent<Character>() : null;
            if (troll == null || !troll.IsTamed() || !Taming.TrollTaming.IsTroll(troll)) return;
            var view = troll.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            var zdo = view.GetZDO();
            var items = new List<ItemDrop.ItemData>();
            for (int i = 0; i < BackpackStore.ChestCount; i++)
            {
                var inv = BackpackStore.Read(zdo, i);
                items.AddRange(inv.GetAllItems());
                BackpackStore.Write(zdo, i, new Inventory("", null, 1, 1));
            }
            if (items.Count == 0) { ModLog.Verbose("BackpackDeath: " + Taming.TrollTaming.Tag(troll) + " died with an empty backpack"); return; }
            int crates = Drop(items, troll.transform.position + Vector3.up);
            ModLog.Info("BackpackDeath: " + Taming.TrollTaming.Tag(troll) + " died - " + items.Count + " stack(s) dropped in " + crates + " cargo crate(s)");
            if (Taming.TrollTaming.Owner(troll) == (Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : -1L))
                Ui.Notify.Show(Loc.T("stevethetroll_backpack_dropped", __instance.GetHoverName(), crates.ToString()), Ui.Notify.Kind.Death);
        }

        // Expected outcome: the number of crates used (0 = dropped on the ground).
        static int Drop(List<ItemDrop.ItemData> items, Vector3 at)
        {
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(CratePrefab) : null;
            if (prefab == null)
            {
                ModLog.Warn("BackpackDeath: no '" + CratePrefab + "' prefab - the items drop on the ground");
                foreach (var it in items) ItemDrop.DropItem(it, it.m_stack, at + Random.insideUnitSphere, Quaternion.identity);
                return 0;
            }
            int crates = 0;
            Inventory inv = null;
            foreach (var it in items)
            {
                if (inv == null || !inv.CanAddItem(it))
                {
                    var go = Object.Instantiate(prefab, at + new Vector3(crates * 1.2f, 0.5f, 0f), Quaternion.identity);
                    var c = go.GetComponent<Container>();
                    if (c == null) { ModLog.Warn("BackpackDeath: '" + CratePrefab + "' has no Container"); return crates; }
                    inv = c.GetInventory();
                    crates++;
                }
                inv.AddItem(it);
            }
            return crates;
        }
    }
}
