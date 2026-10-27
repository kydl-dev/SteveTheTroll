using UnityEngine;

namespace SteveTheTroll.Backpack
{
    /// <summary>D-064: copies of a building piece's solid colliders (no networking, no WearNTear) for the troll depot frame.</summary>
    internal static class PieceCopy
    {
        /// Copies the non-trigger colliders of the piece (not those of its worn / broken looks) under `to`. Expected outcome: how many.
        internal static int Colliders(GameObject prefab, Transform to)
        {
            var src = prefab.transform;
            var wnt = prefab.GetComponent<WearNTear>();
            int n = 0;
            foreach (var col in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (col.isTrigger || Skip(wnt, col.transform)) continue;
                var part = new GameObject(col.name + "_col");
                part.layer = col.gameObject.layer;
                part.transform.SetParent(to, false);
                part.transform.localPosition = src.InverseTransformPoint(col.transform.position);
                part.transform.localRotation = Quaternion.Inverse(src.rotation) * col.transform.rotation;
                var ls = col.transform.lossyScale; var ps = src.lossyScale;
                part.transform.localScale = new Vector3(ls.x / ps.x, ls.y / ps.y, ls.z / ps.z);
                var box = col as BoxCollider; var sph = col as SphereCollider; var cap = col as CapsuleCollider; var mesh = col as MeshCollider;
                if (box != null) { var c = part.AddComponent<BoxCollider>(); c.center = box.center; c.size = box.size; }
                else if (sph != null) { var c = part.AddComponent<SphereCollider>(); c.center = sph.center; c.radius = sph.radius; }
                else if (cap != null) { var c = part.AddComponent<CapsuleCollider>(); c.center = cap.center; c.radius = cap.radius; c.height = cap.height; c.direction = cap.direction; }
                else if (mesh != null && mesh.sharedMesh != null) { var c = part.AddComponent<MeshCollider>(); c.sharedMesh = mesh.sharedMesh; c.convex = mesh.convex; }
                else { Object.Destroy(part); continue; }
                n++;
            }
            return n;
        }

        // Colliders of the worn / broken look (when those are separate from the new look) are left out.
        static bool Skip(WearNTear wnt, Transform t)
        {
            if (wnt == null) return false;
            if (wnt.m_new != null && t.IsChildOf(wnt.m_new.transform)) return false;
            return (wnt.m_worn != null && t.IsChildOf(wnt.m_worn.transform)) || (wnt.m_broken != null && t.IsChildOf(wnt.m_broken.transform));
        }
    }
}
