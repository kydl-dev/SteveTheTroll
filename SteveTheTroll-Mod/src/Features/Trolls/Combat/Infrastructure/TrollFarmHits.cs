using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Taming.Domain;
using SteveTheTroll.Trolls.Taming.Infrastructure;

namespace SteveTheTroll.Trolls.Combat.Infrastructure
{
    /// <summary>
    /// D-059 (user: "with the current damage of the troll, he should be able to break them easily"): a hit of a TAMED troll on a tree, log,
    /// rock / ore vein or other destructible counts as tool tier 100 (TamingRules.TamedToolTier) and carries the higher of its chop / pickaxe
    /// damage as both, so Yggdrasil shoots, ancient trees, silver veins and the like take its full damage. Prefix on the receivers' Damage
    /// (runs on the attacking client, before the hit is sent to the object's owner). Wild trolls and players are not touched.
    /// </summary>
    // Harmony hook: src/Integrations/Harmony/TrollsCombatPatches.cs, TrollFarmHitsHook (MIGRATION_DECISIONS A5)
    internal static class TrollFarmHits
    {
        static float _nextLog;

        internal static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var t in new[] { typeof(TreeBase), typeof(TreeLog), typeof(MineRock), typeof(MineRock5), typeof(Destructible) })
            {
                var m = AccessTools.Method(t, "Damage", new[] { typeof(HitData) });
                if (m != null) yield return m;
                else ModLog.Warn("TrollFarmHits: " + t.Name + ".Damage(HitData) not found - tamed trolls hit it with their own tool tier");
            }
        }

        internal static void Prefix(object __instance, HitData hit)
        {
            if (hit == null) return;
            var attacker = hit.GetAttacker();
            if (attacker == null || !TrollTaming.IsTroll(attacker) || !attacker.IsTamed()) return;
            short before = hit.m_toolTier;
            float dmg = TamingRules.FarmDamage(hit.m_damage.m_chop, hit.m_damage.m_pickaxe);
            hit.m_toolTier = (short)TamingRules.TamedToolTier;
            hit.m_damage.m_chop = dmg;
            hit.m_damage.m_pickaxe = dmg;
            if (UnityEngine.Time.time >= _nextLog)
            {
                _nextLog = UnityEngine.Time.time + 5f;
                var c = __instance as UnityEngine.Component;
                ModLog.Verbose("TrollFarmHits: " + attacker.name + " hits " + (c != null ? TrollTaming.Tag(c) : "?") + ": tool tier " + before + " -> " + hit.m_toolTier + ", chop / pickaxe " + dmg.ToString("0"));
            }
        }
    }
}
