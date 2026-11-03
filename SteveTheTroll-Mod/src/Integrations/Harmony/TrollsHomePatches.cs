using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: every Harmony patch of the TrollsHome feature, declared here; each one only forwards to the feature's own method
    // (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

    // TrollHome.Logout (Features/Trolls/Home/Infrastructure/TrollHome.cs)
    [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
    static class TrollHome_Logout
    {
        static void Prefix() { global::SteveTheTroll.Features.Trolls.Home.Infrastructure.TrollHome.Logout.Prefix(); }
    }
}
