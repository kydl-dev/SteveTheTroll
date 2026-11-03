using System.Collections.Generic;
using HarmonyLib;
using SteveTheTroll.Features.Trolls.World.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony;
// MIGRATION_DECISIONS A5: every Harmony patch of the TrollsWorld feature, declared here; each one only forwards to the feature's own method
// (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

// TrollSpawnBoost.SpawnList (Features/Trolls/World/Infrastructure/TrollSpawnBoost.cs)
[HarmonyPatch(typeof(SpawnSystem), "UpdateSpawnList")]
internal static class TrollSpawnBoost_SpawnList
{
    private static void Prefix(SpawnSystem __instance, List<SpawnSystem.SpawnData> spawners, bool eventSpawners)
    {
        TrollSpawnBoost.SpawnList.Prefix(__instance, spawners, eventSpawners);
    }

    private static void Finalizer()
    {
        TrollSpawnBoost.SpawnList.Finalizer();
    }
}

// TrollSpawnBoost.SpawnRing (Features/Trolls/World/Infrastructure/TrollSpawnBoost.cs)
[HarmonyPatch(typeof(SpawnSystem), "IsSpawnPointGood")]
internal static class TrollSpawnBoost_SpawnRing
{
    private static void Postfix(SpawnSystem.SpawnData spawn, ref Vector3 spawnPoint, ref bool __result)
    {
        TrollSpawnBoost.SpawnRing.Postfix(spawn, ref spawnPoint, ref __result);
    }
}

// TrollSpawnBoost.SpawnOne (Features/Trolls/World/Infrastructure/TrollSpawnBoost.cs)
[HarmonyPatch(typeof(SpawnSystem), "Spawn")]
internal static class TrollSpawnBoost_SpawnOne
{
    private static void Prefix(SpawnSystem.SpawnData critter, Vector3 spawnPoint)
    {
        TrollSpawnBoost.SpawnOne.Prefix(critter, spawnPoint);
    }
}

// TrollSpawnBoost.LogForBoosted (Features/Trolls/World/Infrastructure/TrollSpawnBoost.cs)
[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GiveDefaultItems))]
internal static class TrollSpawnBoost_LogForBoosted
{
    private static void Prefix(Humanoid __instance)
    {
        TrollSpawnBoost.LogForBoosted.Prefix(__instance);
    }
}

// TrollZones.Create (Features/Trolls/World/Infrastructure/TrollZones.cs)
[HarmonyPatch(typeof(ZNetScene), "CreateObjects")]
internal static class TrollZones_Create
{
    private static void Postfix()
    {
        TrollZones.Create.Postfix();
    }

    private static void Prefix(List<ZDO> currentNearObjects)
    {
        TrollZones.Create.Prefix(currentNearObjects);
    }
}
