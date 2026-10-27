using UnityEngine;

namespace SteveTheTroll.Fit
{
    /// <summary>
    /// The pure maths of the fit (troll placement, foot direction, log size, slipper skin weights, flip detector, dropped-pair layout).
    /// Only MANAGED Unity value types here (Vector3, Bounds, Mathf - no Quaternion / Transform / Mesh, which call into the engine), so the
    /// plain .NET unit tests run it and Stryker can mutate it (_agent/testrules/quality.json). Shared by the mod and the Unity fit scene;
    /// TrollFitMath / SlipperFit feed it engine data. That the engine-side wrappers give the same answers is checked by the Unity EditMode
    /// tests (Assets/SteveTheTroll_Fit/Tests).
    /// </summary>
    public static class FitGeometry
    {
        /// Feet never turn further than this from the player's forward (D-029 clamp; the idle stance measured 16 / 64 deg).
        public const float MaxFootTurnDeg = 75f;
        /// D-029 flip detector: a figure turning more than this between two frames counts as a jump (CHECK in TrollFigure).
        public const float JumpDeg = 45f;
        /// m, foot -> toe weight blend around the toe joint (D-025).
        public const float ToeBlendWidth = 0.03f;
        const float MinSqr = 0.0001f;

        /// A direction flattened to the ground and normalised; `fallback` (flattened) when it is (nearly) vertical or zero, then +Z.
        public static Vector3 Flat(Vector3 dir, Vector3 fallback)
        {
            var f = new Vector3(dir.x, 0f, dir.z);
            if (f.sqrMagnitude >= MinSqr) return f.normalized;
            f = new Vector3(fallback.x, 0f, fallback.z);
            // Stryker disable once Equality : < vs <= differ only at exactly 0.0001 m2, which no float input reaches
            return f.sqrMagnitude < MinSqr ? Vector3.forward : f.normalized;
        }

        /// v turned by deg about world up, Unity's sense (+90 turns +Z into +X) - same as Quaternion.AngleAxis(deg, Vector3.up) * v.
        public static Vector3 RotateY(Vector3 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector3(v.x * c + v.z * s, v.y, -v.x * s + v.z * c);
        }

        /// Heading of a direction in degrees about up (+Z = 0, +X = 90, -X = -90, -Z = 180).
        public static float YawDeg(Vector3 dir)
        {
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        /// D-016 / D-029: the foot's own forward on the ground from the foot, toe and shin (foot - knee) directions.
        /// Taken from the foot's LATERAL axis (shin x foot->toe), which stays put when the foot pitches while running - the flattened
        /// foot->toe direction flipped 180 deg on every step (TEST-005). A foot never points backwards (dot with the player's forward
        /// >= 0) and is clamped to +-MaxFootTurnDeg. toeDir zero -> the player's forward.
        public static Vector3 FootForward(Vector3 toeDir, Vector3 shin, Vector3 playerForward)
        {
            var pf = Flat(playerForward, Vector3.forward);
            // Stryker disable once Equality : < vs <= differ only at exactly 1e-10, both answers are fine for a zero-length toe
            if (toeDir.sqrMagnitude < 1e-10f) return pf;
            var side = Vector3.Cross(shin, toeDir);
            // Stryker disable once Equality : > vs >= differ only at exactly 1e-10
            var f = side.sqrMagnitude > 1e-10f ? Flat(Vector3.Cross(side, Vector3.up), pf) : Flat(toeDir, pf);
            // Stryker disable once Equality : dot exactly 0 = foot exactly sideways, ambiguous by design (either side is right)
            if (Vector3.Dot(f, pf) < 0f) f = -f;
            float turn = Vector3.SignedAngle(pf, f, Vector3.up);
            // Stryker disable once Equality : at exactly the limit the clamp returns the same direction
            if (Mathf.Abs(turn) > MaxFootTurnDeg) f = RotateY(pf, Mathf.Sign(turn) * MaxFootTurnDeg);
            return f;
        }

        /// World position in the FOOT's frame: `forward` along the foot, `side` outward from the body (mirrored per foot: the left foot's
        /// outward is -right), height = foot height + up + lift (lift = how far the model's pivot sits above its lowest point).
        public static Vector3 FootFramePosition(Vector3 footPos, Vector3 footForward, bool leftFoot, float forward, float side, float up, float lift)
        {
            var fwd = Flat(footForward, Vector3.forward);
            var right = Vector3.Cross(Vector3.up, fwd);
            float outward = leftFoot ? -1f : 1f;
            var p = footPos + fwd * forward + right * (outward * side);
            p.y = footPos.y + up + lift;
            return p;
        }

        /// The upright figure's heading (deg about up) when it faces footForward and is turned by yawDeg on top.
        public static float FigureYawDeg(Vector3 footForward, float yawDeg)
        {
            return YawDeg(Flat(footForward, Vector3.forward)) + yawDeg;
        }

        /// How far (m) a vertex lies in front of the toe joint along the foot.
        public static float ToeAlong(Vector3 vertexWorld, Vector3 toePos, Vector3 footForward)
        {
            return Vector3.Dot(vertexWorld - toePos, footForward);
        }

        /// The toe bone's skin weight for a vertex `along` metres in front of the toe joint: 0 behind the blend, 1 in front of it, smooth
        /// (SmoothStep) across +-ToeBlendWidth, 0.5 at the joint. The foot bone gets 1 - this.
        public static float ToeWeight(float along)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-ToeBlendWidth, ToeBlendWidth, along));
        }

        /// Factor for the log's scale so its longest mesh axis becomes lengthRatio * height metres (1 when the mesh has no size).
        public static float LogScaleFactor(Vector3 meshSize, float lossyScale, float lengthRatio, float height)
        {
            float worldLong = Mathf.Max(meshSize.x, Mathf.Max(meshSize.y, meshSize.z)) * lossyScale;
            // Stryker disable once Equality : > vs >= differ only at exactly 1e-5 m
            return worldLong > 0.00001f ? lengthRatio * height / worldLong : 1f;
        }

        /// The fitted log offset (in figure heights) plus the in-game fine tune in metres (D-019); height 0 is guarded.
        public static Vector3 LogOffsetRatio(Vector3 offsetRatio, Vector3 extraOffset, float height)
        {
            return offsetRatio + extraOffset / Mathf.Max(0.0001f, height);
        }

        /// D-029 flip detector step: degrees the figure turned since the last frame (0 on the first frame, last = zero).
        public static float FlipStep(Vector3 last, Vector3 now)
        {
            // Stryker disable once Conditional : Vector3.Angle with a zero vector is 0 too; the check documents the first frame
            return last == Vector3.zero ? 0f : Vector3.Angle(last, now);
        }

        /// Whether one frame's turn counts as a jump (strictly more than JumpDeg).
        public static bool IsJump(float stepDeg)
        {
            return stepDeg > JumpDeg;
        }

        /// The dropped item's pair: each slipper's sideways offset (m) from the middle so two slippers `width` wide leave `gap` between.
        public static float PairOffset(float width, float gap)
        {
            return width * 0.5f + gap * 0.5f;
        }

        /// Corner i (0..7) of a box: bit 0 = x max, bit 1 = y max, bit 2 = z max.
        public static Vector3 BoxCorner(Bounds b, int i)
        {
            return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
        }
    }
}
