using NUnit.Framework;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>
    /// D-126: the Troll work drop-down offers biome items only while the troll is in that biome; the troll's biome travels in TrollInfo.
    /// </summary>
    [TestFixture]
    public class BiomeFocusTests
    {
        [Test]
        public void NoBiome_OnlyTheTenChoices()
        {
            var l = TrollFocus.ChoicesFor("", "");
            Assert.That(l.Count, Is.EqualTo(TrollFocus.Choices.Count));
            Assert.That(l, Does.Not.Contain("Blackwood"));
        }

        [TestCase("AshLands", "Blackwood")]
        [TestCase("ashlands", "Blackwood")]
        [TestCase("Mistlands", "BlackMarble")]
        [TestCase("DeepNorth", "Frostwood")]
        [TestCase("DeepNorth", "GoldOre")]
        public void Biome_AddsItsItems(string biome, string item)
        {
            var l = TrollFocus.ChoicesFor(biome, "");
            Assert.That(l, Does.Contain(item));
            Assert.That(l[0], Is.EqualTo(TrollFocus.None));
            Assert.That(l.Count, Is.GreaterThan(TrollFocus.Choices.Count));
        }

        [Test]
        public void Meadows_NoBiomeItems() { Assert.That(TrollFocus.ChoicesFor("Meadows", "").Count, Is.EqualTo(TrollFocus.Choices.Count)); }

        [Test]
        public void DeepNorth_BothItems_Once()
        {
            var l = TrollFocus.ChoicesFor("DeepNorth", "Frostwood");
            Assert.That(l.FindAll(v => v == "Frostwood").Count, Is.EqualTo(1));
            Assert.That(l.Count, Is.EqualTo(TrollFocus.Choices.Count + 2));
        }

        [Test]
        public void LeftTheBiome_KeepsShowingItsFocus()
        {
            var l = TrollFocus.ChoicesFor("Meadows", "Blackwood");
            Assert.That(l[l.Count - 1], Is.EqualTo("Blackwood"));
            Assert.That(l.Count, Is.EqualTo(TrollFocus.Choices.Count + 1));
        }

        [Test]
        public void UnknownCurrent_NotAdded() { Assert.That(TrollFocus.ChoicesFor("Meadows", "Stone").Count, Is.EqualTo(TrollFocus.Choices.Count)); }

        [TestCase("Blackwood", "stevethetroll_focus_blackwood")]
        [TestCase("BlackMarble", "stevethetroll_focus_blackmarble")]
        [TestCase("Frostwood", "stevethetroll_focus_timberwood")]
        [TestCase("GoldOre", "stevethetroll_focus_goldore")]
        public void BiomeItems_KeptAndNamed(string v, string key)
        {
            Assert.That(TrollFocus.Normalize(v), Is.EqualTo(v));
            Assert.That(TrollFocus.KeyOf(v), Is.EqualTo(key));
        }

        [Test]
        public void BiomeItems_NotInDefaultPriority()
        {
            foreach (var g in TrollFocus.DefaultOrder)
                foreach (var b in TrollFocus.BiomeChoices) Assert.That(g.Item, Is.Not.EqualTo(b[0]));
        }

        [Test]
        public void BiomeItems_PickedUp()
        {
            foreach (var b in TrollFocus.BiomeChoices) Assert.That(TrollFocus.PickupItems, Does.Contain(b[0]));
        }

        [Test]
        public void TrollInfo_Biome_RoundTrip()
        {
            var t = new TrollInfo { Id = "1:5", Name = "Steve", Biome = "AshLands", BackpackUsed = 3, BackpackSlots = 24 };
            var back = TrollInfo.Decode(t.Encode());
            Assert.That(back, Is.Not.Null);
            Assert.That(back.Biome, Is.EqualTo("AshLands"));
            Assert.That(back.BackpackUsed, Is.EqualTo(3));
        }

        [Test]
        public void TrollInfo_OldLine18_StillRead_NoBiome()
        {
            var line = new TrollInfo { Id = "1:5", Biome = "Mistlands", BackpackUsed = 2, BackpackSlots = 24 }.Encode();
            string old = line.Substring(0, line.LastIndexOf(TrollInfo.Sep));
            var back = TrollInfo.Decode(old);
            Assert.That(back, Is.Not.Null);
            Assert.That(back.Biome, Is.EqualTo(""));
            Assert.That(back.BackpackSlots, Is.EqualTo(24));
        }
    }
}
