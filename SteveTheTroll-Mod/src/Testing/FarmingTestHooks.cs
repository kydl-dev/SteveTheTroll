using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Trolls.Farming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing
{
    /// <summary>
    /// D-170: TestLab hooks for the farming slice (suite test farm-chops-tree): a free-standing tree near the player, and how many swings the troll's farmer made. Until now no
    /// lab test reached the swing ladder (every farming test ended before the troll swung: "attacks 0" in all logs of 2026-11-02).
    /// </summary>
    internal static class FarmingTestHooks
    {
        static readonly List<GameObject> Planted = new List<GameObject>();

        /// A Beech tree `dx`,`dz` metres from the player, on the ground. Expected outcome: the tree (bind it with "as").
        [TestHook("stt.plantTree")]
        static GameObject PlantTree(float dx, float dz)
        {
            var p = Player.m_localPlayer;
            var at = p.transform.position + new Vector3(dx, 0f, dz);
            at.y = ZoneSystem.instance.GetGroundHeight(at);
            var prefab = ZNetScene.instance.GetPrefab("Beech1");
            if (prefab == null) throw new System.Exception("Beech1 prefab missing");
            var tree = Object.Instantiate(prefab, at, Quaternion.identity);
            Planted.Add(tree);
            ModLog.Info("FarmingTestHooks: tree planted at " + at.ToString("F0") + " (" + new Vector2(dx, dz).magnitude.ToString("F1") + " m from the player)");
            return tree;
        }

        /// Swings the troll's farmer started (StartAttack accepted). Expected outcome: -1 without a farmer.
        [TestHook("stt.farmAttacks")]
        static int FarmAttacks(GameObject troll)
        {
            var f = troll != null ? troll.GetComponent<TrollFarmer>() : null;
            return f != null ? f.Attacks : -1;
        }

        /// Removes the planted trees (stt.reset). Expected outcome: how many were removed.
        internal static int ClearPlanted()
        {
            int n = 0;
            foreach (var g in Planted)
            {
                if (g == null) continue;
                var v = g.GetComponent<ZNetView>();
                if (v != null && v.IsValid()) { v.ClaimOwnership(); ZNetScene.instance.Destroy(g); }
                else Object.Destroy(g);
                n++;
            }
            Planted.Clear();
            return n;
        }
    }
}
