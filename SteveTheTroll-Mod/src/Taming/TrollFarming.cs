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
    /// wins); a fight (target creature) or a follow order ends / pauses farming. Drops stay on the ground until the backpack (T-008c) exists.
    /// </summary>
    internal static class TrollFarming
    {
        internal static readonly string[] Priority = { "CopperOre", "RoundLog", "FineWood", "TinOre", "Wood" };
        static readonly System.Func<BaseAI, float, Vector3, float, bool, bool> MoveTo =
            AccessTools.MethodDelegate<System.Func<BaseAI, float, Vector3, float, bool, bool>>(AccessTools.Method(typeof(BaseAI), "MoveTo"));
        static readonly System.Action<BaseAI, Vector3> LookAt = AccessTools.MethodDelegate<System.Action<BaseAI, Vector3>>(AccessTools.Method(typeof(BaseAI), "LookAt"));
        static readonly Collider[] Hits = new Collider[512];

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

        // The object that owns the source component (trees / rocks have their colliders on children).
        static GameObject SourceOf(Collider c)
        {
            if (c == null) return null;
            Component s = c.GetComponentInParent<TreeBase>();
            if (s == null) s = c.GetComponentInParent<TreeLog>();
            if (s == null) s = c.GetComponentInParent<MineRock5>();
            if (s == null) s = c.GetComponentInParent<MineRock>();
            if (s == null) s = c.GetComponentInParent<Destructible>();
            return s != null ? s.gameObject : null;
        }

        /// Nearest source of `item` within range of `center` the troll's weapon tier can damage. Expected outcome: the source or null.
        /// Ranking (metres added to the distance): fallen logs 0, standing trees 4, ore rocks 0, anything else (stumps, debris) 40.
        internal static float Penalty(GameObject g)
        {
            if (g.GetComponent<TreeLog>() != null || g.GetComponent<MineRock5>() != null || g.GetComponent<MineRock>() != null) return 0f;
            if (g.GetComponent<TreeBase>() != null) return 4f;
            return 40f;
        }

        internal static GameObject Find(Vector3 center, Vector3 from, string item, int tier, float range, out Collider hit, GameObject lastBad = null)
        {
            hit = null;
            GameObject best = null;
            float bestD = float.MaxValue;
            int n = Physics.OverlapSphereNonAlloc(center, range, Hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var g = SourceOf(Hits[i]);
                if (g == null || g == lastBad || MinTier(g) > tier) continue;
                var cache = g.GetComponent<FarmSourceCache>();
                if (cache == null) cache = g.AddComponent<FarmSourceCache>();
                if (!cache.Yields(item)) continue;
                // TEST-018: Collider.ClosestPoint on a non-convex mesh returned the troll's own position (0.0 m to a stump) -> distances from
                // the collider BOUNDS; and a ranking so trees / logs / ore rocks win over stumps and small debris
                float d = Vector3.Distance(from, Hits[i].bounds.ClosestPoint(from)) + Penalty(g);
                if (d < bestD) { bestD = d; best = g; hit = Hits[i]; }
            }
            ModLog.Verbose("TrollFarming: search '" + item + "' within " + range + " m of " + center.ToString("F0") + ": " + n + " collider(s), best " + (best != null ? best.name + " (" + KindOf(best) + ") score " + bestD.ToString("F1") + " m" : "none"));
            return best;
        }

        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
        static class Steer
        {
            static void Postfix(MonsterAI __instance, float dt, bool __result)
            {
                if (!__result || __instance == null) return;
                var f = __instance.GetComponent<TrollFarmer>();
                if (f != null) f.Step(__instance, dt, MoveTo, LookAt);
            }
        }
    }

    /// Remembers per source object which items it yields (drop tables never change at runtime).
    internal sealed class FarmSourceCache : MonoBehaviour
    {
        readonly Dictionary<string, bool> _yields = new Dictionary<string, bool>();

        internal bool Yields(string item)
        {
            bool y;
            if (!_yields.TryGetValue(item, out y)) { y = TrollFarming.Yields(gameObject, item); _yields[item] = y; }
            return y;
        }
    }
}
