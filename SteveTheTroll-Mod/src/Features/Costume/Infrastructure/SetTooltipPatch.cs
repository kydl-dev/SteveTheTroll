using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Features.Slippers.Infrastructure;

namespace SteveTheTroll.Features.Costume.Infrastructure
{
    /// <summary>
    /// D-091 (user: "could we not just say: stamina usage: -50%? there is a lot of text and font-size goes really small"): the set effect's
    /// tooltip shows ONE stamina line instead of the game's eight (run / jump / attack / block / dodge / swim / sneak / base item).
    /// </summary>
    // Harmony hook: src/Integrations/Harmony/CostumePatches.cs, SetTooltipPatchHook (MIGRATION_DECISIONS A5)
    internal static class SetTooltipPatch
    {
        internal const string StaminaLabel = "$stevethetroll_se_stamina", CarryLabel = "$stevethetroll_se_carry";

        internal static void Postfix(SE_Stats __instance, ref string __result)
        {
            if (__instance == null || !__instance.name.StartsWith(CostumeItems.SetEffectName, System.StringComparison.Ordinal)) return;   // also the active copy "(Clone)"
            __result = Compact(__result, StaminaLabel, CostumeItems.SetStamina);
            __result = WithPercent(__result, CarryLabel, CostumeItems.SetCarryPercent);   // D-112: not an SE_Stats field, so the game shows no line for it
        }

        /// Pure: one more "label: +25%" line at the end (in the game's orange). Expected outcome: the tooltip with that line.
        internal static string WithPercent(string tooltip, string label, float fraction)
        {
            string line = label + ": <color=orange>" + (fraction > 0f ? "+" : "") + (fraction * 100f).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%</color>";
            return string.IsNullOrEmpty(tooltip) ? line : tooltip.TrimEnd('\n') + "\n" + line;
        }

        /// 1.0.20 SE_Stats lines: $se_runstamina, $se_jumpstamina, ..., $se_sneakstamina and "$base_item_modifier" (the base item stamina line).
        internal static bool IsStaminaLine(string line)
        {
            return line != null && (line.IndexOf("stamina", System.StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("$base_item_modifier", System.StringComparison.Ordinal) >= 0);
        }

        /// Pure: every line that mentions stamina (game token or English text) is dropped and one "label: -50%" line takes the place of the
        /// first one; text without a stamina line stays as it is. Expected outcome: the shorter tooltip.
        internal static string Compact(string tooltip, string label, float modifier)
        {
            if (string.IsNullOrEmpty(tooltip)) return tooltip ?? "";
            var lines = tooltip.Split('\n');
            var kept = new List<string>(lines.Length);
            bool added = false;
            foreach (var l in lines)
            {
                if (!IsStaminaLine(l)) { kept.Add(l); continue; }
                if (added) continue;
                added = true;
                kept.Add(label + ": <color=orange>" + (modifier > 0f ? "+" : "") + (modifier * 100f).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%</color>");
            }
            return string.Join("\n", kept.ToArray());
        }
    }

    /// <summary>
    /// D-092 (user: "not show the Steve The Troll set bonus ... in the slippers crafting page ... keeping stuff secret"): the slippers'
    /// tooltip (crafting page, inventory) leaves the set out until the character has crafted the whole costume (D-088 CostumeComplete).
    /// </summary>
    // Harmony hook: src/Integrations/Harmony/CostumePatches.cs, SlipperSetSecretPatchHook (MIGRATION_DECISIONS A5)
    internal static class SlipperSetSecretPatch
    {
        static readonly HashSet<string> Logged = new HashSet<string>();

        internal static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(ItemDrop.ItemData)))
            {
                if (m.Name != "GetTooltip" || !m.IsStatic) continue;
                var ps = m.GetParameters();
                if (ps.Length > 0 && ps[0].ParameterType == typeof(ItemDrop.ItemData)) yield return m;
            }
        }

        internal static void Prefix(ItemDrop.ItemData item, out StatusEffect __state)
        {
            __state = null;
            if (item == null || item.m_shared == null || item.m_shared != SlipperItem.Shared || item.m_shared.m_setStatusEffect == null) return;
            if (RecipeUnlockPatch.CostumeComplete(Player.m_localPlayer, ObjectDB.instance)) return;
            __state = item.m_shared.m_setStatusEffect;
            item.m_shared.m_setStatusEffect = null;
            if (Logged.Add("hidden")) ModLog.Verbose("SlipperSetSecretPatch: slippers tooltip without the set (costume not crafted yet)");
        }

        internal static void Finalizer(ItemDrop.ItemData item, StatusEffect __state)
        {
            if (__state != null && item != null && item.m_shared != null) item.m_shared.m_setStatusEffect = __state;
        }
    }
}
