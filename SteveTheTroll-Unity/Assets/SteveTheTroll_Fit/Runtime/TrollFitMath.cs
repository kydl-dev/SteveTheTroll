using System;
using UnityEngine;

namespace SteveTheTroll.Fit
{
    /// <summary>
    /// The ONE place the mini troll's pose, size and placement are computed (D-014). Compiled into the Unity fit scene AND into the mod
    /// (SteveTheTroll.csproj links Assets/SteveTheTroll_Fit/Runtime/*.cs), so what is fitted in Unity is exactly what the game shows.
    /// Only UnityEngine types here (no game, no Kydl.Common): the caller plugs its logger into Log / Warn.
    /// Frame: the figure stands upright, faces the player's forward direction (flattened), its lowest vertex sits `up` metres above
    /// the foot bone; `forward` / `side` move it in that frame (side + = outward from the body, mirrored per foot).
    /// </summary>
    public static class TrollFitMath
    {
        public static Action<string> Log = s => { };
        public static Action<string> Warn = s => { };

        /// Bones the fit poses (the troll's own names; the player has bones with some of the same names).
        public static readonly string[] PosedBones =
        {
            "Root", "Spine0", "Spine1", "Spine2", "Head",
            "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot",
            "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand",
            // D-017: the right hand grips the log
            "RightHandIndex1", "RightHandIndex2", "RightHandIndex3", "RightHandMiddle1", "RightHandMiddle2", "RightHandMiddle3",
            "RightHandRing1", "RightHandRing2", "RightHandRing3", "RightHandThumb1", "RightHandThumb2", "RightHandThumb3",
            // D-024: the left hand closed (user wants to see a fist)
            "LeftHandIndex1", "LeftHandIndex2", "LeftHandIndex3", "LeftHandMiddle1", "LeftHandMiddle2", "LeftHandMiddle3",
            "LeftHandRing1", "LeftHandRing2", "LeftHandRing3", "LeftHandThumb1", "LeftHandThumb2", "LeftHandThumb3",
        };

        /// The player's toe bone for a foot bone name (LeftFoot -> LeftToeBase).
        public static string ToeOf(string footBone) { return footBone.Replace("Foot", "ToeBase"); }

        /// A direction flattened to the ground; `fallback` (flattened) when it is (nearly) vertical or zero.
        public static Vector3 Flat(Vector3 dir, Vector3 fallback) { return FitGeometry.Flat(dir, fallback); }

        /// Feet never turn further than this from the player's forward (D-029 clamp; the idle stance measured 16 / 64 deg).
        public const float MaxFootTurnDeg = FitGeometry.MaxFootTurnDeg;

        /// D-016 / D-029: the foot's own forward on the ground. Taken from the foot's LATERAL axis (shin x foot->toe), which stays put when the
        /// foot pitches while running - the flattened foot->toe direction flipped 180 deg on every step (TEST-005: "foot turned 178 deg",
        /// trolls looked backwards). Sign: a foot never points backwards (dot with the player's forward > 0); clamped to +-MaxFootTurnDeg.
        public static Vector3 FootForward(Transform foot, Transform toe, Vector3 playerForward)
        {
            if (foot == null || toe == null) return Flat(playerForward, Vector3.forward);
            var shin = foot.parent != null ? foot.position - foot.parent.position : Vector3.down;
            return FitGeometry.FootForward(toe.position - foot.position, shin, playerForward);
        }

        /// World pose of the figure root in the FOOT's frame (footForward from FootForward). Expected outcome: rotation is upright.
        public static void Place(Vector3 footPos, Vector3 footForward, bool leftFoot, float forward, float side, float up, float yawDeg,
            float bottomBelowPivot, out Vector3 position, out Quaternion rotation)
        {
            position = FitGeometry.FootFramePosition(footPos, footForward, leftFoot, forward, side, up, bottomBelowPivot);
            rotation = Quaternion.Euler(0f, FitGeometry.FigureYawDeg(footForward, yawDeg), 0f);
        }

        /// bone.localRotation = rest * delta * extra (extra = in-game fine tune, identity when none).
        /// Expected outcome: returns how many bones were posed (= bones.Length when every bone and rest exist).
        public static int ApplyPose(Transform[] bones, Quaternion[] rest, Quaternion[] delta, Quaternion[] extra)
        {
            int posed = 0;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                var q = rest[i] * delta[i];
                if (extra != null && i < extra.Length) q = q * extra[i];
                bones[i].localRotation = q;
                posed++;
            }
            return posed;
        }

        /// World Y range of the posed, baked body. Expected outcome: returns whether the bake had vertices.
        public static bool MeasureY(SkinnedMeshRenderer body, Mesh scratch, out float minY, out float maxY)
        {
            minY = float.MaxValue; maxY = float.MinValue;
            if (body == null || scratch == null) { Warn("TrollFitMath: MeasureY without " + (body == null ? "body" : "scratch mesh")); return false; }
            // BakeMesh (useScale false) returns vertices at WORLD size, only un-rotated / un-translated into the renderer's frame
            // (measured in Unity 6000.0.75: troll Body with lossyScale 130 baked 11 m wide). So: position + rotation, NO scale.
            body.BakeMesh(scratch);
            var m = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
            var verts = scratch.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                float y = m.MultiplyPoint3x4(verts[i]).y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            Log("TrollFitMath: MeasureY " + body.name + " " + verts.Length + " verts, y " + minY.ToString("F3") + " .. " + maxY.ToString("F3") + " m");
            return verts.Length > 0;
        }

        /// Puts the troll's log (a child of the figure root, NOT of the hand: the troll's bones carry ~x130 scales, so the item's own
        /// attach offsets give a log 100x too long - seen in the Unity fit scene 2026-10-27) next to the posed right hand:
        /// longest mesh axis = lengthRatio * height (world m), mesh centre = hand + figure-frame offset (offsetRatio * height),
        /// rotation = figure rotation * euler. Expected outcome: returns the log's world length (m) after scaling.
        public static float PlaceLog(Transform logRoot, MeshFilter logMesh, Transform figure, Transform hand, float height,
            float lengthRatio, Vector3 offsetRatio, Vector3 euler)
        {
            return PlaceLog(logRoot, logMesh, figure, hand, height, lengthRatio, offsetRatio, euler, Vector3.zero, Quaternion.identity);
        }

        /// Same, plus the in-game fine tune (D-019): extraOffset in metres in the figure frame (x right, y up, z forward) and
        /// extraTurn applied in the figure frame on top of the fitted rotation (yaw about up, pitch about right, roll about forward).
        public static float PlaceLog(Transform logRoot, MeshFilter logMesh, Transform figure, Transform hand, float height,
            float lengthRatio, Vector3 offsetRatio, Vector3 euler, Vector3 extraOffset, Quaternion extraTurn)
        {
            if (logRoot == null || logMesh == null || logMesh.sharedMesh == null || figure == null || hand == null) { Warn("TrollFitMath: PlaceLog missing a part"); return 0f; }
            offsetRatio = FitGeometry.LogOffsetRatio(offsetRatio, extraOffset, height);
            logRoot.rotation = figure.rotation * extraTurn * Quaternion.Euler(euler);
            var size = logMesh.sharedMesh.bounds.size;
            float meshLong = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            logRoot.localScale *= FitGeometry.LogScaleFactor(size, logMesh.transform.lossyScale.x, lengthRatio, height);   // uniform scales only
            var centre = logMesh.transform.TransformPoint(logMesh.sharedMesh.bounds.center);
            var want = hand.position + figure.rotation * (offsetRatio * height);
            logRoot.position += want - centre;
            float length = meshLong * logMesh.transform.lossyScale.x;
            // Runs every frame in game: no world position in the text, so the shared 2.5 s repeat filter throttles it (session 145215 spam).
            Log("TrollFitMath: PlaceLog length " + length.ToString("F3") + " m (want " + (lengthRatio * height).ToString("F3") + "), lossy " + logMesh.transform.lossyScale.ToString("F4"));
            return length;
        }

        /// Extra log turn in the figure frame from yaw / pitch / roll degrees (yaw about up, then pitch about right, then roll about the log).
        public static Quaternion LogTurn(float yawDeg, float pitchDeg, float rollDeg)
        {
            return Quaternion.AngleAxis(yawDeg, Vector3.up) * Quaternion.AngleAxis(pitchDeg, Vector3.right) * Quaternion.AngleAxis(rollDeg, Vector3.forward);
        }

        /// The right hand's knuckle line (index root -> ring root) in world space; the axis fingers curl around. Zero when a bone is missing.
        public static Vector3 KnuckleLine(Transform figure)
        {
            var i = FindDeep(figure, "RightHandIndex1");
            var r = FindDeep(figure, "RightHandRing1");
            return i != null && r != null ? (r.position - i.position).normalized : Vector3.zero;
        }

        /// The SkinnedMeshRenderer with the most vertices (the troll's Body, not its Hair).
        public static SkinnedMeshRenderer FindBody(Transform root)
        {
            SkinnedMeshRenderer best = null;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null && (best == null || smr.sharedMesh.vertexCount > best.sharedMesh.vertexCount)) best = smr;
            return best;
        }

        /// Depth-first search by exact name. Expected outcome: the transform named `name`, or null.
        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var t = FindDeep(root.GetChild(i), name);
                if (t != null) return t;
            }
            return null;
        }
    }
}
