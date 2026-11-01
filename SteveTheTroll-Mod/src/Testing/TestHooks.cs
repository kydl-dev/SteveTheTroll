using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Taming;
using UnityEngine;

namespace SteveTheTroll.Testing
{
    /// <summary>
    /// D-160: hooks for DrakesTestLab suites (Tests/suites/SteveTheTroll). Actions call the REAL mod code (TrollDepot.Place, BackpackStore, HubRegistry, RuntimeHealth); reads return short,
    /// stable strings / numbers. They only exist so that the suites can set up a world; nothing here runs unless a test calls it.
    /// </summary>
    internal static class TestHooks
    {
        static readonly List<ZDO> FakeHubs = new List<ZDO>();
        static long Me { get { return Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L; } }

        // ---- world setup -------------------------------------------------------------------------------------------------------------------------------

        /// The player's bed is where they stand (trolls and depots use it), hubs of earlier tests are gone.
        [TestHook("stt.reset")]
        static string Reset()
        {
            var p = Player.m_localPlayer;
            if (p == null) return "no player";
            Game.instance.GetPlayerProfile().SetCustomSpawnPoint(p.transform.position);
            ClearHubs();
            ClearFarTrees();
            RecoveryTestHooks.ClearWalled();           // D-167: the walled tree of the recovery scenario
            int gone = 0;
            foreach (var v in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
            {
                if (v == null || !v.IsValid()) continue;
                string n = Utils.GetPrefabName(v.gameObject);
                bool depot = n == Backpack.DepotPrefab.Name || (n == Backpack.DepotData.ChestPrefab && v.GetZDO().GetBool(Backpack.TrollDepot.FlagKey));
                if (depot) { v.ClaimOwnership(); ZNetScene.instance.Destroy(v.gameObject); gone++; }
            }
            return "bed at " + p.transform.position.ToString("F0") + ", " + gone + " depot piece(s) removed";
        }

        [TestHook("stt.spawnTroll")]
        static GameObject SpawnTroll(Vector3 position)
        {
            var prefab = ZNetScene.instance.GetPrefab(TrollTaming.TrollPrefab);
            if (prefab == null) throw new System.Exception("Troll prefab missing");
            var go = Object.Instantiate(prefab, position, Quaternion.identity);
            var tame = go.GetComponent<Tameable>();
            if (tame == null) throw new System.Exception("the troll has no Tameable (taming not installed)");
            HarmonyLib.AccessTools.Method(typeof(Tameable), "Tame").Invoke(tame, null);
            var c = go.GetComponent<Character>();
            if (!TrollTaming.SetOwner(c, Player.m_localPlayer)) throw new System.Exception("owner not set");
            return go;
        }

        /// A tamed troll `dx`,`dz` metres from the player, set on the ground there (far spawns: the player's own height may be far from the ground at that place).
        [TestHook("stt.spawnTrollFar")]
        static GameObject SpawnTrollFar(float dx, float dz)
        {
            var p = Player.m_localPlayer;
            var at = p.transform.position + new Vector3(dx, 0f, dz);
            at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.5f;
            return SpawnTroll(at);
        }

        /// Moves a troll `dx`,`dz` metres from the player onto the ground there (a troll made far away is removed by the game at once; one that is moved there is kept by TrollZones, like a real one).
        [TestHook("stt.moveTrollFar")]
        static string MoveTrollFar(GameObject troll, float dx, float dz)
        {
            var p = Player.m_localPlayer;
            var at = p.transform.position + new Vector3(dx, 0f, dz);
            at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.5f;
            troll.transform.position = at;
            return at.ToString("F0");
        }

        /// The player teleports `dx`,`dz` metres from where they stand (the game's own TeleportTo: zones load around the new place). A troll made far from the player is removed by the game, so
        /// "a troll 400 m from its hub" is made by taking the player there and spawning the troll next to them.
        [TestHook("stt.teleport")]
        static string Teleport(float dx, float dz)
        {
            var p = Player.m_localPlayer;
            var at = p.transform.position + new Vector3(dx, 0f, dz);
            at.y = ZoneSystem.instance.GetGroundHeight(at) + 1f;
            p.TeleportTo(at, p.transform.rotation, false);
            return at.ToString("F0");
        }

        static readonly Dictionary<int, Vector3> Marks = new Dictionary<int, Vector3>();

        /// Remembers where the troll stands now (see stt.moved).
        [TestHook("stt.mark")]
        static string Mark(GameObject troll) { Marks[troll.GetInstanceID()] = troll.transform.position; return troll.transform.position.ToString("F0"); }

        /// Flat metres the troll has moved since stt.mark.
        [TestHook("stt.moved")]
        static int Moved(GameObject troll)
        {
            Vector3 was;
            if (!Marks.TryGetValue(troll.GetInstanceID(), out was)) throw new System.Exception("stt.mark was not called");
            return Mathf.RoundToInt(Hub.HubRules.Flat(troll.transform.position - was));
        }

        /// Flat metres from the troll to the hub it would use (-1 = none).
        [TestHook("stt.hubDistance")]
        static int HubDistance(GameObject troll)
        {
            Hub.HubRegistry.All(true);
            var hub = Hub.HubRegistry.For(troll.GetComponent<Character>());
            return hub != null ? Mathf.RoundToInt(Hub.HubRules.Flat(hub.Pos - troll.transform.position)) : -1;
        }

        /// The "walk to the owner's bed" order (the Trolls page / E menu command).
        [TestHook("stt.sendHome")]
        static bool SendHomeOrder(GameObject troll) { return TrollFarming.SendHome(troll.GetComponent<Character>()); }

        [TestHook("stt.moveTroll")]
        static string MoveTroll(GameObject troll, Vector3 position)
        {
            if (troll == null) throw new System.Exception("no troll");
            troll.transform.position = position;
            return position.ToString("F0");
        }

        // ---- backpack ------------------------------------------------------------------------------------------------------------------------------------

        static ItemDrop.ItemData Item(string prefab, int count)
        {
            var p = ObjectDB.instance.GetItemPrefab(prefab);
            if (p == null) throw new System.Exception("item " + prefab + " not found");
            var it = p.GetComponent<ItemDrop>().m_itemData.Clone();
            it.m_dropPrefab = p;            // a prefab's template item has none; without it Inventory.Save writes "" and Load drops the stack
            it.m_stack = count; it.m_quality = 1; it.m_durability = it.GetMaxDurability();
            return it;
        }

        /// Puts one stack into the first backpack chest with room, through the real store. Returns the chest index (-1 = full).
        [TestHook("stt.pack")]
        static int Pack(GameObject troll, string prefab, int count)
        {
            var z = TrollTaming.Zdo(troll.GetComponent<Character>());
            return Backpack.BackpackStore.TryAdd(z, Item(prefab, count), null);
        }

        /// Fills every slot of the backpack with Stone stacks. Returns the number of stacks.
        [TestHook("stt.fillBackpack")]
        static int FillBackpack(GameObject troll)
        {
            var z = TrollTaming.Zdo(troll.GetComponent<Character>());
            int n = 0;
            while (n < 500 && Backpack.BackpackStore.TryAdd(z, Item("Stone", 20), null) >= 0) n++;
            return n;
        }

        [TestHook("stt.backpackStacks")]
        static int BackpackStacks(GameObject troll) { int used, slots; Backpack.BackpackStore.Fill(TrollTaming.Zdo(troll.GetComponent<Character>()), out used, out slots); return used; }

        /// True when the backpack holds at most the given number of stacks. A troll that has unloaded goes back to farming
        /// and fills the pack again, so "exactly 0" is a race; "emptied to a few stacks" is what the scenario means.
        [TestHook("stt.backpackAtMost")]
        static bool BackpackAtMost(GameObject troll, int stacks) { return BackpackStacks(troll) <= stacks; }

        [TestHook("stt.backpackSlots")]
        static int BackpackSlots(GameObject troll) { int used, slots; Backpack.BackpackStore.Fill(TrollTaming.Zdo(troll.GetComponent<Character>()), out used, out slots); return slots; }

        [TestHook("stt.backpackAllUsed")]
        static bool BackpackAllUsed(GameObject troll) { int used, slots; Backpack.BackpackStore.Fill(TrollTaming.Zdo(troll.GetComponent<Character>()), out used, out slots); return slots > 0 && used == slots; }

        /// Total quantity of an item over all four chests, read back from the ZDO.
        [TestHook("stt.backpackCount")]
        static int BackpackCount(GameObject troll, string prefab)
        {
            var z = TrollTaming.Zdo(troll.GetComponent<Character>());
            string shared = ObjectDB.instance.GetItemPrefab(prefab).GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
            int n = 0;
            for (int i = 0; i < Backpack.BackpackStore.ChestCount; i++)
                foreach (var it in Backpack.BackpackStore.Read(z, i).GetAllItems()) if (it.m_shared.m_name == shared) n += it.m_stack;
            return n;
        }

        /// The (uncached) full state and the cached one - they must agree right after a change.
        [TestHook("stt.backpackFull")]
        static bool BackpackFull(GameObject troll) { return Backpack.BackpackStore.IsFull(TrollTaming.Zdo(troll.GetComponent<Character>())); }

        [TestHook("stt.backpackFullCached")]
        static bool BackpackFullCached(GameObject troll) { return Backpack.BackpackStore.IsFullCached(TrollTaming.Zdo(troll.GetComponent<Character>())); }

        /// Takes everything out of chest 0 through the real store (Read / Write).
        [TestHook("stt.unpackChest0")]
        static int UnpackChest0(GameObject troll)
        {
            var z = TrollTaming.Zdo(troll.GetComponent<Character>());
            var inv = Backpack.BackpackStore.Read(z, 0);
            int n = inv.NrOfItems();
            foreach (var it in new List<ItemDrop.ItemData>(inv.GetAllItems())) inv.RemoveItem(it);
            Backpack.BackpackStore.Write(z, 0, inv);
            return n;
        }

        /// What the troll is doing now: farming / unloading / free, its hub and the distance to it.
        [TestHook("stt.state")]
        static string State(GameObject troll)
        {
            var c = troll.GetComponent<Character>();
            var f = troll.GetComponent<TrollFarmer>();
            var hub = Hub.HubRegistry.For(c);
            return (f == null ? "free" : f.Unloading ? "unloading" : "farming") + ", hub " + (hub != null ? hub.Number.ToString() : "none")
                + (hub != null ? " " + Hub.HubRules.Flat(hub.Pos - troll.transform.position).ToString("F0") + " m" : "") + ", full " + Backpack.BackpackStore.IsFull(TrollTaming.Zdo(c));
        }

        /// The troll starts farming on its own (an active TrollFarmer: with nothing to farm it stays active and waits).
        [TestHook("stt.startFarm")]
        static bool StartFarm(GameObject troll) { return TrollFarming.Begin(troll.GetComponent<Character>(), "", true); }

        [TestHook("stt.unloading")]
        static bool Unloading(GameObject troll) { var f = troll.GetComponent<TrollFarmer>(); return f != null && f.Unloading; }

        // ---- long-range farming and the remembered spot (user 2026-10-31) -------------------------------------------------------------------------------

        static readonly List<ZDO> FakeTrees = new List<ZDO>();

        /// A tree that exists only as a ZDO (like a source in a zone nobody loads) `dx`,`dz` metres from the player. Returns the number of fake trees.
        [TestHook("stt.addFarTree")]
        static int AddFarTree(float dx, float dz)
        {
            var p = Player.m_localPlayer;
            var at = p.transform.position + new Vector3(dx, 0f, dz);
            at.y = ZoneSystem.instance.GetGroundHeight(at);
            int hash = "Beech1".GetStableHashCode();
            var z = ZDOMan.instance.CreateNewZDO(at, hash);
            z.SetPrefab(hash);
            z.Persistent = false;
            FakeTrees.Add(z);
            return FakeTrees.Count;
        }

        [TestHook("stt.clearFarTrees")]
        static int ClearFarTrees()
        {
            int n = FakeTrees.Count;
            foreach (var z in FakeTrees) { z.SetOwner(ZDOMan.GetSessionID()); ZDOMan.instance.DestroyZDO(z); }
            FakeTrees.Clear();
            return n;
        }

        /// The long-range search from the player for wood: "none" or "<flat metres> m <item>".
        [TestHook("stt.farSearch")]
        static string FarSearch()
        {
            var p = Player.m_localPlayer;
            Vector3 pos; string item;
            bool found = TrollFarming.FindFar(p.transform.position, ModSettings.FarmLongRange.Value, ModSettings.FarmMaxRange.Value, 10,
                new[] { new Ui.TrollFocus.Goal("Wood") }, null, out pos, out item);
            return found ? Mathf.RoundToInt(Hub.HubRules.Flat(pos - p.transform.position)) + " m " + item : "none";
        }

        /// Flat metres to the nearest long-range wood source (-1 = none). The lab world has real trees, so suites assert limits, not exact values.
        [TestHook("stt.farMetres")]
        static int FarMetres()
        {
            var p = Player.m_localPlayer;
            Vector3 pos; string item;
            bool found = TrollFarming.FindFar(p.transform.position, ModSettings.FarmLongRange.Value, ModSettings.FarmMaxRange.Value, 10,
                new[] { new Ui.TrollFocus.Goal("Wood") }, null, out pos, out item);
            return found ? Mathf.RoundToInt(Hub.HubRules.Flat(pos - p.transform.position)) : -1;
        }

        /// The troll remembers a farm spot `dx`,`dz` metres from the player, as if it had broken a tree there.
        [TestHook("stt.rememberSpot")]
        static bool RememberSpot(GameObject troll, float dx, float dz)
        {
            var f = troll.GetComponent<TrollFarmer>();
            if (f == null) return false;
            var at = Player.m_localPlayer.transform.position + new Vector3(dx, 0f, dz);
            f.RememberSpotForTest(at);
            return true;
        }

        /// Flat metres from the troll to its remembered spot (-1 = none).
        [TestHook("stt.spotDistance")]
        static int SpotDistance(GameObject troll) { var f = troll.GetComponent<TrollFarmer>(); return f != null ? f.SpotDistance : -2; }

        [TestHook("stt.travelling")]
        static bool Travelling(GameObject troll) { var f = troll.GetComponent<TrollFarmer>(); return f != null && f.Travelling; }

        // ---- depot -------------------------------------------------------------------------------------------------------------------------------------

        [TestHook("stt.spawnDepot")]
        static GameObject SpawnDepot(Vector3 position)
        {
            var frame = Backpack.TrollDepot.Place(position, Quaternion.identity, Me);
            if (frame == null) throw new System.Exception("depot prefab missing");
            return frame;
        }

        [TestHook("stt.depotChests")]
        static int DepotChests(GameObject frame) { return Backpack.TrollDepot.ChestsOf(frame).Count; }

        /// Items in chest `index` of the depot (the chests are the real Containers).
        [TestHook("stt.depotPut")]
        static bool DepotPut(GameObject frame, int index, string prefab, int count)
        {
            var c = Backpack.TrollDepot.ChestsOf(frame);
            return index < c.Count && c[index].GetInventory().AddItem(Item(prefab, count));
        }

        [TestHook("stt.depotTake")]
        static int DepotTake(GameObject frame, int index = -1)
        {
            int n = 0, i = -1;
            foreach (var c in Backpack.TrollDepot.ChestsOf(frame))
            {
                i++;
                if (index >= 0 && i != index) continue;
                var inv = c.GetInventory();
                foreach (var it in new List<ItemDrop.ItemData>(inv.GetAllItems())) { inv.RemoveItem(it); n++; }
            }
            return n;
        }

        [TestHook("stt.depotItems")]
        static int DepotItems(GameObject frame) { int n = 0; foreach (var c in Backpack.TrollDepot.ChestsOf(frame)) n += c.GetInventory().NrOfItems(); return n; }

        /// A player's chest window opening / closing (Container.SetInUse, the same call the game makes).
        [TestHook("stt.depotWindow")]
        static string DepotWindow(GameObject frame, bool open)
        {
            var c = Backpack.TrollDepot.ChestsOf(frame);
            if (c.Count == 0) throw new System.Exception("no chests");
            HarmonyLib.AccessTools.Method(typeof(Container), "SetInUse").Invoke(c[0], new object[] { open });
            return open ? "open" : "closed";
        }

        [TestHook("stt.depotReserve")]
        static string DepotReserve(GameObject frame)
        {
            foreach (var c in Backpack.TrollDepot.ChestsOf(frame)) Backpack.TrollDepot.ReserveForTest(c);
            return "reserved";
        }

        [TestHook("stt.depotAlive")]
        static bool DepotAlive(GameObject frame) { return frame != null; }

        /// Frames of troll depots in the world (flagged chests are counted too).
        [TestHook("stt.depotCount")]
        static int DepotCount()
        {
            int n = 0;
            foreach (var v in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
                if (v != null && v.IsValid() && Utils.GetPrefabName(v.gameObject) == Backpack.DepotPrefab.Name) n++;
            return n;
        }

        // ---- hubs --------------------------------------------------------------------------------------------------------------------------------------

        /// A hub of the player at a place relative to the player (no building: only its ZDO, which is all the registry reads).
        [TestHook("stt.addHub")]
        static int AddHub(int number, float dx, float dz, string owner)
        {
            var p = Player.m_localPlayer;
            long who = owner == "other" ? Me + 777 : Me;
            var z = ZDOMan.instance.CreateNewZDO(p.transform.position + new Vector3(dx, 0f, dz), Hub.HubData.PrefabName.GetStableHashCode());
            z.SetPrefab(Hub.HubData.PrefabName.GetStableHashCode());      // CreateNewZDO ignores its prefab argument in this game version
            z.Set(Hub.HubRegistry.OwnerKey, who);
            z.Set(Hub.HubRegistry.OwnerNameKey, owner == "other" ? "Other" : p.GetPlayerName());
            z.Set(Hub.HubRegistry.NumberKey, number);
            z.Persistent = false;
            FakeHubs.Add(z);
            int known = Hub.HubRegistry.All(true).Count;
            var scan = new List<ZDO>(); int idx = 0, guard = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(Hub.HubData.PrefabName, scan, ref idx) && guard++ < 10000) { }
            ModLog.Info("TestHooks: addHub " + number + " owner " + who + " prefab " + z.GetPrefab() + " (want " + Hub.HubData.PrefabName.GetStableHashCode() + "), no " + z.GetInt(Hub.HubRegistry.NumberKey, -1) + ", sector " + z.GetSector() + ", registry knows " + known + ", raw scan finds " + scan.Count + ", ZDOMan.GetZDO " + (ZDOMan.instance.GetZDO(z.m_uid) != null));
            return known;
        }

        [TestHook("stt.clearHubs")]
        static int ClearHubs()
        {
            int n = FakeHubs.Count;
            foreach (var z in FakeHubs) { z.SetOwner(ZDOMan.GetSessionID()); ZDOMan.instance.DestroyZDO(z); }
            FakeHubs.Clear();
            Hub.HubRegistry.All(true);
            return n;
        }

        /// The hub number the troll would use now (0 = the bed).
        [TestHook("stt.hubFor")]
        static int HubFor(GameObject troll)
        {
            Hub.HubRegistry.All(true);
            var h = Hub.HubRegistry.For(troll.GetComponent<Character>());
            return h != null ? h.Number : 0;
        }

        // ---- walk back to the hub (D-162) --------------------------------------------------------------------------------------------------------------

        /// The idle check's walk-back decision now: 1 = the troll walks back (already or newly), 0 = not.
        [TestHook("stt.returnCheck")]
        static int ReturnCheck(GameObject troll)
        {
            Hub.HubRegistry.All(true);
            var c = troll.GetComponent<Character>();
            return TrollReturn.Check(c, troll.GetComponent<MonsterAI>()) ? 1 : 0;
        }

        [TestHook("stt.returning")]
        static bool Returning(GameObject troll) { return troll != null && troll.GetComponent<TrollReturn>() != null; }

        /// Flat metres from the troll to the hub it would walk back to (-1 = none).
        [TestHook("stt.returnDistance")]
        static int ReturnDistance(GameObject troll)
        {
            float d;
            Hub.HubRegistry.All(true);
            return TrollReturn.Target(troll.GetComponent<Character>(), out d) != null ? Mathf.RoundToInt(d) : -1;
        }

        /// A follow order for the local player (on) or none (off).
        [TestHook("stt.follow")]
        static bool Follow(GameObject troll, bool on)
        {
            var ai = troll.GetComponent<MonsterAI>();
            ai.SetFollowTarget(on ? Player.m_localPlayer.gameObject : null);
            return ai.GetFollowTarget() != null;
        }

        // ---- health / figures --------------------------------------------------------------------------------------------------------------------------

        /// One pass of the runtime health monitor now; returns the number of problems it found.
        [TestHook("stt.health")]
        static int Health() { RuntimeHealth.RunNow(); return RuntimeHealth.LastProblems; }

        [TestHook("stt.healthText")]
        static string HealthText() { return RuntimeHealth.LastText; }

        /// The little trolls on the slippers: "n figure(s), worst below-sole x m, worst tilt y deg, head above top z m" (measured against the slipper's own bounds).
        [TestHook("stt.figures")]
        static string Figures()
        {
            int n = 0; float worstBelow = float.MinValue, worstTilt = 0f, worstHead = float.MaxValue;
            foreach (var f in Figure.TrollFigure.All)
            {
                if (f == null || float.IsNaN(f.LastBelowSole)) continue;
                n++;
                worstBelow = Mathf.Max(worstBelow, f.LastBelowSole); worstTilt = Mathf.Max(worstTilt, f.LastTilt); worstHead = Mathf.Min(worstHead, f.LastHeadAboveTop);
            }
            return n == 0 ? "no measured figure" : n + " figure(s), worst below-sole " + worstBelow.ToString("F3") + " m, worst tilt " + worstTilt.ToString("F0") + " deg, head above top " + worstHead.ToString("F3") + " m";
        }

        [TestHook("stt.figuresOk")]
        static bool FiguresOk()
        {
            foreach (var f in Figure.TrollFigure.All)
            {
                if (f == null || float.IsNaN(f.LastBelowSole)) continue;
                if (HealthRules.FigureBad(f.LastBelowSole, 0.03f, f.LastTilt, 60f)) return false;
            }
            return true;
        }

        [TestHook("stt.figureCount")]
        static int FigureCount() { int n = 0; foreach (var f in Figure.TrollFigure.All) if (f != null && !float.IsNaN(f.LastBelowSole)) n++; return n; }
    }
}
