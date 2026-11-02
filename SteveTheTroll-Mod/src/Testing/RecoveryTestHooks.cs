using System.Collections.Generic;
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
            int n = Mathf.CeilToInt(2f * Mathf.PI * radius / 3.2f);      // the wall is 4 m long along its local X: forward = radial puts that axis along the tangent
            for (int i = 0; i < n; i++)
            {
                float a = i * 2f * Mathf.PI / n;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var at = centre + radial * radius;
                at.y = ZoneSystem.instance.GetGroundHeight(at) - 0.3f;
                var wall = Object.Instantiate(wallPrefab, at, Quaternion.LookRotation(radial));
                var view = wall.GetComponent<ZNetView>();      // Piece.SetCreator needs the Splatform reference; the creator is just a ZDO long (what IsPlacedByPlayer reads)
                if (view != null && view.IsValid()) view.GetZDO().Set(ZDOVars.s_creator, Me != 0L ? Me : 4711L);      // 0 would mean "not built by a player"
                Walls.Add(wall); Built.Add(wall);
            }
            ModLog.Info("RecoveryTestHooks: tree at " + centre.ToString("F0") + " inside a ring of " + n + " player-built walls, radius " + radius.ToString("F0") + " m, creator " + (Me != 0L ? Me : 4711L));
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
            Built.Clear(); Walls.Clear();
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
