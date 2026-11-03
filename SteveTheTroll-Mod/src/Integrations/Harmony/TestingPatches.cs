using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Testing;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: every Harmony patch of the Testing feature, declared here; each one only forwards to the feature's own method
    // (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

    // RecoveryTestHooks.CountWallHits (Testing/TestHooks/RecoveryTestHooks.cs)
    [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
    static class RecoveryTestHooks_CountWallHits
    {
        static void Prefix(WearNTear __instance, HitData hit) { global::SteveTheTroll.Testing.TestHooks.RecoveryTestHooks.CountWallHits.Prefix(__instance, hit); }
    }
}
