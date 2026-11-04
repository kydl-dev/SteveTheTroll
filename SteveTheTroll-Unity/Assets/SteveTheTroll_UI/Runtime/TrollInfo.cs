using System;
using System.Globalization;
using System.Text;

namespace SteveTheTroll.Ui;

/// <summary>
///     One of the player's tamed trolls as the F6 tabs show it (D-043). Plain data, no Unity / Valheim types. The game
///     keeps one per troll in
///     the player's profile (TrollRegistry, "last known" while the troll is not loaded - user answer 2026-10-27) through
///     Encode / Decode.
///     Status / Farming are text KEYS (localized by the view).
/// </summary>
internal sealed class TrollInfo
{
    internal const char Sep = '|';

    private const int
        OldFields = 14,
        MidFields = 16,
        PackFields = 18,
        Fields = 19,
        DestFields =
            20; // 14 = before attacks / resistances, 16 = before the backpack fill, 18 = before the biome (all still read)

    /// Attacks as "name~Blunt=60,Chop=100~4" joined by ';' (damage already x the tamed factor, last part = seconds between uses).
    internal string Attacks = "";

    /// D-096: backpack stacks used / all slots of its 4 chests (0 / 0 = not known yet).
    internal int BackpackUsed, BackpackSlots;

    internal float BedDistance = -1f; // metres, -1 = no bed set

    /// D-126: the game's biome name where the troll is / was last seen ("" = not known); the focus drop-down offers its biome items.
    internal string Biome = "";

    internal float DamageFactor = 1f;

    /// D-162: the hub the troll works for (its name); "" = the bed. BedDistance is the distance to THAT place.
    internal string Dest = "";

    /// Died at (UTC ticks, 0 = alive): the entry is removed DeadKeepSeconds later (D-044).
    internal long DiedTicks;

    internal string Farming = ""; // item prefab it gathers now ("" = nothing)
    internal string Focus = ""; // TrollFocus value
    internal float Health, MaxHealth;

    internal string Id = ""; // ZDOID as "user:id"
    internal long LastSeenTicks; // UTC ticks of the last live update
    internal bool Loaded; // near the player right now (live values)
    internal string Name = ""; // renamed name, "" = not renamed (the view shows "Troll N")
    internal int Number; // 1-based order in the player's list

    /// Resistances as "Blunt=Normal,Slash=Resistant,..." (what the game uses for this troll now).
    internal string Resists = "";

    internal int Stars; // level - 1
    internal string Status = "stevethetroll_status_unknown";
    internal float X, Y, Z;

    /// "Troll N" when not renamed.
    internal string Shown(Func<string, string> text)
    {
        return Name.Length > 0 ? Name : (text != null ? text("stevethetroll_troll_default") : "Troll") + " " + Number;
    }

    /// One line for the player's profile. Separator and line breaks in the name are replaced so Decode always gets 20 fields.
    internal string Encode()
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(Clean(Id)).Append(Sep).Append(Clean(Name)).Append(Sep).Append(Clean(Status)).Append(Sep)
            .Append(Clean(Focus)).Append(Sep).Append(Clean(Farming)).Append(Sep)
            .Append(Health.ToString("0.#", c)).Append(Sep).Append(MaxHealth.ToString("0.#", c)).Append(Sep)
            .Append(Stars.ToString(c)).Append(Sep)
            .Append(DamageFactor.ToString("0.###", c)).Append(Sep).Append(X.ToString("0.#", c)).Append(Sep)
            .Append(Y.ToString("0.#", c)).Append(Sep)
            .Append(Z.ToString("0.#", c)).Append(Sep).Append(BedDistance.ToString("0.#", c)).Append(Sep)
            .Append(LastSeenTicks.ToString(c)).Append(Sep).Append(Clean(Attacks)).Append(Sep).Append(Clean(Resists))
            .Append(Sep).Append(BackpackUsed.ToString(c)).Append(Sep).Append(BackpackSlots.ToString(c)).Append(Sep)
            .Append(Clean(Biome)).Append(Sep).Append(Clean(Dest));
        return sb.ToString();
    }

    /// Expected outcome: the info, or null for a line that is not 14 / 16 / 18 / 19 fields / has no id / bad numbers.
    internal static TrollInfo Decode(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }

        string[] f = line.Split(Sep);
        if ((f.Length != DestFields && f.Length != Fields && f.Length != PackFields && f.Length != MidFields &&
             f.Length != OldFields) || f[0].Length == 0)
        {
            return null;
        }

        CultureInfo c = CultureInfo.InvariantCulture;
        var t = new TrollInfo { Id = f[0], Name = f[1], Status = f[2], Focus = f[3], Farming = f[4] };
        // every field is parsed even when an earlier one failed (so all out values are set): '&&' with the result last
        bool ok = float.TryParse(f[5], NumberStyles.Float, c, out t.Health);
        ok = float.TryParse(f[6], NumberStyles.Float, c, out t.MaxHealth) && ok;
        ok = int.TryParse(f[7], NumberStyles.Integer, c, out t.Stars) && ok;
        ok = float.TryParse(f[8], NumberStyles.Float, c, out t.DamageFactor) && ok;
        ok = float.TryParse(f[9], NumberStyles.Float, c, out t.X) && ok;
        ok = float.TryParse(f[10], NumberStyles.Float, c, out t.Y) && ok;
        ok = float.TryParse(f[11], NumberStyles.Float, c, out t.Z) && ok;
        ok = float.TryParse(f[12], NumberStyles.Float, c, out t.BedDistance) && ok;
        ok = long.TryParse(f[13], NumberStyles.Integer, c, out t.LastSeenTicks) && ok;
        if (f.Length >= MidFields)
        {
            t.Attacks = f[14];
            t.Resists = f[15];
        }

        if (f.Length >= PackFields)
        {
            ok = int.TryParse(f[16], NumberStyles.Integer, c, out t.BackpackUsed) && ok;
            ok = int.TryParse(f[17], NumberStyles.Integer, c, out t.BackpackSlots) && ok;
            if (t.BackpackUsed < 0 || t.BackpackSlots < 0)
            {
                ok = false;
            }
        }

        if (f.Length >= Fields)
        {
            t.Biome = f[18];
        }

        if (f.Length == DestFields)
        {
            t.Dest = f[19];
        }

        return ok ? t : null;
    }

    private static string Clean(string s)
    {
        return (s ?? "").Replace(Sep, '/').Replace('\n', ' ').Replace('\r', ' ');
    }

    public override string ToString()
    {
        return Shown(null) + " [" + Id + "] " + (Loaded ? "loaded" : "last seen") + ", " + Status + ", focus '" +
               Focus + "', hp " + Health + "/" + MaxHealth;
    }
}
