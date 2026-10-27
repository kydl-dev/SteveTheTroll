using System.Collections.Generic;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Figure
{
    /// <summary>
    /// Builds a VISUAL-ONLY copy of the game's own Troll at runtime (D-013): the "Visual" child of the loaded ZNetScene prefab
    /// "Troll" (Armature + Body + Hair) and the troll's own log (ObjectDB item "troll_log_swing_v", child "attach") in its RightHand.
    /// Nothing of Iron Gate's is shipped: the meshes / materials come from the running game. Every behaviour component is removed
    /// BEFORE the copy is activated (built under an inactive holder), so no troll script ever runs on it.
    /// </summary>
    internal static class TrollFigureFactory
    {
        public const string TrollPrefab = "Troll";
        public const string LogItemPrefab = "troll_log_swing_v";
        const string VisualChild = "Visual";
        const string LogAttachChild = "attach";
        const string HandBone = "RightHand";

        /// Expected outcome: an INACTIVE holder with the stripped copy and at least one renderer, or null with the reason logged.
        public static GameObject Build(string name)
        {
            ModLog.Verbose("TrollFigureFactory: Build '" + name + "' start");
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(TrollPrefab) : null;
            if (prefab == null) { ModLog.Warn("TrollFigureFactory: prefab '" + TrollPrefab + "' not found (ZNetScene " + (ZNetScene.instance != null ? "ready" : "null") + ")"); return null; }
            var visual = prefab.transform.Find(VisualChild);
            if (visual == null) { ModLog.Warn("TrollFigureFactory: '" + TrollPrefab + "' has no child '" + VisualChild + "'"); return null; }

            var holder = new GameObject(name);
            holder.SetActive(false);                                   // nothing in the copy may Awake before it is stripped
            var copy = Object.Instantiate(visual.gameObject, holder.transform, false);
            copy.name = "TrollVisual";
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            ModLog.Verbose("TrollFigureFactory: copied '" + TrollPrefab + "/" + VisualChild + "' local scale " + copy.transform.localScale.ToString("F3")
                + ", " + copy.GetComponentsInChildren<Transform>(true).Length + " transforms");

            int removed = Strip(copy);
            var log = AddLog(copy.transform);
            if (log != null) removed += Strip(log);

            var renderers = holder.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                ModLog.Verbose("TrollFigureFactory: renderer '" + r.name + "' " + r.GetType().Name + ", layer " + LayerMask.LayerToName(r.gameObject.layer)
                    + ", material " + (r.sharedMaterial != null ? r.sharedMaterial.name + " / " + r.sharedMaterial.shader.name : "none"));
            }
            ModLog.Verbose("TrollFigureFactory: Build '" + name + "' done: " + removed + " component(s) removed, " + renderers.Length + " renderer(s), log " + (log != null ? "in " + HandBone : "MISSING"));
            ModLog.CheckRange("TrollFigureFactory: renderer count of '" + name + "'", renderers.Length, 2, 10, "");
            if (renderers.Length > 0) return holder;
            Object.Destroy(holder);
            ModLog.Warn("TrollFigureFactory: the copy has no renderer, discarded");
            return null;
        }

        // The troll's own log, placed like VisEquipment.AttachItem does: the item's "attach" child under the joint at local zero.
        static GameObject AddLog(Transform figure)
        {
            var hand = Utils.FindChild(figure, HandBone);
            var item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(LogItemPrefab) : null;
            var attach = item != null ? item.transform.Find(LogAttachChild) : null;
            ModLog.Verbose("TrollFigureFactory: AddLog hand " + (hand != null ? hand.name : "null") + ", item " + (item != null ? item.name : "null") + ", attach " + (attach != null ? "found" : "null"));
            if (hand == null || attach == null) { ModLog.Warn("TrollFigureFactory: no log (hand " + (hand != null) + ", '" + LogItemPrefab + "/" + LogAttachChild + "' " + (attach != null) + ")"); return null; }
            var log = Object.Instantiate(attach.gameObject, hand, false);
            log.name = "TrollLog";
            log.transform.localPosition = Vector3.zero;
            log.transform.localRotation = Quaternion.identity;
            ModLog.Verbose("TrollFigureFactory: log attached to " + hand.name + ", local scale " + log.transform.localScale.ToString("F3"));
            return log;
        }

        /// Removes everything but transforms and renderers (behaviours first: they can require the Animator / colliders).
        /// Expected outcome: none of those component types is left; returns how many were removed.
        public static int Strip(GameObject root)
        {
            int removed = 0;
            var order = new[] { typeof(MonoBehaviour), typeof(Animator), typeof(LODGroup), typeof(Collider), typeof(Rigidbody), typeof(ParticleSystem), typeof(AudioSource), typeof(Light) };
            var kinds = new Dictionary<string, int>();
            foreach (var type in order)
            {
                foreach (var c in root.GetComponentsInChildren(type, true))
                {
                    var key = c.GetType().Name;
                    int n; kinds.TryGetValue(key, out n); kinds[key] = n + 1;
                    Object.DestroyImmediate(c);
                    removed++;
                }
            }
            var parts = new List<string>();
            foreach (var kv in kinds) parts.Add(kv.Key + " x" + kv.Value);
            ModLog.Verbose("TrollFigureFactory: Strip '" + root.name + "' removed " + removed + ": " + (parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "nothing"));
            int left = 0;
            foreach (var type in order) left += root.GetComponentsInChildren(type, true).Length;
            if (left > 0) ModLog.Warn("TrollFigureFactory: Strip '" + root.name + "' left " + left + " component(s) (DestroyImmediate refused one?)");
            return removed - left;
        }
    }
}
