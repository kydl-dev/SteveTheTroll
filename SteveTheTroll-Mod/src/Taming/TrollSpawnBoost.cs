using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-042: while a slipper wearer whose own tamed-troll limit is not met (no own tamed troll within OwnTrollRange) is near a spawn zone, its
    /// Troll spawn entries run SpawnBoostFactor x as often (interval / factor, chance x factor, TamingRules) and the trolls spawned that way get the
    /// game's "Log" item set (the troll's random sets: 2x Unarmed, 1x Log). Runs where the game spawns: the owner of the zone's SpawnSystem.
    /// The SpawnData values are changed only for the duration of SpawnSystem.UpdateSpawnList and put back in a finalizer.
    /// </summary>
    internal static class TrollSpawnBoost
    {
        const string LogSet = "Log";
        const float PendingSeconds = 10f, PendingMatchMetres = 3f;
        static readonly List<KeyValuePair<SpawnSystem.SpawnData, Vector2>> Changed = new List<KeyValuePair<SpawnSystem.SpawnData, Vector2>>();
        static readonly List<KeyValuePair<Vector3, float>> Pending = new List<KeyValuePair<Vector3, float>>();
        static bool _active;
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
                if (!ModSettings.On || eventSpawners || spawners == null || ModSettings.SpawnBoostFactor.Value <= 1f) return;
                bool hasTroll = false;
                foreach (var s in spawners) if (s != null && s.m_enabled && s.m_prefab != null && s.m_prefab.name == TrollTaming.TrollPrefab) { hasTroll = true; break; }
                if (!hasTroll) return;
                var wearer = BoostingWearer(__instance.transform.position);
                if (wearer == null) return;
                float f = ModSettings.SpawnBoostFactor.Value;
                foreach (var s in spawners)
                {
                    if (s == null || !s.m_enabled || s.m_prefab == null || s.m_prefab.name != TrollTaming.TrollPrefab) continue;
                    Changed.Add(new KeyValuePair<SpawnSystem.SpawnData, Vector2>(s, new Vector2(s.m_spawnInterval, s.m_spawnChance)));
                    s.m_spawnInterval = TamingRules.BoostedInterval(s.m_spawnInterval, f);
                    s.m_spawnChance = TamingRules.BoostedChance(s.m_spawnChance, f);
                }
                _active = Changed.Count > 0;
                if (Time.time >= _nextLog)
                {
                    _nextLog = Time.time + 30f;
                    ModLog.Verbose("TrollSpawnBoost: zone at " + __instance.transform.position.ToString("F0") + ", wearer " + wearer.GetPlayerName() + " without a tamed troll near: "
                        + Changed.Count + " troll entr(y/ies) x" + f + " (first: interval " + Changed[0].Value.x + " -> " + Changed[0].Key.m_spawnInterval + " s, chance "
                        + Changed[0].Value.y + " -> " + Changed[0].Key.m_spawnChance + " %)");
                }
            }

            static void Finalizer()
            {
                foreach (var c in Changed) { c.Key.m_spawnInterval = c.Value.x; c.Key.m_spawnChance = c.Value.y; }
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
