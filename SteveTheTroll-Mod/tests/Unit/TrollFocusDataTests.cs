using System.Linq;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Tests.Unit;

/// <summary>
/// D-086: TrollFocus tables pinned to the user's spec (D-058 priority order 2026-10-27, D-062 pickup list) - Stryker showed that a
/// changed item / prefab / text key in these tables passed every test. A wrong name here = the troll silently farms or picks up nothing.
/// </summary>
[TestFixture]
public class TrollFocusDataTests
{
    [Test]
    public void Choices_ValuesAndKeys()
    {
        string[] expected =
        {
            "=stevethetroll_focus_none", "YggdrasilWood=stevethetroll_focus_yggdrasilwood", "ElderBark=stevethetroll_focus_elderbark",
            "IronScrap=stevethetroll_focus_ironscrap", "FineWood=stevethetroll_focus_finewood", "SilverOre=stevethetroll_focus_silver",
            "CopperOre=stevethetroll_focus_copper", "RoundLog=stevethetroll_focus_corewood", "TinOre=stevethetroll_focus_tin",
            "Wood=stevethetroll_focus_wood",
        };
        Assert.That(TrollFocus.Choices.Select(c => c.Key + "=" + c.Value), Is.EqualTo(expected));
    }

    [Test]
    public void DefaultOrder_ItemsAndSources()
    {
        string[] expected =
        {
            "YggdrasilWood:", "ElderBark:", "IronScrap:mudpile", "FineWood:Birch", "SilverOre:silvervein", "CopperOre:rock4_copper",
            "RoundLog:Pinetree", "FineWood:Oak", "TinOre:MineRock_Tin", "Wood:",
        };
        Assert.That(TrollFocus.DefaultOrder.Select(g => g.Item + ":" + string.Join(",", g.Sources)), Is.EqualTo(expected));
    }

    [Test]
    public void PickupItems_EveryFarmedItem()
    {
        string[] expected =
        {
            "Wood", "FineWood", "RoundLog", "ElderBark", "YggdrasilWood", "Blackwood",
            "CopperOre", "TinOre", "IronOre", "SilverOre", "CopperScrap", "IronScrap", "BlackMetalScrap", "FlametalOre", "FlametalOreNew",
            "BlackMarble", "Frostwood", "GoldOre",
        };
        Assert.That(TrollFocus.PickupItems, Is.EqualTo(expected));
    }

    [Test]
    public void EveryChoiceItem_IsPickedUp()
    {
        foreach (var c in TrollFocus.Choices.Where(c => c.Key.Length > 0)) Assert.That(TrollFocus.PickupItems, Does.Contain(c.Key), c.Key);
    }

    [TestCase("silvervein_frac", true)]
    [TestCase("SILVERVEIN", true)]
    [TestCase("rock4_copper", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void SourceMatches_SilverGoal(string prefab, bool expected)
    {
        var silver = TrollFocus.DefaultOrder.First(g => g.Item == "SilverOre");
        Assert.That(TrollFocus.SourceMatches(silver, prefab), Is.EqualTo(expected));
    }

    [Test]
    public void SourceMatches_NoSourceList_AnyPrefab()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TrollFocus.SourceMatches(new TrollFocus.Goal("Wood"), null), Is.True);
            Assert.That(TrollFocus.SourceMatches(new TrollFocus.Goal("Wood", null), "x"), Is.True);
        }
    }

    [Test]
    public void SourceMatches_SecondSourceCounts()
    {
        Assert.That(TrollFocus.SourceMatches(new TrollFocus.Goal("FineWood", "Birch", "Oak"), "Oak1"), Is.True);
    }
}
