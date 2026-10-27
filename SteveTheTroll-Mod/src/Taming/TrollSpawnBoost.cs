using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-042: while a slipper wearer whose own tamed-troll limit is not met (no own tamed troll within OwnTrollRange) is near a spawn zone, its
    /// Troll spawn entries try every SpawnBoostSeconds with 100 % chance, day and night (D-059, TamingRules) and the trolls spawned that way get the
    /// game's "Log" item set (the troll's random sets: 2x Unarmed, 1x Log). Runs where the game spawns: the owner of the zone's SpawnSystem.
    /// The SpawnData values are changed only for the duration of SpawnSystem.UpdateSpawnList and put back in a finalizer.
    /// </summary>
    internal static class TrollSpawnBoost
    {
        const string LogSet = "Log";
        const float PendingSeconds = 10f, PendingMatchMetres = 3f;
        static readonly List<Saved> Changed = new List<Saved>();
        static readonly List<KeyValuePair<Vector3, float>> Pending = new List<KeyValuePair<Vector3, float>>();
        static bool _active, _loggedEntry;

        // The game's values of one boosted entry, put back in the finalizer.
        sealed class Saved
        {
            internal SpawnSystem.SpawnData Data;
            internal float Interval, Chance;
            internal bool Day, Night;
        }
        static float _nextLog;

        /// A wearer within WearerRange of the zone whose own troll limit is not met. Expected outcome: that wearer, or null.
        static Player BoostingWearer(Vector3 zone)
        {
            foreach (var p in Player.GetAllPlayers())
            {
                if (p == null || p.IsDead() || !TrollTaming.Wears(p)) continue;
                if (Vector3.Distance(p.transform.position, zone) > ModSettings.SpawnBoostWearerRange.Value) continue;
                float own;
                TrollTaming.OwnTroll(p, out own);
                if (own >= 0f && own <= ModSettings.OwnTrollRange.Value) continue;
                return p;
            }
            return null;
        }

        [HarmonyPatch(typeof(SpawnSystem), "UpdateSpawnList")]
        static class SpawnList
        {
            static void Prefix(SpawnSystem __instance, List<SpawnSystem.SpawnData> spawners, bool eventSpawners)
            {
                Changed.Clear();
                _active = false;
                if (!ModSettings.On || eventSpawners || spawners == null || ModSettings.SpawnBoostSeconds.Value <= 0f) return;
                bool hasTroll = false;
                foreach (var s in spawners) if (s != null && s.m_enabled && s.m_prefab != null && s.m_prefab.name == TrollTaming.TrollPrefab) { hasTroll = true; break; }
                if (!hasTroll) return;
                var wearer = BoostingWearer(__instance.transform.position);
                if (wearer == null) return;
                float f = ModSettings.SpawnBoostSeconds.Value;
                foreach (var s in spawners)
                {
                    if (s == null || !s.m_enabled || s.m_prefab == null || s.m_prefab.name != TrollTaming.TrollPrefab) continue;
                    if (!_loggedEntry)
                    {
                        _loggedEntry = true;
                        ModLog.Info("TrollSpawnBoost: the game's troll entry: interval " + s.m_spawnInterval + " s, chance " + s.m_spawnChance + " %, max spawned " + s.m_maxSpawned
                            + ", day " + s.m_spawnAtDay + " / night " + s.m_spawnAtNight + ", global key '" + s.m_requiredGlobalKey + "', environments " + (s.m_requiredEnvironments != null ? s.m_requiredEnvironments.Count : 0)
                            + ", spawn distance " + s.m_spawnDistance + " m, radius " + s.m_spawnRadiusMin + " .. " + s.m_spawnRadiusMax + " m, in forest " + s.m_inForest + " / outside " + s.m_outsideForest);
                    }
                    Changed.Add(new Saved { Data = s, Interval = s.m_spawnInterval, Chance = s.m_spawnChance, Day = s.m_spawnAtDay, Night = s.m_spawnAtNight });
                    s.m_spawnInterval = TamingRules.BoostedInterval(s.m_spawnInterval, f);
                    s.m_spawnChance = TamingRules.BoostedChance(s.m_spawnChance, f);
                    s.m_spawnAtDay = true; s.m_spawnAtNight = true;
                }
                _active = Changed.Count > 0;
                if (Time.time >= _nextLog)
                {
                    _nextLog = Time.time + 30f;
                    ModLog.Verbose("TrollSpawnBoost: zone at " + __instance.transform.position.ToString("F0") + ", wearer " + wearer.GetPlayerName() + " without a tamed troll near: "
                        + Changed.Count + " troll entr(y/ies) boosted (first: interval " + Changed[0].Interval + " -> " + Changed[0].Data.m_spawnInterval + " s, chance "
                        + Changed[0].Chance + " -> " + Changed[0].Data.m_spawnChance + " %, day and night)");
                }
            }

            static void Finalizer()
            {
                foreach (var c in Changed) { c.Data.m_spawnInterval = c.Interval; c.Data.m_spawnChance = c.Chance; c.Data.m_spawnAtDay = c.Day; c.Data.m_spawnAtNight = c.Night; }
                Changed.Clear();
                _active = false;
            }
        }

        // A boosted troll spawn: remember where, so Humanoid.GiveDefaultItems of that new troll picks the Log set.
        [HarmonyPatch(typeof(SpawnSystem), "Spawn")]
        static class SpawnOne
        {
            static void Prefix(SpawnSystem.SpawnData critter, Vector3 spawnPoint)
            {
                if (!_active || critter == null || critter.m_prefab == null || critter.m_prefab.name != TrollTaming.TrollPrefab) return;
                Pending.Add(new KeyValuePair<Vector3, float>(spawnPoint, Time.time));
                ModLog.Info("TrollSpawnBoost: boosted troll spawn at " + spawnPoint.ToString("F0") + " (gets a log)");
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GiveDefaultItems))]
        static class LogForBoosted
        {
            static void Prefix(Humanoid __instance)
            {
                if (Pending.Count == 0 || !TrollTaming.IsTroll(__instance)) return;
                Pending.RemoveAll(p => Time.time - p.Value > PendingSeconds);
                int hit = Pending.FindIndex(p => Vector3.Distance(p.Key, __instance.transform.position) <= PendingMatchMetres);
                if (hit < 0) return;
                Pending.RemoveAt(hit);
                var sets = __instance.m_randomSets;
                var logs = sets != null ? System.Array.FindAll(sets, s => s != null && s.m_name == LogSet) : new Humanoid.ItemSet[0];
                if (logs.Length == 0) { ModLog.Warn("TrollSpawnBoost: " + __instance.name + " has no '" + LogSet + "' item set (" + (sets != null ? sets.Length : 0) + " set(s)) - spawns as usual"); return; }
                __instance.m_randomSets = logs;     // this instance only (the prefab keeps its array)
                ModLog.Info("TrollSpawnBoost: " + __instance.name + " at " + __instance.transform.position.ToString("F0") + " gets the " + LogSet + " set (" + logs.Length + " of " + sets.Length + " sets)");
            }
        }
    }
}
