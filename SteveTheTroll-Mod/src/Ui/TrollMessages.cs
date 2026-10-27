using HarmonyLib;
using Kydl.Common;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// D-047: the game's own messages about OUR trolls (follow / stay / pet in Tameable.RPC_Command / Interact, "tamed" in Tameable.Tame)
    /// go to the left side like ours; the "tamed" one becomes "Steve is coming!" / "Spencer is coming!" (the troll is named just before Tame).
    /// The game builds the follow / stay text from the troll's name (renamed: Steve N / Spencer N), so it never says "Troll" for them.
    /// </summary>
    internal static class TrollMessages
    {
        static int _scope;
        static string _tameText;
        internal static bool Passing;           // Notify's own messages: already left + coloured

        static void Enter(Tameable t) { if (Taming.TrollTaming.IsTroll(t)) _scope++; }
        static void Leave(Tameable t) { if (Taming.TrollTaming.IsTroll(t) && _scope > 0) _scope--; }

        /// Set by TrollTamingPatch right before it tames: the text that replaces the game's "tamed" message.
        internal static void ComingText(string text) { _tameText = text; }

        [HarmonyPatch(typeof(Tameable), "RPC_Command")]
        static class Command { static void Prefix(Tameable __instance) { Enter(__instance); } static void Finalizer(Tameable __instance) { Leave(__instance); } }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
        static class Interact { static void Prefix(Tameable __instance) { Enter(__instance); } static void Finalizer(Tameable __instance) { Leave(__instance); } }

        [HarmonyPatch(typeof(Tameable), "Tame")]
        static class Tame
        {
            static void Prefix(Tameable __instance) { Enter(__instance); }
            static void Finalizer(Tameable __instance) { Leave(__instance); _tameText = null; }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Message))]
        static class ToLeft
        {
            static void Prefix(ref MessageHud.MessageType type, ref string msg)
            {
                if (Passing || _scope <= 0) return;
                string before = msg;
                bool tame = _tameText != null && msg != null && msg.Contains("$hud_tamedone");
                msg = tame ? Notify.Paint(_tameText, Notify.Kind.Good) : Notify.Paint(msg, Notify.Kind.Info);
                type = MessageHud.MessageType.TopLeft;
                ModLog.Verbose("TrollMessages: game message '" + before + "' -> left" + (tame ? " as '" + _tameText + "'" : ""));
            }
        }
    }
}
