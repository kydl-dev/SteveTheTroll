using System;
using System.Collections.Generic;
using System.Globalization;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// Trolls tab (D-043 / D-044): detail blocks of one tamed troll - General (status, stars, health, damage, focus, farming), Attacks (each
    /// attack's damage types, already x the tamed factor, and its interval), Resistances, Position (x / y / z, distance from the bed). Pure logic (unit-tested: TrollDetailsBuilderTests). `nowTicks` = UTC now.
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
            g.Add(text("stevethetroll_troll_farming"), TrollFocus.Shown(t.Farming, text));
            groups.Add(g);
            var dmg = new DetailGroup(text("stevethetroll_troll_attacks"));
            foreach (var row in Attacks(t.Attacks, text)) dmg.Add(row.Key, row.Value);
            if (dmg.Rows.Count == 0) dmg.Add("-", text("stevethetroll_troll_noattacks"));
            groups.Add(dmg);
            var res = new DetailGroup(text("stevethetroll_troll_resists"));
            foreach (var part in (t.Resists ?? "").Split(','))
            {
                int eq = part.IndexOf('=');
                if (eq > 0) res.Add(part.Substring(0, eq), part.Substring(eq + 1));
            }
            if (res.Rows.Count == 0) res.Add("-", text("stevethetroll_troll_noattacks"));
            groups.Add(res);
            var p = new DetailGroup(text("stevethetroll_troll_position"));
            p.Add("X / Y / Z", t.X.ToString("0", c) + " / " + t.Y.ToString("0", c) + " / " + t.Z.ToString("0", c));
            p.Add(text("stevethetroll_troll_bed"), t.BedDistance >= 0f ? t.BedDistance.ToString("0", c) + " m" : text("stevethetroll_troll_nobed"));
            groups.Add(p);
            return groups;
        }

        /// D-044: "name~Blunt=60,Chop=100~4;..." -> (name, "60 Blunt, 100 Chop (every 4 s)"). Broken parts are skipped.
        internal static List<KeyValuePair<string, string>> Attacks(string encoded, Func<string, string> text)
        {
            var rows = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrEmpty(encoded)) return rows;
            foreach (var a in encoded.Split(';'))
            {
                var f = a.Split('~');
                if (f.Length != 3 || f[0].Length == 0) continue;
                var sb = new System.Text.StringBuilder();
                foreach (var d in f[1].Split(','))
                {
                    int eq = d.IndexOf('=');
                    if (eq <= 0) continue;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(d.Substring(eq + 1)).Append(' ').Append(d.Substring(0, eq));
                }
                if (f[2].Length > 0 && f[2] != "0") sb.Append(" (").Append(text("stevethetroll_troll_every")).Append(' ').Append(f[2]).Append(" s)");
                rows.Add(new KeyValuePair<string, string>(f[0], sb.Length > 0 ? sb.ToString() : "-"));
            }
            return rows;
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
