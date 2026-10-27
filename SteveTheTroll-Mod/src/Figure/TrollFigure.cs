using System.Collections.Generic;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Figure
{
    /// <summary>
    /// Drives one mini troll on one foot (T-004b, D-013 trial): seated pose from the TrollPose Tunables, world size from
    /// FigureHeight (measured on a baked mesh, never from lossyScale), placement next to the foot bone, upright and facing the
    /// player's direction (the troll does not tip with the foot while walking). Tunables are read every frame (live in KydlTools);
    /// the size is re-measured whenever one of them changes.
    /// </summary>
    internal sealed class TrollFigure : MonoBehaviour
    {
        const float DiagEverySeconds = 5f;
        const float SizeTolerance = 0.02f;                 // 2 cm
        static int _tunableVersion = 1;
        static bool _subscribed;

        Transform _foot, _player;
        bool _left;
        SkinnedMeshRenderer _body;
        readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Quaternion> _rest = new Dictionary<Transform, Quaternion>();
        int _measuredVersion;
        float _bottomBelowPivot;                          // world metres from the holder pivot down to the lowest vertex
        float _measuredHeight;
        float _nextDiag;
        Mesh _bake;

        static readonly string[] PosedBones = { "Spine0", "LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg", "LeftArm", "RightArm", "LeftForeArm", "RightForeArm" };

        /// Expected outcome: foot, player, body renderer and every posed bone are known (returns that check).
        public bool Init(Transform foot, Transform player, bool left)
        {
            _foot = foot; _player = player; _left = left;
            _body = null;
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (_body == null || smr.sharedMesh != null && smr.sharedMesh.vertexCount > _body.sharedMesh.vertexCount) _body = smr;
            foreach (var name in PosedBones)
            {
                var b = Utils.FindChild(transform, name);
                if (b == null) { ModLog.Warn("TrollFigure: bone '" + name + "' not found in the troll copy"); continue; }
                _bones[name] = b; _rest[b] = b.localRotation;
                ModLog.Verbose("TrollFigure: bone " + name + " rest local euler " + b.localEulerAngles.ToString("F1") + ", local pos " + b.localPosition.ToString("F3"));
            }
            Subscribe();
            ModLog.Verbose("TrollFigure: Init " + (left ? "left" : "right") + " foot '" + (foot != null ? foot.name : "null") + "', body " + (_body != null ? _body.name + " (" + _body.sharedMesh.vertexCount + " verts)" : "null")
                + ", " + _bones.Count + "/" + PosedBones.Length + " bones");
            return _foot != null && _player != null && _body != null && _bones.Count == PosedBones.Length;
        }

        static void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            var all = new[] { ModSettings.FigureHeight, ModSettings.FigureForward, ModSettings.FigureUp, ModSettings.FigureSide, ModSettings.FigureYaw,
                ModSettings.PoseSpineX, ModSettings.PoseUpLegX, ModSettings.PoseUpLegZ, ModSettings.PoseLegX, ModSettings.PoseRightArmX, ModSettings.PoseRightArmZ,
                ModSettings.PoseRightForeArmX, ModSettings.PoseLeftArmX, ModSettings.PoseLeftArmZ, ModSettings.PoseLeftForeArmX };
            foreach (var t in all) t.SettingChanged += (s, e) => { _tunableVersion++; ModLog.Verbose("TrollFigure: " + s + " changed -> re-pose / re-measure (version " + _tunableVersion + ")"); };
            ModLog.Verbose("TrollFigure: listening to " + all.Length + " Tunables");
        }

        void LateUpdate()
        {
            if (_foot == null || _player == null) { ModLog.Verbose("TrollFigure: foot or player gone, removing " + name); Destroy(gameObject); return; }
            ApplyPose();
            Place();
            if (_measuredVersion != _tunableVersion) Measure();
            if (Time.time >= _nextDiag) Diag();
        }

        void ApplyPose()
        {
            Pose("Spine0", ModSettings.PoseSpineX.Value, 0f);
            Pose("LeftUpLeg", ModSettings.PoseUpLegX.Value, ModSettings.PoseUpLegZ.Value);
            Pose("RightUpLeg", ModSettings.PoseUpLegX.Value, -ModSettings.PoseUpLegZ.Value);
            Pose("LeftLeg", ModSettings.PoseLegX.Value, 0f);
            Pose("RightLeg", ModSettings.PoseLegX.Value, 0f);
            Pose("RightArm", ModSettings.PoseRightArmX.Value, ModSettings.PoseRightArmZ.Value);
            Pose("RightForeArm", ModSettings.PoseRightForeArmX.Value, 0f);
            Pose("LeftArm", ModSettings.PoseLeftArmX.Value, ModSettings.PoseLeftArmZ.Value);
            Pose("LeftForeArm", ModSettings.PoseLeftForeArmX.Value, 0f);
        }

        void Pose(string bone, float x, float z)
        {
            Transform b;
            if (_bones.TryGetValue(bone, out b)) b.localRotation = _rest[b] * Quaternion.Euler(x, 0f, z);
        }

        // Upright, facing the player's direction; the lowest vertex sits OffsetUp above the foot bone.
        void Place()
        {
            var fwd = Vector3.ProjectOnPlane(_player.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.5f) fwd = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            float outward = _left ? -1f : 1f;
            var pos = _foot.position + fwd * ModSettings.FigureForward.Value + right * (outward * ModSettings.FigureSide.Value);
            pos.y = _foot.position.y + ModSettings.FigureUp.Value + _bottomBelowPivot;
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, ModSettings.FigureYaw.Value, 0f));
        }

        // Bakes the posed body, scales the holder so the world height is FigureHeight, re-bakes and checks it.
        void Measure()
        {
            _measuredVersion = _tunableVersion;
            float min, max;
            if (!Bounds(out min, out max)) return;
            float before = max - min;
            float target = ModSettings.FigureHeight.Value;
            if (before < 0.0001f) { ModLog.Warn("TrollFigure: measured height " + before.ToString("F4") + " m, cannot scale"); return; }
            var oldScale = transform.localScale;
            transform.localScale = oldScale * (target / before);
            if (!Bounds(out min, out max)) return;
            _measuredHeight = max - min;
            _bottomBelowPivot = transform.position.y - min;
            ModLog.Verbose("TrollFigure: Measure " + name + ": height " + before.ToString("F3") + " m at scale " + oldScale.x.ToString("F4")
                + " -> scale " + transform.localScale.x.ToString("F4") + ", height " + _measuredHeight.ToString("F3") + " m, bottom " + _bottomBelowPivot.ToString("F3") + " m below the pivot");
            ModLog.Check("TrollFigure: " + name + " world height", _measuredHeight, target, SizeTolerance, "m");
            Place();                                        // the bottom offset changed: put it on the slipper this frame
        }

        /// Expected outcome: min / max world Y of the baked body mesh; returns whether a mesh was baked.
        bool Bounds(out float min, out float max)
        {
            min = float.MaxValue; max = float.MinValue;
            if (_body == null) { ModLog.Warn("TrollFigure: no body renderer to measure"); return false; }
            if (_bake == null) _bake = new Mesh();
            _body.BakeMesh(_bake);                          // vertices in the renderer's local space (scale not applied)
            var m = _body.transform.localToWorldMatrix;
            var verts = _bake.vertices;
            foreach (var v in verts)
            {
                float y = m.MultiplyPoint3x4(v).y;
                if (y < min) min = y;
                if (y > max) max = y;
            }
            ModLog.Verbose("TrollFigure: Bounds " + name + ": " + verts.Length + " verts, y " + min.ToString("F3") + " .. " + max.ToString("F3") + " m");
            return verts.Length > 0;
        }

        void Diag()
        {
            _nextDiag = Time.time + DiagEverySeconds;
            ModLog.Verbose("TrollFigure: " + name + " foot " + _foot.position.ToString("F2") + ", figure " + transform.position.ToString("F2")
                + " (" + (transform.position - _foot.position).magnitude.ToString("F3") + " m from the foot), height " + _measuredHeight.ToString("F3")
                + " m, scale " + transform.localScale.x.ToString("F4") + ", yaw " + ModSettings.FigureYaw.Value.ToString("F0") + " deg");
            ModLog.CheckRange("TrollFigure: " + name + " distance to the foot", (transform.position - _foot.position).magnitude, 0f, 0.6f, "m");
        }

        void OnDestroy()
        {
            if (_bake != null) Destroy(_bake);
            ModLog.Verbose("TrollFigure: " + name + " destroyed");
        }
    }
}
