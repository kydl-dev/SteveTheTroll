using System.Runtime.CompilerServices;
using HarmonyLib;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Costume.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony;
// MIGRATION_DECISIONS A5: Harmony patches live in Integrations/Harmony (moved here from Features/Costume/Infrastructure/CostumeItems.cs).

/// <summary>
///     D-077 (TEST-032: the player's body showed through the suit - calves, ribs): while a character wears ALL three
///     costume pieces
///     (head, suit, trousers) its own body mesh is hidden - the suit, mitts and plush feet / slippers cover everything.
///     Any piece off -> shown.
///     Works for every character with the mod (VisEquipment's current item hashes are synced by the game).
/// </summary>
[HarmonyPatch(typeof(VisEquipment), "UpdateEquipmentVisuals")]
internal static class CostumeBodyPatch
{
    private static readonly AccessTools.FieldRef<VisEquipment, int> Helmet =
        AccessTools.FieldRefAccess<VisEquipment, int>("m_currentHelmetItemHash");

    private static readonly AccessTools.FieldRef<VisEquipment, int> Chest =
        AccessTools.FieldRefAccess<VisEquipment, int>("m_currentChestItemHash");

    private static readonly AccessTools.FieldRef<VisEquipment, int> Legs =
        AccessTools.FieldRefAccess<VisEquipment, int>("m_currentLegItemHash");

    private static readonly ConditionalWeakTable<VisEquipment, object> Hidden = new();

    private static readonly int H = CostumeItems.HelmetName.GetStableHashCode(),
        C = CostumeItems.ChestName.GetStableHashCode(),
        L = CostumeItems.LegsName.GetStableHashCode();

    private static void Postfix(VisEquipment __instance)
    {
        SkinnedMeshRenderer body = __instance.m_bodyModel;
        if (body == null)
        {
            return;
        }

        bool full = ModSettings.On && Helmet(__instance) == H && Chest(__instance) == C && Legs(__instance) == L;
        object o;
        bool was = Hidden.TryGetValue(__instance, out o);
        // D-079 (TEST-033: the player froze in place with the suit on): a disabled body renderer let the Animator cull itself (no root
        // motion = no walking). The body stays enabled and only stops drawing (forceRenderingOff), and the Animator always animates
        // while the costume is on (its culling mode is put back after).
        Animator anim = __instance.GetComponentInChildren<Animator>();
        if (full)
        {
            if (!body.forceRenderingOff)
            {
                body.forceRenderingOff = true;
            }

            if (!body.enabled)
            {
                body.enabled = true;
            }

            if (__instance.GetComponent<CostumeBareFeet>() == null)
            {
                __instance.gameObject.AddComponent<CostumeBareFeet>(); // D-089: own feet in the slippers
            }

            if (!was)
            {
                Hidden.Add(__instance, anim != null ? anim.cullingMode : new object());
                if (anim != null)
                {
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                ModLog.Verbose("CostumeBodyPatch: " + __instance.name +
                               " wears the whole costume - own body hidden, animator " +
                               (anim != null ? "always animates" : "not found"));
            }
        }
        else if (was)
        {
            Hidden.Remove(__instance);
            body.forceRenderingOff = false;
            if (anim != null && o is AnimatorCullingMode)
            {
                anim.cullingMode = (AnimatorCullingMode)o;
            }

            ModLog.Verbose("CostumeBodyPatch: " + __instance.name +
                           " costume incomplete - own body shown again, animator culling " +
                           (anim != null ? anim.cullingMode.ToString() : "-"));
        }
    }
}
