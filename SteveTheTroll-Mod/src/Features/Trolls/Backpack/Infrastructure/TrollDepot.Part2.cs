using System;
using System.Collections.Generic;
using HarmonyLib;
using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Backpack.Application;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Features.Trolls.Backpack.Infrastructure;

/// <summary>Part 2 of TrollDepot (split out so the class stays under the size limit).</summary>
internal static partial class TrollDepot
{
    private static void Mark(GameObject chest, long owner)
    {
        ZDO z = chest.GetComponent<ZNetView>().GetZDO();
        z.Set(FlagKey, true);
        z.Set(OwnerKey, owner);
        z.Set(ZDOVars.s_creator, owner); // the game's "built by" (Piece reads it from the ZDO)
        Apply(chest);
    }

    /// A depot chest needs no support and cannot be removed for its materials.
    internal static void Apply(GameObject chest)
    {
        WearNTear w = chest.GetComponent<WearNTear>();
        if (w != null)
        {
            w.m_noSupportWear = true;
        }

        Piece p = chest.GetComponent<Piece>();
        if (p != null)
        {
            p.m_canBeRemoved = false;
        }
    }

    // The highest depot of the stack nearest the bed, when that stack is under MaxStack high.
    // avoidFor != null (D-107): stacks with another troll at them are left out
    private static bool OnStack(Vector3 bed, long owner, out Vector3 at, out Quaternion rot, Character avoidFor = null)
    {
        at = bed;
        rot = Quaternion.identity;
        var frames = new List<ZNetView>();
        foreach (ZNetView v in Near(bed, DepotPrefab.Name))
        {
            if (v.GetZDO().GetLong(OwnerKey) == owner)
            {
                frames.Add(v);
            }
        }

        if (frames.Count == 0)
        {
            return false;
        }

        frames.Sort((a, b) => Flat(a.transform.position - bed).CompareTo(Flat(b.transform.position - bed)));
        foreach (ZNetView bottom in frames)
        {
            // TEST-043: stacks from before the 15 .. 50 m ring (5 - 7 m from the bed) kept growing next to the house - only stacks inside
            // the ring get more levels (the old ones stay as they are and are still used for unloading)
            if (!DepotRules.InRing(Flat(bottom.transform.position - bed), MinFromBed, MaxFromBed))
            {
                continue;
            }

            if (avoidFor != null && Busy(bottom.transform.position, avoidFor))
            {
                continue;
            }

            int levels = 0;
            Transform top = null;
            foreach (ZNetView f in frames)
            {
                if (Flat(f.transform.position - bottom.transform.position) > ColumnMatch)
                {
                    continue;
                }

                levels++;
                if (top == null || f.transform.position.y > top.position.y)
                {
                    top = f.transform;
                }
            }

            if (levels >= DepotData.MaxStack)
            {
                continue;
            }

            at = top.position + Vector3.up * DepotPrefab.Height;
            rot = top.rotation;
            ModLog.Info("TrollDepot: stacks level " + (levels + 1) + " of " + DepotData.MaxStack + " at " +
                        at.ToString("F1"));
            return true;
        }

        return false;
    }

    internal static float Flat(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }

    // D-155: a troll-built structure (frame + its 4 flagged chests) is removed as soon as the last item leaves it. Checked whenever a flagged chest saves its contents
    // (a fresh structure is never checked before something changed in it, so it is not removed before the troll fills it).
    internal static void RemoveIfEmpty(Container saved)
    {
        if (!ModSettings.DepotRemoveEmpty.Value || saved == null)
        {
            return;
        }

        ZNetView sv = saved.GetComponent<ZNetView>();
        if (sv == null || !sv.IsValid() || !sv.GetZDO().GetBool(FlagKey) || !sv.IsOwner())
        {
            return;
        }

        long owner = sv.GetZDO().GetLong(OwnerKey);
        var chestViews = new List<ZNetView>(Near(saved.transform.position, DepotData.ChestPrefab));
        var frames = new List<ZNetView>(Near(saved.transform.position, DepotPrefab.Name));
        foreach (ZNetView f in frames)
        {
            if (f.GetZDO().GetLong(OwnerKey) != owner)
            {
                continue;
            }

            var mine = new List<Container>();
            bool hasSaved = false;
            foreach (DepotData.Part p in DepotData.Chests)
            {
                Vector3 expected = f.transform.position + f.transform.rotation * new Vector3(p.X, p.Y, p.Z);
                foreach (ZNetView cv in chestViews)
                {
                    if ((cv.transform.position - expected).sqrMagnitude > 0.6f * 0.6f || !cv.GetZDO().GetBool(FlagKey))
                    {
                        continue;
                    }

                    Container c = cv.GetComponent<Container>();
                    if (c == null)
                    {
                        continue;
                    }

                    mine.Add(c);
                    if (c == saved)
                    {
                        hasSaved = true;
                    }

                    break;
                }
            }

            if (!hasSaved)
            {
                continue;
            }

            int empty = 0;
            bool inUse = false, reserved = false;
            foreach (Container c in mine)
            {
                if (c.GetInventory().NrOfItems() == 0)
                {
                    empty++;
                }

                if (c.IsInUse())
                {
                    inUse = true;
                }

                KeyValuePair<int, float> r;
                if (Reserved.TryGetValue(c.GetInstanceID(), out r) && Time.time - r.Value < ReserveSeconds)
                {
                    reserved = true;
                }
            }

            bool above = false;
            foreach (ZNetView o in frames)
            {
                if (o != f && Flat(o.transform.position - f.transform.position) <= ColumnMatch &&
                    o.transform.position.y > f.transform.position.y + 0.1f)
                {
                    above = true;
                }
            }

            if (!DepotRules.Dismantle(mine.Count, empty, inUse, reserved, above))
            {
                if (DepotRules.Lifecycle(mine.Count, empty, inUse, reserved, above) == DepotRules.Verdict.Wait)
                {
                    Retry[saved.GetInstanceID()] = saved;
                    if (!WaitingSince.ContainsKey(saved.GetInstanceID()))
                    {
                        WaitingSince[saved.GetInstanceID()] = new KeyValuePair<Container, float>(saved, Time.time);
                    }
                } // D-159: empty but blocked (open / reserved / column) - looked at again by Tick

                ModLog.Verbose("TrollDepot: structure at " + f.transform.position.ToString("F1") + " kept (chests " +
                               mine.Count + ", empty " + empty + ", inUse " + inUse + ", reserved " + reserved +
                               ", frameAbove " + above + ")");
                return;
            }

            ModLog.Info("TrollDepot: structure at " + f.transform.position.ToString("F1") + " is empty - removed (" +
                        mine.Count + " chests + frame)");
            foreach (Container c in mine)
            {
                Remove(c.gameObject);
            }

            Remove(f.gameObject);
            return;
        }
    }

    /// D-160 (RuntimeHealth): how long the longest-waiting empty structure has been blocked (0 = none).
    internal static float LongestWait()
    {
        float worst = 0f;
        var gone = new List<int>();
        foreach (KeyValuePair<int, KeyValuePair<Container, float>> kv in WaitingSince)
        {
            if (kv.Value.Key == null)
            {
                gone.Add(kv.Key);
                continue;
            }

            worst = Mathf.Max(worst, Time.time - kv.Value.Value);
        }

        foreach (int k in gone)
        {
            WaitingSince.Remove(k);
        }

        return worst;
    }

    /// D-159: an empty structure that could not be removed yet (window open, troll reservation, another frame above) is checked again every 5 s until it can go.
    internal static void Tick()
    {
        if (Retry.Count == 0 || Time.time < _nextRetry)
        {
            return;
        }

        _nextRetry = Time.time + 5f;
        var list = new List<Container>(Retry.Values);
        Retry.Clear();
        foreach (Container c in list)
        {
            if (c == null)
            {
                continue;
            }

            try
            {
                RemoveIfEmpty(c);
            } // puts itself back into Retry when still blocked
            catch (Exception e)
            {
                ModLog.Warn("TrollDepot: retry failed: " + e.GetType().Name + ": " + e.Message);
            }

            if (!Retry.ContainsKey(c.GetInstanceID()))
            {
                WaitingSince.Remove(c.GetInstanceID());
            }
        }
    }

    private static void Remove(GameObject go)
    {
        ZNetView v = go.GetComponent<ZNetView>();
        if (v == null || !v.IsValid())
        {
            return;
        }

        v.ClaimOwnership();
        ZNetScene.instance.Destroy(go);
    }

    // Harmony hook: src/Integrations/Harmony/TrollsBackpackPatches.cs, TrollDepot_EmptyCheck (MIGRATION_DECISIONS A5)
    internal static class EmptyCheck
    {
        internal static void Postfix(Container __instance)
        {
            try
            {
                RemoveIfEmpty(__instance);
            }
            catch (Exception e)
            {
                ModLog.Warn("TrollDepot: empty check failed: " + e.GetType().Name + ": " + e.Message);
            }
        }
    }

    // D-159: Save runs while the chest window is still open (IsInUse), so the check is repeated when the window closes
    // Harmony hook: src/Integrations/Harmony/TrollsBackpackPatches.cs, TrollDepot_CloseCheck (MIGRATION_DECISIONS A5)
    internal static class CloseCheck
    {
        internal static void Postfix(Container __instance, bool inUse)
        {
            if (inUse)
            {
                return;
            }

            try
            {
                RemoveIfEmpty(__instance);
            }
            catch (Exception e)
            {
                ModLog.Warn("TrollDepot: close check failed: " + e.GetType().Name + ": " + e.Message);
            }
        }
    }

    // Harmony hook: src/Integrations/Harmony/TrollsBackpackPatches.cs, TrollDepot_ChestFlags (MIGRATION_DECISIONS A5)
    internal static class ChestFlags
    {
        internal static void Postfix(WearNTear __instance)
        {
            ZNetView v = __instance.GetComponent<ZNetView>();
            if (v == null || !v.IsValid() || !v.GetZDO().GetBool(FlagKey))
            {
                return;
            }

            Apply(__instance.gameObject);
        }
    }
}
