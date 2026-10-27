namespace SteveTheTroll.Tests.Unit
{
    /// <summary>
    /// SteveTheTroll.Item.UpgradeCost (D-035): every upgrade of the slippers costs the same per resource (2), unlike vanilla
    /// Piece.Requirement.GetAmount (1.0.20), which asked 2 / 4 / 8 at quality 2 / 3 / 4 for 2 per level (the user's TEST-010 report).
    /// </summary>
    [TestFixture]
    public class UpgradeCostTests
    {
        const string T = "SteveTheTroll.Item.UpgradeCost";

        static int Amount(int quality, int baseAmount, int perLevel, bool upgrader)
        {
            return (int)ModAssembly.Call(T, "Amount", quality, baseAmount, perLevel, upgrader);
        }

        // Vanilla 1.0.20 formula (Cecil dump of Piece/Requirement::GetAmount), only to prove the precondition of the regression.
        static int Vanilla(int quality, int baseAmount, int perLevel, bool upgrader)
        {
            if (quality <= 1) return baseAmount;
            float mult = quality < 4 ? quality - 1 : 4f + (quality - 4) / 2f;
            return (int)System.Math.Floor(mult * perLevel + (upgrader ? baseAmount : 0));
        }

        [Test]
        public void Vanilla_AsksEightAtQualityFour_Precondition()
        {
            Assert.That(Vanilla(4, 5, 2, false), Is.EqualTo(8));
            Assert.That(Vanilla(3, 5, 2, false), Is.EqualTo(4));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void EveryUpgrade_CostsPerLevel(int quality)
        {
            Assert.That(Amount(quality, 5, 2, false), Is.EqualTo(2));
        }

        [Test]
        public void Craft_CostsBaseAmount()
        {
            Assert.That(Amount(1, 5, 2, false), Is.EqualTo(5));
            Assert.That(Amount(0, 6, 2, false), Is.EqualTo(6));
        }

        [Test]
        public void ResourceWithoutUpgradeCost_StaysFree()
        {
            Assert.That(Amount(4, 4, 0, false), Is.EqualTo(0));
        }

        [Test]
        public void UpgraderResource_AddsBaseAmount_AsVanilla()
        {
            Assert.That(Amount(3, 1, 2, true), Is.EqualTo(3));
        }

        [Test]
        public void NegativePerLevel_NeverBelowZero()
        {
            Assert.That(Amount(2, 0, -3, false), Is.EqualTo(0));
        }
    }
}
