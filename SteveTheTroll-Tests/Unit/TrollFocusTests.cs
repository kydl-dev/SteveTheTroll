using SteveTheTroll.Ui;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>Troll work tab (D-039): the five focus items + no focus; unknown stored values fall back to no focus.</summary>
    [TestFixture]
    public class TrollFocusTests
    {
        [Test]
        public void SixChoices_NoFocusFirst()
        {
            Assert.That(TrollFocus.Choices.Count, Is.EqualTo(6));
            Assert.That(TrollFocus.Choices[0].Key, Is.EqualTo(TrollFocus.None));
        }

        [TestCase("Wood")]
        [TestCase("FineWood")]
        [TestCase("RoundLog")]
        [TestCase("CopperOre")]
        [TestCase("TinOre")]
        public void KnownItems_Kept(string v) { Assert.That(TrollFocus.Normalize(v), Is.EqualTo(v)); }

        [TestCase(null)]
        [TestCase("IronOre")]
        [TestCase("wood")]
        public void Unknown_BecomesNone(string v) { Assert.That(TrollFocus.Normalize(v), Is.EqualTo(TrollFocus.None)); }

        [Test]
        public void KeyOf_UnknownGivesNoneKey() { Assert.That(TrollFocus.KeyOf("Stone"), Is.EqualTo("stevethetroll_focus_none")); }
    }
}
