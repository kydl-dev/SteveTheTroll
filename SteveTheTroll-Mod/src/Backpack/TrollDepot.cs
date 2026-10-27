using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Backpack
{
    /// <summary>
    /// D-064 (user spec): a troll with a FULL backpack that reaches its owner's bed empties it into the owner's troll depots near the bed.
    /// No depot with room -> a new one is built: on top of the latest stack while it is under 5 high, else a new stack at a spot that is
    /// near the bed (6 .. 26 m), not inside a house (nothing built above it), free (no pieces / rocks in the footprint), on fairly level ground,
    /// and that the troll can walk to within 5 m of (the game's pathfinding for the troll). Depot chests are the game's reinforced chests
    /// marked stt_depot: they need no support (stacks never crumble) and cannot be removed for their materials.
    /// </summary>
    internal static class TrollDepot
    {
        internal const string FlagKey = "stt_depot", OwnerKey = "stt_depot_owner";
        const float SearchRadius = 40f, MinFromBed = 6f, MaxFromBed = 26f, RingStep = 2f, Reach = 5f, MaxSlope = 1f, ColumnMatch = 0.5f;
        const int Angles = 16, MaxNewDepots = 6;
        static readonly AccessTools.FieldRef<ZNetScene, Dictionary<ZDO, ZNetView>> Instances = AccessTools.FieldRefAccess<ZNetScene, Dictionary<ZDO, ZNetView>>("m_instances");
        static readonly List<Vector3> Path = new List<Vector3>();

        static IEnumerable<ZNetView> Near(Vector3 at, string prefab)
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
                var c = v.GetComponent<Container>();
                if (c != null) list.Add(c);
            }
            return list;
        }

        /// Where the troll walks to unload. Expected outcome: true with a target (a depot chest with room, or a depot built now).
        internal static bool Plan(Character troll, Vector3 bed, long owner, out Vector3 target)
        {
            target = bed;
            float best = float.MaxValue;
            foreach (var c in Chests(bed, owner))
            {
                if (!c.GetInventory().HaveEmptySlot()) continue;
                float d = Vector3.Distance(c.transform.position, troll.transform.position);
                if (d < best) { best = d; target = c.transform.position; }
            }
            if (best < float.MaxValue) { ModLog.Info("TrollDepot: " + troll.name + " unloads at the depot chest " + best.ToString("F1") + " m away"); return true; }
            return Build(troll, bed, owner, out target);
        }

        /// Moves everything from the troll's backpack into the owner's depot chests near the bed, building more depots when needed.
        /// Expected outcome: the number of stacks moved; `left` = stacks that found no room.
        internal static int Unload(Character troll, Vector3 bed, long owner, out int left)
        {
            var zdo = Taming.TrollTaming.Zdo(troll);
            left = 0;
            if (zdo == null) return 0;
            int moved = 0, built = 0;
            for (int i = 0; i < BackpackStore.ChestCount; i++)
            {
                var inv = BackpackStore.Read(zdo, i);
                foreach (var item in new List<ItemDrop.ItemData>(inv.GetAllItems()))
                {
                    var into = Room(bed, owner, item);
                    while (into == null && built < MaxNewDepots)
                    {
                        Vector3 at;
                        if (!Build(troll, bed, owner, out at)) break;
                        built++;
                        into = Room(bed, owner, item);
                    }
                    if (into == null) { left++; continue; }
                    into.GetInventory().AddItem(item.Clone());
                    inv.RemoveItem(item);
                    moved++;
                }
                BackpackStore.Write(zdo, i, inv);
            }
            ModLog.Info("TrollDepot: " + troll.name + " moved " + moved + " stack(s) into the depot, " + left + " left, " + built + " depot(s) built");
            return moved;
        }

        // A depot chest that takes the whole stack (this client takes ownership of it when nobody has it open).
        static Container Room(Vector3 bed, long owner, ItemDrop.ItemData item)
        {
            foreach (var c in Chests(bed, owner))
            {
                if (c.IsInUse() || !c.GetInventory().CanAddItem(item)) continue;
                var v = c.GetComponent<ZNetView>();
                if (!v.IsOwner()) v.ClaimOwnership();
                if (v.IsOwner()) return c;
            }
            return null;
        }

        // A new depot: on the latest stack while it is under MaxStack high, else at a new spot. Expected outcome: true + its position.
        static bool Build(Character troll, Vector3 bed, long owner, out Vector3 at)
        {
            at = bed;
            Quaternion rot;
            if (!OnStack(bed, owner, out at, out rot) && !FindSpot(troll, bed, out at, out rot))
            {
                ModLog.Warn("TrollDepot: no spot for a troll depot near the bed at " + bed.ToString("F0"));
                return false;
            }
            var framePrefab = ZNetScene.instance.GetPrefab(DepotPrefab.Name);
            var chestPrefab = ZNetScene.instance.GetPrefab(DepotData.ChestPrefab);
            if (framePrefab == null || chestPrefab == null) { ModLog.Warn("TrollDepot: prefab missing (frame " + (framePrefab != null) + ", chest " + (chestPrefab != null) + ")"); return false; }
            var frame = Object.Instantiate(framePrefab, at, rot);
            var fz = frame.GetComponent<ZNetView>().GetZDO();
            fz.Set(OwnerKey, owner);
            foreach (var p in DepotData.Chests)
            {
                var chest = Object.Instantiate(chestPrefab, at + rot * new Vector3(p.X, p.Y, p.Z), rot * Quaternion.Euler(0f, p.Yaw, 0f));
                Mark(chest, owner);
            }
            ModLog.Info("TrollDepot: depot built at " + at.ToString("F1") + " (" + DepotData.Chests.Length + " chests) for " + troll.name);
            return true;
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
        static bool OnStack(Vector3 bed, long owner, out Vector3 at, out Quaternion rot)
        {
            at = bed; rot = Quaternion.identity;
            var frames = new List<ZNetView>();
            foreach (var v in Near(bed, DepotPrefab.Name)) if (v.GetZDO().GetLong(OwnerKey) == owner) frames.Add(v);
            if (frames.Count == 0) return false;
            frames.Sort((a, b) => Flat(a.transform.position - bed).CompareTo(Flat(b.transform.position - bed)));
            foreach (var bottom in frames)
            {
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

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        // Rings around the bed, nearest first. Expected outcome: true + the depot's ground position and facing (long side towards the bed).
        static bool FindSpot(Character troll, Vector3 bed, out Vector3 at, out Quaternion rot)
        {
            at = bed; rot = Quaternion.identity;
            var ai = troll.GetComponent<BaseAI>();
            var agent = ai != null ? ai.m_pathAgentType : Pathfinding.AgentType.TrollSize;
            int tried = 0;
            for (float r = MinFromBed; r <= MaxFromBed; r += RingStep)
            {
                for (int k = 0; k < Angles; k++)
                {
                    float a = (k + (r / RingStep) * 0.5f) * Mathf.PI * 2f / Angles;
                    var p = bed + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    var toBed = bed - p; toBed.y = 0f;
                    var q = Quaternion.LookRotation(toBed.normalized, Vector3.up);
                    tried++;
                    Vector3 ground;
                    if (!Level(p, q, out ground) || !Free(ground, q) || Covered(ground, q) || !Reachable(troll, agent, ground)) continue;
                    at = ground; rot = q;
                    ModLog.Info("TrollDepot: new stack at " + at.ToString("F1") + ", " + r.ToString("0") + " m from the bed (" + tried + " spot(s) checked)");
                    return true;
                }
            }
            ModLog.Info("TrollDepot: none of " + tried + " spot(s) near the bed fits");
            return false;
        }

        // Ground under the footprint: corners within MaxSlope of each other; the depot stands on the highest one.
        static bool Level(Vector3 p, Quaternion q, out Vector3 ground)
        {
            ground = p;
            var zs = ZoneSystem.instance;
            if (zs == null) return false;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int k = 0; k < 5; k++)
            {
                var off = k == 4 ? Vector3.zero : new Vector3((k & 1) == 0 ? -DepotData.HalfX : DepotData.HalfX, 0f, (k & 2) == 0 ? -DepotData.HalfZ : DepotData.HalfZ);
                float h = zs.GetGroundHeight(p + q * off);
                lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
            }
            ground = new Vector3(p.x, hi, p.z);
            return hi - lo <= MaxSlope;
        }

        static int _blockMask, _roofMask;

        static bool Free(Vector3 ground, Quaternion q)
        {
            if (_blockMask == 0) _blockMask = LayerMask.GetMask("piece", "piece_nonsolid", "static_solid", "Default", "Default_small", "vehicle");
            float h = DepotPrefab.Height * 0.5f + 0.15f;
            return !Physics.CheckBox(ground + Vector3.up * (h + 0.1f), new Vector3(DepotData.HalfX, h, DepotData.HalfZ), q, _blockMask);
        }

        // "Not inside the house": nothing built above the middle or the ends of the footprint.
        static bool Covered(Vector3 ground, Quaternion q)
        {
            if (_roofMask == 0) _roofMask = LayerMask.GetMask("piece");
            for (int k = -1; k <= 1; k++)
                if (Physics.Raycast(ground + q * new Vector3(k * (DepotData.HalfX - 0.3f), 0f, 0f) + Vector3.up * (DepotPrefab.Height + 0.3f), Vector3.up, 40f, _roofMask)) return true;
            return false;
        }

        static bool Reachable(Character troll, Pathfinding.AgentType agent, Vector3 ground)
        {
            if (Pathfinding.instance == null) return true;
            Path.Clear();
            if (!Pathfinding.instance.GetPath(troll.transform.position, ground, Path, agent, false)) return false;
            return Path.Count > 0 && Flat(Path[Path.Count - 1] - ground) <= Reach;
        }

        [HarmonyPatch(typeof(WearNTear), "Awake")]
        static class ChestFlags
        {
            static void Postfix(WearNTear __instance)
            {
                var v = __instance.GetComponent<ZNetView>();
                if (v == null || !v.IsValid() || !v.GetZDO().GetBool(FlagKey)) return;
                Apply(__instance.gameObject);
            }
        }
    }
}
