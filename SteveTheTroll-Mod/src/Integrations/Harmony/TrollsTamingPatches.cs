using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony
{
    // MIGRATION_DECISIONS A5: every Harmony patch of the TrollsTaming feature, declared here; each one only forwards to the feature's own method
    // (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

    // TamedTrollSpeed.SwimUpdate (Features/Trolls/Taming/Infrastructure/TamedTrollSpeed.cs)
    [HarmonyPatch(typeof(Character), "UpdateSwimming")]
    static class TamedTrollSpeed_SwimUpdate
    {
        static void Prefix(Character __instance) { global::SteveTheTroll.Features.Trolls.Taming.Infrastructure.TamedTrollSpeed.SwimUpdate.Prefix(__instance); }
    }

    // TamedTrollSpeed.Free (Features/Trolls/Taming/Infrastructure/TamedTrollSpeed.cs)
    [HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.CustomFixedUpdate))]
    static class TamedTrollSpeed_Free
    {
        static void Postfix(CharacterAnimEvent __instance) { global::SteveTheTroll.Features.Trolls.Taming.Infrastructure.TamedTrollSpeed.Free.Postfix(__instance); }
    }

    // TamedTrollSpeed.AttackClip (Features/Trolls/Taming/Infrastructure/TamedTrollSpeed.cs)
    [HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.Speed))]
    static class TamedTrollSpeed_AttackClip
    {
        static void Postfix(CharacterAnimEvent __instance, float speedScale) { global::SteveTheTroll.Features.Trolls.Taming.Infrastructure.TamedTrollSpeed.AttackClip.Postfix(__instance, speedScale); }
    }

    // TamedTrollStats.MaxHealthBase (Features/Trolls/Taming/Infrastructure/TamedTrollStats.cs)
    [HarmonyPatch(typeof(Character), nameof(Character.GetMaxHealthBase))]
    static class TamedTrollStats_MaxHealthBase
    {
        static void Postfix(Character __instance, ref float __result) { global::SteveTheTroll.Features.Trolls.Taming.Infrastructure.TamedTrollStats.MaxHealthBase.Postfix(__instance, ref __result); }
    }

    // TamedTrollStats.NoLava (Features/Trolls/Taming/Infrastructure/TamedTrollStats.cs)
    [HarmonyPatch(typeof(Character), "UpdateHeatDamage")]
    static class TamedTrollStats_NoLava
    {
        static bool Prefix(Character __instance) { return global::SteveTheTroll.Features.Trolls.Taming.Infrastructure.TamedTrollStats.NoLava.Prefix(__instance); }
    }

    // TamedTrollStats.HitLog (Features/Trolls/Taming/Infrastructure/TamedTrollStats.cs)
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    static class TamedTrollStats_HitLog
    {
        static void Postfix(Character __instance, HitData hit) { global::SteveTheTroll.Features.Trolls.Taming.Infrastructure.TamedTrollStats.HitLog.Postfix(__instance, hit); }
    }

    // TamedTrollStats.DamageFactor (Features/Trolls/Taming/Infrastructure/TamedTrollStats.cs)
    [HarmonyPatch(typeof(Attack), "GetLevelDamageFactor")]
    static class TamedTrollStats_DamageFactor
    {
        static void Postfix(Attack __instance, ref float __result) { global::SteveTheTroll.Features.Trolls.Taming.Infrastructure.TamedTrollStats.DamageFactor.Postfix(__instance, ref __result); }
    }

    // TamedTrollStats.Resistances (Features/Trolls/Taming/Infrastructure/TamedTrollStats.cs)
    [HarmonyPatch(typeof(Character), nameof(Character.GetDamageModifiers))]
    static class TamedTrollStats_Resistances
    {
        static void Postfix(Character __instance, ref HitData.DamageModifiers __result) { global::SteveTheTroll.Features.Trolls.Taming.Infrastructure.TamedTrollStats.Resistances.Postfix(__instance, ref __result); }
    }

    // TamedTrollStats.NoStagger (Features/Trolls/Taming/Infrastructure/TamedTrollStats.cs)
    [HarmonyPatch(typeof(Character), "AddStaggerDamage")]
    static class TamedTrollStats_NoStagger
    {
        static bool Prefix(Character __instance, float damage, ref bool __result) { return global::SteveTheTroll.Features.Trolls.Taming.Infrastructure.TamedTrollStats.NoStagger.Prefix(__instance, damage, ref __result); }
    }

    // TrollHover (Features/Trolls/Taming/Infrastructure/TrollHover.cs)
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
    static class TrollHoverHook
    {
        static void Postfix(Tameable __instance, ref string __result) { global::SteveTheTroll.Features.Trolls.Taming.Infrastructure.TrollHover.Postfix(__instance, ref __result); }
    }

    // TrollTamingPatch.TamingUpdate (Features/Trolls/Taming/Infrastructure/TrollTamingPatch.cs)
    [HarmonyPatch(typeof(Tameable), "TamingUpdate")]
    static class TrollTamingPatch_TamingUpdate
    {
        static bool Prefix(Tameable __instance) { return global::SteveTheTroll.Features.Trolls.Taming.Infrastructure.TrollTamingPatch.TamingUpdate.Prefix(__instance); }
    }

    // TrollTamingPatch.StatusString (Features/Trolls/Taming/Infrastructure/TrollTamingPatch.cs)
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetStatusString))]
    static class TrollTamingPatch_StatusString
    {
        static void Postfix(Tameable __instance, ref string __result) { global::SteveTheTroll.Features.Trolls.Taming.Infrastructure.TrollTamingPatch.StatusString.Postfix(__instance, ref __result); }
    }
}
