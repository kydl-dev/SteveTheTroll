using NUnit.Framework;
using SteveTheTroll.Trolls.Farming.Domain;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-174: on the way back to its remembered farm spot a troll does not take sources near the way (it must arrive); a long-range search walk still does.</summary>
    [TestFixture]
    public class SpotTravelTests
    {
        [TestCase(true, 160f, false)] [TestCase(true, 31f, false)] [TestCase(true, 30f, true)] [TestCase(true, 5f, true)] [TestCase(false, 160f, true)] [TestCase(false, 5f, true)]
        public void NearSearchWhileTravelling(bool toSpot, float metres, bool expected) { Assert.That(TargetRules.NearSearchWhileTravelling(toSpot, metres), Is.EqualTo(expected)); }
    }
}
