using System.Collections.Generic;
using System.Linq;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>
    /// D-086: the Trolls tab rows, every group and row checked (Stryker: a dropped / renamed row passed every older test). Text keys are
    /// shown as they are (K = identity), so a wrong key shows up here.
    /// </summary>
    [TestFixture]
    public class TrollDetailsGoldenTests
    {
        static string K(string key) { return key; }

        static TrollInfo Live()
        {
            return new TrollInfo { Id = "a1", Name = "Steve", Number = 1, Status = "stevethetroll_status_farming", Focus = "CopperOre", Farming = "Wood",
                Health = 1234.4f, MaxHealth = 1800f, Stars = 2, DamageFactor = 3.25f, X = 412.4f, Y = 77f, Z = -268.6f, BedDistance = 35.5f, Loaded = true,
                Attacks = "Punch~Blunt=180,Chop=300~4", Resists = "Fire=Resistant,Frost=Weak" };
        }

        static string[] Flat(List<DetailGroup> groups)
        {
            return groups.SelectMany(g => new[] { "# " + g.Title }.Concat(g.Rows.Select(r => r.Label + " = " + r.Value))).ToArray();
        }

        [Test]
        public void LiveTroll_AllGroupsAndRows()
        {
            var rows = Flat(TrollDetailsBuilder.Build(Live(), K, 0L));
            var attack = TrollDetailsBuilder.Attacks("Punch~Blunt=180,Chop=300~4", K).Single();
            Assert.That(rows, Is.EqualTo(new[]
            {
                "# stevethetroll_troll_general",
                "stevethetroll_troll_status = stevethetroll_status_farming",
                "stevethetroll_troll_stars = **",
                "stevethetroll_troll_health = 1234 / 1800",
                "stevethetroll_troll_damage = x3.25",
                "stevethetroll_focus_label = stevethetroll_focus_copper",
                "stevethetroll_troll_farming = stevethetroll_focus_wood",
                "# stevethetroll_troll_attacks",
                attack.Key + " = " + attack.Value,
                "# stevethetroll_troll_resists",
                "Fire = Resistant",
                "Frost = Weak",
                "# stevethetroll_troll_position",
                "X / Y / Z = 412 / 77 / -269",
                "stevethetroll_troll_bed = 36 m",
            }));
        }

        [Test]
        public void EmptyTroll_PlaceholderRows()
        {
            var t = Live();
            t.Stars = 0; t.Attacks = ""; t.Resists = null; t.BedDistance = -1f; t.Farming = "";
            var rows = Flat(TrollDetailsBuilder.Build(t, K, 0L));
            Assert.That(rows, Does.Contain("stevethetroll_troll_stars = -"));
            Assert.That(rows, Does.Contain("stevethetroll_troll_farming = -"));
            Assert.That(rows, Does.Contain("stevethetroll_troll_bed = stevethetroll_troll_nobed"));
            int attacks = System.Array.IndexOf(rows, "# stevethetroll_troll_attacks"), resists = System.Array.IndexOf(rows, "# stevethetroll_troll_resists");
            Assert.That(rows[attacks + 1], Is.EqualTo("- = stevethetroll_troll_noattacks"));
            Assert.That(rows[resists + 1], Is.EqualTo("- = stevethetroll_troll_noattacks"));
        }

        [Test]
        public void Resists_BrokenPartsSkipped()
        {
            var t = Live();
            t.Resists = "=Weak,Fire,Poison=Immune";
            var rows = Flat(TrollDetailsBuilder.Build(t, K, 0L));
            int at = System.Array.IndexOf(rows, "# stevethetroll_troll_resists");
            Assert.That(rows[at + 1], Is.EqualTo("Poison = Immune"));
            Assert.That(rows[at + 2], Does.StartWith("# "));
        }

        [Test]
        public void NotLoaded_ShowsLastSeen()
        {
            var t = Live();
            t.Loaded = false; t.LastSeenTicks = 0L;
            var status = TrollDetailsBuilder.Build(t, K, System.TimeSpan.FromMinutes(5).Ticks)[0].Rows[0].Value;
            Assert.That(status, Is.EqualTo("stevethetroll_status_farming (stevethetroll_troll_lastseen " + TrollDetailsBuilder.Ago(System.TimeSpan.FromMinutes(5).Ticks, K) + ")"));
        }

        [Test]
        public void NoText_NoGroups() { Assert.That(TrollDetailsBuilder.Build(Live(), null, 0L), Is.Empty); }
    }
}
