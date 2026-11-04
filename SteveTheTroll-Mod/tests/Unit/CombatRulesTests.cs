using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Combat.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>D-171 P5: the game-free combat rules (guard, rage, slam, counter, no-hit), each case = a branch of the old inline code.</summary>
[TestFixture]
public class CombatRulesTests
{
    const float G2 = 30f * 30f, K2 = 70f * 70f, R2 = 20f * 20f;

    [Test] public void Guard_NoDefence_NoOwnerNear_Never()
    {
        Assert.That(GuardRules.Eligible(false, true, 1f, 1f, G2, K2, R2), Is.False);
        Assert.That(GuardRules.Eligible(true, false, 1f, 1f, G2, K2, R2), Is.False);
    }
    [Test] public void Guard_TooFarFromTheTroll_D081() { Assert.That(GuardRules.Eligible(true, true, 31f * 31f, 1f, G2, K2, R2), Is.False); }
    [Test] public void Guard_NearTroll_OrNearOwner()
    {
        Assert.That(GuardRules.Eligible(true, true, 10f * 10f, 50f * 50f, G2, K2, R2), Is.True, "near the troll");
        Assert.That(GuardRules.Eligible(true, true, 25f * 25f, 5f * 5f, G2, K2, R2), Is.True, "near the owner, inside the guard range");
        Assert.That(GuardRules.Eligible(true, true, 25f * 25f, 25f * 25f, G2, K2, R2), Is.False, "near neither");
    }
    [Test] public void Guard_Nearest() { Assert.That(GuardRules.Nearest(new[] { 5f, 2f, 2f, 9f }), Is.EqualTo(1)); Assert.That(GuardRules.Nearest(new float[0]), Is.EqualTo(-1)); Assert.That(GuardRules.Nearest(null), Is.EqualTo(-1)); }

    [Test] public void Rage_Range_AndOff() { Assert.That(RageRules.Enrages(9.9f, 10f), Is.True); Assert.That(RageRules.Enrages(10.1f, 10f), Is.False); Assert.That(RageRules.Enrages(0f, 0f), Is.False, "0 = off"); }
    [Test] public void Rage_CalmsDown()
    {
        Assert.That(RageRules.CalmsDown(true, true, 5f, 10f), Is.False);
        Assert.That(RageRules.CalmsDown(true, true, 10.1f, 10f), Is.True);
        Assert.That(RageRules.CalmsDown(false, true, 0f, 10f), Is.True);
        Assert.That(RageRules.CalmsDown(true, false, 0f, 10f), Is.True);
    }
    [Test] public void Rage_Reach_D084() { Assert.That(RageRules.Reach(1.5f, 2f), Is.EqualTo(6f)); }

    [Test] public void Slam_Done_AndReady()
    {
        Assert.That(SlamRules.Done(0, false, 1f, 16f), Is.True);
        Assert.That(SlamRules.Done(0, true, 1f, 16f), Is.False, "the last slam still runs");
        Assert.That(SlamRules.Done(2, false, 16.1f, 16f), Is.True, "time up");
        Assert.That(SlamRules.Ready(1, false, true, true), Is.True);
        Assert.That(SlamRules.Ready(0, false, true, true), Is.False);
        Assert.That(SlamRules.Ready(1, true, true, true), Is.False);
        Assert.That(SlamRules.Ready(1, false, false, true), Is.False);
        Assert.That(SlamRules.Ready(1, false, true, false), Is.False);
    }

    [Test] public void Counter_Order()
    {
        Assert.That(CounterRules.Respond(true, 0f, 0f, -999f, false, 60f), Is.EqualTo(CounterResponse.Busy));
        Assert.That(CounterRules.Respond(false, 5f, 10f, -999f, false, 60f), Is.EqualTo(CounterResponse.Ignore));
        Assert.That(CounterRules.Respond(false, 5f, 0f, -999f, false, 60f), Is.EqualTo(CounterResponse.Slam), "first hit");
        Assert.That(CounterRules.Respond(false, 30f, 0f, 10f, true, 60f), Is.EqualTo(CounterResponse.Chase), "hit again soon");
        Assert.That(CounterRules.Respond(false, 100f, 0f, 10f, true, 60f), Is.EqualTo(CounterResponse.Slam), "after the calm time");
    }

    [Test] public void NoHit_Circle()
    {
        Assert.That(NoHitRules.Inside(0f, 0f, 3f, 4f, 5f), Is.True);
        Assert.That(NoHitRules.Inside(0f, 0f, 3f, 4.1f, 5f), Is.False);
        Assert.That(NoHitRules.Inside(0f, 0f, 0f, 0f, 0f), Is.False, "0 = off");
    }
}
