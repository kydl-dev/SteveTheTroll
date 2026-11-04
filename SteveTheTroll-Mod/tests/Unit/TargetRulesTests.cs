using NUnit.Framework;

namespace SteveTheTroll.Tests.Unit;

/// <summary>D-151: trolls do not share a tree, and walk around a friendly troll standing in their way.</summary>
[TestFixture]
public class TargetRulesTests
{
    const string T = "SteveTheTroll.Features.Trolls.Farming.Domain.TargetRules";
    const string PointType = "SteveTheTroll.Features.Trolls.Farming.Domain.GroundPoint";
    const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

    static object P(float x, float z)
    {
        return System.Activator.CreateInstance(ModAssembly.Type(PointType), Any, null, new object[] { x, z }, null);
    }

    static float Coord(object point, string name)
    {
        return (float)point.GetType().GetProperty(name, Any).GetValue(point, null);
    }

    static bool Blocked(float ox, float oz, float gz = 10f)
    {
        return (bool)ModAssembly.Call(T, "Blocked", P(0f, 0f), P(0f, gz), P(ox, oz), 3.5f, 1.8f);
    }

    [Test] public void Blocked_TrollRightAhead() { Assert.That(Blocked(0f, 2f), Is.True); }
    [Test] public void Blocked_TrollAheadAndSlightlyAside_StillInTheWay() { Assert.That(Blocked(1.5f, 2f), Is.True); }
    [Test] public void NotBlocked_TrollWellBesideTheLine() { Assert.That(Blocked(3f, 2f), Is.False); }
    [Test] public void NotBlocked_TrollBehind() { Assert.That(Blocked(0f, -2f), Is.False); }
    [Test] public void NotBlocked_TrollTooFarAhead() { Assert.That(Blocked(0f, 6f), Is.False); }
    [Test] public void NotBlocked_TrollBeyondTheGoal() { Assert.That(Blocked(0f, 12f), Is.False); }
    [Test] public void NotBlocked_TrollAtTheGoal_ItWorksThereAndTheReservationDecidesWho() { Assert.That(Blocked(0f, 9.8f), Is.False); }
    [Test] public void NotBlocked_AlreadyAtTheGoal() { Assert.That(Blocked(0f, 1f, 0.2f), Is.False); }

    static float[] Way(float ox, float oz)
    {
        object way = ModAssembly.Call(T, "Waypoint", P(0f, 0f), P(0f, 10f), P(ox, oz), 2.8f);
        return new[] { Coord(way, "X"), Coord(way, "Z") };
    }

    [Test] public void Waypoint_OtherSlightlyRight_GoesLeftOfIt() { var w = Way(0.5f, 2f); Assert.That(w[0], Is.LessThan(0.5f - 2f)); Assert.That(w[1], Is.EqualTo(3f).Within(1e-3f)); }
    [Test] public void Waypoint_OtherSlightlyLeft_GoesRightOfIt() { var w = Way(-0.5f, 2f); Assert.That(w[0], Is.GreaterThan(-0.5f + 2f)); }
    [Test] public void Waypoint_OtherExactlyOnTheLine_TakesTheWalkersRight() { var w = Way(0f, 2f); Assert.That(w[0], Is.LessThan(-2f)); }
    [Test] public void Waypoint_IsBesideTheOther_NotOnTheLine() { var w = Way(0f, 2f); Assert.That(System.Math.Abs(w[0]), Is.GreaterThan(1.8f)); }

    static bool Held(int holder, float at, int me, float now) { return (bool)ModAssembly.Call(T, "HeldByOther", holder, at, me, now, 6f); }
    [Test] public void Held_ByAnotherTrollRecently() { Assert.That(Held(7, 10f, 5, 12f), Is.True); }
    [Test] public void NotHeld_ByMyself() { Assert.That(Held(5, 10f, 5, 12f), Is.False); }
    [Test] public void NotHeld_AfterTheHoldTime() { Assert.That(Held(7, 10f, 5, 17f), Is.False); }
    [Test] public void NotHeld_ByNobody() { Assert.That(Held(0, 0f, 5, 1f), Is.False); }
}
