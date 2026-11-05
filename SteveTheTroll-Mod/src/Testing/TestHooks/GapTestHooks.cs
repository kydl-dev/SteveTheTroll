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
internal static partial class GapTestHooks
{
    // ---- the player out of the trolls' way (D-178) ------------------------------------------------------------------------------------------------------

    /// D-178: tamed trolls that came within 4 m of the player since stt.reset (TestProximityWatch).
    [TestHook("stt.trollContacts")]
    private static int TrollContacts()
    {
        return TestProximityWatch.Contacts;
    }

    /// D-178 (fix11 instruction-latency: the troll was boxed in by clutter at its start 70 m away and the latency check measured the terrain): trees, logs, bushes and rocks
    /// (not pieces, not creatures) within `radius` m of the troll are removed. Expected outcome: how many.
    [TestHook("stt.clearAroundTroll")]
    private static int ClearAroundTroll(GameObject troll, float radius)
    {
        int n = 0;
        Vector3 at = troll.transform.position;
        foreach (ZNetView v in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
        {
            if (v == null || !v.IsValid())
            {
                continue;
            }

            GameObject g = v.gameObject;
            Vector3 d = g.transform.position - at;
            d.y = 0f;
            if (d.magnitude > radius)
            {
                continue;
            }

            bool veg = g.GetComponent<TreeBase>() != null || g.GetComponent<TreeLog>() != null || g.GetComponent<MineRock>() != null || g.GetComponent<MineRock5>() != null ||
                       (g.GetComponent<Destructible>() != null && g.GetComponent<Piece>() == null && g.GetComponent<Character>() == null);
            if (!veg)
            {
                continue;
            }

            v.ClaimOwnership();
            ZNetScene.instance.Destroy(g);
            n++;
        }

        ModLog.Info("GapTestHooks: " + n + " tree(s) / log(s) / bush(es) / rock(s) removed within " + radius.ToString("F0") + " m of the troll");
        return n;
    }

    /// D-178: the player steps `back` m backwards (still facing +z, the scene in view) once the test has placed everything - trolls that farm, unload or go to the bed
    /// (which stt.reset put on the player's spot) no longer walk into the player. Call it AFTER every player-relative step. Expected outcome: the new position.
    [TestHook("stt.observe")]
    private static string Observe(float back = 15f)
    {
        Player p = Player.m_localPlayer;
        Vector3 to = p.transform.position + new Vector3(0f, 0f, -back);
        to.y = ZoneSystem.instance.GetGroundHeight(to) + 0.2f;
        ArenaTestHooks.Place(p, to, Quaternion.identity);

        ModLog.Info("GapTestHooks: the player watches from " + to.ToString("F0") + " (" + back.ToString("F0") + " m back, out of the trolls' way)");
        return to.ToString("F0");
    }

    /// D-180 (instruction-latency, analysis option A: the order's latency is a STATE, not a distance): the troll's farmer is in its home mode.
    [TestHook("stt.goingHome")]
    private static bool GoingHome(GameObject troll)
    {
        TrollFarmer f = troll.GetComponent<TrollFarmer>();
        return f != null && f.GoingHome;
    }

    /// D-179: bad spots remembered now (TrollPath).
    [TestHook("stt.badSpots")]
    private static int BadSpots()
    {
        return SteveTheTroll.Features.Trolls.Movement.Infrastructure.TrollPath.BadSpotCount;
    }

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
                troll.m_maxSpawned != max || !Mathf.Approximately(troll.m_spawnChance, chance) ? "boosted max " + troll.m_maxSpawned + " (census " + TrollCensus.Count + ")" : "vanilla";
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
}
