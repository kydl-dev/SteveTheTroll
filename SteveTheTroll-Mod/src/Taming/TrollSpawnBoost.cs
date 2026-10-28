using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-042 / D-084: within SpawnBedRadius (500 m) of the local slipper wearer's bed (cap 2 with the slippers and 0-1 tamed trolls, 6 with the set
    /// and up to 5; TamingRules.SpawnCap), its
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
            internal int Max;
        }
        static float _nextLog;

        /// D-084 (user: 500 m around the bed - 2 with the slippers and 0 or 1 tamed troll, 6 with the whole set and up to 5 tamed trolls,
        /// outside that the game's own rates): the local wearer when this zone is within SpawnBedRadius of its bed, with the cap for the zone.
        /// Expected outcome: that wearer (cap 2 / 6), or null (cap 0 = vanilla). Only the local player's bed is known here (multiplayer: each
        /// player's own client boosts the zones it runs).
        static Player BoostingWearer(Vector3 zone, out int cap, out string why)
        {
            cap = 0; why = "";
            var p = Player.m_localPlayer;
            if (p == null || p.IsDead() || !TrollTaming.Wears(p)) { why = "no local slipper wearer"; return null; }
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (profile == null || !profile.HaveCustomSpawnPoint()) { why = "no bed"; return null; }
            var bed = profile.GetCustomSpawnPoint();
            var flat = zone - bed; flat.y = 0f;
            bool set = TrollTaming.TrollLimit(p) > 1;
            int tamed = TrollRegistry.LivingCount();
            cap = TamingRules.SpawnCap(true, set, tamed, flat.magnitude, ModSettings.SpawnBedRadius.Value);
            why = "bed " + flat.magnitude.ToString("0") + " m (radius " + ModSettings.SpawnBedRadius.Value + "), set " + set + ", tamed " + tamed;
            return cap > 0 ? p : null;
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
                int cap; string why;
                var wearer = BoostingWearer(__instance.transform.position, out cap, out why);
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
                    Changed.Add(new Saved { Data = s, Interval = s.m_spawnInterval, Chance = s.m_spawnChance, Day = s.m_spawnAtDay, Night = s.m_spawnAtNight, Max = s.m_maxSpawned });
                    // D-077 (TEST-032: no troll spawned with the set): the game counts the wearer's tamed trolls nearby against max spawned (1)
                    s.m_maxSpawned = cap;                                                     // D-084: 2 / 6 around the bed
                    s.m_spawnInterval = TamingRules.BoostedInterval(s.m_spawnInterval, f);
                    s.m_spawnChance = TamingRules.BoostedChance(s.m_spawnChance, f);
                    s.m_spawnAtDay = true; s.m_spawnAtNight = true;
                }
                _active = Changed.Count > 0;
                if (Time.time >= _nextLog)
                {
                    _nextLog = Time.time + 30f;
                    ModLog.Verbose("TrollSpawnBoost: zone at " + __instance.transform.position.ToString("F0") + ", wearer " + wearer.GetPlayerName() + " (" + why + ", cap " + cap + "): "
                        + Changed.Count + " troll entr(y/ies) boosted (first: interval " + Changed[0].Interval + " -> " + Changed[0].Data.m_spawnInterval + " s, chance "
                        + Changed[0].Chance + " -> " + Changed[0].Data.m_spawnChance + " %, max spawned " + Changed[0].Max + " -> " + Changed[0].Data.m_maxSpawned + ", day and night)");
                }
            }

            static void Finalizer()
            {
                foreach (var c in Changed) { c.Data.m_spawnInterval = c.Interval; c.Data.m_spawnChance = c.Chance; c.Data.m_spawnAtDay = c.Day; c.Data.m_spawnAtNight = c.Night; c.Data.m_maxSpawned = c.Max; }
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

        /// D-063: the item set a troll must keep, saved in its ZDO ("Log" = Steve's club). The game picks the set again on every load from
        /// the troll's seed; a set we forced (boosted spawn) or a tamed Steve lost its log after leaving the loaded area (TEST-027).
        internal const string SetKey = "stt_set";

        /// At taming: remember the troll's set (a troll_log item -> "Log"). Expected outcome: the stored set name ("" = nothing stored).
        internal static string Remember(Character troll)
        {
            var h = troll as Humanoid;
            var zdo = TrollTaming.Zdo(troll);
            if (h == null || zdo == null) return "";
            bool log = false;
            foreach (var it in h.GetInventory().GetAllItems()) if (it.m_dropPrefab != null && it.m_dropPrefab.name.StartsWith("troll_log")) log = true;
            if (log) zdo.Set(SetKey, LogSet);
            return log ? LogSet : "";
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GiveDefaultItems))]
        static class LogForBoosted
        {
            static void Prefix(Humanoid __instance)
            {
                if (!TrollTaming.IsTroll(__instance)) return;
                var zdo = TrollTaming.Zdo(__instance);
                string want = zdo != null ? zdo.GetString(SetKey, "") : "";
                // a Steve tamed before D-063 has no stored set: his name says he had the log
                if (want.Length == 0 && zdo != null && zdo.GetString(ZDOVars.s_tamedName, "").StartsWith("Steve")) want = LogSet;
                bool boosted = false;
                if (want.Length == 0 && Pending.Count > 0)
                {
                    Pending.RemoveAll(p => Time.time - p.Value > PendingSeconds);
                    int hit = Pending.FindIndex(p => Vector3.Distance(p.Key, __instance.transform.position) <= PendingMatchMetres);
                    if (hit >= 0) { Pending.RemoveAt(hit); want = LogSet; boosted = true; }
                }
                if (want.Length == 0) return;
                var sets = __instance.m_randomSets;
                var keep = sets != null ? System.Array.FindAll(sets, s => s != null && s.m_name == want) : new Humanoid.ItemSet[0];
                if (keep.Length == 0) { ModLog.Warn("TrollSpawnBoost: " + __instance.name + " has no '" + want + "' item set (" + (sets != null ? sets.Length : 0) + " set(s)) - spawns as usual"); return; }
                __instance.m_randomSets = keep;     // this instance only (the prefab keeps its array)
                if (zdo != null && zdo.GetString(SetKey, "") != want && (boosted || __instance.GetComponent<ZNetView>().IsOwner())) zdo.Set(SetKey, want);
                if (boosted)
                {
                    int stars = TamingRules.BoostedStars(Random.value);     // D-068: 50 % none, 25 % one, 25 % two stars
                    int before = __instance.GetLevel();
                    __instance.SetLevel(stars + 1);
                    ModLog.Info("TrollSpawnBoost: " + __instance.name + " boosted spawn level " + before + " -> " + (stars + 1) + " (" + stars + " star(s))");
                }
                ModLog.Info("TrollSpawnBoost: " + __instance.name + " at " + __instance.transform.position.ToString("F0") + " gets the " + want + " set (" + (boosted ? "boosted spawn" : "kept from its ZDO") + ")");
            }
        }
    }
}
