using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// Game side of the F6 menu tabs (D-039 / D-043): the slippers' data from the item + recipe in ObjectDB, the 3D model (an inert copy of
    /// the item's ground model "attach": the slipper pair - no trolls, the surprise stays), and each troll's focus (TrollRegistry).
    /// </summary>
    internal static class SlipperMenuData
    {

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

        /// D-044 "Go farm": the troll leaves follow / stay and farms its focus (or the D-038 priority). Only a troll that is near and simulated
        /// here can get the order; otherwise the player is told. Expected outcome: returns whether the troll now farms.
        internal static bool GoFarm(string id)
        {
            var troll = Taming.TrollRegistry.FindLoaded(id);
            var info = Taming.TrollRegistry.Trolls().Find(t => t.Id == id);
            string shown = info != null ? info.Shown(k => Loc.T(k)) : id;
            if (troll == null) { Tell(Loc.T("stevethetroll_gofarm_far", shown), Notify.Kind.Warn); ModLog.Info("SlipperMenuData: Go farm for " + shown + " - not loaded"); return false; }
            string focus = info != null ? TrollFocus.Normalize(info.Focus) : "";
            bool ok = Taming.TrollFarming.Begin(troll, focus);
            Tell(ok ? Loc.T("stevethetroll_gofarm_started", shown, Loc.T(TrollFocus.KeyOf(focus))) : Loc.T("stevethetroll_gofarm_far", shown), ok ? Notify.Kind.Farm : Notify.Kind.Warn);
            Taming.TrollRegistry.Trolls();           // refresh now: the open menu shows "Farming" within a second (TEST-019)
            ModLog.Info("SlipperMenuData: Go farm for " + shown + " focus '" + focus + "' -> " + ok);
            return ok;
        }

        static void Tell(string msg, Notify.Kind kind = Notify.Kind.Info) { Notify.Show(msg, kind); }

        /// D-043: a troll's new focus from the Troll work tab (stored on that troll, pending while it is far). A troll that follows its owner
        /// does not farm: the player gets a message to let it stay (E on the troll). Expected outcome: returns whether the message was shown.
        internal static bool FocusChosen(string id, string value)
        {
            string v = TrollFocus.Normalize(value);
            Taming.TrollRegistry.SetFocus(id, v);
            var troll = Taming.TrollRegistry.FindLoaded(id);
            var ai = troll != null ? troll.GetComponent<MonsterAI>() : null;
            bool following = ai != null && ai.GetFollowTarget() != null;
            ModLog.Verbose("SlipperMenuData: troll [" + id + "] " + (troll != null ? "loaded, following " + following : "not loaded") + ", focus '" + v + "'");
            if (!following || v.Length == 0) return false;
            string msg = Loc.T("stevethetroll_focus_following", Loc.T(TrollFocus.KeyOf(v)));
            ModLog.Info("SlipperMenuData: told the player the troll is following (" + msg + ")");
            return Notify.Show(msg, Notify.Kind.Warn);
        }
    }
}
