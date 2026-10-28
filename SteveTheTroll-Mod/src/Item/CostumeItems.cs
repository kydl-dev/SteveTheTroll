using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Item
{
    /// <summary>
    /// T-009d (D-069 .. D-072, D-075): the troll mascot costume (helmet / chest / legs) and the Troll Club, cloned from vanilla items so
    /// equipping, animations and networking stay the game's own; their worn look is replaced by our parts from the bundle
    /// (Unity menu SteveTheTroll > Costume > Build costume parts, D-074):
    ///  - helmet  <- HelmetTrollLeather: child "attach" = CostumeHelmetParts/attach (rigid, on Helmet_attach), hair and beard hidden
    ///  - chest   <- ArmorTrollLeatherChest: child "attach_skin" = CostumeChestParts/attach_skin (skinned, the game swaps in the wearer's bones)
    ///  - legs    <- ArmorTrollLeatherLegs: child "attach_skin" = CostumeLegsParts/attach_skin (+ plush feet, hidden while the slippers are worn)
    ///  - club    <- SledgeIron (two-handed slam, knockback, stagger): the meshes under "attach" swapped for ClubParts/attach/model
    /// Ground look: the helmet shows our head, chest / legs / club keep the vanilla ground model (club: our trunk); icons stay vanilla for now. Armor pieces: 20 armor +5 per quality, max quality 4, recipe as the slippers
    /// (5 TrollHide, 4 DeerHide, 6 Blueberries, workbench 1, every upgrade 2 TrollHide + 2 Blueberries). Club: 70 blunt + 30 chop + 30 pickaxe,
    /// +10 / +5 / +5 per quality, max quality 4, knockback x1.5, workbench 1, 15 RoundLog + 15 FineWood + 10 Thistle + 2 TrollHide, every
    /// upgrade 5 RoundLog + 5 FineWood. The vanilla troll-leather set bonus and effects are removed (our set bonus is T-009e).
    /// </summary>
    internal static class CostumeItems
    {
        public const string HelmetName = "STT_CostumeHelmet", ChestName = "STT_CostumeChest", LegsName = "STT_CostumeLegs", ClubName = "STT_TrollClub";
        public const float Armor = 20f, ArmorPerLevel = 5f;
        public const int MaxQuality = 4;
        public const float ClubBlunt = 70f, ClubChop = 30f, ClubPickaxe = 30f, ClubBluntPerLevel = 10f, ClubChopPerLevel = 5f, ClubPickaxePerLevel = 5f;
        public const float ClubKnockbackFactor = 1.5f;
        static int _helmetHash, _chestHash, _legsHash, _clubHash;

        public static int LegsHash { get { return _legsHash; } }

        /// Expected outcome: the items are queued for when the vanilla prefabs exist (returns true once subscribed).
        public static bool Install()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Add;
            ModLog.Verbose("CostumeItems: waiting for vanilla prefabs (HelmetTrollLeather, ArmorTrollLeatherChest, ArmorTrollLeatherLegs, SledgeIron)");
            return true;
        }

        static void Add()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Add;
            if (Plugin.Bundle == null) { ModLog.Error("CostumeItems: no asset bundle - no costume items"); return; }
            int ok = 0;
            if (AddArmor(HelmetName, "HelmetTrollLeather", "stevethetroll_costume_helmet", "CostumeHelmetParts", "attach", ItemDrop.ItemData.ItemType.Helmet)) ok++;
            if (AddArmor(ChestName, "ArmorTrollLeatherChest", "stevethetroll_costume_chest", "CostumeChestParts", "attach_skin", ItemDrop.ItemData.ItemType.Chest)) ok++;
            if (AddArmor(LegsName, "ArmorTrollLeatherLegs", "stevethetroll_costume_legs", "CostumeLegsParts", "attach_skin", ItemDrop.ItemData.ItemType.Legs)) ok++;
            if (AddClub()) ok++;
            _helmetHash = HelmetName.GetStableHashCode(); _chestHash = ChestName.GetStableHashCode(); _legsHash = LegsName.GetStableHashCode(); _clubHash = ClubName.GetStableHashCode();
            ModLog.Info("CostumeItems: " + ok + "/4 items added (helmet, chest, legs, club)");
            ModLog.Check("CostumeItems: items added", ok, 4, 0, "");
        }

        static GameObject Part(string prefab, string child)
        {
            var p = Plugin.Bundle.LoadAsset<GameObject>(prefab);
            var c = p != null ? p.transform.Find(child) : null;
            if (c == null) ModLog.Error("CostumeItems: bundle part " + prefab + "/" + child + " missing (bundle has prefab " + (p != null) + ")");
            return c != null ? c.gameObject : null;
        }

        static bool AddArmor(string name, string basePrefab, string token, string partsPrefab, string partChild, ItemDrop.ItemData.ItemType type)
        {
            var part = Part(partsPrefab, partChild);
            if (part == null) return false;
            var cfg = new ItemConfig { Name = "$" + token, Description = "$" + token + "_desc", CraftingStation = CraftingStations.Workbench, MinStationLevel = 1 };
            // D-070: the slipper recipe (SlipperItem) for every piece
            cfg.AddRequirement(new RequirementConfig("TrollHide", SlipperItem.TrollHideCost, SlipperItem.UpgradeCostPerItem, true));
            cfg.AddRequirement(new RequirementConfig("DeerHide", 4, 0, true));
            cfg.AddRequirement(new RequirementConfig("Blueberries", 6, SlipperItem.UpgradeCostPerItem, false));
            var item = new CustomItem(name, basePrefab, cfg);
            if (item.ItemPrefab == null || item.ItemDrop == null) { ModLog.Error("CostumeItems: could not clone " + basePrefab + " for " + name); return false; }
            int removed = StripWorn(item.ItemPrefab);
            var mine = Object.Instantiate(part, item.ItemPrefab.transform, false);
            mine.name = partChild;
            // as vanilla: attach_skin is inactive in the item (VisEquipment activates its copy), a helmet's "attach" is active and is
            // also the ground look (HelmetBronze) - so the helmet's vanilla ground model goes
            mine.SetActive(partChild != "attach_skin");
            int groundRemoved = 0;
            if (partChild == "attach")
                foreach (var r in item.ItemPrefab.GetComponentsInChildren<Renderer>(true))
                    if (!r.transform.IsChildOf(mine.transform)) { var mf = r.GetComponent<MeshFilter>(); Object.DestroyImmediate(r, true); if (mf != null) Object.DestroyImmediate(mf, true); groundRemoved++; }
            if (type == ItemDrop.ItemData.ItemType.Legs)
            {
                var feet = mine.transform.Find("CostumeFeet");
                if (feet != null) feet.gameObject.AddComponent<CostumeFeet>();
                ModLog.Check("CostumeItems: legs have the plush feet", feet != null ? 1 : 0, 1, 0, "");
            }
            var s = item.ItemDrop.m_itemData.m_shared;
            s.m_itemType = type;
            s.m_armor = Armor; s.m_armorPerLevel = ArmorPerLevel; s.m_maxQuality = MaxQuality;
            s.m_setName = ""; s.m_setSize = 0; s.m_setStatusEffect = null; s.m_equipStatusEffect = null;
            s.m_movementModifier = 0f;
            s.m_damageModifiers = new List<HitData.DamageModPair>();
            if (type == ItemDrop.ItemData.ItemType.Helmet) { s.m_helmetHideHair = ItemDrop.ItemData.HelmetHairType.Hidden; s.m_helmetHideBeard = ItemDrop.ItemData.HelmetHairType.Hidden; }
            bool added = ItemManager.Instance.AddItem(item);
            SlipperUpgradeCostPatch.Track(name, 3);
            int renderers = mine.GetComponentsInChildren<Renderer>(true).Length;
            ModLog.Verbose("CostumeItems: " + name + " (" + type + ", from " + basePrefab + ") added " + added + ": vanilla worn parts removed " + removed + ", vanilla ground renderers removed " + groundRemoved + ", ours '" + partChild
                + "' with " + renderers + " renderer(s), armor " + s.m_armor + " +" + s.m_armorPerLevel + "/quality, max quality " + s.m_maxQuality
                + (type == ItemDrop.ItemData.ItemType.Helmet ? ", hair " + s.m_helmetHideHair + ", beard " + s.m_helmetHideBeard : ""));
            ModLog.Check("CostumeItems: " + name + " armor at quality 4", s.m_armor + s.m_armorPerLevel * 3, 35f, 0.001, "");
            return added;
        }

        /// Removes the vanilla worn visual: children named attach / attach_* (the ground model stays).
        static int StripWorn(GameObject prefab)
        {
            var doomed = new List<GameObject>();
            foreach (Transform c in prefab.transform) if (c.name == "attach" || c.name.StartsWith("attach_")) doomed.Add(c.gameObject);
            foreach (var go in doomed) { ModLog.Verbose("CostumeItems: " + prefab.name + " removing vanilla part " + go.name); Object.DestroyImmediate(go, true); }
            return doomed.Count;
        }

        static bool AddClub()
        {
            var part = Part("ClubParts", "attach/model");
            if (part == null) return false;
            var cfg = new ItemConfig { Name = "$stevethetroll_club", Description = "$stevethetroll_club_desc", CraftingStation = CraftingStations.Workbench, MinStationLevel = 1 };
            cfg.AddRequirement(new RequirementConfig("RoundLog", 15, 5, true));        // D-071: core wood
            cfg.AddRequirement(new RequirementConfig("FineWood", 15, 5, true));
            cfg.AddRequirement(new RequirementConfig("Thistle", 10, 0, true));
            cfg.AddRequirement(new RequirementConfig("TrollHide", 2, 0, true));
            var item = new CustomItem(ClubName, "SledgeIron", cfg);
            if (item.ItemPrefab == null || item.ItemDrop == null) { ModLog.Error("CostumeItems: could not clone SledgeIron for the club"); return false; }
            var attach = item.ItemPrefab.transform.Find("attach");
            if (attach == null) { ModLog.Error("CostumeItems: SledgeIron has no 'attach' child - no club"); return false; }
            // the sledge's own meshes go (trail / effects / colliders stay), ours comes in
            int hidden = 0;
            foreach (var mr in attach.GetComponentsInChildren<MeshRenderer>(true)) { if (mr.GetComponent<ParticleSystem>() != null) continue; Object.DestroyImmediate(mr.GetComponent<MeshFilter>(), true); Object.DestroyImmediate(mr, true); hidden++; }
            var model = Object.Instantiate(part, attach, false);
            model.name = "stt_club_model";
            var s = item.ItemDrop.m_itemData.m_shared;
            var v = s.m_damages;
            s.m_damages = new HitData.DamageTypes { m_blunt = ClubBlunt, m_chop = ClubChop, m_pickaxe = ClubPickaxe };
            s.m_damagesPerLevel = new HitData.DamageTypes { m_blunt = ClubBluntPerLevel, m_chop = ClubChopPerLevel, m_pickaxe = ClubPickaxePerLevel };
            float knock = s.m_attackForce;
            s.m_attackForce = knock * ClubKnockbackFactor;
            s.m_maxQuality = MaxQuality;
            s.m_toolTier = Mathf.Max(s.m_toolTier, 2);                                   // mines copper / tin / silver like an iron pickaxe
            bool added = ItemManager.Instance.AddItem(item);
            SlipperUpgradeCostPatch.Track(ClubName, 4);
            ModLog.Verbose("CostumeItems: " + ClubName + " (from SledgeIron) added " + added + ": sledge meshes removed " + hidden + ", damage blunt " + s.m_damages.m_blunt + " chop " + s.m_damages.m_chop
                + " pickaxe " + s.m_damages.m_pickaxe + " (+" + ClubBluntPerLevel + "/" + ClubChopPerLevel + "/" + ClubPickaxePerLevel + "), vanilla blunt " + v.m_blunt + ", knockback " + knock + " -> " + s.m_attackForce
                + ", stagger x" + (s.m_attack != null ? s.m_attack.m_staggerMultiplier.ToString("F2") : "?") + ", tool tier " + s.m_toolTier + ", skill " + s.m_skillType + ", two-handed " + (s.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon));
            ModLog.Check("CostumeItems: club blunt at quality 4", s.m_damages.m_blunt + s.m_damagesPerLevel.m_blunt * 3, 100f, 0.001, "");
            return added;
        }
    }

    /// <summary>
    /// D-074: the costume legs' plush feet show only while the wearer has NOT the Fluffy Slippers on (the slippers are the feet then).
    /// Sits on the "CostumeFeet" renderer inside the legs' attach_skin copy that VisEquipment puts under the character.
    /// </summary>
    internal sealed class CostumeFeet : MonoBehaviour
    {
        Renderer _r;
        VisEquipment _vis;
        int _logged = -1;

        void Awake() { _r = GetComponent<Renderer>(); }

        void LateUpdate()
        {
            if (_r == null) return;
            if (_vis == null) _vis = GetComponentInParent<VisEquipment>();
            SlipperWearer w = null;
            bool slippers = _vis != null && _vis.TryGetComponent(out w) && w.On;
            if (_r.enabled == !slippers) return;
            _r.enabled = !slippers;
            int state = slippers ? 1 : 0;
            if (_logged != state) { _logged = state; ModLog.Verbose("CostumeFeet: " + (_vis != null ? _vis.name : "?") + " plush feet " + (slippers ? "hidden (slippers worn)" : "shown")); }
        }
    }
}
