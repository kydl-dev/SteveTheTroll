using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Backpack.Application;
using SteveTheTroll.Backpack.Domain;
using SteveTheTroll.Hubs.Domain;
using SteveTheTroll.Hubs.Infrastructure;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Backpack.Infrastructure
{
    /// <summary>
    /// D-064 (user spec): a troll with a FULL backpack that reaches its owner's bed empties it into the owner's troll depots near the bed.
    /// No depot with room -> a new one is built: on top of the latest stack while it is under 5 high, else a new stack at a spot that is
    /// near the bed (15 .. 50 m, D-097), not inside a house (nothing built above it), free (no pieces / rocks in the footprint), on ground that
    /// is level or that the game's level op flattens first (up to 2.5 m uneven, D-095),
    /// and that the troll can walk to within 10 m of (the game's pathfinding for the troll; it unloads from 5 - 10 m away, D-106). Depot chests are the game's reinforced chests
    /// marked stt_depot: they need no support (stacks never crumble) and cannot be removed for their materials.
    /// </summary>
    internal static class TrollDepot
    {
        internal const string FlagKey = "stt_depot", OwnerKey = "stt_depot_owner";
        /// D-095 / D-097 (user: "15(minimum) - 50m(maximum) from the bed"): new depot stacks stand this far from the bed, and a full troll
        /// unloads once within MaxFromBed (Tunables TrollBackpack DepotMinFromBed / DepotMaxFromBed).
        internal static float MinFromBed { get { return ModSettings.DepotMinFromBed.Value; } }
        internal static float MaxFromBed { get { return Mathf.Max(ModSettings.DepotMaxFromBed.Value, MinFromBed + RingStep); } }
        static float SearchRadius { get { return MaxFromBed + 15f; } }   // depot chests are found up to here
        // MaxSlope: ground this even is used as is; MaxSlopeLevelled: what the level op (DepotGround) flattens first; MaxSlopeAsIs: the old
        // limit (depot on the highest point) when the game has no level op.
        // Reach: the troll's path must end this close to a new depot (D-106: it unloads from up to 10 m away)
        internal const float RingStep = 2f, Reach = 10f, MaxSlope = 0.15f, MaxSlopeLevelled = 2.5f, MaxSlopeRelaxed = 5f, MaxSlopeAsIs = 1f, ColumnMatch = 0.5f;
        internal const int Angles = 16;
        const int MaxNewDepots = 6;
        const float ChestSlack = 1.5f;
        static readonly AccessTools.FieldRef<ZNetScene, Dictionary<ZDO, ZNetView>> Instances = AccessTools.FieldRefAccess<ZNetScene, Dictionary<ZDO, ZNetView>>("m_instances");

        internal static IEnumerable<ZNetView> Near(Vector3 at, string prefab)
        {
            var scene = ZNetScene.instance;
            var all = scene != null ? Instances(scene) : null;
            if (all == null) yield break;
            float r2 = SearchRadius * SearchRadius;
            foreach (var v in all.Values)
            {
                if (v == null || !v.IsValid() || (v.transform.position - at).sqrMagnitude > r2) continue;
                if (Utils.GetPrefabName(v.gameObject) == prefab) yield return v;
            }
        }

        static List<Container> Chests(Vector3 bed, long owner)
        {
            var list = new List<Container>();
            foreach (var v in Near(bed, DepotData.ChestPrefab))
            {
                var z = v.GetZDO();
                if (!z.GetBool(FlagKey) || z.GetLong(OwnerKey) != owner) continue;
                // D-104 (user: "sure"): depots outside the 15 .. 50 m ring (older ones next to the house) are not used any more
                // (a chest sits up to ~1.2 m from its frame's centre: a little slack on both edges)
                if (!DepotRules.InRing(Flat(v.transform.position - bed), MinFromBed - ChestSlack, MaxFromBed + ChestSlack)) continue;
                var c = v.GetComponent<Container>();
                if (c != null) list.Add(c);
            }
            return list;
        }

        /// Where the troll walks to unload. Expected outcome: true with a target (a depot chest with room, or a depot built now).
        internal static bool Plan(Character troll, Vector3 bed, long owner, out Vector3 target)
        {
            // D-173 (Architecture V2, P6): this reads the chests, DepotPlanner chooses (nearest with room, nobody else at / walking to it - D-107 / D-149)
            target = bed;
            PlanChests.Clear(); PlanFacts.Clear();
            foreach (var c in Chests(bed, owner))
            {
                PlanChests.Add(c);
                bool room = c.GetInventory().HaveEmptySlot();
                PlanFacts.Add(new DepotCandidate(room, room && Busy(c.transform.position, troll), room && ReservedByOther(c, troll), Vector3.Distance(c.transform.position, troll.transform.position)));
            }
            var plan = DepotPlanner.Choose(PlanFacts);
            int busy = plan.Skipped;
            if (!plan.BuildNew)
            {
                var chosen = PlanChests[plan.Chosen];
                float best = PlanFacts[plan.Chosen].Distance;
                target = chosen.transform.position;
                Reserve(chosen, troll);
                PlanChests.Clear();
                ModLog.Info("TrollDepot: " + TrollTaming.Tag(troll) + " unloads at the depot chest " + best.ToString("F1") + " m away" + (busy > 0 ? " (" + busy + " chest(s) skipped: another troll there)" : ""));
                return true;
            }
            PlanChests.Clear();
            if (busy > 0) ModLog.Info("TrollDepot: " + TrollTaming.Tag(troll) + " - every depot with room has another troll at it (" + busy + " chest(s)) - a new stack");
            return Build(troll, bed, owner, out target, plan.AvoidBusy);
        }
        static readonly List<Container> PlanChests = new List<Container>();
        static readonly List<DepotCandidate> PlanFacts = new List<DepotCandidate>();
        static readonly UnloadPipeline Unloads = new UnloadPipeline();

        /// D-107: another tamed troll stands within BusyRadius of the point (flat). Expected outcome: false when only `me` is there.
        internal static bool Busy(Vector3 at, Character me)
        {
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c == me || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c)) continue;
                if (DepotRules.Crowded(Flat(c.transform.position - at), BusyRadius)) return true;
            }
            return false;
        }
        const float BusyRadius = 10f;

        // D-149 (review: two trolls could pick the same chest before either is within BusyRadius): a troll that gets a chest from Plan reserves it for ReserveSeconds
        // (a stuck / dead / given-up troll frees it by time); Unload frees the troll's reservations.
        const float ReserveSeconds = 90f;
        static readonly Dictionary<int, KeyValuePair<int, float>> Reserved = new Dictionary<int, KeyValuePair<int, float>>();
        static bool ReservedByOther(Container c, Character me)
        {
            KeyValuePair<int, float> r;
            return Reserved.TryGetValue(c.GetInstanceID(), out r) && DepotRules.ReservedByOther(r.Key, r.Value, me.GetInstanceID(), Time.time, ReserveSeconds);
        }
        static void Reserve(Container c, Character me)
        {
            if (Reserved.Count > 100) Reserved.Clear();
            Reserved[c.GetInstanceID()] = new KeyValuePair<int, float>(me.GetInstanceID(), Time.time);
        }
        /// D-160 (test hooks): reserves a chest for a troll that does not exist, as a troll on its way would.
        internal static void ReserveForTest(Container c) { Reserved[c.GetInstanceID()] = new KeyValuePair<int, float>(-424242, Time.time); }

        static void Release(Character me)
        {
            int id = me.GetInstanceID();
            var mine = new List<int>();
            foreach (var kv in Reserved) if (kv.Value.Key == id) mine.Add(kv.Key);
            foreach (var k in mine) Reserved.Remove(k);
        }

        /// Moves everything from the troll's backpack into the owner's depot chests near the bed, building more depots when needed.
        /// Expected outcome: the number of stacks moved; `left` = stacks that found no room.
        internal static int Unload(Character troll, Vector3 bed, long owner, out int left)
        {
            var zdo = TrollTaming.Zdo(troll);
            var view = troll != null ? troll.GetComponent<ZNetView>() : null;
            bool trollOwner = view != null && view.IsValid() && view.IsOwner();
            left = 0;
            // D-173 (Architecture V2, P6): UnloadPipeline decides per stack (owner / data checks, then Put / BuildDepot / Leave); this class reads and moves the items
            var gate = Unloads.Evaluate(new UnloadInput(trollOwner, zdo != null, true, 0, MaxNewDepots));
            if (!gate.IsSuccess) { ModLog.Info("TrollDepot: " + TrollTaming.Tag(troll) + " unload not done - " + gate.Reason); return 0; }
            int moved = 0, built = 0;
            for (int i = 0; i < BackpackStore.ChestCount; i++)
            {
                var inv = BackpackStore.Read(zdo, i);
                foreach (var item in new List<ItemDrop.ItemData>(inv.GetAllItems()))
                {
                    var into = Room(bed, owner, item, troll.transform.position);
                    var step = Unloads.Evaluate(new UnloadInput(trollOwner, true, into != null, built, MaxNewDepots)).Value;
                    while (step == UnloadStep.BuildDepot)
                    {
                        Vector3 at;
                        if (!Build(troll, bed, owner, out at)) { step = UnloadStep.Leave; break; }
                        built++;
                        into = Room(bed, owner, item, troll.transform.position);
                        step = Unloads.Evaluate(new UnloadInput(trollOwner, true, into != null, built, MaxNewDepots)).Value;
                    }
                    if (step != UnloadStep.Put) { left++; continue; }
                    into.GetInventory().AddItem(item.Clone());
                    inv.RemoveItem(item);
                    moved++;
                }
                BackpackStore.Write(zdo, i, inv);
            }
            Release(troll);
            ModLog.Info("TrollDepot: " + TrollTaming.Tag(troll) + " moved " + moved + " stack(s) into the depot, " + left + " left, " + built + " depot(s) built");
            return moved;
        }

        // A depot chest that takes the whole stack, nearest to the troll first (D-107: the stack it walked to), this client takes ownership
        // of it when nobody has it open.
        static Container Room(Vector3 bed, long owner, ItemDrop.ItemData item, Vector3 from)
        {
            var list = Chests(bed, owner);
            list.Sort((a, b) => (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
            foreach (var c in list)
            {
                if (c.IsInUse() || !c.GetInventory().CanAddItem(item)) continue;
                var v = c.GetComponent<ZNetView>();
                if (!v.IsOwner()) v.ClaimOwnership();
                if (v.IsOwner()) return c;
            }
            return null;
        }

        // A new depot: on the latest stack while it is under MaxStack high, else at a new spot. Expected outcome: true + its position.
        static bool Build(Character troll, Vector3 bed, long owner, out Vector3 at, bool avoidBusy = false)
        {
            at = bed;
            Quaternion rot;
            bool level = false;
            if (!OnStack(bed, owner, out at, out rot, avoidBusy ? troll : null) && !DepotSpots.FindSpot(troll, bed, out at, out rot, out level))
            {
                ModLog.Warn("TrollDepot: no spot for a troll depot near the bed at " + bed.ToString("F0"));
                return false;
            }
            if (level) DepotGround.Flatten(at, rot);                // D-095: uneven spot -> the ground is levelled first
            if (Place(at, rot, owner) == null) return false;
            ModLog.Info("TrollDepot: depot built at " + at.ToString("F1") + " (" + DepotData.Chests.Length + " chests) for " + TrollTaming.Tag(troll));
            return true;
        }

        /// D-160: instantiates the frame and its 4 flagged chests at the spot (the real build code; the test hooks use it too). Expected outcome: the frame, or null when a prefab is missing.
        internal static GameObject Place(Vector3 at, Quaternion rot, long owner)
        {
            var framePrefab = ZNetScene.instance.GetPrefab(DepotPrefab.Name);
            var chestPrefab = ZNetScene.instance.GetPrefab(DepotData.ChestPrefab);
            if (framePrefab == null || chestPrefab == null) { ModLog.Warn("TrollDepot: prefab missing (frame " + (framePrefab != null) + ", chest " + (chestPrefab != null) + ")"); return null; }
            var frame = Object.Instantiate(framePrefab, at, rot);
            var fz = frame.GetComponent<ZNetView>().GetZDO();
            fz.Set(OwnerKey, owner);
            foreach (var p in DepotData.Chests)
            {
                var chest = Object.Instantiate(chestPrefab, at + rot * new Vector3(p.X, p.Y, p.Z), rot * Quaternion.Euler(0f, p.Yaw, 0f));
                Mark(chest, owner);
            }
            return frame;
        }

        /// D-160: the 4 flagged chests of a frame (test hooks).
        internal static System.Collections.Generic.List<Container> ChestsOf(GameObject frame)
        {
            var list = new System.Collections.Generic.List<Container>();
            if (frame == null) return list;
            foreach (var p in DepotData.Chests)
            {
                var expected = frame.transform.position + frame.transform.rotation * new Vector3(p.X, p.Y, p.Z);
                foreach (var v in Near(frame.transform.position, DepotData.ChestPrefab))
                {
                    if ((v.transform.position - expected).sqrMagnitude > 0.6f * 0.6f || !v.GetZDO().GetBool(FlagKey)) continue;
                    var c = v.GetComponent<Container>();
                    if (c != null && !list.Contains(c)) { list.Add(c); break; }
                }
            }
            return list;
        }

        static void Mark(GameObject chest, long owner)
        {
            var z = chest.GetComponent<ZNetView>().GetZDO();
            z.Set(FlagKey, true);
            z.Set(OwnerKey, owner);
            z.Set(ZDOVars.s_creator, owner);       // the game's "built by" (Piece reads it from the ZDO)
            Apply(chest);
        }

        /// A depot chest needs no support and cannot be removed for its materials.
        internal static void Apply(GameObject chest)
        {
            var w = chest.GetComponent<WearNTear>(); if (w != null) w.m_noSupportWear = true;
            var p = chest.GetComponent<Piece>(); if (p != null) p.m_canBeRemoved = false;
        }

        // The highest depot of the stack nearest the bed, when that stack is under MaxStack high.
        // avoidFor != null (D-107): stacks with another troll at them are left out
        static bool OnStack(Vector3 bed, long owner, out Vector3 at, out Quaternion rot, Character avoidFor = null)
        {
            at = bed; rot = Quaternion.identity;
            var frames = new List<ZNetView>();
            foreach (var v in Near(bed, DepotPrefab.Name)) if (v.GetZDO().GetLong(OwnerKey) == owner) frames.Add(v);
            if (frames.Count == 0) return false;
            frames.Sort((a, b) => Flat(a.transform.position - bed).CompareTo(Flat(b.transform.position - bed)));
            foreach (var bottom in frames)
            {
                // TEST-043: stacks from before the 15 .. 50 m ring (5 - 7 m from the bed) kept growing next to the house - only stacks inside
                // the ring get more levels (the old ones stay as they are and are still used for unloading)
                if (!DepotRules.InRing(Flat(bottom.transform.position - bed), MinFromBed, MaxFromBed)) continue;
                if (avoidFor != null && Busy(bottom.transform.position, avoidFor)) continue;
                int levels = 0; Transform top = null;
                foreach (var f in frames)
                {
                    if (Flat(f.transform.position - bottom.transform.position) > ColumnMatch) continue;
                    levels++;
                    if (top == null || f.transform.position.y > top.position.y) top = f.transform;
                }
                if (levels >= DepotData.MaxStack) continue;
                at = top.position + Vector3.up * DepotPrefab.Height; rot = top.rotation;
                ModLog.Info("TrollDepot: stacks level " + (levels + 1) + " of " + DepotData.MaxStack + " at " + at.ToString("F1"));
                return true;
            }
            return false;
        }

        internal static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        // D-155: a troll-built structure (frame + its 4 flagged chests) is removed as soon as the last item leaves it. Checked whenever a flagged chest saves its contents
        // (a fresh structure is never checked before something changed in it, so it is not removed before the troll fills it).
        internal static void RemoveIfEmpty(Container saved)
        {
            if (!ModSettings.DepotRemoveEmpty.Value || saved == null) return;
            var sv = saved.GetComponent<ZNetView>();
            if (sv == null || !sv.IsValid() || !sv.GetZDO().GetBool(FlagKey) || !sv.IsOwner()) return;
            long owner = sv.GetZDO().GetLong(OwnerKey);
            var chestViews = new List<ZNetView>(Near(saved.transform.position, DepotData.ChestPrefab));
            var frames = new List<ZNetView>(Near(saved.transform.position, DepotPrefab.Name));
            foreach (var f in frames)
            {
                if (f.GetZDO().GetLong(OwnerKey) != owner) continue;
                var mine = new List<Container>();
                bool hasSaved = false;
                foreach (var p in DepotData.Chests)
                {
                    var expected = f.transform.position + f.transform.rotation * new Vector3(p.X, p.Y, p.Z);
                    foreach (var cv in chestViews)
                    {
                        if ((cv.transform.position - expected).sqrMagnitude > 0.6f * 0.6f || !cv.GetZDO().GetBool(FlagKey)) continue;
                        var c = cv.GetComponent<Container>();
                        if (c == null) continue;
                        mine.Add(c);
                        if (c == saved) hasSaved = true;
                        break;
                    }
                }
                if (!hasSaved) continue;
                int empty = 0; bool inUse = false, reserved = false;
                foreach (var c in mine)
                {
                    if (c.GetInventory().NrOfItems() == 0) empty++;
                    if (c.IsInUse()) inUse = true;
                    KeyValuePair<int, float> r;
                    if (Reserved.TryGetValue(c.GetInstanceID(), out r) && Time.time - r.Value < ReserveSeconds) reserved = true;
                }
                bool above = false;
                foreach (var o in frames)
                    if (o != f && Flat(o.transform.position - f.transform.position) <= ColumnMatch && o.transform.position.y > f.transform.position.y + 0.1f) above = true;
                if (!DepotRules.Dismantle(mine.Count, empty, inUse, reserved, above))
                {
                    if (DepotRules.Lifecycle(mine.Count, empty, inUse, reserved, above) == DepotRules.Verdict.Wait) { Retry[saved.GetInstanceID()] = saved; if (!WaitingSince.ContainsKey(saved.GetInstanceID())) WaitingSince[saved.GetInstanceID()] = new KeyValuePair<Container, float>(saved, Time.time); }      // D-159: empty but blocked (open / reserved / column) - looked at again by Tick
                    ModLog.Verbose("TrollDepot: structure at " + f.transform.position.ToString("F1") + " kept (chests " + mine.Count + ", empty " + empty + ", inUse " + inUse + ", reserved " + reserved + ", frameAbove " + above + ")");
                    return;
                }
                ModLog.Info("TrollDepot: structure at " + f.transform.position.ToString("F1") + " is empty - removed (" + mine.Count + " chests + frame)");
                foreach (var c in mine) Remove(c.gameObject);
                Remove(f.gameObject);
                return;
            }
        }

        static readonly Dictionary<int, Container> Retry = new Dictionary<int, Container>();
        static float _nextRetry;
        static readonly Dictionary<int, KeyValuePair<Container, float>> WaitingSince = new Dictionary<int, KeyValuePair<Container, float>>();

        /// D-160 (RuntimeHealth): how long the longest-waiting empty structure has been blocked (0 = none).
        internal static float LongestWait()
        {
            float worst = 0f;
            var gone = new List<int>();
            foreach (var kv in WaitingSince)
            {
                if (kv.Value.Key == null) { gone.Add(kv.Key); continue; }
                worst = Mathf.Max(worst, Time.time - kv.Value.Value);
            }
            foreach (var k in gone) WaitingSince.Remove(k);
            return worst;
        }

        /// D-159: an empty structure that could not be removed yet (window open, troll reservation, another frame above) is checked again every 5 s until it can go.
        internal static void Tick()
        {
            if (Retry.Count == 0 || Time.time < _nextRetry) return;
            _nextRetry = Time.time + 5f;
            var list = new List<Container>(Retry.Values);
            Retry.Clear();
            foreach (var c in list)
            {
                if (c == null) continue;
                try { RemoveIfEmpty(c); }       // puts itself back into Retry when still blocked
                catch (System.Exception e) { ModLog.Warn("TrollDepot: retry failed: " + e.GetType().Name + ": " + e.Message); }
                if (!Retry.ContainsKey(c.GetInstanceID())) WaitingSince.Remove(c.GetInstanceID());
            }
        }

        static void Remove(GameObject go)
        {
            var v = go.GetComponent<ZNetView>();
            if (v == null || !v.IsValid()) return;
            v.ClaimOwnership();
            ZNetScene.instance.Destroy(go);
        }

        // Harmony hook: src/Integrations/Harmony/TrollsBackpackPatches.cs, TrollDepot_EmptyCheck (MIGRATION_DECISIONS A5)
        internal static class EmptyCheck
        {
            internal static void Postfix(Container __instance)
            {
                try { RemoveIfEmpty(__instance); }
                catch (System.Exception e) { ModLog.Warn("TrollDepot: empty check failed: " + e.GetType().Name + ": " + e.Message); }
            }
        }

        // D-159: Save runs while the chest window is still open (IsInUse), so the check is repeated when the window closes
        // Harmony hook: src/Integrations/Harmony/TrollsBackpackPatches.cs, TrollDepot_CloseCheck (MIGRATION_DECISIONS A5)
        internal static class CloseCheck
        {
            internal static void Postfix(Container __instance, bool inUse)
            {
                if (inUse) return;
                try { RemoveIfEmpty(__instance); }
                catch (System.Exception e) { ModLog.Warn("TrollDepot: close check failed: " + e.GetType().Name + ": " + e.Message); }
            }
        }

        // Harmony hook: src/Integrations/Harmony/TrollsBackpackPatches.cs, TrollDepot_ChestFlags (MIGRATION_DECISIONS A5)
        internal static class ChestFlags
        {
            internal static void Postfix(WearNTear __instance)
            {
                var v = __instance.GetComponent<ZNetView>();
                if (v == null || !v.IsValid() || !v.GetZDO().GetBool(FlagKey)) return;
                Apply(__instance.gameObject);
            }
        }
    }
}
