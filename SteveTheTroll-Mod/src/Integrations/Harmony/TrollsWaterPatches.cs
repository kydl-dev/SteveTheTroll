using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Combat.Domain;
using SteveTheTroll.Trolls.Movement.Infrastructure;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using SteveTheTroll.Trolls.Water.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: every Harmony patch of the TrollsWater feature, declared here; each one only forwards to the feature's own method
    // (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

    // TrollWaterKeep.KeepAway (Features/Trolls/Water/Infrastructure/TrollWaterKeep.cs)
    [HarmonyPatch(typeof(BaseAI), "Follow")]
    static class TrollWaterKeep_KeepAway
    {
        static bool Prefix(BaseAI __instance, GameObject go, float dt) { return global::SteveTheTroll.Trolls.Water.Infrastructure.TrollWaterKeep.KeepAway.Prefix(__instance, go, dt); }
    }

    // TrollWaterKeep.NoShipDamage (Features/Trolls/Water/Infrastructure/TrollWaterKeep.cs)
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
    static class TrollWaterKeep_NoShipDamage
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(WearNTear __instance, HitData hit) { return global::SteveTheTroll.Trolls.Water.Infrastructure.TrollWaterKeep.NoShipDamage.Prefix(__instance, hit); }
    }
}
