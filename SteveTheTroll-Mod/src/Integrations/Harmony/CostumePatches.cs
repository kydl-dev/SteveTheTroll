using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SteveTheTroll.Features.Costume.Infrastructure;

namespace SteveTheTroll.Integrations.Harmony;
// MIGRATION_DECISIONS A5: every Harmony patch of the Costume feature, declared here; each one only forwards to the feature's own method
// (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

// ClubBreaks (Features/Costume/Infrastructure/ClubBreaks.cs)
[HarmonyPatch]
internal static class ClubBreaksHook
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return ClubBreaks.TargetMethods();
    }

    private static void Prefix(object __instance, HitData hit)
    {
        ClubBreaks.Prefix(__instance, hit);
    }
}

// RecipeUnlockPatch (Features/Costume/Infrastructure/RecipeUnlockPatch.cs)
[HarmonyPatch(typeof(Player), "HaveRequirementItems")]
internal static class RecipeUnlockPatchHook
{
    private static void Postfix(Player __instance, Recipe piece, bool discover, ref bool __result)
    {
        RecipeUnlockPatch.Postfix(__instance, piece, discover, ref __result);
    }
}

// SetCarryPatch (Features/Costume/Infrastructure/SetCarryPatch.cs)
[HarmonyPatch(typeof(Player), nameof(Player.GetMaxCarryWeight))]
internal static class SetCarryPatchHook
{
    private static void Postfix(Player __instance, ref float __result)
    {
        SetCarryPatch.Postfix(__instance, ref __result);
    }
}

// SetTooltipPatch (Features/Costume/Infrastructure/SetTooltipPatch.cs)
[HarmonyPatch(typeof(SE_Stats), nameof(SE_Stats.GetTooltipString))]
internal static class SetTooltipPatchHook
{
    private static void Postfix(SE_Stats __instance, ref string __result)
    {
        SetTooltipPatch.Postfix(__instance, ref __result);
    }
}

// SlipperSetSecretPatch (Features/Costume/Infrastructure/SetTooltipPatch.cs)
[HarmonyPatch]
internal static class SlipperSetSecretPatchHook
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return SlipperSetSecretPatch.TargetMethods();
    }

    private static void Prefix(ItemDrop.ItemData item, out StatusEffect __state)
    {
        SlipperSetSecretPatch.Prefix(item, out __state);
    }

    private static void Finalizer(ItemDrop.ItemData item, StatusEffect __state)
    {
        SlipperSetSecretPatch.Finalizer(item, __state);
    }
}

// CostumeUnlock.Equip (Features/Costume/Infrastructure/CostumeUnlock.cs) - T-201
[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
internal static class CostumeUnlock_Equip
{
    private static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
    {
        CostumeUnlock.Equip.Postfix(__instance, item, __result);
    }
}
