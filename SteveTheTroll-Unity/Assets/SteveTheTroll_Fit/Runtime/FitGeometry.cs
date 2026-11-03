using UnityEngine;

namespace SteveTheTroll.Fit;

/// <summary>
///     The pure maths of the fit (troll placement, foot direction, log size, slipper skin weights, flip detector,
///     dropped-pair layout).
///     Only MANAGED Unity value types here (Vector3, Bounds, Mathf - no Quaternion / Transform / Mesh, which call into the
///     engine), so the
///     plain .NET unit tests run it and Stryker can mutate it (_agent/testrules/quality.json). Shared by the mod and the
///     Unity fit scene;
///     TrollFitMath / SlipperFit feed it engine data. That the engine-side wrappers give the same answers is checked by
///     the Unity EditMode
///     tests (Assets/SteveTheTroll_Fit/Tests).
/// </summary>
public static class FitGeometry
{
    /// Feet never turn further than this from the player's forward (D-029 clamp; the idle stance measured 16 / 64 deg).
    public const float MaxFootTurnDeg = 75f;

    /// D-029 flip detector: a figure turning more than this between two frames counts as a jump (CHECK in TrollFigure).
    public const float JumpDeg = 45f;

    /// m, foot -> toe weight blend around the toe joint (D-025).
    public const float ToeBlendWidth = 0.03f;

    private const float MinSqr = 0.0001f;

    // ---------------- skin transfer (TEST-007: the slipper bends like the player's own foot skin) ----------------

    /// A source vertex counts 1 / (distance + this) when blending weights (m): close vertices dominate, a vertex AT the point cannot divide by 0.
    public const float SkinBlendEps = 0.002f;

    /// A direction flattened to the ground and normalised; `fallback` (flattened) when it is (nearly) vertical or zero, then +Z.
    public static Vector3 Flat(Vector3 dir, Vector3 fallback)
    {
        var f = new Vector3(dir.x, 0f, dir.z);
        if (f.sqrMagnitude >= MinSqr)
        {
            return f.normalized;
        }

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
        Vector3 pf = Flat(playerForward, Vector3.forward);
        // Stryker disable once Equality : < vs <= differ only at exactly 1e-10, both answers are fine for a zero-length toe
        if (toeDir.sqrMagnitude < 1e-10f)
        {
            return pf;
        }

        var side = Vector3.Cross(shin, toeDir);
        // Stryker disable once Equality : > vs >= differ only at exactly 1e-10
        Vector3 f = side.sqrMagnitude > 1e-10f ? Flat(Vector3.Cross(side, Vector3.up), pf) : Flat(toeDir, pf);
        // Stryker disable once Equality : dot exactly 0 = foot exactly sideways, ambiguous by design (either side is right)
        if (Vector3.Dot(f, pf) < 0f)
        {
            f = -f;
        }

        float turn = Vector3.SignedAngle(pf, f, Vector3.up);
        // Stryker disable once Equality : at exactly the limit the clamp returns the same direction
        if (Mathf.Abs(turn) > MaxFootTurnDeg)
        {
            f = RotateY(pf, Mathf.Sign(turn) * MaxFootTurnDeg);
        }

        return f;
    }

    /// World position in the FOOT's frame: `forward` along the foot, `side` outward from the body (mirrored per foot: the left foot's
    /// outward is -right), height = foot height + up + lift (lift = how far the model's pivot sits above its lowest point).
    public static Vector3 FootFramePosition(Vector3 footPos, Vector3 footForward, bool leftFoot, float forward,
        float side, float up, float lift)
    {
        Vector3 fwd = Flat(footForward, Vector3.forward);
        var right = Vector3.Cross(Vector3.up, fwd);
        float outward = leftFoot ? -1f : 1f;
        Vector3 p = footPos + fwd * forward + right * (outward * side);
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

    /// The `k` points of `pts` nearest to p, closest first: their indices in idx and distances (m) in dist (both at least k long).
    /// Returns how many were found (k, or fewer when pts is shorter). On equal distances the earlier point comes first.
    public static int NearestK(Vector3 p, Vector3[] pts, int k, int[] idx, float[] dist)
    {
        int n = 0;
        if (pts == null || k <= 0)
        {
            return 0;
        }

        for (int i = 0; i < pts.Length; i++)
        {
            float d = (pts[i] - p).sqrMagnitude;
            if (n == k && d >= dist[n - 1])
            {
                continue;
            }

            int j = n < k ? n++ : n - 1;
            while (j > 0 && dist[j - 1] > d)
            {
                dist[j] = dist[j - 1];
                idx[j] = idx[j - 1];
                j--;
            }

            dist[j] = d;
            idx[j] = i;
        }

        for (int i = 0; i < n; i++)
        {
            dist[i] = Mathf.Sqrt(dist[i]);
        }

        return n;
    }

    /// Bone weights for a point from its nearest source vertices (idx / dist from NearestK): each vertex counts 1 / (distance + SkinBlendEps),
    /// its bone weights are summed per bone, the strongest bones (up to 4 and bones.Length) are kept, strongest first, normalised to 1.
    /// Returns how many bones were written (0 when no vertex had a weight). On equal weights the bone met first comes first.
    public static int BlendWeights(BoneWeight[] src, int[] idx, float[] dist, int count, int[] bones, float[] weights)
    {
        int[] ids = new int[count * 4];
        float[] sums = new float[count * 4];
        int m = 0;
        for (int v = 0; v < count; v++)
        {
            BoneWeight bw = src[idx[v]];
            float k = 1f / (dist[v] + SkinBlendEps);
            m = AddWeight(ids, sums, m, bw.boneIndex0, bw.weight0 * k);
            m = AddWeight(ids, sums, m, bw.boneIndex1, bw.weight1 * k);
            m = AddWeight(ids, sums, m, bw.boneIndex2, bw.weight2 * k);
            m = AddWeight(ids, sums, m, bw.boneIndex3, bw.weight3 * k);
        }

        int n = 0;
        float total = 0f;
        int max = Mathf.Min(4, bones.Length);
        while (n < max)
        {
            int best = -1;
            for (int i = 0; i < m; i++)
            {
                if (sums[i] > 0f && (best < 0 || sums[i] > sums[best]))
                {
                    best = i;
                }
            }

            if (best < 0)
            {
                break;
            }

            bones[n] = ids[best];
            weights[n] = sums[best];
            total += sums[best];
            sums[best] = 0f;
            n++;
        }

        for (int i = 0; i < n; i++)
        {
            weights[i] /= total;
        }

        return n;
    }

    private static int AddWeight(int[] ids, float[] sums, int m, int bone, float w)
    {
        // Stryker disable once Equality : a 0 weight added as a bone keeps sum 0 and is never picked (sums > 0 only)
        if (w <= 0f)
        {
            return m;
        }

        for (int i = 0; i < m; i++)
        {
            if (ids[i] == bone)
            {
                sums[i] += w;
                return m;
            }
        }

        ids[m] = bone;
        sums[m] = w;
        return m + 1;
    }

    /// The bone index with the largest weight (the first one on a tie).
    public static int StrongestBone(BoneWeight w)
    {
        int b = w.boneIndex0;
        float best = w.weight0;
        if (w.weight1 > best)
        {
            b = w.boneIndex1;
            best = w.weight1;
        }

        if (w.weight2 > best)
        {
            b = w.boneIndex2;
            best = w.weight2;
        }

        if (w.weight3 > best)
        {
            b = w.boneIndex3;
        }

        return b;
    }

    /// Forward (a -> b) and up (forward x (a -> c)) of the frame three points span: a = origin, b ahead of it, c to its right.
    /// False (and +Z / +Y) when the points are (nearly) on one line.
    public static bool FrameAxes(Vector3 a, Vector3 b, Vector3 c, out Vector3 forward, out Vector3 up)
    {
        forward = b - a;
        up = Vector3.Cross(forward, c - a);
        // Stryker disable once Equality : < vs <= differ only at exactly 1e-12, which no float input reaches
        if (forward.sqrMagnitude < 1e-12f || up.sqrMagnitude < 1e-12f)
        {
            forward = Vector3.forward;
            up = Vector3.up;
            return false;
        }

        forward.Normalize();
        up.Normalize();
        return true;
    }

    /// The glue frame to use (bug 2026-10-30: the frame of three skin points 4 cm apart can go wrong and hang the troll under the slipper):
    /// the skin frame while it stays within maxDegrees of the frame the foot bone predicts, otherwise the foot's.
    public static Quaternion SafeFrame(Quaternion skinFrame, Quaternion footFrame, float maxDegrees, out bool usedFoot)
    {
        usedFoot = Quaternion.Angle(skinFrame, footFrame) > maxDegrees;
        return usedFoot ? footFrame : skinFrame;
    }

    /// D-146: whether a troll that stays tilted should be placed and glued again. Only when it stayed tilted long enough, the player stands still on the ground
    /// and is NOT settling in (spawn / teleport / bed / emote / sitting): then the foot itself is tilted, so a new glue cannot help (test run 235053: tilted
    /// 72 deg twice right after login, the second glue changed nothing, 4 s later the tilt was 4 - 7 deg).
    public static bool ShouldRebind(float tiltedSeconds, float afterSeconds, bool grounded, float speed,
        float stillSpeed, bool settling)
    {
        if (settling || !grounded)
        {
            return false;
        }

        if (tiltedSeconds < afterSeconds)
        {
            return false;
        }

        return speed <= stillSpeed;
    }

    /// D-147: the troll's facing for a socket: the socket's forward flattened onto the plane perpendicular to the player's up (so the troll stands upright whatever the foot does);
    /// `fallback` (also flattened) when the socket points straight along up. Returns a unit horizontal forward (the caller builds the rotation: LookRotation is an engine call).
    public static Vector3 UprightForward(Vector3 socketForward, Vector3 up, Vector3 fallback)
    {
        up.Normalize();
        var f = Vector3.ProjectOnPlane(socketForward, up);
        if (f.sqrMagnitude < 1e-6f)
        {
            f = Vector3.ProjectOnPlane(fallback, up);
        }

        if (f.sqrMagnitude < 1e-6f)
        {
            f = Vector3.ProjectOnPlane(Vector3.forward, up);
        }

        if (f.sqrMagnitude < 1e-6f)
        {
            f = Vector3.ProjectOnPlane(Vector3.right, up);
        }

        return f.normalized;
    }

    /// D-157: turns the heading `prev` towards `target` (both unit, flat against `up`) by at most `maxDegrees`. A foot that pitches steeply (toe up / down in a stride) makes the flattened
    /// socket direction swing fast near the vertical (test 095924: 61 deg in one frame); the troll follows it with a turn-rate limit instead. Expected outcome: `target` when it is
    /// within maxDegrees of `prev`, else `prev` turned maxDegrees the short way; `target` for a zero `prev` (first frame).
    public static Vector3 StepHeading(Vector3 prev, Vector3 target, float maxDegrees, Vector3 up)
    {
        if (prev.sqrMagnitude < 1e-6f)
        {
            return target;
        }

        float angle = Vector3.SignedAngle(prev, target, up);
        if (Mathf.Abs(angle) <= maxDegrees)
        {
            return target;
        }

        // Rodrigues' rotation about `up` (no engine call, so it is unit-testable)
        float r = Mathf.Sign(angle) * maxDegrees * Mathf.Deg2Rad, c = Mathf.Cos(r), sn = Mathf.Sin(r);
        Vector3 axis = up.normalized;
        return prev * c + Vector3.Cross(axis, prev) * sn + axis * (Vector3.Dot(axis, prev) * (1f - c));
    }

    /// D-159 (user: slipper trolls went under the slipper while running): the troll's up axis = the socket's up (it turns with the foot, so the troll leans with the slipper instead of
    /// staying upright at a moving anchor), but never more than `maxDegrees` away from the player's up. Expected outcome: a unit vector; `playerUp` when socketUp is zero.
    public static Vector3 LeanUp(Vector3 playerUp, Vector3 socketUp, float maxDegrees)
    {
        playerUp.Normalize();
        if (socketUp.sqrMagnitude < 1e-6f)
        {
            return playerUp;
        }

        socketUp.Normalize();
        float cos = Mathf.Clamp(Vector3.Dot(playerUp, socketUp), -1f, 1f);
        float angle = Mathf.Acos(cos) * Mathf.Rad2Deg;
        if (angle <= maxDegrees)
        {
            return socketUp;
        }

        var axis = Vector3.Cross(playerUp, socketUp);
        if (axis.sqrMagnitude < 1e-8f)
        {
            return playerUp;
        }

        axis.Normalize();
        float r = maxDegrees * Mathf.Deg2Rad, c = Mathf.Cos(r), sn = Mathf.Sin(r);
        return (playerUp * c + Vector3.Cross(axis, playerUp) * sn + axis * (Vector3.Dot(axis, playerUp) * (1f - c)))
            .normalized;
    }

    /// Whether two points are at most maxMeters apart (D-137: the glued troll against where the foot bone puts it).
    public static bool WithinMeters(Vector3 a, Vector3 b, float maxMeters)
    {
        return (a - b).sqrMagnitude <= maxMeters * maxMeters;
    }

    /// Corner i (0..7) of a box: bit 0 = x max, bit 1 = y max, bit 2 = z max.
    public static Vector3 BoxCorner(Bounds b, int i)
    {
        return b.center + Vector3.Scale(b.extents,
            new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
    }
}
