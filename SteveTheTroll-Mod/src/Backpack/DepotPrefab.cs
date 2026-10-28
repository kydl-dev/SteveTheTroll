using Jotunn.Entities;
using Jotunn.Managers;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Backpack
{
    /// <summary>
    /// D-064: the troll depot frame as our own networked prefab "STT_TrollDepot" (built once the game's prefabs exist): copies of the wood
    /// poles / floors (meshes + solid colliders, nothing that wears or breaks, no material refund), a Piece so the hammer can remove it.
    /// The 4 chests are the game's own reinforced chests, spawned next to it (TrollDepot) - the game allows one Container per networked object.
    /// </summary>
    internal static class DepotPrefab
    {
        internal const string Name = "STT_TrollDepot";
        /// Level height (m): the next depot of a stack stands here (top of the wood floors); measured when the prefab is built.
        internal static float Height = 1.05f;
        /// D-090 / D-095: how far the floors were lifted above the chest lids at build (-1 = not built yet; World_DepotGround checks it).
        internal static float Lift = -1f;

        const string FloorPrefab = "wood_floor";
        const float Clearance = 0.02f, MaxLift = 0.15f;

        internal static void Install() { PrefabManager.OnVanillaPrefabsAvailable += Build; }

        /// How far the floors go up so the chest lids (chest on y 0) stay below the floors' underside. 0 when they already do.
        static float FloorLift()
        {
            var chest = PrefabManager.Instance.GetPrefab(DepotData.ChestPrefab);
            var floor = PrefabManager.Instance.GetPrefab(FloorPrefab);
            if (chest == null || floor == null) { ModLog.Warn("DepotPrefab: chest / floor prefab missing - floors not lifted"); return 0f; }
            float chestBottom, chestTop, floorBottom, floorTop;
            MeshSpan(chest, out chestBottom, out chestTop);
            MeshSpan(floor, out floorBottom, out floorTop);
            float floorY = 0f;
            foreach (var p in DepotData.Frame) if (p.Prefab == FloorPrefab) { floorY = p.Y; break; }
            // D-095 (TEST-040: an inactive chest part measured 1.61 m and lifted the floors 0.77 m): only drawn parts count, and only the
            // floors' TOP face has to clear the lid (a lid inside the floor slab is hidden; the flicker was the two top faces at one height)
            float lift = DepotData.FloorLift(chestTop, floorY + floorTop, Clearance);
            if (lift > MaxLift) { ModLog.Warn("DepotPrefab: floor lift " + lift.ToString("F3") + " m is more than " + MaxLift + " m - measuring went wrong, capped"); lift = MaxLift; }
            ModLog.Info("DepotPrefab: chest lid at " + chestTop.ToString("F3") + " m, floor top at " + (floorY + floorTop).ToString("F3") + " m (underside " + (floorY + floorBottom).ToString("F3") + ") -> floors lifted " + lift.ToString("F3") + " m");
            return lift;
        }

        /// Lowest / highest mesh point of a prefab in its own space (inactive prefabs have no renderer bounds).
        static void MeshSpan(GameObject prefab, out float bottom, out float top)
        {
            bottom = float.MaxValue; top = float.MinValue;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var r = mf.GetComponent<Renderer>();
                if (r == null || !r.enabled || !Drawn(mf.transform, prefab.transform)) continue;
                string n = mf.gameObject.name.ToLowerInvariant();
                if (n.Contains("lod1") || n.Contains("lod2") || n.Contains("lod3")) continue;
                var b = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    float y = prefab.transform.InverseTransformPoint(mf.transform.TransformPoint(corner)).y;
                    bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                }
            }
            if (bottom > top) { bottom = 0f; top = 0f; }
        }

        // Every part between the mesh and the prefab root is switched on (worn / broken / open variants are off in the prefab).
        static bool Drawn(Transform t, Transform root)
        {
            for (var c = t; c != null && c != root; c = c.parent) if (!c.gameObject.activeSelf) return false;
            return true;
        }

        static void Build()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Build;
            if (PrefabManager.Instance.GetPrefab(Name) != null) return;
            var go = PrefabManager.Instance.CreateEmptyPrefab(Name, false);
            foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
            foreach (var r in go.GetComponents<MeshRenderer>()) Object.DestroyImmediate(r);
            foreach (var f in go.GetComponents<MeshFilter>()) Object.DestroyImmediate(f);
            var view = go.GetComponent<ZNetView>();
            if (view == null) view = go.AddComponent<ZNetView>();
            view.m_persistent = true;
            int parts = 0, cols = 0, missing = 0;
            float top = 0f;
            // D-090 (user: "the top of the chest structure is flickering"): the reinforced chests' lids reached the floors' top face (z-fighting).
            // The floors are lifted until their underside clears the chest lids by Clearance.
            float lift = FloorLift();
            Lift = lift;
            foreach (var p in DepotData.Frame)
            {
                var prefab = PrefabManager.Instance.GetPrefab(p.Prefab);
                if (prefab == null) { missing++; ModLog.Warn("DepotPrefab: piece '" + p.Prefab + "' not found"); continue; }
                var copy = BackpackVisual.VisualCopy(prefab, out int r);
                copy.transform.SetParent(go.transform, false);
                copy.transform.localPosition = new Vector3(p.X, p.Y + (p.Prefab == FloorPrefab ? lift : 0f), p.Z);
                if (p.Prefab != FloorPrefab && lift > 0f)
                {
                    // poles grow by the lift (bottom stays where it was) so the floors still rest on them
                    float pb, pt;
                    MeshSpan(prefab, out pb, out pt);
                    if (pt - pb > 0.1f)
                    {
                        float s = 1f + lift / (pt - pb);
                        copy.transform.localScale = new Vector3(1f, s, 1f);
                        copy.transform.localPosition = new Vector3(p.X, p.Y + pb - pb * s, p.Z);
                    }
                }
                copy.transform.localRotation = Quaternion.Euler(0f, p.Yaw, 0f);
                cols += PieceCopy.Colliders(prefab, copy.transform);
                // the prefab container is inactive (no physics bounds): measure the meshes
                foreach (var mf in copy.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    var b = mf.sharedMesh.bounds;
                    for (int k = 0; k < 8; k++)
                    {
                        var corner = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                        top = Mathf.Max(top, go.transform.InverseTransformPoint(mf.transform.TransformPoint(corner)).y);
                    }
                }
                parts++;
            }
            if (top > 0.5f) Height = top;
            var piece = go.AddComponent<Piece>();
            piece.m_name = "Troll depot";
            piece.m_description = "Where a tamed troll empties its full backpack.";
            piece.m_canBeRemoved = true;
            piece.m_resources = new Piece.Requirement[0];
            PrefabManager.Instance.AddPrefab(new CustomPrefab(go, false));
            ModLog.Info("DepotPrefab: " + Name + " built: " + parts + " part(s), " + cols + " collider(s), " + missing + " missing, level height " + Height.ToString("F2") + " m");
            ModLog.Check("DepotPrefab: frame parts", parts, DepotData.Frame.Length, 0, "");
        }
    }
}
