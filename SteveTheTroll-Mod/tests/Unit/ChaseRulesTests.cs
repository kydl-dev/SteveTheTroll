using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Combat.Domain;

namespace SteveTheTroll.Tests.Unit;

[TestFixture]
public class ChaseRulesTests
{
    [Test]
    public void Approach_is_the_reach_while_swings_hurt()
    {
        Assert.That(ChaseRules.ApproachDistance(4.9f, 0), Is.EqualTo(4.9f).Within(0.001f));
        Assert.That(ChaseRules.ApproachDistance(4.9f, ChaseRules.DrySwingsBeforeClosing - 1), Is.EqualTo(4.9f).Within(0.001f));
    }

    [Test]
    public void Approach_closes_in_after_dry_swings()
    {
        Assert.That(ChaseRules.ApproachDistance(4.9f, ChaseRules.DrySwingsBeforeClosing), Is.EqualTo(2.45f).Within(0.001f));
        Assert.That(ChaseRules.ApproachDistance(4.9f, 10), Is.EqualTo(2.45f).Within(0.001f));
    }

    [Test]
    public void Close_in_point_is_inside_the_swing_range()
    {
        Assert.That(ChaseRules.ApproachDistance(4.9f, 5), Is.LessThan(4.9f));
    }

    [TestCase(40f, 40f, true)]
    [TestCase(40f, 40.5f, true)]
    [TestCase(40f, 33f, false)]
    [TestCase(40f, 39.995f, true)]
    public void A_swing_is_dry_when_the_health_did_not_drop(float before, float after, bool dry)
    {
        Assert.That(ChaseRules.IsDry(before, after), Is.EqualTo(dry));
    }
}