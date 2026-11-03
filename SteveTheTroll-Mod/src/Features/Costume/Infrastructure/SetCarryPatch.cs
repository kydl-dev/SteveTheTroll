using KydlCore.Common;
using SteveTheTroll.Configuration;
using UnityEngine;

namespace SteveTheTroll.Features.Costume.Infrastructure;

/// <summary>
///     D-112 (user 2026-10-28: "the set gives 25% increased carry weight ... a value and not a set value; with megingjord
///     the total ... would
///     increase by more than 75kg"): while the set effect "Steve The Troll" is on, the player's max carry weight is the
///     game's total (base
///     300 + Megingjord + any other effect) x (1 + SetCarryPercent). Hook (1.0.21): Player.GetMaxCarryWeight (status
///     effects already applied).
/// </summary>
// Harmony hook: src/Integrations/Harmony/CostumePatches.cs, SetCarryPatchHook (MIGRATION_DECISIONS A5)
internal static class SetCarryPatch
{
    private static float _lastLogged = -1f;

    /// Pure: the carry limit with the set. Expected outcome: limit x (1 + percent); a percent of 0 or less leaves it as it is.
    internal static float WithSet(float limit, float percent)
    {
        return percent > 0f ? limit * (1f + percent) : limit;
    }

    internal static void Postfix(Player __instance, ref float __result)
    {
        int hash = CostumeItems.SetEffectHash;
        if (__instance == null || hash == 0 || !ModSettings.On)
        {
            return;
        }

        SEMan se = __instance.GetSEMan();
        if (se == null || !se.HaveStatusEffect(hash))
        {
            return;
        }

        float before = __result;
        __result = WithSet(before, CostumeItems.SetCarryPercent);
        if (!Mathf.Approximately(before, _lastLogged))
        {
            _lastLogged = before;
            ModLog.Verbose("SetCarryPatch: " + __instance.GetPlayerName() + " max carry " + before.ToString("F0") +
                           " -> " + __result.ToString("F0") + " (set +" +
                           (CostumeItems.SetCarryPercent * 100f).ToString("0") + " %)");
            ModLog.Check("SetCarryPatch: carry with the set", __result, before * (1f + CostumeItems.SetCarryPercent),
                0.01, "kg");
        }
    }
}
