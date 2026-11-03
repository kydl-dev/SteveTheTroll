using HarmonyLib;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;

namespace SteveTheTroll.Integrations.Harmony;
// MIGRATION_DECISIONS A5: every Harmony patch of the TrollsRegistry feature, declared here; each one only forwards to the feature's own method
// (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

// TrollRegistry.Death (Features/Trolls/Registry/Infrastructure/TrollRegistry.cs)
[HarmonyPatch(typeof(Tameable), "OnDeath")]
internal static class TrollRegistry_Death
{
    private static void Postfix(Tameable __instance)
    {
        TrollRegistry.Death.Postfix(__instance);
    }
}
