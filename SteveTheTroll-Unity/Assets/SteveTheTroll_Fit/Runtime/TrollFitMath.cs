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
        };

        /// Player forward flattened to the ground; falls back to world +Z when the player looks straight up / down.
        public static Vector3 FlatForward(Vector3 playerForward)
        {
            var f = Vector3.ProjectOnPlane(playerForward, Vector3.up);
            return f.sqrMagnitude < 0.0001f ? Vector3.forward : f.normalized;
        }

        /// World pose of the figure root. Expected outcome: rotation is upright (its up equals world up within 0.1 deg).
        public static void Place(Vector3 footPos, Vector3 playerForward, bool leftFoot, float forward, float side, float up, float yawDeg,
            float bottomBelowPivot, out Vector3 position, out Quaternion rotation)
        {
            var fwd = FlatForward(playerForward);
            var right = Vector3.Cross(Vector3.up, fwd);
            float outward = leftFoot ? -1f : 1f;
            position = footPos + fwd * forward + right * (outward * side);
            position.y = footPos.y + up + bottomBelowPivot;
            rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, yawDeg, 0f);
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
            if (logRoot == null || logMesh == null || logMesh.sharedMesh == null || figure == null || hand == null) { Warn("TrollFitMath: PlaceLog missing a part"); return 0f; }
            logRoot.rotation = figure.rotation * Quaternion.Euler(euler);
            var size = logMesh.sharedMesh.bounds.size;
            float meshLong = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float worldLong = meshLong * logMesh.transform.lossyScale.x;      // uniform scales only (checked by the caller's log line)
            if (worldLong > 0.00001f) logRoot.localScale *= lengthRatio * height / worldLong;
            var centre = logMesh.transform.TransformPoint(logMesh.sharedMesh.bounds.center);
            var want = hand.position + figure.rotation * (offsetRatio * height);
            logRoot.position += want - centre;
            float length = meshLong * logMesh.transform.lossyScale.x;
            Log("TrollFitMath: PlaceLog length " + length.ToString("F3") + " m (want " + (lengthRatio * height).ToString("F3") + "), centre " + want.ToString("F3") + ", lossy " + logMesh.transform.lossyScale.ToString("F4"));
            return length;
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
