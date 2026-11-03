using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks
{
    /// <summary>D-166: TestLab hooks for the baked slipper skin (suite slipper-skin).</summary>
    internal static class SlipperTestHooks
    {
        /// D-166: the slippers are a skinned mesh with the trolls baked on (prefab SlipperSkinParts, attach_skin): what the local player wears now, as "n skin(s), 'height' m high, bottom x m above the feet".
        [TestHook("stt.slipperSkins")]
        static string SlipperSkins()
        {
            var p = Player.m_localPlayer;
            if (p == null) return "no player";
            int n = 0; float lo = float.MaxValue, hi = float.MinValue; int bones = 0;
            foreach (var r in p.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name != "SlipperSkin" || r.sharedMesh == null) continue;
                n++; bones = r.bones.Length;
                float l, h; MeshSpan(r, out l, out h); lo = Mathf.Min(lo, l); hi = Mathf.Max(hi, h);
            }
            return n == 0 ? "no slipper skin" : n + " skin(s), " + bones + " bones, " + (hi - lo).ToString("F3") + " m high, bottom " + (lo - p.transform.position.y).ToString("F3") + " m above the feet";
        }

        /// World-space lowest / highest point of the skinned mesh AS POSED NOW (BakeMesh without scale, then the renderer's position + rotation).
        /// Not `r.bounds`: for a SkinnedMeshRenderer that is the mesh's stored local box around the root bone, not the skinned vertices - the
        /// 2026-11-02 lab run read 0.46 m 'above the feet' from it while the screenshot showed the slippers on the feet (Claude-Agent-19).
        static void MeshSpan(SkinnedMeshRenderer r, out float lo, out float hi)
        {
            lo = float.MaxValue; hi = float.MinValue;
            var baked = new Mesh();
            try
            {
                r.BakeMesh(baked);      // useScale false = world-SIZED vertices in the renderer's rotated / translated frame (MISTAKES 2026-10-27); true scaled them again (0.004 m)
                var t = r.transform;
                foreach (var v in baked.vertices) { float y = (t.position + t.rotation * v).y; if (y < lo) lo = y; if (y > hi) hi = y; }
                var b = r.bounds;
                ModLog.Verbose("TestHooks: slipper skin baked y " + lo.ToString("F3") + " .. " + hi.ToString("F3") + " m (" + baked.vertexCount + " verts, renderer lossyScale " + t.lossyScale.ToString("F3") + "); renderer bounds y " + b.min.y.ToString("F3") + " .. " + b.max.y.ToString("F3") + " m (not used)");
            }
            finally { Object.Destroy(baked); }
        }

        /// Exactly one slipper skin on the player, all its bones exist, it is 0.2 - 0.8 m high (slippers plus the baked trolls) and stands on the player's feet (not under them).
        [TestHook("stt.slipperSkinsOk")]
        static bool SlipperSkinsOk()
        {
            var p = Player.m_localPlayer;
            if (p == null) return false;
            int n = 0; bool ok = true;
            foreach (var r in p.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name != "SlipperSkin" || r.sharedMesh == null) continue;
                n++;
                foreach (var b in r.bones) if (b == null) ok = false;
                float lo, hi; MeshSpan(r, out lo, out hi);
                float h = hi - lo, bottom = lo - p.transform.position.y;
                ModLog.Check("TestHooks: slipper skin height (slippers + baked trolls)", h, 0.5, 0.3, "m");
                ModLog.Check("TestHooks: slipper skin bottom above the feet", bottom, 0.0, 0.08, "m");
                if (h < 0.2f || h > 0.8f || Mathf.Abs(bottom) > 0.1f || !r.enabled || !r.gameObject.activeInHierarchy) ok = false;
            }
            return n == 1 && ok;
        }
    }
}
