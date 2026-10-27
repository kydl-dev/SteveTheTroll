using System;
using System.Collections.Generic;
using System.Globalization;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// Trolls tab (D-043): detail blocks of one tamed troll - General (status, stars, health, damage, focus, farming) and Position (x / y / z,
    /// distance from the player's bed, live or last seen). Pure logic (unit-tested: TrollDetailsBuilderTests). `nowTicks` = UTC now.
    /// </summary>
    internal static class TrollDetailsBuilder
    {
        internal static List<DetailGroup> Build(TrollInfo t, Func<string, string> text, long nowTicks)
        {
            var groups = new List<DetailGroup>();
            if (t == null || text == null) return groups;
            var c = CultureInfo.InvariantCulture;
            var g = new DetailGroup(text("stevethetroll_troll_general"));
            g.Add(text("stevethetroll_troll_status"), text(t.Status) + (t.Loaded ? "" : " (" + text("stevethetroll_troll_lastseen") + " " + Ago(nowTicks - t.LastSeenTicks, text) + ")"));
            g.Add(text("stevethetroll_troll_stars"), t.Stars > 0 ? new string('*', t.Stars) : "-");
            g.Add(text("stevethetroll_troll_health"), t.Health.ToString("0", c) + " / " + t.MaxHealth.ToString("0", c));
            g.Add(text("stevethetroll_troll_damage"), "x" + t.DamageFactor.ToString("0.##", c));
            g.Add(text("stevethetroll_focus_label"), text(TrollFocus.KeyOf(t.Focus)));
            g.Add(text("stevethetroll_troll_farming"), t.Farming.Length > 0 ? t.Farming : "-");
            groups.Add(g);
            var p = new DetailGroup(text("stevethetroll_troll_position"));
            p.Add("X / Y / Z", t.X.ToString("0", c) + " / " + t.Y.ToString("0", c) + " / " + t.Z.ToString("0", c));
            p.Add(text("stevethetroll_troll_bed"), t.BedDistance >= 0f ? t.BedDistance.ToString("0", c) + " m" : text("stevethetroll_troll_nobed"));
            groups.Add(p);
            return groups;
        }

        /// "N s / min / h" for a tick span (negative = 0 s).
        internal static string Ago(long ticks, Func<string, string> text)
        {
            double s = Math.Max(0, ticks) / (double)TimeSpan.TicksPerSecond;
            var c = CultureInfo.InvariantCulture;
            if (s < 60) return s.ToString("0", c) + " s";
            if (s < 3600) return (s / 60).ToString("0", c) + " min";
            return (s / 3600).ToString("0.#", c) + " h";
        }
    }
}
