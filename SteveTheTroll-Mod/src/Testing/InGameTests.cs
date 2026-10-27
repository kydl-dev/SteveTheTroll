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
            string upgrades = UpgradeAmounts(recipe, out bool upgradesOk);
            string shown = Localization.instance != null ? Localization.instance.Localize(s.m_name) : s.m_name;
            string detail = "ground slippers " + slippers + ", ground collider " + groundCol + ", worn belt part attach_skin " + (prefab.transform.Find("attach_skin") != null) + ", type " + s.m_itemType + ", armor " + s.m_armor + " +" + s.m_armorPerLevel + "/q, max quality " + s.m_maxQuality + ", frost resistant " + frost + ", recipe " + (recipe != null) + ", TrollHide " + hide + " (expected " + Item.SlipperItem.TrollHideCost + "), upgrades q2/q3/q4 " + upgrades + " (expected " + Item.SlipperItem.UpgradeCostPerItem + " each), name " + s.m_name + " = '" + shown + "' (expected '" + ExpectedName + "')";
            ModLog.Verbose("InGameTests: World_SlipperItem " + detail);
            bool ok = s.m_itemType == ItemDrop.ItemData.ItemType.Utility && Mathf.Approximately(s.m_armor, Item.SlipperItem.BaseArmor) && s.m_maxQuality == Item.SlipperItem.MaxQuality && frost && recipe != null && hide == Item.SlipperItem.TrollHideCost && upgradesOk && shown == ExpectedName && slippers == 2 && groundCol && prefab.transform.Find("attach_skin") == null;
            return (ok ? "PASS: " : "FAIL: ") + detail;
        }

        const string ExpectedName = "Fluffy Slippers";

        // TEST-012 lesson: the patch class's reflection lookups (static m_tameDeltaTime) must load without an exception.
        static string Startup_TamingReflection()
        {
            try
            {
                string r = Taming.TrollTamingPatch.SelfCheck();
                bool stats = Taming.TamedTrollStats.SelfCheck(), defend = Taming.TrollDefend.SelfCheck(), farm = Taming.TrollFarming.SelfCheck();
                string more = ", TamedTrollStats " + stats + ", TrollDefend " + defend + ", TrollFarming " + farm;
                return r.StartsWith("PASS") && stats && defend && farm ? r + more : "FAIL: " + r + more;
            }
            catch (System.TypeInitializationException e) { return "FAIL: " + (e.InnerException != null ? e.InnerException.Message : e.Message); }
        }

        // T-008a: the game's Troll prefab carries a Tameable with the taming time and E commands (follow / stay).
        static string World_TrollTameable()
        {
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(Taming.TrollTaming.TrollPrefab) : null;
            var t = prefab != null ? prefab.GetComponent<Tameable>() : null;
            if (t == null) return "FAIL: " + Taming.TrollTaming.TrollPrefab + " " + (prefab != null ? "has no Tameable" : "not in ZNetScene");
            bool effects = t.m_tamedEffect != null && t.m_petEffect != null && t.m_sootheEffect != null;
            string detail = "taming " + t.m_tamingTime + " s (expected " + ModSettings.TameSeconds.Value + "), commandable " + t.m_commandable + ", effects " + effects
                + ", range " + ModSettings.TameRange.Value + " m, own troll limit " + ModSettings.OwnTrollRange.Value + " m";
            ModLog.Verbose("InGameTests: World_TrollTameable " + detail);
            bool ok = Mathf.Approximately(t.m_tamingTime, ModSettings.TameSeconds.Value) && t.m_commandable && effects;
            return (ok ? "PASS: " : "FAIL: ") + detail;
        }

        /// D-035: upgrade amounts of the slippers' TrollHide and Blueberries at quality 2, 3 and 4 (through the patched GetAmount).
        static string UpgradeAmounts(Recipe recipe, out bool ok)
        {
            ok = recipe != null && recipe.m_resources != null;
            var sb = new System.Text.StringBuilder();
            if (!ok) return "no recipe";
            foreach (var r in recipe.m_resources)
            {
                if (r == null || r.m_resItem == null || r.m_amountPerLevel <= 0) continue;
                sb.Append(r.m_resItem.name).Append(' ');
                for (int q = 2; q <= Item.SlipperItem.MaxQuality; q++)
                {
                    int a = r.GetAmount(q);
                    sb.Append(a).Append(q < Item.SlipperItem.MaxQuality ? "/" : " ");
                    ok &= a == Item.SlipperItem.UpgradeCostPerItem;
                }
            }
            ModLog.Verbose("InGameTests: slipper upgrade amounts " + sb + "ok " + ok);
            return sb.ToString().Trim();
        }

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
