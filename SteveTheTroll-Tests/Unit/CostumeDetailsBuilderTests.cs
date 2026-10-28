using System.Collections.Generic;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>F6 Costume tab details (D-080): set block, one block per piece (armor 20 -> 35), club damage 150/50/50 -> 255/110/110, recipes.</summary>
    [TestFixture]
    public class CostumeDetailsBuilderTests
    {
        static string K(string key) { return key; }

        static DetailRow Row(List<DetailGroup> g, int group, string label)
        {
            foreach (var r in g[group].Rows) if (r.Label == label) return r;
            Assert.Fail("no row " + label + " in group " + group);
            return null;
        }

        [Test]
        public void Sample_SetThreePiecesClub()
        {
            var g = CostumeDetailsBuilder.Build(CostumeData.Sample(), K);
            Assert.That(g.Count, Is.EqualTo(5), "set + 3 pieces + club");
            Assert.That(g[0].Title, Is.EqualTo("stevethetroll_details_set: Steve The Troll"));
            Assert.That(Row(g, 0, "stevethetroll_details_set_worn").Value, Is.EqualTo("3 / 4"));
            Assert.That(Row(g, 0, "stevethetroll_details_set_effects").Value, Does.Contain("-50 % stamina use"));
        }

        [Test]
        public void Pieces_ArmorTwentyToThirtyFive()
        {
            var g = CostumeDetailsBuilder.Build(CostumeData.Sample(), K);
            for (int i = 1; i <= 3; i++)
            {
                var armor = Row(g, i, "stevethetroll_details_armor");
                Assert.That(armor.Value, Is.EqualTo("20"));
                Assert.That(armor.MaxValue, Is.EqualTo("35"));
                Assert.That(Row(g, i, "stevethetroll_details_upgrade").Value, Is.EqualTo("2 Troll hide, 2 Blueberries"));
            }
            Assert.That(Row(g, 1, "stevethetroll_details_worn").Value, Is.EqualTo("stevethetroll_details_not_worn"), "the head is not worn in the sample");
        }

        [Test]
        public void Club_DamageAtQualityFour()
        {
            var g = CostumeDetailsBuilder.Build(CostumeData.Sample(), K);
            Assert.That(Row(g, 4, "stevethetroll_details_blunt").MaxValue, Is.EqualTo("255"));
            Assert.That(Row(g, 4, "stevethetroll_details_chop").MaxValue, Is.EqualTo("110"));
            Assert.That(Row(g, 4, "stevethetroll_details_pickaxe").MaxValue, Is.EqualTo("110"));
            Assert.That(Row(g, 4, "stevethetroll_details_upgrade").Value, Is.EqualTo("5 Core wood, 5 Fine wood"));
        }

        [Test]
        public void FullSet_SaysActive()
        {
            var d = CostumeData.Sample();
            d.SetWorn = 4;
            Assert.That(Row(CostumeDetailsBuilder.Build(d, K), 0, "stevethetroll_details_set_worn").Value, Is.EqualTo("4 / 4 - stevethetroll_details_set_active"));
        }

        [Test]
        public void NoData_NoGroups()
        {
            Assert.That(CostumeDetailsBuilder.Build(null, K).Count, Is.EqualTo(0));
        }
    }
}
