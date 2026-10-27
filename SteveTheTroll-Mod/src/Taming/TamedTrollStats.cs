using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-042 (user 2026-10-27, "always when tamed"): a tamed Troll has TamedHealthFactor x its base health, TamedDamageFactor x its damage,
    /// is at least Resistant to pierce / slash / fire / frost / lightning / poison and never staggers. Wild trolls and every other creature stay
    /// vanilla. Hooks (1.0.20, Cecil): Character.GetMaxHealthBase (SetupMaxHealth multiplies it by the level), Attack.GetLevelDamageFactor,
    /// Character.GetDamageModifiers, Character.AddStaggerDamage.
    /// </summary>
    internal static class TamedTrollStats
    {
        static readonly AccessTools.FieldRef<Attack, Humanoid> AttackOwner = AccessTools.FieldRefAccess<Attack, Humanoid>("m_character");
        static readonly System.Action<Character> SetupMaxHealth = AccessTools.MethodDelegate<System.Action<Character>>(AccessTools.Method(typeof(Character), "SetupMaxHealth"));
        static int _healthLogs, _damageLogs, _modLogs, _staggerLogs;

        /// In-game test Startup_TamingReflection: runs this class's reflection lookups. Expected outcome: whether both were found.
        internal static bool SelfCheck() { return SetupMaxHealth != null && AttackOwner != null; }

        /// A tamed game Troll (the mod is on).
        internal static bool IsTamedTroll(Character c)
        {
            return c != null && ModSettings.On && c.IsTamed() && TrollTaming.IsTroll(c);
        }

        /// After taming (owner only): max health follows the new factor at once, health keeps its percentage. Expected outcome: returns the
        /// new max health (logged and checked against base x level x factor).
        internal static float Refresh(Character troll)
        {
            if (troll == null) return 0f;
            float pct = troll.GetHealthPercentage();
            SetupMaxHealth(troll);
            float max = troll.GetMaxHealth();
            troll.SetHealth(max * pct);
            // at least prefab health x level x factor (world-level scaling in GetMaxHealthBase can only add to it)
            float least = troll.m_health * troll.GetLevel() * ModSettings.TamedHealthFactor.Value * 0.99f;
            ModLog.Verbose("TamedTrollStats: " + troll.name + " max health " + max.ToString("F0") + " (prefab " + troll.m_health + ", level " + troll.GetLevel() + ", x"
                + ModSettings.TamedHealthFactor.Value + "), health kept at " + (pct * 100f).ToString("F0") + " %");
            ModLog.CheckRange("TamedTrollStats: " + troll.name + " max health", max, least, 1e9, "hp");
            return max;
        }

        [HarmonyPatch(typeof(Character), nameof(Character.GetMaxHealthBase))]
        static class MaxHealthBase
        {
            static void Postfix(Character __instance, ref float __result)
            {
                if (!IsTamedTroll(__instance)) return;
                float before = __result;
                __result *= ModSettings.TamedHealthFactor.Value;
                if (_healthLogs < 6) { _healthLogs++; ModLog.Verbose("TamedTrollStats: " + __instance.name + " base health " + before + " -> " + __result + " (x" + ModSettings.TamedHealthFactor.Value + ")"); }
            }
        }

        // TEST-017 / TEST-018: two tamed trolls died 5-7 s after the world loaded with nothing in the log. Every hit on a tamed troll is logged
        // (type, damage, attacker, health after) so the next death shows its cause.
        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
        static class HitLog
        {
            static void Postfix(Character __instance, HitData hit)
            {
                if (hit == null || !IsTamedTroll(__instance)) return;
                var a = hit.GetAttacker();
                float total = hit.GetTotalDamage();
                string line = "TamedTrollStats: " + __instance.name + " hit (" + hit.m_hitType + ") " + total.ToString("F1") + " damage by " + (a != null ? a.name : "nothing")
                    + ", health now " + __instance.GetHealth().ToString("F0") + " / " + __instance.GetMaxHealth().ToString("F0") + " at " + __instance.transform.position.ToString("F1");
                if (__instance.GetHealth() <= 0f || total >= __instance.GetMaxHealth() * 0.25f) ModLog.Warn(line + " (big hit / death)");
                else ModLog.Verbose(line);
            }
        }

        [HarmonyPatch(typeof(Attack), "GetLevelDamageFactor")]
        static class DamageFactor
        {
            static void Postfix(Attack __instance, ref float __result)
            {
                var owner = __instance != null ? AttackOwner(__instance) : null;
                if (!IsTamedTroll(owner)) return;
                float before = __result;
                __result *= ModSettings.TamedDamageFactor.Value;
                if (_damageLogs < 6) { _damageLogs++; ModLog.Verbose("TamedTrollStats: " + owner.name + " attack damage factor " + before + " -> " + __result); }
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.GetDamageModifiers))]
        static class Resistances
        {
            static void Postfix(Character __instance, ref HitData.DamageModifiers __result)
            {
                if (!IsTamedTroll(__instance)) return;
                var r = __result;
                __result.m_pierce = Up(r.m_pierce);
                __result.m_slash = Up(r.m_slash);
                __result.m_fire = Up(r.m_fire);
                __result.m_frost = Up(r.m_frost);
                __result.m_lightning = Up(r.m_lightning);
                __result.m_poison = Up(r.m_poison);
                if (_modLogs < 3)
                {
                    _modLogs++;
                    ModLog.Verbose("TamedTrollStats: " + __instance.name + " resistances pierce " + r.m_pierce + "->" + __result.m_pierce + ", slash " + r.m_slash + "->" + __result.m_slash
                        + ", fire " + r.m_fire + "->" + __result.m_fire + ", frost " + r.m_frost + "->" + __result.m_frost + ", lightning " + r.m_lightning + "->" + __result.m_lightning
                        + ", poison " + r.m_poison + "->" + __result.m_poison);
                }
            }

            static HitData.DamageModifier Up(HitData.DamageModifier m) { return (HitData.DamageModifier)TamingRules.AtLeastResistant((int)m); }
        }

        [HarmonyPatch(typeof(Character), "AddStaggerDamage")]
        static class NoStagger
        {
            // Prefix false = the game's stagger code does not run; __result false = "did not stagger".
            static bool Prefix(Character __instance, float damage, ref bool __result)
            {
                bool tamed = IsTamedTroll(__instance);
                if (!tamed) return !tamed;
                __result = !tamed;
                if (_staggerLogs < 6) { _staggerLogs++; ModLog.Verbose("TamedTrollStats: " + __instance.name + " ignored " + damage.ToString("F1") + " stagger damage (immune)"); }
                return !tamed;
            }
        }
    }
}
