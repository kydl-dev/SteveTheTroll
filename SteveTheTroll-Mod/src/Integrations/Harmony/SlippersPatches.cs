using HarmonyLib;
using SteveTheTroll.Features.Slippers.Infrastructure;

namespace SteveTheTroll.Integrations.Harmony;
// MIGRATION_DECISIONS A5: every Harmony patch of the Slippers feature, declared here; each one only forwards to the feature's own method
// (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

// SlipperVisualPatch (Features/Slippers/Infrastructure/SlipperItem.cs)
[HarmonyPatch(typeof(VisEquipment), "SetUtilityEquipped")]
internal static class SlipperVisualPatchHook
{
    private static void Postfix(VisEquipment __instance, int hash)
    {
        SlipperVisualPatch.Postfix(__instance, hash);
    }
}

// SlipperStatsPatch.BodyArmor (Features/Slippers/Infrastructure/SlipperStatsPatch.cs)
[HarmonyPatch(typeof(Player), nameof(Player.GetBodyArmor))]
internal static class SlipperStatsPatch_BodyArmor
{
    private static void Postfix(Player __instance, ref float __result)
    {
        SlipperStatsPatch.BodyArmor.Postfix(__instance, ref __result);
    }
}

// SlipperStatsPatch.ArmorDamageMods (Features/Slippers/Infrastructure/SlipperStatsPatch.cs)
[HarmonyPatch(typeof(Player), "ApplyArmorDamageMods")]
internal static class SlipperStatsPatch_ArmorDamageMods
{
    private static void Postfix(Player __instance, ref HitData.DamageModifiers mods)
    {
        SlipperStatsPatch.ArmorDamageMods.Postfix(__instance, ref mods);
    }
}

// SlipperStatsPatch.Tooltip (Features/Slippers/Infrastructure/SlipperStatsPatch.cs)
[HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), typeof(ItemDrop.ItemData), typeof(int),
    typeof(bool), typeof(float), typeof(int), typeof(bool))]
internal static class SlipperStatsPatch_Tooltip
{
    private static void Postfix(ItemDrop.ItemData item, int qualityLevel, float worldLevel, ref string __result)
    {
        SlipperStatsPatch.Tooltip.Postfix(item, qualityLevel, worldLevel, ref __result);
    }
}

// SlipperUpgradeCostPatch.GetAmount (Features/Slippers/Infrastructure/SlipperUpgradeCostPatch.cs)
[HarmonyPatch(typeof(Piece.Requirement), nameof(Piece.Requirement.GetAmount))]
internal static class SlipperUpgradeCostPatch_GetAmount
{
    private static void Postfix(Piece.Requirement __instance, int qualityLevel, ref int __result)
    {
        SlipperUpgradeCostPatch.GetAmount.Postfix(__instance, qualityLevel, ref __result);
    }
}
