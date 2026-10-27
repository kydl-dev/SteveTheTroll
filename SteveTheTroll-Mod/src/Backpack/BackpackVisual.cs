using System.Collections.Generic;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Backpack
{
    /// <summary>
    /// T-008b (D-053, D-054): the backpack on every tamed Troll, on every client (visual only): the user's construction (BackpackData) rebuilt from the
    /// game's own piece prefabs - only their mesh renderers (the "new" look of WearNTear pieces, LOD 0), no colliders, no networking.
    /// Placed each LateUpdate behind the troll's Spine2 in the troll's own frame (TrollBackpack Tunables: scale, back / up / side offset in
    /// metres, pitch / yaw / roll), so it moves with the body but does not twist with the spine. Storage comes in T-008c.
    /// </summary>
    internal sealed class BackpackVisual : MonoBehaviour
    {
        const string RootName = "STT_Backpack", Bone = "Spine2";
        const float ScanSeconds = 2f;
        static float _nextScan;
        static readonly List<Character> Trolls = new List<Character>();

        Transform _root, _spine;
        int _pieces;
        float _nextLog;

        /// Plugin.Update: adds the backpack to tamed trolls that have none and removes it from trolls that are no longer tamed.
        /// Expected outcome: returns how many backpacks were added this time.
        internal static int Tick()
        {
            if (Time.time < _nextScan || !ModSettings.On) return 0;
            _nextScan = Time.time + ScanSeconds;
            int added = 0;
            Trolls.Clear();
            foreach (var c in Character.GetAllCharacters()) if (c != null && Taming.TrollTaming.IsTroll(c)) Trolls.Add(c);
            foreach (var c in Trolls)
            {
                var v = c.GetComponent<BackpackVisual>();
                bool want = c.IsTamed() && !c.IsDead();
                if (want && v == null) { c.gameObject.AddComponent<BackpackVisual>(); added++; }
                else if (!want && v != null) { ModLog.Verbose("BackpackVisual: " + c.name + " is not tamed (any more) - backpack removed"); Destroy(v); }
            }
            return added;
        }

        void Start()
        {
            _spine = Utils.FindChild(transform, Bone);
            var root = new GameObject(RootName);
            _root = root.transform;
            _root.SetParent(transform, false);
            int missing = 0, renderers = 0;
            foreach (var p in BackpackData.Pieces)
            {
                var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(p.Prefab) : null;
                if (prefab == null) { missing++; ModLog.Warn("BackpackVisual: piece prefab '" + p.Prefab + "' not found"); continue; }
                var copy = VisualCopy(prefab, out int r);
                renderers += r;
                copy.transform.SetParent(_root, false);
                copy.transform.localPosition = new Vector3(p.X, p.Y, p.Z);
                copy.transform.localRotation = Quaternion.Euler(0f, p.Yaw, 0f);
                _pieces++;
            }
            Place();
            ModLog.Info("BackpackVisual: " + name + " backpack built: " + _pieces + " piece(s), " + renderers + " renderer(s), " + missing + " missing, bone " + (_spine != null ? Bone : "NOT FOUND (troll root used)"));
            ModLog.Check("BackpackVisual: " + name + " backpack pieces", _pieces, BackpackData.Pieces.Length, 0, "");
        }

        // A renderer-only copy of a piece: the WearNTear "new" look (or the whole prefab), LOD 0 only. Nothing networked is instantiated.
        static GameObject VisualCopy(GameObject prefab, out int renderers)
        {
            renderers = 0;
            var go = new GameObject(prefab.name);
            var src = prefab.transform;
            var wnt = prefab.GetComponent<WearNTear>();
            var from = wnt != null && wnt.m_new != null ? wnt.m_new.transform : src;
            HashSet<Renderer> lod0 = null;
            var lod = prefab.GetComponentInChildren<LODGroup>(true);
            if (lod != null && lod.GetLODs().Length > 0) lod0 = new HashSet<Renderer>(lod.GetLODs()[0].renderers);
            foreach (var r in from.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (lod0 != null && !lod0.Contains(r)) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var part = new GameObject(r.name);
                part.transform.SetParent(go.transform, false);
                part.transform.localPosition = src.InverseTransformPoint(r.transform.position);
                part.transform.localRotation = Quaternion.Inverse(src.rotation) * r.transform.rotation;
                var ls = r.transform.lossyScale; var ps = src.lossyScale;
                part.transform.localScale = new Vector3(ls.x / ps.x, ls.y / ps.y, ls.z / ps.z);
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                renderers++;
            }
            return go;
        }

        void LateUpdate()
        {
            if (_root == null) return;
            Place();
        }

        // Behind Spine2 in the troll's own frame (forward / up of the troll root); world scale = TrollBackpack Scale.
        void Place()
        {
            var anchor = _spine != null ? _spine.position : transform.position + transform.up * 3f;
            var rot = transform.rotation;
            var offset = new Vector3(ModSettings.BackpackSide.Value, ModSettings.BackpackUp.Value, -ModSettings.BackpackBack.Value);
            _root.position = anchor + rot * offset;
            // local -Z of the data (the struts) points at the troll: the backpack's +Z = the troll's backward
            _root.rotation = rot * Quaternion.Euler(ModSettings.BackpackPitch.Value, 180f + ModSettings.BackpackYaw.Value, ModSettings.BackpackRoll.Value);
            float parent = transform.lossyScale.x > 1e-4f ? transform.lossyScale.x : 1f;
            _root.localScale = Vector3.one * (ModSettings.BackpackScale.Value / parent);
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + 10f;
                ModLog.Verbose("BackpackVisual: " + name + " backpack at " + _root.position.ToString("F2") + ", " + (anchor - transform.position).y.ToString("F2") + " m above the troll's feet (spine), scale "
                    + ModSettings.BackpackScale.Value + " (parent " + parent.ToString("F3") + "), offset side / up / back " + offset.ToString("F2"));
            }
        }

        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
