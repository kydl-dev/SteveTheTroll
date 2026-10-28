using System.Collections.Generic;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Item
{
    /// <summary>
    /// D-089 (user: "give the character his feet back when the slippers are active ... being invisible is weird"): the whole costume hides the
    /// player's body (D-079) and the slippers hide the costume's plush feet, so the slippers stood empty. This draws ONLY the player's own
    /// feet (the body triangles skinned to the foot / toe bones, ankles included) while the body is hidden and the slippers are worn: same
    /// bones, same materials and skin colour as the body. Sits on the character next to VisEquipment (added by CostumeBodyPatch).
    /// </summary>
    internal sealed class CostumeBareFeet : MonoBehaviour
    {
        /// A body vertex belongs to the feet when at least this much of its weight is on foot / toe bones (ankle vertices blend with the shin).
        internal const float MinFootWeight = 0.3f;

        VisEquipment _vis;
        SkinnedMeshRenderer _body, _feet;
        Mesh _builtFrom, _mesh;
        MaterialPropertyBlock _block;
        bool _failed;
        int _logged = -1;

        void LateUpdate()
        {
            if (_vis == null) _vis = GetComponent<VisEquipment>();
            _body = _vis != null ? _vis.m_bodyModel : null;
            SlipperWearer w = null;
            bool show = _body != null && _body.forceRenderingOff && _vis.TryGetComponent(out w) && w.On;
            if (show && !_failed && (_feet == null || _builtFrom != _body.sharedMesh)) Build();
            if (_feet == null) return;
            if (_feet.enabled != show) _feet.enabled = show;
            int state = show ? 1 : 0;
            if (_logged != state) { _logged = state; ModLog.Verbose("CostumeBareFeet: " + name + " own feet " + (show ? "shown (costume + slippers)" : "hidden")); }
            if (!show) return;
            var mats = _body.sharedMaterials;                                 // the game may swap the body material (skin colour instance)
            if (_feet.sharedMaterials.Length != mats.Length || (mats.Length > 0 && _feet.sharedMaterials[0] != mats[0])) _feet.sharedMaterials = mats;
            if (_block == null) _block = new MaterialPropertyBlock();
            _body.GetPropertyBlock(_block);
            _feet.SetPropertyBlock(_block);
        }

        void Build()
        {
            var src = _body.sharedMesh;
            _builtFrom = src;
            if (src == null || !src.isReadable) { _failed = true; ModLog.Warn("CostumeBareFeet: " + name + " body mesh " + (src == null ? "missing" : "'" + src.name + "' not readable") + " - no feet under the costume"); return; }
            var bones = _body.bones;
            var foot = new bool[bones.Length];
            int footBones = 0;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && IsFootBone(bones[i].name)) { foot[i] = true; footBones++; }
            var weights = src.boneWeights;
            var keep = new bool[weights.Length];
            int kept = 0;
            for (int v = 0; v < weights.Length; v++)
            {
                keep[v] = FootWeight(weights[v], foot) >= MinFootWeight;
                if (keep[v]) kept++;
            }
            if (footBones == 0 || kept == 0) { _failed = true; ModLog.Warn("CostumeBareFeet: " + name + " no foot bones / vertices in '" + src.name + "' (" + footBones + " bone(s), " + kept + " vertex(es)) - no feet under the costume"); return; }

            if (_mesh != null) Destroy(_mesh);
            _mesh = new Mesh { name = src.name + "_SteveTheTrollFeet", indexFormat = src.indexFormat };
            _mesh.vertices = src.vertices;
            _mesh.normals = src.normals;
            _mesh.tangents = src.tangents;
            _mesh.uv = src.uv;
            _mesh.boneWeights = weights;
            _mesh.bindposes = src.bindposes;
            _mesh.subMeshCount = src.subMeshCount;
            int tris = 0;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var t = src.GetTriangles(s);
                var outT = new List<int>(t.Length / 8);
                for (int i = 0; i + 2 < t.Length; i += 3)
                    if (keep[t[i]] && keep[t[i + 1]] && keep[t[i + 2]]) { outT.Add(t[i]); outT.Add(t[i + 1]); outT.Add(t[i + 2]); }
                _mesh.SetTriangles(outT, s);
                tris += outT.Count / 3;
            }
            _mesh.RecalculateBounds();

            if (_feet == null)
            {
                var go = new GameObject("SteveTheTroll_BareFeet");
                go.transform.SetParent(_body.transform.parent, false);
                _feet = go.AddComponent<SkinnedMeshRenderer>();
            }
            var tr = _feet.transform;
            tr.localPosition = _body.transform.localPosition; tr.localRotation = _body.transform.localRotation; tr.localScale = _body.transform.localScale;
            _feet.gameObject.layer = _body.gameObject.layer;
            _feet.sharedMesh = _mesh;
            _feet.bones = bones;
            _feet.rootBone = _body.rootBone;
            _feet.sharedMaterials = _body.sharedMaterials;
            _feet.updateWhenOffscreen = true;
            _feet.shadowCastingMode = _body.shadowCastingMode;
            _feet.enabled = false;
            ModLog.Verbose("CostumeBareFeet: " + name + " feet from '" + src.name + "': " + footBones + " foot / toe bone(s), " + kept + " of " + weights.Length + " vertices, " + tris + " triangle(s)");
            ModLog.CheckRange("CostumeBareFeet: " + name + " feet triangles", tris, 20, 20000, "");
        }

        /// The player's foot and toe bones (LeftFoot, LeftToeBase, RightFoot, RightToeBase, ...).
        internal static bool IsFootBone(string bone)
        {
            return !string.IsNullOrEmpty(bone) && (bone.IndexOf("Foot", System.StringComparison.OrdinalIgnoreCase) >= 0 || bone.IndexOf("Toe", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static float FootWeight(BoneWeight w, bool[] foot)
        {
            float s = 0f;
            if (In(w.boneIndex0, foot)) s += w.weight0;
            if (In(w.boneIndex1, foot)) s += w.weight1;
            if (In(w.boneIndex2, foot)) s += w.weight2;
            if (In(w.boneIndex3, foot)) s += w.weight3;
            return s;
        }

        static bool In(int i, bool[] foot) { return i >= 0 && i < foot.Length && foot[i]; }

        void OnDestroy()
        {
            if (_feet != null) Destroy(_feet.gameObject);
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
