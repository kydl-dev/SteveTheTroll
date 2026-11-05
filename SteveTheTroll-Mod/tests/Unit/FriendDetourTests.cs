using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Farming.Domain;

namespace SteveTheTroll.Tests.Unit;

/// 2026-11-05: the friend-detour geometry (Blocked, Waypoint had no unit tests; SideStep is new). Walker at (0,0), goal straight ahead at (0,20), friend on the line.
[TestFixture]
public class FriendDetourTests
{
    private static readonly GroundPoint Me = new GroundPoint(0f, 0f), Goal = new GroundPoint(0f, 20f);

    [Test]
    public void A_friend_on_the_line_ahead_is_in_the_way()
    {
        Assert.That(TargetRules.Blocked(Me, Goal, new GroundPoint(0f, 3.2f), TargetRules.Ahead, TargetRules.HalfWidth), Is.True);
    }

    [TestCase(0f, -2f)]  // behind
    [TestCase(3f, 3f)]   // beside, outside the half width
    [TestCase(0f, 8f)]   // beyond the look-ahead
    public void A_friend_off_the_line_or_far_is_not_in_the_way(float x, float z)
    {
        Assert.That(TargetRules.Blocked(Me, Goal, new GroundPoint(x, z), TargetRules.Ahead, TargetRules.HalfWidth), Is.False);
    }

    [Test]
    public void The_side_step_goes_straight_sideways_by_side_metres()
    {
        GroundPoint s = TargetRules.SideStep(Me, Goal, new GroundPoint(0f, 3.2f), TargetRules.Side);
        Assert.That(s.Z, Is.EqualTo(0f).Within(0.001f), "no step forward towards the friend");
        Assert.That(System.Math.Abs(s.X), Is.EqualTo(TargetRules.Side).Within(0.001f));
    }

    [Test]
    public void The_side_step_goes_away_from_the_friend_and_matches_the_waypoint_side()
    {
        var friend = new GroundPoint(-0.5f, 3.2f); // a little to the walker's left
        GroundPoint s = TargetRules.SideStep(Me, Goal, friend, TargetRules.Side);
        GroundPoint w = TargetRules.Waypoint(Me, Goal, friend, TargetRules.Side);
        Assert.That(s.X, Is.GreaterThan(0f), "steps to the right, away from the friend");
        Assert.That(System.Math.Sign(w.X - friend.X), Is.EqualTo(System.Math.Sign(s.X)), "both legs on the same side");
    }

    [Test]
    public void The_second_leg_keeps_two_troll_bodies_from_the_friend()
    {
        var friend = new GroundPoint(0f, 3.2f);
        GroundPoint s = TargetRules.SideStep(Me, Goal, friend, TargetRules.Side);
        GroundPoint w = TargetRules.Waypoint(Me, Goal, friend, TargetRules.Side);
        // distance from the friend's centre to the segment s -> w (both at x = +-Side: the leg runs parallel to the line)
        Assert.That(System.Math.Abs(s.X - w.X), Is.LessThan(0.001f), "parallel to the walking line");
        Assert.That(System.Math.Abs(s.X - friend.X), Is.GreaterThanOrEqualTo(3.5f), "the old diagonal passed ~2.2 m from the friend - inside two bodies");
    }
}