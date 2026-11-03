using HarmonyLib;
using Kydl.Common;

namespace SteveTheTroll.Items.Slippers
{
    /// <summary>
    /// TEST-007: the game only counts armor and damage resistances of the helmet, chest, legs and shoulder slots (Player.GetBodyArmor,
    /// Player.ApplyArmorDamageMods in 1.0.20) and its tooltip shows armor only for those item types. The slippers sit in the UTILITY slot
    /// (D-009), so these three patches add their armor (5 +1/quality) and frost resistance and show both in the tooltip - for the slippers
    /// only, every other utility item stays vanilla.
    /// </summary>
    internal static class SlipperStatsPatch
    {
        static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> UtilityItem = AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_utilityItem");
        static int _armorLogs;

        /// The slippers when the player wears them in the utility slot, else null.
        internal static ItemDrop.ItemData Worn(Humanoid h)
        {
            var item = h != null ? UtilityItem(h) : null;
            return IsSlippers(item) ? item : null;
        }

        /// The item's damage modifiers make it frost resistant.
        internal static bool FrostResistant(ItemDrop.ItemData.SharedData s)
        {
            return s != null && s.m_damageModifiers != null && s.m_damageModifiers.Exists(m => m.m_type == HitData.DamageType.Frost && m.m_modifier == HitData.DamageModifier.Resistant);
        }

        internal static bool IsSlippers(ItemDrop.ItemData item)
        {
            return item != null && item.m_shared != null && item.m_shared.m_name == SlipperItem.TokenName;
        }

        // Harmony hook: src/Integrations/Harmony/SlippersPatches.cs, SlipperStatsPatch_BodyArmor (MIGRATION_DECISIONS A5)
        internal static class BodyArmor
        {
            internal static void Postfix(Player __instance, ref float __result)
            {
                var s = Worn(__instance);
                if (s == null || !ModSettings.On) return;
                float add = s.GetArmor();
                __result += add;
                if (_armorLogs < 3) { _armorLogs++; ModLog.Verbose("SlipperStatsPatch: body armor +" + add + " from the slippers (quality " + s.m_quality + ") = " + __result); }
            }
        }

        // Harmony hook: src/Integrations/Harmony/SlippersPatches.cs, SlipperStatsPatch_ArmorDamageMods (MIGRATION_DECISIONS A5)
        internal static class ArmorDamageMods
        {
            internal static void Postfix(Player __instance, ref HitData.DamageModifiers mods)
            {
                var s = Worn(__instance);
                if (s == null || !ModSettings.On) return;
                mods.Apply(s.m_shared.m_damageModifiers);
                ModLog.Verbose("SlipperStatsPatch: damage mods + slippers -> frost " + mods.m_frost);
            }
        }

        // Harmony hook: src/Integrations/Harmony/SlippersPatches.cs, SlipperStatsPatch_Tooltip (MIGRATION_DECISIONS A5)
        internal static class Tooltip
        {
            internal static void Postfix(ItemDrop.ItemData item, int qualityLevel, float worldLevel, ref string __result)
            {
                if (!IsSlippers(item) || !ModSettings.On) return;
                float armor = item.GetArmor(qualityLevel, worldLevel);
                string mods = SE_Stats.GetDamageModifiersTooltipString(item.m_shared.m_damageModifiers);
                __result += "\n$item_armor: <color=orange>" + armor + "</color>" + mods;
                ModLog.Verbose("SlipperStatsPatch: tooltip quality " + qualityLevel + ", armor " + armor + ", resistances " + item.m_shared.m_damageModifiers.Count);
            }
        }
    }
}
