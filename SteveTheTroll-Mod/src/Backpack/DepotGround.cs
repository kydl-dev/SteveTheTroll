using System.Collections.Generic;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Backpack
{
    /// <summary>
    /// D-095 (user: "flatten the ground a bit where the structure is to go so that it can go even on terrain that is not that flat"):
    /// levels the ground under a new depot stack with the game's own hoe "level ground" operation (a TerrainOp prefab such as mud_road_v2,
    /// placed like the hoe places it, so the change is saved and synced like a player's), a grid of them over the footprint + a margin.
    /// </summary>
    internal static class DepotGround
    {
        internal const float Margin = 0.8f;
        static readonly string[] Preferred = { "mud_road_v2", "mud_road" };
        static GameObject _op;
        static bool _looked;

        /// The game's level-ground op (level on, no raise). Expected outcome: the prefab, or null (logged once) - the depot then needs flat ground.
        static GameObject LevelOp()
        {
            if (_looked) return _op;
            _looked = true;
            var scene = ZNetScene.instance;
            if (scene == null) return null;
            foreach (var n in Preferred)
            {
                var p = scene.GetPrefab(n);
                if (Good(p)) { _op = p; break; }
            }
            if (_op == null)
                foreach (var p in scene.m_prefabs) if (Good(p)) { _op = p; break; }
            if (_op == null) ModLog.Warn("DepotGround: no level-ground TerrainOp prefab in the game - depots only on flat ground");
            else
            {
                var s = _op.GetComponent<TerrainOp>().m_settings;
                ModLog.Info("DepotGround: level op '" + _op.name + "', radius " + s.m_levelRadius.ToString("F1") + " m, square " + s.m_square + ", offset " + s.m_levelOffset.ToString("F2"));
            }
            return _op;
        }

        static bool Good(GameObject p)
        {
            var op = p != null ? p.GetComponent<TerrainOp>() : null;
            return op != null && op.m_settings.m_level && !op.m_settings.m_raise && op.m_settings.m_levelRadius > 0.2f;
        }

        /// Whether depots may stand on uneven ground (the level op exists).
        internal static bool CanLevel { get { return LevelOp() != null; } }

        /// Levels the footprint (half sizes + Margin) around `ground` to ground.y. Expected outcome: the number of level ops placed (0 = none).
        internal static int Flatten(Vector3 ground, Quaternion rot)
        {
            var prefab = LevelOp();
            if (prefab == null) return 0;
            var s = prefab.GetComponent<TerrainOp>().m_settings;
            int n = 0;
            foreach (var local in Grid(DepotData.HalfX + Margin, DepotData.HalfZ + Margin, s.m_levelRadius))
            {
                var at = ground + rot * local;
                at.y = ground.y - s.m_levelOffset;
                Object.Instantiate(prefab, at, rot);          // TerrainOp.Awake applies itself to the terrain and destroys the object
                n++;
            }
            ModLog.Info("DepotGround: levelled " + ((DepotData.HalfX + Margin) * 2f).ToString("F1") + " x " + ((DepotData.HalfZ + Margin) * 2f).ToString("F1") + " m at " + ground.ToString("F1") + " with " + n + " op(s)");
            return n;
        }

        /// Pure: centres of level ops (each covers `radius` around itself) that together cover the rectangle +-halfX / +-halfZ.
        /// Expected outcome: a grid with spacing at most `radius`, corners included.
        internal static List<Vector3> Grid(float halfX, float halfZ, float radius)
        {
            var list = new List<Vector3>();
            float step = Mathf.Max(0.25f, radius);
            int nx = Mathf.Max(1, Mathf.CeilToInt(2f * halfX / step)), nz = Mathf.Max(1, Mathf.CeilToInt(2f * halfZ / step));
            for (int i = 0; i <= nx; i++)
                for (int k = 0; k <= nz; k++)
                    list.Add(new Vector3(-halfX + 2f * halfX * i / nx, 0f, -halfZ + 2f * halfZ * k / nz));
            return list;
        }
    }
}
