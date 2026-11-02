using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Farming.Infrastructure;
using SteveTheTroll.Trolls.Orders.Commands;
using SteveTheTroll.Trolls.Recovery.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing
{
    /// <summary>
    /// D-167: TestLab hooks for the stuck detection and recovery (suite recovery-walled-tree): a tree inside a closed ring of player-built walls. Nothing a troll may attack stands in
    /// its way, and the game's path search finds no way in, so the troll walks into the ring, gets stuck, must walk around, fail two complete cycles and give the tree up for 120 s.
    /// The hooks call the real mod code; they only build the scene and read results.
    /// </summary>
    internal static class RecoveryTestHooks
    {
        static readonly List<GameObject> Built = new List<GameObject>();
        static readonly List<GameObject> Walls = new List<GameObject>();
        static GameObject _tree;
        static Vector3 _centre;
        static float _radius;
        // D-172: Piece.IsPlacedByPlayer() reads the creator the Piece cached in Awake (m_creator), not the ZDO - writing only the ZDO left the ring walls "not player-built",
        // the troll raged at them and felled the walled tree (recovery-walled-tree, 2026-11-02). Both are set now.
        static readonly AccessTools.FieldRef<Piece, long> CreatorField = AccessTools.FieldRefAccess<Piece, long>("m_creator");
        static long Me { get { return Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L; } }

        /// A Beech tree `dx`,`dz` metres from the player inside a closed ring of player-built stone walls of the given radius. Expected outcome: the tree (bind it with "as").
        [TestHook("stt.walledTree")]
        static GameObject WalledTree(float dx, float dz, float radius)
        {
            ClearWalled();
            var p = Player.m_localPlayer;
            var centre = p.transform.position + new Vector3(dx, 0f, dz);
            centre.y = ZoneSystem.instance.GetGroundHeight(centre);
            var treePrefab = ZNetScene.instance.GetPrefab("Beech1");
            var wallPrefab = ZNetScene.instance.GetPrefab("stone_wall_4x2");
            if (treePrefab == null || wallPrefab == null) throw new System.Exception("Beech1 or stone_wall_4x2 prefab missing");
            var tree = Object.Instantiate(treePrefab, centre, Quaternion.identity);
            Built.Add(tree);
            _tree = tree; _centre = centre; _radius = radius;
            int n = Mathf.CeilToInt(2f * Mathf.PI * radius / 3.2f);      // the wall is 4 m long along its local X: forward = radial puts that axis along the tangent
            // D-172 (run 141510: the walls stood, none was hit, yet the troll reached the tree - it got over / under the ring where the ground slopes): each wall sits at the
            // LOWEST ground of its 4 m length (no gap under the downhill end) and the ring is TWO walls high (4 m), so the walled tree cannot be reached without breaking a wall.
            for (int k = 0; k < 2 * n; k++)
            {
                int i = k % n, layer = k / n;
                float a = i * 2f * Mathf.PI / n;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tangent = new Vector3(-radial.z, 0f, radial.x);
                var at = centre + radial * radius;
                float low = Mathf.Min(ZoneSystem.instance.GetGroundHeight(at), Mathf.Min(ZoneSystem.instance.GetGroundHeight(at + tangent * 2f), ZoneSystem.instance.GetGroundHeight(at - tangent * 2f)));
                at.y = low - 0.3f + layer * 2f;
                var wall = Object.Instantiate(wallPrefab, at, Quaternion.LookRotation(radial));
                var view = wall.GetComponent<ZNetView>();
                var piece = wall.GetComponent<Piece>();
                long creator = Me != 0L ? Me : 4711L;      // 0 would mean "not built by a player"
                if (view == null || !view.IsValid()) ModLog.Warn("RecoveryTestHooks: wall " + i + " has no valid ZNetView - creator not set in the ZDO");
                else view.GetZDO().Set(ZDOVars.s_creator, creator);
                if (piece != null) CreatorField(piece) = creator;
                if (k == 0)      // one line is enough, they are all built the same way
                    ModLog.Info("RecoveryTestHooks: wall 0 creator in ZDO " + (view != null && view.IsValid() ? view.GetZDO().GetLong(ZDOVars.s_creator, 0L).ToString() : "no valid ZDO")
                        + ", Piece found " + (piece != null) + ", GetCreator " + (piece != null ? piece.GetCreator().ToString() : "-") + ", IsPlacedByPlayer " + (piece != null && piece.IsPlacedByPlayer()));
                Walls.Add(wall); Built.Add(wall);
            }
            ModLog.Info("RecoveryTestHooks: tree at " + centre.ToString("F0") + " inside a ring of " + n + " x 2 player-built walls, radius " + radius.ToString("F0") + " m, creator " + (Me != 0L ? Me : 4711L));
            return tree;
        }

        /// D-171: how many walls of the ring still stand (diagnosis of the flaky runs where the troll walked straight through the ring). Expected outcome: 12 right after walledTree.
        [TestHook("stt.wallsAlive")]
        static int WallsAlive()
        {
            int n = 0;
            foreach (var w in Walls) if (w != null && w.activeInHierarchy) n++;
            ModLog.Info("RecoveryTestHooks: " + n + " of " + Walls.Count + " ring walls stand");
            return n;
        }

        /// Removes the tree and the walls of walledTree. Expected outcome: how many objects were removed.
        [TestHook("stt.clearWalled")]
        internal static int ClearWalled()
        {
            int n = 0;
            foreach (var g in Built)
            {
                if (g == null) continue;
                var v = g.GetComponent<ZNetView>();
                if (v != null && v.IsValid()) { v.ClaimOwnership(); ZNetScene.instance.Destroy(g); }
                else Object.Destroy(g);
                n++;
            }
            Built.Clear(); Walls.Clear(); _tree = null;
            return n;
        }

        /// The troll farms exactly this source (the real TrollFarming.BeginOn). Expected outcome: true when it started.
        [TestHook("stt.farmOn")]
        static bool FarmOn(GameObject troll, GameObject source) { return TrollOrderService.Send(new BreakSourceCommand(troll.GetComponent<Character>(), source, "")); }

        /// Resource-specific blacklist of the real RecoveryBook for this troll + this source.
        [TestHook("stt.blacklisted")]
        static bool Blacklisted(GameObject troll, GameObject source) { return TrollRecovery.IsBlacklisted(troll.GetComponent<Character>(), source); }

        /// The stage of the troll's recovery ("none" when it never had one).
        [TestHook("stt.recoveryStage")]
        static string RecoveryStage(GameObject troll)
        {
            var r = troll.GetComponent<TrollRecovery>();
            return r == null ? "none" : r.Stage.ToString();
        }

        /// How many of the ring walls are damaged or gone (a troll must never attack anything a player built). Expected outcome: 0.
        [TestHook("stt.wallsHit")]
        static int WallsHit()
        {
            int hit = 0;
            foreach (var w in Walls)
            {
                if (w == null) { hit++; continue; }
                var wnt = w.GetComponent<WearNTear>();
                if (wnt != null && wnt.GetHealthPercentage() < 0.999f) hit++;
            }
            return hit;
        }

        /// D-172: true once the walled-tree scenario has an outcome - the troll blacklisted the tree (the expected end) OR the tree is gone (felled: the troll broke in). Lets the
        /// test stop at once instead of waiting out 360 s on a tree that no longer exists. Reads the tree walledTree built (the lab's 'tree' binding dies with the tree).
        [TestHook("stt.walledDone")]
        static bool WalledDone(GameObject troll) { return WalledOutcome(troll) != "waiting"; }

        /// D-172: "blacklisted" (expected), "waiting", or what went wrong: "tree felled, walls standing N of M, walls hit K, stage S".
        [TestHook("stt.walledOutcome")]
        static string WalledOutcome(GameObject troll)
        {
            var ch = troll != null ? troll.GetComponent<Character>() : null;
            if (_tree != null && _tree.activeInHierarchy && ch != null && TrollRecovery.IsBlacklisted(ch, _tree)) return "blacklisted";
            if (_tree != null && _tree.activeInHierarchy) return "waiting";
            int alive = 0; foreach (var w in Walls) if (w != null && w.activeInHierarchy) alive++;
            var r = troll != null ? troll.GetComponent<TrollRecovery>() : null;
            string why = "tree felled, walls standing " + alive + " of " + Walls.Count + ", walls hit " + WallsHit() + ", stage " + (r == null ? "none" : r.Stage.ToString())
                + (troll != null ? ", troll " + Vector3.Distance(new Vector3(troll.transform.position.x, 0f, troll.transform.position.z), new Vector3(_centre.x, 0f, _centre.z)).ToString("F1") + " m from the tree (ring " + _radius.ToString("F0") + " m)" : "");
            ModLog.Info("RecoveryTestHooks: walled tree outcome - " + why);
            return why;
        }

        /// 1 while the tree still stands untouched.
        [TestHook("stt.treeStands")]
        static int TreeStands(GameObject tree)
        {
            if (tree == null) return 0;
            var t = tree.GetComponent<TreeBase>();
            var v = tree.GetComponent<ZNetView>();
            bool full = t == null || v == null || !v.IsValid() || v.GetZDO().GetFloat(ZDOVars.s_health, t.m_health) >= t.m_health - 0.01f;
            return full ? 1 : 0;
        }
    }
}
