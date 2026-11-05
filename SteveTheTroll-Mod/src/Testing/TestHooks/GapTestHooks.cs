using System;
using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Costume.Infrastructure;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Features.Trolls.Water.Infrastructure;
using SteveTheTroll.Features.Trolls.World.Infrastructure;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
///     D-177 (fix10 review, user: "add tests first"): hooks for the features that had no in-game test - a newly tamed troll walks to its tamer (D-043), the spawn boost
///     cap around the bed (D-042 / D-085), half / 1.5x speed by hub distance (D-135), one troll per tree and the friend detour (D-151), the 15 m ring in water and no
///     ship damage (D-131), the Troll Club breaking special trees (D-120). Every hook calls the mod's real code on real game objects; none decides an outcome itself.
/// </summary>
internal static class GapTestHooks
{
    // ---- taming / arrival (D-043) ---------------------------------------------------------------------------------------------------------------------

    /// A WILD troll `dx`,`dz` m from the player (in front), remembered as test-made so stt.clearTrolls / stt.clearTrollZdos remove it. Expected outcome: the troll.
    [TestHook("stt.spawnWildTroll")]
    private static GameObject SpawnWildTroll(float dx, float dz)
    {
        Player p = Player.m_localPlayer;
        Vector3 at = p.transform.position + new Vector3(dx, 0f, dz);
        at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.3f;
        GameObject go = Object.Instantiate(ZNetScene.instance.GetPrefab(TrollTaming.TrollPrefab), at, Quaternion.identity);
        ZNetView v = go.GetComponent<ZNetView>();
        if (v != null && v.IsValid())
        {
            TestHooks.MarkMade(v.GetZDO().m_uid);
        }

        ModLog.Info("GapTestHooks: wild troll spawned at " + at.ToString("F0") + " (" + new Vector2(dx, dz).magnitude.ToString("F1") + " m from the player)");
        return go;
    }

    /// The game's taming countdown of a wild troll is set to its last `seconds` - the mod's own TrollTamingPatch finishes the taming (owner, name, arrival). Expected outcome: the time left.
    [TestHook("stt.tameSoon")]
    private static float TameSoon(GameObject troll, float seconds = 0.5f)
    {
        ZNetView v = troll.GetComponent<ZNetView>();
        v.ClaimOwnership();
        v.GetZDO().Set(ZDOVars.s_tameTimeLeft, seconds);
        return v.GetZDO().GetFloat(ZDOVars.s_tameTimeLeft);
    }

    [TestHook("stt.isTamed")]
    private static bool IsTamed(GameObject troll)
    {
        Character c = troll != null ? troll.GetComponent<Character>() : null;
        return c != null && c.IsTamed();
    }

    /// D-043: "walking" while TrollArrival runs, "arrived" once it is gone with the troll within 6 m of the player and no follow order, else what is wrong.
    [TestHook("stt.arrival")]
    private static string Arrival(GameObject troll)
    {
        if (troll.GetComponent<TrollArrival>() != null)
        {
            return "walking";
        }

        MonsterAI ai = troll.GetComponent<MonsterAI>();
        float d = Vector3.Distance(troll.transform.position, Player.m_localPlayer.transform.position);
        bool follows = ai != null && ai.GetFollowTarget() != null;
        return d <= 6f && !follows ? "arrived" : "not arrived (" + d.ToString("F1") + " m, follows " + follows + ")";
    }

    // ---- spawn boost cap (D-042 / D-085) ----------------------------------------------------------------------------------------------------------------

    /// Removes every troll ZDO within `radius` of the bed (custom spawn point) that is wild or test-made - loaded or not (the census counts unloaded ones too).
    /// Tamed trolls a test did not make are kept. Expected outcome: how many were removed.
    [TestHook("stt.clearTrollZdos")]
    private static int ClearTrollZdos(float radius)
    {
        Vector3 bed = Game.instance.GetPlayerProfile().GetCustomSpawnPoint();
        var all = new List<ZDO>();
        int index = 0;
        while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(TrollTaming.TrollPrefab, all, ref index))
        {
        }

        int n = 0, kept = 0;
        foreach (ZDO z in all)
        {
            Vector3 d = z.GetPosition() - bed;
            d.y = 0f;
            if (d.magnitude > radius)
            {
                continue;
            }

            if (z.GetBool(ZDOVars.s_tamed) && !TestHooks.MadeByTest(z.m_uid))
            {
                kept++;
                continue;
            }

            ZNetView v = ZNetScene.instance.FindInstance(z);
            if (v != null)
            {
                v.ClaimOwnership();
                ZNetScene.instance.Destroy(v.gameObject);
            }
            else
            {
                z.SetOwner(ZDOMan.GetSessionID());
                ZDOMan.instance.DestroyZDO(z);
            }

            n++;
        }

        ModLog.Info("GapTestHooks: " + n + " troll ZDO(s) within " + radius.ToString("F0") + " m of the bed removed, " + kept + " tamed troll(s) of the player kept");
        return n;
    }

    /// TrollCensus: trolls within the bed radius at the last count + spawned since (-1 = not counted yet).
    [TestHook("stt.censusCount")]
    private static int CensusCount()
    {
        return TrollCensus.Count;
    }

    /// Runs the mod's real spawn decision (TrollSpawnBoost.SpawnList prefix) on the nearest loaded SpawnSystem's lists, reads what it did to the game's Troll entry
    /// and puts the entry back (the finalizer). Expected outcome: "vanilla" (no boost), "boosted max N" or "full (census N)".
    [TestHook("stt.spawnBoostDecision")]
    private static string SpawnBoostDecision()
    {
        Player p = Player.m_localPlayer;
        SpawnSystem best = null;
        float bestD = float.MaxValue;
        foreach (SpawnSystem s in Object.FindObjectsByType<SpawnSystem>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(s.transform.position, p.transform.position);
            if (d < bestD)
            {
                bestD = d;
                best = s;
            }
        }

        if (best == null)
        {
            return "no spawn system loaded";
        }

        var spawners = new List<SpawnSystem.SpawnData>();
        foreach (SpawnSystemList l in best.m_spawnLists)
        {
            spawners.AddRange(l.m_spawners);
        }

        SpawnSystem.SpawnData troll = spawners.Find(s => s != null && s.m_enabled && s.m_prefab != null && s.m_prefab.name == TrollTaming.TrollPrefab);
        if (troll == null)
        {
            return "no troll entry";
        }

        float chance = troll.m_spawnChance;
        int max = troll.m_maxSpawned;
        string result;
        try
        {
            TrollSpawnBoost.SpawnList.Prefix(best, spawners, false);
            result = troll.m_spawnChance <= 0f && chance > 0f ? "full (census " + TrollCensus.Count + ")" :
                troll.m_maxSpawned != max || troll.m_spawnChance != chance ? "boosted max " + troll.m_maxSpawned + " (census " + TrollCensus.Count + ")" : "vanilla";
        }
        finally
        {
            TrollSpawnBoost.SpawnList.Finalizer();
        }

        bool restored = Mathf.Approximately(troll.m_spawnChance, chance) && troll.m_maxSpawned == max;
        ModLog.Info("GapTestHooks: spawn decision of the zone at " + best.transform.position.ToString("F0") + " - " + result + ", living tamed trolls " + TrollRegistry.LivingCount() +
                    ", game values restored " + restored);
        return restored ? result : result + " - NOT RESTORED";
    }

    // ---- hub speed (D-135) ----------------------------------------------------------------------------------------------------------------------------

    /// The owner's set bonus (SE_STT_SteveTheTroll) is active.
    [TestHook("stt.setActive")]
    private static bool SetActive()
    {
        Player p = Player.m_localPlayer;
        return CostumeItems.SetEffectHash != 0 && p.GetSEMan().HaveStatusEffect(CostumeItems.SetEffectHash);
    }

    /// The speed factor HubSpeed gives this troll now, in per mille (1250 = the usual tamed x1.25). Expected outcome: 625 with no hub within 1250 m, 1500 in a hub circle.
    [TestHook("stt.hubSpeedPermille")]
    private static int HubSpeedPermille(GameObject troll)
    {
        return Mathf.RoundToInt(HubSpeed.Factor(troll.GetComponent<Character>(), ModSettings.TamedSpeedFactor.Value) * 1000f);
    }

    /// The troll's APPLIED run speed against the prefab's, in per mille (what TamedTrollSpeed set on the instance).
    [TestHook("stt.runSpeedPermille")]
    private static int RunSpeedPermille(GameObject troll)
    {
        Character c = troll.GetComponent<Character>();
        Character prefab = ZNetScene.instance.GetPrefab(TrollTaming.TrollPrefab).GetComponent<Character>();
        int v = Mathf.RoundToInt(c.m_runSpeed / prefab.m_runSpeed * 1000f);
        ModLog.Info("GapTestHooks: run speed " + c.m_runSpeed.ToString("F2") + " m/s (prefab " + prefab.m_runSpeed.ToString("F2") + ") = " + v + " per mille");
        return v;
    }

    // ---- one troll per tree, friend detour (D-151) ------------------------------------------------------------------------------------------------------

    private static int _maxOnOne = -1;
    private static float _watchStart = -1f;
    private static string _maxWhere = "";

    /// The most own farmers working on ONE source right now (0 = none farms anything). Logs which source when 2 or more share one.
    [TestHook("stt.farmersOnOneSource")]
    private static int FarmersOnOneSource()
    {
        var on = new Dictionary<GameObject, int>();
        foreach (TrollFarmer f in Object.FindObjectsByType<TrollFarmer>(FindObjectsSortMode.None))
        {
            GameObject g = f != null ? f.Target : null;
            if (g == null)
            {
                continue;
            }

            int n;
            on[g] = on.TryGetValue(g, out n) ? n + 1 : 1;
        }

        int most = 0;
        foreach (KeyValuePair<GameObject, int> kv in on)
        {
            if (kv.Value > most)
            {
                most = kv.Value;
                if (most > 1)
                {
                    _maxWhere = kv.Key.name + " at " + kv.Key.transform.position.ToString("F0");
                }
            }
        }

        if (most > _maxOnOne)
        {
            _maxOnOne = most;
            if (most > 1)
            {
                ModLog.Info("GapTestHooks: " + most + " trolls work on the same source " + _maxWhere);
            }
        }

        return most;
    }

    /// Polls stt.farmersOnOneSource every frame of a `wait until` for `seconds` (the highest is kept for stt.farmersOnOneSourceMax), then answers true.
    [TestHook("stt.watchFarmers")]
    private static bool WatchFarmers(float seconds)
    {
        if (_watchStart < 0f)
        {
            _watchStart = Time.time;
            _maxOnOne = -1;
        }

        FarmersOnOneSource();
        if (Time.time - _watchStart < seconds)
        {
            return false;
        }

        _watchStart = -1f;
        return true;
    }

    /// The highest number of farmers seen on one source during the last stt.watchFarmers.
    [TestHook("stt.farmersOnOneSourceMax")]
    private static int FarmersOnOneSourceMax()
    {
        return _maxOnOne;
    }
    // ---- water ring and ships (D-131) -------------------------------------------------------------------------------------------------------------------

    /// The nearest open water at least `depth` m deep (world-generator heights, ring search out to `maxMetres` from the player), with deep water all around for
    /// `around` m; the player is teleported onto its surface. Expected outcome: a description; throws when there is none.
    [TestHook("stt.toWater")]
    private static string ToWater(float depth, float around, float maxMetres)
    {
        Player p = Player.m_localPlayer;
        WorldGenerator wg = WorldGenerator.instance;
        float water = ZoneSystem.instance.m_waterLevel;
        Vector3 from = p.transform.position;
        for (float r = 50f; r <= maxMetres; r += 25f)
        {
            int steps = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * r / 25f));
            for (int k = 0; k < steps; k++)
            {
                float a = k * Mathf.PI * 2f / steps;
                Vector3 q = from + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!Deep(wg, q, water, depth) || !Deep(wg, q + Vector3.right * around, water, depth) || !Deep(wg, q - Vector3.right * around, water, depth) ||
                    !Deep(wg, q + Vector3.forward * around, water, depth) || !Deep(wg, q - Vector3.forward * around, water, depth))
                {
                    continue;
                }

                q.y = water + 0.5f;
                p.TeleportTo(q, Quaternion.identity, true);
                string info = "open water " + r.ToString("F0") + " m away at " + q.ToString("F0") + ", ground " + (wg.GetHeight(q.x, q.z) - water).ToString("F1") + " m under the surface";
                ModLog.Info("GapTestHooks: " + info);
                return info;
            }
        }

        throw new Exception("no water " + depth + " m deep within " + maxMetres + " m");
    }

    private static bool Deep(WorldGenerator wg, Vector3 q, float water, float depth)
    {
        return wg.GetHeight(q.x, q.z) < water - depth;
    }

    /// A tamed troll `dx`,`dz` m from the player at the water surface (not on the lake bottom). Expected outcome: the troll.
    [TestHook("stt.spawnTrollInWater")]
    private static GameObject SpawnTrollInWater(float dx, float dz)
    {
        Player p = Player.m_localPlayer;
        Vector3 at = p.transform.position + new Vector3(dx, 0f, dz);
        at.y = ZoneSystem.instance.m_waterLevel - 1f;
        return TestHooks.SpawnTrollAt(at);
    }

    /// D-131: flat metres from the troll to the player, and whether TrollWaterKeep says the troll keeps its distance (is in water). Expected outcome: e.g. "15.2 m, keeps True".
    [TestHook("stt.waterRing")]
    private static string WaterRing(GameObject troll)
    {
        Character c = troll.GetComponent<Character>();
        Vector3 d = troll.transform.position - Player.m_localPlayer.transform.position;
        d.y = 0f;
        string s = d.magnitude.ToString("F1") + " m, keeps " + TrollWaterKeep.Keeps(c) + ", swimming " + c.IsSwimming();
        return s;
    }

    /// True while the troll is in water and within `tolerance` m of the WaterKeepDistance ring around the player.
    [TestHook("stt.holdsWaterRing")]
    private static bool HoldsWaterRing(GameObject troll, float tolerance)
    {
        Character c = troll.GetComponent<Character>();
        Vector3 d = troll.transform.position - Player.m_localPlayer.transform.position;
        d.y = 0f;
        return TrollWaterKeep.Keeps(c) && Mathf.Abs(d.magnitude - ModSettings.WaterKeepDistance.Value) <= tolerance;
    }

    /// A Karve (the game's prefab) `dx`,`dz` m from the player on the water. Expected outcome: the ship.
    [TestHook("stt.spawnShip")]
    private static GameObject SpawnShip(float dx, float dz)
    {
        Player p = Player.m_localPlayer;
        Vector3 at = p.transform.position + new Vector3(dx, 0f, dz);
        at.y = ZoneSystem.instance.m_waterLevel;
        GameObject go = Object.Instantiate(ZNetScene.instance.GetPrefab("Karve"), at, Quaternion.identity);
        ModLog.Info("GapTestHooks: Karve spawned at " + at.ToString("F0"));
        return go;
    }

    /// One 300-blunt hit on the ship through the game's WearNTear.Damage - from `troll` when given, else from nobody. Expected outcome: the ship's health in per mille after the hit.
    [TestHook("stt.hitShip")]
    private static int HitShip(GameObject ship, GameObject troll = null)
    {
        WearNTear w = ship.GetComponentInChildren<WearNTear>();
        var hit = new HitData();
        hit.m_damage.m_blunt = 300f;
        hit.m_point = w.transform.position;
        hit.m_dir = Vector3.down;
        if (troll != null)
        {
            hit.SetAttacker(troll.GetComponent<Character>());
        }

        w.Damage(hit);
        int left = ShipHealth(ship);
        ModLog.Info("GapTestHooks: ship hit by " + (troll != null ? "a tamed troll" : "nobody") + " - health " + left + " per mille");
        return left;
    }

    [TestHook("stt.shipHealth")]
    private static int ShipHealth(GameObject ship)
    {
        WearNTear w = ship.GetComponentInChildren<WearNTear>();
        return Mathf.RoundToInt(w.GetHealthPercentage() * 1000f);
    }

    // ---- Troll Club (D-120) ------------------------------------------------------------------------------------------------------------------------------

    /// Any prefab of the game `dx`,`dz` m from the player on the ground. Expected outcome: the object.
    [TestHook("stt.spawnPrefab")]
    private static GameObject SpawnPrefab(string prefab, float dx, float dz)
    {
        Player p = Player.m_localPlayer;
        Vector3 at = p.transform.position + new Vector3(dx, 0f, dz);
        at.y = ZoneSystem.instance.GetGroundHeight(at);
        GameObject pf = ZNetScene.instance.GetPrefab(prefab);
        if (pf == null)
        {
            throw new Exception("no prefab " + prefab);
        }

        GameObject go = Object.Instantiate(pf, at, Quaternion.identity);
        ModLog.Info("GapTestHooks: " + prefab + " spawned at " + at.ToString("F0"));
        return go;
    }

    /// One hit of the player's CURRENT weapon on `target` through the game's Damage (the path ClubBreaks patches): its damage and tool tier, the player as attacker.
    /// Expected outcome: "tool tier A -> B" (B = what the hit carried after the mod's prefix).
    [TestHook("stt.hitWithWeapon")]
    private static string HitWithWeapon(GameObject target)
    {
        Player p = Player.m_localPlayer;
        ItemDrop.ItemData w = p.GetCurrentWeapon();
        var hit = new HitData();
        hit.m_damage = w.GetDamage();
        hit.m_toolTier = (short)w.m_shared.m_toolTier;
        hit.m_point = target.transform.position + Vector3.up;
        hit.m_dir = (target.transform.position - p.transform.position).normalized;
        hit.SetAttacker(p);
        short before = hit.m_toolTier;
        IDestructible d = target.GetComponent<IDestructible>();
        if (d == null)
        {
            throw new Exception(target.name + " cannot be damaged");
        }

        d.Damage(hit);
        string s = "tool tier " + before + " -> " + hit.m_toolTier + " (" + (w.m_dropPrefab != null ? w.m_dropPrefab.name : w.m_shared.m_name) + ")";
        ModLog.Info("GapTestHooks: hit " + target.name + " - " + s);
        return s;
    }

    /// True once `target` lost health (or is gone - destroyed by the hit). Expected outcome: true after a hit that counts.
    [TestHook("stt.objectHurt")]
    private static bool ObjectHurt(GameObject target)
    {
        if (target == null)
        {
            return true;
        }

        TreeBase t = target.GetComponent<TreeBase>();
        Destructible ds = target.GetComponent<Destructible>();
        float full = t != null ? t.m_health : ds != null ? ds.m_health : 0f;
        return ObjectHealth(target) < Mathf.RoundToInt(full);
    }

    /// The three costume pieces (helmet, chest, legs) come off and out of the inventory - the set bonus ends. The slippers stay. Expected outcome: how many pieces were removed.
    [TestHook("stt.takeOffCostume")]
    internal static int TakeOffCostume()
    {
        Player p = Player.m_localPlayer;
        int n = 0;
        foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(p.GetInventory().GetAllItems()))
        {
            string name = it.m_dropPrefab != null ? it.m_dropPrefab.name : "";
            if (name != CostumeItems.HelmetName && name != CostumeItems.ChestName && name != CostumeItems.LegsName)
            {
                continue;
            }

            if (p.IsItemEquiped(it))
            {
                p.UnequipItem(it, false);
            }

            p.GetInventory().RemoveItem(it);
            n++;
        }

        if (n > 0)
        {
            ModLog.Info("GapTestHooks: " + n + " costume piece(s) taken off and removed");
        }

        return n;
    }

    /// The health stored for `target` (ZDO "health"; the prefab's when never hit), rounded. Expected outcome: falls after a hit that counts.
    [TestHook("stt.objectHealth")]
    private static int ObjectHealth(GameObject target)
    {
        ZNetView v = target.GetComponent<ZNetView>();
        TreeBase t = target.GetComponent<TreeBase>();
        Destructible ds = target.GetComponent<Destructible>();
        float full = t != null ? t.m_health : ds != null ? ds.m_health : 0f;
        return Mathf.RoundToInt(v != null && v.IsValid() ? v.GetZDO().GetFloat(ZDOVars.s_health, full) : full);
    }
}
