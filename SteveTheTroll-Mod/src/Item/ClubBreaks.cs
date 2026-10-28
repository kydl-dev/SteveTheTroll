using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Taming;
using UnityEngine;

namespace SteveTheTroll.Item
{
    /// <summary>
    /// D-120 (user 2026-10-28: "Troll Club (even at higher levels) can chop / pickaxe: yggdrasil shoot, scorched trees, timberwood trees,
    /// Giant Remains, Petrified Gammeltroll"): a hit of a player holding the Troll Club on one of those (BreakRules.ClubTarget; Timberwood
    /// trees by their drops) counts as tool tier TamingRules.TamedToolTier and carries the highest of its blunt / chop / pickaxe damage
    /// as chop and pickaxe. Every other target keeps the club's own tool tier (fortress walls stay out of its reach: WearNTear is not patched).
    /// Prefix on the receivers' Damage (runs on the attacking client before the hit is sent to the owner).
    /// </summary>
    [HarmonyPatch]
    internal static class ClubBreaks
    {
        static readonly Dictionary<string, bool> Timber = new Dictionary<string, bool>();   // prefab name -> drops timber (a few per session)
        static float _nextLog;

        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var t in new[] { typeof(TreeBase), typeof(TreeLog), typeof(MineRock), typeof(MineRock5), typeof(Destructible) })
            {
                var m = AccessTools.Method(t, "Damage", new[] { typeof(HitData) });
                if (m != null) yield return m;
                else ModLog.Warn("ClubBreaks: " + t.Name + ".Damage(HitData) not found - the Troll Club keeps its own tool tier there");
            }
        }

        /// The local / remote player's current weapon is the Troll Club. Expected outcome: true only for STT_TrollClub.
        internal static bool HoldsClub(Character attacker)
        {
            var p = attacker as Humanoid;
            if (p == null || !(attacker is Player)) return false;
            var w = p.GetCurrentWeapon();
            return w != null && w.m_dropPrefab != null && w.m_dropPrefab.name == CostumeItems.ClubName;
        }

        /// The object (a tree, its log pieces) drops a timber material, 4 levels deep; cached per prefab name.
        internal static bool DropsTimber(GameObject g)
        {
            if (g == null) return false;
            string key = BreakRules.PrefabName(g.name);
            bool v;
            if (Timber.TryGetValue(key, out v)) return v;
            v = Walk(g, 0);
            Timber[key] = v;
            if (v) ModLog.Info("ClubBreaks: " + key + " drops timber - the Troll Club breaks it");
            return v;
        }

        static bool Walk(GameObject g, int depth)
        {
            if (g == null || depth > 4) return false;
            var tree = g.GetComponent<TreeBase>();
            if (tree != null) return Has(tree.m_dropWhenDestroyed) || Walk(tree.m_logPrefab, depth + 1);
            var log = g.GetComponent<TreeLog>();
            if (log != null) return Has(log.m_dropWhenDestroyed) || Walk(log.m_subLogPrefab, depth + 1);
            return false;
        }

        static bool Has(DropTable t)
        {
            if (t == null || t.m_drops == null) return false;
            foreach (var d in t.m_drops) if (d.m_item != null && BreakRules.TimberDrop(d.m_item.name)) return true;
            return false;
        }

        static void Prefix(object __instance, HitData hit)
        {
            if (hit == null || !ModSettings.On) return;
            var attacker = hit.GetAttacker();
            if (attacker == null || !HoldsClub(attacker)) return;
            var c = __instance as Component;
            if (c == null) return;
            bool timber = (c is TreeBase || c is TreeLog) && DropsTimber(c.gameObject);
            if (!BreakRules.ClubTarget(c.name, timber)) return;
            short before = hit.m_toolTier;
            float dmg = BreakRules.BreakDamage(hit.m_damage.m_blunt, hit.m_damage.m_chop, hit.m_damage.m_pickaxe);
            hit.m_toolTier = (short)TamingRules.TamedToolTier;
            hit.m_damage.m_chop = dmg;
            hit.m_damage.m_pickaxe = dmg;
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + 3f;
                ModLog.Info("ClubBreaks: Troll Club hits " + c.name + " (" + c.GetType().Name + (timber ? ", timber" : "") + "): tool tier " + before + " -> " + hit.m_toolTier + ", chop / pickaxe " + dmg.ToString("0"));
            }
        }
    }
}
