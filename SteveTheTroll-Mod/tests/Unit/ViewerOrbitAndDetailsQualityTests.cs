using System.Collections.Generic;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Tests.Unit;

/// <summary>
/// Test quality (user 2026-10-28: "can we increase this?"): the cases Stryker.NET showed untested in ViewerOrbit (reset after a pan /
/// turn along ONE axis only, pan direction with a turned + tilted camera, the pan limit, NaN on one axis, rotate speed on the tilt, zoom
/// log lines) and CostumeDetailsBuilder (every row of the F6 Costume tab, quality-1 items, empty lists, decimals).
/// </summary>
[TestFixture]
public class ViewerOrbitAndDetailsQualityTests
{
    static ViewerSettings Flat() { return new ViewerSettings { StartYaw = 0f, StartPitch = 0f }; }

    // ---- ViewerOrbit: Reset ----

    [Test]
    public void Reset_AfterAPanAlongUpOnly_IsAChange()
    {
        var o = new ViewerOrbit(Flat());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(o.Pan(0f, 10f, 100f), Is.True);
            Assert.That(o.PanX, Is.EqualTo(0f).Within(1e-6)); Assert.That(o.PanZ, Is.EqualTo(0f).Within(1e-6)); Assert.That(o.PanY, Is.Not.EqualTo(0f));
            Assert.That(o.Reset(), Is.True, "only PanY was off");
        }
    }

    [Test]
    public void Reset_AfterAPanAlongRightOnly_IsAChange()
    {
        var o = new ViewerOrbit(Flat());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(o.Pan(10f, 0f, 100f), Is.True);
            Assert.That(o.PanY, Is.EqualTo(0f).Within(1e-6)); Assert.That(o.PanX, Is.Not.EqualTo(0f));
            Assert.That(o.Reset(), Is.True, "only PanX was off");
        }
    }

    [Test]
    public void Reset_AfterOnlyATilt_IsAChange()
    {
        var o = new ViewerOrbit(Flat());
        o.Drag(0f, 10f);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(o.Yaw, Is.EqualTo(0f));
            Assert.That(o.Reset(), Is.True);
        }
    }

    [Test]
    public void Reset_AfterOnlyATurn_IsAChange()
    {
        var o = new ViewerOrbit(Flat());
        o.Drag(10f, 0f);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(o.Pitch, Is.EqualTo(0f));
            Assert.That(o.Reset(), Is.True);
        }
    }

    [Test]
    public void Reset_LogLines()
    {
        var log = new List<string>();
        var o = new ViewerOrbit(Flat(), log.Add);
        log.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(o.Reset(), Is.False);
            Assert.That(log, Is.EqualTo(new[] { "ViewerOrbit: reset to " + o + " (already there)" }));
        }
        o.StepZoom(1f); log.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(o.Reset(), Is.True);
            Assert.That(log, Is.EqualTo(new[] { "ViewerOrbit: reset to " + o }));
        }
    }

    // ---- Pan ----

    [Test]
    public void Pan_TurnedAndTilted_MovesAgainstTheCameraAxes()
    {
        var s = new ViewerSettings { StartYaw = 30f, StartPitch = 20f, PanSpeed = 1f, PanLimit = 3f };
        var o = new ViewerOrbit(s);
        ViewerMath.CameraAxes(o.Yaw, o.Pitch, out float rx, out float ry, out float rz, out float ux, out float uy, out float uz);
        Assert.That(o.Pan(10f, 20f, 100f), Is.True);
        const float k = 1f / 100f;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(o.PanX, Is.EqualTo((-rx * 10f + ux * 20f) * k).Within(1e-5));
            Assert.That(o.PanY, Is.EqualTo((-ry * 10f + uy * 20f) * k).Within(1e-5));
            Assert.That(o.PanZ, Is.EqualTo((-rz * 10f + uz * 20f) * k).Within(1e-5));
        }
    }

    [Test]
    public void Pan_AtTheLimit_NoChange_AndOneLimitLine()
    {
        var log = new List<string>();
        var o = new ViewerOrbit(Flat(), log.Add);
        Assert.That(o.Pan(-1000f, 0f, 100f), Is.True, "first pan reaches the limit");
        float x = o.PanX;
        Assert.That(x, Is.EqualTo(0.8f).Within(1e-5));
        log.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(o.Pan(-1000f, 0f, 100f), Is.False, "already at the limit");
            Assert.That(o.PanX, Is.EqualTo(x));
            Assert.That(log, Is.EqualTo(new[] { "ViewerOrbit: pan at the limit (0.8 view heights), view " + o }));
        }
    }

    [Test]
    public void Pan_NaNOnOneAxis_Ignored_WithItsLine()
    {
        var log = new List<string>();
        var o = new ViewerOrbit(Flat(), log.Add);
        log.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(o.Pan(float.NaN, 1f, 100f), Is.False);
            Assert.That(o.Pan(1f, float.NaN, 100f), Is.False);
            Assert.That(log, Is.EqualTo(new[] { "ViewerOrbit: pan with NaN ignored", "ViewerOrbit: pan with NaN ignored" }));
        }
    }

    [Test]
    public void Pan_FrameHeightOnePixel_IsUsable_ZeroIsNot()
    {
        var log = new List<string>();
        var o = new ViewerOrbit(Flat(), log.Add);
        Assert.That(o.Pan(0.1f, 0f, 1f), Is.True);
        log.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(o.Pan(1f, 1f, 0f), Is.False);
            Assert.That(log, Is.EqualTo(new[] { "ViewerOrbit: pan ignored, frame height 0 px is not usable" }));
        }
    }

    [Test]
    public void Pan_RoundingDust_IsNoChange()
    {
        var o = new ViewerOrbit(Flat());
        Assert.That(o.Pan(1e-5f, 0f, 1000f), Is.False);
    }

    // ---- Drag / zoom ----

    [Test]
    public void Drag_TiltUsesTheRotateSpeed()
    {
        var o = new ViewerOrbit(new ViewerSettings { StartYaw = 0f, StartPitch = 0f, RotateSpeed = 0.5f });
        using (Assert.EnterMultipleScope())
        {
            Assert.That(o.Drag(0f, 10f), Is.True);
            Assert.That(o.Pitch, Is.EqualTo(5f).Within(1e-5));
        }
    }

    [Test]
    public void Drag_NaNOnOneAxis_Ignored()
    {
        var log = new List<string>();
        var o = new ViewerOrbit(Flat(), log.Add);
        log.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(o.Drag(float.NaN, 1f), Is.False);
            Assert.That(o.Drag(1f, float.NaN), Is.False);
            Assert.That(log, Is.EqualTo(new[] { "ViewerOrbit: drag with NaN ignored", "ViewerOrbit: drag with NaN ignored" }));
            Assert.That(o.Yaw, Is.EqualTo(0f));
        }
    }

    [Test]
    public void StepZoom_LogLines_AndTheLimit()
    {
        var log = new List<string>();
        var o = new ViewerOrbit(new ViewerSettings { ZoomStep = 2f, ZoomMax = 2f }, log.Add);
        log.Clear();
        Assert.That(o.StepZoom(1f), Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(o.StepZoom(1f), Is.False);
            Assert.That(log, Is.EqualTo(new[]
            {
            "ViewerOrbit: zoom 1 -> 2 (1 step(s), range 0.5..2)",
            "ViewerOrbit: zoom 2 -> 2 (1 step(s), range 0.5..2) - at the limit",
        }));
        }
    }

    [Test]
    public void ToString_AllParts()
    {
        var o = new ViewerOrbit(new ViewerSettings { StartYaw = 20f, StartPitch = 12f });
        Assert.That(o.ToString(), Is.EqualTo("yaw 20 deg, pitch 12 deg, zoom x1, pan (0, 0, 0) view heights"));
    }

    // ---- CostumeDetailsBuilder: every row of the sample (what the F6 Costume tab shows) ----

    static string K(string key) { return key; }

    static List<string> Rows(List<DetailGroup> groups)
    {
        var all = new List<string>();
        foreach (var g in groups) { all.Add("# " + g); foreach (var r in g.Rows) all.Add(r.ToString()); }
        return all;
    }

    [Test]
    public void Sample_EveryRow()
    {
        var expected = new List<string> { "# stevethetroll_details_set: Steve The Troll (2 rows)", "stevethetroll_details_set_worn = 3 / 4",
            "stevethetroll_details_set_effects = +50 % movement speed, +25 % carry weight, -50 % stamina use, Up to 5 tamed trolls, -75 % stagger" };
        string[] names = { "Troll Mascot Head", "Troll Mascot Suit", "Troll Mascot Trousers" };
        string[] slots = { "helmet", "chest", "legs" };
        for (int i = 0; i < 3; i++)
        {
            expected.Add("# " + names[i] + " (5 rows)");
            expected.Add("stevethetroll_details_slot = stevethetroll_details_slot_" + slots[i]);
            expected.Add("stevethetroll_details_armor = 20 -> 35");
            expected.Add("stevethetroll_details_worn = " + (i == 0 ? "stevethetroll_details_not_worn" : "stevethetroll_details_quality 1"));
            expected.Add("stevethetroll_details_recipe = 5 Troll hide, 4 Deer hide, 6 Blueberries");
            expected.Add("stevethetroll_details_upgrade = 2 Troll hide, 2 Blueberries");
        }
        expected.AddRange(new[]
        {
            "# Troll Club (6 rows)", "stevethetroll_details_blunt = 150 -> 255", "stevethetroll_details_chop = 50 -> 110", "stevethetroll_details_pickaxe = 50 -> 110",
            "stevethetroll_details_recipe = 15 Core wood, 15 Fine wood, 10 Thistle, 2 Troll hide", "stevethetroll_details_upgrade = 5 Core wood, 5 Fine wood",
            "stevethetroll_details_station = Workbench (stevethetroll_details_level 1)",
        });
        Assert.That(Rows(CostumeDetailsBuilder.Build(CostumeData.Sample(), K)), Is.EqualTo(expected));
    }

    [Test]
    public void QualityOne_NoMaxNoUpgrade_EmptyListsShowDash_Decimals()
    {
        var d = new CostumeData { SetName = "S", ClubName = "C", ClubBlunt = 12.5f, ClubChop = 1f, ClubPickaxe = 0f, ClubMaxQuality = 1, Station = "W", StationLevel = 2 };
        var p = new CostumeData.Piece { Name = "P", SlotKey = "k", Armor = 3f, ArmorPerLevel = 9f, MaxQuality = 1, WornQuality = 1 };
        p.Recipe.Add(new SlipperData.Ingredient("X", 0, 4));
        d.Pieces.Add(p);
        d.ClubRecipe.Add(new SlipperData.Ingredient("Y", 0, 0));
        Assert.That(Rows(CostumeDetailsBuilder.Build(d, K)), Is.EqualTo(new[]
        {
            "# stevethetroll_details_set: S (2 rows)", "stevethetroll_details_set_worn = 0 / 4", "stevethetroll_details_set_effects = -",
            "# P (4 rows)", "stevethetroll_details_slot = k", "stevethetroll_details_armor = 3", "stevethetroll_details_worn = stevethetroll_details_quality 1",
            "stevethetroll_details_recipe = -",
            "# C (5 rows)", "stevethetroll_details_blunt = 12.5", "stevethetroll_details_chop = 1", "stevethetroll_details_pickaxe = 0",
            "stevethetroll_details_recipe = -", "stevethetroll_details_station = W (stevethetroll_details_level 2)",
        }));
    }
}
