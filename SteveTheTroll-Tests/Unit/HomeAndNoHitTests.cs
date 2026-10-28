using System.Collections.Generic;
using UnityEngine;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-097: home spots on a ring 15 m around the bed (never on top of it); no-hit circle around the bed.</summary>
    [TestFixture]
    public class HomeAndNoHitTests
    {
        const string Spot = "SteveTheTroll.Taming.TrollHomeSpot", NoHit = "SteveTheTroll.Taming.TrollNoHit";

        static Vector3 Candidate(int n, float r) { return (Vector3)ModAssembly.Call(Spot, "Candidate", n, r); }

        [Test]
        public void FirstRing_AtTheRadius_AllDifferent()
        {
            var seen = new HashSet<string>();
            for (int n = 0; n < 24; n++)
            {
                var c = Candidate(n, 15f);
                Assert.That(new Vector2(c.x, c.z).magnitude, Is.EqualTo(15f).Within(1e-3), "spot " + n);
                Assert.That(c.y, Is.EqualTo(0f));
                Assert.That(seen.Add(c.x.ToString("F2") + "/" + c.z.ToString("F2")), Is.True, "two trolls on one spot");
            }
        }

        [Test]
        public void LaterRings_FurtherOut_NeverInside()
        {
            for (int n = 0; n < 24 * 6; n++) Assert.That(new Vector2(Candidate(n, 15f).x, Candidate(n, 15f).z).magnitude, Is.GreaterThanOrEqualTo(15f - 1e-3));
            Assert.That(new Vector2(Candidate(24, 15f).x, Candidate(24, 15f).z).magnitude, Is.EqualTo(18f).Within(1e-3));
            Assert.That(Candidate(-5, 15f), Is.EqualTo(Candidate(0, 15f)), "negative index = first spot");
        }

        // TEST-043: depots only grow inside the 15 .. 50 m ring and keep 8 m between stacks
        [TestCase(7f, false)]
        [TestCase(15f, true)]
        [TestCase(50f, true)]
        [TestCase(50.5f, false)]
        [TestCase(-1f, false)]
        public void DepotRing(float toBed, bool expected) { Assert.That((bool)ModAssembly.Call("SteveTheTroll.Backpack.DepotRules", "InRing", toBed, 15f, 50f), Is.EqualTo(expected)); }

        [TestCase(11.9f, false)]
        [TestCase(12f, true)]
        public void DepotSpacing(float d, bool expected) { Assert.That((bool)ModAssembly.Call("SteveTheTroll.Backpack.DepotRules", "FarEnough", d, 12f), Is.EqualTo(expected)); }

        // D-107: another troll within 10 m makes a depot busy
        [TestCase(10f, true)]
        [TestCase(3f, true)]
        [TestCase(10.1f, false)]
        [TestCase(-1f, false)]
        public void DepotBusy(float d, bool expected) { Assert.That((bool)ModAssembly.Call("SteveTheTroll.Backpack.DepotRules", "Crowded", d, 10f), Is.EqualTo(expected)); }

        [Test]
        public void DepotSpacing_MoreThanTheBusyRing()
        {
            float spacing = (float)ModAssembly.GetStatic("SteveTheTroll.Backpack.DepotRules", "StackSpacing");
            Assert.That(spacing, Is.GreaterThan(10f), "a troll unloading at one stack does not block the next one");
        }

        [Test]
        public void DepotSpacing_FitsTheFootprintAndTheGap()
        {
            float halfX = (float)ModAssembly.GetStatic("SteveTheTroll.Backpack.DepotData", "HalfX");
            float gap = (float)ModAssembly.GetStatic("SteveTheTroll.Backpack.DepotRules", "Gap");
            float spacing = (float)ModAssembly.GetStatic("SteveTheTroll.Backpack.DepotRules", "StackSpacing");
            Assert.That(spacing, Is.GreaterThanOrEqualTo(2f * halfX + gap), "two stacks side by side keep the walking gap");
        }

        static bool Inside(Vector3 bed, Vector3 pos, float r) { return (bool)ModAssembly.Call(NoHit, "Inside", bed, pos, r); }

        [Test]
        public void NoHitCircle_FlatAndInclusive()
        {
            var bed = new Vector3(10f, 40f, -20f);
            Assert.That(Inside(bed, new Vector3(19f, 0f, -8f), 15f), Is.True, "9 / 12 = 15 m flat, height ignored");
            Assert.That(Inside(bed, new Vector3(19.1f, 40f, -8f), 15f), Is.False);
            Assert.That(Inside(bed, bed, 15f), Is.True);
            Assert.That(Inside(bed, bed, 0f), Is.False, "radius 0 = off");
        }
    }
}
