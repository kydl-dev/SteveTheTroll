using System;
using System.Collections.Generic;
using KydlCore.Common;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>Spawn helpers of the test hooks (split from TestHooks.cs: class-size gate G04).</summary>
internal static partial class TestHooks
{
    private static GameObject Spawn(float dx, float dz, float minMetres)
    {
        Player p = Player.m_localPlayer;
        var off = new Vector3(dx, 0f, dz);
        Vector3 fwd = p.GetLookDir();
        fwd.y = 0f;
        if (Vector3.Dot(off, fwd) <= 0f)
        {
            throw new Exception("troll offset (" + dx + ", " + dz + ") is not in front of the player (facing " +
                                fwd.normalized.ToString("F2") + ") - spawn trolls in front (D-172)");
        }

        if (off.magnitude < minMetres)
        {
            off = (off.sqrMagnitude > 0.01f ? off.normalized : -p.transform.forward) * minMetres;
        }

        Vector3 at = p.transform.position + off;
        at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.3f;
        ModLog.Info("TestHooks: troll spawned " + off.magnitude.ToString("F1") + " m from the player at " +
                    at.ToString("F0") + (off.magnitude > new Vector2(dx, dz).magnitude + 0.1f
                        ? " (pushed out to " + minMetres + " m)"
                        : ""));
        ModLog.Info("TestHooks: ground under the spawn " + GroundProbe(at));
        int cleared = ClearBodySpace(at);
        if (cleared > 0) ModLog.Info("TestHooks: " + cleared + " tree(s) / rock(s) / bush(es) removed from the troll's body space before the spawn");
        bool ground = SpawnHold.GroundReady(at);
        GameObject troll = SpawnTrollAt(at);
        if (!ground)
        {
            troll.AddComponent<SpawnHold>();
            ModLog.Info("TestHooks: no terrain collider under the spawn yet (zone still loading) - the troll is held until there is one");
        }

        return troll;
    }

    /// 2026-11-05 (ecosystem run, instruction-latency): a troll spawned INSIDE tree colliders was thrown out by the physics engine at 16 - 22 m/s, once down through the
    /// terrain into the water volume (y 51 -> 38, "wades 48 m"); stt.clearAroundTroll removed the trees only after the spawn. Natural objects whose colliders overlap
    /// the troll's body (capsule 2 m radius, 6 m tall) are removed first. Build pieces and creatures are never touched. Expected outcome: how many were removed.
    private static int ClearBodySpace(Vector3 at)
    {
        var done = new HashSet<ZNetView>();
        // only SOLID colliders on layers a character collides with: physics pushes the troll out of those, not out of triggers or canopy "viewblock" volumes
        // (bodyspace-focus run: with every layer + triggers the canopy of farm-chops-tree's planted tree 11 m away counted and the test lost its tree)
        foreach (Collider c in Physics.OverlapCapsule(at + Vector3.up * 2f, at + Vector3.up * 6f, 2f, CharacterMask(), QueryTriggerInteraction.Ignore))
        {
            ZNetView v = c.GetComponentInParent<ZNetView>();
            if (v == null || !v.IsValid() || done.Contains(v) || !GapTestHooks.IsNatural(v.gameObject)) continue;
            done.Add(v);
            v.ClaimOwnership();
            ZNetScene.instance.Destroy(v.gameObject);
        }

        return done.Count;
    }

    private static int CharacterMask()
    {
        int layer = LayerMask.NameToLayer("character");
        int mask = 0;
        for (int i = 0; i < 32; i++)
        {
            if (!Physics.GetIgnoreLayerCollision(layer, i)) mask |= 1 << i;
        }

        return mask;
    }

    /// Diagnostics (2026-11-05): every collider under the spawn spot (name, layer, height), bare terrain, liquid level.
    private static string GroundProbe(Vector3 at)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("terrain ").Append(WorldGenerator.instance != null ? WorldGenerator.instance.GetHeight(at.x, at.z).ToString("F1") : "?")
          .Append(", GetGroundHeight ").Append(ZoneSystem.instance.GetGroundHeight(at).ToString("F1"))
          .Append(", liquid ").Append(Floating.GetLiquidLevel(at).ToString("F1"))
          .Append(", sea ").Append(ZoneSystem.instance.m_waterLevel.ToString("F0")).Append("; hits down from +30 m:");
        RaycastHit[] hits = Physics.RaycastAll(at + Vector3.up * 30f, Vector3.down, 120f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        int n = 0;
        foreach (RaycastHit h in hits)
        {
            if (n++ >= 8) break;
            sb.Append(' ').Append(h.collider.transform.root.name).Append('/').Append(h.collider.name).Append('[').Append(LayerMask.LayerToName(h.collider.gameObject.layer))
              .Append(h.collider.isTrigger ? ",trigger" : "").Append("]@").Append(h.point.y.ToString("F1"));
        }

        if (hits.Length == 0) sb.Append(" none");
        return sb.ToString();
    }
}
