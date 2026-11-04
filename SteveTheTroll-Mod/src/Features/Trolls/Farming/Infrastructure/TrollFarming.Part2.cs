using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Movement.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Domain;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Farming.Infrastructure;

/// <summary>Part 2 of TrollFarming (split out so the class stays under the size limit).</summary>
internal static partial class TrollFarming
{
    private static bool FarYieldsItem(int hash, string item)
    {
        long key = ((long)hash << 20) ^ item.GetStableHashCode();
        bool y;
        if (FarYields.TryGetValue(key, out y))
        {
            return y;
        }

        GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(hash) : null;
        y = prefab != null && Yields(prefab, item);
        FarYields[key] = y;
        return y;
    }

    /// Nearest farm source beyond `nearRange` and within `range` of `from` that yields a goal, the goals in priority order (the first goal that has any source wins).
    /// `skip` gets the source's position (no-hit area, hub leash, positions given up). Expected outcome: true with its position and the item it yields.
    internal static bool FindFar(Vector3 from, SearchRange band, int tier,
        IEnumerable<TrollFocus.Goal> goals, Func<Vector3, bool> skip, out Vector3 pos, out string item)
    {
        float range = band.Range, nearRange = band.NearRange;
        pos = Vector3.zero;
        item = "";
        ZDOMan man = ZDOMan.instance;
        if (man == null || ZNetScene.instance == null || FindObjectsMethod == null)
        {
            return false;
        }

        var gl = new List<TrollFocus.Goal>(goals);
        float[] bestD = new float[gl.Count];
        var bestP = new Vector3[gl.Count];
        for (int i = 0; i < bestD.Length; i++)
        {
            bestD[i] = float.MaxValue;
        }

        long t0 = Stopwatch.GetTimestamp();
        FarZdos.Clear();
        Vector2s zone = ZoneSystem.GetZone(from);
        int reachZones = Mathf.CeilToInt(range / ZoneSystem.c_ZoneSize);
        // The visited-sector set is HashSet<int> in some game versions and HashSet<ZoneSystem.SectorIndex> in others (TEST-037 again, found by the lab):
        // built from the method's own parameter type so it matches whichever game runs.
        object visited = Activator.CreateInstance(FindObjectsMethod.GetParameters()[2].ParameterType);
        try
        {
            for (int zx = -reachZones; zx <= reachZones; zx++)
            for (int zy = -reachZones; zy <= reachZones; zy++)
            {
                FindObjectsMethod.Invoke(man,
                    new[] { new Vector2s((short)(zone.x + zx), (short)(zone.y + zy)), FarZdos, visited });
            }
        }
        catch (Exception e)
        {
            LastFar = "FAILED: " + (e.InnerException != null ? e.InnerException.Message : e.Message);
            if (!_farWarned)
            {
                _farWarned = true;
                ModLog.Warn("TrollFarming: the long-range search failed once and is skipped: " + LastFar);
            }

            FarZdos.Clear();
            return false;
        }

        float r2 = range * range, n2 = nearRange * nearRange;
        bool skipYoung = ModSettings.FarmSkipYoung.Value;
        int sources = 0;
        foreach (ZDO z in FarZdos)
        {
            if (z == null)
            {
                continue;
            }

            Vector3 zp = z.GetPosition();
            float dx = zp.x - from.x, dz = zp.z - from.z, d2 = dx * dx + dz * dz;
            if (d2 > r2 || d2 <= n2)
            {
                continue;
            }

            int hash = z.GetPrefab();
            FarInfo info = FarOf(hash);
            if (!info.Source || info.MinTier > tier || (info.Young && skipYoung))
            {
                continue;
            }

            sources++;
            for (int i = 0; i < gl.Count; i++)
            {
                if (d2 >= bestD[i] || !FarYieldsItem(hash, gl[i].Item) || !TrollFocus.SourceMatches(gl[i], info.Name))
                {
                    continue;
                }

                if (skip != null && skip(zp))
                {
                    continue;
                }

                bestD[i] = d2;
                bestP[i] = zp;
            }
        }

        double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        LastFar = FarZdos.Count + " object(s) known, " + sources + " source(s) in " + nearRange.ToString("0") + " .. " +
                  range.ToString("0") + " m, " + ms.ToString("F1") + " ms";
        for (int i = 0; i < gl.Count; i++)
        {
            if (bestD[i] >= float.MaxValue)
            {
                continue;
            }

            pos = bestP[i];
            item = gl[i].Item;
            LastFar += ", best " + item + " " + Mathf.Sqrt(bestD[i]).ToString("F0") + " m away";
            return true;
        }

        return false;
    }

    // The source data of a networked object (null = not a farm source), cached per object.
    private static FarmSourceCache Source(ZNetView v)
    {
        int id = v.GetInstanceID();
        FarmSourceCache c;
        if (Known.TryGetValue(id, out c))
        {
            return c;
        }

        GameObject g = v.gameObject;
        bool tree = g.GetComponent<TreeBase>() != null, log = g.GetComponent<TreeLog>() != null;
        bool rock = g.GetComponent<MineRock5>() != null || g.GetComponent<MineRock>() != null;
        bool other = !tree && !log && !rock && g.GetComponent<Destructible>() != null;
        if (Known.Count > 20000)
        {
            Known.Clear();
        }

        if (!tree && !log && !rock && !other)
        {
            Known[id] = null;
            return null;
        }

        c = g.GetComponent<FarmSourceCache>();
        if (c == null)
        {
            c = g.AddComponent<FarmSourceCache>();
        }

        c.Collider = Biggest(g);
        c.IsTree = tree;
        c.PrefabName = Utils.GetPrefabName(g);
        c.MinTier = MinTier(g);
        c.Young = !log && !rock && TamingRules.IsYoungGrowth(c.PrefabName);
        c.Tier = TamingRules.Tier(log, rock, tree, c.Collider != null ? c.Collider.bounds.size.y : 0f, MinOtherHeight);
        Known[id] = c;
        return c;
    }

    /// D-082 (TEST-035: Steve 4 circled a silver vein - the game said 'Minerock hit has no collider or invalid hit area'): for a MineRock5
    /// (veins, big rocks) the nearest hit area that still has health, else null (then the source's biggest collider is used).
    internal static Collider LiveArea(GameObject source, Vector3 from)
    {
        MineRock5 mr = source != null ? source.GetComponentInParent<MineRock5>() : null;
        if (mr == null || HitAreas == null)
        {
            return null;
        }

        var list = HitAreas.GetValue(mr) as IList;
        if (list == null)
        {
            return null;
        }

        Collider best = null;
        float bestD = float.MaxValue;
        foreach (object a in list)
        {
            if (a == null)
            {
                continue;
            }

            Type t = a.GetType();
            Collider col = AccessTools.Field(t, "m_collider") != null
                ? AccessTools.Field(t, "m_collider").GetValue(a) as Collider
                : null;
            float hp = AccessTools.Field(t, "m_health") != null
                ? (float)AccessTools.Field(t, "m_health").GetValue(a)
                : 1f;
            if (col == null || !col.enabled || hp <= 0f)
            {
                continue;
            }

            float d = (col.bounds.center - from).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = col;
            }
        }

        return best;
    }

    internal static Collider Biggest(GameObject g)
    {
        Collider best = null;
        float size = -1f;
        foreach (Collider col in g.GetComponentsInChildren<Collider>())
        {
            if (col == null || col.isTrigger)
            {
                continue;
            }

            float sz = col.bounds.size.sqrMagnitude;
            if (sz > size)
            {
                size = sz;
                best = col;
            }
        }

        return best;
    }

    /// D-084 performance: one farm-source candidate near a troll (gathered once per search, then filtered per goal / range).
    internal struct Candidate
    {
        internal GameObject G;
        internal FarmSourceCache Src;
        internal Vector3 Pos;
    }

    // ---- Long range (user 2026-10-31: "increase troll search for material (long range) at 500 m, keep the immediate search at 20 m") ----
    // The loaded objects (Instances) only reach as far as the loaded area; beyond it the search walks the game's ZDO list (every object this session knows about, in zones
    // that need not be loaded) and returns the POSITION of the nearest source: the troll walks there, the zone loads around it, and the normal search takes over.
    internal struct FarInfo
    {
        internal bool Source, Young;
        internal string Name;
        internal int MinTier;
    }

    // Harmony hook: src/Integrations/Harmony/TrollsFarmingPatches.cs, TrollFarming_Steer (MIGRATION_DECISIONS A5)
    internal static class Steer
    {
        // D-048: a PREFIX now - while the troll farms, the game's AI step (idle wandering, the feeding animation, its own attack timer) is
        // skipped completely; it runs again when farming hands back (fight, follow order, nothing to farm).
        internal static bool Prefix(MonsterAI __instance, float dt, ref bool __result)
        {
            if (__instance == null)
            {
                return true;
            }

            ZNetView view0 = __instance.GetComponent<ZNetView>();
            if (view0 == null || !view0.IsValid() || !view0.IsOwner())
            {
                return true;
            }

            TrollSlam slam = __instance.GetComponent<TrollSlam>();
            if (slam != null && slam.Step(__instance))
            {
                __result = true;
                return false;
            } // D-065: ground slams first

            TrollRage rage = __instance.GetComponent<TrollRage>();
            if (rage != null && rage.Step(__instance, dt))
            {
                __result = true;
                return false;
            } // D-082: rage while the player is hit

            TrollChase chase = __instance.GetComponent<TrollChase>();
            if (chase != null && chase.Step(__instance, dt))
            {
                __result = true;
                return false;
            } // D-084: goes for the one that hit it

            Character self = __instance.GetComponent<Character>();
            if (TrollCounter.Ignoring(self) && !TrollGuard.HasForced(self) &&
                __instance.GetComponent<TrollFarmer>() != null)
            {
                TrollGuard.ClearTarget(__instance); // D-084: hits ignored, keeps farming
            }

            TrollGuard.Keep(__instance); // D-066: the forced target stays
            if (TrollHold.Holds(__instance))
            {
                __instance.StopMoving();
                __result = true;
                return false;
            } // D-063

            TrollReturn back = __instance.GetComponent<TrollReturn>();
            if (back != null && back.Step(__instance, dt))
            {
                __result = true;
                return false;
            } // D-162: past the hub's reach, no follow order - walks back

            TrollFarmer f = __instance.GetComponent<TrollFarmer>();
            if (f == null)
            {
                return true;
            }

            ZNetView view = __instance.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                return true;
            }

            bool drove = f.Step(__instance, dt); // D-170: the farmer moves through its TrollContext capability
            if (drove)
            {
                __result = true;
            }

            return !drove;
        }
    }
}
