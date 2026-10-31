using System.Collections.Generic;
using UnityEngine;

namespace SteveTheTroll.Fit
{
    /// <summary>
    /// D-025: the plush slipper on one foot, built at runtime and skinned to the PLAYER's own bones (shared by the Unity fit scene and the
    /// mod). Parts (upper / rim / sole) come from the prefab "SlipperParts" (MeshFilter + MeshRenderer each). Weights: with a SkinTransfer
    /// (TEST-007) every vertex copies the blended weights of the player's nearest foot / toe / shin skin vertices, so the slipper bends
    /// exactly like the foot inside it; without one (Unity fit scene) the toe bone gets a weight by how far the vertex lies in front of the
    /// toe joint (smooth over BlendWidth), the rest the foot bone. Bindposes are taken once, with the slipper placed in the foot frame.
    /// Mesh axes (FBX from Blender, see slipper_export.py): up = +Y, origin on the ground under the middle, TOE = -Z (checked in Unity).
    /// </summary>
    public static class SlipperFit
    {
        public static readonly Quaternion MeshToFoot = Quaternion.Euler(0f, 180f, 0f);     // mesh toe -Z -> foot forward +Z
        public const float BlendWidth = FitGeometry.ToeBlendWidth;                          // m, foot -> toe weight blend around the toe joint
        public const string RootName = "SteveTheTroll_Slipper";
        public const string SocketName = "TrollSocket";                                    // D-147: the prefab child that says where and which way the mini troll stands

        public sealed class Part
        {
            public string Name;
            public SkinnedMeshRenderer Renderer;
            public int ToeWeighted;
        }

        /// Builds the slipper next to `parent` with foot + toe weights (the Unity fit scene). See the full overload.
        public static List<Part> Build(GameObject partsPrefab, Transform parent, Transform foot, Transform toe, Vector3 playerForward, bool left,
            float forward, float side, float up, float yawDeg, float scale, out Transform root)
        {
            return Build(partsPrefab, parent, foot, toe, playerForward, left, forward, side, up, yawDeg, scale, 1f, null, out root);
        }

        /// Builds the slipper next to `parent` (the player root in game); width scales it across the foot only. skin (refreshed by the
        /// caller in the current pose) gives the weights; null or empty = foot + toe weights.
        /// Expected outcome: one skinned part per prefab child, all bound.
        public static List<Part> Build(GameObject partsPrefab, Transform parent, Transform foot, Transform toe, Vector3 playerForward, bool left,
            float forward, float side, float up, float yawDeg, float scale, float width, SkinTransfer skin, out Transform root)
        {
            var parts = new List<Part>();
            root = null;
            if (partsPrefab == null || foot == null) { TrollFitMath.Warn("SlipperFit: Build without " + (partsPrefab == null ? "parts prefab" : "foot")); return parts; }
            var go = new GameObject(RootName + (left ? "_L" : "_R"));
            root = go.transform;
            root.SetParent(parent, false);
            var fwd = TrollFitMath.FootForward(foot, toe, playerForward);
            var pos = FitGeometry.FootFramePosition(foot.position, fwd, left, forward, side, up, 0f);
            root.SetPositionAndRotation(pos, Quaternion.Euler(0f, FitGeometry.FigureYawDeg(fwd, yawDeg), 0f) * MeshToFoot);
            root.localScale = new Vector3(scale * width, scale, scale);                    // the mesh is left/right symmetric: no mirroring
            bool fromSkin = skin != null && skin.Count > 0;
            foreach (var mf in partsPrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null) continue;
                parts.Add(BuildPart(root, mf, mr, foot, toe, fwd, fromSkin ? skin : null));
            }
            AddSocket(partsPrefab, root);
            TrollFitMath.Log("SlipperFit: " + root.name + " at " + pos.ToString("F3") + ", foot forward " + fwd.ToString("F2") + ", scale " + scale.ToString("F2") + " x width " + width.ToString("F2")
                + ", weights from " + (fromSkin ? "the player's skin (" + skin.Count + " source verts)" : "foot + toe") + ", " + parts.Count + " part(s): "
                + string.Join(", ", parts.ConvertAll(p => p.Name + " (" + p.ToeWeighted + " toe verts, " + p.Renderer.bones.Length + " bones)").ToArray()));
            return parts;
        }

        /// D-147: copies the prefab's TrollSocket (position + rotation relative to the prefab root) under the slipper root, so it carries the slipper scale / width.
        /// Its +Z is the way the troll faces. Missing socket = Warn (the caller then has no anchor).
        static void AddSocket(GameObject partsPrefab, Transform root)
        {
            var src = FindSocket(partsPrefab.transform);
            if (src == null) { TrollFitMath.Warn("SlipperFit: prefab '" + partsPrefab.name + "' has no '" + SocketName + "' child - the troll has no anchor (rebuild the bundle)"); return; }
            var s = new GameObject(SocketName).transform;
            s.SetParent(root, false);
            s.localPosition = src.localPosition;
            s.localRotation = src.localRotation;
            TrollFitMath.Log("SlipperFit: " + root.name + " socket at " + s.position.ToString("F3") + ", faces " + s.forward.ToString("F2"));
        }

        static Transform FindSocket(Transform t)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == SocketName) return c;
            return null;
        }

        static Part BuildPart(Transform root, MeshFilter mf, MeshRenderer mr, Transform foot, Transform toe, Vector3 fwd, SkinTransfer skin)
        {
            var go = new GameObject(mf.name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = mf.transform.localPosition;
            go.transform.localRotation = mf.transform.localRotation;
            go.transform.localScale = mf.transform.localScale;
            var mesh = Object.Instantiate(mf.sharedMesh);
            mesh.name = mf.sharedMesh.name + "_skinned";
            var verts = mesh.vertices;
            var toPart = go.transform.localToWorldMatrix;
            var bones = new List<Transform> { foot };
            if (skin == null && toe != null) bones.Add(toe);
            var weights = skin != null ? FromSkin(verts, toPart, skin, bones) : FootAndToe(verts, toPart, toe, fwd);
            int toeIndex = toe != null ? bones.IndexOf(toe) : -1;
            int toeVerts = 0;
            for (int i = 0; i < weights.Length; i++) if (toeIndex >= 0 && Weight(weights[i], toeIndex) > 0.5f) toeVerts++;
            var binds = new Matrix4x4[bones.Count];
            for (int b = 0; b < bones.Count; b++) binds[b] = bones[b].worldToLocalMatrix * toPart;
            mesh.boneWeights = weights;
            mesh.bindposes = binds;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones.ToArray();
            smr.rootBone = foot;
            smr.sharedMaterials = mr.sharedMaterials;
            smr.updateWhenOffscreen = true;
            return new Part { Name = mf.name, Renderer = smr, ToeWeighted = toeVerts };
        }

        // D-025 two-bone weights: toe by the distance in front of the toe joint, foot the rest (bones = [foot, toe]).
        static BoneWeight[] FootAndToe(Vector3[] verts, Matrix4x4 toPart, Transform toe, Vector3 fwd)
        {
            var weights = new BoneWeight[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                float w = toe != null ? FitGeometry.ToeWeight(FitGeometry.ToeAlong(toPart.MultiplyPoint3x4(verts[i]), toe.position, fwd)) : 0f;
                weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f - w, boneIndex1 = toe != null ? 1 : 0, weight1 = w };
            }
            return weights;
        }

        // TEST-007: the player's own weights at each vertex; `bones` grows with every player bone used (foot stays index 0).
        static BoneWeight[] FromSkin(Vector3[] verts, Matrix4x4 toPart, SkinTransfer skin, List<Transform> bones)
        {
            var weights = new BoneWeight[verts.Length];
            var b = new Transform[4];
            var w = new float[4];
            var index = new int[4];
            for (int i = 0; i < verts.Length; i++)
            {
                int n = skin.WeightsAt(toPart.MultiplyPoint3x4(verts[i]), b, w);
                if (n == 0) { weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f }; continue; }
                for (int k = 0; k < 4; k++)
                {
                    index[k] = 0;
                    if (k >= n) { w[k] = 0f; continue; }
                    if (b[k] == null) continue;                                     // weight stays, on the foot
                    int at = bones.IndexOf(b[k]);
                    if (at < 0) { bones.Add(b[k]); at = bones.Count - 1; }
                    index[k] = at;
                }
                weights[i] = new BoneWeight
                {
                    boneIndex0 = index[0],
                    weight0 = w[0],
                    boneIndex1 = index[1],
                    weight1 = w[1],
                    boneIndex2 = index[2],
                    weight2 = w[2],
                    boneIndex3 = index[3],
                    weight3 = w[3],
                };
            }
            return weights;
        }

        static float Weight(BoneWeight w, int bone)
        {
            float s = 0f;
            if (w.boneIndex0 == bone) s += w.weight0;
            if (w.boneIndex1 == bone) s += w.weight1;
            if (w.boneIndex2 == bone) s += w.weight2;
            if (w.boneIndex3 == bone) s += w.weight3;
            return s;
        }
    }
}
