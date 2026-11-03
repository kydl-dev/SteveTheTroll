using HarmonyLib;
using SteveTheTroll.Presentation.Notifications;

namespace SteveTheTroll.Integrations.Harmony;
// MIGRATION_DECISIONS A5: every Harmony patch of the Presentation feature, declared here; each one only forwards to the feature's own method
// (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

// TrollMessages.Command (Presentation/Notifications/TrollMessages.cs)
[HarmonyPatch(typeof(Tameable), "RPC_Command")]
internal static class TrollMessages_Command
{
    private static void Prefix(Tameable __instance)
    {
        TrollMessages.Command.Prefix(__instance);
    }

    private static void Finalizer(Tameable __instance)
    {
        TrollMessages.Command.Finalizer(__instance);
    }
}

// TrollMessages.Interact (Presentation/Notifications/TrollMessages.cs)
[HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
internal static class TrollMessages_Interact
{
    private static void Prefix(Tameable __instance)
    {
        TrollMessages.Interact.Prefix(__instance);
    }

    private static void Finalizer(Tameable __instance)
    {
        TrollMessages.Interact.Finalizer(__instance);
    }
}

// TrollMessages.Tame (Presentation/Notifications/TrollMessages.cs)
[HarmonyPatch(typeof(Tameable), "Tame")]
internal static class TrollMessages_Tame
{
    private static void Prefix(Tameable __instance)
    {
        TrollMessages.Tame.Prefix(__instance);
    }

    private static void Finalizer(Tameable __instance)
    {
        TrollMessages.Tame.Finalizer(__instance);
    }
}

// TrollMessages.ToLeft (Presentation/Notifications/TrollMessages.cs)
[HarmonyPatch(typeof(Player), nameof(Player.Message))]
internal static class TrollMessages_ToLeft
{
    private static bool Prefix(ref MessageHud.MessageType type, ref string msg)
    {
        return TrollMessages.ToLeft.Prefix(ref type, ref msg);
    }
}
