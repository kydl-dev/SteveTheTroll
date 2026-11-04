using System.Collections.Generic;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Tests.Unit;

/// <summary>Slippers tab details (D-039): armor at quality 1 and max, frost, recipe and the constant upgrade cost (D-035).</summary>
[TestFixture]
public class SlipperDetailsBuilderTests
{
    static string K(string key) { return key; }

    static DetailRow Row(List<DetailGroup> g, int group, string label)
    {
        foreach (var r in g[group].Rows) if (r.Label == label) return r;
        Assert.Fail("no row " + label);
        return null;
    }

    [Test]
    public void Sample_ArmorFiveToEight()
    {
        var g = SlipperDetailsBuilder.Build(SlipperData.Sample(), K);
        var armor = Row(g, 0, "stevethetroll_details_armor");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(armor.Value, Is.EqualTo("5"));
            Assert.That(armor.MaxValue, Is.EqualTo("8"));
        }
    }

    [Test]
    public void Sample_RecipeAndUpgrade()
    {
        var g = SlipperDetailsBuilder.Build(SlipperData.Sample(), K);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Row(g, 1, "stevethetroll_details_recipe").Value, Is.EqualTo("5 Troll hide, 4 Deer hide, 6 Blueberries"));
            Assert.That(Row(g, 1, "stevethetroll_details_upgrade").Value, Is.EqualTo("2 Troll hide, 2 Blueberries"), "deer hide is not needed for upgrades");
        }
    }

    [Test]
    public void FrostAndWorn()
    {
        var d = SlipperData.Sample();
        var g = SlipperDetailsBuilder.Build(d, K);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Row(g, 0, "stevethetroll_details_frost").Value, Is.EqualTo("stevethetroll_details_resistant"));
            Assert.That(Row(g, 0, "stevethetroll_details_worn").Value, Is.EqualTo("stevethetroll_details_quality 2"));
        }
        d.WornQuality = 0; d.FrostResistant = false;
        g = SlipperDetailsBuilder.Build(d, K);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Row(g, 0, "stevethetroll_details_worn").Value, Is.EqualTo("stevethetroll_details_not_worn"));
            Assert.That(Row(g, 0, "stevethetroll_details_frost").Value, Is.EqualTo("stevethetroll_details_none"));
        }
    }

    [Test]
    public void MaxQualityOne_NoUpgradeRow_NoMaxColumn()
    {
        var d = SlipperData.Sample();
        d.MaxQuality = 1;
        var g = SlipperDetailsBuilder.Build(d, K);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Row(g, 0, "stevethetroll_details_armor").HasMax, Is.False);
            Assert.That(g[1].Rows.Exists(r => r.Label == "stevethetroll_details_upgrade"), Is.False);
        }
    }

    [Test]
    public void NoData_NoGroups()
    {
        Assert.That(SlipperDetailsBuilder.Build(null, K), Is.Empty);
    }
}
