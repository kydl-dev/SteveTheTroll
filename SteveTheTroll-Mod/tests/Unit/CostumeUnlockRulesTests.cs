using SteveTheTroll.Features.Costume.Domain;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>T-201: the costume recipes appear only after the slippers were equipped once (forced path: slippers -> equip -> costume).</summary>
    [TestFixture]
    public class CostumeUnlockRulesTests
    {
        [TestCase(true, true, false, ExpectedResult = false, TestName = "Never worn: hidden even when the game and the materials say yes")]
        [TestCase(false, true, false, ExpectedResult = false, TestName = "Never worn: a known material does not show it")]
        [TestCase(true, false, true, ExpectedResult = true, TestName = "Worn: the game's own yes shows it")]
        [TestCase(false, true, true, ExpectedResult = true, TestName = "Worn: one known material shows it (D-076)")]
        [TestCase(false, false, true, ExpectedResult = false, TestName = "Worn but no material known: still hidden")]
        public bool Discoverable(bool gameSays, bool anyMaterial, bool worn) { return CostumeUnlockRules.Discoverable(gameSays, anyMaterial, worn); }

        [Test] public void Discoverable_NeverWithoutTheSlippers_AllInputs()
        {
            foreach (bool g in new[] { false, true })
                foreach (bool a in new[] { false, true })
                    Assert.That(CostumeUnlockRules.Discoverable(g, a, false), Is.False, "game " + g + ", material " + a);
        }

        [TestCase(true, true, true, false, ExpectedResult = true, TestName = "First slipper equip of the local player marks")]
        [TestCase(true, true, true, true, ExpectedResult = false, TestName = "Already marked: no second mark")]
        [TestCase(false, true, true, false, ExpectedResult = false, TestName = "Another player equipping: no mark on us")]
        [TestCase(true, false, true, false, ExpectedResult = false, TestName = "Another item: no mark")]
        [TestCase(true, true, false, false, ExpectedResult = false, TestName = "Equip refused: no mark")]
        public bool ShouldMark(bool local, bool slippers, bool equipped, bool marked) { return CostumeUnlockRules.ShouldMark(local, slippers, equipped, marked); }

        [Test] public void WornKey_IsStable() { Assert.That(CostumeUnlockRules.WornKey, Is.EqualTo("stt_slippers_worn"), "saved in characters: never rename"); }
    }
}
