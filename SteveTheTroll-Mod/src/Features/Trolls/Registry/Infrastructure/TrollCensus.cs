using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Registry.Infrastructure
{
    /// <summary>
    /// D-085 (TEST-037: 11 trolls spawned around the bed while the cap was 6 - the game's max spawned counts only the trolls near ONE spawn
    /// zone, and each zone around the bed could fill up to 6): how many trolls (wild + tamed, loaded or not) stand within SpawnBedRadius of
    /// the local player's bed. Counted from the world's troll ZDOs in slices (GetAllZDOsWithPrefabIterative, a little each frame), a fresh
    /// count every RecountSeconds; a troll spawned in between is added at once (Spawned), so a burst cannot pass the cap.
    /// </summary>
    internal static class TrollCensus
    {
        const float RecountSeconds = 5f;
        static readonly List<ZDO> Scan = new List<ZDO>();
        static int _index, _count = -1, _extra;
        static float _nextStart;
        static bool _scanning;
        static ZDOMan _for;

        /// Trolls within the bed radius at the last full count + those spawned since (-1 = not counted yet / no bed).
        internal static int Count { get { return _count < 0 ? -1 : _count + _extra; } }

        /// A troll was just spawned near the bed (TrollSpawnBoost.SpawnOne). Expected outcome: the new count.
        internal static int Spawned() { if (_count >= 0) _extra++; return Count; }

        /// Plugin.Update: one slice of the world scan. Expected outcome: whether a count finished this frame.
        internal static bool Tick()
        {
            var zm = ZDOMan.instance;
            var p = Player.m_localPlayer;
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (zm == null || p == null || profile == null || !profile.HaveCustomSpawnPoint()) { _count = -1; _scanning = false; return false; }
            if (zm != _for) { _for = zm; _scanning = false; _count = -1; _nextStart = 0f; }
            if (!_scanning)
            {
                if (Time.time < _nextStart) return false;
                _scanning = true; _index = 0; Scan.Clear();
            }
            if (!zm.GetAllZDOsWithPrefabIterative(TrollTaming.TrollPrefab, Scan, ref _index)) return false;
            var bed = profile.GetCustomSpawnPoint();
            float r2 = ModSettings.SpawnBedRadius.Value * ModSettings.SpawnBedRadius.Value;
            int n = 0;
            foreach (var z in Scan)
            {
                if (z == null) continue;
                var d = z.GetPosition() - bed; d.y = 0f;
                if (d.sqrMagnitude <= r2) n++;
            }
            if (n != _count + _extra) ModLog.Verbose("TrollCensus: " + n + " troll(s) within " + ModSettings.SpawnBedRadius.Value + " m of the bed (" + Scan.Count + " in the world)");
            _count = n; _extra = 0;
            _scanning = false; _nextStart = Time.time + RecountSeconds;
            Scan.Clear();
            return true;
        }
    }
}
