using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Kydl.Common;
using SteveTheTroll.Figure;
using UnityEngine;

namespace SteveTheTroll.Item
{
    /// <summary>
    /// T-007 (D-030): the craftable item "Steve the Troll Slippers" in the utility slot (D-009). Cloned from the vanilla belt so the
    /// utility-slot handling is the game's own; nothing of the belt's look stays: worn look = SlipperSet, ground look = SlipperDropModel.
    /// Stats (D-005): armor 5 (+1 per quality), max quality 4, frost resistant, workbench level 1.
    /// </summary>
    internal static class SlipperItem
    {
        public const string PrefabName = "STT_TrollSlippers";
        public const string TokenName = "$stevethetroll_slippers";
        const string BasePrefab = "BeltStrength";
        const string IconName = "SlipperIcon";
        public const float BaseArmor = 5f, ArmorPerLevel = 1f;
        public const int MaxQuality = 4;
        static int _hash;

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
            cfg.AddRequirement(new RequirementConfig("TrollHide", 10, 2, true));
            cfg.AddRequirement(new RequirementConfig("DeerHide", 4, 0, true));
            cfg.AddRequirement(new RequirementConfig("Blueberries", 6, 2, false));
            var icon = Plugin.Bundle != null ? Plugin.Bundle.LoadAsset<Sprite>(IconName) : null;
            if (icon != null) cfg.Icons = new[] { icon };
            else ModLog.Warn("SlipperItem: icon '" + IconName + "' not in the bundle (bundle loaded " + (Plugin.Bundle != null) + ") - the belt icon stays");

            var item = new CustomItem(PrefabName, BasePrefab, cfg);
            if (item.ItemPrefab == null || item.ItemDrop == null) { ModLog.Error("SlipperItem: could not clone " + BasePrefab + " - no slipper item"); return; }
            int removed = Strip(item.ItemPrefab);
            bool dropLook = SlipperDropModel.Build(item.ItemPrefab);
            var shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Utility;
            shared.m_armor = BaseArmor;
            shared.m_armorPerLevel = ArmorPerLevel;
            shared.m_maxQuality = MaxQuality;
            shared.m_equipStatusEffect = null;
            shared.m_setStatusEffect = null;
            shared.m_movementModifier = 0f;
            shared.m_damageModifiers = new List<HitData.DamageModPair>
            {
                new HitData.DamageModPair { m_type = HitData.DamageType.Frost, m_modifier = HitData.DamageModifier.Resistant },
            };
            bool added = ItemManager.Instance.AddItem(item);
            _hash = PrefabName.GetStableHashCode();
            ModLog.Info("SlipperItem: " + PrefabName + " added " + added + " (armor " + shared.m_armor + " +" + shared.m_armorPerLevel + "/quality, max quality " + shared.m_maxQuality + ", worn belt parts removed " + removed + ", ground look slippers " + dropLook + ", hash " + _hash + ")");
            ModLog.Check("SlipperItem: armor at quality 1", shared.m_armor, BaseArmor, 0.001, "");
            ModLog.Check("SlipperItem: max quality", shared.m_maxQuality, MaxQuality, 0, "");
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

    /// Every character (local and remote players) wearing the slippers in the utility slot gets a SlipperSet: VisEquipment is synced over
    /// the network by the game, so other players see it as long as they have the mod (D-007).
    [HarmonyPatch(typeof(VisEquipment), "SetUtilityEquipped")]
    internal static class SlipperVisualPatch
    {
        static void Postfix(VisEquipment __instance, int hash)
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

    /// Owns the SlipperSet of one character; removes it when unequipped or when the character is destroyed.
    internal sealed class SlipperWearer : MonoBehaviour
    {
        SlipperSet _set;
        bool _on;

        /// Expected outcome: the set exists exactly when on is true (returns that check).
        public bool Set(bool on)
        {
            if (on == _on) return (_set != null) == on;
            _on = on;
            if (on) _set = SlipperSet.Spawn(transform, name);
            else if (_set != null) { _set.Despawn(); _set = null; }
            ModLog.Verbose("SlipperWearer: " + name + " slippers " + (on ? "on" : "off") + ", set " + (_set != null ? (_set.Complete ? "complete" : "incomplete") : "none"));
            _fills = 0;
            return (_set != null) == on;
        }

        const float FillEverySeconds = 1f;
        const int MaxFills = 60;
        float _nextFill;
        int _fills;

        // TEST-008: worn while the world loads -> the trolls could not be made yet; add the missing parts once it is ready (1 s steps, 60 tries).
        void Update()
        {
            if (!_on || _set == null || _set.Complete || _fills >= MaxFills || Time.time < _nextFill) return;
            _nextFill = Time.time + FillEverySeconds;
            _fills++;
            int n = _set.Fill();
            ModLog.Verbose("SlipperWearer: " + name + " filled the set, try " + _fills + ": " + n + "/4 parts");
            if (_fills == MaxFills && !_set.Complete) ModLog.Warn("SlipperWearer: " + name + " set still incomplete after " + MaxFills + " tries (" + n + "/4)");
        }

        void OnDestroy()
        {
            if (_set != null) { _set.Despawn(); _set = null; }
        }
    }
}
