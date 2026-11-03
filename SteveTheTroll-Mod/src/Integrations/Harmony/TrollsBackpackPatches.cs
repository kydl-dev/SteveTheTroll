using HarmonyLib;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;

namespace SteveTheTroll.Integrations.Harmony;
// MIGRATION_DECISIONS A5: every Harmony patch of the TrollsBackpack feature, declared here; each one only forwards to the feature's own method
// (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

// BackpackDeath (Features/Trolls/Backpack/Infrastructure/BackpackDeath.cs)
[HarmonyPatch(typeof(Tameable), "OnDeath")]
internal static class BackpackDeathHook
{
    private static void Prefix(Tameable __instance)
    {
        BackpackDeath.Prefix(__instance);
    }
}

// BackpackStore.LoadPatch (Features/Trolls/Backpack/Infrastructure/BackpackStore.cs)
[HarmonyPatch(typeof(Container), "Load")]
internal static class BackpackStore_LoadPatch
{
    private static bool Prefix(Container __instance, ref bool __result)
    {
        return BackpackStore.LoadPatch.Prefix(__instance, ref __result);
    }
}

// BackpackStore.SavePatch (Features/Trolls/Backpack/Infrastructure/BackpackStore.cs)
[HarmonyPatch(typeof(Container), "Save")]
internal static class BackpackStore_SavePatch
{
    private static bool Prefix(Container __instance)
    {
        return BackpackStore.SavePatch.Prefix(__instance);
    }
}

// TrollDepot.EmptyCheck (Features/Trolls/Backpack/Infrastructure/TrollDepot.cs)
[HarmonyPatch(typeof(Container), "Save")]
internal static class TrollDepot_EmptyCheck
{
    private static void Postfix(Container __instance)
    {
        TrollDepot.EmptyCheck.Postfix(__instance);
    }
}

// TrollDepot.CloseCheck (Features/Trolls/Backpack/Infrastructure/TrollDepot.cs)
[HarmonyPatch(typeof(Container), "SetInUse")]
internal static class TrollDepot_CloseCheck
{
    private static void Postfix(Container __instance, bool inUse)
    {
        TrollDepot.CloseCheck.Postfix(__instance, inUse);
    }
}

// TrollDepot.ChestFlags (Features/Trolls/Backpack/Infrastructure/TrollDepot.cs)
[HarmonyPatch(typeof(WearNTear), "Awake")]
internal static class TrollDepot_ChestFlags
{
    private static void Postfix(WearNTear __instance)
    {
        TrollDepot.ChestFlags.Postfix(__instance);
    }
}
