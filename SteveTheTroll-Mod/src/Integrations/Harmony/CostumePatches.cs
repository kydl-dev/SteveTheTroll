using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: every Harmony patch of the Costume feature, declared here; each one only forwards to the feature's own method
    // (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

    // ClubBreaks (Features/Costume/Infrastructure/ClubBreaks.cs)
    [HarmonyPatch]
    static class ClubBreaksHook
    {
        static IEnumerable<MethodBase> TargetMethods() { return global::SteveTheTroll.Features.Costume.Infrastructure.ClubBreaks.TargetMethods(); }
        static void Prefix(object __instance, HitData hit) { global::SteveTheTroll.Features.Costume.Infrastructure.ClubBreaks.Prefix(__instance, hit); }
    }

    // RecipeUnlockPatch (Features/Costume/Infrastructure/RecipeUnlockPatch.cs)
    [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
    static class RecipeUnlockPatchHook
    {
        static void Postfix(Player __instance, Recipe piece, bool discover, ref bool __result) { global::SteveTheTroll.Features.Costume.Infrastructure.RecipeUnlockPatch.Postfix(__instance, piece, discover, ref __result); }
    }

    // SetCarryPatch (Features/Costume/Infrastructure/SetCarryPatch.cs)
    [HarmonyPatch(typeof(Player), nameof(Player.GetMaxCarryWeight))]
    static class SetCarryPatchHook
    {
        static void Postfix(Player __instance, ref float __result) { global::SteveTheTroll.Features.Costume.Infrastructure.SetCarryPatch.Postfix(__instance, ref __result); }
    }

    // SetTooltipPatch (Features/Costume/Infrastructure/SetTooltipPatch.cs)
    [HarmonyPatch(typeof(SE_Stats), nameof(SE_Stats.GetTooltipString))]
    static class SetTooltipPatchHook
    {
        static void Postfix(SE_Stats __instance, ref string __result) { global::SteveTheTroll.Features.Costume.Infrastructure.SetTooltipPatch.Postfix(__instance, ref __result); }
    }

    // SlipperSetSecretPatch (Features/Costume/Infrastructure/SetTooltipPatch.cs)
    [HarmonyPatch]
    static class SlipperSetSecretPatchHook
    {
        static IEnumerable<MethodBase> TargetMethods() { return global::SteveTheTroll.Features.Costume.Infrastructure.SlipperSetSecretPatch.TargetMethods(); }
        static void Prefix(ItemDrop.ItemData item, out StatusEffect __state) { global::SteveTheTroll.Features.Costume.Infrastructure.SlipperSetSecretPatch.Prefix(item, out __state); }
        static void Finalizer(ItemDrop.ItemData item, StatusEffect __state) { global::SteveTheTroll.Features.Costume.Infrastructure.SlipperSetSecretPatch.Finalizer(item, __state); }
    }

    // CostumeUnlock.Equip (Features/Costume/Infrastructure/CostumeUnlock.cs) - T-201
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    static class CostumeUnlock_Equip
    {
        static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result) { global::SteveTheTroll.Features.Costume.Infrastructure.CostumeUnlock.Equip.Postfix(__instance, item, __result); }
    }
}
