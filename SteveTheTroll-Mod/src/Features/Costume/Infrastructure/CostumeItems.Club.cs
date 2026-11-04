using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using KydlCore.Common;
using SteveTheTroll.Bootstrap;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Slippers.Infrastructure;
using SteveTheTroll.Features.Trolls.Combat.Domain;
using SteveTheTroll.Integrations.Unity;
using UnityEngine;

namespace SteveTheTroll.Features.Costume.Infrastructure;

/// <summary>The Troll Club half of CostumeItems (split out so the class stays under the size limit).</summary>
internal static partial class CostumeItems
{
    private static bool AddClub()
    {
        GameObject part = Part(BundleAssets.ClubParts, "attach/model");
        if (part == null)
        {
            return false;
        }

        var cfg = new ItemConfig
        {
            Name = "$stevethetroll_club", Description = "$stevethetroll_club_desc",
            CraftingStation = CraftingStations.Workbench, MinStationLevel = 1
        };
        cfg.AddRequirement(new RequirementConfig("RoundLog", 15, 5)); // D-071: core wood
        cfg.AddRequirement(new RequirementConfig("FineWood", 15, 5));
        cfg.AddRequirement(new RequirementConfig("Thistle", 10));
        cfg.AddRequirement(new RequirementConfig("TrollHide", 2));
        Icon(cfg, BundleAssets.TrollClubIcon);
        var item = new CustomItem(ClubName, "SledgeIron", cfg);
        if (item.ItemPrefab == null || item.ItemDrop == null)
        {
            ModLog.Error("CostumeItems: could not clone SledgeIron for the club");
            return false;
        }

        Transform attach = item.ItemPrefab.transform.Find("attach");
        if (attach == null)
        {
            ModLog.Error("CostumeItems: SledgeIron has no 'attach' child - no club");
            return false;
        }

        // the sledge's own meshes go (trail / effects / colliders stay), ours comes in
        int hidden = 0;
        foreach (MeshRenderer mr in attach.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.GetComponent<ParticleSystem>() != null)
            {
                continue;
            }

            Object.DestroyImmediate(mr.GetComponent<MeshFilter>(), true);
            Object.DestroyImmediate(mr, true);
            hidden++;
        }

        GameObject model = Object.Instantiate(part, attach, false);
        model.name = "stt_club_model";
        model.AddComponent<ClubPose>();
        ItemDrop.ItemData.SharedData s = item.ItemDrop.m_itemData.m_shared;
        HitData.DamageTypes v = s.m_damages;
        s.m_damages = new HitData.DamageTypes { m_blunt = ClubBlunt, m_chop = ClubChop, m_pickaxe = ClubPickaxe };
        s.m_damagesPerLevel = new HitData.DamageTypes
            { m_blunt = ClubBluntPerLevel, m_chop = ClubChopPerLevel, m_pickaxe = ClubPickaxePerLevel };
        float knock = s.m_attackForce;
        s.m_attackForce = knock * ClubKnockbackFactor;
        s.m_maxQuality = MaxQuality;
        s.m_toolTier = Mathf.Max(s.m_toolTier, 2); // mines copper / tin / silver like an iron pickaxe
        float slow = s.m_movementModifier;
        s.m_movementModifier = 0f; // D-081 (user: 'no slowing down' - the sledge's -15 %)
        float dur = s.m_maxDurability;
        s.m_maxDurability = ClubDurability; // D-082 (user: 'club durability, set that to 500')
        string secondary = ClubSecondary(s);
        bool added = ItemManager.Instance.AddItem(item);
        SlipperUpgradeCostPatch.Track(ClubName, 4);
        ModLog.Verbose("CostumeItems: " + ClubName + " (from SledgeIron) added " + added + ": sledge meshes removed " +
                       hidden + ", damage blunt " + s.m_damages.m_blunt + " chop " + s.m_damages.m_chop
                       + " pickaxe " + s.m_damages.m_pickaxe + " (+" + ClubBluntPerLevel + "/" + ClubChopPerLevel +
                       "/" + ClubPickaxePerLevel + "), vanilla blunt " + v.m_blunt + ", knockback " + knock + " -> " +
                       s.m_attackForce
                       + ", secondary " + secondary + ", durability " + dur + " -> " + s.m_maxDurability +
                       ", movement " + (slow * 100f).ToString("0") + " % -> 0 %, stagger x" +
                       (s.m_attack != null ? s.m_attack.m_staggerMultiplier.ToString("F2") : "?") + ", tool tier " +
                       s.m_toolTier + ", skill " + s.m_skillType + ", two-handed " +
                       (s.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon));
        ModLog.Check("CostumeItems: club blunt at quality 4", s.m_damages.m_blunt + s.m_damagesPerLevel.m_blunt * 3,
            255f, 0.001, "");
        ModLog.Check("CostumeItems: club chop at quality 4", s.m_damages.m_chop + s.m_damagesPerLevel.m_chop * 3, 110f,
            0.001, "");
        ModLog.Check("CostumeItems: club pickaxe at quality 4",
            s.m_damages.m_pickaxe + s.m_damagesPerLevel.m_pickaxe * 3, 110f, 0.001, "");
        return added;
    }

    /// D-114: the club's secondary attack = a clone of the Battleaxe's primary (horizontal, wide arc), one swing (no chain), damage x
    /// ClubSecondaryDamage; the club's own hit / trail effects stay (blunt sounds, not the axe's). Expected outcome: a short description
    /// ("none" when the Battleaxe is missing - the club then keeps no secondary attack).
    private static string ClubSecondary(ItemDrop.ItemData.SharedData club)
    {
        GameObject axe = PrefabManager.Instance.GetPrefab(ClubSecondaryFrom);
        ItemDrop drop = axe != null ? axe.GetComponent<ItemDrop>() : null;
        Attack from = drop != null ? drop.m_itemData.m_shared.m_attack : null;
        if (from == null)
        {
            ModLog.Warn("CostumeItems: " + ClubSecondaryFrom + " not found - the Troll Club has no secondary attack");
            return "none";
        }

        Attack a = from.Clone();
        Attack own = club.m_attack;
        a.m_attackAnimation = ClubSecondaryAnimation;
        a.m_attackChainLevels = 1;
        a.m_damageMultiplier = ClubSecondaryDamage;
        if (own != null)
        {
            a.m_hitEffect = own.m_hitEffect;
            a.m_hitTerrainEffect = own.m_hitTerrainEffect;
            a.m_startEffect = own.m_startEffect;
            a.m_triggerEffect = own.m_triggerEffect;
            a.m_trailStartEffect = own.m_trailStartEffect;
        }

        club.m_secondaryAttack = a;
        string d = a.m_attackAnimation + " (" + a.m_attackType + ", range " + a.m_attackRange.ToString("F1") +
                   " m, angle " + a.m_attackAngle.ToString("F0") + " deg, damage x" + a.m_damageMultiplier
                   + ", stamina " + a.m_attackStamina.ToString("F0") + ", from " + ClubSecondaryFrom + "'s " +
                   from.m_attackAnimation + " x" + from.m_attackChainLevels + ")";
        ModLog.Check("CostumeItems: club secondary damage multiplier", a.m_damageMultiplier, ClubSecondaryDamage, 0.001,
            "x");
        return d;
    }
}
