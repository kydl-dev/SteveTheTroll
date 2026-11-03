using HarmonyLib;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;

namespace SteveTheTroll.Integrations.Harmony;
// MIGRATION_DECISIONS A5: every Harmony patch of the TrollsTaming feature, declared here; each one only forwards to the feature's own method
// (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

// TamedTrollSpeed.SwimUpdate (Features/Trolls/Taming/Infrastructure/TamedTrollSpeed.cs)
[HarmonyPatch(typeof(Character), "UpdateSwimming")]
internal static class TamedTrollSpeed_SwimUpdate
{
    private static void Prefix(Character __instance)
    {
        TamedTrollSpeed.SwimUpdate.Prefix(__instance);
    }
}

// TamedTrollSpeed.Free (Features/Trolls/Taming/Infrastructure/TamedTrollSpeed.cs)
[HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.CustomFixedUpdate))]
internal static class TamedTrollSpeed_Free
{
    private static void Postfix(CharacterAnimEvent __instance)
    {
        TamedTrollSpeed.Free.Postfix(__instance);
    }
}

// TamedTrollSpeed.AttackClip (Features/Trolls/Taming/Infrastructure/TamedTrollSpeed.cs)
[HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.Speed))]
internal static class TamedTrollSpeed_AttackClip
{
    private static void Postfix(CharacterAnimEvent __instance, float speedScale)
    {
        TamedTrollSpeed.AttackClip.Postfix(__instance, speedScale);
    }
}

// TamedTrollStats.MaxHealthBase (Features/Trolls/Taming/Infrastructure/TamedTrollStats.cs)
[HarmonyPatch(typeof(Character), nameof(Character.GetMaxHealthBase))]
internal static class TamedTrollStats_MaxHealthBase
{
    private static void Postfix(Character __instance, ref float __result)
    {
        TamedTrollStats.MaxHealthBase.Postfix(__instance, ref __result);
    }
}

// TamedTrollStats.NoLava (Features/Trolls/Taming/Infrastructure/TamedTrollStats.cs)
[HarmonyPatch(typeof(Character), "UpdateHeatDamage")]
internal static class TamedTrollStats_NoLava
{
    private static bool Prefix(Character __instance)
    {
        return TamedTrollStats.NoLava.Prefix(__instance);
    }
}

// TamedTrollStats.HitLog (Features/Trolls/Taming/Infrastructure/TamedTrollStats.cs)
[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
internal static class TamedTrollStats_HitLog
{
    private static void Postfix(Character __instance, HitData hit)
    {
        TamedTrollStats.HitLog.Postfix(__instance, hit);
    }
}

// TamedTrollStats.DamageFactor (Features/Trolls/Taming/Infrastructure/TamedTrollStats.cs)
[HarmonyPatch(typeof(Attack), "GetLevelDamageFactor")]
internal static class TamedTrollStats_DamageFactor
{
    private static void Postfix(Attack __instance, ref float __result)
    {
        TamedTrollStats.DamageFactor.Postfix(__instance, ref __result);
    }
}

// TamedTrollStats.Resistances (Features/Trolls/Taming/Infrastructure/TamedTrollStats.cs)
[HarmonyPatch(typeof(Character), nameof(Character.GetDamageModifiers))]
internal static class TamedTrollStats_Resistances
{
    private static void Postfix(Character __instance, ref HitData.DamageModifiers __result)
    {
        TamedTrollStats.Resistances.Postfix(__instance, ref __result);
    }
}

// TamedTrollStats.NoStagger (Features/Trolls/Taming/Infrastructure/TamedTrollStats.cs)
[HarmonyPatch(typeof(Character), "AddStaggerDamage")]
internal static class TamedTrollStats_NoStagger
{
    private static bool Prefix(Character __instance, float damage, ref bool __result)
    {
        return TamedTrollStats.NoStagger.Prefix(__instance, damage, ref __result);
    }
}

// TrollHover (Features/Trolls/Taming/Infrastructure/TrollHover.cs)
[HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
internal static class TrollHoverHook
{
    private static void Postfix(Tameable __instance, ref string __result)
    {
        TrollHover.Postfix(__instance, ref __result);
    }
}

// TrollTamingPatch.TamingUpdate (Features/Trolls/Taming/Infrastructure/TrollTamingPatch.cs)
[HarmonyPatch(typeof(Tameable), "TamingUpdate")]
internal static class TrollTamingPatch_TamingUpdate
{
    private static bool Prefix(Tameable __instance)
    {
        return TrollTamingPatch.TamingUpdate.Prefix(__instance);
    }
}

// TrollTamingPatch.StatusString (Features/Trolls/Taming/Infrastructure/TrollTamingPatch.cs)
[HarmonyPatch(typeof(Tameable), nameof(Tameable.GetStatusString))]
internal static class TrollTamingPatch_StatusString
{
    private static void Postfix(Tameable __instance, ref string __result)
    {
        TrollTamingPatch.StatusString.Postfix(__instance, ref __result);
    }
}
