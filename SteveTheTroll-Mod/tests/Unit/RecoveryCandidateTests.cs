using System.Collections.Generic;
using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Recovery.Domain;

namespace SteveTheTroll.Tests.Unit;

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
            using (Assert.EnterMultipleScope())
            {
                Assert.That(rx, Is.EqualTo(dx).Within(1e-4f)); Assert.That(rz, Is.EqualTo(dz).Within(1e-4f));
            }
        }
    }
    [Test] public void Rotate45Right_FromNorth_IsNorthEast()
    {
        float x, z; StuckRules.Rotate(0f, 1f, Side.Right, 45f, out x, out z);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(x, Is.EqualTo(0.7071f).Within(1e-3f)); Assert.That(z, Is.EqualTo(0.7071f).Within(1e-3f));
        }
    }
    [Test] public void Rotate135Left_FromNorth_IsSouthWest()
    {
        float x, z; StuckRules.Rotate(0f, 1f, Side.Left, 135f, out x, out z);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(x, Is.EqualTo(-0.7071f).Within(1e-3f)); Assert.That(z, Is.EqualTo(-0.7071f).Within(1e-3f));
        }
    }
    [Test] public void Rotate_ZeroInput_Zero() { float x, z; StuckRules.Rotate(0f, 0f, Side.Right, 70f, out x, out z);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(x, Is.EqualTo(0f)); Assert.That(z, Is.EqualTo(0f));
        }
    }

    [Test] public void Angles_TenCandidatesWideAsSpecified() { Assert.That(StuckRules.CandidateAngles, Is.EquivalentTo(new[] { 45f, 70f, 90f, 110f, 135f })); }
    [TestCase(45f, 20f)] [TestCase(70f, 20f)] [TestCase(90f, 30f)] [TestCase(135f, 30f)]
    public void CandidateMetres(float a, float m) { Assert.That(StuckRules.CandidateMetres(a), Is.EqualTo(m)); }

    [Test] public void Pick_FirstGoodInSearchOrder() { Assert.That(StuckRules.PickCandidate(new List<float> { 0f, 2f, 5f, 30f }), Is.EqualTo(2)); }
    [Test] public void Pick_NoGood_Longest() { Assert.That(StuckRules.PickCandidate(new List<float> { 0f, 1.5f, 2.5f, 1f }), Is.EqualTo(2)); }
    [Test] public void Pick_AllBlocked_None() {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(StuckRules.PickCandidate(new List<float> { 0f, 0.5f, 0f }), Is.EqualTo(-1)); Assert.That(StuckRules.PickCandidate(null), Is.EqualTo(-1));
        }
    }
    [Test] public void Pick_OtherSideWhenPreferredBlocked()
    {
        var m = new List<float>(); for (int i = 0; i < 5; i++) m.Add(0f); m.Add(0f); m.Add(12f);      // preferred side all blocked, other side 90 degrees free
        Assert.That(StuckRules.PickCandidate(m), Is.EqualTo(6));
    }

    [Test] public void LegSeconds_ShortLegKeepsTheOldLimit() { Assert.That(StuckRules.LegSeconds(10f), Is.EqualTo(StuckRules.LegMaxSeconds)); }
    [Test] public void LegSeconds_LongLegGetsTime() { Assert.That(StuckRules.LegSeconds(30f), Is.EqualTo(30f / StuckRules.MinLegSpeed)); }
    [Test] public void LegDone_LongLegNotCutAtEightSeconds() {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(StuckRules.LegDone(15f, 30f, 9f, 0f), Is.False); Assert.That(StuckRules.LegDone(15f, 30f, 12.1f, 0f), Is.True);
        }
    }

    // D-175: walking back after a long detour leg is not "progress" - only beating the closest distance of the attempt is
    [Test] public void WalkBackAfterALeg_IsNoSustainedProgress()
    {
        var m = new ProgressMeter(); m.NewAttempt(10f);
        for (int i = 0; i < 20; i++) m.Update(10f - i * 0.05f, 0.1f, true);    // creeps to 9 m (the closest of the attempt)
        m.Reset(30f);                                                             // a 20 m leg took it away
        for (int i = 0; i < 100; i++) m.Update(30f - i * 0.2f, 0.1f, true);      // 10 s walking back from 30 m to 10 m
        using (Assert.EnterMultipleScope())
        {
            Assert.That(m.SustainedSeconds, Is.LessThan(StuckRules.SustainedReset));
            Assert.That(m.StuckSeconds, Is.LessThan(StuckRules.StuckTrigger), "the stuck timer still sees the walk back as movement");
        }
    }
    [Test] public void BeatingTheClosestDistance_IsSustainedProgress()
    {
        var m = new ProgressMeter(); m.NewAttempt(30f);
        for (int i = 0; i < 120; i++) m.Update(30f - i * 0.2f, 0.1f, true);      // 12 s of real approach
        Assert.That(m.SustainedSeconds, Is.GreaterThanOrEqualTo(StuckRules.SustainedReset));
    }
    [Test] public void NewAttempt_ForgetsTheOldClosest() { var m = new ProgressMeter(); m.NewAttempt(5f); m.NewAttempt(40f); Assert.That(m.SeriesBest, Is.EqualTo(40f)); }
}
