using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: every Harmony patch of the TrollsFarming feature, declared here; each one only forwards to the feature's own method
    // (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

    // TrollFarming.Steer (Features/Trolls/Farming/Infrastructure/TrollFarming.cs)
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    static class TrollFarming_Steer
    {
        static bool Prefix(MonsterAI __instance, float dt, ref bool __result) { return global::SteveTheTroll.Features.Trolls.Farming.Infrastructure.TrollFarming.Steer.Prefix(__instance, dt, ref __result); }
    }
}
