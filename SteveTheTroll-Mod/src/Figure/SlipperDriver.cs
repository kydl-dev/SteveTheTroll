using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Fit;
using UnityEngine;

namespace SteveTheTroll.Figure
{
    /// <summary>
    /// D-025: one plush slipper on one foot. Builds it through the shared SlipperFit (same code as the Unity fit scene) from the bundle
    /// prefab "SlipperParts", skinned with the player's OWN foot / toe / shin weights (SkinTransfer, TEST-007; foot + toe weights when the body
    /// mesh is not found). Placement Tunables rebuild it (bindposes are taken at build); the troll on it binds through PointAt,
    /// brightness Tunables only re-colour its own material copies. Logs the size / place every few seconds.
    /// </summary>
    internal sealed class SlipperDriver : MonoBehaviour
    {
        const string PrefabName = BundleAssets.SlipperParts;
        const float DiagEverySeconds = 5f;
        static GameObject _prefab;
        static bool _prefabLooked;
        static int _placeVersion = 1, _colourVersion = 1;
        static bool _subscribed;

        Transform _foot, _toe, _player, _root;
        SkinTransfer _skin;
        int _buildVersion;
        bool _left;
        int _builtPlace, _builtColour;
        readonly List<Material> _own = new List<Material>();
        readonly Dictionary<Material, Color> _base = new Dictionary<Material, Color>();
        readonly List<string> _partOf = new List<string>();
        float _nextDiag;

        static GameObject Prefab()
        {
            if (_prefabLooked) return _prefab;
            _prefabLooked = true;
            _prefab = BundleAssets.Load<GameObject>(PrefabName);
            if (_prefab == null) ModLog.Warn("SlipperDriver: prefab '" + PrefabName + "' not in the bundle (" + (Plugin.Bundle != null ? "bundle loaded" : "no bundle") + ")");
            else ModLog.Verbose("SlipperDriver: prefab '" + PrefabName + "' loaded, " + _prefab.GetComponentsInChildren<MeshFilter>(true).Length + " part(s)");
            return _prefab;
        }

        /// Expected outcome: foot and player are set and the slipper is built (returns that check).
        public bool Init(Transform foot, Transform player, bool left)
        {
            _foot = foot; _player = player; _left = left;
            _toe = foot != null ? foot.Find(TrollFitMath.ToeOf(foot.name)) : null;
            var vis = player != null ? player.GetComponent<VisEquipment>() : null;
            var body = vis != null ? vis.m_bodyModel : null;
            if (foot != null && body != null) _skin = new SkinTransfer(body, new[] { foot, _toe, foot.parent });
            ModLog.Verbose("SlipperDriver: skin source " + (_skin != null ? body.name + " (" + body.sharedMesh.vertexCount + " verts), bones " + foot.name + " / " + (_toe != null ? _toe.name : "-") + " / " + (foot.parent != null ? foot.parent.name : "-") : "NOT FOUND (foot + toe weights)"));
            Subscribe();
            ModLog.Verbose("SlipperDriver: Init " + (left ? "left" : "right") + " foot " + (foot != null ? foot.name : "null") + ", toe " + (_toe != null ? _toe.name : "MISSING (no toe bend)"));
            Build();
            return _foot != null && _player != null && _root != null;
        }

        static void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            foreach (var t in new[] { ModSettings.SlipperForward, ModSettings.SlipperUp, ModSettings.SlipperSide, ModSettings.SlipperYaw, ModSettings.SlipperScale, ModSettings.SlipperWidth })
                t.SettingChanged += (s, e) => { _placeVersion++; ModLog.Verbose("SlipperDriver: " + s + " changed -> rebuild"); };
            foreach (var t in new[] { ModSettings.SlipperBrightness, ModSettings.SlipperRimBrightness })
                t.SettingChanged += (s, e) => { _colourVersion++; ModLog.Verbose("SlipperDriver: " + s + " changed -> re-colour"); };
        }

        void Build()
        {
            _builtPlace = _placeVersion;
            if (_root != null) Destroy(_root.gameObject);
            foreach (var m in _own) if (m != null) Destroy(m);
            _own.Clear(); _base.Clear(); _partOf.Clear();
            int sources = _skin != null ? _skin.Refresh() : 0;
            if (_skin != null) ModLog.CheckRange("SlipperDriver: " + name + " skin source vertices (foot / toe / shin)", sources, 50, 20000, "verts");
            var parts = SlipperFit.Build(Prefab(), transform, _foot, _toe, _player.forward, _left, ModSettings.SlipperForward.Value, ModSettings.SlipperSide.Value,
                ModSettings.SlipperUp.Value, ModSettings.SlipperYaw.Value, ModSettings.SlipperScale.Value, ModSettings.SlipperWidth.Value, _skin, out _root);
            _buildVersion++;
            foreach (var p in parts)
            {
                var mats = p.Renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var copy = new Material(mats[i]) { name = mats[i].name + "_" + name };
                    _base[copy] = copy.GetColor("_Color");
                    _own.Add(copy);
                    _partOf.Add(p.Name);
                    mats[i] = copy;
                }
                p.Renderer.sharedMaterials = mats;
                p.Renderer.gameObject.layer = _foot.gameObject.layer;
            }
            ModLog.Verbose("SlipperDriver: " + name + " built " + parts.Count + " part(s), " + _own.Count + " material copy(ies), forward " + ModSettings.SlipperForward.Value.ToString("F3")
                + " m, up " + ModSettings.SlipperUp.Value.ToString("F3") + " m, side " + ModSettings.SlipperSide.Value.ToString("F3") + " m, yaw " + ModSettings.SlipperYaw.Value.ToString("F0") + ", scale " + ModSettings.SlipperScale.Value.ToString("F2") + ", width " + ModSettings.SlipperWidth.Value.ToString("F2"));
            ModLog.Check("SlipperDriver: " + name + " parts", parts.Count, 3, 0, "");
            if (!HasSocket) ModLog.Warn("SlipperDriver: " + name + " has no TrollSocket - the troll cannot be anchored (bundle built before D-147?)");
            foreach (var p in parts) ModLog.Verbose("SlipperDriver: " + name + " " + p.Name + " skinned to " + p.Renderer.bones.Length + " bone(s): " + string.Join(", ", System.Array.ConvertAll(p.Renderer.bones, b => b != null ? b.name : "null")));
            Colour();
        }

        // Upper (and its lining) follow Brightness, the rim RimBrightness; the sole stays black.
        void Colour()
        {
            _builtColour = _colourVersion;
            for (int i = 0; i < _own.Count; i++)
            {
                var m = _own[i];
                float k = _partOf[i] == "SlipperRim" ? ModSettings.SlipperRimBrightness.Value : _partOf[i] == "SlipperUpper" ? ModSettings.SlipperBrightness.Value : 1f;
                var c = _base[m] * k;
                c.a = 1f;
                m.SetColor("_Color", c);
            }
            ModLog.Verbose("SlipperDriver: " + name + " colour: plush x" + ModSettings.SlipperBrightness.Value.ToString("F2") + ", rim x" + ModSettings.SlipperRimBrightness.Value.ToString("F2"));
        }

        void LateUpdate()
        {
            if (_foot == null || _player == null) { ModLog.Verbose("SlipperDriver: foot or player gone, removing " + name); Destroy(gameObject); return; }
            if (_builtPlace != _placeVersion) Build();
            else if (_builtColour != _colourVersion) Colour();
            if (Time.time >= _nextDiag && _root != null)
            {
                _nextDiag = Time.time + DiagEverySeconds;
                var r = _root.GetComponentInChildren<SkinnedMeshRenderer>();
                var b = r != null ? r.bounds : new Bounds();
                ModLog.Verbose("SlipperDriver: " + name + " bounds centre " + b.center.ToString("F2") + " size " + b.size.ToString("F3") + " m, foot " + _foot.position.ToString("F2"));
                ModLog.CheckRange("SlipperDriver: " + name + " distance bounds centre -> foot", (b.center - _foot.position).magnitude, 0f, 0.4f, "m");
            }
        }

        /// Changes every time the slipper is rebuilt (the troll on it re-binds then).
        public int BuildVersion { get { return _buildVersion; } }

        /// D-147: the socket on the built slipper. False = prefab/bundle has no TrollSocket.
        public bool HasSocket { get { return _root != null && _root.Find(SlipperFit.SocketName) != null; } }

        /// The socket's skin anchor (bound in the CURRENT pose) and the socket's forward expressed in the foot bone's space (so it turns with the foot). False without a socket.
        public bool TrySocket(out SkinPoint anchor, out Vector3 forwardInFoot)
        {
            anchor = null; forwardInFoot = Vector3.forward;
            var s = _root != null ? _root.Find(SlipperFit.SocketName) : null;
            if (s == null || _foot == null) return false;
            anchor = PointAt(s.position);
            forwardInFoot = Quaternion.Inverse(_foot.rotation) * s.forward;
            SocketUpInFoot = Quaternion.Inverse(_foot.rotation) * s.up;
            return anchor != null;
        }

        /// D-159: the socket's up in the foot bone's space (set by TrySocket).
        public Vector3 SocketUpInFoot = Vector3.up;

        /// D-160: the slipper's own vertical extent in the world (its skinned renderer's bounds), not the foot bone. False when there is no renderer.
        public bool TryExtent(out float bottomY, out float topY)
        {
            bottomY = topY = 0f;
            var r = _root != null ? _root.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            if (r == null) return false;
            bottomY = r.bounds.min.y; topY = r.bounds.max.y;
            return true;
        }

        /// A point that follows the slipper / foot skin like a slipper vertex at `world` would, bound in the CURRENT pose (the troll's anchor).
        /// Null when the foot is gone.
        public SkinPoint PointAt(Vector3 world)
        {
            if (_foot == null) return null;
            if (_skin != null && _skin.Refresh() > 0)
            {
                var sp = _skin.PointAt(world);
                if (sp != null) return sp;
            }
            float w = _toe != null ? FitGeometry.ToeWeight(FitGeometry.ToeAlong(world, _toe.position, TrollFitMath.FootForward(_foot, _toe, _player.forward))) : 0f;
            return new SkinPoint(world, new[] { _foot, _toe }, new[] { 1f - w, w }, _toe != null ? 2 : 1);
        }

        void OnDestroy()
        {
            if (_skin != null) _skin.Dispose();
            foreach (var m in _own) if (m != null) Destroy(m);
            ModLog.Verbose("SlipperDriver: " + name + " destroyed");
        }
    }
}
