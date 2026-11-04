using KydlCore.Common;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
///     D-175 (run 185023: noweapon-farm saw its planted tree "not untouched" 0.25 s after planting, and recovery-walled-tree's walled tree lost 8.6 of 80 HP -
///     in both the log could not say WHO damaged the tree; TrollFarmHits logs one hit per 5 s). Logs every damage a test tree takes (the walled tree, the planted farm
///     trees) with its source, so a failure names the cause: a tamed troll, a falling log, or something else. It only observes. Harmony hook:
///     src/Integrations/Harmony/TestingPatches.cs, TreeDamageLog_Patch.
/// </summary>
internal static class TreeDamageLog
{
    /// Hits the planted trees took from a tamed creature since the last stt.reset (a falling log / debris is not one): a farm test waits for THIS troll's hit, not for any damage. Counting only.
    internal static int TrollHitsOnPlanted;

    internal static void Prefix(TreeBase __instance, HitData hit)
    {
        if (__instance == null || hit == null)
        {
            return;
        }

        GameObject g = __instance.gameObject;
        string which = RecoveryTestHooks.IsWalledTree(g) ? "walled tree" : FarmingTestHooks.IsPlanted(g) ? "planted tree" : null;
        if (which == null)
        {
            return;
        }

        Character a = hit.GetAttacker();
        if (which == "planted tree" && a != null && a.IsTamed())
        {
            TrollHitsOnPlanted++;
        }

        string who = a == null
            ? "no attacker"
            : a.name + (a.IsTamed() ? " (tamed)" : "") + ", " +
              Vector3.Distance(a.transform.position, g.transform.position).ToString("F1") + " m away";
        ModLog.Info("TreeDamageLog: " + which + " takes " + hit.GetTotalDamage().ToString("F1") + " (chop " +
                    hit.m_damage.m_chop.ToString("F1") + ", blunt " + hit.m_damage.m_blunt.ToString("F1") + ", type " +
                    hit.m_hitType + ", tool tier " + hit.m_toolTier + ") from " + who + ", hit point " +
                    hit.m_point.ToString("F1") + ", tree at " + g.transform.position.ToString("F1"));
    }
}
