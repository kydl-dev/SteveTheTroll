using System.Collections.Generic;
using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Application;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Hubs.Infrastructure;

/// <summary>One Troll Hub as the world knows it (read from its ZDO).</summary>
internal sealed class HubInfo
{
    internal int Number;
    internal long Owner;
    internal string OwnerName, Name;
    internal Vector3 Pos;
    internal ZDO Zdo;
}

/// <summary>
///     D-135: the Troll Hubs that exist, found through the game's object list (ZDOs: loaded or not), refreshed every few
///     seconds, and which hub a
///     troll uses (HubRules.Pick). All state lives in the hub's ZDO (owner, number, name, sign style), so every player
///     sees the same.
///     Limit: a client only knows the ZDOs the game has sent it (single player / host: all).
/// </summary>
internal static class HubRegistry
{
    internal const string OwnerKey = "stt_hub_owner",
        OwnerNameKey = "stt_hub_ownername",
        NumberKey = "stt_hub_no",
        NameKey = "stt_hub_name",
        StyleKey = "stt_hub_style";

    /// On a troll's ZDO: the hub number the owner sent it to (0 / missing = automatic).
    internal const string AssignKey = "stt_hub_assign";

    private static readonly List<HubInfo> Hubs = new();
    private static readonly List<ZDO> Scratch = new();
    private static float _next = -100f;
    private static int _lastCount = -1;

    private static readonly HubAssignmentPipeline Assignment = new();

    internal static IList<HubInfo> All(bool force = false)
    {
        if (force || Time.time >= _next)
        {
            Refresh();
        }

        return Hubs;
    }

    private static void Refresh()
    {
        _next = Time.time + 3f;
        Hubs.Clear();
        ZDOMan zm = ZDOMan.instance;
        if (zm == null)
        {
            return;
        }

        Scratch.Clear();
        int index = 0, guard = 0;
        while (!zm.GetAllZDOsWithPrefabIterative(HubData.PrefabName, Scratch, ref index) && guard++ < 10000)
        {
        }

        foreach (ZDO z in Scratch)
        {
            int no = z.GetInt(NumberKey);
            if (no <= 0)
            {
                continue; // not set up yet (the piece numbers it a moment after it is placed)
            }

            Hubs.Add(new HubInfo
            {
                Zdo = z, Owner = z.GetLong(OwnerKey), OwnerName = z.GetString(OwnerNameKey), Number = no,
                Name = NameOf(z, no), Pos = z.GetPosition()
            });
        }

        if (Hubs.Count != _lastCount)
        {
            _lastCount = Hubs.Count;
            ModLog.Info("HubRegistry: " + Hubs.Count + " Troll Hub(s) known");
        }
    }

    internal static string NameOf(ZDO z, int number)
    {
        string n = z != null ? z.GetString(NameKey) : "";
        return n.Length > 0 ? n : HubRules.DefaultName(number);
    }

    internal static List<HubInfo> Own(long owner)
    {
        var list = new List<HubInfo>();
        foreach (HubInfo h in All())
        {
            if (h.Owner == owner)
            {
                list.Add(h);
            }
        }

        list.Sort((a, b) => a.Number.CompareTo(b.Number));
        return list;
    }

    internal static HubInfo Find(long owner, int number)
    {
        foreach (HubInfo h in All())
        {
            if (h.Owner == owner && h.Number == number)
            {
                return h;
            }
        }

        return null;
    }

    /// The owner's hub nearest to `pos` (null = none); `distance` = flat metres (-1).
    internal static HubInfo Nearest(long owner, Vector3 pos, out float distance)
    {
        IList<HubInfo> all = All();
        long[] owners = new long[all.Count];
        float[] dists = new float[all.Count];
        for (int i = 0; i < all.Count; i++)
        {
            owners[i] = all[i].Owner;
            dists[i] = HubGeometry.Flat(all[i].Pos - pos);
        }

        int at = HubRules.NearestOwned(owner, owners, dists, out distance); // D-160: the choice is the unit-tested rule
        return at >= 0 ? all[at] : null;
    }

    /// Placement check for a new hub at `pos` by `owner`. Expected outcome: Ok, or why not; `nearest` = flat distance to the nearest hub of anyone (-1).
    internal static HubRules.Verdict CanBuild(long owner, Vector3 pos, out float nearest)
    {
        nearest = -1f;
        foreach (HubInfo h in All(true))
        {
            float d = HubGeometry.Flat(h.Pos - pos);
            if (nearest < 0f || d < nearest)
            {
                nearest = d;
            }
        }

        return HubRules.Check(Own(owner).Count, nearest);
    }

    /// The hub a troll uses, or null (use the bed): `owner`'s hub chosen by hand while it is within reach, else the nearest when AutoAssign is on.
    internal static HubInfo For(long owner, Vector3 trollPos, int manual)
    {
        // D-173 (Architecture V2, P7): HubAssignmentPipeline decides (hubs on / owner, then HubRules.Pick); this reads the registry
        if (!ModSettings.HubsOn || owner == 0L)
        {
            return null; // cheap exit before the registry scan; the pipeline repeats it for the record
        }

        float nearestDist;
        HubInfo nearest = Nearest(owner, trollPos, out nearestDist);
        HubInfo chosen = manual > 0 ? Find(owner, manual) : null;
        OperationResponse<int> pick = Assignment.Evaluate(new HubAssignmentInput(ModSettings.HubsOn, owner, manual,
            chosen != null ? HubGeometry.Flat(chosen.Pos - trollPos) : -1f, nearest != null ? nearest.Number : 0,
            nearestDist, ModSettings.HubAuto));
        return pick.IsSuccess && pick.Value > 0 ? Find(owner, pick.Value) : null;
    }

    internal static HubInfo For(Character troll)
    {
        ZDO z = TrollTaming.Zdo(troll);
        return z == null ? null : For(TrollTaming.Owner(troll), troll.transform.position, z.GetInt(AssignKey));
    }

    /// D-159: what the messages call the troll's destination: its hub's name, else "your bed" (Loc key stevethetroll_dest_bed).
    internal static string DestinationName(Character troll)
    {
        HubInfo h = For(troll);
        return h != null ? h.Name : Loc.T("stevethetroll_dest_bed");
    }

    /// Where an own troll goes: its hub, else the bed. Expected outcome: the hub's position (true) or `bed` (false).
    internal static bool Anchor(Character troll, Vector3 bed, out Vector3 at)
    {
        HubInfo h = For(troll);
        at = h != null ? h.Pos : bed;
        return h != null;
    }
}
