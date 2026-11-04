using System;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Tests.Unit;

/// <summary>D-043: tamed troll records in the player's profile (Encode / Decode) and the Trolls tab details.</summary>
[TestFixture]
public class TrollInfoTests
{
    static string K(string key) { return key; }

    static TrollInfo Sample()
    {
        return new TrollInfo { Id = "a1b2c3", Name = "Steve", Number = 2, Status = "stevethetroll_status_staying", Focus = "CopperOre", Farming = "",
            Health = 1234.5f, MaxHealth = 1800f, Stars = 1, DamageFactor = 3f, X = 412.4f, Y = 77f, Z = -268.2f, BedDistance = 35.5f, LastSeenTicks = 638000000000000000L };
    }

    [Test]
    public void EncodeDecode_RoundTrip()
    {
        var t = TrollInfo.Decode(Sample().Encode());
        Assert.That(t, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(t.Id, Is.EqualTo("a1b2c3"));
            Assert.That(t.Name, Is.EqualTo("Steve"));
            Assert.That(t.Focus, Is.EqualTo("CopperOre"));
            Assert.That(t.Health, Is.EqualTo(1234.5f).Within(0.01));
            Assert.That(t.Stars, Is.EqualTo(1));
            Assert.That(t.Z, Is.EqualTo(-268.2f).Within(0.01));
            Assert.That(t.BedDistance, Is.EqualTo(35.5f).Within(0.01));
            Assert.That(t.LastSeenTicks, Is.EqualTo(638000000000000000L));
        }
    }

    [Test]
    public void AttacksAndResists_RoundTrip_OldLinesStillRead()
    {
        var s = Sample();
        s.Attacks = "Punch~Blunt=180,Chop=300~4;Log swing~Blunt=210~6";
        s.Resists = "Blunt=Normal,Pierce=Resistant";
        var t = TrollInfo.Decode(s.Encode());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(t.Attacks, Is.EqualTo(s.Attacks));
            Assert.That(t.Resists, Is.EqualTo(s.Resists));
        }
        string old = string.Join("|", s.Encode().Split('|'), 0, 14);
        var o = TrollInfo.Decode(old);
        Assert.That(o, Is.Not.Null, "14-field lines from before D-044");
        Assert.That(o.Attacks, Is.EqualTo(""));
    }

    [Test]
    public void Details_AttackRows()
    {
        var rows = TrollDetailsBuilder.Attacks("Punch~Blunt=180,Chop=300,Pickaxe=120~4;broken;Throw~Blunt=150~0", K);
        Assert.That(rows.Count, Is.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows[0].Key, Is.EqualTo("Punch"));
            Assert.That(rows[0].Value, Is.EqualTo("180 Blunt, 300 Chop, 120 Pickaxe (stevethetroll_troll_every 4 s)"));
            Assert.That(rows[1].Value, Is.EqualTo("150 Blunt"));
        }
    }

    [Test]
    public void NameWithSeparator_StillDecodes()
    {
        var s = Sample();
        s.Name = "Big|Steve\nthe 2nd";
        var t = TrollInfo.Decode(s.Encode());
        Assert.That(t, Is.Not.Null);
        Assert.That(t.Name, Is.EqualTo("Big/Steve the 2nd"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("only|three|fields")]
    [TestCase("|n|s|f|g|1|2|3|4|5|6|7|8|9")]
    [TestCase("id|n|s|f|g|x|2|3|4|5|6|7|8|9")]
    public void BadLines_Null(string line) { Assert.That(TrollInfo.Decode(line), Is.Null); }

    [Test]
    public void Shown_DefaultName()
    {
        var t = Sample();
        t.Name = "";
        using (Assert.EnterMultipleScope())
        {
            Assert.That(t.Shown(K), Is.EqualTo("stevethetroll_troll_default 2"));
            Assert.That(Sample().Shown(K), Is.EqualTo("Steve"));
        }
    }

    [Test]
    public void Details_StatusStarsBed()
    {
        var t = Sample();
        long now = t.LastSeenTicks + TimeSpan.TicksPerMinute * 12;
        var g = TrollDetailsBuilder.Build(t, K, now);
        Assert.That(g.Count, Is.EqualTo(4), "general, damage, resistances, position");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(g[0].Rows[0].Value, Is.EqualTo("stevethetroll_status_staying (stevethetroll_troll_lastseen 12 min)"));
            Assert.That(g[0].Rows[1].Value, Is.EqualTo("*"));
            Assert.That(g[0].Rows[2].Value, Is.EqualTo("1235 / 1800").Or.EqualTo("1234 / 1800"));
            Assert.That(g[3].Rows[0].Value, Is.EqualTo("412 / 77 / -268"));
            Assert.That(g[3].Rows[1].Value, Is.EqualTo("36 m").Or.EqualTo("35 m"));
        }
        t.Loaded = true; t.BedDistance = -1f; t.Stars = 0;
        g = TrollDetailsBuilder.Build(t, K, now);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(g[0].Rows[0].Value, Is.EqualTo("stevethetroll_status_staying"));
            Assert.That(g[0].Rows[1].Value, Is.EqualTo("-"));
            Assert.That(g[3].Rows[1].Value, Is.EqualTo("stevethetroll_troll_nobed"));
        }
    }

    [Test]
    public void Destination_RoundTrip_And_OldLineWithoutIt_ReadsAsBed()
    {
        var t = Sample(); t.Dest = "Hub 2";
        var back = TrollInfo.Decode(t.Encode());
        Assert.That(back.Dest, Is.EqualTo("Hub 2"));
        string old = t.Encode(); old = old.Substring(0, old.LastIndexOf(TrollInfo.Sep));       // the 19-field line of before D-162
        var older = TrollInfo.Decode(old);
        Assert.That(older, Is.Not.Null);
        Assert.That(older.Dest, Is.EqualTo(""));
    }

    [Test]
    public void Position_Row_SaysHub_WhenTheTrollHasOne_AndBed_Otherwise()
    {
        var t = Sample(); t.Loaded = true; t.Dest = "Hub 2";
        var g = TrollDetailsBuilder.Build(t, k => k == "stevethetroll_troll_hubdist" ? "Distance from {0}" : k, DateTime.UtcNow.Ticks);
        Assert.That(g[3].Rows[1].Label, Is.EqualTo("Distance from Hub 2"));
        t.Dest = "";
        g = TrollDetailsBuilder.Build(t, K, DateTime.UtcNow.Ticks);
        Assert.That(g[3].Rows[1].Label, Is.EqualTo("stevethetroll_troll_bed"));
    }

    [TestCase(30L, "30 s")]
    [TestCase(600L, "10 min")]
    [TestCase(5400L, "1.5 h")]
    [TestCase(-5L, "0 s")]
    public void Ago(long seconds, string expected) { Assert.That(TrollDetailsBuilder.Ago(seconds * TimeSpan.TicksPerSecond, K), Is.EqualTo(expected)); }
}
