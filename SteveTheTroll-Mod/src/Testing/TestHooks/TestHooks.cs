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

/// <summary>
///     D-160: hooks for DrakesTestLab suites (Tests/suites/SteveTheTroll). Actions call the REAL mod code
///     (TrollDepot.Place, BackpackStore, HubRegistry, RuntimeHealth); reads return short,
///     stable strings / numbers. They only exist so that the suites can set up a world; nothing here runs unless a test
///     calls it.
/// </summary>
internal static partial class TestHooks
{
    // D-172 (user 2026-11-02: "spawn trolls further from me (not behind me) so that they don't walk into player"): at least 16 m (was 12) and in FRONT of the player
    // (stt.reset faces the player along world +z, so a test's dz > 0 is in front). A troll offset behind the player fails the step loudly instead of being moved silently.
    private const float MinFromPlayer = 16f;
    private static readonly List<ZDO> FakeHubs = new();

    private static readonly HashSet<ZDOID> Made = new();

    private static readonly Dictionary<int, Vector3> Marks = new();

    // D-173 / D-174 (review): the far troll crosses zones TrollZones keeps; its GameObject is re-created on the way, so the test follows it by its ZDOID (stable while the
    // object comes and goes) and reads its backpack from that ZDO - loaded or not. Item conservation: what the fill put in = what is still in the backpack + what the
    // owner's depots gained since stt.followTroll (read from the depot chests' ZDOs, so it works with the depot area unloaded too).
    private static string _followId = "";
    private static ZDOID _follow = ZDOID.None;
    private static int _depotStoneAtFollow;
    private static readonly Dictionary<ZDOID, int> Filled = new();

    // ---- long-range farming and the remembered spot (user 2026-10-31) -------------------------------------------------------------------------------

    private static readonly List<ZDO> FakeTrees = new();
    private static Vector3 _spotTarget;
    private static long Me => Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L;

    // ---- world setup -------------------------------------------------------------------------------------------------------------------------------

    /// The player is moved to the test spot (TestArena), the bed is put there (trolls and depots use it), hubs of earlier tests are gone.
    [TestHook("stt.reset")]
    private static string Reset()
    {
        Player p = Player.m_localPlayer;
        if (p == null)
        {
            return "no player";
        }

        ArenaTestHooks
            .MovePlayer(p); // D-171 (user): every test starts on the same open spot, 70 m from the lab start (trophy stones, house)
        int trolls =
            CombatTestHooks
                .ClearTrolls(); // D-174 (review): every test starts without test / arena trolls (17 tests never called stt.clearTrolls)
        Marks.Clear();
        _spotTarget = Vector3.zero;
        _followId = "";
        _follow = ZDOID.None;
        Filled.Clear();
        _depotStoneAtFollow = 0;
        Game.instance.GetPlayerProfile().SetCustomSpawnPoint(p.transform.position);
        ClearHubs();
        ClearFarTrees();
        RecoveryTestHooks.ClearWalled(); // D-167: the walled tree of the recovery scenario
        FarmingTestHooks.ClearPlanted(); // D-170: the trees of farm-chops-tree
        int gone = 0;
        foreach (ZNetView v in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
        {
            if (v == null || !v.IsValid())
            {
                continue;
            }

            string n = Utils.GetPrefabName(v.gameObject);
            bool depot = n == DepotPrefab.Name ||
                         (n == DepotData.ChestPrefab && v.GetZDO().GetBool(TrollDepot.FlagKey));
            if (depot)
            {
                v.ClaimOwnership();
                ZNetScene.instance.Destroy(v.gameObject);
                gone++;
            }
        }

        return "bed at " + p.transform.position.ToString("F0") + ", " + gone + " depot piece(s) removed, " + trolls +
               " troll(s) removed";
    }

    /// D-171 (user: "sometimes troll walks into me ... spawn him further from player"): a tamed troll `dx`,`dz` metres from the player AS IT STANDS NOW (the lab's own
    /// position argument was relative to the test's start, so after stt.arenaFurther the troll appeared 100 m back at the bed), pushed out to at least MinFromPlayer m.
    [TestHook("stt.spawnTroll")]
    private static GameObject SpawnTroll(float dx, float dz)
    {
        return Spawn(dx, dz, MinFromPlayer);
    }

    /// Like stt.spawnTroll but as close as asked (only for tests about a troll right next to the player: hold-look, defend-rage, nohit-zone) - still in front.
    [TestHook("stt.spawnTrollNear")]
    private static GameObject SpawnTrollNear(float dx, float dz)
    {
        return Spawn(dx, dz, 0f);
    }

    private static GameObject Spawn(float dx, float dz, float minMetres)
    {
        Player p = Player.m_localPlayer;
        var off = new Vector3(dx, 0f, dz);
        Vector3 fwd = p.GetLookDir();
        fwd.y = 0f;
        if (Vector3.Dot(off, fwd) <= 0f)
        {
            throw new Exception("troll offset (" + dx + ", " + dz + ") is not in front of the player (facing " +
                                fwd.normalized.ToString("F2") + ") - spawn trolls in front (D-172)");
        }

        if (off.magnitude < minMetres)
        {
            off = (off.sqrMagnitude > 0.01f ? off.normalized : -p.transform.forward) * minMetres;
        }

        Vector3 at = p.transform.position + off;
        at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.3f;
        ModLog.Info("TestHooks: troll spawned " + off.magnitude.ToString("F1") + " m from the player at " +
                    at.ToString("F0") + (off.magnitude > new Vector2(dx, dz).magnitude + 0.1f
                        ? " (pushed out to " + minMetres + " m)"
                        : ""));
        return SpawnTrollAt(at);
    }

    /// D-172: trolls a test hook made (stt.clearTrolls removes these wherever they walked).
    internal static bool MadeByTest(ZDOID id)
    {
        return Made.Contains(id);
    }

    private static GameObject SpawnTrollAt(Vector3 position)
    {
        GameObject prefab = ZNetScene.instance.GetPrefab(TrollTaming.TrollPrefab);
        if (prefab == null)
        {
            throw new Exception("Troll prefab missing");
        }

        GameObject go = Object.Instantiate(prefab, position, Quaternion.identity);
        Tameable tame = go.GetComponent<Tameable>();
        if (tame == null)
        {
            throw new Exception("the troll has no Tameable (taming not installed)");
        }

        AccessTools.Method(typeof(Tameable), "Tame").Invoke(tame, null);
        Character c = go.GetComponent<Character>();
        if (!TrollTaming.SetOwner(c, Player.m_localPlayer))
        {
            throw new Exception("owner not set");
        }

        ZNetView mv = go.GetComponent<ZNetView>();
        if (mv != null && mv.IsValid())
        {
            Made.Add(mv.GetZDO().m_uid);
        }

        return go;
    }

    /// A tamed troll `dx`,`dz` metres from the player, set on the ground there (far spawns: the player's own height may be far from the ground at that place).
    [TestHook("stt.spawnTrollFar")]
    private static GameObject SpawnTrollFar(float dx, float dz)
    {
        Player p = Player.m_localPlayer;
        Vector3 at = p.transform.position + new Vector3(dx, 0f, dz);
        at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.5f;
        return SpawnTrollAt(at);
    }

    /// Moves a troll `dx`,`dz` metres from the player onto the ground there (a troll made far away is removed by the game at once; one that is moved there is kept by TrollZones, like a real one).
    [TestHook("stt.moveTrollFar")]
    private static string MoveTrollFar(GameObject troll, float dx, float dz)
    {
        Player p = Player.m_localPlayer;
        Vector3 at = p.transform.position + new Vector3(dx, 0f, dz);
        at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.5f;
        troll.transform.position = at;
        return at.ToString("F0");
    }

    /// The player teleports `dx`,`dz` metres from where they stand (the game's own TeleportTo: zones load around the new place). A troll made far from the player is removed by the game, so
    /// "a troll 400 m from its hub" is made by taking the player there and spawning the troll next to them.
    [TestHook("stt.teleport")]
    private static string Teleport(float dx, float dz)
    {
        Player p = Player.m_localPlayer;
        Vector3 at = p.transform.position + new Vector3(dx, 0f, dz);
        at.y = Mathf.Max(ZoneSystem.instance.GetGroundHeight(at), p.transform.position.y) + 1f;
        // D-173 (run 154147: far-troll-goes-home spawned its troll at the BED): the target 400 m away is not loaded, so its ground height read 0, the player arrived inside the
        // ground and the game put it back at the spawn point. distantTeleport = true: the game finds the floor itself once the area is loaded.
        p.TeleportTo(at, p.transform.rotation, true);
        ModLog.Info("TestHooks: teleport " + dx.ToString("F0") + ", " + dz.ToString("F0") + " m to " +
                    at.ToString("F0") + " (the game finds the floor there)");
        return at.ToString("F0");
    }

    /// Remembers where the troll stands now (see stt.moved).
    [TestHook("stt.mark")]
    private static string Mark(GameObject troll)
    {
        Marks[troll.GetInstanceID()] = troll.transform.position;
        return troll.transform.position.ToString("F0");
    }

    /// Flat metres the troll has moved since stt.mark.
    [TestHook("stt.moved")]
    private static int Moved(GameObject troll)
    {
        Vector3 was;
        if (!Marks.TryGetValue(troll.GetInstanceID(), out was))
        {
            throw new Exception("stt.mark was not called");
        }

        return Mathf.RoundToInt(HubGeometry.Flat(troll.transform.position - was));
    }

    /// Flat metres from the troll to the hub it would use (-1 = none).
    [TestHook("stt.hubDistance")]
    private static int HubDistance(GameObject troll)
    {
        HubRegistry.All(true);
        HubInfo hub = HubRegistry.For(troll.GetComponent<Character>());
        return hub != null ? Mathf.RoundToInt(HubGeometry.Flat(hub.Pos - troll.transform.position)) : -1;
    }

    /// The "walk to the owner's bed" order (the Trolls page / E menu command).
    [TestHook("stt.sendHome")]
    private static bool SendHomeOrder(GameObject troll)
    {
        return TrollOrderService.Send(new SendHomeCommand(troll.GetComponent<Character>()));
    }

    [TestHook("stt.moveTroll")]
    private static string MoveTroll(GameObject troll, Vector3 position)
    {
        if (troll == null)
        {
            throw new Exception("no troll");
        }

        troll.transform.position = position;
        return position.ToString("F0");
    }

    // ---- backpack ------------------------------------------------------------------------------------------------------------------------------------

    private static ItemDrop.ItemData Item(string prefab, int count)
    {
        GameObject p = ObjectDB.instance.GetItemPrefab(prefab);
        if (p == null)
        {
            throw new Exception("item " + prefab + " not found");
        }

        ItemDrop.ItemData it = p.GetComponent<ItemDrop>().m_itemData.Clone();
        it.m_dropPrefab =
            p; // a prefab's template item has none; without it Inventory.Save writes "" and Load drops the stack
        it.m_stack = count;
        it.m_quality = 1;
        it.m_durability = it.GetMaxDurability();
        return it;
    }

    /// Puts one stack into the first backpack chest with room, through the real store. Returns the chest index (-1 = full).
    [TestHook("stt.pack")]
    private static int Pack(GameObject troll, string prefab, int count)
    {
        ZDO z = TrollTaming.Zdo(troll.GetComponent<Character>());
        return BackpackStore.TryAdd(z, Item(prefab, count), null);
    }

    /// Fills every slot of the backpack with Stone stacks. Returns the number of stacks.
    [TestHook("stt.fillBackpack")]
    private static int FillBackpack(GameObject troll)
    {
        ZDO z = TrollTaming.Zdo(troll.GetComponent<Character>());
        int n = 0;
        while (n < 500 && BackpackStore.TryAdd(z, Item("Stone", 20), null) >= 0)
        {
            n++;
        }

        int had;
        Filled.TryGetValue(z.m_uid, out had);
        Filled[z.m_uid] = had + n * 20; // D-174: item conservation (stt.followedConserved)
        return n;
    }

    [TestHook("stt.backpackStacks")]
    private static int BackpackStacks(GameObject troll)
    {
        int used, slots;
        BackpackStore.Fill(TrollTaming.Zdo(troll.GetComponent<Character>()), out used, out slots);
        return used;
    }

    /// True when the backpack holds at most the given number of stacks. A troll that has unloaded goes back to farming
    /// and fills the pack again, so "exactly 0" is a race; "emptied to a few stacks" is what the scenario means.
    [TestHook("stt.backpackAtMost")]
    private static bool BackpackAtMost(GameObject troll, int stacks)
    {
        return BackpackStacks(troll) <= stacks;
    }

    /// Follows the troll by its ZDOID (and registry id for the log). Call it after stt.fillBackpack... or before - the depot baseline is taken here. Expected outcome: the id.
    [TestHook("stt.followTroll")]
    private static string FollowTroll(GameObject troll)
    {
        Character c = troll.GetComponent<Character>();
        _followId = TrollRegistry.IdOf(c);
        ZDO z = TrollTaming.Zdo(c);
        _follow = z != null ? z.m_uid : ZDOID.None;
        _depotStoneAtFollow = DepotQuantity("Stone");
        ModLog.Info("TestHooks: following troll " + _followId + " (ZDO " + _follow + "), depots hold " +
                    _depotStoneAtFollow + " Stone now");
        return _followId;
    }

    private static ZDO FollowedZdo()
    {
        return _follow != ZDOID.None ? ZDOMan.instance.GetZDO(_follow) : null;
    }

    /// The followed troll holds at most `stacks` stacks (read from its ZDO, whichever GameObject it has). Expected outcome: false when its ZDO is gone.
    [TestHook("stt.followedBackpackAtMost")]
    private static bool FollowedBackpackAtMost(int stacks)
    {
        ZDO z = FollowedZdo();
        if (z == null)
        {
            return z != null;
        }

        int used, slots;
        BackpackStore.Fill(z, out used, out slots);
        return used <= stacks;
    }

    /// D-174: one line about the followed troll - loaded or not, ZDO there or not, stacks / slots, distance to its hub. Expected outcome: the text (diagnosis).
    [TestHook("stt.followedState")]
    private static string FollowedState()
    {
        ZDO z = FollowedZdo();
        if (z == null)
        {
            return "followed troll: ZDO gone (" + _follow + ")";
        }

        Character c = _followId.Length > 0 ? TrollRegistry.FindLoaded(_followId) : null;
        int used, slots;
        BackpackStore.Fill(z, out used, out slots);
        HubInfo hub = c != null ? HubRegistry.For(c) : null;
        string s = "followed troll " + _followId + ": " + (c != null ? "loaded" : "not loaded") + ", at " +
                   z.GetPosition().ToString("F0") + ", backpack " + used + " of " + slots
                   + (hub != null
                       ? ", " + HubGeometry.Flat(hub.Pos - z.GetPosition()).ToString("F0") + " m from " + hub.Name
                       : "");
        ModLog.Info("TestHooks: " + s);
        return s;
    }

    /// D-174: nothing was lost or made: Stone put in by stt.fillBackpack = Stone still in the backpack + Stone the owner's depots gained since stt.followTroll.
    /// Expected outcome: true when conserved (the numbers are logged either way).
    [TestHook("stt.followedConserved")]
    private static bool FollowedConserved()
    {
        ZDO z = FollowedZdo();
        if (z == null)
        {
            return z != null;
        }

        int filled;
        Filled.TryGetValue(_follow, out filled);
        int inPack = 0;
        for (int i = 0; i < BackpackStore.ChestCount; i++)
        {
            foreach (ItemDrop.ItemData it in BackpackStore.Read(z, i).GetAllItems())
            {
                if (it.m_dropPrefab != null && it.m_dropPrefab.name == "Stone")
                {
                    inPack += it.m_stack;
                }
            }
        }

        int gained = DepotQuantity("Stone") - _depotStoneAtFollow;
        ModLog.Info("TestHooks: conservation - filled " + filled + " Stone, backpack " + inPack + ", depots gained " +
                    gained + (filled == inPack + gained ? " - conserved" : " - MISMATCH"));
        return filled > 0 && filled == inPack + gained;
    }
}
