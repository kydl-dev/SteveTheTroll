using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Kydl.Common;
using SteveTheTroll.Bootstrap;
using SteveTheTroll.Configuration;
using SteveTheTroll.Integrations.Unity;
using UnityEngine;

namespace SteveTheTroll.Features.Slippers.Infrastructure
{
    /// <summary>
    /// T-007 (D-030): the craftable item "Fluffy Slippers" (D-034; was "Steve the Troll Slippers") in the utility slot (D-009). Cloned from the vanilla belt so the
    /// utility-slot handling is the game's own; nothing of the belt's look stays: worn look = the bundle's SlipperSkinParts (D-166: the slippers WITH their trolls, one skinned mesh, grafted as
    /// attach_skin exactly like the costume pieces: the game puts it on every wearer's own bones), ground look = SlipperDropModel.
    /// Stats (D-005): armor 5 (+1 per quality), max quality 4, frost resistant, workbench level 1. Recipe: 5 TrollHide (D-034), 4 DeerHide, 6 Blueberries.
    /// </summary>
    internal static class SlipperItem
    {
        public const string PrefabName = "STT_TrollSlippers";
        public const string TokenName = "$stevethetroll_slippers";
        const string BasePrefab = "BeltStrength";
        const string IconName = BundleAssets.SlipperIcon;
        public const float BaseArmor = 5f, ArmorPerLevel = 1f;
        public const int MaxQuality = 4;
        /// D-034 (user 2026-10-27): 5 TrollHide (was 10).
        public const int TrollHideCost = 5;
        /// TrollHide, DeerHide, Blueberries (SlipperUpgradeCostPatch tracks them).
        public const int RequirementCount = 3;
        /// D-035: each upgrade costs this much TrollHide and Blueberries, the same at every quality (SlipperUpgradeCostPatch).
        public const int UpgradeCostPerItem = 2;
        static int _hash;

        /// The slippers' shared item data once added (the costume set bonus adds the slippers to its set, D-076).
        internal static ItemDrop.ItemData.SharedData Shared;

        /// The utility hash VisEquipment uses for this item (prefab name hash; 0 until the item is added).
        public static int Hash { get { return _hash; } }

        /// Expected outcome: the item is queued for when the vanilla prefabs exist (returns true once subscribed).
        public static bool Install()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Add;
            ModLog.Verbose("SlipperItem: waiting for vanilla prefabs to clone " + BasePrefab + " as " + PrefabName);
            return true;
        }

        static void Add()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Add;
            var cfg = new ItemConfig
            {
                Name = TokenName,
                Description = "$stevethetroll_slippers_desc",
                CraftingStation = CraftingStations.Workbench,
                MinStationLevel = 1,
            };
            cfg.AddRequirement(new RequirementConfig("TrollHide", TrollHideCost, UpgradeCostPerItem, true));
            cfg.AddRequirement(new RequirementConfig("DeerHide", 4, 0, true));
            cfg.AddRequirement(new RequirementConfig("Blueberries", 6, UpgradeCostPerItem, false));
            ModLog.Verbose("SlipperItem: recipe TrollHide x" + TrollHideCost + " (" + UpgradeCostPerItem + " per upgrade, every quality), DeerHide x4, Blueberries x6 (" + UpgradeCostPerItem + " per upgrade), workbench level " + cfg.MinStationLevel);
            ModLog.Check("SlipperItem: recipe TrollHide", cfg.Requirements[0].Amount, TrollHideCost, 0, "");
            var icon = BundleAssets.Load<Sprite>(IconName);
            if (icon != null) cfg.Icons = new[] { icon };
            else ModLog.Warn("SlipperItem: icon '" + IconName + "' not in the bundle (bundle loaded " + (Plugin.Bundle != null) + ") - the belt icon stays");

            var item = new CustomItem(PrefabName, BasePrefab, cfg);
            if (item.ItemPrefab == null || item.ItemDrop == null) { ModLog.Error("SlipperItem: could not clone " + BasePrefab + " - no slipper item"); return; }
            int removed = Strip(item.ItemPrefab);
            bool worn = AddSkin(item.ItemPrefab);
            bool dropLook = SlipperDropModel.Build(item.ItemPrefab);
            var shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Utility;
            shared.m_armor = BaseArmor;
            shared.m_armorPerLevel = ArmorPerLevel;
            shared.m_maxQuality = MaxQuality;
            shared.m_equipStatusEffect = FrostEffect(icon);      // D-033: frost resistance shown under Active effects with the slipper icon
            shared.m_setStatusEffect = null;
            shared.m_movementModifier = 0f;
            shared.m_damageModifiers = new List<HitData.DamageModPair>
            {
                new HitData.DamageModPair { m_type = HitData.DamageType.Frost, m_modifier = HitData.DamageModifier.Resistant },
            };
            Shared = shared;
            bool added = ItemManager.Instance.AddItem(item);
            _hash = PrefabName.GetStableHashCode();
            ModLog.Info("SlipperItem: " + PrefabName + " added " + added + " (armor " + shared.m_armor + " +" + shared.m_armorPerLevel + "/quality, max quality " + shared.m_maxQuality + ", worn belt parts removed " + removed + ", worn skin grafted " + worn + ", ground look slippers " + dropLook + ", hash " + _hash + ")");
            ModLog.Check("SlipperItem: armor at quality 1", shared.m_armor, BaseArmor, 0.001, "");
            ModLog.Check("SlipperItem: max quality", shared.m_maxQuality, MaxQuality, 0, "");
        }

        public const string FrostEffectName = "SE_STT_SlipperFrost";

        /// D-033 (now, with T-008): a status effect while the slippers are worn: frost Resistant, the slipper icon, shown in Compendium >
        /// Active effects. Registered with Jotunn. Expected outcome: the effect (null if it could not be made, logged).
        static StatusEffect FrostEffect(Sprite icon)
        {
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = FrostEffectName;
            se.m_name = "$stevethetroll_se_frost";
            se.m_tooltip = "$stevethetroll_se_frost_tooltip";
            se.m_icon = icon;
            se.m_mods = new List<HitData.DamageModPair> { new HitData.DamageModPair { m_type = HitData.DamageType.Frost, m_modifier = HitData.DamageModifier.Resistant } };
            bool added = ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, false));
            ModLog.Verbose("SlipperItem: status effect " + FrostEffectName + " added " + added + " (icon " + (icon != null) + ", frost Resistant)");
            ModLog.Check("SlipperItem: frost status effect registered", added ? 1 : 0, 1, 0, "");
            return se;
        }

        /// D-166: the worn look. The bundle prefab SlipperSkinParts has one child "attach_skin" (the player's armature + the SlipperSkin mesh: both slippers and both trolls, weights from the
        /// player's body); it goes into the item inactive, as vanilla: VisEquipment.AttachArmor activates its copy under each wearer. Expected outcome: true when the skinned mesh is on the item.
        static bool AddSkin(GameObject prefab)
        {
            var parts = BundleAssets.Load<GameObject>(BundleAssets.SlipperSkinParts);
            var skin = parts != null ? parts.transform.Find("attach_skin") : null;
            if (skin == null) { ModLog.Error("SlipperItem: bundle part " + BundleAssets.SlipperSkinParts + "/attach_skin missing (prefab " + (parts != null) + ") - the slippers have no worn look"); return false; }
            var mine = Object.Instantiate(skin.gameObject, prefab.transform, false);
            mine.name = "attach_skin";
            mine.SetActive(false);
            var smr = mine.GetComponentInChildren<SkinnedMeshRenderer>(true);
            ModLog.Verbose("SlipperItem: worn skin grafted: " + (smr != null && smr.sharedMesh != null ? smr.sharedMesh.vertexCount + " verts, " + smr.sharedMaterials.Length + " materials, " + smr.bones.Length + " bones" : "NO skinned mesh"));
            return smr != null && smr.sharedMesh != null;
        }

        /// Removes the belt's worn visual (children named attach_*, e.g. attach_skin); the ground model "attach" stays and gets the
        /// slipper pair (SlipperDropModel). Returns how many worn parts were removed.
        static int Strip(GameObject prefab)
        {
            var doomed = new List<GameObject>();
            foreach (Transform child in prefab.transform) if (child.name.StartsWith("attach_")) doomed.Add(child.gameObject);
            foreach (var go in doomed) { ModLog.Verbose("SlipperItem: removing belt part " + go.name); Object.DestroyImmediate(go, true); }
            return doomed.Count;
        }
    }

    /// Every character (local and remote players) wearing the slippers in the utility slot is marked (the costume legs hide their plush feet then). The look itself is the game's:
    /// VisEquipment attaches the item's attach_skin on each wearer and syncs it over the network, so other players see it as long as they have the mod (D-007).
    // Harmony hook: src/Integrations/Harmony/SlippersPatches.cs, SlipperVisualPatchHook (MIGRATION_DECISIONS A5)
    internal static class SlipperVisualPatch
    {
        internal static void Postfix(VisEquipment __instance, int hash)
        {
            if (__instance == null || SlipperItem.Hash == 0) return;
            bool wear = hash == SlipperItem.Hash && ModSettings.On;
            SlipperWearer w;
            if (!__instance.TryGetComponent(out w))
            {
                if (!wear) return;
                w = __instance.gameObject.AddComponent<SlipperWearer>();
            }
            w.Set(wear);
        }
    }

    /// Tells whether this character wears the slippers now (read by the costume legs).
    internal sealed class SlipperWearer : MonoBehaviour
    {
        bool _on;

        /// The slippers are worn by this character now (the costume legs hide their plush feet then).
        public bool On { get { return _on; } }

        /// Expected outcome: the state is the one asked for (returns that check).
        public bool Set(bool on)
        {
            _on = on;
            ModLog.Verbose("SlipperWearer: " + name + " slippers " + (on ? "on" : "off"));
            return _on == on;
        }
    }
}