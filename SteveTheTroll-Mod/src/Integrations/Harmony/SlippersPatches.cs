using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: every Harmony patch of the Slippers feature, declared here; each one only forwards to the feature's own method
    // (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

    // SlipperVisualPatch (Features/Slippers/Infrastructure/SlipperItem.cs)
    [HarmonyPatch(typeof(VisEquipment), "SetUtilityEquipped")]
    static class SlipperVisualPatchHook
    {
        static void Postfix(VisEquipment __instance, int hash) { global::SteveTheTroll.Features.Slippers.Infrastructure.SlipperVisualPatch.Postfix(__instance, hash); }
    }

    // SlipperStatsPatch.BodyArmor (Features/Slippers/Infrastructure/SlipperStatsPatch.cs)
    [HarmonyPatch(typeof(Player), nameof(Player.GetBodyArmor))]
    static class SlipperStatsPatch_BodyArmor
    {
        static void Postfix(Player __instance, ref float __result) { global::SteveTheTroll.Features.Slippers.Infrastructure.SlipperStatsPatch.BodyArmor.Postfix(__instance, ref __result); }
    }

    // SlipperStatsPatch.ArmorDamageMods (Features/Slippers/Infrastructure/SlipperStatsPatch.cs)
    [HarmonyPatch(typeof(Player), "ApplyArmorDamageMods")]
    static class SlipperStatsPatch_ArmorDamageMods
    {
        static void Postfix(Player __instance, ref HitData.DamageModifiers mods) { global::SteveTheTroll.Features.Slippers.Infrastructure.SlipperStatsPatch.ArmorDamageMods.Postfix(__instance, ref mods); }
    }

    // SlipperStatsPatch.Tooltip (Features/Slippers/Infrastructure/SlipperStatsPatch.cs)
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
    static class SlipperStatsPatch_Tooltip
    {
        static void Postfix(ItemDrop.ItemData item, int qualityLevel, float worldLevel, ref string __result) { global::SteveTheTroll.Features.Slippers.Infrastructure.SlipperStatsPatch.Tooltip.Postfix(item, qualityLevel, worldLevel, ref __result); }
    }

    // SlipperUpgradeCostPatch.GetAmount (Features/Slippers/Infrastructure/SlipperUpgradeCostPatch.cs)
    [HarmonyPatch(typeof(Piece.Requirement), nameof(Piece.Requirement.GetAmount))]
    static class SlipperUpgradeCostPatch_GetAmount
    {
        static void Postfix(Piece.Requirement __instance, int qualityLevel, ref int __result) { global::SteveTheTroll.Features.Slippers.Infrastructure.SlipperUpgradeCostPatch.GetAmount.Postfix(__instance, qualityLevel, ref __result); }
    }
}
