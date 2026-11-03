using System.Collections.Generic;
using NUnit.Framework;
using SteveTheTroll.Features.Hubs.Domain;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-135: the Troll Hub rules (limits, spacing, numbering, speed, assignment) and the building's data.</summary>
    [TestFixture]
    public class HubRulesTests
    {
        [TestCase(0, -1f, "Ok")]
        [TestCase(9, 1000f, "Ok")]
        [TestCase(9, 999.9f, "TooClose")]
        [TestCase(10, 5000f, "TooMany")]
        public void Check(int own, float nearest, string expected) { Assert.That(HubRules.Check(own, nearest).ToString(), Is.EqualTo(expected)); }

        [Test] public void NextNumber_FillsTheGap()
        {
            Assert.That(HubRules.NextNumber(new List<int>()), Is.EqualTo(1));
            Assert.That(HubRules.NextNumber(new List<int> { 1, 2 }), Is.EqualTo(3));
            Assert.That(HubRules.NextNumber(new List<int> { 1, 3 }), Is.EqualTo(2));
        }

        // D-162: walk back to the hub - starts beyond 1000 m, goes on until inside 950 m, never without a hub
        [TestCase(true, 1000f, false, false)]
        [TestCase(true, 1000.1f, false, true)]
        [TestCase(true, 980f, false, false)]
        [TestCase(true, 980f, true, true)]
        [TestCase(true, 950f, true, false)]
        [TestCase(true, 950.1f, true, true)]
        [TestCase(false, 5000f, false, false)]
        [TestCase(true, -1f, true, false)]
        public void ShouldReturn(bool hasHub, float distance, bool returning, bool expected) { Assert.That(HubRules.ShouldReturn(hasHub, distance, returning), Is.EqualTo(expected)); }

        [TestCase(true, false, 3000f, "Cancel")]        // a new follow order always ends the walk
        [TestCase(true, true, 100f, "Cancel")]
        [TestCase(false, true, 3000f, "Yield")]         // attacked (troll or player in range): combat drives, the walk stays
        [TestCase(false, false, 951f, "Walk")]
        [TestCase(false, false, 950f, "Done")]
        [TestCase(false, false, 10f, "Done")]
        [TestCase(false, false, -1f, "Walk")]
        public void ReturnNext(bool follow, bool attacked, float distance, string expected) { Assert.That(HubRules.ReturnNext(follow, attacked, distance).ToString(), Is.EqualTo(expected)); }

        [Test] public void DefaultName() { Assert.That(HubRules.DefaultName(7), Is.EqualTo("HUB 7")); }

        [TestCase(1.25f, true, true, 100f, 1.5f)]       // inside the 500 m circle
        [TestCase(1.25f, true, true, 500f, 1.5f)]
        [TestCase(1.25f, true, true, 600f, 1.25f)]      // between 500 and 1250 m: as usual (D-159)
        [TestCase(1.25f, true, true, 1250f, 1.25f)]
        [TestCase(1.25f, true, true, 1251f, 0.625f)]    // no hub within 1250 m: half speed
        [TestCase(1.25f, true, true, -1f, 0.625f)]      // no hub at all
        [TestCase(1.25f, false, true, -1f, 1.25f)]      // hubs off
        [TestCase(1.25f, true, false, -1f, 1.25f)]      // no set bonus
        public void SpeedFactor(float tamed, bool on, bool set, float nearest, float expected)
        {
            Assert.That((double)HubRules.SpeedFactor(tamed, on, set, nearest), Is.EqualTo((double)expected).Within(0.0001));
        }

        // manual, distance to the manual hub, nearest hub, distance to it, auto, expected (D-159: auto reach 1000 m)
        [TestCase(2, 400f, 5, 400f, true, 2)]         // manual and within reach
        [TestCase(2, 1500f, 5, 900f, true, 5)]        // teleported away: the nearest, it is within 1000 m
        [TestCase(2, 1500f, 5, 1200f, true, 0)]       // nothing within 1000 m: the bed
        [TestCase(2, 1500f, 5, 900f, false, 5)]       // manual stays a hub even with auto off
        [TestCase(0, -1f, 5, 1000f, true, 5)]         // exactly 1000 m is in reach
        [TestCase(0, -1f, 5, 1000.1f, true, 0)]
        [TestCase(0, -1f, 5, 300f, false, 0)]         // auto off, nothing chosen: the bed
        [TestCase(2, -1f, 0, -1f, true, 0)]
        public void Pick(int manual, float dist, int nearest, float nearestDist, bool auto, int expected)
        {
            Assert.That(HubRules.Pick(manual, dist, nearest, nearestDist, auto), Is.EqualTo(expected));
        }

        // D-159: no farming further than 950 m from the hub (a troll without a hub is not limited)
        [TestCase(true, 950f, true)]
        [TestCase(true, 950.1f, false)]
        [TestCase(true, 0f, true)]
        [TestCase(true, -1f, false)]
        [TestCase(false, 5000f, true)]
        public void FarmAllowed(bool hasHub, float spotToHub, bool expected) { Assert.That(HubRules.FarmAllowed(hasHub, spotToHub), Is.EqualTo(expected)); }

        [Test] public void Data_RecipeAndParts()
        {
            Assert.That(HubData.Parts.Length, Is.EqualTo(351));
            Assert.That(HubData.Lanterns.Length, Is.EqualTo(4));
            Assert.That(HubData.Recipe.Length, Is.EqualTo(5));
            int stone = 0; foreach (var c in HubData.Recipe) if (c.Item == "Stone") stone = c.Amount;
            Assert.That(stone, Is.EqualTo(50));
            foreach (var p in HubData.Parts)
            {
                Assert.That(System.Math.Sqrt(p.X * p.X + p.Z * p.Z), Is.LessThanOrEqualTo(HubData.Radius), p.Prefab);
                Assert.That(p.Y, Is.LessThanOrEqualTo(HubData.Height), p.Prefab);
            }
        }
    }
}
