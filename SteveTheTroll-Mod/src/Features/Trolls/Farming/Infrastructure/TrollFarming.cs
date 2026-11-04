using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Movement.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Domain;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Farming.Infrastructure;

/// <summary>
///     T-008d step 1 (D-038 / D-044, "Go farm"): a tamed troll gathers its focus item. It looks for the nearest source
///     within FarmRange of
///     where it was sent (then of the owner's bed): standing trees / logs (TreeBase -> m_logPrefab -> TreeLog ->
///     m_subLogPrefab ... drops),
///     rocks (MineRock5 / MineRock) and other destructibles (DropOnDestroyed) whose drop tables contain the item. No focus
///     = the D-038
///     priority (D-058: TrollFocus.DefaultOrder - Yggdrasil wood, ancient bark, iron scrap, birch, silver, copper, pine,
///     oak, tin, wood). Steering: postfix on MonsterAI.UpdateAI (the last MoveTo of the frame
///     wins -> since D-048 a prefix that replaces the AI step while farming); a fight (target creature) or a follow order
///     ends / pauses farming. Drops stay on the ground until the backpack (T-008c) exists.
/// </summary>
internal static partial class TrollFarming
{
    private const float MinOtherHeight = 1.2f;

    private static readonly Func<BaseAI, float, Vector3, float, bool, bool>
        MoveTo = ValheimTrollMovement.MoveToFn; // D-171 (P9): one definition

    private static readonly Action<BaseAI, Vector3> LookAt = ValheimTrollMovement.LookAtFn;

    // D-051: the search walks the game's list of loaded networked objects (ZNetScene) instead of a physics overlap: no buffer to fill up
    // (TEST-021), any radius up to the loaded area (150 m)
    private static readonly AccessTools.FieldRef<ZNetScene, Dictionary<ZDO, ZNetView>> Instances =
        AccessTools.FieldRefAccess<ZNetScene, Dictionary<ZDO, ZNetView>>("m_instances");

    private static readonly Dictionary<int, FarmSourceCache> Known = new();


    /// Nearest source of `item` within range of `center` the troll's weapon tier can damage. Expected outcome: the source or null.
    /// The last search, for the in-game test World_FarmScan.
    internal static string LastScan = "";

    internal static bool LastScanFull;

    private static long _gatherTicks, _gatherWorst;
    private static int _gatherCalls, _gatherScene, _gatherCands;
    private static float _nextGatherLog;

    private static readonly Dictionary<int, FarInfo> FarKnown = new();
    private static readonly Dictionary<long, bool> FarYields = new();
    private static readonly List<ZDO> FarZdos = new();

    internal static string LastFar = "";

    // ZDOMan.FindObjects(sector, list, visitedSectorIndices) is private in the game: the ZDOs of ONE zone (loaded or not), whatever this session knows of it
    private static readonly MethodInfo FindObjectsMethod = AccessTools.Method(typeof(ZDOMan), "FindObjects");

    private static bool _farWarned;

    private static readonly FieldInfo HitAreas = AccessTools.Field(typeof(MineRock5), "m_hitAreas");

    /// In-game test Startup_TamingReflection. Expected outcome: whether both delegates were found.
    internal static bool SelfCheck()
    {
        return MoveTo != null && LookAt != null;
    }

    /// The item the troll farms now ("" = not farming).
    internal static string Farming(Character troll)
    {
        TrollFarmer f = troll != null ? troll.GetComponent<TrollFarmer>() : null;
        return f != null ? f.CurrentItem : "";
    }

    /// The source's drop table contains `item` (through felled trees and their log pieces and what a destructible turns into, 4 levels deep).
    internal static bool Yields(GameObject source, string item, int depth = 0)
    {
        if (source == null || depth > 4)
        {
            return false;
        }

        TreeBase tree = source.GetComponent<TreeBase>();
        if (tree != null)
        {
            return Has(tree.m_dropWhenDestroyed, item) || Yields(tree.m_logPrefab, item, depth + 1);
        }

        TreeLog log = source.GetComponent<TreeLog>();
        if (log != null)
        {
            return Has(log.m_dropWhenDestroyed, item) || Yields(log.m_subLogPrefab, item, depth + 1);
        }

        MineRock5 rock5 = source.GetComponent<MineRock5>();
        if (rock5 != null)
        {
            return Has(rock5.m_dropItems, item);
        }

        MineRock rock = source.GetComponent<MineRock>();
        if (rock != null)
        {
            return Has(rock.m_dropItems, item);
        }

        DropOnDestroyed drop = source.GetComponent<DropOnDestroyed>();
        if (drop != null && Has(drop.m_dropWhenDestroyed, item))
        {
            return true;
        }

        // D-126: a destructible that turns into another object (giant_skull -> giant_skull_frac -> BlackMarble)
        Destructible des = source.GetComponent<Destructible>();
        return des != null && des.m_spawnWhenDestroyed != null && Yields(des.m_spawnWhenDestroyed, item, depth + 1);
    }

    private static bool Has(DropTable t, string item)
    {
        if (t == null || t.m_drops == null)
        {
            return false;
        }

        foreach (DropTable.DropData d in t.m_drops)
        {
            if (d.m_item != null && d.m_item.name == item)
            {
                return true;
            }
        }

        return false;
    }

    internal static string KindOf(GameObject g)
    {
        if (g.GetComponent<TreeLog>() != null)
        {
            return "log";
        }

        if (g.GetComponent<TreeBase>() != null)
        {
            return "tree";
        }

        if (g.GetComponent<MineRock5>() != null || g.GetComponent<MineRock>() != null)
        {
            return "rock";
        }

        return "other";
    }

    private static int MinTier(GameObject g)
    {
        TreeBase tree = g.GetComponent<TreeBase>();
        if (tree != null)
        {
            return tree.m_minToolTier;
        }

        TreeLog log = g.GetComponent<TreeLog>();
        if (log != null)
        {
            return log.m_minToolTier;
        }

        MineRock5 r5 = g.GetComponent<MineRock5>();
        if (r5 != null)
        {
            return r5.m_minToolTier;
        }

        MineRock r = g.GetComponent<MineRock>();
        if (r != null)
        {
            return r.m_minToolTier;
        }

        Destructible d = g.GetComponent<Destructible>();
        return d != null ? d.m_minToolTier : 0;
    }

    /// D-084 performance: every farm source within range of center in ONE pass over the scene (a search used to walk all networked
    /// objects once per goal and range - up to 30 passes over ~5000 objects). Expected outcome: the list filled (cleared first).
    internal static List<Candidate> Gather(Vector3 center, float range, List<Candidate> into)
    {
        into.Clear();
        ZNetScene scene = ZNetScene.instance;
        Dictionary<ZDO, ZNetView> all = scene != null ? Instances(scene) : null;
        if (all == null)
        {
            return into;
        }

        long t0 = Stopwatch.GetTimestamp();
        float r2 = range * range;
        Vector3 bed;
        bool hasBed = TrollNoHit.Bed(out bed);
        float noHitR = ModSettings.NoHitRadius.Value;
        bool noHit = ModSettings.On && hasBed && noHitR > 0f;
        foreach (ZNetView v in all.Values)
        {
            if (v == null)
            {
                continue;
            }

            Vector3 pos = v.transform.position;
            if ((pos - center).sqrMagnitude > r2)
            {
                continue;
            }

            if (noHit && TrollNoHit.Inside(bed, pos, noHitR))
            {
                continue; // D-097: nothing near the house is farmed
            }

            FarmSourceCache src = Source(v);
            if (src == null || src.Collider == null)
            {
                continue;
            }

            into.Add(new Candidate { G = v.gameObject, Src = src, Pos = pos });
        }

        // D-141 (review: Gather was never timed): every 5 s, how often it ran, how long it took and how big the scene dictionary was
        long took = Stopwatch.GetTimestamp() - t0;
        _gatherTicks += took;
        _gatherCalls++;
        _gatherScene += all.Count;
        _gatherCands += into.Count;
        if (took > _gatherWorst)
        {
            _gatherWorst = took;
        }

        if (ModLog.DebugOn && Time.time >= _nextGatherLog)
        {
            _nextGatherLog = Time.time + 5f;
            double perMs = 1000.0 / Stopwatch.Frequency;
            ModLog.Verbose("TrollFarming: last 5 s - Gather " + _gatherCalls + "x, total " +
                           (_gatherTicks * perMs).ToString("F2") + " ms, avg " +
                           (_gatherTicks * perMs / _gatherCalls).ToString("F3") + " ms, worst " +
                           (_gatherWorst * perMs).ToString("F2")
                           + " ms; scene " + _gatherScene / _gatherCalls + " object(s), " +
                           _gatherCands / _gatherCalls + " candidate(s) per call");
            _gatherTicks = _gatherWorst = 0;
            _gatherCalls = _gatherScene = _gatherCands = 0;
        }

        return into;
    }

    internal static GameObject Find(Vector3 center, Vector3 from, TrollFocus.Goal goal, int tier, float range,
        out Collider hit, Func<GameObject, bool> skip = null)
    {
        return FindIn(Gather(center, range, new List<Candidate>()), new SearchSphere(center, range), from, goal, tier, out hit, skip);
    }

    /// The best source of the goal among gathered candidates within range of center (see Find). Expected outcome: the source or null.
    /// <summary>The ball a search looks in: its centre and radius in metres.</summary>
    internal readonly struct SearchSphere
    {
        internal SearchSphere(Vector3 center, float range) { Center = center; Range = range; }
        internal Vector3 Center { get; }
        internal float Range { get; }
    }

    /// <summary>The long-range search band: sources up to Range metres from the troll, but beyond NearRange (the near search covers that).</summary>
    internal readonly struct SearchRange
    {
        internal SearchRange(float range, float nearRange) { Range = range; NearRange = nearRange; }
        internal float Range { get; }
        internal float NearRange { get; }
    }

    internal static GameObject FindIn(List<Candidate> cands, SearchSphere area, Vector3 from, TrollFocus.Goal goal,
        int tier, out Collider hit, Func<GameObject, bool> skip = null)
    {
        Vector3 center = area.Center;
        float range = area.Range;
        string item = goal.Item;
        hit = null;
        GameObject best = null;
        float bestD = float.MaxValue, r2 = range * range;
        int[] counts = new int[3];
        int seen = 0, skipped = 0;
        foreach (Candidate c in cands)
        {
            if (c.G == null || (c.Pos - center).sqrMagnitude > r2)
            {
                continue;
            }

            seen++;
            FarmSourceCache src = c.Src;
            if ((skip != null && skip(c.G)) || src.MinTier > tier || !src.Yields(item) ||
                !TrollFocus.SourceMatches(goal, src.PrefabName))
            {
                continue;
            }

            if (src.Tier == TamingRules.TierNone || (src.Young && ModSettings.FarmSkipYoung.Value))
            {
                skipped++;
                continue;
            }

            counts[src.Tier]++;
            Vector3 at = src.IsTree ? c.Pos : src.Collider.bounds.ClosestPoint(from); // trees: the trunk (TEST-020)
            float d = TamingRules.SourceScore(src.Tier, Vector3.Distance(from, at));
            if (d < bestD)
            {
                bestD = d;
                best = c.G;
                hit = src.Collider;
            }
        }

        LastScanFull = false;
        if (ModLog.DebugOn)
        {
            LastScan = "sources " + seen + ", logs/rocks " + counts[0] + ", trees " + counts[1] + ", other " +
                       counts[2] + ", too small " + skipped
                       + ", best " + (best != null ? best.name + " (" + KindOf(best) + ")" : "none");
            if (best != null)
            {
                ModLog.Verbose("TrollFarming: search '" + item + "'" +
                               (goal.Sources.Length > 0 ? " from " + string.Join(" / ", goal.Sources) : "") +
                               " within " + range + " m of " + center.ToString("F0") + ": " + LastScan +
                               (best != null ? " score " + bestD.ToString("F1") : ""));
            }
        }
        else
        {
            LastScan = "best " + (best != null ? best.name : "none");
        }

        return best;
    }

    private static FarInfo FarOf(int hash)
    {
        FarInfo fi;
        if (FarKnown.TryGetValue(hash, out fi))
        {
            return fi;
        }

        GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(hash) : null;
        if (prefab != null)
        {
            bool tree = prefab.GetComponent<TreeBase>() != null, log = prefab.GetComponent<TreeLog>() != null;
            bool rock = prefab.GetComponent<MineRock5>() != null || prefab.GetComponent<MineRock>() != null;
            if (tree || log || rock)
            {
                fi.Source = true;
                fi.Name = Utils.GetPrefabName(prefab);
                fi.MinTier = MinTier(prefab);
                fi.Young = !log && !rock && TamingRules.IsYoungGrowth(fi.Name);
            }
        }

        FarKnown[hash] = fi;
        return fi;
    }
}

/// Remembers per source object which items it yields (drop tables never change at runtime), its biggest collider and its tier.
internal sealed class FarmSourceCache : MonoBehaviour
{
    private readonly Dictionary<string, bool> _yields = new();
    internal Collider Collider;
    internal bool IsTree;
    internal int MinTier, Tier = TamingRules.TierNone;
    internal string PrefabName = "";
    internal bool Young; // D-153: sapling / bush by name

    internal bool Yields(string item)
    {
        bool y;
        if (!_yields.TryGetValue(item, out y))
        {
            y = TrollFarming.Yields(gameObject, item);
            _yields[item] = y;
        }

        return y;
    }
}
