using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using KydlCore.Common;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Orders.Commands;
using SteveTheTroll.Features.Trolls.Recovery.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
///     D-167: TestLab hooks for the stuck detection and recovery (suite recovery-walled-tree): a tree inside a closed ring
///     of player-built walls. Nothing a troll may attack stands in
///     its way, and the game's path search finds no way in, so the troll walks into the ring, gets stuck, must walk
///     around, fail two complete cycles and give the tree up for 120 s.
///     The hooks call the real mod code; they only build the scene and read results.
/// </summary>
internal static class RecoveryTestHooks
{
    private static readonly List<GameObject> Built = new();
    private static readonly List<GameObject> Walls = new();
    private static GameObject _tree;
    private static Vector3 _centre;

    private static float _radius;

    // D-172: Piece.IsPlacedByPlayer() reads the creator the Piece cached in Awake (m_creator), not the ZDO - writing only the ZDO left the ring walls "not player-built",
    // the troll raged at them and felled the walled tree (recovery-walled-tree, 2026-11-02). Both are set now.
    private static readonly AccessTools.FieldRef<Piece, long> CreatorField =
        AccessTools.FieldRefAccess<Piece, long>("m_creator");

    // D-173 (user 2026-11-02, falling logs: "Logs may damage" - only the troll's own swings must never damage a player's piece): every hit that reaches a ring wall's
    // RPC_Damage (where the damage is applied) is counted by its source; PlayerPieceGuard drops troll hits before that, so a troll count above 0 means a hit got through.
    private static readonly HashSet<int> WallIds = new();
    private static int _trollHits, _otherHits;
    private static string _lastTrollHit = "";
    private static long Me => Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L;

    /// A Beech tree `dx`,`dz` metres from the player inside a closed ring of player-built stone walls of the given radius. Expected outcome: the tree (bind it with "as").
    [TestHook("stt.walledTree")]
    private static GameObject WalledTree(float dx, float dz, float radius)
    {
        ClearWalled();
        Player p = Player.m_localPlayer;
        Vector3 centre = p.transform.position + new Vector3(dx, 0f, dz);
        centre.y = ZoneSystem.instance.GetGroundHeight(centre);
        GameObject treePrefab = ZNetScene.instance.GetPrefab("Beech1");
        GameObject wallPrefab = ZNetScene.instance.GetPrefab("stone_wall_4x2");
        if (treePrefab == null || wallPrefab == null)
        {
            throw new Exception("Beech1 or stone_wall_4x2 prefab missing");
        }

        GameObject tree = Object.Instantiate(treePrefab, centre, Quaternion.identity);
        Built.Add(tree);
        _tree = tree;
        _centre = centre;
        _radius = radius;
        int n = Mathf.CeilToInt(2f * Mathf.PI * radius /
                                3.2f); // the wall is 4 m long along its local X: forward = radial puts that axis along the tangent
        // D-172 (run 141510: the walls stood, none was hit, yet the troll reached the tree - it got over / under the ring where the ground slopes): each wall sits at the
        // LOWEST ground of its 4 m length (no gap under the downhill end) and the ring is TWO walls high (4 m), so the walled tree cannot be reached without breaking a wall.
        for (int k = 0; k < 2 * n; k++)
        {
            int i = k % n, layer = k / n;
            float a = i * 2f * Mathf.PI / n;
            var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var tangent = new Vector3(-radial.z, 0f, radial.x);
            Vector3 at = centre + radial * radius;
            float low = Mathf.Min(ZoneSystem.instance.GetGroundHeight(at),
                Mathf.Min(ZoneSystem.instance.GetGroundHeight(at + tangent * 2f),
                    ZoneSystem.instance.GetGroundHeight(at - tangent * 2f)));
            at.y = low - 0.3f + layer * 2f;
            GameObject wall = Object.Instantiate(wallPrefab, at, Quaternion.LookRotation(radial));
            ZNetView view = wall.GetComponent<ZNetView>();
            Piece piece = wall.GetComponent<Piece>();
            long creator = Me != 0L ? Me : 4711L; // 0 would mean "not built by a player"
            if (view == null || !view.IsValid())
            {
                ModLog.Warn("RecoveryTestHooks: wall " + i + " has no valid ZNetView - creator not set in the ZDO");
            }
            else
            {
                view.GetZDO().Set(ZDOVars.s_creator, creator);
            }

            if (piece != null)
            {
                CreatorField(piece) = creator;
            }

            // D-172 (run 143014: 5 of 24 walls lost health with no troll swing at a wall in the log - the second layer wore for lack of support): the ring tests the
            // troll, not the game's building physics, so the test walls do not wear from missing support / rain
            WearNTear wnt = wall.GetComponent<WearNTear>();
            if (wnt != null)
            {
                wnt.m_noSupportWear = false;
                wnt.m_noRoofWear = false;
            }

            if (k == 0) // one line is enough, they are all built the same way
            {
                ModLog.Info("RecoveryTestHooks: wall 0 creator in ZDO " + (view != null && view.IsValid()
                                                                            ? view.GetZDO().GetLong(ZDOVars.s_creator)
                                                                                .ToString()
                                                                            : "no valid ZDO")
                                                                        + ", Piece found " + (piece != null) +
                                                                        ", GetCreator " +
                                                                        (piece != null
                                                                            ? piece.GetCreator().ToString()
                                                                            : "-") + ", IsPlacedByPlayer " +
                                                                        (piece != null && piece.IsPlacedByPlayer()));
            }

            Walls.Add(wall);
            Built.Add(wall);
            WallIds.Add(wall.GetInstanceID());
        }

        ModLog.Info("RecoveryTestHooks: walled tree created, " + TreeHealth(tree));
        ModLog.Info("RecoveryTestHooks: tree at " + centre.ToString("F0") + " inside a ring of " + n +
                    " x 2 player-built walls, radius " + radius.ToString("F0") + " m, creator " +
                    (Me != 0L ? Me : 4711L));
        return tree;
    }

    /// D-171: how many walls of the ring still stand (diagnosis of the flaky runs where the troll walked straight through the ring). Expected outcome: 12 right after walledTree.
    [TestHook("stt.wallsAlive")]
    private static int WallsAlive()
    {
        int n = 0;
        foreach (GameObject w in Walls)
        {
            if (w != null && w.activeInHierarchy)
            {
                n++;
            }
        }

        ModLog.Info("RecoveryTestHooks: " + n + " of " + Walls.Count + " ring walls stand");
        return n;
    }

    /// Removes the tree and the walls of walledTree. Expected outcome: how many objects were removed.
    [TestHook("stt.clearWalled")]
    internal static int ClearWalled()
    {
        int n = 0;
        foreach (GameObject g in Built)
        {
            if (g == null)
            {
                continue;
            }

            ZNetView v = g.GetComponent<ZNetView>();
            if (v != null && v.IsValid())
            {
                v.ClaimOwnership();
                ZNetScene.instance.Destroy(g);
            }
            else
            {
                Object.Destroy(g);
            }

            n++;
        }

        Built.Clear();
        Walls.Clear();
        _tree = null;
        WallIds.Clear();
        _trollHits = 0;
        _otherHits = 0;
        _lastTrollHit = "";
        return n;
    }

    /// The troll farms exactly this source (the real TrollFarming.BeginOn). Expected outcome: true when it started.
    [TestHook("stt.farmOn")]
    private static bool FarmOn(GameObject troll, GameObject source)
    {
        return TrollOrderService.Send(new BreakSourceCommand(troll.GetComponent<Character>(), source, ""));
    }

    /// Resource-specific blacklist of the real RecoveryBook for this troll + this source.
    [TestHook("stt.blacklisted")]
    private static bool Blacklisted(GameObject troll, GameObject source)
    {
        return TrollRecovery.IsBlacklisted(troll.GetComponent<Character>(), source);
    }

    /// The stage of the troll's recovery ("none" when it never had one).
    [TestHook("stt.recoveryStage")]
    private static string RecoveryStage(GameObject troll)
    {
        TrollRecovery r = troll.GetComponent<TrollRecovery>();
        return r == null ? "none" : r.Stage.ToString();
    }

    /// How many of the ring walls are damaged or gone (a troll must never attack anything a player built). Expected outcome: 0.
    [TestHook("stt.wallsHit")]
    private static int WallsHit()
    {
        int hit = 0;
        var detail = new StringBuilder();
        for (int k = 0; k < Walls.Count; k++)
        {
            GameObject w = Walls[k];
            int layer = k < Walls.Count / 2 ? 0 : 1;
            if (w == null)
            {
                hit++;
                detail.Append(" #" + k + " (layer " + layer + ") gone;");
                continue;
            }

            WearNTear wnt = w.GetComponent<WearNTear>();
            if (wnt == null)
            {
                throw new Exception("ring wall #" + k +
                                    " has no WearNTear - fixture error, its damage cannot be measured");
            }

            float pct = wnt.GetHealthPercentage();
            if (pct < 0.999f)
            {
                hit++;
                ZNetView wv = w.GetComponent<ZNetView>();
                float cur = wv != null && wv.IsValid()
                    ? wv.GetZDO().GetFloat(ZDOVars.s_health, wnt.m_health)
                    : pct * wnt.m_health;
                detail.Append(" #" + k + " (layer " + layer + ") " + ((1f - pct) * 100f).ToString("F0") +
                              "% damage, health " + cur.ToString("F0") + " of " + wnt.m_health.ToString("F0") + ";");
            }
        }

        ModLog.Info("RecoveryTestHooks: " + hit + " of " + Walls.Count + " ring walls damaged or gone" +
                    (hit > 0 ? ":" + detail : ""));
        return hit;
    }

    /// D-172 (review: log the target tree's ZDO health and max health after creation and after the recovery): "health H of M (ZDO value / prefab m_health)".
    private static string TreeHealth(GameObject tree)
    {
        if (tree == null || !tree.activeInHierarchy)
        {
            return "tree gone";
        }

        TreeBase t = tree.GetComponent<TreeBase>();
        ZNetView v = tree.GetComponent<ZNetView>();
        if (t == null || v == null || !v.IsValid())
        {
            return "no TreeBase / valid ZNetView";
        }

        float h = v.GetZDO().GetFloat(ZDOVars.s_health, t.m_health);
        return "health " + h.ToString("F1") + " of " + t.m_health.ToString("F1") + (h < t.m_health - 0.01f
            ? " (damaged " + (t.m_health - h).ToString("F1") + ")"
            : " (untouched)");
    }

    /// D-172: the walled tree's health now (diagnosis step). Expected outcome: the TreeHealth text.
    [TestHook("stt.walledTreeHealth")]
    private static string WalledTreeHealth()
    {
        string h = TreeHealth(_tree);
        ModLog.Info("RecoveryTestHooks: walled tree " + h);
        return h;
    }

    /// D-174 (review: far-troll-goes-home-obstacle-route): a line of player-built stone walls (two high, `length` m long) across the troll's straight way to its hub,
    /// `ahead` m in front of the troll - nothing a troll may break, so it must find a way round (D-174 candidate search). Expected outcome: how many walls.
    [TestHook("stt.wallAcross")]
    private static int WallAcross(GameObject troll, float ahead, float length)
    {
        HubRegistry.All(true);
        HubInfo hub = HubRegistry.For(troll.GetComponent<Character>());
        if (hub == null)
        {
            throw new Exception("the troll has no hub");
        }

        Vector3 to = hub.Pos - troll.transform.position;
        to.y = 0f;
        Vector3 fwd = to.normalized;
        var side = new Vector3(fwd.z, 0f, -fwd.x);
        Vector3 centre = troll.transform.position + fwd * ahead;
        GameObject wallPrefab = ZNetScene.instance.GetPrefab("stone_wall_4x2");
        int n = Mathf.CeilToInt(length / 4f), made = 0;
        long creator = Me != 0L ? Me : 4711L;
        for (int layer = 0; layer < 2; layer++)
        for (int i = 0; i < n; i++)
        {
            Vector3 at = centre + side * ((i - (n - 1) * 0.5f) * 4f);
            float low = Mathf.Min(ZoneSystem.instance.GetGroundHeight(at),
                Mathf.Min(ZoneSystem.instance.GetGroundHeight(at + side * 2f),
                    ZoneSystem.instance.GetGroundHeight(at - side * 2f)));
            at.y = low - 0.3f + layer * 2f;
            GameObject wall = Object.Instantiate(wallPrefab, at, Quaternion.LookRotation(fwd));
            ZNetView view = wall.GetComponent<ZNetView>();
            Piece piece = wall.GetComponent<Piece>();
            WearNTear wnt = wall.GetComponent<WearNTear>();
            if (view != null && view.IsValid())
            {
                view.GetZDO().Set(ZDOVars.s_creator, creator);
            }

            if (piece != null)
            {
                CreatorField(piece) = creator;
            }

            if (wnt != null)
            {
                wnt.m_noSupportWear = false;
                wnt.m_noRoofWear = false;
            }

            Walls.Add(wall);
            Built.Add(wall);
            WallIds.Add(wall.GetInstanceID());
            made++;
        }

        ModLog.Info("RecoveryTestHooks: wall line of " + n + " x 2 player-built walls (" + length.ToString("F0") +
                    " m) " + ahead.ToString("F0") + " m in front of " +
                    TrollTaming.Tag(troll.GetComponent<Character>()) + ", across its way to " + hub.Name);
        return made;
    }

    /// D-172: true once the walled-tree scenario has an outcome - the troll blacklisted the tree (the expected end) OR the tree is gone (felled: the troll broke in). Lets the
    /// test stop at once instead of waiting out 360 s on a tree that no longer exists. Reads the tree walledTree built (the lab's 'tree' binding dies with the tree).
    [TestHook("stt.walledDone")]
    private static bool WalledDone(GameObject troll)
    {
        return WalledOutcome(troll) != "waiting";
    }

    /// D-172: "blacklisted" (expected), "waiting", or what went wrong: "tree felled, walls standing N of M, walls hit K, stage S".
    [TestHook("stt.walledOutcome")]
    private static string WalledOutcome(GameObject troll)
    {
        Character ch = troll != null ? troll.GetComponent<Character>() : null;
        if (_tree != null && _tree.activeInHierarchy && ch != null && TrollRecovery.IsBlacklisted(ch, _tree))
        {
            ModLog.Info("RecoveryTestHooks: walled tree outcome - blacklisted, " + TreeHealth(_tree));
            return "blacklisted";
        }

        if (_tree != null && _tree.activeInHierarchy)
        {
            return "waiting";
        }

        int alive = 0;
        foreach (GameObject w in Walls)
        {
            if (w != null && w.activeInHierarchy)
            {
                alive++;
            }
        }

        TrollRecovery r = troll != null ? troll.GetComponent<TrollRecovery>() : null;
        string why = "tree felled, walls standing " + alive + " of " + Walls.Count + ", walls hit " + WallsHit() +
                     ", stage " + (r == null ? "none" : r.Stage.ToString())
                     + (troll != null
                         ? ", troll " +
                           Vector3.Distance(new Vector3(troll.transform.position.x, 0f, troll.transform.position.z),
                               new Vector3(_centre.x, 0f, _centre.z)).ToString("F1") + " m from the tree (ring " +
                           _radius.ToString("F0") + " m)"
                         : "");
        ModLog.Info("RecoveryTestHooks: walled tree outcome - " + why);
        return why;
    }

    /// D-173: ring-wall hits that reached the wall from a tamed troll (must stay 0: PlayerPieceGuard). Other sources (a felled tree's log) are only logged.
    [TestHook("stt.wallsHitByTroll")]
    private static int WallsHitByTroll()
    {
        ModLog.Info("RecoveryTestHooks: ring walls - hits from a tamed troll " + _trollHits +
                    (_trollHits > 0 ? " (last: " + _lastTrollHit + ")" : "") +
                    ", from other sources (falling logs ...) " + _otherHits);
        return _trollHits;
    }

    /// 1 while the tree still stands untouched.
    [TestHook("stt.treeStands")]
    private static int TreeStands(GameObject tree)
    {
        if (tree == null)
        {
            return 0;
        }

        TreeBase t = tree.GetComponent<TreeBase>();
        ZNetView v = tree.GetComponent<ZNetView>();
        bool full = t == null || v == null || !v.IsValid() ||
                    v.GetZDO().GetFloat(ZDOVars.s_health, t.m_health) >= t.m_health - 0.01f;
        return full ? 1 : 0;
    }

    // Harmony hook: src/Integrations/Harmony/TestingPatches.cs, RecoveryTestHooks_CountWallHits (MIGRATION_DECISIONS A5)
    internal static class CountWallHits
    {
        internal static void Prefix(WearNTear __instance, HitData hit)
        {
            if (__instance == null || hit == null || WallIds.Count == 0 ||
                !WallIds.Contains(__instance.gameObject.GetInstanceID()))
            {
                return;
            }

            Character a = hit.GetAttacker();
            if (a != null && a.IsTamed() && TrollTaming.IsTroll(a))
            {
                _trollHits++;
                _lastTrollHit = a.name + " " + hit.GetTotalDamage().ToString("F0");
            }
            else
            {
                _otherHits++;
            }
        }
    }
}
