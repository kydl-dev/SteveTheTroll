using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// T-008d step 1 (D-038 / D-044, "Go farm"): a tamed troll gathers its focus item. It looks for the nearest source within FarmRange of
    /// where it was sent (then of the owner's bed): standing trees / logs (TreeBase -> m_logPrefab -> TreeLog -> m_subLogPrefab ... drops),
    /// rocks (MineRock5 / MineRock) and other destructibles (DropOnDestroyed) whose drop tables contain the item. No focus = the D-038
    /// priority (copper, core wood / pine, fine wood / birch, tin, wood). Steering: postfix on MonsterAI.UpdateAI (the last MoveTo of the frame
    /// wins -> since D-048 a prefix that replaces the AI step while farming); a fight (target creature) or a follow order ends / pauses farming. Drops stay on the ground until the backpack (T-008c) exists.
    /// </summary>
    internal static class TrollFarming
    {
        internal static readonly string[] Priority = { "CopperOre", "RoundLog", "FineWood", "TinOre", "Wood" };
        static readonly System.Func<BaseAI, float, Vector3, float, bool, bool> MoveTo =
            AccessTools.MethodDelegate<System.Func<BaseAI, float, Vector3, float, bool, bool>>(AccessTools.Method(typeof(BaseAI), "MoveTo"));
        static readonly System.Action<BaseAI, Vector3> LookAt = AccessTools.MethodDelegate<System.Action<BaseAI, Vector3>>(AccessTools.Method(typeof(BaseAI), "LookAt"));
        // D-051: the search walks the game's list of loaded networked objects (ZNetScene) instead of a physics overlap: no buffer to fill up
        // (TEST-021), any radius up to the loaded area (150 m)
        static readonly AccessTools.FieldRef<ZNetScene, Dictionary<ZDO, ZNetView>> Instances = AccessTools.FieldRefAccess<ZNetScene, Dictionary<ZDO, ZNetView>>("m_instances");
        static readonly Dictionary<int, FarmSourceCache> Known = new Dictionary<int, FarmSourceCache>();
        const float MinOtherHeight = 1.2f;

        /// In-game test Startup_TamingReflection. Expected outcome: whether both delegates were found.
        internal static bool SelfCheck() { return MoveTo != null && LookAt != null; }

        /// "Go farm" (owner of the troll's ZDO only). Expected outcome: returns whether the troll now farms.
        internal static bool Begin(Character troll, string focus)
        {
            var view = troll != null ? troll.GetComponent<ZNetView>() : null;
            var ai = troll != null ? troll.GetComponent<MonsterAI>() : null;
            if (ai == null || view == null || !view.IsValid() || !view.IsOwner()) { ModLog.Warn("TrollFarming: cannot send " + (troll != null ? troll.name : "?") + " (ai " + (ai != null) + ", owner " + (view != null && view.IsOwner()) + ")"); return false; }
            ai.SetFollowTarget(null);
            ai.SetPatrolPoint();
            var arrival = troll.GetComponent<TrollArrival>();
            if (arrival != null) Object.Destroy(arrival);
            var f = troll.GetComponent<TrollFarmer>();
            if (f == null) f = troll.gameObject.AddComponent<TrollFarmer>();
            f.Setup(focus, troll.transform.position);
            ModLog.Info("TrollFarming: " + troll.name + " farms '" + (focus.Length > 0 ? focus : "priority " + string.Join(" > ", Priority)) + "' around " + troll.transform.position.ToString("F0"));
            return troll.GetComponent<TrollFarmer>() != null;
        }

        /// The item the troll farms now ("" = not farming).
        internal static string Farming(Character troll)
        {
            var f = troll != null ? troll.GetComponent<TrollFarmer>() : null;
            return f != null ? f.CurrentItem : "";
        }

        /// The source's drop table contains `item` (through felled trees and their log pieces, 4 levels deep).
        internal static bool Yields(GameObject source, string item, int depth = 0)
        {
            if (source == null || depth > 4) return false;
            var tree = source.GetComponent<TreeBase>();
            if (tree != null) return Has(tree.m_dropWhenDestroyed, item) || Yields(tree.m_logPrefab, item, depth + 1);
            var log = source.GetComponent<TreeLog>();
            if (log != null) return Has(log.m_dropWhenDestroyed, item) || Yields(log.m_subLogPrefab, item, depth + 1);
            var rock5 = source.GetComponent<MineRock5>();
            if (rock5 != null) return Has(rock5.m_dropItems, item);
            var rock = source.GetComponent<MineRock>();
            if (rock != null) return Has(rock.m_dropItems, item);
            var drop = source.GetComponent<DropOnDestroyed>();
            return drop != null && Has(drop.m_dropWhenDestroyed, item);
        }

        static bool Has(DropTable t, string item)
        {
            if (t == null || t.m_drops == null) return false;
            foreach (var d in t.m_drops) if (d.m_item != null && d.m_item.name == item) return true;
            return false;
        }

        internal static string KindOf(GameObject g)
        {
            if (g.GetComponent<TreeLog>() != null) return "log";
            if (g.GetComponent<TreeBase>() != null) return "tree";
            if (g.GetComponent<MineRock5>() != null || g.GetComponent<MineRock>() != null) return "rock";
            return "other";
        }

        static int MinTier(GameObject g)
        {
            var tree = g.GetComponent<TreeBase>(); if (tree != null) return tree.m_minToolTier;
            var log = g.GetComponent<TreeLog>(); if (log != null) return log.m_minToolTier;
            var r5 = g.GetComponent<MineRock5>(); if (r5 != null) return r5.m_minToolTier;
            var r = g.GetComponent<MineRock>(); if (r != null) return r.m_minToolTier;
            var d = g.GetComponent<Destructible>(); return d != null ? d.m_minToolTier : 0;
        }



        /// Nearest source of `item` within range of `center` the troll's weapon tier can damage. Expected outcome: the source or null.


        /// The last search, for the in-game test World_FarmScan.
        internal static string LastScan = "";
        internal static bool LastScanFull;

        internal static GameObject Find(Vector3 center, Vector3 from, string item, int tier, float range, out Collider hit, System.Func<GameObject, bool> skip = null)
        {
            hit = null;
            GameObject best = null;
            float bestD = float.MaxValue, r2 = range * range;
            var counts = new int[3];
            int seen = 0, skipped = 0;
            var scene = ZNetScene.instance;
            var all = scene != null ? Instances(scene) : null;
            if (all == null) { LastScan = "no ZNetScene"; LastScanFull = false; return null; }
            foreach (var v in all.Values)
            {
                if (v == null) continue;
                var pos = v.transform.position;
                if ((pos - center).sqrMagnitude > r2) continue;
                seen++;
                var src = Source(v);
                if (src == null || src.Collider == null) continue;
                var g = v.gameObject;
                if ((skip != null && skip(g)) || src.MinTier > tier || !src.Yields(item)) continue;
                if (src.Tier == TamingRules.TierNone) { skipped++; continue; }
                counts[src.Tier]++;
                var at = src.IsTree ? pos : src.Collider.bounds.ClosestPoint(from);     // trees: the trunk (TEST-020)
                float d = TamingRules.SourceScore(src.Tier, Vector3.Distance(from, at));
                if (d < bestD) { bestD = d; best = g; hit = src.Collider; }
            }
            LastScan = "objects " + seen + ", logs/rocks " + counts[0] + ", trees " + counts[1] + ", other " + counts[2] + ", too small " + skipped
                + ", best " + (best != null ? best.name + " (" + KindOf(best) + ")" : "none");
            LastScanFull = false;
            ModLog.Verbose("TrollFarming: search '" + item + "' within " + range + " m of " + center.ToString("F0") + ": " + LastScan + (best != null ? " score " + bestD.ToString("F1") : ""));
            return best;
        }

        // The source data of a networked object (null = not a farm source), cached per object.
        static FarmSourceCache Source(ZNetView v)
        {
            int id = v.GetInstanceID();
            FarmSourceCache c;
            if (Known.TryGetValue(id, out c)) return c;
            var g = v.gameObject;
            bool tree = g.GetComponent<TreeBase>() != null, log = g.GetComponent<TreeLog>() != null;
            bool rock = g.GetComponent<MineRock5>() != null || g.GetComponent<MineRock>() != null;
            bool other = !tree && !log && !rock && g.GetComponent<Destructible>() != null;
            if (Known.Count > 20000) Known.Clear();
            if (!tree && !log && !rock && !other) { Known[id] = null; return null; }
            c = g.GetComponent<FarmSourceCache>();
            if (c == null) c = g.AddComponent<FarmSourceCache>();
            c.Collider = Biggest(g);
            c.IsTree = tree;
            c.MinTier = MinTier(g);
            c.Tier = TamingRules.Tier(log, rock, tree, c.Collider != null ? c.Collider.bounds.size.y : 0f, MinOtherHeight);
            Known[id] = c;
            return c;
        }

        static Collider Biggest(GameObject g)
        {
            Collider best = null;
            float size = -1f;
            foreach (var col in g.GetComponentsInChildren<Collider>())
            {
                if (col == null || col.isTrigger) continue;
                float sz = col.bounds.size.sqrMagnitude;
                if (sz > size) { size = sz; best = col; }
            }
            return best;
        }

        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
        static class Steer
        {
            // D-048: a PREFIX now - while the troll farms, the game's AI step (idle wandering, the feeding animation, its own attack timer) is
            // skipped completely; it runs again when farming hands back (fight, follow order, nothing to farm).
            static bool Prefix(MonsterAI __instance, float dt, ref bool __result)
            {
                var f = __instance != null ? __instance.GetComponent<TrollFarmer>() : null;
                if (f == null) return true;
                var view = __instance.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner()) return true;
                bool drove = f.Step(__instance, dt, MoveTo, LookAt);
                if (drove) __result = true;
                return !drove;
            }
        }
    }

    /// Remembers per source object which items it yields (drop tables never change at runtime), its biggest collider and its tier.
    internal sealed class FarmSourceCache : MonoBehaviour
    {
        internal Collider Collider;
        internal bool IsTree;
        internal int MinTier, Tier = TamingRules.TierNone;
        readonly Dictionary<string, bool> _yields = new Dictionary<string, bool>();

        internal bool Yields(string item)
        {
            bool y;
            if (!_yields.TryGetValue(item, out y)) { y = TrollFarming.Yields(gameObject, item); _yields[item] = y; }
            return y;
        }
    }
}
