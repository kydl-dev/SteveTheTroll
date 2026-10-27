using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Testing
{
    /// <summary>
    /// In-game tests run by the user's Kydl.GameProbe (dev tool, never a dependency: it finds this class by its NAME `InGameTests`).
    ///   Startup_*  run when the main menu is reached;   World_*  run 5 s after a world is loaded; both again on GameProbe's RetestKey (PageDown).
    /// Each method: static, no parameters, returns a text starting with PASS / FAIL / SKIP and the detail with numbers + units,
    /// e.g. "FAIL: coil radius 0.900 m, expected 0.180 m". null or an exception counts as FAIL. Results land in the session report.
    /// Keep them cheap and read-only: they also run in players' games when GameProbe is installed there (it never is for players).
    /// </summary>
    internal static class InGameTests
    {
        /// The player config is bound and the mod is switched on.
        static string Startup_ConfigBound()
        {
            if (ModSettings.EnableAll == null) return "FAIL: ModSettings.Bind did not run (EnableAll is null)";
            return ModSettings.On ? "PASS: EnableAll on, menu key " + ModSettings.MenuKey.Value : "SKIP: EnableAll is off in the config";
        }

        /// The local player exists once a world is loaded (example of a World_ test; replace with the mod's own checks).
        static string World_PlayerPresent()
        {
            var p = Player.m_localPlayer;
            ModLog.Verbose("InGameTests: World_PlayerPresent player " + (p != null ? p.GetPlayerName() : "null"));
            return p != null ? "PASS: " + p.GetPlayerName() + " at " + p.transform.position.ToString("F1") : "FAIL: no local player 5 s after the world loaded";
        }

        /// T-004b: everything the runtime troll figure reads from the game exists (Troll/Visual, the troll's log, the player's feet).
        static string World_TrollFigureSources()
        {
            var troll = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(Figure.TrollFigureFactory.TrollPrefab) : null;
            var visual = troll != null ? troll.transform.Find("Visual") : null;
            var logItem = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(Figure.TrollFigureFactory.LogItemPrefab) : null;
            var logAttach = logItem != null ? logItem.transform.Find("attach") : null;
            var p = Player.m_localPlayer;
            var foot = p != null ? Utils.FindChild(p.transform, "LeftFoot") : null;
            string detail = "Troll " + (troll != null) + ", Troll/Visual " + (visual != null) + ", " + Figure.TrollFigureFactory.LogItemPrefab + "/attach " + (logAttach != null) + ", player LeftFoot " + (foot != null);
            ModLog.Verbose("InGameTests: World_TrollFigureSources " + detail);
            return troll != null && visual != null && logAttach != null && foot != null ? "PASS: " + detail : "FAIL: " + detail;
        }

        /// T-007: the slipper item exists with its stats (armor 5, max quality 4, utility, frost resistant) and has a recipe.
        static string World_SlipperItem()
        {
            var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(Item.SlipperItem.PrefabName) : null;
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null) return "FAIL: " + Item.SlipperItem.PrefabName + " not in ObjectDB";
            var s = drop.m_itemData.m_shared;
            bool frost = s.m_damageModifiers != null && s.m_damageModifiers.Exists(m => m.m_type == HitData.DamageType.Frost && m.m_modifier == HitData.DamageModifier.Resistant);
            var recipe = ObjectDB.instance.GetRecipe(drop.m_itemData);
            var ground = prefab.transform.Find("attach");
            int slippers = ground != null ? (ground.Find("SlipperDropLeft") != null ? 1 : 0) + (ground.Find("SlipperDropRight") != null ? 1 : 0) : 0;
            bool groundCol = ground != null && ground.GetComponent<BoxCollider>() != null;
            int hide = TrollHideAmount(recipe);
            string shown = Localization.instance != null ? Localization.instance.Localize(s.m_name) : s.m_name;
            string detail = "ground slippers " + slippers + ", ground collider " + groundCol + ", worn belt part attach_skin " + (prefab.transform.Find("attach_skin") != null) + ", type " + s.m_itemType + ", armor " + s.m_armor + " +" + s.m_armorPerLevel + "/q, max quality " + s.m_maxQuality + ", frost resistant " + frost + ", recipe " + (recipe != null) + ", TrollHide " + hide + " (expected " + Item.SlipperItem.TrollHideCost + "), name " + s.m_name + " = '" + shown + "' (expected '" + ExpectedName + "')";
            ModLog.Verbose("InGameTests: World_SlipperItem " + detail);
            bool ok = s.m_itemType == ItemDrop.ItemData.ItemType.Utility && Mathf.Approximately(s.m_armor, Item.SlipperItem.BaseArmor) && s.m_maxQuality == Item.SlipperItem.MaxQuality && frost && recipe != null && hide == Item.SlipperItem.TrollHideCost && shown == ExpectedName && slippers == 2 && groundCol && prefab.transform.Find("attach_skin") == null;
            return (ok ? "PASS: " : "FAIL: ") + detail;
        }

        const string ExpectedName = "Fluffy Slippers";

        /// D-034: TrollHide amount in the slippers' recipe (-1 = no recipe / no TrollHide line).
        static int TrollHideAmount(Recipe recipe)
        {
            int amount = -1;
            if (recipe != null && recipe.m_resources != null)
            {
                foreach (var r in recipe.m_resources)
                {
                    if (r != null && r.m_resItem != null && r.m_resItem.name == "TrollHide") amount = r.m_amount;
                }
            }
            ModLog.Verbose("InGameTests: slipper recipe TrollHide amount " + amount);
            return amount;
        }
    }
}
