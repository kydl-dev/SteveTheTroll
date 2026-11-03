using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Domain;
using SteveTheTroll.Features.Trolls.Orders.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Home.Infrastructure;

/// <summary>
///     D-085 (user: "when trolls are within 50m of bed, have their hp regen 50HP/s"): every second, the local player's own
///     tamed trolls that
///     this client runs and that stand within BedHealRange of that player's bed get BedHealPerSecond HP
///     (TamingRules.BedHeal, never above max).
/// </summary>
internal static class TrollBedHeal
{
    private const float TickSeconds = 1f;

    private static float _next, _nextLog;

    // Within `range2` (squared, flat) of the bed or of any of the hubs.
    private static bool InRange(Vector3 at, Vector3 bed, bool hasBed, IList<HubInfo> hubs, float range2)
    {
        if (hasBed && HomeRules.InHealRange(at.x - bed.x, at.z - bed.z, range2))
        {
            return hasBed; // D-171: the rule is game-free
        }

        if (hubs != null)
        {
            foreach (HubInfo h in hubs)
            {
                if (HomeRules.InHealRange(at.x - h.Pos.x, at.z - h.Pos.z, range2))
                {
                    return hubs != null;
                }
            }
        }

        return hubs == null && !hasBed && range2 < 0f;
    }

    /// Plugin.Update. Expected outcome: how many trolls were healed this second (-1 = nothing done this frame).
    internal static int Tick()
    {
        if (Time.time < _next)
        {
            return -1;
        }

        const float dt = TickSeconds;
        _next = Time.time + TickSeconds;
        Player p = Player.m_localPlayer;
        PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
        if (p == null || ModSettings.BedHealPerSecond.Value <= 0f)
        {
            return 0;
        }

        bool hasBed = profile != null && profile.HaveCustomSpawnPoint();
        Vector3 bed = hasBed ? profile.GetCustomSpawnPoint() : Vector3.zero;
        long me = p.GetPlayerID();
        List<HubInfo>
            hubs = ModSettings.HubsOn
                ? HubRegistry.Own(me)
                : null; // D-135: the same heal in the same range around each Troll Hub
        if (!hasBed && (hubs == null || hubs.Count == 0))
        {
            return 0;
        }

        float range2 = ModSettings.BedHealRange.Value * ModSettings.BedHealRange.Value;
        int healed = 0;
        foreach (Character c in Character.GetAllCharacters())
        {
            if (c == null || c.IsDead() || !TamedTrollStats.IsTamedTroll(c) || TrollTaming.Owner(c) != me)
            {
                continue;
            }

            ZNetView view = c.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                continue;
            }

            if (!InRange(c.transform.position, bed, hasBed, hubs, range2))
            {
                continue;
            }

            float add = TamingRules.BedHeal(c.GetHealth(), c.GetMaxHealth(), ModSettings.BedHealPerSecond.Value, dt);
            if (add <= 0f)
            {
                continue;
            }

            c.Heal(add, false);
            healed++;
            if (Time.time >= _nextLog && ModLog.DebugOn)
            {
                _nextLog = Time.time + 10f;
                ModLog.Verbose("TrollBedHeal: " + TrollOrders.Shown(c) + " +" + add.ToString("F0") +
                               " HP at the bed (" + c.GetHealth().ToString("F0") + " / " +
                               c.GetMaxHealth().ToString("F0") + ")");
            }
        }

        return healed;
    }
}
