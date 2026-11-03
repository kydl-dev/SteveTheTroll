using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: every Harmony patch of the Presentation feature, declared here; each one only forwards to the feature's own method
    // (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

    // TrollMessages.Command (Presentation/Notifications/TrollMessages.cs)
    [HarmonyPatch(typeof(Tameable), "RPC_Command")]
    static class TrollMessages_Command
    {
        static void Prefix(Tameable __instance) { global::SteveTheTroll.Presentation.Notifications.TrollMessages.Command.Prefix(__instance); }
        static void Finalizer(Tameable __instance) { global::SteveTheTroll.Presentation.Notifications.TrollMessages.Command.Finalizer(__instance); }
    }

    // TrollMessages.Interact (Presentation/Notifications/TrollMessages.cs)
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
    static class TrollMessages_Interact
    {
        static void Prefix(Tameable __instance) { global::SteveTheTroll.Presentation.Notifications.TrollMessages.Interact.Prefix(__instance); }
        static void Finalizer(Tameable __instance) { global::SteveTheTroll.Presentation.Notifications.TrollMessages.Interact.Finalizer(__instance); }
    }

    // TrollMessages.Tame (Presentation/Notifications/TrollMessages.cs)
    [HarmonyPatch(typeof(Tameable), "Tame")]
    static class TrollMessages_Tame
    {
        static void Prefix(Tameable __instance) { global::SteveTheTroll.Presentation.Notifications.TrollMessages.Tame.Prefix(__instance); }
        static void Finalizer(Tameable __instance) { global::SteveTheTroll.Presentation.Notifications.TrollMessages.Tame.Finalizer(__instance); }
    }

    // TrollMessages.ToLeft (Presentation/Notifications/TrollMessages.cs)
    [HarmonyPatch(typeof(Player), nameof(Player.Message))]
    static class TrollMessages_ToLeft
    {
        static bool Prefix(ref MessageHud.MessageType type, ref string msg) { return global::SteveTheTroll.Presentation.Notifications.TrollMessages.ToLeft.Prefix(ref type, ref msg); }
    }
}
