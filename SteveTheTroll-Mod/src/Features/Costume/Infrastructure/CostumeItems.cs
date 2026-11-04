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

/// <summary>
///     T-009d (D-069 .. D-072, D-075): the troll mascot costume (helmet / chest / legs) and the Troll Club, cloned from
///     vanilla items so
///     equipping, animations and networking stay the game's own; their worn look is replaced by our parts from the bundle
///     (Unity menu SteveTheTroll > Costume > Build costume parts, D-074):
///     - helmet
///     <- HelmetTrollLeather: child "attach" = CostumeHelmetParts/ attach ( rigid, on Helmet_attach), hair and beard
///         hidden
///         - chest
///     <- ArmorTrollLeatherChest: child "attach_skin" = CostumeChestParts/ attach_skin ( skinned, the game swaps in the
///         wearer's bones)
///  - legs
///     <- ArmorTrollLeatherLegs: child "attach_skin" = CostumeLegsParts/ attach_skin (+ plush feet, hidden while the
///         slippers are worn)
///         - club
///     <- SledgeIron ( two-handed slam, knockback, stagger) : the meshes under "attach" swapped for ClubParts/ attach/
///         model
///         Ground look ( D-080) : the helmet shows our head, chest / legs our laid-down suit / trousers, the club our
///         trunk; our icons ( scripts/ costume_icon.py). Armor pieces: 20 armor +5 per quality, max quality 4, recipe as
///         the slippers
///         (5 TrollHide, 4 DeerHide, 6 Blueberries, workbench 1, every upgrade 2 TrollHide + 2 Blueberries). Club: 70
///         blunt + 30 chop + 30 pickaxe,
///         +10 / +5 / +5 per quality, max quality 4, knockback x1.5, workbench 1, 15 RoundLog + 15 FineWood + 10 Thistle + 2
///         TrollHide, every
///         upgrade 5 RoundLog + 5 FineWood. The vanilla troll-leather set bonus is replaced by ours ( D-076,
///         set 'Steve The Troll' with the slippers).
/// </summary>
internal static class CostumeItems
{
    public const string HelmetName = "STT_CostumeHelmet",
        ChestName = "STT_CostumeChest",
        LegsName = "STT_CostumeLegs",
        ClubName = "STT_TrollClub";

    public const float Armor = 20f, ArmorPerLevel = 5f;

    public const int MaxQuality = 4;

    // D-113 (user 2026-10-28): 150 blunt + 50 chop + 50 pickaxe, +35 / +20 / +20 per upgrade -> 255 / 110 / 110 at quality 4
    public const float ClubBlunt = 150f,
        ClubChop = 50f,
        ClubPickaxe = 50f,
        ClubBluntPerLevel = 35f,
        ClubChopPerLevel = 20f,
        ClubPickaxePerLevel = 20f;

    public const float ClubKnockbackFactor = 1.5f;

    /// D-114 (user 2026-10-28: "2nd attack ... similar to the swipe attack of the troll, does 2x dmg"; chose: reuse the battleaxe swing):
    /// the secondary attack is the iron Battleaxe's wide horizontal swing (one swing, first clip of its chain) at ClubSecondaryDamage x damage.
    public const string ClubSecondaryFrom = "Battleaxe", ClubSecondaryAnimation = "battleaxe_attack0";

    public const float ClubSecondaryDamage = 2f;
    public const float ClubDurability = 500f;

    /// D-076: the set "Steve The Troll" = helmet + chest + legs + Fluffy Slippers (the game counts the utility slot in a set).
    public const string SetName = "stevethetroll_set";

    public const int SetSize = 4;
    public const string SetEffectName = "SE_STT_SteveTheTroll";
    public const float SetSpeed = 0.5f; // run 50 % faster

    public const float
        SetCarryPercent = 0.25f; // D-112: carry weight x 1.25 of the TOTAL (Megingjord included; SetCarryPatch)

    public const int SetTrollLimit = 5;

    public const float
        SetStaggerResistance =
            BreakRules.SetStaggerResistance; // D-125: 75 % less stagger (SE_Stats.m_staggerModifier -0.75)

    public const float
        SetStamina = -0.5f; // D-077: -50 % stamina use (run, jump, attack, block, dodge, swim, sneak, tools)

    private static int _helmetHash, _chestHash, _clubHash;
    private static StatusEffect _set;

    /// The set effect's name hash (0 until added); the taming limit reads it (TrollTaming.TrollLimit).
    public static int SetEffectHash => _set != null ? _set.NameHash() : 0;

    public static int LegsHash { get; private set; }

    /// Expected outcome: the items are queued for when the vanilla prefabs exist (returns true once subscribed).
    public static bool Install()
    {
        PrefabManager.OnVanillaPrefabsAvailable += Add;
        ModLog.Verbose(
            "CostumeItems: waiting for vanilla prefabs (HelmetTrollLeather, ArmorTrollLeatherChest, ArmorTrollLeatherLegs, SledgeIron)");
        return true;
    }

    private static void Add()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= Add;
        if (Plugin.Bundle == null)
        {
            ModLog.Error("CostumeItems: no asset bundle - no costume items");
            return;
        }

        _set = SetEffect();
        int ok = 0;
        if (AddArmor(HelmetName, "HelmetTrollLeather", "stevethetroll_costume_helmet", BundleAssets.CostumeHelmetParts,
                "attach", ItemDrop.ItemData.ItemType.Helmet))
        {
            ok++;
        }

        if (AddArmor(ChestName, "ArmorTrollLeatherChest", "stevethetroll_costume_chest", BundleAssets.CostumeChestParts,
                "attach_skin", ItemDrop.ItemData.ItemType.Chest))
        {
            ok++;
        }

        if (AddArmor(LegsName, "ArmorTrollLeatherLegs", "stevethetroll_costume_legs", BundleAssets.CostumeLegsParts,
                "attach_skin", ItemDrop.ItemData.ItemType.Legs))
        {
            ok++;
        }

        if (AddClub())
        {
            ok++;
        }

        _helmetHash = HelmetName.GetStableHashCode();
        _chestHash = ChestName.GetStableHashCode();
        LegsHash = LegsName.GetStableHashCode();
        _clubHash = ClubName.GetStableHashCode();
        if (SlipperItem.Shared != null)
        {
            SlipperItem.Shared.m_setName = SetName;
            SlipperItem.Shared.m_setSize = SetSize;
            SlipperItem.Shared.m_setStatusEffect = _set;
        }
        else
        {
            ModLog.Warn("CostumeItems: slippers not added yet - they are not part of the set");
        }

        ModLog.Info("CostumeItems: " + ok + "/4 items added (helmet, chest, legs, club), set '" + SetName + "' of " +
                    SetSize + " with " + SetEffectName + " (slippers in the set " + (SlipperItem.Shared != null) + ")");
        ModLog.Check("CostumeItems: items added", ok, 4, 0, "");
    }

    private static string IconOf(ItemDrop.ItemData.ItemType type)
    {
        return type == ItemDrop.ItemData.ItemType.Helmet ? BundleAssets.CostumeHelmetIcon :
            type == ItemDrop.ItemData.ItemType.Chest ? BundleAssets.CostumeChestIcon : BundleAssets.CostumeLegsIcon;
    }

    /// Our icon from the bundle (rendered by scripts/costume_icon.py); the vanilla icon stays when it is missing.
    private static void Icon(ItemConfig cfg, string name)
    {
        Sprite icon = BundleAssets.Load<Sprite>(name);
        if (icon != null)
        {
            cfg.Icons = new[] { icon };
        }
        else
        {
            ModLog.Warn("CostumeItems: icon '" + name + "' not in the bundle - the vanilla icon stays");
        }
    }

    /// D-076: "Steve The Troll" - the set effect (all 3 costume pieces + the Fluffy Slippers): run 50 % faster, carry 25 % more (D-112, SetCarryPatch),
    /// up to SetTrollLimit tamed trolls (TrollTaming.TrollLimit). Registered with Jotunn; shown with its own icon.
    private static StatusEffect SetEffect()
    {
        SE_Stats se = ScriptableObject.CreateInstance<SE_Stats>();
        se.name = SetEffectName;
        se.m_name = "$stevethetroll_se_set";
        se.m_tooltip = "$stevethetroll_se_set_tooltip";
        se.m_icon = BundleAssets.Load<Sprite>(BundleAssets.SetIcon);
        se.m_speedModifier = SetSpeed;
        se.m_addMaxCarryWeight = 0f; // D-112: a percentage of the total instead (SetCarryPatch)
        // D-077 (user: "-50% stamina usage"): every stamina use of the player halves
        se.m_runStaminaDrainModifier = SetStamina;
        se.m_jumpStaminaUseModifier = SetStamina;
        se.m_attackStaminaUseModifier = SetStamina;
        se.m_blockStaminaUseModifier = SetStamina;
        se.m_dodgeStaminaUseModifier = SetStamina;
        se.m_swimStaminaUseModifier = SetStamina;
        se.m_sneakStaminaUseModifier = SetStamina;
        se.m_homeItemStaminaUseModifier = SetStamina;
        se.m_staggerModifier =
            -SetStaggerResistance; // D-125 (user: "stagger resistance 75%"): the game adds damage x modifier (SEMan.ModifyStagger) and shows the tooltip line
        bool added = ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, false));
        ModLog.Verbose("CostumeItems: set effect " + SetEffectName + " added " + added + " (icon " +
                       (se.m_icon != null) + ", speed +" + SetSpeed * 100f + " %, carry +" + SetCarryPercent * 100f +
                       " %, stamina use " + SetStamina * 100f + " %, trolls " + SetTrollLimit + ", stagger " +
                       (se.m_staggerModifier * 100f).ToString("0") + " %)");
        ModLog.Check("CostumeItems: set stagger modifier", se.m_staggerModifier, -0.75f, 0.0001, "");
        ModLog.Check("CostumeItems: set effect registered", added ? 1 : 0, 1, 0, "");
        return se;
    }

    private static GameObject Part(string prefab, string child)
    {
        GameObject p = BundleAssets.Load<GameObject>(prefab);
        Transform c = p != null ? p.transform.Find(child) : null;
        if (c == null)
        {
            ModLog.Error("CostumeItems: bundle part " + prefab + "/" + child + " missing (bundle has prefab " +
                         (p != null) + ")");
        }

        return c != null ? c.gameObject : null;
    }

    private static bool AddArmor(string name, string basePrefab, string token, string partsPrefab, string partChild,
        ItemDrop.ItemData.ItemType type)
    {
        GameObject part = Part(partsPrefab, partChild);
        if (part == null)
        {
            return false;
        }

        var cfg = new ItemConfig
        {
            Name = "$" + token, Description = "$" + token + "_desc", CraftingStation = CraftingStations.Workbench,
            MinStationLevel = 1
        };
        // D-070: the slipper recipe (SlipperItem) for every piece
        cfg.AddRequirement(
            new RequirementConfig("TrollHide", SlipperItem.TrollHideCost, SlipperItem.UpgradeCostPerItem));
        cfg.AddRequirement(new RequirementConfig("DeerHide", 4));
        cfg.AddRequirement(new RequirementConfig("Blueberries", 6, SlipperItem.UpgradeCostPerItem, false));
        Icon(cfg, IconOf(type));
        var item = new CustomItem(name, basePrefab, cfg);
        if (item.ItemPrefab == null || item.ItemDrop == null)
        {
            ModLog.Error("CostumeItems: could not clone " + basePrefab + " for " + name);
            return false;
        }

        int removed = StripWorn(item.ItemPrefab);
        GameObject mine = Object.Instantiate(part, item.ItemPrefab.transform, false);
        mine.name = partChild;
        // as vanilla: attach_skin is inactive in the item (VisEquipment activates its copy), a helmet's "attach" is active and is
        // also the ground look (HelmetBronze) - so the helmet's vanilla ground model goes
        mine.SetActive(partChild != "attach_skin");
        // D-080: the vanilla ground model goes for every piece; chest / legs get our laid-down suit / trousers (bundle CostumeChestGround /
        // CostumeLegsGround), the helmet shows its 'attach' head; one box collider fitted to our ground look (so it rests on the ground)
        int groundRemoved = 0;
        foreach (Renderer r in item.ItemPrefab.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.transform.IsChildOf(mine.transform))
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                Object.DestroyImmediate(r, true);
                if (mf != null)
                {
                    Object.DestroyImmediate(mf, true);
                }

                groundRemoved++;
            }
        }

        GameObject ground = mine;
        string groundName = type == ItemDrop.ItemData.ItemType.Chest ? BundleAssets.CostumeChestGround :
            type == ItemDrop.ItemData.ItemType.Legs ? BundleAssets.CostumeLegsGround : null;
        if (groundName != null)
        {
            GameObject gp = BundleAssets.Load<GameObject>(groundName);
            if (gp != null)
            {
                ground = Object.Instantiate(gp, item.ItemPrefab.transform, false);
                ground.name = "stt_ground";
            }
            else
            {
                ModLog.Warn("CostumeItems: ground model '" + groundName + "' not in the bundle - " + name +
                            " has no ground look");
            }
        }

        string collider = FitCollider(item.ItemPrefab, ground);
        if (type == ItemDrop.ItemData.ItemType.Legs)
        {
            Transform feet = mine.transform.Find("CostumeFeet");
            if (feet != null)
            {
                feet.gameObject.AddComponent<CostumeFeet>();
            }

            ModLog.Check("CostumeItems: legs have the plush feet", feet != null ? 1 : 0, 1, 0, "");
        }

        ItemDrop.ItemData.SharedData s = item.ItemDrop.m_itemData.m_shared;
        s.m_itemType = type;
        s.m_armor = Armor;
        s.m_armorPerLevel = ArmorPerLevel;
        s.m_maxQuality = MaxQuality;
        s.m_setName = SetName;
        s.m_setSize = SetSize;
        s.m_setStatusEffect = _set;
        s.m_equipStatusEffect = null;
        s.m_movementModifier = 0f;
        s.m_damageModifiers = new List<HitData.DamageModPair>();
        if (type == ItemDrop.ItemData.ItemType.Helmet)
        {
            s.m_helmetHideHair = ItemDrop.ItemData.HelmetHairType.Hidden;
            s.m_helmetHideBeard = ItemDrop.ItemData.HelmetHairType.Hidden;
        }

        bool added = ItemManager.Instance.AddItem(item);
        SlipperUpgradeCostPatch.Track(name, 3);
        int renderers = mine.GetComponentsInChildren<Renderer>(true).Length;
        ModLog.Verbose("CostumeItems: " + name + " (" + type + ", from " + basePrefab + ") added " + added +
                       ": vanilla worn parts removed " + removed + ", vanilla ground renderers removed " +
                       groundRemoved + ", ground look " + (ground != null ? ground.name : "none") + ", " + collider +
                       ", ours '" + partChild
                       + "' with " + renderers + " renderer(s), armor " + s.m_armor + " +" + s.m_armorPerLevel +
                       "/quality, max quality " + s.m_maxQuality
                       + (type == ItemDrop.ItemData.ItemType.Helmet
                           ? ", hair " + s.m_helmetHideHair + ", beard " + s.m_helmetHideBeard
                           : ""));
        ModLog.Check("CostumeItems: " + name + " armor at quality 4", s.m_armor + s.m_armorPerLevel * 3, 35f, 0.001,
            "");
        return added;
    }

    /// D-080: replaces the item's colliders with one box around our ground look (mesh bounds in the item's space; the prefab is not
    /// instantiated, so renderer bounds are empty). Expected outcome: a short description of the new box ("no collider" when no mesh).
    private static string FitCollider(GameObject item, GameObject look)
    {
        if (look == null)
        {
            return "no collider (no ground look)";
        }

        Transform root = item.transform;
        bool any = false;
        var b = new Bounds();
        foreach (MeshFilter mf in look.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null)
            {
                continue;
            }

            Matrix4x4 m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(c);
                if (!any)
                {
                    b = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    b.Encapsulate(p);
                }
            }
        }

        if (!any)
        {
            return "no collider (no mesh)";
        }

        int removed = 0;
        foreach (Collider c in item.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(c, true);
            removed++;
        }

        BoxCollider box = item.AddComponent<BoxCollider>();
        box.center = b.center;
        box.size = Vector3.Max(b.size, Vector3.one * 0.1f);
        return "box collider " + box.size.ToString("F2") + " m at " + box.center.ToString("F2") +
               " (vanilla colliders removed " + removed + ")";
    }

    /// Removes the vanilla worn visual: children named attach / attach_* (the ground model stays).
    private static int StripWorn(GameObject prefab)
    {
        var doomed = new List<GameObject>();
        foreach (Transform c in prefab.transform)
        {
            if (c.name == "attach" || c.name.StartsWith("attach_"))
            {
                doomed.Add(c.gameObject);
            }
        }

        foreach (GameObject go in doomed)
        {
            ModLog.Verbose("CostumeItems: " + prefab.name + " removing vanilla part " + go.name);
            Object.DestroyImmediate(go, true);
        }

        return doomed.Count;
    }

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

/// <summary>
///     D-074: the costume legs' plush feet show only while the wearer has NOT the Fluffy Slippers on (the slippers are the
///     feet then).
///     Sits on the "CostumeFeet" renderer inside the legs' attach_skin copy that VisEquipment puts under the character.
/// </summary>
internal sealed class CostumeFeet : MonoBehaviour
{
    private int _logged = -1;
    private Renderer _r;
    private VisEquipment _vis;

    private void Awake()
    {
        _r = GetComponent<Renderer>();
    }

    private void LateUpdate()
    {
        if (_r == null)
        {
            return;
        }

        if (_vis == null)
        {
            _vis = GetComponentInParent<VisEquipment>();
        }

        SlipperWearer w = null;
        bool slippers = _vis != null && _vis.TryGetComponent(out w) && w.On;
        if (_r.enabled == !slippers)
        {
            return;
        }

        _r.enabled = !slippers;
        int state = slippers ? 1 : 0;
        if (_logged != state)
        {
            _logged = state;
            ModLog.Verbose("CostumeFeet: " + (_vis != null ? _vis.name : "?") + " plush feet " +
                           (slippers ? "hidden (slippers worn)" : "shown"));
        }
    }
}

/// <summary>
///     D-077: the Troll Club's pose in the hand from the Club Tunables (F7, live): tilt outward / up, twist, slide. The
///     rest pose is the
///     built model's (ClubParts: shaft along +Z of the sledge's attach). Axes: in the player's rest pose the hand's local
///     (-0.77, 0.64, 0)
///     points up and (0.64, 0.77, 0) to the player's right (measured on the reference player in Unity).
/// </summary>
internal sealed class ClubPose : MonoBehaviour
{
    private static readonly Vector3
        UpAxis = new Vector3(-0.77f, 0.64f, 0f).normalized; // rotating about it moves the tip to the right

    private static readonly Vector3
        SideAxis = -new Vector3(0.64f, 0.77f, 0f).normalized; // rotating about it lifts the tip

    private float _o = float.NaN, _u, _t, _s;
    private Quaternion _rest;
    private Vector3 _restPos;

    private void Awake()
    {
        _rest = transform.localRotation;
        _restPos = transform.localPosition;
    }

    private void LateUpdate()
    {
        float o = ModSettings.ClubOutward.Value,
            u = ModSettings.ClubUp.Value,
            t = ModSettings.ClubTwist.Value,
            sl = ModSettings.ClubSlide.Value;
        if (Mathf.Approximately(o, _o) && Mathf.Approximately(u, _u) && Mathf.Approximately(t, _t) && Mathf.Approximately(sl, _s))
        {
            return;
        }

        _o = o;
        _u = u;
        _t = t;
        _s = sl;
        Quaternion turn = Quaternion.AngleAxis(o, UpAxis) * Quaternion.AngleAxis(u, SideAxis);
        transform.localRotation = turn * _rest * Quaternion.AngleAxis(t, Vector3.up); // the model's own +Y is the trunk
        transform.localPosition = _restPos + turn * Vector3.forward * sl;
        ModLog.Verbose("ClubPose: " + name + " outward " + o.ToString("F0") + ", up " + u.ToString("F0") + ", twist " +
                       t.ToString("F0") + " deg, slide " + sl.ToString("F2") + " m");
    }
}
