using System.Collections.Generic;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Item
{
    /// <summary>
    /// The look of the item lying on the ground: the cloned belt keeps its ground model in the child "attach" (worn parts are "attach_*").
    /// Its belt mesh is replaced by a pair of plush slippers from the bundle prefab "SlipperParts" (static meshes, toe at -Z) plus one box
    /// collider around both, so the item still lands, rolls and can be picked up. Colours use the slipper brightness from the config.
    /// </summary>
    internal static class SlipperDropModel
    {
        const string PartsPrefab = BundleAssets.SlipperParts;
        const string GroundChild = "attach";
        const float PairGap = 0.015f;        // m between the two slippers
        const float PairToeIn = 8f;          // deg each slipper turns inwards (looks placed, not lined up)

        /// Expected outcome: "attach" holds 2 slippers and a collider (returns that check; false keeps whatever was there).
        public static bool Build(GameObject itemPrefab)
        {
            var parts = BundleAssets.Load<GameObject>(PartsPrefab);
            var ground = itemPrefab != null ? itemPrefab.transform.Find(GroundChild) : null;
            if (parts == null || ground == null) { ModLog.Warn("SlipperDropModel: parts prefab " + (parts != null) + ", ground child '" + GroundChild + "' " + (ground != null) + " - the dropped item keeps the belt look"); return false; }

            int layer = ground.gameObject.layer;
            var old = new List<GameObject>();
            foreach (Transform c in ground) { old.Add(c.gameObject); layer = c.gameObject.layer; }
            foreach (var go in old) Object.DestroyImmediate(go, true);
            foreach (var col in ground.GetComponents<Collider>()) Object.DestroyImmediate(col, true);
            var lod = ground.GetComponent<LODGroup>();
            if (lod != null) Object.DestroyImmediate(lod, true);
            ModLog.Verbose("SlipperDropModel: removed " + old.Count + " belt part(s) under " + GroundChild + ", layer " + LayerMask.LayerToName(layer));

            var mats = new Dictionary<Material, Material>();
            Bounds width = MeshBounds(parts);
            float dx = Fit.FitGeometry.PairOffset(width.size.x, PairGap);
            var left = AddSlipper(parts, ground, "SlipperDropLeft", new Vector3(-dx, 0f, 0f), PairToeIn, layer, mats);
            var right = AddSlipper(parts, ground, "SlipperDropRight", new Vector3(dx, 0f, 0f), -PairToeIn, layer, mats);

            var box = ground.gameObject.AddComponent<BoxCollider>();
            var all = new Bounds(ground.InverseTransformPoint(left.position), Vector3.zero);
            foreach (var r in ground.GetComponentsInChildren<MeshFilter>(true)) Encapsulate(ref all, ground, r);
            box.center = all.center;
            box.size = all.size;
            ModLog.Verbose("SlipperDropModel: 2 slippers (width " + width.size.x.ToString("F3") + " m), collider centre " + box.center.ToString("F3") + " size " + box.size.ToString("F3") + " m");
            ModLog.CheckRange("SlipperDropModel: collider length", box.size.z, 0.1, 0.6, "m");
            bool ok = left != null && right != null && box.size.sqrMagnitude > 0f;
            return ok;
        }

        static Transform AddSlipper(GameObject parts, Transform ground, string name, Vector3 pos, float yaw, int layer, Dictionary<Material, Material> mats)
        {
            var go = Object.Instantiate(parts, ground, false);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var shared = mr.sharedMaterials;
                for (int i = 0; i < shared.Length; i++) shared[i] = Tinted(shared[i], mr.name, mats);
                mr.sharedMaterials = shared;
            }
            return go.transform;
        }

        /// One tinted copy per source material: upper/lining x Brightness, rim x RimBrightness, sole unchanged (as SlipperDriver).
        static Material Tinted(Material src, string part, Dictionary<Material, Material> mats)
        {
            if (src == null || !src.HasProperty("_Color")) return src;
            Material copy;
            if (mats.TryGetValue(src, out copy)) return copy;
            float k = part == "SlipperRim" ? ModSettings.SlipperRimBrightness.Value : part == "SlipperUpper" ? ModSettings.SlipperBrightness.Value : 1f;
            copy = new Material(src) { name = src.name + "_drop" };
            var c = src.GetColor("_Color");
            copy.SetColor("_Color", new Color(c.r * k, c.g * k, c.b * k, c.a));
            mats[src] = copy;
            ModLog.Verbose("SlipperDropModel: material " + copy.name + " x" + k.ToString("F2"));
            return copy;
        }

        static Bounds MeshBounds(GameObject parts)
        {
            var b = new Bounds();
            bool first = true;
            foreach (var mf in parts.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (first) { b = mf.sharedMesh.bounds; first = false; } else b.Encapsulate(mf.sharedMesh.bounds);
            }
            return b;
        }

        static void Encapsulate(ref Bounds all, Transform ground, MeshFilter mf)
        {
            if (mf.sharedMesh == null) return;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                all.Encapsulate(ground.InverseTransformPoint(mf.transform.TransformPoint(Fit.FitGeometry.BoxCorner(mb, i))));
            }
        }
    }
}
