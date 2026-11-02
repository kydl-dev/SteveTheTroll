using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Combat.Infrastructure;
using SteveTheTroll.Trolls.Taming.Domain;
using UnityEngine;

namespace SteveTheTroll.Trolls.Taming.Infrastructure
{
    /// <summary>
    /// D-042 (user 2026-10-27, "always when tamed"): a tamed Troll has TamedHealthFactor x its base health, TamedDamageFactor x its damage,
    /// is at least Resistant to pierce / slash, Immune to fire / frost / lightning / poison / spirit (D-118) and never staggers. Wild trolls and every other creature stay
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
            ModLog.Verbose("TamedTrollStats: " + TrollTaming.Tag(troll) + " max health " + max.ToString("F0") + " (prefab " + troll.m_health + ", level " + troll.GetLevel() + ", x"
                + ModSettings.TamedHealthFactor.Value + "), health kept at " + (pct * 100f).ToString("F0") + " %");
            ModLog.CheckRange("TamedTrollStats: " + TrollTaming.Tag(troll) + " max health", max, least, 1e9, "hp");
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
                if (_healthLogs < 6) { _healthLogs++; ModLog.Verbose("TamedTrollStats: " + TrollTaming.Tag(__instance) + " base health " + before + " -> " + __result + " (x" + ModSettings.TamedHealthFactor.Value + ")"); }
            }
        }

        static float _nextLavaLog;

        // D-128 (TEST-052, user: "2 trolls took lava damage in ashlands, one actually died. They should be immune to fire"): the game's lava
        // damage is untyped true damage (HitType.AshlandsLava) that no fire immunity reduces. Tamed trolls skip it (and the burning status
        // it adds); wild trolls and players keep the game's heat damage.
        [HarmonyPatch(typeof(Character), "UpdateHeatDamage")]
        static class NoLava
        {
            static bool Prefix(Character __instance)
            {
                if (!IsTamedTroll(__instance)) return true;
                if (__instance.InLava() && Time.time >= _nextLavaLog)
                {
                    _nextLavaLog = Time.time + 5f;
                    ModLog.Verbose("TamedTrollStats: " + TrollTaming.Tag(__instance) + " in lava at " + __instance.transform.position.ToString("F0") + " - no heat damage (tamed, D-128)");
                }
                return false;
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
                // D-048: the owner is told when an enemy hits its troll (the troll fights back on its own)
                if (a != null && !a.IsPlayer() && !a.IsTamed() && !__instance.IsDead() && Player.m_localPlayer != null && TrollTaming.Owner(__instance) == Player.m_localPlayer.GetPlayerID())
                {
                    var tame = __instance.GetComponent<Tameable>();
                    TrollDefend.UnderAttack(tame != null && !string.IsNullOrEmpty(tame.GetText()) ? tame.GetText() : __instance.name);
                    TrollCounter.OnHit(__instance, a);             // D-084: slams first, then 15 s of farming on, then it goes for the attacker
                }
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
                if (_damageLogs < 6) { _damageLogs++; ModLog.Verbose("TamedTrollStats: " + TrollTaming.Tag(owner) + " attack damage factor " + before + " -> " + __result); }
            }
        }

        // D-118 (user 2026-10-28: "immune to fire / lightning and any other elemental type of dmg, poison included"): fire, frost, lightning,
        // poison and spirit (the game's elemental types) are Immune; pierce and slash stay at least Resistant (D-042).
        [HarmonyPatch(typeof(Character), nameof(Character.GetDamageModifiers))]
        static class Resistances
        {
            static void Postfix(Character __instance, ref HitData.DamageModifiers __result)
            {
                if (!IsTamedTroll(__instance)) return;
                var r = __result;
                __result.m_pierce = Up(r.m_pierce);
                __result.m_slash = Up(r.m_slash);
                __result.m_fire = HitData.DamageModifier.Immune;
                __result.m_frost = HitData.DamageModifier.Immune;
                __result.m_lightning = HitData.DamageModifier.Immune;
                __result.m_poison = HitData.DamageModifier.Immune;
                __result.m_spirit = HitData.DamageModifier.Immune;
                if (_modLogs < 3)
                {
                    _modLogs++;
                    ModLog.Verbose("TamedTrollStats: " + TrollTaming.Tag(__instance) + " resistances pierce " + r.m_pierce + "->" + __result.m_pierce + ", slash " + r.m_slash + "->" + __result.m_slash
                        + ", fire " + r.m_fire + ", frost " + r.m_frost + ", lightning " + r.m_lightning + ", poison " + r.m_poison + ", spirit " + r.m_spirit + " -> all Immune");
                    ModLog.Check("TamedTrollStats: " + TrollTaming.Tag(__instance) + " elemental types immune", Immune(__result), 5, 0, "type(s)");
                }
            }

            static HitData.DamageModifier Up(HitData.DamageModifier m) { return (HitData.DamageModifier)TamingRules.AtLeastResistant((int)m); }
        }

        /// How many of the five elemental types (fire, frost, lightning, poison, spirit) are Immune. Expected outcome: 5 for a tamed troll.
        internal static int Immune(HitData.DamageModifiers m)
        {
            int n = 0;
            foreach (var x in new[] { m.m_fire, m.m_frost, m.m_lightning, m.m_poison, m.m_spirit }) if (x == HitData.DamageModifier.Immune) n++;
            return n;
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
                if (_staggerLogs < 6) { _staggerLogs++; ModLog.Verbose("TamedTrollStats: " + TrollTaming.Tag(__instance) + " ignored " + damage.ToString("F1") + " stagger damage (immune)"); }
                return !tamed;
            }
        }
    }
}
