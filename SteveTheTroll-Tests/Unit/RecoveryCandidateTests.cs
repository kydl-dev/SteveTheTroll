using System.Collections.Generic;
using NUnit.Framework;
using SteveTheTroll.Trolls.Recovery.Domain;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-174 (review of far-troll-goes-home): the first detour leg searches several angles and lengths instead of one 10 m leg at 90 degrees.</summary>
    [TestFixture]
    public class RecoveryCandidateTests
    {
        [Test] public void Rotate90_IsDirection()
        {
            float rx, rz, dx, dz;
            foreach (var side in new[] { Side.Right, Side.Left })
            {
                StuckRules.Rotate(0.6f, 0.8f, side, 90f, out rx, out rz);
                StuckRules.Direction(0.6f, 0.8f, side, out dx, out dz);
                Assert.That(rx, Is.EqualTo(dx).Within(1e-4f)); Assert.That(rz, Is.EqualTo(dz).Within(1e-4f));
            }
        }
        [Test] public void Rotate45Right_FromNorth_IsNorthEast()
        {
            float x, z; StuckRules.Rotate(0f, 1f, Side.Right, 45f, out x, out z);
            Assert.That(x, Is.EqualTo(0.7071f).Within(1e-3f)); Assert.That(z, Is.EqualTo(0.7071f).Within(1e-3f));
        }
        [Test] public void Rotate135Left_FromNorth_IsSouthWest()
        {
            float x, z; StuckRules.Rotate(0f, 1f, Side.Left, 135f, out x, out z);
            Assert.That(x, Is.EqualTo(-0.7071f).Within(1e-3f)); Assert.That(z, Is.EqualTo(-0.7071f).Within(1e-3f));
        }
        [Test] public void Rotate_ZeroInput_Zero() { float x, z; StuckRules.Rotate(0f, 0f, Side.Right, 70f, out x, out z); Assert.That(x, Is.EqualTo(0f)); Assert.That(z, Is.EqualTo(0f)); }

        [Test] public void Angles_TenCandidatesWideAsSpecified() { Assert.That(StuckRules.CandidateAngles, Is.EquivalentTo(new[] { 45f, 70f, 90f, 110f, 135f })); }
        [TestCase(45f, 20f)] [TestCase(70f, 20f)] [TestCase(90f, 30f)] [TestCase(135f, 30f)]
        public void CandidateMetres(float a, float m) { Assert.That(StuckRules.CandidateMetres(a), Is.EqualTo(m)); }

        [Test] public void Pick_FirstGoodInSearchOrder() { Assert.That(StuckRules.PickCandidate(new List<float> { 0f, 2f, 5f, 30f }), Is.EqualTo(2)); }
        [Test] public void Pick_NoGood_Longest() { Assert.That(StuckRules.PickCandidate(new List<float> { 0f, 1.5f, 2.5f, 1f }), Is.EqualTo(2)); }
        [Test] public void Pick_AllBlocked_None() { Assert.That(StuckRules.PickCandidate(new List<float> { 0f, 0.5f, 0f }), Is.EqualTo(-1)); Assert.That(StuckRules.PickCandidate(null), Is.EqualTo(-1)); }
        [Test] public void Pick_OtherSideWhenPreferredBlocked()
        {
            var m = new List<float>(); for (int i = 0; i < 5; i++) m.Add(0f); m.Add(0f); m.Add(12f);      // preferred side all blocked, other side 90 degrees free
            Assert.That(StuckRules.PickCandidate(m), Is.EqualTo(6));
        }

        [Test] public void LegSeconds_ShortLegKeepsTheOldLimit() { Assert.That(StuckRules.LegSeconds(10f), Is.EqualTo(StuckRules.LegMaxSeconds)); }
        [Test] public void LegSeconds_LongLegGetsTime() { Assert.That(StuckRules.LegSeconds(30f), Is.EqualTo(30f / StuckRules.MinLegSpeed)); }
        [Test] public void LegDone_LongLegNotCutAtEightSeconds() { Assert.That(StuckRules.LegDone(15f, 30f, 9f, 0f), Is.False); Assert.That(StuckRules.LegDone(15f, 30f, 12.1f, 0f), Is.True); }
    }
}
