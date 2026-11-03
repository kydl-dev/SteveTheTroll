using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;

namespace SteveTheTroll.Integrations.Harmony;
// MIGRATION_DECISIONS A5: every Harmony patch of the TrollsCombat feature, declared here; each one only forwards to the feature's own method
// (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

// PlayerPieceGuard.GuardDamage (Features/Trolls/Combat/Infrastructure/PlayerPieceGuard.cs)
[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
internal static class PlayerPieceGuard_GuardDamage
{
    private static bool Prefix(WearNTear __instance, HitData hit)
    {
        return PlayerPieceGuard.GuardDamage.Prefix(__instance, hit);
    }
}

// TrollDefend.PlayerHit (Features/Trolls/Combat/Infrastructure/TrollDefend.cs)
[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
internal static class TrollDefend_PlayerHit
{
    private static void Postfix(Character __instance, HitData hit)
    {
        TrollDefend.PlayerHit.Postfix(__instance, hit);
    }
}

// TrollFarmHits (Features/Trolls/Combat/Infrastructure/TrollFarmHits.cs)
[HarmonyPatch]
internal static class TrollFarmHitsHook
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return TrollFarmHits.TargetMethods();
    }

    private static void Prefix(object __instance, HitData hit)
    {
        TrollFarmHits.Prefix(__instance, hit);
    }
}

// TrollNoHit.NoSwing (Features/Trolls/Combat/Infrastructure/TrollNoHit.cs)
[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
internal static class TrollNoHit_NoSwing
{
    private static bool Prefix(Humanoid __instance, ref bool __result)
    {
        return TrollNoHit.NoSwing.Prefix(__instance, ref __result);
    }
}

// TrollWalls.WallHit (Features/Trolls/Combat/Infrastructure/TrollWalls.cs)
[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
internal static class TrollWalls_WallHit
{
    private static void Prefix(WearNTear __instance, HitData hit)
    {
        TrollWalls.WallHit.Prefix(__instance, hit);
    }
}
