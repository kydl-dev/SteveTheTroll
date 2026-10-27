using Kydl.Common;
using SteveTheTroll.Fit;
using UnityEngine;

namespace SteveTheTroll.Figure
{
    /// <summary>
    /// Drives one mini troll on one foot (D-013 trial). Pose, size and placement come from the Unity fit (TrollFitData, D-014) through the
    /// shared TrollFitMath - the same code the Unity fit scene runs - plus the user's in-game fine tune (TrollFigure / TrollPose Tunables,
    /// read every frame). The size is measured on a baked mesh (never from lossyScale) and re-measured whenever a Tunable changes.
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
        Transform[] _bones;
        Quaternion[] _rest, _delta, _extra;
        int _measuredVersion;
        float _bottomBelowPivot;                          // world metres from the figure pivot down to its lowest vertex
        float _measuredHeight;
        float _nextDiag;
        Mesh _bake;
        Transform _log, _hand;
        MeshFilter _logMesh;

        /// Expected outcome: foot, player, body renderer and every fitted bone are known (returns that check).
        public bool Init(Transform foot, Transform player, bool left)
        {
            _foot = foot; _player = player; _left = left;
            _body = TrollFitMath.FindBody(transform);
            int n = TrollFitMath.PosedBones.Length, found = 0;
            _bones = new Transform[n]; _rest = new Quaternion[n]; _delta = new Quaternion[n]; _extra = new Quaternion[n];
            var d = TrollFitData.Delta;
            for (int i = 0; i < n; i++)
            {
                _delta[i] = d.Length >= (i + 1) * 4 ? new Quaternion(d[i * 4], d[i * 4 + 1], d[i * 4 + 2], d[i * 4 + 3]) : Quaternion.identity;
                _extra[i] = Quaternion.identity;
                _bones[i] = TrollFitMath.FindDeep(transform, TrollFitMath.PosedBones[i]);
                if (_bones[i] == null) { ModLog.Warn("TrollFigure: fitted bone '" + TrollFitMath.PosedBones[i] + "' not found in the troll copy"); continue; }
                _rest[i] = _bones[i].localRotation;
                found++;
                ModLog.Verbose("TrollFigure: bone " + TrollFitMath.PosedBones[i] + " rest " + _bones[i].localEulerAngles.ToString("F1") + ", fit delta " + _delta[i].eulerAngles.ToString("F1"));
            }
            _log = transform.Find(TrollFigureFactory.LogName);
            _logMesh = _log != null ? _log.GetComponentInChildren<MeshFilter>(true) : null;
            _hand = TrollFitMath.FindDeep(transform, TrollFigureFactory.HandBone);
            ModLog.Verbose("TrollFigure: log " + (_log != null ? "found" : "MISSING") + ", log mesh " + (_logMesh != null && _logMesh.sharedMesh != null ? _logMesh.sharedMesh.name : "null") + ", hand " + (_hand != null ? _hand.name : "null")
                + "; fit log length " + TrollFitData.LogLength + " x height, euler (" + string.Join(", ", System.Array.ConvertAll(TrollFitData.LogEuler, v => v.ToString("F0"))) + ")");
            Subscribe();
            ModLog.Verbose("TrollFigure: Init " + (left ? "left" : "right") + " foot '" + (foot != null ? foot.name : "null") + "', body " + (_body != null ? _body.name + " (" + _body.sharedMesh.vertexCount + " verts)" : "null")
                + ", " + found + "/" + n + " bones, fit saved " + TrollFitData.SavedAt);
            ModLog.Check("TrollFigure: fit data length", d.Length, n * 4, 0, "floats");
            return _foot != null && _player != null && _body != null && found == n;
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
            bool dirty = _measuredVersion != _tunableVersion;
            if (dirty) UpdateExtras();
            TrollFitMath.ApplyPose(_bones, _rest, _delta, _extra);
            Place();
            if (dirty) Measure();
            PlaceLog(dirty);
            if (Time.time >= _nextDiag) Diag();
        }

        // In-game fine tune on top of the fit (identity at the default 0 values).
        void UpdateExtras()
        {
            for (int i = 0; i < _extra.Length; i++) _extra[i] = Quaternion.identity;
            Extra("Spine0", ModSettings.PoseSpineX.Value, 0f);
            Extra("LeftUpLeg", ModSettings.PoseUpLegX.Value, ModSettings.PoseUpLegZ.Value);
            Extra("RightUpLeg", ModSettings.PoseUpLegX.Value, -ModSettings.PoseUpLegZ.Value);
            Extra("LeftLeg", ModSettings.PoseLegX.Value, 0f);
            Extra("RightLeg", ModSettings.PoseLegX.Value, 0f);
            Extra("RightArm", ModSettings.PoseRightArmX.Value, ModSettings.PoseRightArmZ.Value);
            Extra("RightForeArm", ModSettings.PoseRightForeArmX.Value, 0f);
            Extra("LeftArm", ModSettings.PoseLeftArmX.Value, ModSettings.PoseLeftArmZ.Value);
            Extra("LeftForeArm", ModSettings.PoseLeftForeArmX.Value, 0f);
        }

        void Extra(string bone, float x, float z)
        {
            int i = System.Array.IndexOf(TrollFitMath.PosedBones, bone);
            if (i >= 0) _extra[i] = Quaternion.Euler(x, 0f, z);
            if (x != 0f || z != 0f) ModLog.Verbose("TrollFigure: fine tune " + bone + " x " + x.ToString("F0") + ", z " + z.ToString("F0") + " deg");
        }

        void Place()
        {
            Vector3 pos; Quaternion rot;
            TrollFitMath.Place(_foot.position, _player.forward, _left, ModSettings.FigureForward.Value, ModSettings.FigureSide.Value, ModSettings.FigureUp.Value,
                ModSettings.FigureYaw.Value, _bottomBelowPivot, out pos, out rot);
            transform.SetPositionAndRotation(pos, rot);
        }

        // Scales the figure so the posed body is FigureHeight tall in the world, re-measures and checks it.
        void Measure()
        {
            _measuredVersion = _tunableVersion;
            if (_bake == null) _bake = new Mesh();
            float min, max;
            if (!TrollFitMath.MeasureY(_body, _bake, out min, out max)) return;
            float before = max - min, target = ModSettings.FigureHeight.Value;
            if (before < 0.0001f) { ModLog.Warn("TrollFigure: measured height " + before.ToString("F4") + " m, cannot scale"); return; }
            var oldScale = transform.localScale;
            transform.localScale = oldScale * (target / before);
            if (!TrollFitMath.MeasureY(_body, _bake, out min, out max)) return;
            _measuredHeight = max - min;
            _bottomBelowPivot = transform.position.y - min;
            ModLog.Verbose("TrollFigure: Measure " + name + ": height " + before.ToString("F3") + " m at scale " + oldScale.x.ToString("F4")
                + " -> scale " + transform.localScale.x.ToString("F4") + ", height " + _measuredHeight.ToString("F3") + " m, bottom " + _bottomBelowPivot.ToString("F3") + " m below the pivot");
            ModLog.Check("TrollFigure: " + name + " world height", _measuredHeight, target, SizeTolerance, "m");
            Place();                                        // the bottom offset changed: put it on the slipper this frame
        }

        // The log follows the posed right hand (fit values), sized from the troll height. Checked whenever the size was re-measured.
        void PlaceLog(bool check)
        {
            if (_log == null) return;
            float target = TrollFitData.LogLength * ModSettings.FigureHeight.Value;
            float length = TrollFitMath.PlaceLog(_log, _logMesh, transform, _hand, ModSettings.FigureHeight.Value, TrollFitData.LogLength,
                new Vector3(TrollFitData.LogOffset[0], TrollFitData.LogOffset[1], TrollFitData.LogOffset[2]),
                new Vector3(TrollFitData.LogEuler[0], TrollFitData.LogEuler[1], TrollFitData.LogEuler[2]));
            if (check) ModLog.Check("TrollFigure: " + name + " log length", length, target, 0.01, "m");
        }

        void Diag()
        {
            _nextDiag = Time.time + DiagEverySeconds;
            float dist = (transform.position - _foot.position).magnitude;
            ModLog.Verbose("TrollFigure: " + name + " foot " + _foot.position.ToString("F2") + ", figure " + transform.position.ToString("F2")
                + " (" + dist.ToString("F3") + " m from the foot), height " + _measuredHeight.ToString("F3") + " m, scale " + transform.localScale.x.ToString("F4")
                + ", yaw " + ModSettings.FigureYaw.Value.ToString("F0") + " deg");
            ModLog.CheckRange("TrollFigure: " + name + " distance to the foot", dist, 0f, 0.6f, "m");
        }

        void OnDestroy()
        {
            if (_bake != null) Destroy(_bake);
            ModLog.Verbose("TrollFigure: " + name + " destroyed");
        }
    }
}
