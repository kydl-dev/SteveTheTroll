using HarmonyLib;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;

namespace SteveTheTroll.Integrations.Harmony;
// MIGRATION_DECISIONS A5: every Harmony patch of the TrollsHome feature, declared here; each one only forwards to the feature's own method
// (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

// TrollHome.Logout (Features/Trolls/Home/Infrastructure/TrollHome.cs)
[HarmonyPatch(typeof(Game), nameof(Game.Logout))]
internal static class TrollHome_Logout
{
    private static void Prefix()
    {
        TrollHome.Logout.Prefix();
    }
}
