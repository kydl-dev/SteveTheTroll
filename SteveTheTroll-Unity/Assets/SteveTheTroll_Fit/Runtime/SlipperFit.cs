using System.Collections.Generic;
using UnityEngine;

namespace SteveTheTroll.Fit
{
    /// <summary>
    /// D-025: the plush slipper on one foot, built at runtime and skinned to the PLAYER's own foot + toe bones (shared by the Unity fit scene
    /// and the mod). Parts (upper / rim / sole) come from the prefab "SlipperParts" (MeshFilter + MeshRenderer each). Every vertex gets the
    /// toe bone's weight by how far it lies in front of the toe joint along the foot (smooth over BlendWidth), the rest goes to the foot
    /// bone; bindposes are taken once, with the slipper placed in the foot frame - from then on the bones move it (walk, toe bend).
    /// Mesh axes (FBX from Blender, see slipper_export.py): up = +Y, origin on the ground under the middle, TOE = -Z (checked in Unity).
    /// </summary>
    public static class SlipperFit
    {
        public static readonly Quaternion MeshToFoot = Quaternion.Euler(0f, 180f, 0f);     // mesh toe -Z -> foot forward +Z
        public const float BlendWidth = FitGeometry.ToeBlendWidth;                          // m, foot -> toe weight blend around the toe joint
        public const string RootName = "SteveTheTroll_Slipper";

        public sealed class Part
        {
            public string Name;
            public SkinnedMeshRenderer Renderer;
            public int ToeWeighted;
        }

        /// Builds the slipper next to `parent` (the player root in game). Expected outcome: one skinned part per prefab child, all bound.
        public static List<Part> Build(GameObject partsPrefab, Transform parent, Transform foot, Transform toe, Vector3 playerForward, bool left,
            float forward, float side, float up, float yawDeg, float scale, out Transform root)
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
            root.localScale = Vector3.one * scale;                                          // the mesh is left/right symmetric: no mirroring
            foreach (var mf in partsPrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null) continue;
                parts.Add(BuildPart(root, mf, mr, foot, toe, fwd));
            }
            TrollFitMath.Log("SlipperFit: " + root.name + " at " + pos.ToString("F3") + ", foot forward " + fwd.ToString("F2") + ", scale " + scale.ToString("F2")
                + ", " + parts.Count + " part(s): " + string.Join(", ", parts.ConvertAll(p => p.Name + " (" + p.ToeWeighted + " toe verts)").ToArray()));
            return parts;
        }

        static Part BuildPart(Transform root, MeshFilter mf, MeshRenderer mr, Transform foot, Transform toe, Vector3 fwd)
        {
            var go = new GameObject(mf.name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = mf.transform.localPosition;
            go.transform.localRotation = mf.transform.localRotation;
            go.transform.localScale = mf.transform.localScale;
            var mesh = Object.Instantiate(mf.sharedMesh);
            mesh.name = mf.sharedMesh.name + "_skinned";
            var verts = mesh.vertices;
            var weights = new BoneWeight[verts.Length];
            var toPart = go.transform.localToWorldMatrix;
            int toeVerts = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                float w = 0f;
                if (toe != null) w = FitGeometry.ToeWeight(FitGeometry.ToeAlong(toPart.MultiplyPoint3x4(verts[i]), toe.position, fwd));
                if (w > 0.5f) toeVerts++;
                weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f - w, boneIndex1 = 1, weight1 = w };
            }
            var bones = new[] { foot, toe != null ? toe : foot };
            mesh.boneWeights = weights;
            mesh.bindposes = new[] { bones[0].worldToLocalMatrix * toPart, bones[1].worldToLocalMatrix * toPart };
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = foot;
            smr.sharedMaterials = mr.sharedMaterials;
            smr.updateWhenOffscreen = true;
            return new Part { Name = mf.name, Renderer = smr, ToeWeighted = toeVerts };
        }
    }
}
