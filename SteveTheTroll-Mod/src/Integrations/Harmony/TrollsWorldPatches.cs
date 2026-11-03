using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Combat.Domain;
using SteveTheTroll.Trolls.Registry.Infrastructure;
using SteveTheTroll.Trolls.Taming.Domain;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using SteveTheTroll.World.Spawning;
using SteveTheTroll.World.Zones;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: every Harmony patch of the TrollsWorld feature, declared here; each one only forwards to the feature's own method
    // (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

    // TrollSpawnBoost.SpawnList (Features/Trolls/World/Infrastructure/TrollSpawnBoost.cs)
    [HarmonyPatch(typeof(SpawnSystem), "UpdateSpawnList")]
    static class TrollSpawnBoost_SpawnList
    {
        static void Prefix(SpawnSystem __instance, List<SpawnSystem.SpawnData> spawners, bool eventSpawners) { global::SteveTheTroll.World.Spawning.TrollSpawnBoost.SpawnList.Prefix(__instance, spawners, eventSpawners); }
        static void Finalizer() { global::SteveTheTroll.World.Spawning.TrollSpawnBoost.SpawnList.Finalizer(); }
    }

    // TrollSpawnBoost.SpawnRing (Features/Trolls/World/Infrastructure/TrollSpawnBoost.cs)
    [HarmonyPatch(typeof(SpawnSystem), "IsSpawnPointGood")]
    static class TrollSpawnBoost_SpawnRing
    {
        static void Postfix(SpawnSystem.SpawnData spawn, ref Vector3 spawnPoint, ref bool __result) { global::SteveTheTroll.World.Spawning.TrollSpawnBoost.SpawnRing.Postfix(spawn, ref spawnPoint, ref __result); }
    }

    // TrollSpawnBoost.SpawnOne (Features/Trolls/World/Infrastructure/TrollSpawnBoost.cs)
    [HarmonyPatch(typeof(SpawnSystem), "Spawn")]
    static class TrollSpawnBoost_SpawnOne
    {
        static void Prefix(SpawnSystem.SpawnData critter, Vector3 spawnPoint) { global::SteveTheTroll.World.Spawning.TrollSpawnBoost.SpawnOne.Prefix(critter, spawnPoint); }
    }

    // TrollSpawnBoost.LogForBoosted (Features/Trolls/World/Infrastructure/TrollSpawnBoost.cs)
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GiveDefaultItems))]
    static class TrollSpawnBoost_LogForBoosted
    {
        static void Prefix(Humanoid __instance) { global::SteveTheTroll.World.Spawning.TrollSpawnBoost.LogForBoosted.Prefix(__instance); }
    }

    // TrollZones.Create (Features/Trolls/World/Infrastructure/TrollZones.cs)
    [HarmonyPatch(typeof(ZNetScene), "CreateObjects")]
    static class TrollZones_Create
    {
        static void Postfix() { global::SteveTheTroll.World.Zones.TrollZones.Create.Postfix(); }
        static void Prefix(List<ZDO> currentNearObjects) { global::SteveTheTroll.World.Zones.TrollZones.Create.Prefix(currentNearObjects); }
    }
}
