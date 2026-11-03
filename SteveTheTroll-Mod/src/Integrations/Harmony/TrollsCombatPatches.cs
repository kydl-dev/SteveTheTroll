using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Combat.Application;
using SteveTheTroll.Trolls.Combat.Domain;
using SteveTheTroll.Trolls.Combat.Execution;
using SteveTheTroll.Trolls.Combat.Infrastructure;
using SteveTheTroll.Trolls.Farming.Infrastructure;
using SteveTheTroll.Trolls.Home.Infrastructure;
using SteveTheTroll.Trolls.Orders.Commands;
using SteveTheTroll.Trolls.Orders.Infrastructure;
using SteveTheTroll.Trolls.Registry.Infrastructure;
using SteveTheTroll.Trolls.Taming.Domain;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: every Harmony patch of the TrollsCombat feature, declared here; each one only forwards to the feature's own method
    // (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

    // PlayerPieceGuard.GuardDamage (Features/Trolls/Combat/Infrastructure/PlayerPieceGuard.cs)
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
    static class PlayerPieceGuard_GuardDamage
    {
        static bool Prefix(WearNTear __instance, HitData hit) { return global::SteveTheTroll.Trolls.Combat.Infrastructure.PlayerPieceGuard.GuardDamage.Prefix(__instance, hit); }
    }

    // TrollDefend.PlayerHit (Features/Trolls/Combat/Infrastructure/TrollDefend.cs)
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    static class TrollDefend_PlayerHit
    {
        static void Postfix(Character __instance, HitData hit) { global::SteveTheTroll.Trolls.Combat.Infrastructure.TrollDefend.PlayerHit.Postfix(__instance, hit); }
    }

    // TrollFarmHits (Features/Trolls/Combat/Infrastructure/TrollFarmHits.cs)
    [HarmonyPatch]
    static class TrollFarmHitsHook
    {
        static IEnumerable<MethodBase> TargetMethods() { return global::SteveTheTroll.Trolls.Combat.Infrastructure.TrollFarmHits.TargetMethods(); }
        static void Prefix(object __instance, HitData hit) { global::SteveTheTroll.Trolls.Combat.Infrastructure.TrollFarmHits.Prefix(__instance, hit); }
    }

    // TrollNoHit.NoSwing (Features/Trolls/Combat/Infrastructure/TrollNoHit.cs)
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    static class TrollNoHit_NoSwing
    {
        static bool Prefix(Humanoid __instance, ref bool __result) { return global::SteveTheTroll.Trolls.Combat.Infrastructure.TrollNoHit.NoSwing.Prefix(__instance, ref __result); }
    }

    // TrollWalls.WallHit (Features/Trolls/Combat/Infrastructure/TrollWalls.cs)
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
    static class TrollWalls_WallHit
    {
        static void Prefix(WearNTear __instance, HitData hit) { global::SteveTheTroll.Trolls.Combat.Infrastructure.TrollWalls.WallHit.Prefix(__instance, hit); }
    }
}
