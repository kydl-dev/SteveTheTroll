using System.Collections.Generic;
using System.Globalization;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Tests.Unit;

/// <summary>
/// Test quality (user 2026-10-28: "can we increase this?" - mutation score 78 %): the gaps Stryker.NET listed in ViewerSettings (range
/// fixes, their log lines, the StartPitch limit at its exact edges, the summary) and CostumeData (the Unity preview sample must show the
/// game's real numbers and texts; ToString of the data used in the logs).
/// </summary>
[TestFixture]
public class PreviewAndSettingsQualityTests
{
    static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

    // ---- ViewerSettings ----

    [Test]
    public void Defaults_ToString_NamesEveryValue()
    {
        var s = new ViewerSettings();
        Assert.That(s.ToString(), Is.EqualTo(
            "rotate 0.4 deg/px, zoom step x1.2, zoom 0.5..8, pitch limit 80 deg, frame 420 px, margin 0.08, start yaw 20 / pitch 12 deg, pan speed x1, "
            + "pan limit 0.8 view heights, light key 0.7 / fill 0.25 / ambient 0.22"));
    }

    // every value out of range at once: one exact log line per value (name, bad value, range, value used), then the summary
    [Test]
    public void Clamp_EveryValueOutOfRange_OneExactLinePerValue()
    {
        var s = new ViewerSettings
        {
            RotateSpeed = 9f, ZoomStep = 9f, ZoomMax = 99f, ZoomMin = 9f, PitchLimit = 99f, FrameHeight = 9999f, Margin = 9f, StartYaw = 999f,
            StartPitch = 99f, PanSpeed = 9f, PanLimit = 9f, KeyLight = 9f, FillLight = 9f, Ambient = 9f,
        };
        var log = new List<string>();
        int changed = s.Clamp(log.Add);
        var expected = new[]
        {
            Line("RotateSpeed", 9f, ViewerSettings.RotateSpeedMin, ViewerSettings.RotateSpeedMax),
            Line("ZoomStep", 9f, ViewerSettings.ZoomStepMin, ViewerSettings.ZoomStepMax),
            Line("ZoomMax", 99f, ViewerSettings.ZoomMaxMin, ViewerSettings.ZoomMaxMax),
            Line("ZoomMin", 9f, ViewerSettings.ZoomMinMin, ViewerSettings.ZoomMinMax),
            Line("PitchLimit", 99f, ViewerSettings.PitchLimitMin, ViewerSettings.PitchLimitMax),
            Line("FrameHeight", 9999f, ViewerSettings.FrameHeightMin, ViewerSettings.FrameHeightMax),
            Line("Margin", 9f, ViewerSettings.MarginMin, ViewerSettings.MarginMax),
            Line("StartYaw", 999f, ViewerSettings.StartYawMin, ViewerSettings.StartYawMax),
            Line("StartPitch", 99f, ViewerSettings.StartPitchMin, ViewerSettings.StartPitchMax),
            Line("PanSpeed", 9f, ViewerSettings.PanSpeedMin, ViewerSettings.PanSpeedMax),
            Line("PanLimit", 9f, ViewerSettings.PanLimitMin, ViewerSettings.PanLimitMax),
            Line("KeyLight", 9f, ViewerSettings.KeyLightMin, ViewerSettings.KeyLightMax),
            Line("FillLight", 9f, ViewerSettings.FillLightMin, ViewerSettings.FillLightMax),
            Line("Ambient", 9f, ViewerSettings.AmbientMin, ViewerSettings.AmbientMax),
        };
        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.EqualTo(14), "StartPitch 89 stays inside PitchLimit 89: no 15th fix");
            Assert.That(log.GetRange(0, 14), Is.EqualTo(expected));
            Assert.That(log[14], Is.EqualTo("ViewerSettings: " + s + " (14 value(s) put into range)"));
            Assert.That(log.Count, Is.EqualTo(15));
        }
    }

    static string Line(string name, float v, float min, float max)
    {
        return "ViewerSettings: " + name + " " + F(v) + " is outside " + F(min) + ".." + F(max) + ", using " + F(max);
    }

    [Test]
    public void Clamp_InRange_NoFixLine_OnlyTheSummary()
    {
        var log = new List<string>();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(new ViewerSettings().Clamp(log.Add), Is.EqualTo(0));
            Assert.That(log, Is.EqualTo(new[] { "ViewerSettings: " + new ViewerSettings() + " (0 value(s) put into range)" }));
        }
    }

    [Test]
    public void StartPitch_ExactlyAtTheLimit_Kept()
    {
        var up = new ViewerSettings { PitchLimit = 30f, StartPitch = 30f };
        var down = new ViewerSettings { PitchLimit = 30f, StartPitch = -30f };
        using (Assert.EnterMultipleScope())
        {
            Assert.That(up.Clamp(), Is.EqualTo(0));
            Assert.That(down.Clamp(), Is.EqualTo(0));
            Assert.That(up.StartPitch, Is.EqualTo(30f));
            Assert.That(down.StartPitch, Is.EqualTo(-30f));
        }
    }

    [Test]
    public void StartPitch_BelowTheLimit_PutOnTheLowerLimit_WithItsLine()
    {
        var s = new ViewerSettings { PitchLimit = 30f, StartPitch = -45f };
        var log = new List<string>();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(s.Clamp(log.Add), Is.EqualTo(1));
            Assert.That(s.StartPitch, Is.EqualTo(-30f));
            Assert.That(log[0], Is.EqualTo("ViewerSettings: StartPitch -45 is outside PitchLimit 30, using -30"));
        }
    }

    // ---- CostumeData (the Unity preview sample = the game's items) ----

    const string Items = "SteveTheTroll.Features.Costume.Infrastructure.CostumeItems";

    static float Const(string name) { return System.Convert.ToSingle(ModAssembly.GetStatic(Items, name)); }

    [Test]
    public void Sample_ClubNumbers_AreTheGames()
    {
        var d = CostumeData.Sample();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(new[] { d.ClubBlunt, d.ClubChop, d.ClubPickaxe, d.ClubBluntPerLevel, d.ClubChopPerLevel, d.ClubPickaxePerLevel },
                    Is.EqualTo(new[] { Const("ClubBlunt"), Const("ClubChop"), Const("ClubPickaxe"), Const("ClubBluntPerLevel"), Const("ClubChopPerLevel"), Const("ClubPickaxePerLevel") }));
            Assert.That(d.ClubMaxQuality, Is.EqualTo((int)ModAssembly.GetStatic(Items, "MaxQuality")));
        }
    }

    [Test]
    public void Sample_SetEffects_AreTheGames()
    {
        var d = CostumeData.Sample();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(d.SetEffects, Is.EqualTo(new[]
                {
            "+" + F(Const("SetSpeed") * 100f) + " % movement speed",
            "+" + F(Const("SetCarryPercent") * 100f) + " % carry weight",
            F(Const("SetStamina") * 100f) + " % stamina use",
            "Up to " + (int)ModAssembly.GetStatic(Items, "SetTrollLimit") + " tamed trolls",
            "-" + F(Const("SetStaggerResistance") * 100f) + " % stagger",
        }));
            Assert.That(d.SetSize, Is.EqualTo((int)ModAssembly.GetStatic(Items, "SetSize")));
        }
    }

    // the mod's English texts (UiText is compiled into the mod, not linked here)
    static string En(string key) { return (string)((System.Collections.IDictionary)ModAssembly.GetStatic("SteveTheTroll.Ui.UiText", "En"))[key]; }

    [Test]
    public void Sample_NamesAndTexts_AreTheEnglishTexts()
    {
        var d = CostumeData.Sample();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(d.SetName, Is.EqualTo(En("stevethetroll_se_set")));
            Assert.That(d.ClubName, Is.EqualTo(En("stevethetroll_club")));
            Assert.That(d.SetDescription, Is.Not.Empty);
            Assert.That(d.ClubDescription, Is.Not.Empty);
            Assert.That(d.Station, Is.EqualTo("Workbench"));
            Assert.That(d.Pieces.ConvertAll(p => p.Name), Is.EqualTo(new[] { En("stevethetroll_costume_helmet"), En("stevethetroll_costume_chest"), En("stevethetroll_costume_legs") }));
            Assert.That(d.Pieces.ConvertAll(p => p.SlotKey), Is.EqualTo(new[] { "stevethetroll_details_slot_helmet", "stevethetroll_details_slot_chest", "stevethetroll_details_slot_legs" }));
            Assert.That(d.Pieces.ConvertAll(p => p.WornQuality), Is.EqualTo(new[] { 0, 1, 1 }), "the sample head is not worn, suit and trousers are");
        }
    }

    [Test]
    public void Sample_Recipes_AreTheGames()
    {
        var d = CostumeData.Sample();
        foreach (var p in d.Pieces)
            Assert.That(p.Recipe.ConvertAll(i => i.Name + " " + i.Amount + "+" + i.PerUpgrade), Is.EqualTo(new[] { "Troll hide 5+2", "Deer hide 4+0", "Blueberries 6+2" }), p.Name);
        Assert.That(d.ClubRecipe.ConvertAll(i => i.Name + " " + i.Amount + "+" + i.PerUpgrade), Is.EqualTo(new[] { "Core wood 15+5", "Fine wood 15+5", "Thistle 10+0", "Troll hide 2+0" }));
    }

    [Test]
    public void New_IsEmpty()
    {
        var d = new CostumeData();
        var p = new CostumeData.Piece();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(new[] { d.SetName, d.SetDescription, d.ClubName, d.ClubDescription, d.Station, p.Name, p.SlotKey }, Is.All.Empty);
            Assert.That(new[] { d.SetSize, d.ClubMaxQuality, d.StationLevel, p.MaxQuality }, Is.EqualTo(new[] { 4, 1, 1, 1 }));
        }
    }

    [Test]
    public void ToString_SampleAndPiece()
    {
        var d = CostumeData.Sample();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(d.ToString(), Is.EqualTo("costume 3 piece(s), set 'Steve The Troll' 3/4 worn, 5 effect(s), club 'Troll Club' 150/50/50"));
            Assert.That(d.Pieces[1].ToString(), Is.EqualTo("Troll Mascot Suit (armor 20 +5/q, max q4, worn q1, recipe 3)"));
        }
    }
}
