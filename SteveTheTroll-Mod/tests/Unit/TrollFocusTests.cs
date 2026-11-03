using System.Collections.Generic;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>Troll work tab (D-039, D-058): 9 focus items + no focus in the default priority order; unknown stored values fall back to
    /// no focus; the default priority with its sources (user 2026-10-27).</summary>
    [TestFixture]
    public class TrollFocusTests
    {
        [Test]
        public void TenChoices_NoFocusFirst_InPriorityOrder()
        {
            Assert.That(TrollFocus.Choices.Count, Is.EqualTo(10));
            Assert.That(TrollFocus.Choices[0].Key, Is.EqualTo(TrollFocus.None));
            var order = new List<string>();
            for (int i = 1; i < TrollFocus.Choices.Count; i++) order.Add(TrollFocus.Choices[i].Key);
            Assert.That(order, Is.EqualTo(new[] { "YggdrasilWood", "ElderBark", "IronScrap", "FineWood", "SilverOre", "CopperOre", "RoundLog", "TinOre", "Wood" }));
        }

        [TestCase("Wood")]
        [TestCase("FineWood")]
        [TestCase("RoundLog")]
        [TestCase("CopperOre")]
        [TestCase("TinOre")]
        [TestCase("SilverOre")]
        [TestCase("IronScrap")]
        [TestCase("ElderBark")]
        [TestCase("YggdrasilWood")]
        public void KnownItems_Kept(string v) { Assert.That(TrollFocus.Normalize(v), Is.EqualTo(v)); }

        [TestCase(null)]
        [TestCase("IronOre")]
        [TestCase("wood")]
        public void Unknown_BecomesNone(string v) { Assert.That(TrollFocus.Normalize(v), Is.EqualTo(TrollFocus.None)); }

        [Test]
        public void KeyOf_UnknownGivesNoneKey() { Assert.That(TrollFocus.KeyOf("Stone"), Is.EqualTo("stevethetroll_focus_none")); }

        [Test]
        public void DefaultOrder_IsTheUsersList()
        {
            var items = new List<string>();
            foreach (var g in TrollFocus.DefaultOrder) items.Add(g.Item);
            Assert.That(items, Is.EqualTo(new[] { "YggdrasilWood", "ElderBark", "IronScrap", "FineWood", "SilverOre", "CopperOre", "RoundLog", "FineWood", "TinOre", "Wood" }));
        }

        [TestCase(3, "mudpile_beacon", true)]
        [TestCase(3, "mudpile_frac", true)]
        [TestCase(3, "Birch1", false)]
        [TestCase(4, "Birch2_aut", true)]
        [TestCase(4, "Birch_log_half", true)]
        [TestCase(4, "Oak1", false)]
        [TestCase(5, "silvervein_frac", true)]
        [TestCase(6, "rock4_copper_frac", true)]
        [TestCase(6, "MineRock_Tin", false)]
        [TestCase(7, "PineTree_log", true)]
        [TestCase(8, "Oak_log_half", true)]
        [TestCase(9, "MineRock_Tin", true)]
        [TestCase(10, "Beech1", true)]
        [TestCase(1, "YggaShoot3", true)]
        public void Sources(int step, string prefab, bool matches)
        {
            Assert.That(TrollFocus.SourceMatches(TrollFocus.DefaultOrder[step - 1], prefab), Is.EqualTo(matches));
        }

        [Test]
        public void EveryFarmedItem_IsPickedUp()
        {
            var pick = new List<string>(TrollFocus.PickupItems);
            foreach (var g in TrollFocus.DefaultOrder) Assert.That(pick, Does.Contain(g.Item));
            Assert.That(pick, Does.Contain("BlackMetalScrap"));
            Assert.That(pick, Does.Not.Contain("Stone"), "only wood, ores and scrap");
        }

        [Test]
        public void SearchOrder_FocusFirstThenDefault()
        {
            var order = TrollFocus.SearchOrder("TinOre");
            Assert.That(order.Count, Is.EqualTo(11));
            Assert.That(order[0].Item, Is.EqualTo("TinOre"));
            Assert.That(order[0].Sources.Length, Is.EqualTo(0), "the focus item comes from any source");
            Assert.That(TrollFocus.SearchOrder("").Count, Is.EqualTo(10));
            Assert.That(TrollFocus.SearchOrder("Bogus").Count, Is.EqualTo(10));
        }

        [Test]
        public void Shown_EnglishName()
        {
            System.Func<string, string> text = k => k == "stevethetroll_focus_corewood" ? "Core wood" : k;
            Assert.That(TrollFocus.Shown("RoundLog", text), Is.EqualTo("Core wood"));
            Assert.That(TrollFocus.Shown("", text), Is.EqualTo("-"));
            Assert.That(TrollFocus.Shown("Stone", text), Is.EqualTo("Stone"));
        }
    }
}
