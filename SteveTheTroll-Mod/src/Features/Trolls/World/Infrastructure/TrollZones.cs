using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.World.Zones
{
    /// <summary>
    /// D-084 (user: "small area around the trolls that are active so they farm all the time ... not follow the game rendering areas setting,
    /// something like 50m around them at any time"): while the local player wears the slippers, every own tamed troll outside the player's
    /// active area keeps the zones within TrollZoneRadius (50 m) of it loaded and simulated on this client:
    ///  - ZoneSystem.PokeLocalZone keeps the terrain of those zones (the game drops a zone that is not poked within its TTL);
    ///  - the ZDOs of those sectors are added to the list ZNetScene.CreateObjects / RemoveObjects work on (so the troll and its trees / rocks
    ///    exist and are not removed);
    ///  - unowned ZDOs there are claimed by this client (the game only claims inside a peer's own area), so the troll's AI runs and the
    ///    trees / veins it hits take damage.
    /// The own trolls are tracked by ZDOID (seen loaded, or found once per world in the ZDO list), so a far troll is kept even when it was never
    /// near the player. Only zones outside the player's active area are touched; nothing at all happens without slippers.
    /// </summary>
    internal static class TrollZones
    {
        const float TickSeconds = 1f, ZoneSize = 64f;
        static readonly HashSet<ZDOID> Trolls = new HashSet<ZDOID>();
        static readonly List<Vector2s> Kept = new List<Vector2s>();
        static readonly HashSet<Vector2s> KeptSet = new HashSet<Vector2s>();
        static readonly List<ZDO> Temp = new List<ZDO>();
        static readonly List<Vector2s> Stale = new List<Vector2s>();
        static readonly List<Vector3> Centres = new List<Vector3>();       // the far trolls of this tick: objects are claimed only within the farming radius of one
        static readonly List<ZDO> Scan = new List<ZDO>();
        static readonly System.Reflection.MethodInfo Poke = AccessTools.Method(typeof(ZoneSystem), "PokeLocalZone");
        static readonly System.Reflection.MethodInfo FindObjects = AccessTools.Method(typeof(ZDOMan), "FindObjects");
        // TEST-037: the 3rd parameter is HashSet<ZoneSystem.SectorIndex> in 1.0.20, not HashSet<int> - made from the real parameter type
        static readonly System.Type VisitedType = FindObjects != null && FindObjects.GetParameters().Length == 3 ? FindObjects.GetParameters()[2].ParameterType : null;
        static bool _broken;

        // D-138 (review of the 2026-10-30 test runs: 1 % lows of 11 - 17 fps, hitches up to 417 ms, ~5 000 unowned objects scanned per kept-zone pass): the world is searched
        // ONCE per tick (1 s) and the result is kept per zone; CreateObjects (30+ times a second) only appends it - it used to run FindObjects for every kept zone, with new
        // argument arrays, new visited sets and a new de-duplication set, on every call.
        sealed class ZoneObjects
        {
            internal Vector2s Zone;
            internal readonly List<ZDO> All = new List<ZDO>();
            internal readonly List<ZDO> NotDistant = new List<ZDO>();
        }
        static readonly List<ZoneObjects> Cached = new List<ZoneObjects>();
        static readonly Dictionary<Vector2s, ZoneObjects> CachePool = new Dictionary<Vector2s, ZoneObjects>();
        static readonly HashSet<ZDO> CacheSeen = new HashSet<ZDO>();
        static readonly HashSet<ZDOID> LastClaimed = new HashSet<ZDOID>(), NowClaimed = new HashSet<ZDOID>();
        static readonly object[] FindArgs = new object[3];
        static readonly object[] PokeArgs = new object[1];
        static object _visited;
        static readonly System.Reflection.MethodInfo VisitedClear = VisitedType != null ? VisitedType.GetMethod("Clear", System.Type.EmptyTypes) : null;
        static readonly System.Diagnostics.Stopwatch Clock = new System.Diagnostics.Stopwatch();
        static double _tickMs, _tickMaxMs, _createMs, _createMaxMs;
        static int _tickCount, _createCount, _scanned, _claimedTotal, _reclaimed;
        static float _nextStats, _nextZoneLog;
        static double _fullMs, _fullMaxMs;
        static int _fullCount;
        static long _findTicks, _claimTicks;
        static readonly System.Text.StringBuilder _zoneLines = new System.Text.StringBuilder();

        /// One FindObjects call with reused arguments and a cleared (so equal to fresh) visited set. Expected outcome: `into` holds the zone's ZDOs.
        static void Find(ZDOMan zm, Vector2s zone, List<ZDO> into)
        {
            if (_visited == null) _visited = NewVisited();
            if (VisitedClear != null) VisitedClear.Invoke(_visited, null); else _visited = NewVisited();
            FindArgs[0] = zone; FindArgs[1] = into; FindArgs[2] = _visited;
            FindObjects.Invoke(zm, FindArgs);
        }

        /// A fresh "visited sectors" set of the type the game's FindObjects wants. Expected outcome: the set, or null when it cannot be made.
        internal static object NewVisited() { return VisitedType != null ? System.Activator.CreateInstance(VisitedType) : null; }

        /// In-game test: the reflected methods exist with the parameters this class passes, and one FindObjects call on the player's zone works.
        /// Expected outcome: "PASS: ..." / "FAIL: ...".
        internal static string SelfCheck()
        {
            if (Poke == null || FindObjects == null || VisitedType == null) return "FAIL: PokeLocalZone " + (Poke != null) + ", FindObjects " + (FindObjects != null) + ", visited type " + (VisitedType != null ? VisitedType.Name : "none");
            var p = Player.m_localPlayer;
            if (p == null || ZDOMan.instance == null) return "SKIP: no player / ZDOMan (methods found, visited " + VisitedType + ")";
            try
            {
                var list = new List<ZDO>();
                FindObjects.Invoke(ZDOMan.instance, new object[] { ZoneSystem.GetZone(p.transform.position), list, NewVisited() });
                return "PASS: FindObjects on the player's zone -> " + list.Count + " object(s), visited set " + VisitedType.Name + ", poke " + Poke.GetParameters()[0].ParameterType.Name;
            }
            catch (System.Exception e) { return "FAIL: FindObjects threw " + (e.InnerException ?? e).GetType().Name + ": " + (e.InnerException ?? e).Message; }
        }
        static int _scanIndex;
        static bool _scanDone;
        static ZDOMan _scanFor;
        static float _next;
        static int _lastKept = -1;

        /// Sectors kept now (for the in-game check and the log).
        internal static int KeptCount { get { return Kept.Count; } }

        /// Plugin.Update. Expected outcome: how many zones are kept alive this second (-1 = nothing done this frame).
        internal static int Tick()
        {
            if (_broken || Time.time < _next) return -1;
            try { return TickInner(); }
            catch (System.Exception e) { Break("Tick", e); return -1; }
        }

        // TEST-037: an exception here ran every frame and stopped the mod's later ticks - now it switches the zones off once, with one error line.
        static void Break(string where, System.Exception e)
        {
            _broken = true;
            Kept.Clear(); KeptSet.Clear();
            var inner = e.InnerException ?? e;
            ModLog.Error("TrollZones: " + where + " failed - kept zones switched OFF for this session: " + inner.GetType().Name + ": " + inner.Message);
        }

        static int TickInner()
        {
            if (Time.time < _next) return -1;
            _next = Time.time + TickSeconds;
            Clock.Reset(); Clock.Start();                       // D-141: the whole tick is measured, the clean-up included
            Kept.Clear(); KeptSet.Clear(); Cached.Clear(); Centres.Clear();
            var p = Player.m_localPlayer;
            var zm = ZDOMan.instance;
            var zs = ZoneSystem.instance;
            if (p == null || zm == null || zs == null || ZNetScene.instance == null || ZNet.instance == null || !ModSettings.On || ModSettings.TrollZoneRadius.Value <= 0f || !TrollTaming.Wears(p)) { if (CachePool.Count > 0) CachePool.Clear(); return Log(0); }
            Track(p, zm);
            long me = p.GetPlayerID();
            var refPos = ZNet.instance != null ? ZNet.instance.GetReferencePosition() : p.transform.position;
            float r = ModSettings.TrollZoneRadius.Value;
            var gone = new List<ZDOID>();
            foreach (var id in Trolls)
            {
                var z = zm.GetZDO(id);
                if (z == null || z.GetFloat(ZDOVars.s_health, 1f) <= 0f || z.GetLong(TrollTaming.OwnerKey, 0L) != me) { gone.Add(id); continue; }
                var pos = z.GetPosition();
                if (ZNetScene.InActiveArea(ZoneSystem.GetZonePos(ZoneSystem.GetZone(pos)), refPos)) continue;      // the game runs it anyway
                Centres.Add(pos);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        var zone = ZoneSystem.GetZone(pos + new Vector3(dx * ZoneSize, 0f, dz * ZoneSize));
                        var c = ZoneSystem.GetZonePos(zone);
                        float ex = Mathf.Max(Mathf.Abs(pos.x - c.x) - ZoneSize / 2f, 0f), ez = Mathf.Max(Mathf.Abs(pos.z - c.z) - ZoneSize / 2f, 0f);
                        if (ex * ex + ez * ez > r * r || KeptSet.Contains(zone) || ZNetScene.InActiveArea(c, refPos)) continue;
                        KeptSet.Add(zone); Kept.Add(zone);
                    }
            }
            foreach (var id in gone) Trolls.Remove(id);
            // D-141 (review): the pool kept an entry (and its ZDO lists) for every zone a troll ever visited and the tick walked them all; only the kept zones stay
            if (CachePool.Count > Kept.Count)
            {
                Stale.Clear();
                foreach (var key in CachePool.Keys) if (!KeptSet.Contains(key)) Stale.Add(key);
                foreach (var key in Stale) CachePool.Remove(key);
            }
            long session = ZDOMan.GetSessionID();
            int claimed = 0, scanned = 0, reclaimed = 0;
            bool debug = ModLog.DebugOn;
            if (debug) NowClaimed.Clear();
            CacheSeen.Clear();
            var sim = ZNet.instance.GetSyncedSimulationDistance();
            var centre = ZoneSystem.GetZone(refPos);
            bool claimNear = ModSettings.ZoneClaimNearBand.Value;
            float claimReach = r + ClaimMargin, claimReach2 = claimReach * claimReach;
            bool zoneLog = debug && Time.time >= _nextZoneLog;
            if (zoneLog) { _nextZoneLog = Time.time + 5f; _zoneLines.Length = 0; }
            long t0;
            foreach (var zone in Kept)
            {
                string band = !debug && claimNear ? null : (Within(centre, zone, sim.NearSimulationDistance, sim.IsClassic, false) ? "near" : Within(centre, zone, sim.TotalSimulationDistance, sim.IsClassic, true) ? "far" : "beyond");
                int zoneUnowned = 0, zoneClaimed = 0;
                if (Poke != null) { PokeArgs[0] = zone; Poke.Invoke(zs, PokeArgs); }
                Temp.Clear();
                if (FindObjects == null) continue;
                t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                Find(zm, zone, Temp);
                _findTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                scanned += Temp.Count;
                ZoneObjects zo;
                if (!CachePool.TryGetValue(zone, out zo)) { zo = new ZoneObjects { Zone = zone }; CachePool[zone] = zo; }
                zo.All.Clear(); zo.NotDistant.Clear();
                bool mayClaim = claimNear || band != "near";
                t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                foreach (var z in Temp)
                {
                    if (z == null) continue;
                    if (z.Persistent && !z.HasOwner())
                    {
                        zoneUnowned++;
                        // D-139: only what a troll can reach. The kept zones are whole 64 m squares (3 x 3 around each troll, ~5 000 objects), the troll works within
                        // TrollZoneRadius (50 m): the rest was claimed every second for nothing (thousands of SetOwner calls a second, each a ZDO update for the network).
                        // T-211 (run 20261103-124703): the game releases an object its peer owns once it leaves the peer's ACTIVE area, and claims unowned ones only inside it.
                        // A far troll that walks through a zone the game still lists as NEAR was therefore released and, with ClaimNearBand off, never claimed again (it stopped, spot-return /
                        // far-troll-goes-home never arrived). The own tracked trolls (a handful of ZDOs) are claimed in every band; ClaimNearBand only governs the other objects.
                        bool ownTroll = Trolls.Contains(z.m_uid);
                        if ((mayClaim || ownTroll) && claimed < MaxClaimsPerTick && NearTroll(z.GetPosition(), claimReach2))
                        {
                            z.SetOwner(session); claimed++; zoneClaimed++;
                            if (ownTroll && !mayClaim && debug) ModLog.Verbose("TrollZones: own troll " + z.m_uid + " claimed in a zone of the game's near band (the game had released it outside the active area)");
                            if (debug) { if (LastClaimed.Contains(z.m_uid)) reclaimed++; NowClaimed.Add(z.m_uid); }
                        }
                    }
                    if (!CacheSeen.Add(z)) continue;               // portal objects can come once per sector call
                    zo.All.Add(z);
                    if (!z.Distant) zo.NotDistant.Add(z);
                }
                _claimTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                if (zoneLog) _zoneLines.Append("\n    zone ").Append(zone.x).Append('/').Append(zone.y).Append(' ').Append(band).Append(": ").Append(Temp.Count).Append(" object(s), ").Append(zoneUnowned).Append(" unowned persistent, ").Append(zoneClaimed).Append(" claimed");
                Cached.Add(zo);
            }
            if (debug) { LastClaimed.Clear(); foreach (var id in NowClaimed) LastClaimed.Add(id); }
            _scanned += scanned; _claimedTotal += claimed; _reclaimed += reclaimed;
            Clock.Stop();
            double ms = Clock.Elapsed.TotalMilliseconds;
            _tickMs += ms; _tickCount++; if (ms > _tickMaxMs) _tickMaxMs = ms;
            if (zoneLog) ModLog.Verbose("TrollZones: per kept zone (band = the game's simulation band; near " + sim.NearSimulationDistance + ", total " + sim.TotalSimulationDistance + " zones; claim in the near band " + (claimNear ? "ON" : "OFF (test)") + ")" + _zoneLines);
            if (claimed > 0 && debug) ModLog.Verbose("TrollZones: " + claimed + " unowned object(s) claimed in " + Kept.Count + " kept zone(s), " + reclaimed + " of them were already claimed by this client in the pass before (the game gave them back)");
            Stats();
            return Log(Kept.Count);
        }

        // Review figures (D-138): what the zone work costs, every 5 s - the numbers to compare before / after and with [TrollZones] radius 0.
        static void Stats()
        {
            if (!ModLog.DebugOn || Time.time < _nextStats) return;
            _nextStats = Time.time + 5f;
            ModLog.Verbose("TrollZones: last 5 s - Tick " + _tickCount + "x avg " + (_tickCount > 0 ? _tickMs / _tickCount : 0).ToString("F2") + " ms, max " + _tickMaxMs.ToString("F2") + " ms; CreateObjects prefix " + _createCount
                + "x avg " + (_createCount > 0 ? _createMs / _createCount : 0).ToString("F3") + " ms, max " + _createMaxMs.ToString("F3") + " ms; " + Kept.Count + " zone(s), " + _scanned + " object(s) scanned, "
                + _claimedTotal + " claimed (" + _reclaimed + " of them again after the game gave them back)");
            ModLog.Verbose("TrollZones: last 5 s - the game's whole CreateObjects (with our prefix) " + _fullCount + "x avg " + (_fullCount > 0 ? _fullMs / _fullCount : 0).ToString("F2") + " ms, max " + _fullMaxMs.ToString("F2") + " ms; zone cache " + CachePool.Count + " entr(ies) for " + Kept.Count + " kept zone(s)");
            _fullMs = _fullMaxMs = 0; _fullCount = 0;
            double perMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            ModLog.Verbose("TrollZones: last 5 s - of the Tick time " + (_findTicks * perMs).ToString("F1") + " ms went into FindObjects and " + (_claimTicks * perMs).ToString("F1") + " ms into the claim / cache loop");
            _findTicks = _claimTicks = 0;
            _tickMs = _tickMaxMs = _createMs = _createMaxMs = 0; _tickCount = _createCount = _scanned = _claimedTotal = _reclaimed = 0;
        }

        /// Is `zone` inside the game's own ring of `radius` zones around `centre`? Classic distance = a square (the game's IsClassic rule),
        /// else the game's ZonesWithinRadius. Expected outcome: the game's answer for this zone.
        static bool Within(Vector2s centre, Vector2s zone, int radius, bool classic, bool ghost)
        {
            if (classic) return System.Math.Max(System.Math.Abs(zone.x - centre.x), System.Math.Abs(zone.y - centre.y)) <= radius;
            return ZoneSystem.instance.ZonesWithinRadius(centre, zone, radius, ghost);
        }

        const int MaxClaimsPerTick = 500;     // D-142: test run 235053 had single ticks of 26 - 35 ms when new zones came in; claims are repeated every second, so the rest follows
        const float ClaimMargin = 10f;       // m beyond the farming radius: the troll walks, trees fall, drops roll

        /// Is `at` within sqrt(reach2) metres (flat) of a far troll of this tick? Expected outcome: true when a troll can reach it.
        static bool NearTroll(Vector3 at, float reach2)
        {
            for (int i = 0; i < Centres.Count; i++)
            {
                float dx = at.x - Centres[i].x, dz = at.z - Centres[i].z;
                if (dx * dx + dz * dz <= reach2) return true;
            }
            return false;
        }

        static int Log(int n)
        {
            if (n != _lastKept) { _lastKept = n; ModLog.Verbose("TrollZones: " + n + " zone(s) kept alive around " + Trolls.Count + " own troll(s) outside the player's area"); }
            return n;
        }

        // Own tamed trolls: the loaded ones every tick, plus one pass over the world's troll ZDOs (a slice per tick) per world.
        static void Track(Player p, ZDOMan zm)
        {
            long me = p.GetPlayerID();
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c) || TrollTaming.Owner(c) != me) continue;
                var z = TrollTaming.Zdo(c);
                if (z != null) Trolls.Add(z.m_uid);
            }
            if (_scanFor != zm) { _scanFor = zm; _scanDone = false; _scanIndex = 0; Scan.Clear(); Trolls.Clear(); }
            if (_scanDone) return;
            _scanDone = zm.GetAllZDOsWithPrefabIterative(TrollTaming.TrollPrefab, Scan, ref _scanIndex);
            if (!_scanDone) return;
            int found = 0;
            foreach (var z in Scan)
                if (z != null && z.GetBool(ZDOVars.s_tamed, false) && z.GetLong(TrollTaming.OwnerKey, 0L) == me) { Trolls.Add(z.m_uid); found++; }
            ModLog.Verbose("TrollZones: world scan done - " + Scan.Count + " troll(s) in the world, " + found + " own tamed");
            Scan.Clear();
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
            static float _nextLog;
            static long _started;

            // D-141 (review: "the timing covers only this mod's prefix"): the postfix closes the time of the WHOLE game method (prefix + the game's own creating / removing).
            internal static void Postfix()
            {
                if (_started == 0) return;
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - _started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                _started = 0;
                _fullMs += ms; _fullCount++; if (ms > _fullMaxMs) _fullMaxMs = ms;
            }

            internal static void Prefix(List<ZDO> currentNearObjects)
            {
                _started = System.Diagnostics.Stopwatch.GetTimestamp();
                if (_broken || Cached.Count == 0 || currentNearObjects == null || ZNet.instance == null || ZoneSystem.instance == null) return;
                try
                {
                    Clock.Reset(); Clock.Start();
                    var sim = ZNet.instance.GetSyncedSimulationDistance();
                    var centre = ZoneSystem.GetZone(ZNet.instance.GetReferencePosition());
                    int notLoaded = 0, nearZones = 0, farZones = 0, outZones = 0, before = currentNearObjects.Count, added = 0;
                    // the zone's band is decided on every call (the player moves between ticks); only the search is cached
                    foreach (var zo in Cached)
                    {
                        if (Within(centre, zo.Zone, sim.NearSimulationDistance, sim.IsClassic, false)) { nearZones++; continue; }
                        List<ZDO> list;
                        if (Within(centre, zo.Zone, sim.TotalSimulationDistance, sim.IsClassic, true)) { farZones++; list = zo.NotDistant; }
                        else { outZones++; list = zo.All; }
                        // D-150 (test run 014829: 134 NullReferenceExceptions in TerrainComp.Load, "Terrain compiler could not find hmap"): objects of a kept zone are only handed to the
                        // game once that zone's terrain is loaded (we poke it every second, it loads a moment later) - a TerrainComp created before its heightmap throws every frame.
                        if (!ZoneSystem.instance.IsZoneLoaded(zo.Zone)) { notLoaded++; continue; }
                        for (int k = 0; k < list.Count; k++)
                        {
                            var z = list[k];
                            if (z == null || !z.IsValid()) continue;     // destroyed since the last search
                            currentNearObjects.Add(z); added++;
                        }
                    }
                    Clock.Stop();
                    double ms = Clock.Elapsed.TotalMilliseconds;
                    _createMs += ms; _createCount++; if (ms > _createMaxMs) _createMaxMs = ms;
                    if (Time.time >= _nextLog)
                    {
                        _nextLog = Time.time + 5f;
                        ModLog.Verbose("TrollZones: CreateObjects - simulation near " + sim.NearSimulationDistance + " / total " + sim.TotalSimulationDistance + (sim.IsClassic ? " classic" : "")
                            + ", game listed " + before + " ZDO(s); kept zones " + Cached.Count + ": " + nearZones + " already in the game's near list, " + farZones + " in its far band (Distant ZDOs left to the game), "
                            + outZones + " beyond; " + notLoaded + " not loaded yet (skipped); added " + added + " from the cache");
                    }
                    ModLog.Check("TrollZones: near list grew by the added ZDOs only", currentNearObjects.Count - before, added, 0f, "ZDO(s)");
                }
                catch (System.Exception e) { Break("CreateObjects", e); }
            }
        }
    }
}
