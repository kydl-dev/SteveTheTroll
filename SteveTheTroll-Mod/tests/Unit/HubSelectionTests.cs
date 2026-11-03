using NUnit.Framework;
using SteveTheTroll.Features.Hubs.Domain;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-160: several hubs, several owners - which hub a troll uses and whether it may farm / slows down (the three distance rules of D-159).</summary>
    [TestFixture]
    public class HubSelectionTests
    {
        const long Me = 1, Other = 2;

        static int Nearest(long owner, long[] owners, float[] dists, out float d) { return HubRules.NearestOwned(owner, owners, dists, out d); }

        [Test] public void NoHubs_NoneAndNoDistance() { float d; Assert.That(Nearest(Me, new long[0], new float[0], out d), Is.EqualTo(-1)); Assert.That(d, Is.EqualTo(-1f)); }

        [Test] public void TwoHubs_TheNearerOneWins()
        {
            float d; int i = Nearest(Me, new[] { Me, Me }, new[] { 1100f, 800f }, out d);
            Assert.That(i, Is.EqualTo(1)); Assert.That(d, Is.EqualTo(800f));
        }

        [Test] public void AnotherPlayersHub_IsIgnored()
        {
            float d; int i = Nearest(Me, new[] { Other, Me }, new[] { 100f, 900f }, out d);
            Assert.That(i, Is.EqualTo(1)); Assert.That(d, Is.EqualTo(900f));
        }

        [Test] public void OnlyAnotherPlayersHub_MeansNone() { float d; Assert.That(Nearest(Me, new[] { Other }, new[] { 10f }, out d), Is.EqualTo(-1)); }

        [Test] public void NegativeDistance_IsIgnored() { float d; Assert.That(Nearest(Me, new[] { Me, Me }, new[] { -1f, 700f }, out d), Is.EqualTo(1)); }

        // the full chain for a full troll: the nearest own hub, then the 1000 m reach decides between hub and bed (D-159)
        [TestCase(999f, true)]
        [TestCase(1000f, true)]
        [TestCase(1001f, false)]
        public void FullTroll_GoesToTheNearestHubOnlyWithin1000(float nearestDist, bool toHub)
        {
            float d; int i = Nearest(Me, new[] { Me, Me }, new[] { nearestDist, nearestDist + 400f }, out d);
            int pick = HubRules.Pick(0, -1f, i + 1, d, true);
            Assert.That(pick > 0, Is.EqualTo(toHub));
            if (toHub) Assert.That(pick, Is.EqualTo(1));
        }

        [Test] public void TwoHubs_FarOneIsNotChosenOverTheNearOne()
        {
            float d; int i = Nearest(Me, new[] { Me, Me }, new[] { 1100f, 800f }, out d);
            Assert.That(HubRules.Pick(0, -1f, i + 1, d, true), Is.EqualTo(2));       // hub number 2 is 800 m away
        }

        // the manual hub is out of reach after an F6 teleport: the nearest hub within 1000 m takes over, else the bed
        [TestCase(1500f, 600f, 3)]
        [TestCase(1500f, 1400f, 0)]
        public void TeleportedAwayFromTheManualHub(float manualDist, float nearestDist, int expected) { Assert.That(HubRules.Pick(2, manualDist, 3, nearestDist, true), Is.EqualTo(expected)); }

        // slowdown is measured to the nearest hub (-1 = none): 1250 m is fine, 1251 m is half speed, no hub at all is half speed
        [TestCase(1249f, 1.25f)]
        [TestCase(1250f, 1.25f)]
        [TestCase(1251f, 0.625f)]
        [TestCase(-1f, 0.625f)]
        public void Slowdown(float nearest, float expected) { Assert.That(HubRules.SpeedFactor(1.25f, true, true, nearest), Is.EqualTo(expected).Within(1e-4f)); }

        // the 950 m limit is measured hub -> resource (not troll -> resource): a troll that wandered 400 m away may only take resources that are within 950 m of its HUB
        [TestCase(949f, true)]
        [TestCase(950f, true)]
        [TestCase(951f, false)]
        public void FarmLimit_IsFromTheHub(float resourceToHub, bool allowed) { Assert.That(HubRules.FarmAllowed(true, resourceToHub), Is.EqualTo(allowed)); }

        [Test] public void FarmLimit_TrollWithoutAHubIsNotLimited() { Assert.That(HubRules.FarmAllowed(false, 99999f), Is.True); }
    }
}
