using NUnit.Framework;
using SteveTheTroll.Trolls.Taming.Domain;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-160: what the read-only runtime health monitor calls a problem.</summary>
    [TestFixture]
    public class HealthRulesTests
    {
        [TestCase(true, false, false, 91f, true)]       // full for over 90 s, not unloading: problem
        [TestCase(true, false, false, 90f, false)]      // still within the grace time
        [TestCase(true, true, false, 500f, false)]      // unloading
        [TestCase(true, false, true, 500f, false)]      // fighting
        [TestCase(false, false, false, 500f, false)]    // not full
        public void FullNotUnloading(bool full, bool unloading, bool fighting, float seconds, bool expected) { Assert.That(HealthRules.FullNotUnloading(full, unloading, fighting, seconds), Is.EqualTo(expected)); }

        [TestCase(true, false, true, 981f, true)]       // 950 + 30 slack = 980
        [TestCase(true, false, true, 980f, false)]
        [TestCase(true, true, true, 3000f, false)]      // following: allowed
        [TestCase(false, false, true, 3000f, false)]    // not farming
        [TestCase(true, false, false, 3000f, false)]    // no hub: not limited
        public void FarmingBeyondLeash(bool farming, bool following, bool hasHub, float dist, bool expected) { Assert.That(HealthRules.FarmingBeyondLeash(farming, following, hasHub, dist, 950f), Is.EqualTo(expected)); }

        [TestCase(3, false, true)]
        [TestCase(3, true, false)]
        [TestCase(0, false, false)]
        public void StaleAssignment(int assigned, bool exists, bool expected) { Assert.That(HealthRules.StaleAssignment(assigned, exists), Is.EqualTo(expected)); }

        [TestCase(120.1f, true)]
        [TestCase(120f, false)]
        [TestCase(0f, false)]
        public void DepotStuck(float s, bool expected) { Assert.That(HealthRules.DepotStuck(s), Is.EqualTo(expected)); }
    }
}
