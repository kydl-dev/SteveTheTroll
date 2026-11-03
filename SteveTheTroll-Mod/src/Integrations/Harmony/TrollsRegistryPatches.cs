using System.Collections.Generic;
using System;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Backpack.Infrastructure;
using SteveTheTroll.Hubs.Domain;
using SteveTheTroll.Hubs.Infrastructure;
using SteveTheTroll.Trolls.Farming.Infrastructure;
using SteveTheTroll.Trolls.Home.Infrastructure;
using SteveTheTroll.Trolls.Registry.Infrastructure;
using SteveTheTroll.Trolls.Taming.Domain;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: every Harmony patch of the TrollsRegistry feature, declared here; each one only forwards to the feature's own method
    // (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

    // TrollRegistry.Death (Features/Trolls/Registry/Infrastructure/TrollRegistry.cs)
    [HarmonyPatch(typeof(Tameable), "OnDeath")]
    static class TrollRegistry_Death
    {
        static void Postfix(Tameable __instance) { global::SteveTheTroll.Trolls.Registry.Infrastructure.TrollRegistry.Death.Postfix(__instance); }
    }
}
