using HarmonyLib;
using Kydl.Common;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// D-047: the game's own messages about OUR trolls (follow / stay / pet in Tameable.RPC_Command / Interact, "tamed" in Tameable.Tame)
    /// go to the left side in green (D-048); the game's "tamed" text is dropped - TrollTamingPatch always shows "Steve / Spencer is coming!".
    /// The game builds the follow / stay text from the troll's name (renamed: Steve N / Spencer N), so it never says "Troll" for them.
    /// </summary>
    internal static class TrollMessages
    {
        static int _scope;
        internal static bool Passing;           // Notify's own messages: already left + coloured

        static void Enter(Tameable t) { if (Taming.TrollTaming.IsTroll(t)) _scope++; }
        static void Leave(Tameable t) { if (Taming.TrollTaming.IsTroll(t) && _scope > 0) _scope--; }

        [HarmonyPatch(typeof(Tameable), "RPC_Command")]
        static class Command { static void Prefix(Tameable __instance) { Enter(__instance); } static void Finalizer(Tameable __instance) { Leave(__instance); } }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
        static class Interact { static void Prefix(Tameable __instance) { Enter(__instance); } static void Finalizer(Tameable __instance) { Leave(__instance); } }

        [HarmonyPatch(typeof(Tameable), "Tame")]
        static class Tame
        {
            static void Prefix(Tameable __instance) { Enter(__instance); }
            static void Finalizer(Tameable __instance) { Leave(__instance); }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Message))]
        static class ToLeft
        {
            static bool Prefix(ref MessageHud.MessageType type, ref string msg)
            {
                if (Passing || _scope <= 0) return true;
                string before = msg;
                // TEST-019: the game only tells the CLOSEST player within a few metres when a creature is tamed - mostly nobody. Our
                // "X is coming!" is always shown by TrollTamingPatch (Notify), so the game's own text is dropped here.
                if (msg != null && msg.Contains("$hud_tamedone")) { ModLog.Verbose("TrollMessages: game message '" + before + "' dropped (ours is shown)"); return false; }
                msg = Notify.Paint(msg, Notify.Kind.Good);
                type = MessageHud.MessageType.TopLeft;
                ModLog.Verbose("TrollMessages: game message '" + before + "' -> left, green");
                return true;
            }
        }
    }
}
