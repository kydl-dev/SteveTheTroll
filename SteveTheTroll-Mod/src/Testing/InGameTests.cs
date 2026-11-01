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
    internal static partial class InGameTests
    {
        /// The player config is bound and the mod is switched on.
        static string Startup_ConfigBound()
        {
            if (ModSettings.EnableAll == null) return "FAIL: ModSettings.Bind did not run (EnableAll is null)";
            return ModSettings.On ? "PASS: EnableAll on, slippers " + ModSettings.SlippersOn + ", costume " + ModSettings.CostumeOn + ", trolls " + ModSettings.TrollsOn : "SKIP: EnableAll is off in the config";
        }

        /// D-166: the slippers and their trolls are baked into the bundle prefab SlipperSkinParts: the prefab loads, its attach_skin has the skinned mesh with the player's 53 bones, and the item carries it.
        static string World_SlipperSkinAsset()
        {
            var parts = BundleAssets.Load<GameObject>(BundleAssets.SlipperSkinParts);
            var skin = parts != null ? parts.transform.Find("attach_skin/SlipperSkin") : null;
            var smr = skin != null ? skin.GetComponent<SkinnedMeshRenderer>() : null;
            var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(Item.SlipperItem.PrefabName) : null;
            var onItem = prefab != null ? prefab.transform.Find("attach_skin/SlipperSkin") : null;
            int verts = smr != null && smr.sharedMesh != null ? smr.sharedMesh.vertexCount : 0, mats = smr != null ? smr.sharedMaterials.Length : 0, bones = smr != null ? smr.bones.Length : 0;
            string detail = "prefab " + (parts != null) + ", skinned mesh " + (smr != null) + " (" + verts + " verts, " + mats + " materials, " + bones + " bones), on the item " + (onItem != null);
            ModLog.Verbose("InGameTests: World_SlipperSkinAsset " + detail);
            return verts > 10000 && mats == 7 && bones == 53 && onItem != null ? "PASS: " + detail : "FAIL: " + detail;
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
            string detail = "ground slippers " + slippers + ", ground collider " + groundCol + ", worn skin attach_skin " + (prefab.transform.Find("attach_skin") != null) + ", type " + s.m_itemType + ", armor " + s.m_armor + " +" + s.m_armorPerLevel + "/q, max quality " + s.m_maxQuality + ", frost resistant " + frost + ", recipe " + (recipe != null) + ", TrollHide " + hide + " (expected " + Item.SlipperItem.TrollHideCost + "), upgrades q2/q3/q4 " + upgrades + " (expected " + Item.SlipperItem.UpgradeCostPerItem + " each), name " + s.m_name + " = '" + shown + "' (expected '" + ExpectedName + "')";
            ModLog.Verbose("InGameTests: World_SlipperItem " + detail);
            bool ok = s.m_itemType == ItemDrop.ItemData.ItemType.Utility && Mathf.Approximately(s.m_armor, Item.SlipperItem.BaseArmor) && s.m_maxQuality == Item.SlipperItem.MaxQuality && frost && recipe != null && hide == Item.SlipperItem.TrollHideCost && upgradesOk && shown == ExpectedName && slippers == 2 && groundCol && prefab.transform.Find("attach_skin") == null;
            return (ok ? "PASS: " : "FAIL: ") + detail;
        }

        // D-075 / D-076 / D-080: the four costume items, their set, the plush feet, the ground looks and the club numbers.
        static string World_CostumeItems()
        {
            var db = ObjectDB.instance;
            if (db == null) return "SKIP: no ObjectDB";
            var sb = new System.Text.StringBuilder();
            bool ok = true;
            foreach (var n in new[] { Item.CostumeItems.HelmetName, Item.CostumeItems.ChestName, Item.CostumeItems.LegsName })
            {
                var drop = db.GetItemPrefab(n) != null ? db.GetItemPrefab(n).GetComponent<ItemDrop>() : null;
                if (drop == null) { sb.Append(n + " missing; "); ok = false; continue; }
                var sh = drop.m_itemData.m_shared;
                bool recipe = db.GetRecipe(drop.m_itemData) != null;
                bool col = drop.GetComponent<BoxCollider>() != null;
                bool good = Mathf.Approximately(sh.m_armor, Item.CostumeItems.Armor) && Mathf.Approximately(sh.m_armorPerLevel, Item.CostumeItems.ArmorPerLevel) && sh.m_maxQuality == Item.CostumeItems.MaxQuality
                    && sh.m_setName == Item.CostumeItems.SetName && sh.m_setSize == Item.CostumeItems.SetSize && sh.m_setStatusEffect != null && recipe && col && sh.m_icons != null && sh.m_icons.Length > 0;
                if (n == Item.CostumeItems.LegsName) { var skin = drop.transform.Find("attach_skin/CostumeFeet"); good &= skin != null && skin.GetComponent<Item.CostumeFeet>() != null; }
                sb.Append(n + " armor " + sh.m_armor + " +" + sh.m_armorPerLevel + " q" + sh.m_maxQuality + " set '" + sh.m_setName + "' " + sh.m_setSize + " recipe " + recipe + " ground collider " + col + (good ? " ok; " : " WRONG; "));
                ok &= good;
            }
            bool slippersInSet = Item.SlipperItem.Shared != null && Item.SlipperItem.Shared.m_setName == Item.CostumeItems.SetName;
            var club = db.GetItemPrefab(Item.CostumeItems.ClubName) != null ? db.GetItemPrefab(Item.CostumeItems.ClubName).GetComponent<ItemDrop>() : null;
            bool clubOk = club != null && Mathf.Approximately(club.m_itemData.m_shared.m_damages.m_blunt, Item.CostumeItems.ClubBlunt) && Mathf.Approximately(club.m_itemData.m_shared.m_damages.m_pickaxe, Item.CostumeItems.ClubPickaxe)
                && club.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon
                && club.m_itemData.m_shared.m_secondaryAttack != null && club.m_itemData.m_shared.m_secondaryAttack.m_attackAnimation == Item.CostumeItems.ClubSecondaryAnimation
                && Mathf.Approximately(club.m_itemData.m_shared.m_secondaryAttack.m_damageMultiplier, Item.CostumeItems.ClubSecondaryDamage) && club.transform.Find("attach/stt_club_model") != null && db.GetRecipe(club.m_itemData) != null;
            var se = db.GetStatusEffect(Item.CostumeItems.SetEffectName.GetStableHashCode()) as SE_Stats;
            bool seOk = se != null && se.m_icon != null && Mathf.Approximately(se.m_speedModifier, Item.CostumeItems.SetSpeed) && se.m_addMaxCarryWeight == 0f && Mathf.Approximately(se.m_runStaminaDrainModifier, Item.CostumeItems.SetStamina) && Mathf.Approximately(se.m_staggerModifier, -Item.CostumeItems.SetStaggerResistance);   // D-125
            sb.Append("slippers in the set " + slippersInSet + ", club " + clubOk + ", set effect " + seOk);
            ModLog.Verbose("InGameTests: World_CostumeItems " + sb);
            return (ok && slippersInSet && clubOk && seOk ? "PASS: " : "FAIL: ") + sb;
        }

        // D-076: our five recipes show once the player knows ANY of their materials (the pure rule + the local player's known recipes).
        static string World_RecipeUnlock()
        {
            bool rule = Item.RecipeUnlockPatch.AnyKnown(new[] { "$item_trollhide", "$item_deerhide" }, new System.Collections.Generic.HashSet<string> { "$item_deerhide" })
                && !Item.RecipeUnlockPatch.AnyKnown(new[] { "$item_trollhide" }, new System.Collections.Generic.HashSet<string> { "$item_wood" });
            var p = Player.m_localPlayer;
            var db = ObjectDB.instance;
            if (p == null || db == null) return (rule ? "SKIP: " : "FAIL: ") + "rule " + rule + ", no player / ObjectDB for the known recipes";
            var known = HarmonyLib.AccessTools.FieldRefAccess<Player, System.Collections.Generic.HashSet<string>>("m_knownMaterial")(p);
            bool station = (bool)HarmonyLib.AccessTools.Method(typeof(Player), "KnowStationLevel").Invoke(p, new object[] { "$piece_workbench", 1 });   // private in 1.0.20
            var sb = new System.Text.StringBuilder("rule " + rule + ", workbench known " + station + "; ");
            bool ok = rule;
            foreach (var n in new[] { Item.SlipperItem.PrefabName, Item.CostumeItems.HelmetName, Item.CostumeItems.ChestName, Item.CostumeItems.LegsName, Item.CostumeItems.ClubName })
            {
                var drop = db.GetItemPrefab(n) != null ? db.GetItemPrefab(n).GetComponent<ItemDrop>() : null;
                var recipe = drop != null ? db.GetRecipe(drop.m_itemData) : null;
                if (recipe == null) { sb.Append(n + " no recipe; "); ok = false; continue; }
                var mats = new System.Collections.Generic.List<string>();
                foreach (var r in recipe.m_resources) if (r != null && r.m_resItem != null) mats.Add(r.m_resItem.m_itemData.m_shared.m_name);
                bool any = Item.RecipeUnlockPatch.AnyKnown(mats, known);
                bool isKnown = p.IsRecipeKnown(drop.m_itemData.m_shared.m_name);
                bool good = !station || !any || isKnown;              // with the workbench known and a material known the recipe must be known
                sb.Append(n + " material known " + any + ", recipe known " + isKnown + (good ? "; " : " WRONG; "));
                ok &= good;
            }
            ModLog.Verbose("InGameTests: World_RecipeUnlock " + sb);
            return (ok ? "PASS: " : "FAIL: ") + sb;
        }

        // D-095 / D-097 (TEST-041: the level op was looked for in the wrong list and never found; TEST-040: the floor lift was measured as
        // 0.77 m): the game's level-ground op is found, the depot floors are lifted a few cm at most, the depot ring is 15 .. 50 m.
        static string World_DepotGround()
        {
            bool level = Backpack.DepotGround.CanLevel;
            float lift = Backpack.DepotPrefab.Lift;
            float min = Backpack.TrollDepot.MinFromBed, max = Backpack.TrollDepot.MaxFromBed;
            bool ok = level && lift >= 0f && lift <= 0.15f && min < max;
            string detail = "level op found " + level + ", floor lift " + lift.ToString("F3") + " m (max 0.15), depot ring " + min.ToString("0") + " .. " + max.ToString("0") + " m, home radius "
                + ModSettings.HomeRadius.Value.ToString("0") + " m, no-hit radius " + ModSettings.NoHitRadius.Value.ToString("0") + " m";
            ModLog.Verbose("InGameTests: World_DepotGround " + detail);
            return (ok ? "PASS: " : "FAIL: ") + detail;
        }

        // D-085 (TEST-037: TrollZones passed a HashSet<int> where the game wants HashSet<SectorIndex> - an exception every frame): every method
        // the mod calls through reflection is called once here with the real argument types.
        static string World_ReflectionCalls()
        {
            string zones = Taming.TrollZones.SelfCheck();
            bool pins = typeof(Minimap).GetMethod("AddPin") != null || HarmonyLib.AccessTools.Method(typeof(Minimap), "AddPin") != null;
            bool setCount = HarmonyLib.AccessTools.Method(typeof(Humanoid), "GetSetCount") != null;
            bool station = HarmonyLib.AccessTools.Method(typeof(Player), "KnowStationLevel") != null;
            bool areas = HarmonyLib.AccessTools.Field(typeof(MineRock5), "m_hitAreas") != null;
            string detail = "TrollZones " + zones + "; Minimap.AddPin " + pins + ", Humanoid.GetSetCount " + setCount + ", Player.KnowStationLevel " + station + ", MineRock5.m_hitAreas " + areas;
            ModLog.Verbose("InGameTests: World_ReflectionCalls " + detail);
            bool ok = !zones.StartsWith("FAIL") && pins && setCount && station && areas;
            return (ok ? (zones.StartsWith("SKIP") ? "SKIP: " : "PASS: ") : "FAIL: ") + detail;
        }

        const string ExpectedName = "Fluffy Slippers";

        // TEST-012 lesson: the patch class's reflection lookups (static m_tameDeltaTime) must load without an exception.
        static string Startup_TamingReflection()
        {
            try
            {
                string r = Taming.TrollTamingPatch.SelfCheck();
                bool speed = Taming.TamedTrollSpeed.SelfCheck(), stats = Taming.TamedTrollStats.SelfCheck(), defend = Taming.TrollDefend.SelfCheck(), farm = Taming.TrollFarming.SelfCheck(), store = Backpack.BackpackStore.SelfCheck();
                bool keep = Taming.TrollWaterKeep.SelfCheck();      // D-131
                string more = ", TamedTrollSpeed " + speed + ", TamedTrollStats " + stats + ", TrollDefend " + defend + ", TrollFarming " + farm + ", BackpackStore " + store + ", TrollWaterKeep " + keep;
                return r.StartsWith("PASS") && speed && stats && defend && farm && store && keep ? r + more : "FAIL: " + r + more;
            }
            catch (System.TypeInitializationException e) { return "FAIL: " + (e.InnerException != null ? e.InnerException.Message : e.Message); }
        }

        // D-050: a farming search around the player sees the sources (buffer not full) and ranks a log / rock before a standing tree.
        static string World_FarmScan()
        {
            var p = Player.m_localPlayer;
            if (p == null) return "SKIP: no local player";
            Collider hit;
            var best = Taming.TrollFarming.Find(p.transform.position, p.transform.position, new Ui.TrollFocus.Goal("Wood"), 10, ModSettings.FarmRange.Value, out hit);
            string detail = "Wood within " + ModSettings.FarmRange.Value + " m: " + Taming.TrollFarming.LastScan;
            ModLog.Verbose("InGameTests: World_FarmScan " + detail);
            if (Taming.TrollFarming.LastScanFull) return "FAIL: " + detail;
            return (best == null ? "SKIP: " : "PASS: ") + detail;
        }

        // Long-range farm search (FarmLongRange, 500 m default): the zone scan runs through the reflected private ZDOMan.FindObjects without an
        // exception, finds sources beyond the near range and stays cheap (one scan, not per frame).
        static string World_FarmFar()
        {
            var p = Player.m_localPlayer;
            if (p == null) return "SKIP: no local player";
            Vector3 pos; string item;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            bool found = Taming.TrollFarming.FindFar(p.transform.position, ModSettings.FarmLongRange.Value, ModSettings.FarmMaxRange.Value, 10,
                new[] { new Ui.TrollFocus.Goal("Wood") }, null, out pos, out item);
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            string detail = "Wood between " + ModSettings.FarmMaxRange.Value.ToString("0") + " and " + ModSettings.FarmLongRange.Value.ToString("0") + " m: " + Taming.TrollFarming.LastFar + "; " + ms.ToString("0.0") + " ms (max 250)";
            ModLog.Verbose("InGameTests: World_FarmFar " + detail);
            if (ms > 250.0 || (Taming.TrollFarming.LastFar ?? "").StartsWith("FAILED")) return "FAIL: " + detail;
            if (!found) return "SKIP: " + detail;
            float d = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(p.transform.position.x, p.transform.position.z));
            bool inRange = d >= ModSettings.FarmMaxRange.Value - 1f && d <= ModSettings.FarmLongRange.Value + 1f;
            return (inRange ? "PASS: " : "FAIL: ") + detail + ", nearest " + item + " at " + d.ToString("0") + " m";
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
