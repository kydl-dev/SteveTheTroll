using System.Collections.Generic;
using UnityEngine;

namespace SteveTheTroll.Fit
{
    /// <summary>
    /// TEST-007 fix: the slipper (and the troll glued on it) take their skin weights from the PLAYER's own body mesh, so they bend exactly
    /// like the foot under them - ankle, foot and toes - instead of the two-bone guess that let the foot poke through the sole while running.
    /// Source = the body's vertices in their current pose (BakeMesh, world = TRS(position, rotation, one) * baked - MISTAKES "BakeMesh space")
    /// whose strongest bone is one of the allowed bones (one leg: shin, foot, toe; never the other foot). A point gets the blended weights
    /// of its Neighbours nearest source vertices (FitGeometry.NearestK / BlendWeights). Shared by the mod and the Unity fit scene.
    /// </summary>
    public sealed class SkinTransfer
    {
        public const int Neighbours = 4;

        readonly SkinnedMeshRenderer _body;
        readonly HashSet<Transform> _allowed;
        readonly int[] _idx = new int[Neighbours];
        readonly float[] _dist = new float[Neighbours];
        readonly int[] _boneIdx = new int[4];
        Mesh _bake;
        Vector3[] _pos = new Vector3[0];
        BoneWeight[] _weights = new BoneWeight[0];
        Transform[] _bones = new Transform[0];

        public SkinTransfer(SkinnedMeshRenderer body, IEnumerable<Transform> allowedBones)
        {
            _body = body;
            _allowed = new HashSet<Transform>();
            if (allowedBones != null) foreach (var b in allowedBones) if (b != null) _allowed.Add(b);
        }

        /// Usable source vertices after the last Refresh.
        public int Count { get { return _pos.Length; } }

        /// Bakes the body in its CURRENT pose and keeps the vertices of the allowed bones. Call it right before building / binding.
        /// Expected outcome: returns the number of usable source vertices (0 = no body, no weights or no allowed bone).
        public int Refresh()
        {
            _pos = new Vector3[0];
            if (_body == null || _body.sharedMesh == null || _allowed.Count == 0) { TrollFitMath.Warn("SkinTransfer: no body mesh or no allowed bones - slipper falls back to foot + toe weights"); return 0; }
            var src = _body.sharedMesh.boneWeights;
            _bones = _body.bones;
            if (src.Length == 0 || _bones == null || _bones.Length == 0) { TrollFitMath.Warn("SkinTransfer: body '" + _body.name + "' has no bone weights"); return 0; }
            if (_bake == null) _bake = new Mesh();
            _body.BakeMesh(_bake);
            var baked = _bake.vertices;
            var m = Matrix4x4.TRS(_body.transform.position, _body.transform.rotation, Vector3.one);
            var pos = new List<Vector3>();
            var w = new List<BoneWeight>();
            for (int i = 0; i < baked.Length && i < src.Length; i++)
            {
                int strongest = FitGeometry.StrongestBone(src[i]);
                if (strongest < 0 || strongest >= _bones.Length || !_allowed.Contains(_bones[strongest])) continue;
                pos.Add(m.MultiplyPoint3x4(baked[i]));
                w.Add(src[i]);
            }
            _pos = pos.ToArray();
            _weights = w.ToArray();
            TrollFitMath.Log("SkinTransfer: " + _body.name + " " + baked.Length + " verts, " + _pos.Length + " on " + _allowed.Count + " allowed bone(s)");
            return _pos.Length;
        }

        /// Bones and weights (strongest first, sum 1) for a world point in the pose of the last Refresh. Returns how many (0..4).
        public int WeightsAt(Vector3 world, Transform[] bones, float[] weights)
        {
            int n = FitGeometry.NearestK(world, _pos, Neighbours, _idx, _dist);
            int b = FitGeometry.BlendWeights(_weights, _idx, _dist, n, _boneIdx, weights);
            for (int i = 0; i < b; i++) bones[i] = _boneIdx[i] >= 0 && _boneIdx[i] < _bones.Length ? _bones[_boneIdx[i]] : null;
            return b;
        }

        /// A point that follows the skin like a vertex there would (null when there is no source). Uses the pose of the last Refresh.
        public SkinPoint PointAt(Vector3 world)
        {
            var bones = new Transform[4];
            var weights = new float[4];
            int n = WeightsAt(world, bones, weights);
            return n > 0 ? new SkinPoint(world, bones, weights, n) : null;
        }

        public void Dispose()
        {
            if (_bake != null) { if (Application.isPlaying) Object.Destroy(_bake); else Object.DestroyImmediate(_bake); }
            _bake = null;
        }
    }

    /// <summary>A world point bound to up to 4 bones with weights (linear blend skinning, like a mesh vertex).</summary>
    public sealed class SkinPoint
    {
        readonly Transform[] _bones;
        readonly Vector3[] _local;
        readonly float[] _w;

        /// Binds `world` in the bones' CURRENT pose. Missing bones are skipped and the rest renormalised.
        public SkinPoint(Vector3 world, Transform[] bones, float[] weights, int count)
        {
            var b = new List<Transform>();
            var l = new List<Vector3>();
            var w = new List<float>();
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                if (bones[i] == null || weights[i] <= 0f) continue;
                b.Add(bones[i]);
                l.Add(bones[i].InverseTransformPoint(world));
                w.Add(weights[i]);
                total += weights[i];
            }
            for (int i = 0; i < w.Count; i++) w[i] /= total;
            _bones = b.ToArray();
            _local = l.ToArray();
            _w = w.ToArray();
        }

        public int BoneCount { get { return _bones.Length; } }

        /// Where the point is now (the weighted bone positions); Vector3.zero when no bone is left.
        public Vector3 Position
        {
            get
            {
                var p = Vector3.zero;
                for (int i = 0; i < _bones.Length; i++) if (_bones[i] != null) p += _bones[i].TransformPoint(_local[i]) * _w[i];
                return p;
            }
        }
    }
}
