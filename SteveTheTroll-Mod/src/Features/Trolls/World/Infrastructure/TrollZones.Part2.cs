using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.World.Infrastructure;

/// <summary>Part 2 of TrollZones (split out so the class stays under the size limit).</summary>
internal static partial class TrollZones
{
    // Review figures (D-138): what the zone work costs, every 5 s - the numbers to compare before / after and with [TrollZones] radius 0.
    private static void Stats()
    {
        if (!ModLog.DebugOn || Time.time < _nextStats)
        {
            return;
        }

        _nextStats = Time.time + 5f;
        ModLog.Verbose("TrollZones: last 5 s - Tick " + _tickCount + "x avg " +
                       (_tickCount > 0 ? _tickMs / _tickCount : 0).ToString("F2") + " ms, max " +
                       _tickMaxMs.ToString("F2") + " ms; CreateObjects prefix " + _createCount
                       + "x avg " + (_createCount > 0 ? _createMs / _createCount : 0).ToString("F3") + " ms, max " +
                       _createMaxMs.ToString("F3") + " ms; " + Kept.Count + " zone(s), " + _scanned +
                       " object(s) scanned, "
                       + _claimedTotal + " claimed (" + _reclaimed + " of them again after the game gave them back)");
        ModLog.Verbose("TrollZones: last 5 s - the game's whole CreateObjects (with our prefix) " + _fullCount +
                       "x avg " + (_fullCount > 0 ? _fullMs / _fullCount : 0).ToString("F2") + " ms, max " +
                       _fullMaxMs.ToString("F2") + " ms; zone cache " + CachePool.Count + " entr(ies) for " +
                       Kept.Count + " kept zone(s)");
        _fullMs = _fullMaxMs = 0;
        _fullCount = 0;
        double perMs = 1000.0 / Stopwatch.Frequency;
        ModLog.Verbose("TrollZones: last 5 s - of the Tick time " + (_findTicks * perMs).ToString("F1") +
                       " ms went into FindObjects and " + (_claimTicks * perMs).ToString("F1") +
                       " ms into the claim / cache loop");
        _findTicks = _claimTicks = 0;
        _tickMs = _tickMaxMs = _createMs = _createMaxMs = 0;
        _tickCount = _createCount = _scanned = _claimedTotal = _reclaimed = 0;
    }

    /// Is `zone` inside the game's own ring of `radius` zones around `centre`? Classic distance = a square (the game's IsClassic rule),
    /// else the game's ZonesWithinRadius. Expected outcome: the game's answer for this zone.
    private static bool Within(Vector2s centre, Vector2s zone, int radius, bool classic, bool ghost)
    {
        if (classic)
        {
            return Math.Max(Math.Abs(zone.x - centre.x), Math.Abs(zone.y - centre.y)) <= radius;
        }

        return ZoneSystem.instance.ZonesWithinRadius(centre, zone, radius, ghost);
    }

    /// Is `at` within sqrt(reach2) metres (flat) of a far troll of this tick? Expected outcome: true when a troll can reach it.
    private static bool NearTroll(Vector3 at, float reach2)
    {
        for (int i = 0; i < Centres.Count; i++)
        {
            float dx = at.x - Centres[i].x, dz = at.z - Centres[i].z;
            if (dx * dx + dz * dz <= reach2)
            {
                return true;
            }
        }

        return false;
    }

    private static int Log(int n)
    {
        if (n != _lastKept)
        {
            _lastKept = n;
            ModLog.Verbose("TrollZones: " + n + " zone(s) kept alive around " + Trolls.Count +
                           " own troll(s) outside the player's area");
        }

        return n;
    }

    // Own tamed trolls: the loaded ones every tick, plus one pass over the world's troll ZDOs (a slice per tick) per world.
    private static void Track(Player p, ZDOMan zm)
    {
        long me = p.GetPlayerID();
        foreach (Character c in Character.GetAllCharacters())
        {
            if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c) || TrollTaming.Owner(c) != me)
            {
                continue;
            }

            ZDO z = TrollTaming.Zdo(c);
            if (z != null)
            {
                Trolls.Add(z.m_uid);
            }
        }

        if (_scanFor != zm)
        {
            _scanFor = zm;
            _scanDone = false;
            _scanIndex = 0;
            Scan.Clear();
            Trolls.Clear();
        }

        if (_scanDone)
        {
            return;
        }

        _scanDone = zm.GetAllZDOsWithPrefabIterative(TrollTaming.TrollPrefab, Scan, ref _scanIndex);
        if (!_scanDone)
        {
            return;
        }

        int found = 0;
        foreach (ZDO z in Scan)
        {
            if (z != null && z.GetBool(ZDOVars.s_tamed) && z.GetLong(TrollTaming.OwnerKey) == me)
            {
                Trolls.Add(z.m_uid);
                found++;
            }
        }

        ModLog.Verbose(
            "TrollZones: world scan done - " + Scan.Count + " troll(s) in the world, " + found + " own tamed");
        Scan.Clear();
    }

    // D-138 (review of the 2026-10-30 test runs: 1 % lows of 11 - 17 fps, hitches up to 417 ms, ~5 000 unowned objects scanned per kept-zone pass): the world is searched
    // ONCE per tick (1 s) and the result is kept per zone; CreateObjects (30+ times a second) only appends it - it used to run FindObjects for every kept zone, with new
    // argument arrays, new visited sets and a new de-duplication set, on every call.
    private sealed class ZoneObjects
    {
        internal readonly List<ZDO> All = new();
        internal readonly List<ZDO> NotDistant = new();
        internal Vector2s Zone;
    }

    /// The kept sectors' objects join the list the game creates / keeps (ZNetScene.CreateDestroyObjects passes the same list to RemoveObjects).
    // Harmony hook: src/Integrations/Harmony/TrollsWorldPatches.cs, TrollZones_Create (MIGRATION_DECISIONS A5)
    internal static class Create
    {
        // TEST-048 / TEST-049: 1.0.21 builds the near list from the player's SIMULATION distance (Settings > graphics; 18 000+ ZDOs in
        // TEST-049), which is larger than the active area the kept zones are chosen against. A kept zone inside that near distance is
        // already listed (adding it again made the game create the troll twice); in the far band the game lists its Distant ZDOs in the
        // distant list. So per call: near-band zones add nothing, far-band zones add only non-Distant ZDOs, zones beyond add all - the
        // lists stay disjoint without a per-frame set of the whole near list (TEST-049: that set cost ~19 000 inserts 30 times a second).
        private static float _nextLog;
        private static long _started;

        // D-141 (review: "the timing covers only this mod's prefix"): the postfix closes the time of the WHOLE game method (prefix + the game's own creating / removing).
        internal static void Postfix()
        {
            if (_started == 0)
            {
                return;
            }

            double ms = (Stopwatch.GetTimestamp() - _started) * 1000.0 / Stopwatch.Frequency;
            _started = 0;
            _fullMs += ms;
            _fullCount++;
            if (ms > _fullMaxMs)
            {
                _fullMaxMs = ms;
            }
        }

        internal static void Prefix(List<ZDO> currentNearObjects)
        {
            _started = Stopwatch.GetTimestamp();
            if (_broken || Cached.Count == 0 || currentNearObjects == null || ZNet.instance == null ||
                ZoneSystem.instance == null)
            {
                return;
            }

            try
            {
                Clock.Reset();
                Clock.Start();
                SimulationDistance sim = ZNet.instance.GetSyncedSimulationDistance();
                Vector2s centre = ZoneSystem.GetZone(ZNet.instance.GetReferencePosition());
                int notLoaded = 0,
                    nearZones = 0,
                    farZones = 0,
                    outZones = 0,
                    before = currentNearObjects.Count,
                    added = 0;
                // the zone's band is decided on every call (the player moves between ticks); only the search is cached
                foreach (ZoneObjects zo in Cached)
                {
                    if (Within(centre, zo.Zone, sim.NearSimulationDistance, sim.IsClassic, false))
                    {
                        nearZones++;
                        continue;
                    }

                    List<ZDO> list;
                    if (Within(centre, zo.Zone, sim.TotalSimulationDistance, sim.IsClassic, true))
                    {
                        farZones++;
                        list = zo.NotDistant;
                    }
                    else
                    {
                        outZones++;
                        list = zo.All;
                    }

                    // D-150 (test run 014829: 134 NullReferenceExceptions in TerrainComp.Load, "Terrain compiler could not find hmap"): objects of a kept zone are only handed to the
                    // game once that zone's terrain is loaded (we poke it every second, it loads a moment later) - a TerrainComp created before its heightmap throws every frame.
                    if (!ZoneSystem.instance.IsZoneLoaded(zo.Zone))
                    {
                        notLoaded++;
                        continue;
                    }

                    for (int k = 0; k < list.Count; k++)
                    {
                        ZDO z = list[k];
                        if (z == null || !z.IsValid())
                        {
                            continue; // destroyed since the last search
                        }

                        currentNearObjects.Add(z);
                        added++;
                    }
                }

                Clock.Stop();
                double ms = Clock.Elapsed.TotalMilliseconds;
                _createMs += ms;
                _createCount++;
                if (ms > _createMaxMs)
                {
                    _createMaxMs = ms;
                }

                if (Time.time >= _nextLog)
                {
                    _nextLog = Time.time + 5f;
                    ModLog.Verbose("TrollZones: CreateObjects - simulation near " + sim.NearSimulationDistance +
                                   " / total " + sim.TotalSimulationDistance + (sim.IsClassic ? " classic" : "")
                                   + ", game listed " + before + " ZDO(s); kept zones " + Cached.Count + ": " +
                                   nearZones + " already in the game's near list, " + farZones +
                                   " in its far band (Distant ZDOs left to the game), "
                                   + outZones + " beyond; " + notLoaded + " not loaded yet (skipped); added " + added +
                                   " from the cache");
                }

                ModLog.Check("TrollZones: near list grew by the added ZDOs only", currentNearObjects.Count - before,
                    added, 0f, "ZDO(s)");
            }
            catch (Exception e)
            {
                Break("CreateObjects", e);
            }
        }
    }
}
