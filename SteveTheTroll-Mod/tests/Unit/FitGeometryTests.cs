using System;
using FsCheck;
using UnityEngine;

namespace SteveTheTroll.Tests.Unit;

/// <summary>
/// SteveTheTroll.Fit.FitGeometry: the pure fit maths shared by the mod and the Unity fit scene (foot direction + its +-75 deg clamp and
/// running-flip fix D-029, placement in the foot frame, log size / offset, slipper toe weights D-025, flip detector, dropped-pair layout).
/// Expected values come from the documented behaviour (DECISIONS D-016 / D-019 / D-025 / D-029, the XML docs), not from the formulas.
/// The engine-side wrappers (TrollFitMath / SlipperFit with Quaternion, Transform, Mesh) are tested in Unity: Assets/SteveTheTroll_Fit/Tests.
/// </summary>
[TestFixture]
public class FitGeometryTests
{
    const string T = "SteveTheTroll.Fit.FitGeometry";
    const float Eps = 1e-4f;
    static readonly Vector3 Fwd = new Vector3(0f, 0f, 1f);
    static readonly Vector3 KneeToFoot = new Vector3(0f, -0.4f, 0f);       // shin of a standing leg (foot - knee)
    static readonly Vector3 ToeAhead = new Vector3(0f, -0.05f, 0.15f);     // foot -> toe of a flat foot

    static object C(string m, params object[] a) { return ModAssembly.Call(T, m, a); }
    static Vector3 V(string m, params object[] a) { return (Vector3)C(m, a); }
    static float F(string m, params object[] a) { return (float)C(m, a); }
    static float Const(string name) { return (float)ModAssembly.GetStatic(T, name); }

    static void Near(Vector3 expected, Vector3 actual, float eps = Eps)
    {
        Assert.That((expected - actual).magnitude, Is.LessThan(eps), "expected " + expected.ToString("F4") + ", got " + actual.ToString("F4"));
    }

    static float Yaw(Vector3 v) { return Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg; }
    static Vector3 Dir(float yawDeg) { float r = yawDeg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)); }

    static readonly Gen<float> Coord = Props.GameFloat(5f);
    static readonly Gen<Vector3> AnyVec = from x in Coord from y in Coord from z in Coord select new Vector3(x, y, z);
    static readonly Gen<Vector3> FlatVec = AnyVec.Select(v => new Vector3(v.x, 0f, v.z)).Where(v => v.sqrMagnitude > 0.01f);
    static readonly Gen<float> Angle = Props.GameFloat(179f);

    // ---------------- constants (the documented limits) ----------------

    [Test]
    public void Constants_AreTheDocumentedLimits()
    {
        Assert.That(Const("MaxFootTurnDeg"), Is.EqualTo(75f));
        Assert.That(Const("JumpDeg"), Is.EqualTo(45f));
        Assert.That(Const("ToeBlendWidth"), Is.EqualTo(0.03f));
    }

    // ---------------- Flat ----------------

    [Test]
    public void Flat_DropsHeightAndNormalises() { Near(new Vector3(0.6f, 0f, 0.8f), V("Flat", new Vector3(3f, 5f, 4f), Fwd)); }

    [Test]
    public void Flat_VerticalUsesTheFlattenedFallback() { Near(new Vector3(1f, 0f, 0f), V("Flat", Vector3.up, new Vector3(2f, 7f, 0f))); }

    [Test]
    public void Flat_VerticalWithVerticalFallback_IsWorldForward() { Near(Vector3.forward, V("Flat", Vector3.down, Vector3.up * 3f)); }

    [Test]
    public void Flat_ZeroUsesTheFallback() { Near(new Vector3(-1f, 0f, 0f), V("Flat", Vector3.zero, new Vector3(-0.5f, 0f, 0f))); }

    [Test]
    public void Flat_NearlyVertical_UsesTheFallback_SmallButClearSlopeDoesNot()
    {
        Near(new Vector3(0f, 0f, -1f), V("Flat", new Vector3(0.005f, 1f, 0f), new Vector3(0f, 0f, -1f)));      // 0.005 m: below 0.01
        Near(new Vector3(1f, 0f, 0f), V("Flat", new Vector3(0.02f, 1f, 0f), new Vector3(0f, 0f, -1f)));        // 0.02 m: kept
    }

    [Test]
    public void Flat_AlwaysGivesAHorizontalUnitVector()
    {
        Props.Check(Prop.ForAll((from d in AnyVec from f in AnyVec select new[] { d, f }).ToArbitrary(), p =>
        {
            var r = V("Flat", p[0], p[1]);
            return Mathf.Abs(r.y) < Eps && Mathf.Abs(r.magnitude - 1f) < Eps;
        }));
    }

    // ---------------- RotateY / YawDeg ----------------

    [Test]
    public void RotateY_TurnsLikeUnity_Plus90IsForwardToRight()
    {
        Near(new Vector3(1f, 0f, 0f), V("RotateY", Vector3.forward, 90f));
        Near(new Vector3(-1f, 0f, 0f), V("RotateY", Vector3.forward, -90f));
        Near(new Vector3(0f, 0f, -1f), V("RotateY", new Vector3(1f, 0f, 0f), 90f));
        Near(new Vector3(0f, 2f, -1f), V("RotateY", new Vector3(0f, 2f, 1f), 180f));
    }

    [Test]
    public void RotateY_KeepsHeightAndLength_AndUndoesItself()
    {
        Props.Check(Prop.ForAll((from v in AnyVec from a in Angle select Tuple.Create(v, a)).ToArbitrary(), p =>
        {
            var r = V("RotateY", p.Item1, p.Item2);
            var back = V("RotateY", r, -p.Item2);
            return Mathf.Abs(r.y - p.Item1.y) < Eps && Mathf.Abs(r.magnitude - p.Item1.magnitude) < 1e-3f && (back - p.Item1).magnitude < 1e-3f;
        }));
    }

    [Test]
    public void YawDeg_IsTheHeadingFromForward()
    {
        Assert.That(F("YawDeg", Vector3.forward), Is.EqualTo(0f).Within(Eps));
        Assert.That(F("YawDeg", new Vector3(1f, 0f, 0f)), Is.EqualTo(90f).Within(Eps));
        Assert.That(F("YawDeg", new Vector3(-2f, 0f, 0f)), Is.EqualTo(-90f).Within(Eps));
        Assert.That(Mathf.Abs(F("YawDeg", new Vector3(0f, 0f, -1f))), Is.EqualTo(180f).Within(Eps));
    }

    [Test]
    public void YawDeg_OfARotatedForward_IsTheAngle()
    {
        Props.Check(Prop.ForAll(Angle.ToArbitrary(), a => Mathf.Abs(F("YawDeg", V("RotateY", Vector3.forward, a)) - a) < 1e-2f));
    }

    // ---------------- FootForward (D-016 / D-029) ----------------

    [Test]
    public void FootForward_StandingFlatFoot_IsTheToeDirection()
    {
        Near(Vector3.forward, V("FootForward", ToeAhead, KneeToFoot, Fwd));
        Near(Dir(30f), V("FootForward", V("RotateY", ToeAhead, 30f), KneeToFoot, Fwd), 1e-3f);
        Near(Dir(-50f), V("FootForward", V("RotateY", ToeAhead, -50f), KneeToFoot, Fwd), 1e-3f);
    }

    /// TEST-005 regression: running, leg swung back, toe pointing down and slightly BACK. The flattened toe direction is backwards
    /// (that made the trolls look at the camera); the lateral axis still gives forward.
    [Test]
    public void FootForward_RunningToePitchedPastVertical_StillPointsForward()
    {
        var toe = new Vector3(0f, -0.15f, -0.01f);
        var shin = new Vector3(0f, -0.3f, -0.25f);
        Assert.That(Vector3.Dot(V("Flat", toe, Fwd), Fwd), Is.LessThan(0f), "precondition: the old method pointed backwards");
        Near(Vector3.forward, V("FootForward", toe, shin, Fwd));
    }

    /// Why the LATERAL axis (D-029): toe pointing almost straight down (mid stride, leg swung back) - its flattened direction is just
    /// noise (here 90 deg sideways, clamped to 75); the axis across the foot still knows where forward is.
    [Test]
    public void FootForward_ToeStraightDown_UsesTheAxisAcrossTheFoot()
    {
        var toe = new Vector3(0.01f, -0.15f, 0f);
        var shin = new Vector3(0f, -0.3f, -0.2f);
        Assert.That(Yaw(V("Flat", toe, Fwd)), Is.EqualTo(90f).Within(1e-2f), "precondition: the flattened toe points sideways");
        Assert.That(Mathf.Abs(Yaw(V("FootForward", toe, shin, Fwd))), Is.LessThan(10f));
    }

    [Test]
    public void FootForward_FootTurnedPastTheLimit_IsClampedTo75()
    {
        // (exactly 90 deg is ambiguous by design: the lateral axis has no front/back there, either +-75 is right)
        Assert.That(Yaw(V("FootForward", V("RotateY", ToeAhead, 85f), KneeToFoot, Fwd)), Is.EqualTo(75f).Within(1e-2f));
        Assert.That(Yaw(V("FootForward", V("RotateY", ToeAhead, -88f), KneeToFoot, Fwd)), Is.EqualTo(-75f).Within(1e-2f));
        Assert.That(Yaw(V("FootForward", V("RotateY", ToeAhead, 74f), KneeToFoot, Fwd)), Is.EqualTo(74f).Within(1e-2f));
    }

    [Test]
    public void FootForward_ClampIsMeasuredFromThePlayersForward()
    {
        var pf = Dir(100f);
        Assert.That(Yaw(V("FootForward", V("RotateY", ToeAhead, 100f + 85f), KneeToFoot, pf)), Is.EqualTo(175f).Within(1e-2f));
        Assert.That(Yaw(V("FootForward", V("RotateY", ToeAhead, 100f + 20f), KneeToFoot, pf)), Is.EqualTo(120f).Within(1e-2f));
    }

    [Test]
    public void FootForward_ToePointingBackwards_IsTurnedToTheFront()
    {
        Near(Vector3.forward, V("FootForward", new Vector3(0f, -0.05f, -0.15f), KneeToFoot, Fwd));
    }

    [Test]
    public void FootForward_NoToeDirection_IsThePlayersFlattenedForward()
    {
        Near(Dir(40f), V("FootForward", Vector3.zero, KneeToFoot, Dir(40f) + Vector3.up * 3f));
    }

    [Test]
    public void FootForward_ShinAlongTheFoot_FallsBackToTheFlattenedToe()
    {
        var toe = new Vector3(0.1f, -0.02f, 0.1f);
        Near(Dir(45f), V("FootForward", toe, toe * 3f, Fwd), 1e-3f);
    }

    [Test]
    public void FootForward_VerticalPlayerForward_UsesWorldForward()
    {
        Near(Vector3.forward, V("FootForward", ToeAhead, KneeToFoot, Vector3.up));
    }

    [Test]
    public void FootForward_NeverBackwards_NeverPastTheLimit_AlwaysFlatUnit()
    {
        Props.Check(Prop.ForAll((from t in AnyVec from s in AnyVec from p in FlatVec select new[] { t, s, p }).ToArbitrary(), q =>
        {
            var r = V("FootForward", q[0], q[1], q[2]);
            var pf = q[2].normalized;
            return Mathf.Abs(r.y) < Eps && Mathf.Abs(r.magnitude - 1f) < 1e-3f && Vector3.Dot(r, pf) > -Eps
                && Mathf.Abs(Vector3.SignedAngle(pf, r, Vector3.up)) <= 75f + 1e-2f;
        }));
    }

    // ---------------- FootFramePosition / FigureYawDeg ----------------

    [Test]
    public void FootFramePosition_RightFoot_SideIsOutwardToTheRight()
    {
        Near(new Vector3(1.1f, 1.98f, 3.2f), V("FootFramePosition", new Vector3(1f, 2f, 3f), Fwd, false, 0.2f, 0.1f, -0.05f, 0.03f));
    }

    [Test]
    public void FootFramePosition_LeftFoot_SideIsMirrored()
    {
        Near(new Vector3(0.9f, 1.98f, 3.2f), V("FootFramePosition", new Vector3(1f, 2f, 3f), Fwd, true, 0.2f, 0.1f, -0.05f, 0.03f));
    }

    [Test]
    public void FootFramePosition_FollowsATurnedFoot_AndIgnoresItsPitch()
    {
        Near(new Vector3(0.3f, 0.1f, -0.2f), V("FootFramePosition", Vector3.zero, new Vector3(1f, -2f, 0f), false, 0.3f, 0.2f, 0.1f, 0f));
    }

    [Test]
    public void FootFramePosition_HeightOnlyFromFootUpAndLift_FeetMirrorEachOther()
    {
        var gen = from foot in AnyVec from fwd in FlatVec from f in Coord from s in Coord from u in Coord from l in Coord
                  select Tuple.Create(foot, fwd, new[] { f, s, u, l });
        Props.Check(Prop.ForAll(gen.ToArbitrary(), p =>
        {
            var a = p.Item3;
            var right = V("FootFramePosition", p.Item1, p.Item2, false, a[0], a[1], a[2], a[3]);
            var left = V("FootFramePosition", p.Item1, p.Item2, true, a[0], a[1], a[2], a[3]);
            var mid = (right + left) * 0.5f;
            var alongFoot = p.Item1 + p.Item2.normalized * a[0];
            return Mathf.Abs(right.y - (p.Item1.y + a[2] + a[3])) < 1e-3f && Mathf.Abs(left.y - right.y) < 1e-4f
                && new Vector2(mid.x - alongFoot.x, mid.z - alongFoot.z).magnitude < 1e-3f
                && Mathf.Abs((right - left).magnitude - 2f * Mathf.Abs(a[1])) < 1e-3f;
        }));
    }

    [Test]
    public void FigureYawDeg_IsFootHeadingPlusYaw()
    {
        Assert.That(F("FigureYawDeg", new Vector3(1f, 0f, 0f), 10f), Is.EqualTo(100f).Within(Eps));
        Assert.That(F("FigureYawDeg", new Vector3(0f, 0.5f, 1f), -12f), Is.EqualTo(-12f).Within(Eps));
        Assert.That(F("FigureYawDeg", Vector3.up, 5f), Is.EqualTo(5f).Within(Eps));
    }

    // ---------------- slipper skin weights (D-025) ----------------

    [Test]
    public void ToeAlong_IsTheDistanceInFrontOfTheToeJoint()
    {
        Assert.That(F("ToeAlong", new Vector3(1f, 5f, 2.25f), new Vector3(1f, 0f, 2f), Fwd), Is.EqualTo(0.25f).Within(Eps));
        Assert.That(F("ToeAlong", new Vector3(0f, 0f, 1.9f), new Vector3(3f, 0f, 2f), Fwd), Is.EqualTo(-0.1f).Within(Eps));
    }

    [TestCase(-1f, 0f)]
    [TestCase(-0.03f, 0f)]
    [TestCase(-0.015f, 0.15625f)]
    [TestCase(0f, 0.5f)]
    [TestCase(0.015f, 0.84375f)]
    [TestCase(0.03f, 1f)]
    [TestCase(2f, 1f)]
    public void ToeWeight_BlendsSmoothlyAcrossTheToeJoint(float along, float weight)
    {
        Assert.That(F("ToeWeight", along), Is.EqualTo(weight).Within(Eps));
    }

    [Test]
    public void ToeWeight_StaysIn01_RisesForward_AndFootPlusToeIsSymmetric()
    {
        var gen = from a in Props.GameFloat(0.1f) from b in Props.GameFloat(0.1f) select Tuple.Create(Math.Min(a, b), Math.Max(a, b));
        Props.Check(Prop.ForAll(gen.ToArbitrary(), p =>
        {
            float lo = F("ToeWeight", p.Item1), hi = F("ToeWeight", p.Item2);
            return lo >= 0f && hi <= 1f && lo <= hi + 1e-6f && Mathf.Abs(F("ToeWeight", p.Item1) + F("ToeWeight", -p.Item1) - 1f) < 1e-4f;
        }));
    }

    // ---------------- log size / offset (D-018 / D-019) ----------------

    [Test]
    public void LogScaleFactor_MakesTheLongestAxisTheWantedLength()
    {
        Assert.That(F("LogScaleFactor", new Vector3(0.1f, 0.5f, 0.2f), 2f, 0.5f, 0.28f), Is.EqualTo(0.14f).Within(Eps));
        Assert.That(F("LogScaleFactor", new Vector3(0.9f, 0.1f, 0.2f), 1f, 0.5f, 0.36f), Is.EqualTo(0.2f).Within(Eps));
        Assert.That(F("LogScaleFactor", new Vector3(0.1f, 0.2f, 0.4f), 1f, 1f, 0.2f), Is.EqualTo(0.5f).Within(Eps));
    }

    [Test]
    public void LogScaleFactor_NoSize_LeavesTheScale()
    {
        Assert.That(F("LogScaleFactor", Vector3.zero, 1f, 0.5f, 0.28f), Is.EqualTo(1f));
        Assert.That(F("LogScaleFactor", new Vector3(1f, 1f, 1f), 0f, 0.5f, 0.28f), Is.EqualTo(1f));
    }

    [Test]
    public void LogScaleFactor_ScaledLogIsAlwaysTheWantedLength()
    {
        var size = from x in Props.GameFloat(2f) from y in Props.GameFloat(2f) from z in Props.GameFloat(2f)
                   select new Vector3(Math.Abs(x) + 0.01f, Math.Abs(y) + 0.01f, Math.Abs(z) + 0.01f);
        var gen = from s in size from lossy in Gen.Choose(1, 2000) from r in Gen.Choose(1, 100) from h in Gen.Choose(10, 1000)
                  select Tuple.Create(s, lossy / 10f, r / 100f, h / 1000f);
        Props.Check(Prop.ForAll(gen.ToArbitrary(), p =>
        {
            float worldLong = Math.Max(p.Item1.x, Math.Max(p.Item1.y, p.Item1.z)) * p.Item2;
            float want = p.Item3 * p.Item4;
            return Math.Abs(F("LogScaleFactor", p.Item1, p.Item2, p.Item3, p.Item4) * worldLong - want) < want * 1e-4f;
        }));
    }

    [Test]
    public void LogOffsetRatio_AddsTheFineTuneInHeights()
    {
        Near(new Vector3(0.2f, 0.2f, 0.3f), V("LogOffsetRatio", new Vector3(0.1f, 0.2f, 0.3f), new Vector3(0.028f, 0f, 0f), 0.28f));
        Near(new Vector3(0f, -0.5f, 0.25f), V("LogOffsetRatio", Vector3.zero, new Vector3(0f, -0.1f, 0.05f), 0.2f));
    }

    [Test]
    public void LogOffsetRatio_ZeroHeight_IsGuardedNotInfinite()
    {
        var r = V("LogOffsetRatio", Vector3.zero, new Vector3(0.001f, 0f, 0f), 0f);
        Assert.That(r.x, Is.EqualTo(10f).Within(1e-3f));
    }

    // ---------------- flip detector (D-029) ----------------

    [Test]
    public void FlipStep_FirstFrameIsZero_ThenTheTurnInDegrees()
    {
        Assert.That(F("FlipStep", Vector3.zero, Dir(130f)), Is.EqualTo(0f));
        Assert.That(F("FlipStep", Vector3.forward, new Vector3(1f, 0f, 0f)), Is.EqualTo(90f).Within(1e-3f));
        Assert.That(F("FlipStep", Dir(10f), Dir(-170f)), Is.EqualTo(180f).Within(1e-2f));
        Assert.That(F("FlipStep", Dir(5f), Dir(5f)), Is.EqualTo(0f).Within(1e-2f));
    }

    [TestCase(0f, false)]
    [TestCase(45f, false)]
    [TestCase(45.01f, true)]
    [TestCase(178f, true)]
    public void IsJump_OnlyMoreThan45Degrees(float step, bool jump)
    {
        Assert.That((bool)C("IsJump", step), Is.EqualTo(jump));
    }

    // ---------------- dropped pair (SlipperDropModel) ----------------

    [Test]
    public void PairOffset_LeavesTheGapBetweenTheSlippers()
    {
        Assert.That(F("PairOffset", 0.12f, 0.015f), Is.EqualTo(0.0675f).Within(Eps));
        Props.Check(Prop.ForAll((from w in Gen.Choose(1, 500) from g in Gen.Choose(0, 100) select Tuple.Create(w / 1000f, g / 1000f)).ToArbitrary(),
            p => Math.Abs(2f * F("PairOffset", p.Item1, p.Item2) - p.Item1 - p.Item2) < 1e-5f));
    }

    [Test]
    public void BoxCorner_BitsPickTheMaxSide()
    {
        var b = new Bounds(new Vector3(1f, 2f, 3f), new Vector3(2f, 4f, 6f));
        Near(new Vector3(0f, 0f, 0f), V("BoxCorner", b, 0));
        Near(new Vector3(2f, 0f, 0f), V("BoxCorner", b, 1));
        Near(new Vector3(0f, 4f, 0f), V("BoxCorner", b, 2));
        Near(new Vector3(0f, 0f, 6f), V("BoxCorner", b, 4));
        Near(new Vector3(2f, 4f, 6f), V("BoxCorner", b, 7));
    }

    [Test]
    public void BoxCorner_TheEightCornersRebuildTheBox()
    {
        var gen = from c in AnyVec from s in AnyVec select new Bounds(c, new Vector3(Math.Abs(s.x) + 0.01f, Math.Abs(s.y) + 0.01f, Math.Abs(s.z) + 0.01f));
        Props.Check(Prop.ForAll(gen.ToArbitrary(), b =>
        {
            var r = new Bounds(V("BoxCorner", b, 0), Vector3.zero);
            for (int i = 1; i < 8; i++) r.Encapsulate(V("BoxCorner", b, i));
            return (r.center - b.center).magnitude < 1e-4f && (r.size - b.size).magnitude < 1e-4f;
        }));
    }
}
