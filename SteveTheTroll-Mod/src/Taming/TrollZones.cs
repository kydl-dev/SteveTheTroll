using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
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
        static readonly List<ZDO> Scan = new List<ZDO>();
        static readonly System.Reflection.MethodInfo Poke = AccessTools.Method(typeof(ZoneSystem), "PokeLocalZone");
        static readonly System.Reflection.MethodInfo FindObjects = AccessTools.Method(typeof(ZDOMan), "FindObjects");
        // TEST-037: the 3rd parameter is HashSet<ZoneSystem.SectorIndex> in 1.0.20, not HashSet<int> - made from the real parameter type
        static readonly System.Type VisitedType = FindObjects != null && FindObjects.GetParameters().Length == 3 ? FindObjects.GetParameters()[2].ParameterType : null;
        static bool _broken;

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
            Kept.Clear(); KeptSet.Clear();
            var p = Player.m_localPlayer;
            var zm = ZDOMan.instance;
            var zs = ZoneSystem.instance;
            if (p == null || zm == null || zs == null || ZNetScene.instance == null || !ModSettings.On || ModSettings.TrollZoneRadius.Value <= 0f || !TrollTaming.Wears(p)) return Log(0);
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
            long session = ZDOMan.GetSessionID();
            int claimed = 0;
            foreach (var zone in Kept)
            {
                if (Poke != null) Poke.Invoke(zs, new object[] { zone });
                Temp.Clear();
                if (FindObjects != null) FindObjects.Invoke(zm, new object[] { zone, Temp, NewVisited() });
                foreach (var z in Temp) if (z != null && z.Persistent && !z.HasOwner()) { z.SetOwner(session); claimed++; }
            }
            if (claimed > 0 && ModLog.DebugOn) ModLog.Verbose("TrollZones: " + claimed + " unowned object(s) claimed in " + Kept.Count + " kept zone(s)");
            return Log(Kept.Count);
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
        [HarmonyPatch(typeof(ZNetScene), "CreateObjects")]
        static class Create
        {
            static void Prefix(List<ZDO> currentNearObjects)
            {
                if (_broken || Kept.Count == 0 || currentNearObjects == null || FindObjects == null || ZDOMan.instance == null) return;
                try
                {
                    var visited = NewVisited();
                    foreach (var zone in Kept) FindObjects.Invoke(ZDOMan.instance, new object[] { zone, currentNearObjects, visited });
                }
                catch (System.Exception e) { Break("CreateObjects", e); }
            }
        }
    }
}
