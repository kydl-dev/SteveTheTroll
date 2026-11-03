using HarmonyLib;
using SteveTheTroll.Features.Trolls.Water.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony;
// MIGRATION_DECISIONS A5: every Harmony patch of the TrollsWater feature, declared here; each one only forwards to the feature's own method
// (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

// TrollWaterKeep.KeepAway (Features/Trolls/Water/Infrastructure/TrollWaterKeep.cs)
[HarmonyPatch(typeof(BaseAI), "Follow")]
internal static class TrollWaterKeep_KeepAway
{
    private static bool Prefix(BaseAI __instance, GameObject go, float dt)
    {
        return TrollWaterKeep.KeepAway.Prefix(__instance, go, dt);
    }
}

// TrollWaterKeep.NoShipDamage (Features/Trolls/Water/Infrastructure/TrollWaterKeep.cs)
[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
internal static class TrollWaterKeep_NoShipDamage
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(WearNTear __instance, HitData hit)
    {
        return TrollWaterKeep.NoShipDamage.Prefix(__instance, hit);
    }
}
