using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Backpack.Domain;
using SteveTheTroll.Backpack.Infrastructure;
using SteveTheTroll.Hubs.Domain;
using SteveTheTroll.Hubs.Infrastructure;
using SteveTheTroll.Trolls.Combat.Infrastructure;
using SteveTheTroll.Trolls.Farming.Infrastructure;
using SteveTheTroll.Trolls.Home.Infrastructure;
using SteveTheTroll.Trolls.Orders.Commands;
using SteveTheTroll.Trolls.Registry.Infrastructure;
using SteveTheTroll.Trolls.Taming.Infrastructure;
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

        /// The player is moved to the test spot (TestArena), the bed is put there (trolls and depots use it), hubs of earlier tests are gone.
        [TestHook("stt.reset")]
        static string Reset()
        {
            var p = Player.m_localPlayer;
            if (p == null) return "no player";
            TestArena.MovePlayer(p);                   // D-171 (user): every test starts on the same open spot, 70 m from the lab start (trophy stones, house)
            int trolls = CombatTestHooks.ClearTrolls();      // D-174 (review): every test starts without test / arena trolls (17 tests never called stt.clearTrolls)
            Marks.Clear(); _spotTarget = Vector3.zero; _followId = ""; _follow = ZDOID.None; Filled.Clear(); _depotStoneAtFollow = 0;
            Game.instance.GetPlayerProfile().SetCustomSpawnPoint(p.transform.position);
            ClearHubs();
            ClearFarTrees();
            RecoveryTestHooks.ClearWalled();           // D-167: the walled tree of the recovery scenario
            FarmingTestHooks.ClearPlanted();           // D-170: the trees of farm-chops-tree
            int gone = 0;
            foreach (var v in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
            {
                if (v == null || !v.IsValid()) continue;
                string n = Utils.GetPrefabName(v.gameObject);
                bool depot = n == DepotPrefab.Name || (n == DepotData.ChestPrefab && v.GetZDO().GetBool(TrollDepot.FlagKey));
                if (depot) { v.ClaimOwnership(); ZNetScene.instance.Destroy(v.gameObject); gone++; }
            }
            return "bed at " + p.transform.position.ToString("F0") + ", " + gone + " depot piece(s) removed, " + trolls + " troll(s) removed";
        }

        /// D-171 (user: "sometimes troll walks into me ... spawn him further from player"): a tamed troll `dx`,`dz` metres from the player AS IT STANDS NOW (the lab's own
        /// position argument was relative to the test's start, so after stt.arenaFurther the troll appeared 100 m back at the bed), pushed out to at least MinFromPlayer m.
        [TestHook("stt.spawnTroll")]
        static GameObject SpawnTroll(float dx, float dz) { return Spawn(dx, dz, MinFromPlayer); }

        /// Like stt.spawnTroll but as close as asked (only for tests about a troll right next to the player: hold-look, defend-rage, nohit-zone) - still in front.
        [TestHook("stt.spawnTrollNear")]
        static GameObject SpawnTrollNear(float dx, float dz) { return Spawn(dx, dz, 0f); }

        // D-172 (user 2026-11-02: "spawn trolls further from me (not behind me) so that they don't walk into player"): at least 16 m (was 12) and in FRONT of the player
        // (stt.reset faces the player along world +z, so a test's dz > 0 is in front). A troll offset behind the player fails the step loudly instead of being moved silently.
        const float MinFromPlayer = 16f;

        static GameObject Spawn(float dx, float dz, float minMetres)
        {
            var p = Player.m_localPlayer;
            var off = new Vector3(dx, 0f, dz);
            var fwd = p.GetLookDir(); fwd.y = 0f;
            if (Vector3.Dot(off, fwd) <= 0f)
                throw new System.Exception("troll offset (" + dx + ", " + dz + ") is not in front of the player (facing " + fwd.normalized.ToString("F2") + ") - spawn trolls in front (D-172)");
            if (off.magnitude < minMetres) off = (off.sqrMagnitude > 0.01f ? off.normalized : -p.transform.forward) * minMetres;
            var at = p.transform.position + off;
            at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.3f;
            ModLog.Info("TestHooks: troll spawned " + off.magnitude.ToString("F1") + " m from the player at " + at.ToString("F0") + (off.magnitude > new Vector2(dx, dz).magnitude + 0.1f ? " (pushed out to " + minMetres + " m)" : ""));
            return SpawnTrollAt(at);
        }

        static readonly HashSet<ZDOID> Made = new HashSet<ZDOID>();
        /// D-172: trolls a test hook made (stt.clearTrolls removes these wherever they walked).
        internal static bool MadeByTest(ZDOID id) { return Made.Contains(id); }

        static GameObject SpawnTrollAt(Vector3 position)
        {
            var prefab = ZNetScene.instance.GetPrefab(TrollTaming.TrollPrefab);
            if (prefab == null) throw new System.Exception("Troll prefab missing");
            var go = Object.Instantiate(prefab, position, Quaternion.identity);
            var tame = go.GetComponent<Tameable>();
            if (tame == null) throw new System.Exception("the troll has no Tameable (taming not installed)");
            HarmonyLib.AccessTools.Method(typeof(Tameable), "Tame").Invoke(tame, null);
            var c = go.GetComponent<Character>();
            if (!TrollTaming.SetOwner(c, Player.m_localPlayer)) throw new System.Exception("owner not set");
            var mv = go.GetComponent<ZNetView>();
            if (mv != null && mv.IsValid()) Made.Add(mv.GetZDO().m_uid);
            return go;
        }

        /// A tamed troll `dx`,`dz` metres from the player, set on the ground there (far spawns: the player's own height may be far from the ground at that place).
        [TestHook("stt.spawnTrollFar")]
        static GameObject SpawnTrollFar(float dx, float dz)
        {
            var p = Player.m_localPlayer;
            var at = p.transform.position + new Vector3(dx, 0f, dz);
            at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.5f;
            return SpawnTrollAt(at);
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
            at.y = Mathf.Max(ZoneSystem.instance.GetGroundHeight(at), p.transform.position.y) + 1f;
            // D-173 (run 154147: far-troll-goes-home spawned its troll at the BED): the target 400 m away is not loaded, so its ground height read 0, the player arrived inside the
            // ground and the game put it back at the spawn point. distantTeleport = true: the game finds the floor itself once the area is loaded.
            p.TeleportTo(at, p.transform.rotation, true);
            ModLog.Info("TestHooks: teleport " + dx.ToString("F0") + ", " + dz.ToString("F0") + " m to " + at.ToString("F0") + " (the game finds the floor there)");
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
            return Mathf.RoundToInt(HubGeometry.Flat(troll.transform.position - was));
        }

        /// Flat metres from the troll to the hub it would use (-1 = none).
        [TestHook("stt.hubDistance")]
        static int HubDistance(GameObject troll)
        {
            HubRegistry.All(true);
            var hub = HubRegistry.For(troll.GetComponent<Character>());
            return hub != null ? Mathf.RoundToInt(HubGeometry.Flat(hub.Pos - troll.transform.position)) : -1;
        }

        /// The "walk to the owner's bed" order (the Trolls page / E menu command).
        [TestHook("stt.sendHome")]
        static bool SendHomeOrder(GameObject troll) { return TrollOrderService.Send(new SendHomeCommand(troll.GetComponent<Character>())); }

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
            return BackpackStore.TryAdd(z, Item(prefab, count), null);
        }

        /// Fills every slot of the backpack with Stone stacks. Returns the number of stacks.
        [TestHook("stt.fillBackpack")]
        static int FillBackpack(GameObject troll)
        {
            var z = TrollTaming.Zdo(troll.GetComponent<Character>());
            int n = 0;
            while (n < 500 && BackpackStore.TryAdd(z, Item("Stone", 20), null) >= 0) n++;
            int had; Filled.TryGetValue(z.m_uid, out had); Filled[z.m_uid] = had + n * 20;      // D-174: item conservation (stt.followedConserved)
            return n;
        }

        [TestHook("stt.backpackStacks")]
        static int BackpackStacks(GameObject troll) { int used, slots; BackpackStore.Fill(TrollTaming.Zdo(troll.GetComponent<Character>()), out used, out slots); return used; }

        /// True when the backpack holds at most the given number of stacks. A troll that has unloaded goes back to farming
        /// and fills the pack again, so "exactly 0" is a race; "emptied to a few stacks" is what the scenario means.
        [TestHook("stt.backpackAtMost")]
        static bool BackpackAtMost(GameObject troll, int stacks) { return BackpackStacks(troll) <= stacks; }

        // D-173 / D-174 (review): the far troll crosses zones TrollZones keeps; its GameObject is re-created on the way, so the test follows it by its ZDOID (stable while the
        // object comes and goes) and reads its backpack from that ZDO - loaded or not. Item conservation: what the fill put in = what is still in the backpack + what the
        // owner's depots gained since stt.followTroll (read from the depot chests' ZDOs, so it works with the depot area unloaded too).
        static string _followId = "";
        static ZDOID _follow = ZDOID.None;
        static int _depotStoneAtFollow;
        static readonly Dictionary<ZDOID, int> Filled = new Dictionary<ZDOID, int>();

        /// Follows the troll by its ZDOID (and registry id for the log). Call it after stt.fillBackpack... or before - the depot baseline is taken here. Expected outcome: the id.
        [TestHook("stt.followTroll")]
        static string FollowTroll(GameObject troll)
        {
            var c = troll.GetComponent<Character>();
            _followId = TrollRegistry.IdOf(c);
            var z = TrollTaming.Zdo(c);
            _follow = z != null ? z.m_uid : ZDOID.None;
            _depotStoneAtFollow = DepotQuantity("Stone");
            ModLog.Info("TestHooks: following troll " + _followId + " (ZDO " + _follow + "), depots hold " + _depotStoneAtFollow + " Stone now");
            return _followId;
        }

        static ZDO FollowedZdo() { return _follow != ZDOID.None ? ZDOMan.instance.GetZDO(_follow) : null; }

        /// The followed troll holds at most `stacks` stacks (read from its ZDO, whichever GameObject it has). Expected outcome: false when its ZDO is gone.
        [TestHook("stt.followedBackpackAtMost")]
        static bool FollowedBackpackAtMost(int stacks)
        {
            var z = FollowedZdo();
            if (z == null) return z != null;
            int used, slots; BackpackStore.Fill(z, out used, out slots);
            return used <= stacks;
        }

        /// D-174: one line about the followed troll - loaded or not, ZDO there or not, stacks / slots, distance to its hub. Expected outcome: the text (diagnosis).
        [TestHook("stt.followedState")]
        static string FollowedState()
        {
            var z = FollowedZdo();
            if (z == null) return "followed troll: ZDO gone (" + _follow + ")";
            var c = _followId.Length > 0 ? TrollRegistry.FindLoaded(_followId) : null;
            int used, slots; BackpackStore.Fill(z, out used, out slots);
            var hub = c != null ? HubRegistry.For(c) : null;
            string s = "followed troll " + _followId + ": " + (c != null ? "loaded" : "not loaded") + ", at " + z.GetPosition().ToString("F0") + ", backpack " + used + " of " + slots
                + (hub != null ? ", " + HubGeometry.Flat(hub.Pos - z.GetPosition()).ToString("F0") + " m from " + hub.Name : "");
            ModLog.Info("TestHooks: " + s);
            return s;
        }

        /// D-174: nothing was lost or made: Stone put in by stt.fillBackpack = Stone still in the backpack + Stone the owner's depots gained since stt.followTroll.
        /// Expected outcome: true when conserved (the numbers are logged either way).
        [TestHook("stt.followedConserved")]
        static bool FollowedConserved()
        {
            var z = FollowedZdo();
            if (z == null) return z != null;
            int filled; Filled.TryGetValue(_follow, out filled);
            int inPack = 0;
            for (int i = 0; i < BackpackStore.ChestCount; i++) foreach (var it in BackpackStore.Read(z, i).GetAllItems()) if (it.m_dropPrefab != null && it.m_dropPrefab.name == "Stone") inPack += it.m_stack;
            int gained = DepotQuantity("Stone") - _depotStoneAtFollow;
            ModLog.Info("TestHooks: conservation - filled " + filled + " Stone, backpack " + inPack + ", depots gained " + gained + (filled == inPack + gained ? " - conserved" : " - MISMATCH"));
            return filled > 0 && filled == inPack + gained;
        }

        /// Quantity of `prefab` in every depot chest of this player (from the chests' ZDOs: works where the depot area is not loaded).
        static int DepotQuantity(string prefab)
        {
            var drop = ObjectDB.instance.GetItemPrefab(prefab);
            string shared = drop != null ? drop.GetComponent<ItemDrop>().m_itemData.m_shared.m_name : prefab;
            int n = 0, chests = 0, mine = 0, loaded = 0, fromZdo = 0;
            // loaded depot chests: their live inventory (what TrollDepot.Unload wrote, saved or not)
            var seen = new HashSet<ZDOID>();
            foreach (var c in Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                var v = c != null ? c.GetComponent<ZNetView>() : null;
                if (v == null || !v.IsValid()) continue;
                var z = v.GetZDO();
                if (!z.GetBool(TrollDepot.FlagKey) || z.GetLong(TrollDepot.OwnerKey) != Me) continue;
                seen.Add(z.m_uid); loaded++;
                foreach (var it in c.GetInventory().GetAllItems()) if (it.m_shared.m_name == shared) n += it.m_stack;
            }
            // unloaded ones: the inventory the game saved in the chest's ZDO
            var list = new List<ZDO>(); int idx = 0, guard = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(DepotData.ChestPrefab, list, ref idx) && guard++ < 10000) { }
            foreach (var z in list)
            {
                chests++;
                if (!z.GetBool(TrollDepot.FlagKey) || z.GetLong(TrollDepot.OwnerKey) != Me) continue;
                mine++;
                if (seen.Contains(z.m_uid)) continue;
                string data = z.GetString(ZDOVars.s_items, "");
                if (data.Length == 0) continue;
                var inv = new Inventory("depot", null, 8, 8);
                inv.Load(new ZPackage(data));
                fromZdo++;
                foreach (var it in inv.GetAllItems()) if (it.m_shared.m_name == shared) n += it.m_stack;
            }
            ModLog.Info("TestHooks: depot " + prefab + " " + n + " (chest ZDOs " + chests + ", this player's depot chests " + mine + ", loaded " + loaded + ", read from ZDO " + fromZdo + ")");
            return n;
        }

        /// D-174 (review): the hub exists and is the troll's, and the troll stands `min` .. `max` flat metres from it - a missing hub fails the setup at once.
        [TestHook("stt.hubDistanceInRange")]
        static bool HubDistanceInRange(GameObject troll, float min, float max)
        {
            HubRegistry.All(true);
            var hub = HubRegistry.For(troll.GetComponent<Character>());
            if (hub == null) { ModLog.Info("TestHooks: hubDistanceInRange - the troll has no hub"); return hub != null; }
            float d = HubGeometry.Flat(hub.Pos - troll.transform.position);
            ModLog.Info("TestHooks: hubDistanceInRange - " + d.ToString("F0") + " m from " + hub.Name + " (want " + min.ToString("F0") + " .. " + max.ToString("F0") + ")");
            return d >= min && d <= max;
        }

        /// D-174 (review: manual hub precedence): the owner sends the troll to hub `number` (0 = automatic), as the Troll page does.
        [TestHook("stt.assignHub")]
        static int AssignHub(GameObject troll, int number)
        {
            var z = TrollTaming.Zdo(troll.GetComponent<Character>());
            z.Set(HubRegistry.AssignKey, number);
            return z.GetInt(HubRegistry.AssignKey, 0);
        }

        /// D-174: removes this player's test hub `number` (its ZDO). Expected outcome: how many were removed.
        [TestHook("stt.removeHub")]
        static int RemoveHub(int number)
        {
            int n = 0;
            foreach (var z in new List<ZDO>(FakeHubs))
            {
                if (z == null || !z.IsValid() || z.GetInt(HubRegistry.NumberKey, 0) != number || z.GetLong(HubRegistry.OwnerKey, 0L) != Me) continue;
                z.SetOwner(ZDOMan.GetSessionID()); ZDOMan.instance.DestroyZDO(z); FakeHubs.Remove(z); n++;
            }
            HubRegistry.All(true);
            return n;
        }

        /// D-174 (review: far-search can pass on a nearer real tree): the long-range search finds THIS synthetic tree - every other source is skipped, so a hit is the ZDO-only
        /// tree `dx`,`dz` from the player. Expected outcome: its flat distance, -1 when the search did not find it.
        [TestHook("stt.farFindsSynthetic")]
        static int FarFindsSynthetic(float dx, float dz)
        {
            var p = Player.m_localPlayer;
            var at = p.transform.position + new Vector3(dx, 0f, dz);
            Vector3 pos; string item;
            bool found = TrollFarming.FindFar(p.transform.position, ModSettings.FarmLongRange.Value, ModSettings.FarmMaxRange.Value, 10,
                new[] { new Ui.TrollFocus.Goal("Wood") }, q => HubGeometry.Flat(q - at) > 3f, out pos, out item);
            int d = found ? Mathf.RoundToInt(HubGeometry.Flat(pos - p.transform.position)) : -1;
            ModLog.Info("TestHooks: far search for the synthetic tree at " + at.ToString("F0") + " - " + (found ? "found " + item + " at " + d + " m" : "not found"));
            return d;
        }

        /// D-174: the elemental immunity of a tamed troll (TamedTrollStats): how many of fire / frost / lightning / poison / spirit are Immune. Expected outcome: 5.
        [TestHook("stt.elementalImmune")]
        static int ElementalImmune(GameObject troll)
        {
            var m = troll.GetComponent<Character>().GetDamageModifiers();
            int n = 0;
            if (m.m_fire == HitData.DamageModifier.Immune) n++;
            if (m.m_frost == HitData.DamageModifier.Immune) n++;
            if (m.m_lightning == HitData.DamageModifier.Immune) n++;
            if (m.m_poison == HitData.DamageModifier.Immune) n++;
            if (m.m_spirit == HitData.DamageModifier.Immune) n++;
            return n;
        }

        /// D-174 (review: reset isolation): tamed trolls within `radius` m of the player right now (after stt.reset this must be 0).
        [TestHook("stt.tamedTrollsNear")]
        static int TamedTrollsNear(float radius)
        {
            var p = Player.m_localPlayer; int n = 0;
            foreach (var c in Character.GetAllCharacters()) if (c != null && !c.IsDead() && c.IsTamed() && TrollTaming.IsTroll(c) && HubGeometry.Flat(c.transform.position - p.transform.position) <= radius) n++;
            return n;
        }

        /// D-174: the static tracking state a test can leave behind (followed id, marks, fill counts) - "" after stt.reset.
        [TestHook("stt.trackingState")]
        static string TrackingState() { return (_followId.Length > 0 ? "follow " + _followId + " " : "") + (Marks.Count > 0 ? "marks " + Marks.Count + " " : "") + (Filled.Count > 0 ? "filled " + Filled.Count : ""); }

        [TestHook("stt.backpackSlots")]
        static int BackpackSlots(GameObject troll) { int used, slots; BackpackStore.Fill(TrollTaming.Zdo(troll.GetComponent<Character>()), out used, out slots); return slots; }

        [TestHook("stt.backpackAllUsed")]
        static bool BackpackAllUsed(GameObject troll)
        {
            int used, slots; BackpackStore.Fill(TrollTaming.Zdo(troll.GetComponent<Character>()), out used, out slots);
            ModLog.Info("TestHooks: backpack of " + troll.name + " - " + used + " of " + slots + " slot(s) used");      // D-172: the failing run said only 'False'
            return slots > 0 && used == slots;
        }

        /// Total quantity of an item over all four chests, read back from the ZDO.
        [TestHook("stt.backpackCount")]
        static int BackpackCount(GameObject troll, string prefab)
        {
            var z = TrollTaming.Zdo(troll.GetComponent<Character>());
            string shared = ObjectDB.instance.GetItemPrefab(prefab).GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
            int n = 0;
            for (int i = 0; i < BackpackStore.ChestCount; i++)
                foreach (var it in BackpackStore.Read(z, i).GetAllItems()) if (it.m_shared.m_name == shared) n += it.m_stack;
            return n;
        }

        /// The (uncached) full state and the cached one - they must agree right after a change.
        [TestHook("stt.backpackFull")]
        static bool BackpackFull(GameObject troll) { return BackpackStore.IsFull(TrollTaming.Zdo(troll.GetComponent<Character>())); }

        [TestHook("stt.backpackFullCached")]
        static bool BackpackFullCached(GameObject troll) { return BackpackStore.IsFullCached(TrollTaming.Zdo(troll.GetComponent<Character>())); }

        /// Takes everything out of chest 0 through the real store (Read / Write).
        [TestHook("stt.unpackChest0")]
        static int UnpackChest0(GameObject troll)
        {
            var z = TrollTaming.Zdo(troll.GetComponent<Character>());
            var inv = BackpackStore.Read(z, 0);
            int n = inv.NrOfItems();
            foreach (var it in new List<ItemDrop.ItemData>(inv.GetAllItems())) inv.RemoveItem(it);
            BackpackStore.Write(z, 0, inv);
            return n;
        }

        /// D-173: the whole storage / cache cycle of backpack-full-cache IN ONE FRAME - fill, full, cached full, every slot used, a write through the real store, cache dropped.
        /// Between separate lab steps the troll's own AI may unload (D-173: a full troll near home unloads there), which is the plugin working, not a storage fault - so the
        /// storage test does not leave it a frame. Expected outcome: "ok", else the first check that failed with its numbers.
        [TestHook("stt.backpackCacheCycle")]
        static string BackpackCacheCycle(GameObject troll)
        {
            var z = TrollTaming.Zdo(troll.GetComponent<Character>());
            if (z == null) return "no ZDO";
            if (BackpackStore.IsFullCached(z)) return "cached full before the fill";
            int added = FillBackpack(troll);
            if (!BackpackStore.IsFull(z)) return "not full after " + added + " stack(s)";
            if (!BackpackStore.IsFullCached(z)) return "cache says not full right after the fill";
            int used, slots; BackpackStore.Fill(z, out used, out slots);
            if (slots <= 0 || used != slots) return "slots used " + used + " of " + slots;
            int moved = UnpackChest0(troll);
            if (moved <= 0) return "chest 0 was empty";
            if (BackpackStore.IsFullCached(z)) return "cache not dropped by the write";
            ModLog.Info("TestHooks: backpack cache cycle ok - " + added + " stack(s) added, " + used + " of " + slots + " slots, " + moved + " taken out of chest 1, cache dropped");
            return "ok";
        }

        /// What the troll is doing now: farming / unloading / free, its hub and the distance to it.
        [TestHook("stt.state")]
        static string State(GameObject troll)
        {
            var c = troll.GetComponent<Character>();
            var f = troll.GetComponent<TrollFarmer>();
            var hub = HubRegistry.For(c);
            return (f == null ? "free" : f.Unloading ? "unloading" : "farming") + ", hub " + (hub != null ? hub.Number.ToString() : "none")
                + (hub != null ? " " + HubGeometry.Flat(hub.Pos - troll.transform.position).ToString("F0") + " m" : "") + ", full " + BackpackStore.IsFull(TrollTaming.Zdo(c));
        }

        /// The troll starts farming on its own (an active TrollFarmer: with nothing to farm it stays active and waits).
        [TestHook("stt.startFarm")]
        static bool StartFarm(GameObject troll) { return TrollOrderService.Send(new FarmFocusCommand(troll.GetComponent<Character>(), "", true)); }

        /// D-174 (review: "Unloading" = home trip because the backpack is full, NOT proof of an unload): the troll is on its way to unload.
        [TestHook("stt.returningToUnload")]
        static bool ReturningToUnload(GameObject troll) { var f = troll.GetComponent<TrollFarmer>(); return f != null && f.Unloading; }

        // ---- long-range farming and the remembered spot (user 2026-10-31) -------------------------------------------------------------------------------

        static readonly List<ZDO> FakeTrees = new List<ZDO>();
        static Vector3 _spotTarget;

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
            return found ? Mathf.RoundToInt(HubGeometry.Flat(pos - p.transform.position)) + " m " + item : "none";
        }

        /// Flat metres to the nearest long-range wood source (-1 = none). The lab world has real trees, so suites assert limits, not exact values.
        [TestHook("stt.farMetres")]
        static int FarMetres()
        {
            var p = Player.m_localPlayer;
            Vector3 pos; string item;
            bool found = TrollFarming.FindFar(p.transform.position, ModSettings.FarmLongRange.Value, ModSettings.FarmMaxRange.Value, 10,
                new[] { new Ui.TrollFocus.Goal("Wood") }, null, out pos, out item);
            return found ? Mathf.RoundToInt(HubGeometry.Flat(pos - p.transform.position)) : -1;
        }

        /// The troll remembers a farm spot `dx`,`dz` metres from the player, as if it had broken a tree there.
        [TestHook("stt.rememberSpot")]
        static bool RememberSpot(GameObject troll, float dx, float dz)
        {
            var f = troll.GetComponent<TrollFarmer>();
            if (f == null) return false;
            var at = Player.m_localPlayer.transform.position + new Vector3(dx, 0f, dz);
            _spotTarget = at;
            return f.Spots.Remember(at);
        }

        /// Flat metres from the troll to its remembered spot (-1 = none).
        [TestHook("stt.spotDistance")]
        static int SpotDistance(GameObject troll)
        {
            var f = troll.GetComponent<TrollFarmer>();
            if (f == null) return -2;
            Vector3 s;
            if (!f.Spots.Spot(out s)) return -1;
            s.y = troll.transform.position.y;
            return Mathf.RoundToInt(Vector3.Distance(s, troll.transform.position));
        }

        [TestHook("stt.travelling")]
        static bool Travelling(GameObject troll) { var f = troll.GetComponent<TrollFarmer>(); return f != null && f.Travelling; }

        // ---- depot -------------------------------------------------------------------------------------------------------------------------------------

        [TestHook("stt.spawnDepot")]
        static GameObject SpawnDepot(Vector3 position)
        {
            var frame = TrollDepot.Place(position, Quaternion.identity, Me);
            if (frame == null) throw new System.Exception("depot prefab missing");
            return frame;
        }

        [TestHook("stt.depotChests")]
        static int DepotChests(GameObject frame) { return TrollDepot.ChestsOf(frame).Count; }

        /// Items in chest `index` of the depot (the chests are the real Containers).
        [TestHook("stt.depotPut")]
        static bool DepotPut(GameObject frame, int index, string prefab, int count)
        {
            var c = TrollDepot.ChestsOf(frame);
            return index < c.Count && c[index].GetInventory().AddItem(Item(prefab, count));
        }

        [TestHook("stt.depotTake")]
        static int DepotTake(GameObject frame, int index = -1)
        {
            int n = 0, i = -1;
            foreach (var c in TrollDepot.ChestsOf(frame))
            {
                i++;
                if (index >= 0 && i != index) continue;
                var inv = c.GetInventory();
                foreach (var it in new List<ItemDrop.ItemData>(inv.GetAllItems())) { inv.RemoveItem(it); n++; }
            }
            return n;
        }

        /// D-174 (review: depot fallback in a live world): every chest of the depot filled to the last slot with Wood. Expected outcome: stacks added.
        [TestHook("stt.depotFill")]
        static int DepotFill(GameObject frame)
        {
            int n = 0;
            foreach (var c in TrollDepot.ChestsOf(frame))
            {
                var v = c.GetComponent<ZNetView>(); if (v != null && !v.IsOwner()) v.ClaimOwnership();
                while (c.GetInventory().HaveEmptySlot() && n < 1000) { c.GetInventory().AddItem(Item("Wood", 50)); n++; }
            }
            return n;
        }

        /// D-174 (review: spot-return must COMPLETE): the troll stands within `metres` of its remembered farm spot. Expected outcome: false without a farmer / spot.
        [TestHook("stt.reachedSpot")]
        static bool ReachedSpot(GameObject troll, float metres)
        {
            if (_spotTarget == Vector3.zero) return _spotTarget != Vector3.zero;
            float d = HubGeometry.Flat(_spotTarget - troll.transform.position);
            if (d <= metres) ModLog.Info("TestHooks: the troll reached its remembered spot (" + d.ToString("F1") + " m)");
            return d <= metres;
        }

        [TestHook("stt.depotItems")]
        static int DepotItems(GameObject frame) { int n = 0; foreach (var c in TrollDepot.ChestsOf(frame)) n += c.GetInventory().NrOfItems(); return n; }

        /// A player's chest window opening / closing (Container.SetInUse, the same call the game makes).
        [TestHook("stt.depotWindow")]
        static string DepotWindow(GameObject frame, bool open)
        {
            var c = TrollDepot.ChestsOf(frame);
            if (c.Count == 0) throw new System.Exception("no chests");
            HarmonyLib.AccessTools.Method(typeof(Container), "SetInUse").Invoke(c[0], new object[] { open });
            return open ? "open" : "closed";
        }

        [TestHook("stt.depotReserve")]
        static string DepotReserve(GameObject frame)
        {
            foreach (var c in TrollDepot.ChestsOf(frame)) TrollDepot.ReserveForTest(c);
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
                if (v != null && v.IsValid() && Utils.GetPrefabName(v.gameObject) == DepotPrefab.Name) n++;
            return n;
        }

        // ---- hubs --------------------------------------------------------------------------------------------------------------------------------------

        /// A hub of the player at a place relative to the player (no building: only its ZDO, which is all the registry reads).
        [TestHook("stt.addHub")]
        static int AddHub(int number, float dx, float dz, string owner)
        {
            var p = Player.m_localPlayer;
            long who = owner == "other" ? Me + 777 : Me;
            var z = ZDOMan.instance.CreateNewZDO(p.transform.position + new Vector3(dx, 0f, dz), HubData.PrefabName.GetStableHashCode());
            z.SetPrefab(HubData.PrefabName.GetStableHashCode());      // CreateNewZDO ignores its prefab argument in this game version
            z.Persistent = true;      // D-173 (run 161451): a raw ZDO is not persistent - when the player went 400 m away its zone unloaded and the game DELETED the test hub ("0 Troll Hub(s) known")
            z.Set(HubRegistry.OwnerKey, who);
            z.Set(HubRegistry.OwnerNameKey, owner == "other" ? "Other" : p.GetPlayerName());
            z.Set(HubRegistry.NumberKey, number);            // D-174 (review): the old "z.Persistent = false" here undid the line above - removed
            FakeHubs.Add(z);
            int known = HubRegistry.All(true).Count;
            var scan = new List<ZDO>(); int idx = 0, guard = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(HubData.PrefabName, scan, ref idx) && guard++ < 10000) { }
            ModLog.Info("TestHooks: addHub " + number + " owner " + who + " prefab " + z.GetPrefab() + " (want " + HubData.PrefabName.GetStableHashCode() + "), no " + z.GetInt(HubRegistry.NumberKey, -1) + ", sector " + z.GetSector() + ", registry knows " + known + ", raw scan finds " + scan.Count + ", ZDOMan.GetZDO " + (ZDOMan.instance.GetZDO(z.m_uid) != null));
            return known;
        }

        [TestHook("stt.clearHubs")]
        static int ClearHubs()
        {
            int n = FakeHubs.Count;
            foreach (var z in FakeHubs) { z.SetOwner(ZDOMan.GetSessionID()); ZDOMan.instance.DestroyZDO(z); }
            FakeHubs.Clear();
            HubRegistry.All(true);
            return n;
        }

        /// The hub number the troll would use now (0 = the bed).
        [TestHook("stt.hubFor")]
        static int HubFor(GameObject troll)
        {
            HubRegistry.All(true);
            var h = HubRegistry.For(troll.GetComponent<Character>());
            return h != null ? h.Number : 0;
        }

        // ---- walk back to the hub (D-162) --------------------------------------------------------------------------------------------------------------

        /// The idle check's walk-back decision now: 1 = the troll walks back (already or newly), 0 = not.
        [TestHook("stt.returnCheck")]
        static int ReturnCheck(GameObject troll)
        {
            HubRegistry.All(true);
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
            HubRegistry.All(true);
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
    }
}
