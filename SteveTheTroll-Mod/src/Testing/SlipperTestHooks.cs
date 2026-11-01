using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Testing
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
                var b = r.bounds; lo = Mathf.Min(lo, b.min.y); hi = Mathf.Max(hi, b.max.y);
            }
            return n == 0 ? "no slipper skin" : n + " skin(s), " + bones + " bones, " + (hi - lo).ToString("F3") + " m high, bottom " + (lo - p.transform.position.y).ToString("F3") + " m above the feet";
        }

        /// Exactly one slipper skin on the player, all its bones exist, it is about 0.3 - 0.5 m high and stands on the player's feet (not under them).
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
                var bb = r.bounds; float h = bb.size.y, bottom = bb.min.y - p.transform.position.y;
                ModLog.Check("TestHooks: slipper skin height", h, 0.4, 0.15, "m");
                ModLog.Check("TestHooks: slipper skin bottom above the feet", bottom, 0.0, 0.08, "m");
                if (h < 0.25f || h > 0.55f || Mathf.Abs(bottom) > 0.1f || !r.enabled || !r.gameObject.activeInHierarchy) ok = false;
            }
            return n == 1 && ok;
        }
    }
}
