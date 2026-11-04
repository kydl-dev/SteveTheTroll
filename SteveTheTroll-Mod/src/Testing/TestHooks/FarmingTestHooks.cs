using System;
using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
///     D-170: TestLab hooks for the farming slice (suite test farm-chops-tree): a free-standing tree near the player, and
///     how many swings the troll's farmer made. Until now no
///     lab test reached the swing ladder (every farming test ended before the troll swung: "attacks 0" in all logs of
///     2026-11-02).
/// </summary>
internal static class FarmingTestHooks
{
    private static readonly List<GameObject> Planted = new();
    private static readonly List<Vector3> PlantedAt = new();
    private const float DebrisRadius = 4f;

    /// A Beech tree `dx`,`dz` metres from the player, on the ground. Expected outcome: the tree (bind it with "as").
    [TestHook("stt.plantTree")]
    private static GameObject PlantTree(float dx, float dz)
    {
        Player p = Player.m_localPlayer;
        Vector3 at = p.transform.position + new Vector3(dx, 0f, dz);
        at.y = ZoneSystem.instance.GetGroundHeight(at);
        GameObject prefab = ZNetScene.instance.GetPrefab("Beech1");
        if (prefab == null)
        {
            throw new Exception("Beech1 prefab missing");
        }

        GameObject tree = Object.Instantiate(prefab, at, Quaternion.identity);
        Planted.Add(tree);
        PlantedAt.Add(at);
        ModLog.Info("FarmingTestHooks: tree planted at " + at.ToString("F0") + " (" +
                    new Vector2(dx, dz).magnitude.ToString("F1") + " m from the player)");
        return tree;
    }

    /// Planted trees still standing untouched (a felled tree is destroyed, so the lab cannot name it any more - this hook needs no tree argument). Expected outcome: 0 once hit.
    [TestHook("stt.plantedUntouched")]
    private static int PlantedUntouched()
    {
        int n = 0;
        foreach (GameObject g in Planted)
        {
            if (g == null)
            {
                continue;
            }

            TreeBase t = g.GetComponent<TreeBase>();
            ZNetView v = g.GetComponent<ZNetView>();
            bool full = t == null || v == null || !v.IsValid() ||
                        v.GetZDO().GetFloat(ZDOVars.s_health, t.m_health) >= t.m_health - 0.01f;
            if (full)
            {
                n++;
            }
        }

        return n;
    }

    /// D-175: is this one of the trees stt.plantTree made. Expected outcome: true for a planted tree.
    internal static bool IsPlanted(GameObject g)
    {
        return g != null && Planted.Contains(g);
    }

    /// Hits a tamed creature (not debris) did to the planted trees since stt.reset. Expected outcome: the count.
    [TestHook("stt.plantedHitByTroll")]
    private static int PlantedHitByTroll()
    {
        return TreeDamageLog.TrollHitsOnPlanted;
    }

    /// Swings the troll's farmer started (StartAttack accepted). Expected outcome: -1 without a farmer.
    [TestHook("stt.farmAttacks")]
    private static int FarmAttacks(GameObject troll)
    {
        TrollFarmer f = troll != null ? troll.GetComponent<TrollFarmer>() : null;
        return f != null ? f.Attacks : -1;
    }

    /// Removes the planted trees (stt.reset). Expected outcome: how many were removed.
    internal static int ClearPlanted()
    {
        TreeDamageLog.TrollHitsOnPlanted = 0;
        int n = 0;
        foreach (GameObject g in Planted)
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

        // D-176 (noweapon-farm, run 222441: the previous test's felled tree left a stump 2.5 m from the same planting spot; it blocked the next troll, recovery raged at it and the rage swing hit the tree):
        // what a planted tree leaves behind (stump, logs) goes with it - only within a few metres of where the test planted, never the world around
        int debris = 0;
        foreach (Vector3 at in PlantedAt)
        {
            foreach (ZNetView v in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
            {
                if (v == null || !v.IsValid())
                {
                    continue;
                }

                GameObject g = v.gameObject;
                Vector3 d = g.transform.position - at;
                d.y = 0f;
                if (d.magnitude > DebrisRadius)
                {
                    continue;
                }

                bool leftover = g.GetComponent<TreeLog>() != null || g.GetComponent<TreeBase>() != null ||
                                (g.GetComponent<Destructible>() != null && g.GetComponent<Piece>() == null && g.GetComponent<Character>() == null);
                if (!leftover)
                {
                    continue;
                }

                v.ClaimOwnership();
                ZNetScene.instance.Destroy(g);
                debris++;
            }
        }

        if (PlantedAt.Count > 0)
        {
            ModLog.Info("FarmingTestHooks: " + n + " planted tree(s) removed, " + debris + " stump / log / debris object(s) within " + DebrisRadius.ToString("F0") + " m of the planting spots removed");
        }

        PlantedAt.Clear();
        Planted.Clear();
        return n;
    }
}
