using SteveTheTroll.Taming;
using UnityEngine;

namespace SteveTheTroll.Trolls.Farming.Infrastructure
{
    /// <summary>
    /// Where a farming troll aims (D-059 / D-082), moved out of TrollFarmer unchanged (D-170, class size). Trees: the trunk at chest height. Logs and other long sources: the
    /// nearest point on the collider's long axis (D-059, TEST-025: a lying log's world bounds are a big box - the troll stood inside it, "0.0 m" away, and swung at nothing).
    /// Others: the nearest point of the collider bounds (its centre when the troll stands inside them). Always at chest height.
    /// </summary>
    internal static class FarmAim
    {
        /// Expected outcome: the aim point; `hit` becomes the source's nearest live hit area when it has one (a vein / big rock).
        internal static Vector3 Point(GameObject target, ref Collider hit, Vector3 me)
        {
            var area = TrollFarming.LiveArea(target, me);          // D-082
            if (area != null && area != hit) hit = area;
            var b = hit.bounds;
            Vector3 p, a, e;
            if (target.GetComponent<TreeBase>() != null) p = target.transform.position;
            else if (LongAxis(hit, out a, out e)) p = Vector3.Lerp(a, e, TamingRules.SegmentT(a.x, a.z, e.x, e.z, me.x, me.z));
            else { p = b.ClosestPoint(me); p.y = me.y; if ((p - me).sqrMagnitude < 0.01f) p = b.center; }
            p.y = Mathf.Clamp(me.y + 1.5f, b.min.y, b.max.y);
            return p;
        }

        // The long axis of an elongated collider (box / capsule / mesh), as two world points. Expected outcome: false when it is not clearly longer in one direction (then the bounds are used).
        static bool LongAxis(Collider c, out Vector3 a, out Vector3 b)
        {
            a = b = Vector3.zero;
            Vector3 centre, size;
            var box = c as BoxCollider; var cap = c as CapsuleCollider; var mesh = c as MeshCollider;
            if (box != null) { centre = box.center; size = box.size; }
            else if (cap != null) { centre = cap.center; size = Vector3.one * cap.radius * 2f; size[cap.direction] = Mathf.Max(cap.height, cap.radius * 2f); }
            else if (mesh != null && mesh.sharedMesh != null) { centre = mesh.sharedMesh.bounds.center; size = mesh.sharedMesh.bounds.size; }
            else return c == null;
            var ls = c.transform.lossyScale;
            var world = new Vector3(Mathf.Abs(size.x * ls.x), Mathf.Abs(size.y * ls.y), Mathf.Abs(size.z * ls.z));
            int k = world.x >= world.y && world.x >= world.z ? 0 : (world.y >= world.z ? 1 : 2);
            float longest = world[k], other = Mathf.Max(world[(k + 1) % 3], world[(k + 2) % 3]);
            if (longest < 1.5f * other) return longest >= 1.5f * other;
            var dir = Vector3.zero; dir[k] = Mathf.Max(0f, size[k] - Mathf.Min(size[(k + 1) % 3], size[(k + 2) % 3])) * 0.5f;
            a = c.transform.TransformPoint(centre - dir);
            b = c.transform.TransformPoint(centre + dir);
            return longest >= 1.5f * other;
        }
    }
}
