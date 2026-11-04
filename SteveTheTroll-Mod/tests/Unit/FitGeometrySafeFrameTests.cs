
using SteveTheTroll.Features.Trolls.Taming.Domain;
using UnityEngine;

namespace SteveTheTroll.Tests.Unit;

/// <summary>
/// FitGeometry.SafeFrame: the troll on a slipper uses the frame of three skin points while it stays within maxDegrees of the frame the foot
/// bone predicts; otherwise the foot's (bug 2026-10-30: the troll hung under the slipper, tilted 65 - 88 deg for minutes).
/// </summary>
[TestFixture]
public class FitGeometrySafeFrameTests
{
    const string T = "SteveTheTroll.Fit.FitGeometry";

    // Quaternion.Euler / AngleAxis call into the engine, which a unit test does not have: build the rotation by hand.
    static Quaternion About(float x, float y, float z, float degrees)
    {
        float h = degrees * Mathf.Deg2Rad * 0.5f, s = Mathf.Sin(h);
        return new Quaternion(x * s, y * s, z * s, Mathf.Cos(h));
    }

    static bool Within(Vector3 a, Vector3 b, float max)
    {
        return (bool)ModAssembly.Call(T, "WithinMeters", new object[] { a, b, max });
    }

    static Quaternion Safe(Quaternion skin, Quaternion foot, float max, out bool usedFoot)
    {
        var args = new object[] { skin, foot, max, null };
        var q = (Quaternion)ModAssembly.Call(T, "SafeFrame", args);
        usedFoot = (bool)args[3];
        return q;
    }

    [TestCase(0f, false)]
    [TestCase(20f, false)]
    [TestCase(39f, false)]
    [TestCase(41f, true)]
    [TestCase(65f, true)]
    [TestCase(88f, true)]
    [TestCase(180f, true)]
    public void SkinFrame_IsKeptUntilItIsMoreThanTheLimitOffTheFoot(float offDegrees, bool wantFoot)
    {
        var foot = About(0f, 1f, 0f, 30f) * About(1f, 0f, 0f, 10f);
        var skin = foot * About(1f, 0f, 0f, offDegrees);
        bool usedFoot;

        var chosen = Safe(skin, foot, 40f, out usedFoot);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(usedFoot, Is.EqualTo(wantFoot));
            Assert.That(Quaternion.Angle(chosen, wantFoot ? foot : skin), Is.LessThan(0.01f));
        }
    }

    [Test]
    public void WithinMeters_IsTrueUpToAndIncludingTheLimit()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Within(Vector3.zero, new Vector3(0.1f, 0f, 0f), 0.12f), Is.True);
            Assert.That(Within(Vector3.zero, new Vector3(0.12f, 0f, 0f), 0.12f), Is.True);
            Assert.That(Within(Vector3.zero, new Vector3(0.1f, 0.1f, 0f), 0.12f), Is.False);
        }
    }

    static bool Rebind(float tiltedFor, bool grounded, float speed, bool settling)
    {
        return (bool)ModAssembly.Call(T, "ShouldRebind", new object[] { tiltedFor, 1f, grounded, speed, 0.5f, settling });
    }

    [Test] public void ShouldRebind_WhenTiltedLongEnoughAndStandingStill() { Assert.That(Rebind(1.2f, true, 0f, false), Is.True); }
    [Test] public void ShouldRebind_NotBeforeTheTime() { Assert.That(Rebind(0.5f, true, 0f, false), Is.False); }
    [Test] public void ShouldRebind_NotWhileMoving() { Assert.That(Rebind(3f, true, 4f, false), Is.False); }
    [Test] public void ShouldRebind_NotInTheAir() { Assert.That(Rebind(3f, false, 0f, false), Is.False); }
    [Test] public void ShouldRebind_NotWhileSettlingIn_TheFootItselfIsTilted() { Assert.That(Rebind(3f, true, 0f, true), Is.False); }

    static Vector3 Upright(Vector3 fwd, Vector3 up, Vector3 fallback)
    {
        return (Vector3)ModAssembly.Call(T, "UprightForward", new object[] { fwd, up, fallback });
    }

    static void Near(Vector3 a, Vector3 b) { Assert.That((a - b).magnitude, Is.LessThan(1e-4f), a + " vs " + b); }

    [Test] public void UprightForward_FlatSocket_IsKept() { Near(Upright(new Vector3(0, 0, 1), Vector3.up, Vector3.right), new Vector3(0, 0, 1)); }
    [Test] public void UprightForward_TiltedFootSocket_IsFlattened() { Near(Upright(new Vector3(0, 0.7f, 0.7f), Vector3.up, Vector3.right), new Vector3(0, 0, 1)); }
    [Test] public void UprightForward_PointingStraightUp_UsesFallback() { Near(Upright(Vector3.up, Vector3.up, new Vector3(1, 0.5f, 0)), new Vector3(1, 0, 0)); }
    [Test] public void UprightForward_IsUnitAndPerpendicularToUp() { var f = Upright(new Vector3(3, 5, 4), new Vector3(0.1f, 1, 0), Vector3.right);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(f.magnitude, Is.EqualTo(1f).Within(1e-4f)); Assert.That(Vector3.Dot(f, new Vector3(0.1f, 1, 0).normalized), Is.EqualTo(0f).Within(1e-4f));
        }
    }
    [Test] public void UprightForward_TiltedPlayerUp_StaysInThatPlane() { var up = new Vector3(0, 1, 1).normalized; var f = Upright(new Vector3(0, 0, 1), up, Vector3.right); Assert.That(Vector3.Dot(f, up), Is.EqualTo(0f).Within(1e-4f)); }

    // D-157: StepHeading limits how far the facing turns in one frame
    static Vector3 Yaw(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)); }   // forward turned deg about up (positive = towards +x)
    static Vector3 Step(Vector3 prev, Vector3 target, float max) { return (Vector3)ModAssembly.Call(T, "StepHeading", new object[] { prev, target, max, Vector3.up }); }
    // D-159: LeanUp follows the socket's up, clamped against the player's up
    static Vector3 Lean(Vector3 playerUp, Vector3 socketUp, float max) { return (Vector3)ModAssembly.Call(T, "LeanUp", new object[] { playerUp, socketUp, max }); }
    static Vector3 Tilt(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(r), Mathf.Cos(r), 0f); }   // up tilted deg towards +x
    [Test] public void LeanUp_WithinLimit_TakesTheSocketUp() { Near(Lean(Vector3.up, Tilt(30f), 45f), Tilt(30f)); }
    [Test] public void LeanUp_BeyondLimit_IsClamped() { Near(Lean(Vector3.up, Tilt(80f), 45f), Tilt(45f)); }
    [Test] public void LeanUp_Upside_IsClampedTowardsTheSocket() { Near(Lean(Vector3.up, Tilt(-170f), 45f), Tilt(-45f)); }
    [Test] public void LeanUp_ZeroSocket_IsPlayerUp() { Near(Lean(Vector3.up, Vector3.zero, 45f), Vector3.up); }
    [Test] public void StepHeading_WithinLimit_TakesTheTarget() { Near(Step(Vector3.forward, Yaw(5), 9f), Yaw(5)); }
    [Test] public void StepHeading_BeyondLimit_TurnsOnlyTheLimit() { Near(Step(Vector3.forward, Yaw(61), 9f), Yaw(9)); }
    [Test] public void StepHeading_TurnsTheShortWay() { Near(Step(Vector3.forward, Yaw(-170), 9f), Yaw(-9)); }
    [Test] public void StepHeading_NoPreviousHeading_TakesTheTarget() { Near(Step(Vector3.zero, Vector3.right, 9f), Vector3.right); }
    [Test] public void StepHeading_ExactlyTheLimit_TakesTheTarget() { Near(Step(Vector3.forward, Yaw(9), 9f), Yaw(9)); }
}
