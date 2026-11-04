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

/// <summary>
///     D-084 (user: "small area around the trolls that are active so they farm all the time ... not follow the game
///     rendering areas setting,
///     something like 50m around them at any time"): while the local player wears the slippers, every own tamed troll
///     outside the player's
///     active area keeps the zones within TrollZoneRadius (50 m) of it loaded and simulated on this client:
///     - ZoneSystem.PokeLocalZone keeps the terrain of those zones (the game drops a zone that is not poked within its
///     TTL);
///     - the ZDOs of those sectors are added to the list ZNetScene.CreateObjects / RemoveObjects work on (so the troll and
///     its trees / rocks
///     exist and are not removed);
///     - unowned ZDOs there are claimed by this client (the game only claims inside a peer's own area), so the troll's AI
///     runs and the
///     trees / veins it hits take damage.
///     The own trolls are tracked by ZDOID (seen loaded, or found once per world in the ZDO list), so a far troll is kept
///     even when it was never
///     near the player. Only zones outside the player's active area are touched; nothing at all happens without slippers.
/// </summary>
internal static partial class TrollZones
{
    private const float TickSeconds = 1f, ZoneSize = 64f;

    private const int
        MaxClaimsPerTick =
            500; // D-142: test run 235053 had single ticks of 26 - 35 ms when new zones came in; claims are repeated every second, so the rest follows

    private const float ClaimMargin = 10f; // m beyond the farming radius: the troll walks, trees fall, drops roll
    private static readonly HashSet<ZDOID> Trolls = new();
    private static readonly List<Vector2s> Kept = new();
    private static readonly HashSet<Vector2s> KeptSet = new();
    private static readonly List<ZDO> Temp = new();
    private static readonly List<Vector2s> Stale = new();

    private static readonly List<Vector3>
        Centres = new(); // the far trolls of this tick: objects are claimed only within the farming radius of one

    private static readonly List<ZDO> Scan = new();
    private static readonly MethodInfo Poke = AccessTools.Method(typeof(ZoneSystem), "PokeLocalZone");

    private static readonly MethodInfo FindObjects = AccessTools.Method(typeof(ZDOMan), "FindObjects");

    // TEST-037: the 3rd parameter is HashSet<ZoneSystem.SectorIndex> in 1.0.20, not HashSet<int> - made from the real parameter type
    private static readonly Type VisitedType = FindObjects != null && FindObjects.GetParameters().Length == 3
        ? FindObjects.GetParameters()[2].ParameterType
        : null;

    private static bool _broken;

    private static readonly List<ZoneObjects> Cached = new();
    private static readonly Dictionary<Vector2s, ZoneObjects> CachePool = new();
    private static readonly HashSet<ZDO> CacheSeen = new();
    private static readonly HashSet<ZDOID> LastClaimed = new(), NowClaimed = new();
    private static readonly object[] FindArgs = new object[3];
    private static readonly object[] PokeArgs = new object[1];
    private static object _visited;

    private static readonly MethodInfo VisitedClear =
        VisitedType != null ? VisitedType.GetMethod("Clear", Type.EmptyTypes) : null;

    private static readonly Stopwatch Clock = new();
    private static double _tickMs, _tickMaxMs, _createMs, _createMaxMs;
    private static int _tickCount, _createCount, _scanned, _claimedTotal, _reclaimed;
    private static float _nextStats, _nextZoneLog;
    private static double _fullMs, _fullMaxMs;
    private static int _fullCount;
    private static long _findTicks, _claimTicks;
    private static readonly StringBuilder _zoneLines = new();

    private static int _scanIndex;
    private static bool _scanDone;
    private static ZDOMan _scanFor;
    private static float _next;
    private static int _lastKept = -1;

    /// Sectors kept now (for the in-game check and the log).
    internal static int KeptCount => Kept.Count;

    /// One FindObjects call with reused arguments and a cleared (so equal to fresh) visited set. Expected outcome: `into` holds the zone's ZDOs.
    private static void Find(ZDOMan zm, Vector2s zone, List<ZDO> into)
    {
        if (_visited == null)
        {
            _visited = NewVisited();
        }

        if (VisitedClear != null)
        {
            VisitedClear.Invoke(_visited, null);
        }
        else
        {
            _visited = NewVisited();
        }

        FindArgs[0] = zone;
        FindArgs[1] = into;
        FindArgs[2] = _visited;
        FindObjects.Invoke(zm, FindArgs);
    }

    /// A fresh "visited sectors" set of the type the game's FindObjects wants. Expected outcome: the set, or null when it cannot be made.
    internal static object NewVisited()
    {
        return VisitedType != null ? Activator.CreateInstance(VisitedType) : null;
    }

    /// In-game test: the reflected methods exist with the parameters this class passes, and one FindObjects call on the player's zone works.
    /// Expected outcome: "PASS: ..." / "FAIL: ...".
    internal static string SelfCheck()
    {
        if (Poke == null || FindObjects == null || VisitedType == null)
        {
            return "FAIL: PokeLocalZone " + (Poke != null) + ", FindObjects " + (FindObjects != null) +
                   ", visited type " + (VisitedType != null ? VisitedType.Name : "none");
        }

        Player p = Player.m_localPlayer;
        if (p == null || ZDOMan.instance == null)
        {
            return "SKIP: no player / ZDOMan (methods found, visited " + VisitedType + ")";
        }

        try
        {
            var list = new List<ZDO>();
            FindObjects.Invoke(ZDOMan.instance, new[] { ZoneSystem.GetZone(p.transform.position), list, NewVisited() });
            return "PASS: FindObjects on the player's zone -> " + list.Count + " object(s), visited set " +
                   VisitedType.Name + ", poke " + Poke.GetParameters()[0].ParameterType.Name;
        }
        catch (Exception e)
        {
            return "FAIL: FindObjects threw " + (e.InnerException ?? e).GetType().Name + ": " +
                   (e.InnerException ?? e).Message;
        }
    }

    /// Plugin.Update. Expected outcome: how many zones are kept alive this second (-1 = nothing done this frame).
    internal static int Tick()
    {
        if (_broken || Time.time < _next)
        {
            return -1;
        }

        try
        {
            return TickInner();
        }
        catch (Exception e)
        {
            Break("Tick", e);
            return -1;
        }
    }

    // TEST-037: an exception here ran every frame and stopped the mod's later ticks - now it switches the zones off once, with one error line.
    private static void Break(string where, Exception e)
    {
        _broken = true;
        Kept.Clear();
        KeptSet.Clear();
        Exception inner = e.InnerException ?? e;
        ModLog.Error("TrollZones: " + where + " failed - kept zones switched OFF for this session: " +
                     inner.GetType().Name + ": " + inner.Message);
    }

    private static int TickInner()
    {
        if (Time.time < _next)
        {
            return -1;
        }

        _next = Time.time + TickSeconds;
        Clock.Reset();
        Clock.Start(); // D-141: the whole tick is measured, the clean-up included
        Kept.Clear();
        KeptSet.Clear();
        Cached.Clear();
        Centres.Clear();
        Player p = Player.m_localPlayer;
        ZDOMan zm = ZDOMan.instance;
        ZoneSystem zs = ZoneSystem.instance;
        if (p == null || zm == null || zs == null || ZNetScene.instance == null || ZNet.instance == null ||
            !ModSettings.On || ModSettings.TrollZoneRadius.Value <= 0f || !TrollTaming.Wears(p))
        {
            if (CachePool.Count > 0)
            {
                CachePool.Clear();
            }

            return Log(0);
        }

        Track(p, zm);
        long me = p.GetPlayerID();
        Vector3 refPos = ZNet.instance != null ? ZNet.instance.GetReferencePosition() : p.transform.position;
        float r = ModSettings.TrollZoneRadius.Value;
        var gone = new List<ZDOID>();
        foreach (ZDOID id in Trolls)
        {
            ZDO z = zm.GetZDO(id);
            if (z == null || z.GetFloat(ZDOVars.s_health, 1f) <= 0f || z.GetLong(TrollTaming.OwnerKey) != me)
            {
                gone.Add(id);
                continue;
            }

            Vector3 pos = z.GetPosition();
            if (ZNetScene.InActiveArea(ZoneSystem.GetZonePos(ZoneSystem.GetZone(pos)), refPos))
            {
                continue; // the game runs it anyway
            }

            Centres.Add(pos);
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                Vector2s zone = ZoneSystem.GetZone(pos + new Vector3(dx * ZoneSize, 0f, dz * ZoneSize));
                Vector3 c = ZoneSystem.GetZonePos(zone);
                float ex = Mathf.Max(Mathf.Abs(pos.x - c.x) - ZoneSize / 2f, 0f),
                    ez = Mathf.Max(Mathf.Abs(pos.z - c.z) - ZoneSize / 2f, 0f);
                if (ex * ex + ez * ez > r * r || KeptSet.Contains(zone) || ZNetScene.InActiveArea(c, refPos))
                {
                    continue;
                }

                KeptSet.Add(zone);
                Kept.Add(zone);
            }
        }

        foreach (ZDOID id in gone)
        {
            Trolls.Remove(id);
        }

        // D-141 (review): the pool kept an entry (and its ZDO lists) for every zone a troll ever visited and the tick walked them all; only the kept zones stay
        if (CachePool.Count > Kept.Count)
        {
            Stale.Clear();
            foreach (Vector2s key in CachePool.Keys)
            {
                if (!KeptSet.Contains(key))
                {
                    Stale.Add(key);
                }
            }

            foreach (Vector2s key in Stale)
            {
                CachePool.Remove(key);
            }
        }

        long session = ZDOMan.GetSessionID();
        int claimed = 0, scanned = 0, reclaimed = 0;
        bool debug = ModLog.DebugOn;
        if (debug)
        {
            NowClaimed.Clear();
        }

        CacheSeen.Clear();
        SimulationDistance sim = ZNet.instance.GetSyncedSimulationDistance();
        Vector2s centre = ZoneSystem.GetZone(refPos);
        bool claimNear = ModSettings.ZoneClaimNearBand.Value;
        float claimReach = r + ClaimMargin, claimReach2 = claimReach * claimReach;
        bool zoneLog = debug && Time.time >= _nextZoneLog;
        if (zoneLog)
        {
            _nextZoneLog = Time.time + 5f;
            _zoneLines.Length = 0;
        }

        long t0;
        foreach (Vector2s zone in Kept)
        {
            string band = !debug && claimNear ? null :
                Within(centre, zone, sim.NearSimulationDistance, sim.IsClassic, false) ? "near" :
                Within(centre, zone, sim.TotalSimulationDistance, sim.IsClassic, true) ? "far" : "beyond";
            int zoneUnowned = 0, zoneClaimed = 0;
            if (Poke != null)
            {
                PokeArgs[0] = zone;
                Poke.Invoke(zs, PokeArgs);
            }

            Temp.Clear();
            if (FindObjects == null)
            {
                continue;
            }

            t0 = Stopwatch.GetTimestamp();
            Find(zm, zone, Temp);
            _findTicks += Stopwatch.GetTimestamp() - t0;
            scanned += Temp.Count;
            ZoneObjects zo;
            if (!CachePool.TryGetValue(zone, out zo))
            {
                zo = new ZoneObjects { Zone = zone };
                CachePool[zone] = zo;
            }

            zo.All.Clear();
            zo.NotDistant.Clear();
            bool mayClaim = claimNear || band != "near";
            t0 = Stopwatch.GetTimestamp();
            foreach (ZDO z in Temp)
            {
                if (z == null)
                {
                    continue;
                }

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
                        z.SetOwner(session);
                        claimed++;
                        zoneClaimed++;
                        if (ownTroll && !mayClaim && debug)
                        {
                            ModLog.Verbose("TrollZones: own troll " + z.m_uid +
                                           " claimed in a zone of the game's near band (the game had released it outside the active area)");
                        }

                        if (debug)
                        {
                            if (LastClaimed.Contains(z.m_uid))
                            {
                                reclaimed++;
                            }

                            NowClaimed.Add(z.m_uid);
                        }
                    }
                }

                if (!CacheSeen.Add(z))
                {
                    continue; // portal objects can come once per sector call
                }

                zo.All.Add(z);
                if (!z.Distant)
                {
                    zo.NotDistant.Add(z);
                }
            }

            _claimTicks += Stopwatch.GetTimestamp() - t0;
            if (zoneLog)
            {
                _zoneLines.Append("\n    zone ").Append(zone.x).Append('/').Append(zone.y).Append(' ').Append(band)
                    .Append(": ").Append(Temp.Count).Append(" object(s), ").Append(zoneUnowned)
                    .Append(" unowned persistent, ").Append(zoneClaimed).Append(" claimed");
            }

            Cached.Add(zo);
        }

        if (debug)
        {
            LastClaimed.Clear();
            foreach (ZDOID id in NowClaimed)
            {
                LastClaimed.Add(id);
            }
        }

        _scanned += scanned;
        _claimedTotal += claimed;
        _reclaimed += reclaimed;
        Clock.Stop();
        double ms = Clock.Elapsed.TotalMilliseconds;
        _tickMs += ms;
        _tickCount++;
        if (ms > _tickMaxMs)
        {
            _tickMaxMs = ms;
        }

        if (zoneLog)
        {
            ModLog.Verbose("TrollZones: per kept zone (band = the game's simulation band; near " +
                           sim.NearSimulationDistance + ", total " + sim.TotalSimulationDistance +
                           " zones; claim in the near band " + (claimNear ? "ON" : "OFF (test)") + ")" + _zoneLines);
        }

        if (claimed > 0 && debug)
        {
            ModLog.Verbose("TrollZones: " + claimed + " unowned object(s) claimed in " + Kept.Count +
                           " kept zone(s), " + reclaimed +
                           " of them were already claimed by this client in the pass before (the game gave them back)");
        }

        Stats();
        return Log(Kept.Count);
    }
}
