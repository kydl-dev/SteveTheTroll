using Kydl.Common;
using SteveTheTroll.Fit;
using UnityEngine;

namespace SteveTheTroll.Figure
{
    /// <summary>
    /// Drives one mini troll on one foot (D-013 trial). Pose, size and placement come from the Unity fit (TrollFitData, D-014) through the
    /// shared TrollFitMath - the same code the Unity fit scene runs - plus the user's in-game fine tune (TrollFigure / TrollPose Tunables,
    /// read every frame). The size is measured on a baked mesh (never from lossyScale) and re-measured whenever a Tunable changes.
    /// TEST-007: on a slipper the troll is GLUED to it - placed once with the fit (flat, D-016 / D-029), then bound to three points under it
    /// that follow the slipper's skin (SlipperDriver.PointAt); every frame it follows their frame (bends / tilts with the slipper and toes).
    /// </summary>
    internal sealed class TrollFigure : MonoBehaviour
    {
        const float DiagEverySeconds = 5f;
        const float SizeTolerance = 0.02f;                 // 2 cm
        static int _tunableVersion = 1;
        static bool _subscribed;

        Transform _foot, _toe, _player;
        SlipperDriver _slipper;
        SkinPoint _glueA;                                  // the socket anchor (D-147)
        Vector3 _socketFwdInFoot = Vector3.forward, _socketUpInFoot = Vector3.up;
        const float MaxLeanDeg = 50f;      // D-159
        Vector3 _heading = Vector3.zero;                  // D-157: the troll's flat facing last frame (zero = none yet)
        const float HeadingRateDegPerSecond = 540f;
        // D-137 (user 22:55, the troll was under the slipper AGAIN): the safe fallback is the troll's own PLACED pose (Place(): upright, on the foot
        // bone's position and forward) as seen from the foot bone at bind time - NOT the skin frame, which was wrong itself when the bug hit and so
        // could never be compared against anything. Follow checks the final pose (turn AND position) against it every frame.
        int _boundSlipper = -1;
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
        Vector3[] _curlAxis;
        FigureFur _fur;
        Vector3 _lastFwd;                                 // D-029 flip detector: the troll must not swing round within one frame
        int _jumps;
        float _maxStep;                              // per posed bone: the knuckle line in that bone's own frame (fingers / thumb only)

        /// Expected outcome: foot, player, body renderer and every fitted bone are known (returns that check).
        public bool Init(Transform foot, Transform player, bool left, SlipperDriver slipper)
        {
            _foot = foot; _player = player; _left = left; _slipper = slipper;
            _toe = foot != null ? foot.Find(TrollFitMath.ToeOf(foot.name)) : null;     // direct child of the player's foot (D-016)
            ModLog.Verbose("TrollFigure: toe '" + (foot != null ? TrollFitMath.ToeOf(foot.name) : "?") + "' " + (_toe != null ? "found, foot->toe " + (_toe.position - foot.position).magnitude.ToString("F3") + " m" : "MISSING (falls back to the player's forward)"));
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
            CurlAxes();
            _fur = new FigureFur(_body, name);
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

        // Puts the fitted pose on once and stores, for every finger / thumb joint, the knuckle line in that joint's own frame:
        // FingerCurl / ThumbCurl turn around it, so "curl" means the same thing for every joint whatever the bone axes are.
        void CurlAxes()
        {
            TrollFitMath.ApplyPose(_bones, _rest, _delta, null);
            var k = TrollFitMath.KnuckleLine(transform);
            _curlAxis = new Vector3[_bones.Length];
            int n = 0;
            for (int i = 0; i < _bones.Length; i++)
            {
                var name = TrollFitMath.PosedBones[i];
                if (_bones[i] == null || k == Vector3.zero || !name.StartsWith("RightHand") || name == "RightHand") continue;
                _curlAxis[i] = Quaternion.Inverse(_bones[i].rotation) * k;
                n++;
            }
            ModLog.Verbose("TrollFigure: knuckle line " + k.ToString("F2") + " -> curl axes for " + n + " finger / thumb joint(s)");
            ModLog.Check("TrollFigure: curl axes", n, 12, 0, "joints");
        }

        static void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            var all = new[] { ModSettings.FigureHeight, ModSettings.FigureForward, ModSettings.FigureUp, ModSettings.FigureSide, ModSettings.FigureSideLeft, ModSettings.FigureSideRight, ModSettings.FigureYaw,
                ModSettings.PoseSpineX, ModSettings.PoseUpLegX, ModSettings.PoseUpLegZ, ModSettings.PoseLegX, ModSettings.PoseRightArmX, ModSettings.PoseRightArmZ,
                ModSettings.PoseRightForeArmX, ModSettings.PoseLeftArmX, ModSettings.PoseLeftArmZ, ModSettings.PoseLeftForeArmX,
                ModSettings.LogForward, ModSettings.LogUp, ModSettings.LogSide, ModSettings.LogYaw, ModSettings.LogPitch, ModSettings.LogRoll, ModSettings.LogLengthScale,
                ModSettings.HandWristX, ModSettings.HandWristY, ModSettings.HandWristZ, ModSettings.HandFingerCurl, ModSettings.HandThumbCurl };
            foreach (var t in all) t.SettingChanged += (s, e) => { _tunableVersion++; ModLog.Verbose("TrollFigure: " + s + " changed -> re-pose / re-measure (version " + _tunableVersion + ")"); };
            ModSettings.PlushEnable.SettingChanged += (s, e) => { _tunableVersion++; ModLog.Verbose("TrollFigure: " + s + " changed -> plush"); };
            foreach (var p in new[] { ModSettings.PlushBrightness, ModSettings.PlushSmoothness }) p.SettingChanged += (s, e) => { _tunableVersion++; ModLog.Verbose("TrollFigure: " + s + " changed -> plush colour"); };
            foreach (var c in new[] { ModSettings.PlushColorR, ModSettings.PlushColorG, ModSettings.PlushColorB }) c.SettingChanged += (s, e) => { _tunableVersion++; ModLog.Verbose("TrollFigure: " + s + " changed -> plush colour"); };
            foreach (var c in new[] { ModSettings.FurColorR, ModSettings.FurColorG, ModSettings.FurColorB }) c.SettingChanged += (s, e) => { _tunableVersion++; ModLog.Verbose("TrollFigure: " + s + " changed -> fur update"); };
            ModSettings.FurEnable.SettingChanged += (s, e) => { _tunableVersion++; ModLog.Verbose("TrollFigure: " + s + " changed -> fur rebuild"); };
            ModSettings.FurShells.SettingChanged += (s, e) => { _tunableVersion++; ModLog.Verbose("TrollFigure: " + s + " changed -> fur rebuild"); };
            var fur = new[] { ModSettings.FurLength, ModSettings.FurDensity, ModSettings.FurThickness, ModSettings.FurDroop, ModSettings.FurBrightness, ModSettings.FurTipLight };
            foreach (var t in fur) t.SettingChanged += (s, e) => { _tunableVersion++; ModLog.Verbose("TrollFigure: " + s + " changed -> fur update"); };
            ModLog.Verbose("TrollFigure: listening to " + (all.Length + fur.Length + 2) + " Tunables");
        }

        void LateUpdate()
        {
            if (_foot == null || _player == null) { ModLog.Verbose("TrollFigure: foot or player gone, removing " + name); Destroy(gameObject); return; }
            bool dirty = _measuredVersion != _tunableVersion;
            if (dirty) UpdateExtras();
            TrollFitMath.ApplyPose(_bones, _rest, _delta, _extra);
            bool rebind = _slipper != null && (dirty || _boundSlipper != _slipper.BuildVersion);
            if (_slipper == null || rebind || _glueA == null) Place();
            if (dirty) Measure();
            if (rebind) Bind();
            else if (_glueA != null) Follow();
            Watch();
            PlaceLog(dirty);
            if (dirty && _fur != null) _fur.Refresh();
            if (Time.time >= _nextDiag) Diag();
        }

        /// D-068 (F6 viewer): pose, size and place it right away (the viewer measures the model before the first LateUpdate).
        internal void Settle()
        {
            if (_measuredVersion != _tunableVersion) UpdateExtras();
            TrollFitMath.ApplyPose(_bones, _rest, _delta, _extra);
            Measure();
            PlaceLog(false);
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
            int wrist = System.Array.IndexOf(TrollFitMath.PosedBones, "RightHand");
            if (wrist >= 0) _extra[wrist] = Quaternion.Euler(ModSettings.HandWristX.Value, ModSettings.HandWristY.Value, ModSettings.HandWristZ.Value);
            int curled = 0;
            for (int i = 0; i < _extra.Length; i++)
            {
                if (_curlAxis == null || _curlAxis[i] == Vector3.zero) continue;
                float deg = TrollFitMath.PosedBones[i].Contains("Thumb") ? ModSettings.HandThumbCurl.Value : ModSettings.HandFingerCurl.Value;
                _extra[i] = Quaternion.AngleAxis(deg, _curlAxis[i]);
                curled++;
            }
            ModLog.Verbose("TrollFigure: hand fine tune wrist (" + ModSettings.HandWristX.Value.ToString("F0") + ", " + ModSettings.HandWristY.Value.ToString("F0") + ", " + ModSettings.HandWristZ.Value.ToString("F0") + ") deg, finger curl " + ModSettings.HandFingerCurl.Value.ToString("F0") + ", thumb curl " + ModSettings.HandThumbCurl.Value.ToString("F0") + " deg on " + curled + " joint(s)");
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
            float side = ModSettings.FigureSide.Value + (_left ? ModSettings.FigureSideLeft.Value : ModSettings.FigureSideRight.Value);
            var fwd = TrollFitMath.FootForward(_foot, _toe, _player.forward);
            TrollFitMath.Place(_foot.position, fwd, _left, ModSettings.FigureForward.Value, side, ModSettings.FigureUp.Value,
                ModSettings.FigureYaw.Value, _bottomBelowPivot, out pos, out rot);
            transform.SetPositionAndRotation(pos, rot);
        }

        // D-147: anchors the troll to the slipper's TrollSocket - ONE skin point (moves with the slipper and toe bend) - and stands it upright: the socket's forward flattened
        // onto the player's up plane gives the facing, so what the foot does (tilts at login, toe bend) cannot tilt the troll. No socket = Warn, the troll stays placed on the foot.
        void Bind()
        {
            _boundSlipper = _slipper.BuildVersion;
            SkinPoint anchor; Vector3 fwd;
            if (!_slipper.TrySocket(out anchor, out fwd))
            {
                ModLog.Warn("TrollFigure: " + name + " cannot anchor - the slipper has no TrollSocket (rebuild the bundle) - stays placed on the foot");
                _glueA = null;
                return;
            }
            _glueA = anchor; _socketFwdInFoot = fwd; _socketUpInFoot = _slipper.SocketUpInFoot;
            // D-157 (test runs 020834 and 095924: ONE 61 deg turn ~5 s after the bind, both times): the troll was placed with the foot's heading + FigureYaw and the first socket frame
            // snapped to the socket's heading. It now starts from the heading it has and turns to the socket's at the rate limit (~0.1 s).
            var h = Vector3.ProjectOnPlane(transform.forward, _player.up);
            _heading = h.sqrMagnitude > 1e-4f ? h.normalized : Vector3.zero;
            ModLog.Verbose("TrollFigure: " + name + " anchored to the slipper socket, build " + _boundSlipper + " (" + anchor.BoneCount + " bones), pivot " + _bottomBelowPivot.ToString("F3") + " m above it");
        }

        // Follows the socket anchor: position from the slipper skin, facing from the socket flattened to the player's up, yaw tunable on top.
        void Follow()
        {
            // D-159: the troll leans with the slipper (socket up, at most MaxLeanDeg from the player's up); the heading stays flat against the player's up
            var up = _player.up;
            var lean = FitGeometry.LeanUp(up, _foot.rotation * _socketUpInFoot, MaxLeanDeg);
            var want = FitGeometry.UprightForward(_foot.rotation * _socketFwdInFoot, up, _player.forward);
            // D-157: a turn-rate limit on the facing (a steep foot pitch swings the flattened socket direction by tens of degrees in one frame)
            var fwd = FitGeometry.StepHeading(_heading, want, HeadingRateDegPerSecond * Mathf.Max(Time.deltaTime, 0.001f), up);
            _heading = fwd;
            var rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, lean).sqrMagnitude > 1e-4f ? Vector3.ProjectOnPlane(fwd, lean) : fwd, lean) * Quaternion.Euler(0f, ModSettings.FigureYaw.Value, 0f);
            transform.SetPositionAndRotation(_glueA.Position + lean * _bottomBelowPivot, rot);
            ModLog.CheckRange("TrollFigure: " + name + " troll bottom -> socket", (transform.position - transform.up * _bottomBelowPivot - _glueA.Position).magnitude, 0f, 0.001f, "m");
            CheckTilt();
        }

        // D-140 (test run 231819: from the FIRST glue on, 398 of 400 sole checks showed the trolls tilted 71 / 88 deg, i.e. hanging under the slippers, and the foot-pose guard
        // never fired): the guard compares with the pose the troll had at bind time in the foot's space - and when that bind happens while the foot bone is in an odd pose
        // (just after the slippers appear, the animator not settled), the troll is "upright" in THAT pose and tilted for good, skin and foot agreeing with each other.
        // So the world is the judge: a troll that stays tilted more than RebindTiltDeg against the player's up for RebindAfterSeconds while the player stands still on the
        // ground is placed and glued again (foot pose settled by then).
        const float RebindTiltDeg = 60f, RebindAfterSeconds = 1f, StillSpeed = 0.5f;
        float _tiltSince = -1f, _nextSettleLog;
        const float SettleSeconds = 8f;
        static int _playerId; static float _playerAt;
        static float PlayerSince(Player p) { int id = p.GetInstanceID(); if (id != _playerId) { _playerId = id; _playerAt = Time.time; } return _playerAt; }
        static string SettleState(Player p)
        {
            return "since spawn " + (Time.time - PlayerSince(p)).ToString("F1") + " s, teleporting " + p.IsTeleporting() + ", bed " + p.InBed() + ", sitting " + p.IsSitting() + ", emote " + p.InEmote() + ", attached " + p.IsAttached();
        }
        int _rebinds;

        void CheckTilt()
        {
            if (Vector3.Angle(transform.up, _player.up) <= RebindTiltDeg) { _tiltSince = -1f; return; }
            if (_tiltSince < 0f) _tiltSince = Time.time;
            var me = Player.m_localPlayer;
            if (me == null) return;
            // D-146: the player is settling in (just spawned / teleporting / in bed / emote / sitting): the FOOT is tilted, a new glue changes nothing
            bool settling = me.IsTeleporting() || me.InBed() || me.IsSitting() || me.InEmote() || me.IsAttached() || Time.time - PlayerSince(me) < SettleSeconds;
            if (!FitGeometry.ShouldRebind(Time.time - _tiltSince, RebindAfterSeconds, me.IsOnGround(), me.GetVelocity().magnitude, StillSpeed, settling))
            {
                if (settling && Time.time >= _nextSettleLog) { _nextSettleLog = Time.time + 5f; ModLog.Info("TrollFigure: " + name + " is tilted " + Vector3.Angle(transform.up, _player.up).ToString("F0") + " deg while the player is settling in (" + SettleState(me) + ") - no new glue, the foot is tilted"); }
                return;
            }
            _tiltSince = -1f; _rebinds++;
            _boundSlipper = -1;                              // LateUpdate places it flat on the foot and glues it again
            ModLog.Warn("TrollFigure: " + name + " stayed tilted " + Vector3.Angle(transform.up, _player.up).ToString("F0") + " deg against the player's up for over " + RebindAfterSeconds + " s while the player stood still - placed and glued again (" + _rebinds + ". time)");
        }

        // D-029 flip detector, measured AGAINST THE FOOT (D-036): the glued troll turns with the foot, so a world-space step also counts the
        // foot's own swing (TEST-009 / TEST-010: 10-36 "jumps" up to 158 deg per session while running / jumping, the user saw no flip).
        // A real glue or placement flip still turns the troll against its foot. 3D, not flattened (TEST-008).
        void Watch()
        {
            // D-157: glued to a socket the troll stands upright on purpose and does not follow the foot's pitch / roll, so its turn is measured against the PLAYER's facing
            var fwd = _glueA != null && _player != null ? Quaternion.Inverse(Quaternion.LookRotation(FitGeometry.UprightForward(_player.forward, _player.up, Vector3.forward), _player.up)) * transform.forward
                : _foot != null ? Quaternion.Inverse(_foot.rotation) * transform.forward : transform.forward;
            float step = FitGeometry.FlipStep(_lastFwd, fwd);
            if (step > _maxStep) _maxStep = step;
            if (FitGeometry.IsJump(step)) _jumps++;
            _lastFwd = fwd;
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
            float ratio = TrollFitData.LogLength * ModSettings.LogLengthScale.Value;
            float target = ratio * ModSettings.FigureHeight.Value;
            var extraOffset = new Vector3(ModSettings.LogSide.Value, ModSettings.LogUp.Value, ModSettings.LogForward.Value);
            var extraTurn = TrollFitMath.LogTurn(ModSettings.LogYaw.Value, ModSettings.LogPitch.Value, ModSettings.LogRoll.Value);
            float length = TrollFitMath.PlaceLog(_log, _logMesh, transform, _hand, ModSettings.FigureHeight.Value, ratio,
                new Vector3(TrollFitData.LogOffset[0], TrollFitData.LogOffset[1], TrollFitData.LogOffset[2]),
                new Vector3(TrollFitData.LogEuler[0], TrollFitData.LogEuler[1], TrollFitData.LogEuler[2]), extraOffset, extraTurn);
            if (!check) return;
            ModLog.Verbose("TrollFigure: " + name + " log fine tune offset " + extraOffset.ToString("F3") + " m, yaw " + ModSettings.LogYaw.Value.ToString("F0")
                + ", pitch " + ModSettings.LogPitch.Value.ToString("F0") + ", roll " + ModSettings.LogRoll.Value.ToString("F0") + " deg, length x" + ModSettings.LogLengthScale.Value.ToString("F2"));
            ModLog.Check("TrollFigure: " + name + " log length", length, target, 0.01, "m");
        }

        bool _say;
        float _nextSay;

        void Diag()
        {
            _nextDiag = Time.time + DiagEverySeconds;
            float dist = (transform.position - _foot.position).magnitude;
            float tiltNow = Vector3.Angle(transform.up, Vector3.up);
            _say = Time.time >= _nextSay || tiltNow >= 10f;       // D-145: the routine numbers once a minute (or when tilted); the CHECKs below stay in every pass
            if (_say) _nextSay = Time.time + 60f;
            if (_say) ModLog.Verbose("TrollFigure: " + name + " foot " + _foot.position.ToString("F2") + ", figure " + transform.position.ToString("F2")
                + " (" + dist.ToString("F3") + " m from the foot), height " + _measuredHeight.ToString("F3") + " m, scale " + transform.localScale.x.ToString("F4")
                + ", yaw " + ModSettings.FigureYaw.Value.ToString("F0") + " deg, foot turned " + Vector3.SignedAngle(TrollFitMath.Flat(_player.forward, Vector3.forward), TrollFitMath.FootForward(_foot, _toe, _player.forward), Vector3.up).ToString("F0") + " deg from the player's forward");
            ModLog.CheckRange("TrollFigure: " + name + " distance to the foot", dist, 0f, 0.6f, "m");
            if (_say) ModLog.Verbose("TrollFigure: " + name + " " + (_glueA != null ? "glued to the slipper, tilt " + Vector3.Angle(transform.up, Vector3.up).ToString("F0") + " deg" : "placed on the foot (not glued)"));
            ModLog.Check("TrollFigure: " + name + " frame-to-frame turns against the foot over 45 deg in the last " + DiagEverySeconds + " s (largest " + _maxStep.ToString("F0") + " deg)", _jumps, 0, 0, "");
            _jumps = 0; _maxStep = 0f;
            WatchSole();
        }

        // Known bug (user, 2026-10-30): now and then the troll hangs UNDER the slipper, its head at the sole. Nothing checked it before, so the logs
        // could not show it. Checks: the troll's up axis points the way the player's does; while upright its lowest vertex is near the sole
        // (measured when standing: 0.095 m below the foot bone, 0.375 m when hanging under it) and its head is ABOVE the foot bone (measured: 0.185 m
        // above = -0.185, when hanging it is ~0.095 m below). The 21:35 session had the troll tilted 65 - 88 deg for minutes (screenshots).
        // D-160: the figures that exist and what the last sole check measured (RuntimeHealth and the test hooks read them)
        internal static readonly System.Collections.Generic.List<TrollFigure> All = new System.Collections.Generic.List<TrollFigure>();
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }
        internal float LastTilt, LastBelowSole = float.NaN, LastHeadAboveTop = float.NaN;

        void WatchSole()
        {
            float tilt = Vector3.Angle(transform.up, _player.up);
            LastTilt = tilt;
            ModLog.CheckRange("TrollFigure: " + name + " up axis against the player's up", tilt, 0f, 90f, "deg");
            if (_bake == null || _body == null) return;
            float min, max;
            if (!TrollFitMath.MeasureY(_body, _bake, out min, out max)) return;
            float feet = _foot.position.y - min, head = _foot.position.y - max;
            // D-160: against the SLIPPER (its renderer bounds), not only the foot bone: how far the figure's lowest vertex is below the slipper's bottom, and its head above the slipper's top
            float sb, st;
            if (_slipper != null && _slipper.TryExtent(out sb, out st)) { LastBelowSole = sb - min; LastHeadAboveTop = max - st; }
            if (_say) ModLog.Verbose("TrollFigure: " + name + " lowest vertex " + feet.ToString("F3") + " m and head " + head.ToString("F3") + " m below the foot bone, tilt " + tilt.ToString("F0") + " deg");
            if (tilt >= 30f) return;
            ModLog.CheckRange("TrollFigure: " + name + " upright troll: lowest vertex below the foot bone (sole)", feet, 0f, 0.2f, "m");
            ModLog.CheckRange("TrollFigure: " + name + " upright troll: head below the foot bone (more = hangs under the slipper)", head, -0.3f, -0.05f, "m");
        }

        void OnDestroy()
        {
            if (_bake != null) Destroy(_bake);
            if (_fur != null) ModLog.Verbose("TrollFigure: " + name + " fur removed (" + _fur.Dispose() + " shell(s))");
            ModLog.Verbose("TrollFigure: " + name + " destroyed");
        }
    }
}
