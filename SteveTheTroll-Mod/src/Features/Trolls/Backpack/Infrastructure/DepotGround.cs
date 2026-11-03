using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Jotunn.Managers;
using KydlCore.Common;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Features.Trolls.Backpack.Infrastructure;

/// <summary>
///     D-095 / D-103 / D-105 (user: "flatten the ground a bit where the structure is to go"): levels the ground under a
///     new depot stack.
///     1.0.21 has no level-ground terrain op (the hoe's mud_road_v2 only smooths / paints), so the mod brings its own
///     copies of mud_road_v2
///     with level on, registered in ObjectDB's terrain op list on every ObjectDB load (all players have the mod;
///     TerrainComp's RPC finds an op's
///     settings by its name).
///     TEST-044 (user: "it leveled at same time very high and very low"): 24 small ops placed in one frame overlapped; the
///     game levels each
///     vertex by "target - height" where height is the heightmap NOT yet rebuilt in that frame, so a vertex under 4 ops
///     got the change 4 times
///     (clamped at +-8 m) - spikes and pits. Now ONE square op per depot covers the whole footprint: no vertex is levelled
///     twice.
/// </summary>
internal static class DepotGround
{
    internal const float Margin = 0.8f;

    /// One depot: a square op (axis-aligned in the world) whose half side covers the turned footprint + Margin (LevelRadius()).
    internal const string DepotOp = "STT_DepotLevel";

    /// Repair: a wider square (stt_levelground).
    internal const string RepairOp = "STT_GroundLevel";

    internal const float RepairRadius = 7f;
    private const string OwnBase = "mud_road_v2";

    /// D-135: the Troll Hub's ground (one square op over the whole building).
    internal const string HubOp = "STT_HubLevel";

    private static GameObject _depot, _repair, _hub;

    // User 2026-10-31 (SmarterHoe, idea only): a terrain op that reaches over several terrain tiles (a TerrainComp each, 64 m zones) can only change the tiles this client has
    // initialised, loaded and owns (the likely reason a wide op levels only part of the ground - not proven in game for the depot). SmarterHoe therefore (1) initialises + loads + claims every
    // tile under the op's radius before it, and (2) force-sends the tiles' ZDOs right after. The same two steps wrap every op of this class.
    private static readonly MethodInfo CompInit = AccessTools.Method(typeof(TerrainComp), "Initialize");
    private static readonly MethodInfo CompCheckLoad = AccessTools.Method(typeof(TerrainComp), "CheckLoad");
    private static readonly FieldInfo CompInitialized = AccessTools.Field(typeof(TerrainComp), "m_initialized");
    private static readonly FieldInfo CompView = AccessTools.Field(typeof(TerrainComp), "m_nview");
    private static readonly List<Heightmap> Maps = new();

    /// Whether depots may stand on uneven ground (our depot op is made and registered).
    internal static bool CanLevel => Registered(_depot);

    /// Pure: half side of an axis-aligned square that holds a rectangle of half sizes halfX / halfZ turned any way (= its half diagonal).
    internal static float LevelRadius(float halfX, float halfZ)
    {
        return Mathf.Sqrt(halfX * halfX + halfZ * halfZ);
    }

    internal static void Install()
    {
        PrefabManager.OnVanillaPrefabsAvailable += MakeOwn;
        ItemManager.OnItemsRegistered += RegisterOwn;
    }

    private static void MakeOwn()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= MakeOwn;
        if (_depot == null)
        {
            _depot = Make(DepotOp, LevelRadius(DepotData.HalfX + Margin, DepotData.HalfZ + Margin) + 0.2f);
        }

        if (_repair == null)
        {
            _repair = Make(RepairOp, RepairRadius);
        }

        if (_hub == null)
        {
            _hub = Make(HubOp, HubData.Radius + 1.5f);
        }
    }

    private static GameObject Make(string name, float radius)
    {
        GameObject clone = PrefabManager.Instance.CreateClonedPrefab(name, OwnBase);
        TerrainOp op = clone != null ? clone.GetComponent<TerrainOp>() : null;
        if (op == null)
        {
            ModLog.Warn("DepotGround: could not copy '" + OwnBase + "' (" +
                        (clone != null ? "no TerrainOp" : "not found") + ") - depots only on flat ground");
            return null;
        }

        foreach (ZNetView v in clone.GetComponents<ZNetView>())
        {
            Object.DestroyImmediate(v); // local only: TerrainOp applies itself and is gone
        }

        foreach (Piece p in clone.GetComponents<Piece>())
        {
            Object.DestroyImmediate(p);
        }

        TerrainOp.Settings s = op.m_settings;
        s.m_level = true;
        s.m_levelRadius = radius;
        s.m_square = true;
        s.m_levelOffset = 0f;
        s.m_raise = false;
        s.m_smooth = false;
        s.m_paintRadius = radius;
        op.m_spawnOnPlaced = null;
        ModLog.Info("DepotGround: own level op '" + name + "' made from " + OwnBase + " (level, square, half side " +
                    radius.ToString("F2") + " m, paint " + s.m_paintCleared + ")");
        return clone;
    }

    // ObjectDB is made anew for the main menu and for every world: our ops go into its terrain op list each time.
    private static void RegisterOwn()
    {
        ObjectDB db = ObjectDB.instance;
        if (db == null)
        {
            return;
        }

        foreach (GameObject go in new[] { _depot, _repair, _hub })
        {
            if (go == null)
            {
                continue;
            }

            TerrainOp op = go.GetComponent<TerrainOp>();
            if (!db.m_terrainOps.Contains(op))
            {
                db.m_terrainOps.Add(op);
            }

            db.m_terrainOpsByHash[go.name.GetStableHashCode()] = op;
        }

        ModLog.Verbose("DepotGround: own level ops registered in ObjectDB (" + db.m_terrainOps.Count + " terrain ops)");
    }

    private static bool Registered(GameObject go)
    {
        TerrainOp found;
        return go != null && ObjectDB.instance != null && ObjectDB.instance.TryGetTerrainOp(go.name, out found) &&
               found != null;
    }

    /// Prepares every terrain tile within `radius` of `centre` for writing. Expected outcome: the number of tiles prepared (0 = none found / the game's members were not found).
    internal static int Prepare(Vector3 centre, float radius, List<ZNetView> views = null)
    {
        Maps.Clear();
        Heightmap.FindHeightmap(centre, radius, Maps);
        int n = 0;
        foreach (Heightmap hm in Maps)
        {
            TerrainComp comp = hm != null ? hm.GetAndCreateTerrainCompiler() : null;
            if (comp == null)
            {
                continue;
            }

            try
            {
                if (CompInitialized != null && CompInit != null && !(bool)CompInitialized.GetValue(comp))
                {
                    CompInit.Invoke(comp, null);
                }

                ZNetView nv = CompView != null ? CompView.GetValue(comp) as ZNetView : null;
                if (nv != null && nv.IsValid())
                {
                    if (CompCheckLoad != null)
                    {
                        CompCheckLoad.Invoke(comp, null);
                    }

                    if (!nv.IsOwner())
                    {
                        nv.ClaimOwnership();
                    }

                    if (views != null)
                    {
                        views.Add(nv);
                    }
                }

                n++;
            }
            catch (Exception e)
            {
                ModLog.Warn("DepotGround: could not prepare a terrain tile: " + e.GetType().Name + ": " + e.Message);
            }
        }

        return n;
    }

    /// Sends the prepared tiles' data at once (a client that is not the tile's owner would otherwise see the old ground until the next sync).
    private static void Flush(List<ZNetView> views)
    {
        if (ZDOMan.instance == null)
        {
            return;
        }

        foreach (ZNetView nv in views)
        {
            if (nv == null || !nv.IsValid())
            {
                continue;
            }

            ZDO z = nv.GetZDO();
            if (z != null)
            {
                ZDOMan.instance.ForceSendZDO(z.m_uid);
            }
        }
    }

    // Prepare, place the op (TerrainOp.Awake applies itself to the terrain and destroys the object), flush.
    private static void Apply(GameObject op, Vector3 at)
    {
        var views = new List<ZNetView>();
        int tiles = Prepare(at, op.GetComponent<TerrainOp>().m_settings.m_levelRadius * 1.5f, views);
        Object.Instantiate(op, at, Quaternion.identity);
        Flush(views);
        ModLog.Verbose("DepotGround: " + tiles + " terrain tile(s) prepared and sent for " + op.name + " at " +
                       at.ToString("F1"));
    }

    /// Levels the depot's footprint around `ground` to ground.y with ONE op. Expected outcome: true when it was levelled.
    internal static bool Flatten(Vector3 ground, Quaternion rot)
    {
        if (!CanLevel)
        {
            return false;
        }

        Apply(_depot, ground);
        ModLog.Info("DepotGround: levelled a " +
                    (2f * _depot.GetComponent<TerrainOp>().m_settings.m_levelRadius).ToString("F1") + " m square at " +
                    ground.ToString("F1"));
        return true;
    }

    /// D-135: levels the Troll Hub's square (half side Radius + 1.5 m) to ground.y with ONE op. Expected outcome: true when it was levelled.
    internal static bool FlattenHub(Vector3 ground)
    {
        if (!Registered(_hub))
        {
            return false;
        }

        Apply(_hub, ground);
        ModLog.Info("DepotGround: levelled the Troll Hub square at " + ground.ToString("F1"));
        return true;
    }

    /// stt_levelground: one wide square levelled to `height` around `centre`. Expected outcome: true when it was levelled.
    internal static bool Repair(Vector3 centre, float height)
    {
        if (!Registered(_repair))
        {
            return false;
        }

        Apply(_repair, new Vector3(centre.x, height, centre.z));
        ModLog.Info("DepotGround: repaired a " + (2f * RepairRadius).ToString("0") + " m square at " +
                    centre.ToString("F1") + " to height " + height.ToString("F2"));
        return true;
    }
}
