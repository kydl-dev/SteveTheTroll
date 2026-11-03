using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: every Harmony patch of the Hubs feature, declared here; each one only forwards to the feature's own method
    // (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

    // HubPlacement.Visible (Features/Hubs/Infrastructure/HubPlacement.cs)
    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
    static class HubPlacement_Visible
    {
        static void Postfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result) { global::SteveTheTroll.Features.Hubs.Infrastructure.HubPlacement.Visible.Postfix(__instance, piece, mode, ref __result); }
    }

    // HubPlacement.Place (Features/Hubs/Infrastructure/HubPlacement.cs)
    [HarmonyPatch(typeof(Player), "PlacePiece")]
    static class HubPlacement_Place
    {
        static bool Prefix(Player __instance, Piece piece, Vector3 pos) { return global::SteveTheTroll.Features.Hubs.Infrastructure.HubPlacement.Place.Prefix(__instance, piece, pos); }
    }
}
