using System;
using System.Collections.Generic;
using HarmonyLib;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Orders.Commands;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Ui;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>Part 2 of TestHooks (split out so the class stays under the size limit).</summary>
internal static partial class TestHooks
{
    /// Quantity of `prefab` in every depot chest of this player (from the chests' ZDOs: works where the depot area is not loaded).
    private static int DepotQuantity(string prefab)
    {
        GameObject drop = ObjectDB.instance.GetItemPrefab(prefab);
        string shared = drop != null ? drop.GetComponent<ItemDrop>().m_itemData.m_shared.m_name : prefab;
        int n = 0, chests = 0, mine = 0, loaded = 0, fromZdo = 0;
        // loaded depot chests: their live inventory (what TrollDepot.Unload wrote, saved or not)
        var seen = new HashSet<ZDOID>();
        foreach (Container c in Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
        {
            ZNetView v = c != null ? c.GetComponent<ZNetView>() : null;
            if (v == null || !v.IsValid())
            {
                continue;
            }

            ZDO z = v.GetZDO();
            if (!z.GetBool(TrollDepot.FlagKey) || z.GetLong(TrollDepot.OwnerKey) != Me)
            {
                continue;
            }

            seen.Add(z.m_uid);
            loaded++;
            foreach (ItemDrop.ItemData it in c.GetInventory().GetAllItems())
            {
                if (it.m_shared.m_name == shared)
                {
                    n += it.m_stack;
                }
            }
        }

        // unloaded ones: the inventory the game saved in the chest's ZDO
        var list = new List<ZDO>();
        int idx = 0, guard = 0;
        while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(DepotData.ChestPrefab, list, ref idx) && guard++ < 10000)
        {
        }

        foreach (ZDO z in list)
        {
            chests++;
            if (!z.GetBool(TrollDepot.FlagKey) || z.GetLong(TrollDepot.OwnerKey) != Me)
            {
                continue;
            }

            mine++;
            if (seen.Contains(z.m_uid))
            {
                continue;
            }

            string data = z.GetString(ZDOVars.s_items);
            if (data.Length == 0)
            {
                continue;
            }

            var inv = new Inventory("depot", null, 8, 8);
            inv.Load(new ZPackage(data));
            fromZdo++;
            foreach (ItemDrop.ItemData it in inv.GetAllItems())
            {
                if (it.m_shared.m_name == shared)
                {
                    n += it.m_stack;
                }
            }
        }

        ModLog.Info("TestHooks: depot " + prefab + " " + n + " (chest ZDOs " + chests +
                    ", this player's depot chests " + mine + ", loaded " + loaded + ", read from ZDO " + fromZdo + ")");
        return n;
    }

    /// D-174 (review): the hub exists and is the troll's, and the troll stands `min` .. `max` flat metres from it - a missing hub fails the setup at once.
    [TestHook("stt.hubDistanceInRange")]
    private static bool HubDistanceInRange(GameObject troll, float min, float max)
    {
        HubRegistry.All(true);
        HubInfo hub = HubRegistry.For(troll.GetComponent<Character>());
        if (hub == null)
        {
            ModLog.Info("TestHooks: hubDistanceInRange - the troll has no hub");
            return hub != null;
        }

        float d = HubGeometry.Flat(hub.Pos - troll.transform.position);
        ModLog.Info("TestHooks: hubDistanceInRange - " + d.ToString("F0") + " m from " + hub.Name + " (want " +
                    min.ToString("F0") + " .. " + max.ToString("F0") + ")");
        return d >= min && d <= max;
    }

    /// D-174 (review: manual hub precedence): the owner sends the troll to hub `number` (0 = automatic), as the Troll page does.
    [TestHook("stt.assignHub")]
    private static int AssignHub(GameObject troll, int number)
    {
        ZDO z = TrollTaming.Zdo(troll.GetComponent<Character>());
        z.Set(HubRegistry.AssignKey, number);
        return z.GetInt(HubRegistry.AssignKey);
    }

    /// D-174: removes this player's test hub `number` (its ZDO). Expected outcome: how many were removed.
    [TestHook("stt.removeHub")]
    private static int RemoveHub(int number)
    {
        int n = 0;
        foreach (ZDO z in new List<ZDO>(FakeHubs))
        {
            if (z == null || !z.IsValid() || z.GetInt(HubRegistry.NumberKey) != number ||
                z.GetLong(HubRegistry.OwnerKey) != Me)
            {
                continue;
            }

            z.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(z);
            FakeHubs.Remove(z);
            n++;
        }

        HubRegistry.All(true);
        return n;
    }

    /// D-174 (review: far-search can pass on a nearer real tree): the long-range search finds THIS synthetic tree - every other source is skipped, so a hit is the ZDO-only
    /// tree `dx`,`dz` from the player. Expected outcome: its flat distance, -1 when the search did not find it.
    [TestHook("stt.farFindsSynthetic")]
    private static int FarFindsSynthetic(float dx, float dz)
    {
        Player p = Player.m_localPlayer;
        Vector3 at = p.transform.position + new Vector3(dx, 0f, dz);
        Vector3 pos;
        string item;
        bool found = TrollFarming.FindFar(p.transform.position, new TrollFarming.SearchRange(ModSettings.FarmLongRange.Value, ModSettings.FarmMaxRange.Value), 10,
            new[] { new TrollFocus.Goal("Wood") }, q => HubGeometry.Flat(q - at) > 3f, out pos, out item);
        int d = found ? Mathf.RoundToInt(HubGeometry.Flat(pos - p.transform.position)) : -1;
        ModLog.Info("TestHooks: far search for the synthetic tree at " + at.ToString("F0") + " - " +
                    (found ? "found " + item + " at " + d + " m" : "not found"));
        return d;
    }

    /// D-174: the elemental immunity of a tamed troll (TamedTrollStats): how many of fire / frost / lightning / poison / spirit are Immune. Expected outcome: 5.
    [TestHook("stt.elementalImmune")]
    private static int ElementalImmune(GameObject troll)
    {
        HitData.DamageModifiers m = troll.GetComponent<Character>().GetDamageModifiers();
        int n = 0;
        if (m.m_fire == HitData.DamageModifier.Immune)
        {
            n++;
        }

        if (m.m_frost == HitData.DamageModifier.Immune)
        {
            n++;
        }

        if (m.m_lightning == HitData.DamageModifier.Immune)
        {
            n++;
        }

        if (m.m_poison == HitData.DamageModifier.Immune)
        {
            n++;
        }

        if (m.m_spirit == HitData.DamageModifier.Immune)
        {
            n++;
        }

        return n;
    }

    /// D-174 (review: reset isolation): tamed trolls within `radius` m of the player right now (after stt.reset this must be 0).
    [TestHook("stt.tamedTrollsNear")]
    private static int TamedTrollsNear(float radius)
    {
        Player p = Player.m_localPlayer;
        int n = 0;
        foreach (Character c in Character.GetAllCharacters())
        {
            if (c != null && !c.IsDead() && c.IsTamed() && TrollTaming.IsTroll(c) &&
                HubGeometry.Flat(c.transform.position - p.transform.position) <= radius)
            {
                n++;
            }
        }

        return n;
    }

    /// D-174: the static tracking state a test can leave behind (followed id, marks, fill counts) - "" after stt.reset.
    [TestHook("stt.trackingState")]
    private static string TrackingState()
    {
        return (_followId.Length > 0 ? "follow " + _followId + " " : "") +
               (Marks.Count > 0 ? "marks " + Marks.Count + " " : "") +
               (Filled.Count > 0 ? "filled " + Filled.Count : "");
    }

    [TestHook("stt.backpackSlots")]
    private static int BackpackSlots(GameObject troll)
    {
        int used, slots;
        BackpackStore.Fill(TrollTaming.Zdo(troll.GetComponent<Character>()), out used, out slots);
        return slots;
    }

    [TestHook("stt.backpackAllUsed")]
    private static bool BackpackAllUsed(GameObject troll)
    {
        int used, slots;
        BackpackStore.Fill(TrollTaming.Zdo(troll.GetComponent<Character>()), out used, out slots);
        ModLog.Info("TestHooks: backpack of " + troll.name + " - " + used + " of " + slots +
                    " slot(s) used"); // D-172: the failing run said only 'False'
        return slots > 0 && used == slots;
    }

    /// Total quantity of an item over all four chests, read back from the ZDO.
    [TestHook("stt.backpackCount")]
    private static int BackpackCount(GameObject troll, string prefab)
    {
        ZDO z = TrollTaming.Zdo(troll.GetComponent<Character>());
        string shared = ObjectDB.instance.GetItemPrefab(prefab).GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
        int n = 0;
        for (int i = 0; i < BackpackStore.ChestCount; i++)
        {
            foreach (ItemDrop.ItemData it in BackpackStore.Read(z, i).GetAllItems())
            {
                if (it.m_shared.m_name == shared)
                {
                    n += it.m_stack;
                }
            }
        }

        return n;
    }

    /// The (uncached) full state and the cached one - they must agree right after a change.
    [TestHook("stt.backpackFull")]
    private static bool BackpackFull(GameObject troll)
    {
        return BackpackStore.IsFull(TrollTaming.Zdo(troll.GetComponent<Character>()));
    }

    [TestHook("stt.backpackFullCached")]
    private static bool BackpackFullCached(GameObject troll)
    {
        return BackpackStore.IsFullCached(TrollTaming.Zdo(troll.GetComponent<Character>()));
    }

    /// Takes everything out of chest 0 through the real store (Read / Write).
    [TestHook("stt.unpackChest0")]
    private static int UnpackChest0(GameObject troll)
    {
        ZDO z = TrollTaming.Zdo(troll.GetComponent<Character>());
        Inventory inv = BackpackStore.Read(z, 0);
        int n = inv.NrOfItems();
        foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(inv.GetAllItems()))
        {
            inv.RemoveItem(it);
        }

        BackpackStore.Write(z, 0, inv);
        return n;
    }

    /// D-173: the whole storage / cache cycle of backpack-full-cache IN ONE FRAME - fill, full, cached full, every slot used, a write through the real store, cache dropped.
    /// Between separate lab steps the troll's own AI may unload (D-173: a full troll near home unloads there), which is the plugin working, not a storage fault - so the
    /// storage test does not leave it a frame. Expected outcome: "ok", else the first check that failed with its numbers.
    [TestHook("stt.backpackCacheCycle")]
    private static string BackpackCacheCycle(GameObject troll)
    {
        ZDO z = TrollTaming.Zdo(troll.GetComponent<Character>());
        if (z == null)
        {
            return "no ZDO";
        }

        if (BackpackStore.IsFullCached(z))
        {
            return "cached full before the fill";
        }

        int added = FillBackpack(troll);
        if (!BackpackStore.IsFull(z))
        {
            return "not full after " + added + " stack(s)";
        }

        if (!BackpackStore.IsFullCached(z))
        {
            return "cache says not full right after the fill";
        }

        int used, slots;
        BackpackStore.Fill(z, out used, out slots);
        if (slots <= 0 || used != slots)
        {
            return "slots used " + used + " of " + slots;
        }

        int moved = UnpackChest0(troll);
        if (moved <= 0)
        {
            return "chest 0 was empty";
        }

        if (BackpackStore.IsFullCached(z))
        {
            return "cache not dropped by the write";
        }

        ModLog.Info("TestHooks: backpack cache cycle ok - " + added + " stack(s) added, " + used + " of " + slots +
                    " slots, " + moved + " taken out of chest 1, cache dropped");
        return "ok";
    }

    /// What the troll is doing now: farming / unloading / free, its hub and the distance to it.
    [TestHook("stt.state")]
    private static string State(GameObject troll)
    {
        Character c = troll.GetComponent<Character>();
        TrollFarmer f = troll.GetComponent<TrollFarmer>();
        HubInfo hub = HubRegistry.For(c);
        return (f == null ? "free" : f.Unloading ? "unloading" : "farming") + ", hub " +
               (hub != null ? hub.Number.ToString() : "none")
               + (hub != null ? " " + HubGeometry.Flat(hub.Pos - troll.transform.position).ToString("F0") + " m" : "") +
               ", full " + BackpackStore.IsFull(TrollTaming.Zdo(c));
    }

    /// The troll starts farming on its own (an active TrollFarmer: with nothing to farm it stays active and waits).
    [TestHook("stt.startFarm")]
    private static bool StartFarm(GameObject troll)
    {
        return TrollOrderService.Send(new FarmFocusCommand(troll.GetComponent<Character>(), "", true));
    }

    /// D-174 (review: "Unloading" = home trip because the backpack is full, NOT proof of an unload): the troll is on its way to unload.
    [TestHook("stt.returningToUnload")]
    private static bool ReturningToUnload(GameObject troll)
    {
        TrollFarmer f = troll.GetComponent<TrollFarmer>();
        return f != null && f.Unloading;
    }

    /// A tree that exists only as a ZDO (like a source in a zone nobody loads) `dx`,`dz` metres from the player. Returns the number of fake trees.
    [TestHook("stt.addFarTree")]
    private static int AddFarTree(float dx, float dz)
    {
        Player p = Player.m_localPlayer;
        Vector3 at = p.transform.position + new Vector3(dx, 0f, dz);
        at.y = ZoneSystem.instance.GetGroundHeight(at);
        int hash = "Beech1".GetStableHashCode();
        ZDO z = ZDOMan.instance.CreateNewZDO(at, hash);
        z.SetPrefab(hash);
        z.Persistent = false;
        FakeTrees.Add(z);
        return FakeTrees.Count;
    }

    [TestHook("stt.clearFarTrees")]
    private static int ClearFarTrees()
    {
        int n = FakeTrees.Count;
        foreach (ZDO z in FakeTrees)
        {
            z.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(z);
        }

        FakeTrees.Clear();
        return n;
    }
}
