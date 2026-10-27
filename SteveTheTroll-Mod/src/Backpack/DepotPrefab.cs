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

        internal static void Install() { PrefabManager.OnVanillaPrefabsAvailable += Build; }

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
            foreach (var p in DepotData.Frame)
            {
                var prefab = PrefabManager.Instance.GetPrefab(p.Prefab);
                if (prefab == null) { missing++; ModLog.Warn("DepotPrefab: piece '" + p.Prefab + "' not found"); continue; }
                var copy = BackpackVisual.VisualCopy(prefab, out int r);
                copy.transform.SetParent(go.transform, false);
                copy.transform.localPosition = new Vector3(p.X, p.Y, p.Z);
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
