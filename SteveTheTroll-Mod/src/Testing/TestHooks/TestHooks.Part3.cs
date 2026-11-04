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

/// <summary>Part 3 of TestHooks (split out so the class stays under the size limit).</summary>
internal static partial class TestHooks
{
    /// The long-range search from the player for wood: "none" or "
    /// <flat metres> m <item>".
    [TestHook("stt.farSearch")]
    private static string FarSearch()
    {
        Player p = Player.m_localPlayer;
        Vector3 pos;
        string item;
        bool found = TrollFarming.FindFar(p.transform.position, new TrollFarming.SearchRange(ModSettings.FarmLongRange.Value, ModSettings.FarmMaxRange.Value), 10,
            new[] { new TrollFocus.Goal("Wood") }, null, out pos, out item);
        return found ? Mathf.RoundToInt(HubGeometry.Flat(pos - p.transform.position)) + " m " + item : "none";
    }

    /// Flat metres to the nearest long-range wood source (-1 = none). The lab world has real trees, so suites assert limits, not exact values.
    [TestHook("stt.farMetres")]
    private static int FarMetres()
    {
        Player p = Player.m_localPlayer;
        Vector3 pos;
        string item;
        bool found = TrollFarming.FindFar(p.transform.position, new TrollFarming.SearchRange(ModSettings.FarmLongRange.Value, ModSettings.FarmMaxRange.Value), 10,
            new[] { new TrollFocus.Goal("Wood") }, null, out pos, out item);
        return found ? Mathf.RoundToInt(HubGeometry.Flat(pos - p.transform.position)) : -1;
    }

    /// The troll remembers a farm spot `dx`,`dz` metres from the player, as if it had broken a tree there.
    [TestHook("stt.rememberSpot")]
    private static bool RememberSpot(GameObject troll, float dx, float dz)
    {
        TrollFarmer f = troll.GetComponent<TrollFarmer>();
        if (f == null)
        {
            return false;
        }

        Vector3 at = Player.m_localPlayer.transform.position + new Vector3(dx, 0f, dz);
        _spotTarget = at;
        return f.Spots.Remember(at);
    }

    /// Flat metres from the troll to its remembered spot (-1 = none).
    [TestHook("stt.spotDistance")]
    private static int SpotDistance(GameObject troll)
    {
        TrollFarmer f = troll.GetComponent<TrollFarmer>();
        if (f == null)
        {
            return -2;
        }

        Vector3 s;
        if (!f.Spots.Spot(out s))
        {
            return -1;
        }

        s.y = troll.transform.position.y;
        return Mathf.RoundToInt(Vector3.Distance(s, troll.transform.position));
    }

    [TestHook("stt.travelling")]
    private static bool Travelling(GameObject troll)
    {
        TrollFarmer f = troll.GetComponent<TrollFarmer>();
        return f != null && f.Travelling;
    }

    // ---- depot -------------------------------------------------------------------------------------------------------------------------------------

    [TestHook("stt.spawnDepot")]
    private static GameObject SpawnDepot(Vector3 position)
    {
        GameObject frame = TrollDepot.Place(position, Quaternion.identity, Me);
        if (frame == null)
        {
            throw new Exception("depot prefab missing");
        }

        return frame;
    }

    [TestHook("stt.depotChests")]
    private static int DepotChests(GameObject frame)
    {
        return TrollDepot.ChestsOf(frame).Count;
    }

    /// Items in chest `index` of the depot (the chests are the real Containers).
    [TestHook("stt.depotPut")]
    private static bool DepotPut(GameObject frame, int index, string prefab, int count)
    {
        List<Container> c = TrollDepot.ChestsOf(frame);
        return index < c.Count && c[index].GetInventory().AddItem(Item(prefab, count));
    }

    [TestHook("stt.depotTake")]
    private static int DepotTake(GameObject frame, int index = -1)
    {
        int n = 0, i = -1;
        foreach (Container c in TrollDepot.ChestsOf(frame))
        {
            i++;
            if (index >= 0 && i != index)
            {
                continue;
            }

            Inventory inv = c.GetInventory();
            foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(inv.GetAllItems()))
            {
                inv.RemoveItem(it);
                n++;
            }
        }

        return n;
    }

    /// D-174 (review: depot fallback in a live world): every chest of the depot filled to the last slot with Wood. Expected outcome: stacks added.
    [TestHook("stt.depotFill")]
    private static int DepotFill(GameObject frame)
    {
        int n = 0;
        foreach (Container c in TrollDepot.ChestsOf(frame))
        {
            ZNetView v = c.GetComponent<ZNetView>();
            if (v != null && !v.IsOwner())
            {
                v.ClaimOwnership();
            }

            while (c.GetInventory().HaveEmptySlot() && n < 1000)
            {
                c.GetInventory().AddItem(Item("Wood", 50));
                n++;
            }
        }

        return n;
    }

    /// D-174 (review: spot-return must COMPLETE): the troll stands within `metres` of its remembered farm spot. Expected outcome: false without a farmer / spot.
    [TestHook("stt.reachedSpot")]
    private static bool ReachedSpot(GameObject troll, float metres)
    {
        if (_spotTarget == Vector3.zero)
        {
            return _spotTarget != Vector3.zero;
        }

        float d = HubGeometry.Flat(_spotTarget - troll.transform.position);
        if (d <= metres)
        {
            ModLog.Info("TestHooks: the troll reached its remembered spot (" + d.ToString("F1") + " m)");
        }

        return d <= metres;
    }

    /// D-175 (run 181331: the troll's object was re-created on the way - "'troll' not found"): the FOLLOWED troll (stt.followTroll, by its ZDO) stands within `metres` of the
    /// remembered spot. Expected outcome: false without a followed troll / spot.
    [TestHook("stt.followedReachedSpot")]
    private static bool FollowedReachedSpot(float metres)
    {
        ZDO z = FollowedZdo();
        if (z == null || _spotTarget == Vector3.zero)
        {
            return z != null && z == null;
        }

        float d = HubGeometry.Flat(_spotTarget - z.GetPosition());
        if (d <= metres)
        {
            ModLog.Info("TestHooks: the followed troll reached its remembered spot (" + d.ToString("F1") + " m)");
        }

        return d <= metres;
    }

    /// D-175: the followed troll is loaded and has a farm job that is not a home / unload trip. Expected outcome: false while not loaded.
    [TestHook("stt.followedFarming")]
    private static bool FollowedFarming()
    {
        Character c = _followId.Length > 0 ? TrollRegistry.FindLoaded(_followId) : null;
        TrollFarmer f = c != null ? c.GetComponent<TrollFarmer>() : null;
        return f != null && !f.Unloading;
    }

    [TestHook("stt.depotItems")]
    private static int DepotItems(GameObject frame)
    {
        int n = 0;
        foreach (Container c in TrollDepot.ChestsOf(frame))
        {
            n += c.GetInventory().NrOfItems();
        }

        return n;
    }

    /// A player's chest window opening / closing (Container.SetInUse, the same call the game makes).
    [TestHook("stt.depotWindow")]
    private static string DepotWindow(GameObject frame, bool open)
    {
        List<Container> c = TrollDepot.ChestsOf(frame);
        if (c.Count == 0)
        {
            throw new Exception("no chests");
        }

        AccessTools.Method(typeof(Container), "SetInUse").Invoke(c[0], new object[] { open });
        return open ? "open" : "closed";
    }

    [TestHook("stt.depotReserve")]
    private static string DepotReserve(GameObject frame)
    {
        foreach (Container c in TrollDepot.ChestsOf(frame))
        {
            TrollDepot.ReserveForTest(c);
        }

        return "reserved";
    }

    [TestHook("stt.depotAlive")]
    private static bool DepotAlive(GameObject frame)
    {
        return frame != null;
    }

    /// Frames of troll depots in the world (flagged chests are counted too).
    [TestHook("stt.depotCount")]
    private static int DepotCount()
    {
        int n = 0;
        foreach (ZNetView v in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
        {
            if (v != null && v.IsValid() && Utils.GetPrefabName(v.gameObject) == DepotPrefab.Name)
            {
                n++;
            }
        }

        return n;
    }

    // ---- hubs --------------------------------------------------------------------------------------------------------------------------------------

    /// A hub of the player at a place relative to the player (no building: only its ZDO, which is all the registry reads).
    [TestHook("stt.addHub")]
    private static int AddHub(int number, float dx, float dz, string owner)
    {
        Player p = Player.m_localPlayer;
        long who = owner == "other" ? Me + 777 : Me;
        ZDO z = ZDOMan.instance.CreateNewZDO(p.transform.position + new Vector3(dx, 0f, dz),
            HubData.PrefabName.GetStableHashCode());
        z.SetPrefab(HubData.PrefabName
            .GetStableHashCode()); // CreateNewZDO ignores its prefab argument in this game version
        z.Persistent =
            true; // D-173 (run 161451): a raw ZDO is not persistent - when the player went 400 m away its zone unloaded and the game DELETED the test hub ("0 Troll Hub(s) known")
        z.Set(HubRegistry.OwnerKey, who);
        z.Set(HubRegistry.OwnerNameKey, owner == "other" ? "Other" : p.GetPlayerName());
        z.Set(HubRegistry.NumberKey,
            number); // D-174 (review): the old "z.Persistent = false" here undid the line above - removed
        FakeHubs.Add(z);
        int known = HubRegistry.All(true).Count;
        var scan = new List<ZDO>();
        int idx = 0, guard = 0;
        while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(HubData.PrefabName, scan, ref idx) && guard++ < 10000)
        {
        }

        ModLog.Info("TestHooks: addHub " + number + " owner " + who + " prefab " + z.GetPrefab() + " (want " +
                    HubData.PrefabName.GetStableHashCode() + "), no " + z.GetInt(HubRegistry.NumberKey, -1) +
                    ", sector " + z.GetSector() + ", registry knows " + known + ", raw scan finds " + scan.Count +
                    ", ZDOMan.GetZDO " + (ZDOMan.instance.GetZDO(z.m_uid) != null));
        return known;
    }

    [TestHook("stt.clearHubs")]
    private static int ClearHubs()
    {
        int n = FakeHubs.Count;
        foreach (ZDO z in FakeHubs)
        {
            z.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(z);
        }

        FakeHubs.Clear();
        HubRegistry.All(true);
        return n;
    }

    /// The hub number the troll would use now (0 = the bed).
    [TestHook("stt.hubFor")]
    private static int HubFor(GameObject troll)
    {
        HubRegistry.All(true);
        HubInfo h = HubRegistry.For(troll.GetComponent<Character>());
        return h != null ? h.Number : 0;
    }

    // ---- walk back to the hub (D-162) --------------------------------------------------------------------------------------------------------------

    /// The idle check's walk-back decision now: 1 = the troll walks back (already or newly), 0 = not.
    [TestHook("stt.returnCheck")]
    private static int ReturnCheck(GameObject troll)
    {
        HubRegistry.All(true);
        Character c = troll.GetComponent<Character>();
        return TrollReturn.Check(c, troll.GetComponent<MonsterAI>()) ? 1 : 0;
    }

    [TestHook("stt.returning")]
    private static bool Returning(GameObject troll)
    {
        return troll != null && troll.GetComponent<TrollReturn>() != null;
    }

    /// Flat metres from the troll to the hub it would walk back to (-1 = none).
    [TestHook("stt.returnDistance")]
    private static int ReturnDistance(GameObject troll)
    {
        float d;
        HubRegistry.All(true);
        return TrollReturn.Target(troll.GetComponent<Character>(), out d) != null ? Mathf.RoundToInt(d) : -1;
    }

    /// A follow order for the local player (on) or none (off).
    [TestHook("stt.follow")]
    private static bool Follow(GameObject troll, bool on)
    {
        MonsterAI ai = troll.GetComponent<MonsterAI>();
        ai.SetFollowTarget(on ? Player.m_localPlayer.gameObject : null);
        return ai.GetFollowTarget() != null;
    }

    // ---- health / figures --------------------------------------------------------------------------------------------------------------------------

    /// One pass of the runtime health monitor now; returns the number of problems it found.
    [TestHook("stt.health")]
    private static int Health()
    {
        RuntimeHealth.RunNow();
        return RuntimeHealth.LastProblems;
    }

    [TestHook("stt.healthText")]
    private static string HealthText()
    {
        return RuntimeHealth.LastText;
    }
}
