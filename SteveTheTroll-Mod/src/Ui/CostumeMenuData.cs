using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// D-080 game side of the F6 Costume tab: the three costume pieces, the set and the Troll Club read from ObjectDB / the local player, and
    /// the 3D model (bundle prefab CostumeViewer: the whole mascot standing, static meshes made by Unity's CostumeBuild).
    /// </summary>
    internal static class CostumeMenuData
    {
        const string ViewerPrefab = BundleAssets.CostumeViewer;

        /// Expected outcome: the costume's data, or null before a world is loaded / when the items are missing (logged).
        internal static CostumeData Read()
        {
            var db = ObjectDB.instance;
            if (db == null) { ModLog.Verbose("CostumeMenuData: no ObjectDB yet - no Costume tab"); return null; }
            if (!Item.RecipeUnlockPatch.CostumeComplete(Player.m_localPlayer, db)) { ModLog.Verbose("CostumeMenuData: not all 4 costume items crafted yet - no Costume tab (D-088)"); return null; }
            var d = new CostumeData
            {
                SetName = Loc.T("stevethetroll_se_set"),
                SetDescription = Loc.T("stevethetroll_se_set_tooltip"),
                SetSize = Item.CostumeItems.SetSize,
            };
            var p = Player.m_localPlayer;
            AddPiece(d, db, p, Item.CostumeItems.HelmetName, "stevethetroll_details_slot_helmet");
            AddPiece(d, db, p, Item.CostumeItems.ChestName, "stevethetroll_details_slot_chest");
            AddPiece(d, db, p, Item.CostumeItems.LegsName, "stevethetroll_details_slot_legs");
            if (d.Pieces.Count == 0) { ModLog.Warn("CostumeMenuData: no costume item in ObjectDB - no Costume tab"); return null; }
            d.SetWorn = p != null ? (int)HarmonyLib.AccessTools.Method(typeof(Humanoid), "GetSetCount").Invoke(p, new object[] { Item.CostumeItems.SetName }) : 0;   // private in 1.0.20
            d.SetEffects.Add(string.Format(Loc.T("stevethetroll_set_move"), (Item.CostumeItems.SetSpeed * 100f).ToString("0")));
            d.SetEffects.Add(string.Format(Loc.T("stevethetroll_set_carry"), (Item.CostumeItems.SetCarryPercent * 100f).ToString("0")));
            d.SetEffects.Add(string.Format(Loc.T("stevethetroll_set_stamina"), (Item.CostumeItems.SetStamina * 100f).ToString("0")));
            d.SetEffects.Add(string.Format(Loc.T("stevethetroll_set_trolls"), Item.CostumeItems.SetTrollLimit));
            d.SetEffects.Add(string.Format(Loc.T("stevethetroll_set_stagger"), (Item.CostumeItems.SetStaggerResistance * 100f).ToString("0")));   // D-125

            var club = Drop(db, Item.CostumeItems.ClubName);
            if (club != null)
            {
                var s = club.m_itemData.m_shared;
                d.ClubName = Loc.T(s.m_name.TrimStart('$'));
                d.ClubDescription = Loc.T(s.m_description.TrimStart('$'));
                d.ClubBlunt = s.m_damages.m_blunt; d.ClubChop = s.m_damages.m_chop; d.ClubPickaxe = s.m_damages.m_pickaxe;
                d.ClubBluntPerLevel = s.m_damagesPerLevel.m_blunt; d.ClubChopPerLevel = s.m_damagesPerLevel.m_chop; d.ClubPickaxePerLevel = s.m_damagesPerLevel.m_pickaxe;
                d.ClubMaxQuality = s.m_maxQuality;
                var recipe = db.GetRecipe(club.m_itemData);
                if (recipe != null)
                {
                    d.Station = recipe.m_craftingStation != null ? Localization.instance.Localize(recipe.m_craftingStation.m_name) : "-";
                    d.StationLevel = recipe.m_minStationLevel;
                    foreach (var r in recipe.m_resources)
                        if (r != null && r.m_resItem != null) d.ClubRecipe.Add(new SlipperData.Ingredient(Localization.instance.Localize(r.m_resItem.m_itemData.m_shared.m_name), r.GetAmount(1), r.GetAmount(2)));
                }
            }
            ModLog.Verbose("CostumeMenuData: " + d);
            return d;
        }

        static ItemDrop Drop(ObjectDB db, string prefab)
        {
            var go = db.GetItemPrefab(prefab);
            return go != null ? go.GetComponent<ItemDrop>() : null;
        }

        static void AddPiece(CostumeData d, ObjectDB db, Player p, string prefab, string slotKey)
        {
            var drop = Drop(db, prefab);
            if (drop == null) { ModLog.Warn("CostumeMenuData: item " + prefab + " missing"); return; }
            var s = drop.m_itemData.m_shared;
            var piece = new CostumeData.Piece { Name = Loc.T(s.m_name.TrimStart('$')), SlotKey = slotKey, Armor = s.m_armor, ArmorPerLevel = s.m_armorPerLevel, MaxQuality = s.m_maxQuality };
            if (p != null)
                foreach (var it in p.GetInventory().GetEquippedItems())
                    if (it.m_shared.m_name == s.m_name) piece.WornQuality = it.m_quality;
            var recipe = db.GetRecipe(drop.m_itemData);
            if (recipe != null)
                foreach (var r in recipe.m_resources)
                    if (r != null && r.m_resItem != null) piece.Recipe.Add(new SlipperData.Ingredient(Localization.instance.Localize(r.m_resItem.m_itemData.m_shared.m_name), r.GetAmount(1), r.GetAmount(2)));
            d.Pieces.Add(piece);
            if (d.Station.Length == 0 && recipe != null && recipe.m_craftingStation != null) { d.Station = Localization.instance.Localize(recipe.m_craftingStation.m_name); d.StationLevel = recipe.m_minStationLevel; }
        }

        /// The viewer's model maker. Expected outcome: a copy of the bundle's standing mascot, or null (logged).
        internal static GameObject MakeModel(string id)
        {
            if (id != MainMenuView.CostumeModel) { ModLog.Warn("CostumeMenuData: unknown viewer model '" + id + "'"); return null; }
            var prefab = BundleAssets.Load<GameObject>(ViewerPrefab);
            if (prefab == null) { ModLog.Warn("CostumeMenuData: '" + ViewerPrefab + "' not in the bundle"); return null; }
            var copy = Object.Instantiate(prefab);
            copy.name = "SteveTheTroll_ViewerCostume";
            copy.SetActive(true);
            ModLog.Verbose("CostumeMenuData: viewer model with " + copy.GetComponentsInChildren<Renderer>(true).Length + " renderer(s)");
            return copy;
        }
    }
}
