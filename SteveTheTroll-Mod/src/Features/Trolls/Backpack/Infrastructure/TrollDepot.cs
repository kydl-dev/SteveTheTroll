using System;
using System.Collections.Generic;
using HarmonyLib;
using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Backpack.Application;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Features.Trolls.Backpack.Infrastructure;

/// <summary>
///     D-064 (user spec): a troll with a FULL backpack that reaches its owner's bed empties it into the owner's troll
///     depots near the bed.
///     No depot with room -> a new one is built: on top of the latest stack while it is under 5 high, else a new stack at
///     a spot that is
///     near the bed (15 .. 50 m, D-097), not inside a house (nothing built above it), free (no pieces / rocks in the
///     footprint), on ground that
///     is level or that the game's level op flattens first (up to 2.5 m uneven, D-095),
///     and that the troll can walk to within 10 m of (the game's pathfinding for the troll; it unloads from 5 - 10 m away,
///     D-106). Depot chests are the game's reinforced chests
///     marked stt_depot: they need no support (stacks never crumble) and cannot be removed for their materials.
/// </summary>
internal static partial class TrollDepot
{
    internal const string FlagKey = "stt_depot", OwnerKey = "stt_depot_owner";

    // MaxSlope: ground this even is used as is; MaxSlopeLevelled: what the level op (DepotGround) flattens first; MaxSlopeAsIs: the old
    // limit (depot on the highest point) when the game has no level op.
    // Reach: the troll's path must end this close to a new depot (D-106: it unloads from up to 10 m away)
    internal const float RingStep = 2f,
        Reach = 10f,
        MaxSlope = 0.15f,
        MaxSlopeLevelled = 2.5f,
        MaxSlopeRelaxed = 5f,
        MaxSlopeAsIs = 1f,
        ColumnMatch = 0.5f;

    internal const int Angles = 16;
    private const int MaxNewDepots = 6;
    private const float ChestSlack = 1.5f;

    private const float BusyRadius = 10f;

    // D-149 (review: two trolls could pick the same chest before either is within BusyRadius): a troll that gets a chest from Plan reserves it for ReserveSeconds
    // (a stuck / dead / given-up troll frees it by time); Unload frees the troll's reservations.
    private const float ReserveSeconds = 90f;

    private static readonly AccessTools.FieldRef<ZNetScene, Dictionary<ZDO, ZNetView>> Instances =
        AccessTools.FieldRefAccess<ZNetScene, Dictionary<ZDO, ZNetView>>("m_instances");

    private static readonly List<Container> PlanChests = new();
    private static readonly List<DepotCandidate> PlanFacts = new();
    private static readonly UnloadPipeline Unloads = new();
    private static readonly Dictionary<int, KeyValuePair<int, float>> Reserved = new();

    private static readonly Dictionary<int, Container> Retry = new();
    private static float _nextRetry;
    private static readonly Dictionary<int, KeyValuePair<Container, float>> WaitingSince = new();

    /// D-095 / D-097 (user: "15(minimum) - 50m(maximum) from the bed"): new depot stacks stand this far from the bed, and a full troll
    /// unloads once within MaxFromBed (Tunables TrollBackpack DepotMinFromBed / DepotMaxFromBed).
    internal static float MinFromBed => ModSettings.DepotMinFromBed.Value;

    internal static float MaxFromBed => Mathf.Max(ModSettings.DepotMaxFromBed.Value, MinFromBed + RingStep);

    private static float SearchRadius => MaxFromBed + 15f; // depot chests are found up to here

    internal static IEnumerable<ZNetView> Near(Vector3 at, string prefab)
    {
        ZNetScene scene = ZNetScene.instance;
        Dictionary<ZDO, ZNetView> all = scene != null ? Instances(scene) : null;
        if (all == null)
        {
            yield break;
        }

        float r2 = SearchRadius * SearchRadius;
        foreach (ZNetView v in all.Values)
        {
            if (v == null || !v.IsValid() || (v.transform.position - at).sqrMagnitude > r2)
            {
                continue;
            }

            if (Utils.GetPrefabName(v.gameObject) == prefab)
            {
                yield return v;
            }
        }
    }

    private static List<Container> Chests(Vector3 bed, long owner)
    {
        var list = new List<Container>();
        foreach (ZNetView v in Near(bed, DepotData.ChestPrefab))
        {
            ZDO z = v.GetZDO();
            if (!z.GetBool(FlagKey) || z.GetLong(OwnerKey) != owner)
            {
                continue;
            }

            // D-104 (user: "sure"): depots outside the 15 .. 50 m ring (older ones next to the house) are not used any more
            // (a chest sits up to ~1.2 m from its frame's centre: a little slack on both edges)
            if (!DepotRules.InRing(Flat(v.transform.position - bed), MinFromBed - ChestSlack, MaxFromBed + ChestSlack))
            {
                continue;
            }

            Container c = v.GetComponent<Container>();
            if (c != null)
            {
                list.Add(c);
            }
        }

        return list;
    }

    /// Where the troll walks to unload. Expected outcome: true with a target (a depot chest with room, or a depot built now).
    internal static bool Plan(Character troll, Vector3 bed, long owner, out Vector3 target)
    {
        // D-173 (Architecture V2, P6): this reads the chests, DepotPlanner chooses (nearest with room, nobody else at / walking to it - D-107 / D-149)
        target = bed;
        PlanChests.Clear();
        PlanFacts.Clear();
        foreach (Container c in Chests(bed, owner))
        {
            PlanChests.Add(c);
            bool room = c.GetInventory().HaveEmptySlot();
            PlanFacts.Add(new DepotCandidate(room, room && Busy(c.transform.position, troll),
                room && ReservedByOther(c, troll), Vector3.Distance(c.transform.position, troll.transform.position)));
        }

        DepotPlan plan = DepotPlanner.Choose(PlanFacts);
        int busy = plan.Skipped;
        if (!plan.BuildNew)
        {
            Container chosen = PlanChests[plan.Chosen];
            float best = PlanFacts[plan.Chosen].Distance;
            target = chosen.transform.position;
            Reserve(chosen, troll);
            PlanChests.Clear();
            ModLog.Info("TrollDepot: " + TrollTaming.Tag(troll) + " unloads at the depot chest " + best.ToString("F1") +
                        " m away" + (busy > 0 ? " (" + busy + " chest(s) skipped: another troll there)" : ""));
            return true;
        }

        PlanChests.Clear();
        if (busy > 0)
        {
            ModLog.Info("TrollDepot: " + TrollTaming.Tag(troll) + " - every depot with room has another troll at it (" +
                        busy + " chest(s)) - a new stack");
        }

        return Build(troll, bed, owner, out target, plan.AvoidBusy);
    }

    /// D-107: another tamed troll stands within BusyRadius of the point (flat). Expected outcome: false when only `me` is there.
    internal static bool Busy(Vector3 at, Character me)
    {
        foreach (Character c in Character.GetAllCharacters())
        {
            if (c == null || c == me || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c))
            {
                continue;
            }

            if (DepotRules.Crowded(Flat(c.transform.position - at), BusyRadius))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReservedByOther(Container c, Character me)
    {
        KeyValuePair<int, float> r;
        return Reserved.TryGetValue(c.GetInstanceID(), out r) &&
               DepotRules.ReservedByOther(r.Key, r.Value, me.GetInstanceID(), Time.time, ReserveSeconds);
    }

    private static void Reserve(Container c, Character me)
    {
        if (Reserved.Count > 100)
        {
            Reserved.Clear();
        }

        Reserved[c.GetInstanceID()] = new KeyValuePair<int, float>(me.GetInstanceID(), Time.time);
    }

    /// D-160 (test hooks): reserves a chest for a troll that does not exist, as a troll on its way would.
    internal static void ReserveForTest(Container c)
    {
        Reserved[c.GetInstanceID()] = new KeyValuePair<int, float>(-424242, Time.time);
    }

    private static void Release(Character me)
    {
        int id = me.GetInstanceID();
        var mine = new List<int>();
        foreach (KeyValuePair<int, KeyValuePair<int, float>> kv in Reserved)
        {
            if (kv.Value.Key == id)
            {
                mine.Add(kv.Key);
            }
        }

        foreach (int k in mine)
        {
            Reserved.Remove(k);
        }
    }

    /// Moves everything from the troll's backpack into the owner's depot chests near the bed, building more depots when needed.
    /// Expected outcome: the number of stacks moved; `left` = stacks that found no room.
    internal static int Unload(Character troll, Vector3 bed, long owner, out int left)
    {
        ZDO zdo = TrollTaming.Zdo(troll);
        ZNetView view = troll != null ? troll.GetComponent<ZNetView>() : null;
        bool trollOwner = view != null && view.IsValid() && view.IsOwner();
        left = 0;
        // D-173 (Architecture V2, P6): UnloadPipeline decides per stack (owner / data checks, then Put / BuildDepot / Leave); this class reads and moves the items
        OperationResponse<UnloadStep> gate =
            Unloads.Evaluate(new UnloadInput(trollOwner, zdo != null, true, 0, MaxNewDepots));
        if (!gate.IsSuccess)
        {
            ModLog.Info("TrollDepot: " + TrollTaming.Tag(troll) + " unload not done - " + gate.Reason);
            return 0;
        }

        int moved = 0, built = 0;
        for (int i = 0; i < BackpackStore.ChestCount; i++)
        {
            Inventory inv = BackpackStore.Read(zdo, i);
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inv.GetAllItems()))
            {
                Container into = Room(bed, owner, item, troll.transform.position);
                UnloadStep step = Unloads.Evaluate(new UnloadInput(trollOwner, true, into != null, built, MaxNewDepots))
                    .Value;
                while (step == UnloadStep.BuildDepot)
                {
                    Vector3 at;
                    if (!Build(troll, bed, owner, out at))
                    {
                        step = UnloadStep.Leave;
                        break;
                    }

                    built++;
                    into = Room(bed, owner, item, troll.transform.position);
                    step = Unloads.Evaluate(new UnloadInput(trollOwner, true, into != null, built, MaxNewDepots)).Value;
                }

                if (step != UnloadStep.Put)
                {
                    left++;
                    continue;
                }

                into.GetInventory().AddItem(item.Clone());
                inv.RemoveItem(item);
                moved++;
            }

            BackpackStore.Write(zdo, i, inv);
        }

        Release(troll);
        ModLog.Info("TrollDepot: " + TrollTaming.Tag(troll) + " moved " + moved + " stack(s) into the depot, " + left +
                    " left, " + built + " depot(s) built");
        return moved;
    }

    // A depot chest that takes the whole stack, nearest to the troll first (D-107: the stack it walked to), this client takes ownership
    // of it when nobody has it open.
    private static Container Room(Vector3 bed, long owner, ItemDrop.ItemData item, Vector3 from)
    {
        List<Container> list = Chests(bed, owner);
        list.Sort((a, b) =>
            (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
        foreach (Container c in list)
        {
            if (c.IsInUse() || !c.GetInventory().CanAddItem(item))
            {
                continue;
            }

            ZNetView v = c.GetComponent<ZNetView>();
            if (!v.IsOwner())
            {
                v.ClaimOwnership();
            }

            if (v.IsOwner())
            {
                return c;
            }
        }

        return null;
    }

    // A new depot: on the latest stack while it is under MaxStack high, else at a new spot. Expected outcome: true + its position.
    private static bool Build(Character troll, Vector3 bed, long owner, out Vector3 at, bool avoidBusy = false)
    {
        at = bed;
        Quaternion rot;
        bool level = false;
        if (!OnStack(bed, owner, out at, out rot, avoidBusy ? troll : null) &&
            !DepotSpots.FindSpot(troll, bed, out at, out rot, out level))
        {
            ModLog.Warn("TrollDepot: no spot for a troll depot near the bed at " + bed.ToString("F0"));
            return false;
        }

        if (level)
        {
            DepotGround.Flatten(at, rot); // D-095: uneven spot -> the ground is levelled first
        }

        if (Place(at, rot, owner) == null)
        {
            return false;
        }

        ModLog.Info("TrollDepot: depot built at " + at.ToString("F1") + " (" + DepotData.Chests.Length +
                    " chests) for " + TrollTaming.Tag(troll));
        return true;
    }

    /// D-160: instantiates the frame and its 4 flagged chests at the spot (the real build code; the test hooks use it too). Expected outcome: the frame, or null when a prefab is missing.
    internal static GameObject Place(Vector3 at, Quaternion rot, long owner)
    {
        GameObject framePrefab = ZNetScene.instance.GetPrefab(DepotPrefab.Name);
        GameObject chestPrefab = ZNetScene.instance.GetPrefab(DepotData.ChestPrefab);
        if (framePrefab == null || chestPrefab == null)
        {
            ModLog.Warn("TrollDepot: prefab missing (frame " + (framePrefab != null) + ", chest " +
                        (chestPrefab != null) + ")");
            return null;
        }

        GameObject frame = Object.Instantiate(framePrefab, at, rot);
        ZDO fz = frame.GetComponent<ZNetView>().GetZDO();
        fz.Set(OwnerKey, owner);
        foreach (DepotData.Part p in DepotData.Chests)
        {
            GameObject chest = Object.Instantiate(chestPrefab, at + rot * new Vector3(p.X, p.Y, p.Z),
                rot * Quaternion.Euler(0f, p.Yaw, 0f));
            Mark(chest, owner);
        }

        return frame;
    }

    /// D-160: the 4 flagged chests of a frame (test hooks).
    internal static List<Container> ChestsOf(GameObject frame)
    {
        var list = new List<Container>();
        if (frame == null)
        {
            return list;
        }

        foreach (DepotData.Part p in DepotData.Chests)
        {
            Vector3 expected = frame.transform.position + frame.transform.rotation * new Vector3(p.X, p.Y, p.Z);
            foreach (ZNetView v in Near(frame.transform.position, DepotData.ChestPrefab))
            {
                if ((v.transform.position - expected).sqrMagnitude > 0.6f * 0.6f || !v.GetZDO().GetBool(FlagKey))
                {
                    continue;
                }

                Container c = v.GetComponent<Container>();
                if (c != null && !list.Contains(c))
                {
                    list.Add(c);
                    break;
                }
            }
        }

        return list;
    }
}
