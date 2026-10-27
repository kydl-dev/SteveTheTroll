using Kydl.Common;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// D-047: every message of the mod goes to the LEFT side of the screen (MessageHud TopLeft, normal text), coloured by kind.
    /// D-048 (user colours): Farm dark yellow, Attack red, Good green (coming, follow / stay), Death blue; Info white, Warn orange.
    /// </summary>
    internal static class Notify
    {
        internal enum Kind { Info, Good, Warn, Bad, Farm, Attack, Death }

        static string Colour(Kind k)
        {
            switch (k)
            {
                case Kind.Good: return "#9BE07A";
                case Kind.Warn: return "#FFB347";
                case Kind.Bad: return "#FF6B6B";
                case Kind.Farm: return "#C9A227";
                case Kind.Attack: return "#FF4040";
                case Kind.Death: return "#5AA9FF";
                default: return "#E8E0D0";
            }
        }

        /// The text with the kind's colour (rich text, MessageHud shows it).
        internal static string Paint(string text, Kind kind) { return "<color=" + Colour(kind) + ">" + text + "</color>"; }

        /// Expected outcome: returns whether a local player got the message.
        internal static bool Show(string text, Kind kind)
        {
            var p = Player.m_localPlayer;
            ModLog.Verbose("Notify: [" + kind + "] " + text + (p == null ? " (no local player - not shown)" : ""));
            if (p == null) return p != null;
            TrollMessages.Passing = true;
            try { p.Message(MessageHud.MessageType.TopLeft, Paint(text, kind)); }
            finally { TrollMessages.Passing = false; }
            return p != null;
        }
    }
}
