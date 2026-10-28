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
    /// priority (D-058: TrollFocus.DefaultOrder - Yggdrasil wood, ancient bark, iron scrap, birch, silver, copper, pine, oak, tin, wood). Steering: postfix on MonsterAI.UpdateAI (the last MoveTo of the frame
    /// wins -> since D-048 a prefix that replaces the AI step while farming); a fight (target creature) or a follow order ends / pauses farming. Drops stay on the ground until the backpack (T-008c) exists.
    /// </summary>
    internal static class TrollFarming
    {
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
        internal static bool Begin(Character troll, string focus, bool announce = false)
        {
            var view = troll != null ? troll.GetComponent<ZNetView>() : null;
            var ai = troll != null ? troll.GetComponent<MonsterAI>() : null;
            if (ai == null || view == null || !view.IsValid() || !view.IsOwner()) { ModLog.Warn("TrollFarming: cannot send " + (troll != null ? troll.name : "?") + " (ai " + (ai != null) + ", owner " + (view != null && view.IsOwner()) + ")"); return false; }
            TrollTaming.StopFollow(ai);
            ai.SetPatrolPoint();
            var arrival = troll.GetComponent<TrollArrival>();
            if (arrival != null) Object.Destroy(arrival);
            var f = troll.GetComponent<TrollFarmer>();
            if (f == null) f = troll.gameObject.AddComponent<TrollFarmer>();
            f.Setup(focus, troll.transform.position, announce);
            ModLog.Info("TrollFarming: " + troll.name + " farms '" + (focus.Length > 0 ? focus : "default priority") + (announce ? " (no orders for a while)" : "") + "' around " + troll.transform.position.ToString("F0"));
            return troll.GetComponent<TrollFarmer>() != null;
        }

        /// T-008d / D-065 attack key on a tree / log / rock: farm THIS source first, then go on with the focus / default priority.
        /// Expected outcome: whether the troll now farms it.
        internal static bool BeginOn(Character troll, GameObject source, string focus)
        {
            if (!Begin(troll, focus)) return false;
            var f = troll.GetComponent<TrollFarmer>();
            return f != null && f.Force(source);
        }

        /// T-008e: walk to the owner's bed and wait there (silent). Expected outcome: whether the troll now walks home.
        internal static bool SendHome(Character troll)
        {
            var view = troll != null ? troll.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid() || !view.IsOwner()) return false;
            var arrival = troll.GetComponent<TrollArrival>();
            if (arrival != null) Object.Destroy(arrival);
            var f = troll.GetComponent<TrollFarmer>();
            if (f == null) f = troll.gameObject.AddComponent<TrollFarmer>();
            f.SetupHome();
            ModLog.Info("TrollFarming: " + troll.name + " walks to the owner's bed and waits");
            return true;
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

        /// D-084 performance: one farm-source candidate near a troll (gathered once per search, then filtered per goal / range).
        internal struct Candidate { internal GameObject G; internal FarmSourceCache Src; internal Vector3 Pos; }

        /// D-084 performance: every farm source within range of center in ONE pass over the scene (a search used to walk all networked
        /// objects once per goal and range - up to 30 passes over ~5000 objects). Expected outcome: the list filled (cleared first).
        internal static List<Candidate> Gather(Vector3 center, float range, List<Candidate> into)
        {
            into.Clear();
            var scene = ZNetScene.instance;
            var all = scene != null ? Instances(scene) : null;
            if (all == null) return into;
            float r2 = range * range;
            Vector3 bed;
            bool hasBed = TrollNoHit.Bed(out bed);
            float noHitR = ModSettings.NoHitRadius.Value;
            bool noHit = ModSettings.On && hasBed && noHitR > 0f;
            foreach (var v in all.Values)
            {
                if (v == null) continue;
                var pos = v.transform.position;
                if ((pos - center).sqrMagnitude > r2) continue;
                if (noHit && TrollNoHit.Inside(bed, pos, noHitR)) continue;            // D-097: nothing near the house is farmed
                var src = Source(v);
                if (src == null || src.Collider == null) continue;
                into.Add(new Candidate { G = v.gameObject, Src = src, Pos = pos });
            }
            return into;
        }

        internal static GameObject Find(Vector3 center, Vector3 from, Ui.TrollFocus.Goal goal, int tier, float range, out Collider hit, System.Func<GameObject, bool> skip = null)
        {
            return FindIn(Gather(center, range, new List<Candidate>()), center, from, goal, tier, range, out hit, skip);
        }

        /// The best source of the goal among gathered candidates within range of center (see Find). Expected outcome: the source or null.
        internal static GameObject FindIn(List<Candidate> cands, Vector3 center, Vector3 from, Ui.TrollFocus.Goal goal, int tier, float range, out Collider hit, System.Func<GameObject, bool> skip = null)
        {
            string item = goal.Item;
            hit = null;
            GameObject best = null;
            float bestD = float.MaxValue, r2 = range * range;
            var counts = new int[3];
            int seen = 0, skipped = 0;
            foreach (var c in cands)
            {
                if (c.G == null || (c.Pos - center).sqrMagnitude > r2) continue;
                seen++;
                var src = c.Src;
                if ((skip != null && skip(c.G)) || src.MinTier > tier || !src.Yields(item) || !Ui.TrollFocus.SourceMatches(goal, src.PrefabName)) continue;
                if (src.Tier == TamingRules.TierNone) { skipped++; continue; }
                counts[src.Tier]++;
                var at = src.IsTree ? c.Pos : src.Collider.bounds.ClosestPoint(from);     // trees: the trunk (TEST-020)
                float d = TamingRules.SourceScore(src.Tier, Vector3.Distance(from, at));
                if (d < bestD) { bestD = d; best = c.G; hit = src.Collider; }
            }
            LastScanFull = false;
            if (ModLog.DebugOn)
            {
                LastScan = "sources " + seen + ", logs/rocks " + counts[0] + ", trees " + counts[1] + ", other " + counts[2] + ", too small " + skipped
                    + ", best " + (best != null ? best.name + " (" + KindOf(best) + ")" : "none");
                ModLog.Verbose("TrollFarming: search '" + item + "'" + (goal.Sources.Length > 0 ? " from " + string.Join(" / ", goal.Sources) : "") + " within " + range + " m of " + center.ToString("F0") + ": " + LastScan + (best != null ? " score " + bestD.ToString("F1") : ""));
            }
            else LastScan = "best " + (best != null ? best.name : "none");
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
            c.PrefabName = Utils.GetPrefabName(g);
            c.MinTier = MinTier(g);
            c.Tier = TamingRules.Tier(log, rock, tree, c.Collider != null ? c.Collider.bounds.size.y : 0f, MinOtherHeight);
            Known[id] = c;
            return c;
        }

        static readonly System.Reflection.FieldInfo HitAreas = AccessTools.Field(typeof(MineRock5), "m_hitAreas");

        /// D-082 (TEST-035: Steve 4 circled a silver vein - the game said 'Minerock hit has no collider or invalid hit area'): for a MineRock5
        /// (veins, big rocks) the nearest hit area that still has health, else null (then the source's biggest collider is used).
        internal static Collider LiveArea(GameObject source, Vector3 from)
        {
            var mr = source != null ? source.GetComponentInParent<MineRock5>() : null;
            if (mr == null || HitAreas == null) return null;
            var list = HitAreas.GetValue(mr) as System.Collections.IList;
            if (list == null) return null;
            Collider best = null;
            float bestD = float.MaxValue;
            foreach (var a in list)
            {
                if (a == null) continue;
                var t = a.GetType();
                var col = AccessTools.Field(t, "m_collider") != null ? AccessTools.Field(t, "m_collider").GetValue(a) as Collider : null;
                var hp = AccessTools.Field(t, "m_health") != null ? (float)AccessTools.Field(t, "m_health").GetValue(a) : 1f;
                if (col == null || !col.enabled || hp <= 0f) continue;
                float d = (col.bounds.center - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = col; }
            }
            return best;
        }

        internal static Collider Biggest(GameObject g)
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
                if (__instance == null) return true;
                var view0 = __instance.GetComponent<ZNetView>();
                if (view0 == null || !view0.IsValid() || !view0.IsOwner()) return true;
                var slam = __instance.GetComponent<TrollSlam>();
                if (slam != null && slam.Step(__instance, LookAt)) { __result = true; return false; }   // D-065: ground slams first
                var rage = __instance.GetComponent<TrollRage>();
                if (rage != null && rage.Step(__instance, dt, MoveTo, LookAt)) { __result = true; return false; }   // D-082: rage while the player is hit
                var chase = __instance.GetComponent<TrollChase>();
                if (chase != null && chase.Step(__instance, dt, MoveTo, LookAt)) { __result = true; return false; }   // D-084: goes for the one that hit it
                var self = __instance.GetComponent<Character>();
                if (TrollCounter.Ignoring(self) && !TrollGuard.HasForced(self) && __instance.GetComponent<TrollFarmer>() != null) TrollGuard.ClearTarget(__instance);   // D-084: hits ignored, keeps farming
                TrollGuard.Keep(__instance);                                                                     // D-066: the forced target stays
                if (TrollHold.Holds(__instance)) { __instance.StopMoving(); __result = true; return false; }     // D-063
                var f = __instance.GetComponent<TrollFarmer>();
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
        internal string PrefabName = "";
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
