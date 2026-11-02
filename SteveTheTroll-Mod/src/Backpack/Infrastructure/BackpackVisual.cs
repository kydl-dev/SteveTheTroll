using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Backpack.Domain;
using SteveTheTroll.Hubs.Infrastructure;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Backpack.Infrastructure
{
    /// <summary>
    /// T-008b (D-053, D-054, D-057): the backpack on every tamed Troll, on every client (visual only): the user's construction (BackpackData) rebuilt from the
    /// game's own piece prefabs - only their mesh renderers (the "new" look of WearNTear pieces, no lower LODs), no colliders, no networking.
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
        int _pieces, _chests;
        float _nextLog;

        /// Plugin.Update: adds the backpack to tamed trolls that have none and removes it from trolls that are no longer tamed.
        /// Expected outcome: returns how many backpacks were added this time.
        internal static int Tick()
        {
            if (Time.time < _nextScan || !ModSettings.On) return 0;
            _nextScan = Time.time + ScanSeconds;
            int added = 0;
            Trolls.Clear();
            foreach (var c in Character.GetAllCharacters()) if (c != null && TrollTaming.IsTroll(c)) Trolls.Add(c);
            foreach (var c in Trolls)
            {
                var v = c.GetComponent<BackpackVisual>();
                bool want = c.IsTamed() && !c.IsDead();
                if (want && v == null) { c.gameObject.AddComponent<BackpackVisual>(); added++; }
                else if (!want && v != null) { ModLog.Verbose("BackpackVisual: " + TrollTaming.Tag(c) + " is not tamed (any more) - backpack removed"); Destroy(v); }
            }
            return added;
        }

        void Start()
        {
            _spine = Utils.FindChild(transform, Bone);
            var root = new GameObject(RootName);
            _root = root.transform;
            _root.SetParent(transform, false);
            int missing = 0, renderers = 0, empty = 0;
            for (int idx = 0; idx < BackpackData.Pieces.Length; idx++)
            {
                var p = BackpackData.Pieces[idx];
                var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(p.Prefab) : null;
                if (prefab == null) { missing++; ModLog.Warn("BackpackVisual: piece prefab '" + p.Prefab + "' not found"); continue; }
                var copy = VisualCopy(prefab, out int r);
                renderers += r;
                if (r == 0) { empty++; ModLog.Warn("BackpackVisual: piece '" + p.Prefab + "' has no mesh to show"); }
                copy.transform.SetParent(_root, false);
                copy.transform.localPosition = new Vector3(p.X, p.Y, p.Z);
                copy.transform.localRotation = Quaternion.Euler(0f, p.Yaw, 0f);
                int chest = System.Array.IndexOf(BackpackData.Chests, idx);
                if (chest >= 0) { BackpackChest.Make(copy, GetComponent<Character>(), chest); _chests++; }
                _pieces++;
            }
            BackpackStore.Store(GetComponent<Character>());      // T-008c: the chests' storage (once per troll, every client)
            Place();
            ModLog.Check("BackpackVisual: " + name + " openable chests", _chests, BackpackData.Chests.Length, 0, "");
            ModLog.Info("BackpackVisual: " + name + " backpack built: " + _pieces + " piece(s), " + renderers + " renderer(s), " + missing + " missing, bone " + (_spine != null ? Bone : "NOT FOUND (troll root used)"));
            ModLog.Check("BackpackVisual: " + name + " backpack pieces", _pieces, BackpackData.Pieces.Length, 0, "");
            ModLog.Check("BackpackVisual: " + name + " pieces without a mesh", empty, 0, 0, "");
        }

        // A renderer-only copy of a piece: the WearNTear "new" look (or the whole prefab), no lower LODs. Nothing networked is instantiated.
        // D-057 (TEST-024: 50 pieces but only 35 renderers): the old code took the FIRST LODGroup of the prefab (often the worn / broken look)
        // as "LOD 0" and so dropped every renderer of the new look. Now only renderers listed in LOD 1+ of any LODGroup are skipped, and a piece
        // that still ends up empty falls back to the whole prefab.
        internal static GameObject VisualCopy(GameObject prefab, out int renderers)
        {
            var src = prefab.transform;
            var wnt = prefab.GetComponent<WearNTear>();
            var go = new GameObject(prefab.name);
            renderers = 0;
            if (wnt != null && wnt.m_new != null) renderers = CopyRenderers(wnt.m_new.transform, src, go.transform);
            if (renderers == 0) renderers = CopyRenderers(src, src, go.transform);
            return go;
        }

        /// Expected outcome: copies every mesh of `from` (MeshRenderer + SkinnedMeshRenderer, LOD 0 only) under `to`; returns how many.
        static int CopyRenderers(Transform from, Transform src, Transform to)
        {
            var lowerLods = new HashSet<Renderer>();
            foreach (var lod in src.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = lod.GetLODs();
                for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r != null) lowerLods.Add(r);
            }
            int n = 0;
            foreach (var r in from.GetComponentsInChildren<Renderer>(true))
            {
                if (lowerLods.Contains(r)) continue;
                Mesh mesh = null;
                if (r is MeshRenderer) { var mf = r.GetComponent<MeshFilter>(); if (mf != null) mesh = mf.sharedMesh; }
                else if (r is SkinnedMeshRenderer) mesh = ((SkinnedMeshRenderer)r).sharedMesh;
                if (mesh == null) continue;
                var part = new GameObject(r.name);
                part.transform.SetParent(to, false);
                part.transform.localPosition = src.InverseTransformPoint(r.transform.position);
                part.transform.localRotation = Quaternion.Inverse(src.rotation) * r.transform.rotation;
                var ls = r.transform.lossyScale; var ps = src.lossyScale;
                part.transform.localScale = new Vector3(ls.x / ps.x, ls.y / ps.y, ls.z / ps.z);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                n++;
            }
            return n;
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
