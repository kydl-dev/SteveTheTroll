using System;
using System.Globalization;
using System.Text;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// One of the player's tamed trolls as the F6 tabs show it (D-043). Plain data, no Unity / Valheim types. The game keeps one per troll in
    /// the player's profile (TrollRegistry, "last known" while the troll is not loaded - user answer 2026-10-27) through Encode / Decode.
    /// Status / Farming are text KEYS (localized by the view).
    /// </summary>
    internal sealed class TrollInfo
    {
        internal const char Sep = '|';
        const int OldFields = 14, MidFields = 16, Fields = 18;     // 14 = before attacks / resistances, 16 = before the backpack fill (still read)

        internal string Id = "";            // ZDOID as "user:id"
        internal string Name = "";          // renamed name, "" = not renamed (the view shows "Troll N")
        internal int Number;                // 1-based order in the player's list
        internal bool Loaded;               // near the player right now (live values)
        internal string Status = "stevethetroll_status_unknown";
        internal string Focus = "";         // TrollFocus value
        internal string Farming = "";       // item prefab it gathers now ("" = nothing)
        internal float Health, MaxHealth;
        internal int Stars;                 // level - 1
        internal float DamageFactor = 1f;
        internal float X, Y, Z;
        internal float BedDistance = -1f;   // metres, -1 = no bed set
        internal long LastSeenTicks;        // UTC ticks of the last live update
        /// Attacks as "name~Blunt=60,Chop=100~4" joined by ';' (damage already x the tamed factor, last part = seconds between uses).
        internal string Attacks = "";
        /// Resistances as "Blunt=Normal,Slash=Resistant,..." (what the game uses for this troll now).
        internal string Resists = "";
        /// Died at (UTC ticks, 0 = alive): the entry is removed DeadKeepSeconds later (D-044).
        internal long DiedTicks;
        /// D-096: backpack stacks used / all slots of its 4 chests (0 / 0 = not known yet).
        internal int BackpackUsed, BackpackSlots;

        /// "Troll N" when not renamed.
        internal string Shown(Func<string, string> text)
        {
            return Name.Length > 0 ? Name : (text != null ? text("stevethetroll_troll_default") : "Troll") + " " + Number;
        }

        /// One line for the player's profile. Separator and line breaks in the name are replaced so Decode always gets 16 fields.
        internal string Encode()
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(Clean(Id)).Append(Sep).Append(Clean(Name)).Append(Sep).Append(Clean(Status)).Append(Sep).Append(Clean(Focus)).Append(Sep).Append(Clean(Farming)).Append(Sep)
              .Append(Health.ToString("0.#", c)).Append(Sep).Append(MaxHealth.ToString("0.#", c)).Append(Sep).Append(Stars.ToString(c)).Append(Sep)
              .Append(DamageFactor.ToString("0.###", c)).Append(Sep).Append(X.ToString("0.#", c)).Append(Sep).Append(Y.ToString("0.#", c)).Append(Sep)
              .Append(Z.ToString("0.#", c)).Append(Sep).Append(BedDistance.ToString("0.#", c)).Append(Sep).Append(LastSeenTicks.ToString(c)).Append(Sep).Append(Clean(Attacks)).Append(Sep).Append(Clean(Resists))
              .Append(Sep).Append(BackpackUsed.ToString(c)).Append(Sep).Append(BackpackSlots.ToString(c));
            return sb.ToString();
        }

        /// Expected outcome: the info, or null for a line that is not 14 / 16 fields / has no id / bad numbers.
        internal static TrollInfo Decode(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            var f = line.Split(Sep);
            if ((f.Length != Fields && f.Length != MidFields && f.Length != OldFields) || f[0].Length == 0) return null;
            var c = CultureInfo.InvariantCulture;
            var t = new TrollInfo { Id = f[0], Name = f[1], Status = f[2], Focus = f[3], Farming = f[4] };
            bool ok = float.TryParse(f[5], NumberStyles.Float, c, out t.Health) & float.TryParse(f[6], NumberStyles.Float, c, out t.MaxHealth)
                & int.TryParse(f[7], NumberStyles.Integer, c, out t.Stars) & float.TryParse(f[8], NumberStyles.Float, c, out t.DamageFactor)
                & float.TryParse(f[9], NumberStyles.Float, c, out t.X) & float.TryParse(f[10], NumberStyles.Float, c, out t.Y)
                & float.TryParse(f[11], NumberStyles.Float, c, out t.Z) & float.TryParse(f[12], NumberStyles.Float, c, out t.BedDistance)
                & long.TryParse(f[13], NumberStyles.Integer, c, out t.LastSeenTicks);
            if (f.Length >= MidFields) { t.Attacks = f[14]; t.Resists = f[15]; }
            if (f.Length == Fields)
            {
                ok &= int.TryParse(f[16], NumberStyles.Integer, c, out t.BackpackUsed) & int.TryParse(f[17], NumberStyles.Integer, c, out t.BackpackSlots);
                if (t.BackpackUsed < 0 || t.BackpackSlots < 0) ok = false;
            }
            return ok ? t : null;
        }

        static string Clean(string s) { return (s ?? "").Replace(Sep, '/').Replace('\n', ' ').Replace('\r', ' '); }

        public override string ToString() { return Shown(null) + " [" + Id + "] " + (Loaded ? "loaded" : "last seen") + ", " + Status + ", focus '" + Focus + "', hp " + Health + "/" + MaxHealth; }
    }
}
