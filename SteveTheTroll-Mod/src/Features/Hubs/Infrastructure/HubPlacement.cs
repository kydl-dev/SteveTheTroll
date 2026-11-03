using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Costume.Infrastructure;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Presentation.Notifications;
using UnityEngine;

namespace SteveTheTroll.Features.Hubs.Infrastructure;

/// <summary>
///     D-135: the hammer side of the Troll Hub. Unlock: the hub shows in the build menu once the player has had the "Steve
///     The Troll" set bonus
///     active (stored on the character, so it stays). Placement: refused with a message when the player has 10 hubs,
///     another hub (anyone's) is
///     closer than 1000 m, or the ground under it is too uneven for the level op (more than 8 m between the highest and
///     lowest point).
/// </summary>
internal static class HubPlacement
{
    internal const string UnlockKey = "stt_hub_unlocked";
    internal const float MaxUneven = 8f;
    private static float _next;

    internal static bool Unlocked(Player p)
    {
        return p != null && p.m_customData != null && p.m_customData.ContainsKey(UnlockKey);
    }

    private static bool SetActive(Player p)
    {
        int h = CostumeItems.SetEffectHash;
        return p != null && h != 0 && p.GetSEMan() != null && p.GetSEMan().HaveStatusEffect(h);
    }

    /// Plugin.Update, every 2 s: the first time the set bonus is active the hub is unlocked. Expected outcome: true when it was unlocked now.
    internal static bool Tick()
    {
        if (Time.time < _next || !ModSettings.HubsOn)
        {
            return false;
        }

        _next = Time.time + 2f;
        Player p = Player.m_localPlayer;
        if (p == null || Unlocked(p) || !SetActive(p))
        {
            return false;
        }

        p.m_customData[UnlockKey] = "1";
        ModLog.Info("HubPlacement: the set bonus is active for the first time - Troll Hub unlocked for " +
                    p.GetPlayerName());
        Notify.Show(Loc.T("stevethetroll_hub_msg_unlocked"), Notify.Kind.Good);
        return true;
    }

    private static bool IsHub(Piece piece)
    {
        return piece != null && piece.gameObject.name.StartsWith(HubData.PrefabName);
    }

    /// Height difference of the ground under the hub's footprint (9 x 9 samples). Expected outcome: metres.
    internal static float Uneven(Vector3 at)
    {
        ZoneSystem zs = ZoneSystem.instance;
        if (zs == null)
        {
            return 0f;
        }

        float lo = float.MaxValue, hi = float.MinValue;
        for (int ix = -4; ix <= 4; ix++)
        for (int iz = -4; iz <= 4; iz++)
        {
            float h;
            if (!zs.GetGroundHeight(at + new Vector3(ix, 0f, iz) * (HubData.Radius / 4f), out h))
            {
                continue;
            }

            lo = Mathf.Min(lo, h);
            hi = Mathf.Max(hi, h);
        }

        return hi >= lo ? hi - lo : 0f;
    }

    // Harmony hook: src/Integrations/Harmony/HubsPatches.cs, HubPlacement_Visible (MIGRATION_DECISIONS A5)
    internal static class Visible
    {
        internal static void Postfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
        {
            if (!IsHub(piece))
            {
                return;
            }

            if (mode == Player.RequirementMode.IsKnown)
            {
                __result = true; // known materials are not required to SEE the hub once unlocked
            }
        }
    }

    // Harmony hook: src/Integrations/Harmony/HubsPatches.cs, HubPlacement_Place (MIGRATION_DECISIONS A5)
    internal static class Place
    {
        internal static bool Prefix(Player __instance, Piece piece, Vector3 pos)
        {
            if (!IsHub(piece))
            {
                return true;
            }

            string refuse = null;
            float nearest;
            HubRules.Verdict verdict = HubRegistry.CanBuild(__instance.GetPlayerID(), pos, out nearest);
            if (!ModSettings.HubsOn || !Unlocked(__instance))
            {
                refuse = Loc.T("stevethetroll_hub_msg_locked");
            }
            else if (verdict == HubRules.Verdict.TooMany)
            {
                refuse = Loc.T("stevethetroll_hub_msg_toomany", HubRules.MaxHubs.ToString());
            }
            else if (verdict == HubRules.Verdict.TooClose)
            {
                refuse = Loc.T("stevethetroll_hub_msg_tooclose", nearest.ToString("0"),
                    HubRules.MinSpacing.ToString("0"));
            }
            else if (Uneven(pos) > MaxUneven)
            {
                refuse = Loc.T("stevethetroll_hub_msg_uneven");
            }

            if (refuse == null)
            {
                return true;
            }

            __instance.Message(MessageHud.MessageType.Center, refuse);
            ModLog.Info("HubPlacement: refused - " + refuse);
            return false;
        }
    }
}
