using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Backpack.Application;
using SteveTheTroll.Backpack.Domain;
using SteveTheTroll.Backpack.Infrastructure;
using SteveTheTroll.Hubs.Domain;
using SteveTheTroll.Hubs.Infrastructure;
using SteveTheTroll.Trolls.Registry.Infrastructure;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: every Harmony patch of the TrollsBackpack feature, declared here; each one only forwards to the feature's own method
    // (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

    // BackpackDeath (Features/Trolls/Backpack/Infrastructure/BackpackDeath.cs)
    [HarmonyPatch(typeof(Tameable), "OnDeath")]
    static class BackpackDeathHook
    {
        static void Prefix(Tameable __instance) { global::SteveTheTroll.Backpack.Infrastructure.BackpackDeath.Prefix(__instance); }
    }

    // BackpackStore.LoadPatch (Features/Trolls/Backpack/Infrastructure/BackpackStore.cs)
    [HarmonyPatch(typeof(Container), "Load")]
    static class BackpackStore_LoadPatch
    {
        static bool Prefix(Container __instance, ref bool __result) { return global::SteveTheTroll.Backpack.Infrastructure.BackpackStore.LoadPatch.Prefix(__instance, ref __result); }
    }

    // BackpackStore.SavePatch (Features/Trolls/Backpack/Infrastructure/BackpackStore.cs)
    [HarmonyPatch(typeof(Container), "Save")]
    static class BackpackStore_SavePatch
    {
        static bool Prefix(Container __instance) { return global::SteveTheTroll.Backpack.Infrastructure.BackpackStore.SavePatch.Prefix(__instance); }
    }

    // TrollDepot.EmptyCheck (Features/Trolls/Backpack/Infrastructure/TrollDepot.cs)
    [HarmonyPatch(typeof(Container), "Save")]
    static class TrollDepot_EmptyCheck
    {
        static void Postfix(Container __instance) { global::SteveTheTroll.Backpack.Infrastructure.TrollDepot.EmptyCheck.Postfix(__instance); }
    }

    // TrollDepot.CloseCheck (Features/Trolls/Backpack/Infrastructure/TrollDepot.cs)
    [HarmonyPatch(typeof(Container), "SetInUse")]
    static class TrollDepot_CloseCheck
    {
        static void Postfix(Container __instance, bool inUse) { global::SteveTheTroll.Backpack.Infrastructure.TrollDepot.CloseCheck.Postfix(__instance, inUse); }
    }

    // TrollDepot.ChestFlags (Features/Trolls/Backpack/Infrastructure/TrollDepot.cs)
    [HarmonyPatch(typeof(WearNTear), "Awake")]
    static class TrollDepot_ChestFlags
    {
        static void Postfix(WearNTear __instance) { global::SteveTheTroll.Backpack.Infrastructure.TrollDepot.ChestFlags.Postfix(__instance); }
    }
}
