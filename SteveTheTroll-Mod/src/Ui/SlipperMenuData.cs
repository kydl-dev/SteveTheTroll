using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// Game side of the F6 menu tabs (D-039): the slippers' data from the item + recipe in ObjectDB, the 3D model (an inert copy of the item's
    /// ground model "attach": the slipper pair - no trolls, the surprise stays), and the player's troll focus (saved in the player's profile,
    /// Player.m_customData; the tamed troll reads it in T-008d).
    /// </summary>
    internal static class SlipperMenuData
    {
        public const string FocusKey = "stevethetroll_troll_focus";

        /// Expected outcome: the data of the real item, or null before a world is loaded (logged).
        internal static SlipperData Read()
        {
            var db = ObjectDB.instance;
            var prefab = db != null ? db.GetItemPrefab(Item.SlipperItem.PrefabName) : null;
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null) { ModLog.Verbose("SlipperMenuData: no ObjectDB / item yet (db " + (db != null) + ", prefab " + (prefab != null) + ") - no details"); return null; }
            var s = drop.m_itemData.m_shared;
            var d = new SlipperData
            {
                Name = Loc.T(s.m_name.TrimStart('$')),
                Description = Loc.T(s.m_description.TrimStart('$')),
                BaseArmor = s.m_armor,
                ArmorPerLevel = s.m_armorPerLevel,
                MaxQuality = s.m_maxQuality,
                FrostResistant = Item.SlipperStatsPatch.FrostResistant(s),
            };
            var recipe = db.GetRecipe(drop.m_itemData);
            if (recipe != null)
            {
                d.Station = recipe.m_craftingStation != null ? Localization.instance.Localize(recipe.m_craftingStation.m_name) : "-";
                d.StationLevel = recipe.m_minStationLevel;
                foreach (var r in recipe.m_resources)
                {
                    if (r == null || r.m_resItem == null) continue;
                    d.Recipe.Add(new SlipperData.Ingredient(Localization.instance.Localize(r.m_resItem.m_itemData.m_shared.m_name), r.GetAmount(1), r.GetAmount(2)));
                }
            }
            var worn = Item.SlipperStatsPatch.Worn(Player.m_localPlayer);
            d.WornQuality = worn != null ? worn.m_quality : 0;
            ModLog.Verbose("SlipperMenuData: " + d + " (recipe " + (recipe != null) + ")");
            return d;
        }

        /// The viewer's model maker. Expected outcome: an inactive-free copy of the item's ground model, or null for unknown ids / no item.
        internal static GameObject MakeModel(string id)
        {
            if (id != MainMenuView.SlippersModel) { ModLog.Warn("SlipperMenuData: unknown viewer model '" + id + "'"); return null; }
            var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(Item.SlipperItem.PrefabName) : null;
            var attach = prefab != null ? prefab.transform.Find("attach") : null;
            if (attach == null) { ModLog.Warn("SlipperMenuData: no ground model 'attach' on " + Item.SlipperItem.PrefabName + " (prefab " + (prefab != null) + ")"); return null; }
            var copy = Object.Instantiate(attach.gameObject);
            copy.name = "SteveTheTroll_ViewerSlippers";
            int behaviours = 0;
            foreach (var b in copy.GetComponentsInChildren<MonoBehaviour>(true)) { b.enabled = false; behaviours++; }
            copy.SetActive(true);
            ModLog.Verbose("SlipperMenuData: viewer model from '" + attach.name + "', " + copy.GetComponentsInChildren<Renderer>(true).Length + " renderer(s), " + behaviours + " behaviour(s) off");
            return copy;
        }

        /// The stored troll focus of the local player ("" = no focus).
        internal static string Focus()
        {
            var p = Player.m_localPlayer;
            string v;
            string focus = p != null && p.m_customData.TryGetValue(FocusKey, out v) ? TrollFocus.Normalize(v) : TrollFocus.None;
            ModLog.Verbose("SlipperMenuData: troll focus read '" + focus + "' (player " + (p != null) + ")");
            return focus;
        }

        /// Expected outcome: the player's profile holds the focus (returns that check).
        internal static bool SetFocus(string value)
        {
            var p = Player.m_localPlayer;
            if (p == null) { ModLog.Warn("SlipperMenuData: no local player - troll focus '" + value + "' not saved"); return p != null; }
            string v = TrollFocus.Normalize(value);
            p.m_customData[FocusKey] = v;
            ModLog.Info("SlipperMenuData: troll focus set to '" + (v.Length > 0 ? v : "no focus") + "'");
            return p.m_customData[FocusKey] == v;
        }
    }
}
