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

/// <summary>D-177 (class size): GapTestHooks continued - the water ring and ships (D-131) and the Troll Club (D-120).</summary>
internal static partial class GapTestHooks
{
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

        _lastHit = target;
        _lastHitFull = ObjectHealth(target);
        d.Damage(hit);
        string s = "tool tier " + before + " -> " + hit.m_toolTier + " (" + (w.m_dropPrefab != null ? w.m_dropPrefab.name : w.m_shared.m_name) + ")";
        ModLog.Info("GapTestHooks: hit " + target.name + " - " + s);
        return s;
    }

    private static GameObject _lastHit;
    private static int _lastHitFull;

    /// D-178 (fix11 club-breaks: the club broke the shoot in ONE hit, and a destroyed object cannot be named as a hook parameter): the object stt.hitWithWeapon hit last
    /// lost health or is gone. Expected outcome: true after a hit that counted.
    [TestHook("stt.lastHitHurt")]
    private static bool LastHitHurt()
    {
        if (_lastHit == null)
        {
            ModLog.Info("GapTestHooks: the object hit last is gone (destroyed by the hit)");
            return true;
        }

        int now = ObjectHealth(_lastHit);
        ModLog.Info("GapTestHooks: " + _lastHit.name + " health " + _lastHitFull + " before the hit, " + now + " now");
        return now < _lastHitFull;
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
