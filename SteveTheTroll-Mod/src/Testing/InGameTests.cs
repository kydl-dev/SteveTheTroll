using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HarmonyLib;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Core;
using SteveTheTroll.Features.Costume.Infrastructure;
using SteveTheTroll.Features.Slippers.Infrastructure;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Movement.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Features.Trolls.Water.Infrastructure;
using SteveTheTroll.Features.Trolls.World.Infrastructure;
using SteveTheTroll.Integrations.Unity;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Testing;

/// <summary>
///     In-game tests run by the user's Kydl.GameProbe (dev tool, never a dependency: it finds this class by its NAME
///     `InGameTests`).
///     Startup_*  run when the main menu is reached;   World_*  run 5 s after a world is loaded; both again on GameProbe's
///     RetestKey (PageDown).
///     Each method: static, no parameters, returns a text starting with PASS / FAIL / SKIP and the detail with numbers +
///     units,
///     e.g. "FAIL: coil radius 0.900 m, expected 0.180 m". null or an exception counts as FAIL. Results land in the
///     session report.
///     Keep them cheap and read-only: they also run in players' games when GameProbe is installed there (it never is for
///     players).
/// </summary>
internal static partial class InGameTests
{
    private const string ExpectedName = "Fluffy Slippers";

    /// The player config is bound and the mod is switched on.
    private static string Startup_ConfigBound()
    {
        if (ModSettings.EnableAll == null)
        {
            return "FAIL: ModSettings.Bind did not run (EnableAll is null)";
        }

        return ModSettings.On
            ? "PASS: EnableAll on, slippers " + ModSettings.SlippersOn + ", costume " + ModSettings.CostumeOn +
              ", trolls " + ModSettings.TrollsOn
            : "SKIP: EnableAll is off in the config";
    }

    /// D-166: the slippers and their trolls are baked into the bundle prefab SlipperSkinParts: the prefab loads, its attach_skin has the skinned mesh with the player's 53 bones, and the item carries it.
    private static string World_SlipperSkinAsset()
    {
        GameObject parts = BundleAssets.Load<GameObject>(BundleAssets.SlipperSkinParts);
        Transform skin = parts != null ? parts.transform.Find("attach_skin/SlipperSkin") : null;
        SkinnedMeshRenderer smr = skin != null ? skin.GetComponent<SkinnedMeshRenderer>() : null;
        GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(SlipperItem.PrefabName) : null;
        Transform onItem = prefab != null ? prefab.transform.Find("attach_skin/SlipperSkin") : null;
        int verts = smr != null && smr.sharedMesh != null ? smr.sharedMesh.vertexCount : 0,
            mats = smr != null ? smr.sharedMaterials.Length : 0,
            bones = smr != null ? smr.bones.Length : 0;
        string detail = "prefab " + (parts != null) + ", skinned mesh " + (smr != null) + " (" + verts + " verts, " +
                        mats + " materials, " + bones + " bones), on the item " + (onItem != null);
        ModLog.Verbose("InGameTests: World_SlipperSkinAsset " + detail);
        return verts > 10000 && mats == 7 && bones == 53 && onItem != null ? "PASS: " + detail : "FAIL: " + detail;
    }

    /// T-007: the slipper item exists with its stats (armor 5, max quality 4, utility, frost resistant) and has a recipe.
    private static string World_SlipperItem()
    {
        GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(SlipperItem.PrefabName) : null;
        ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        if (drop == null)
        {
            return "FAIL: " + SlipperItem.PrefabName + " not in ObjectDB";
        }

        ItemDrop.ItemData.SharedData s = drop.m_itemData.m_shared;
        bool frost = s.m_damageModifiers != null && s.m_damageModifiers.Exists(m =>
            m.m_type == HitData.DamageType.Frost && m.m_modifier == HitData.DamageModifier.Resistant);
        Recipe recipe = ObjectDB.instance.GetRecipe(drop.m_itemData);
        Transform ground = prefab.transform.Find("attach");
        int slippers = ground != null
            ? (ground.Find("SlipperDropLeft") != null ? 1 : 0) + (ground.Find("SlipperDropRight") != null ? 1 : 0)
            : 0;
        bool groundCol = ground != null && ground.GetComponent<BoxCollider>() != null;
        int hide = TrollHideAmount(recipe);
        string upgrades = UpgradeAmounts(recipe, out bool upgradesOk);
        string shown = Localization.instance != null ? Localization.instance.Localize(s.m_name) : s.m_name;
        string detail = "ground slippers " + slippers + ", ground collider " + groundCol + ", worn skin attach_skin " +
                        (prefab.transform.Find("attach_skin") != null) + ", type " + s.m_itemType + ", armor " +
                        s.m_armor + " +" + s.m_armorPerLevel + "/q, max quality " + s.m_maxQuality +
                        ", frost resistant " + frost + ", recipe " + (recipe != null) + ", TrollHide " + hide +
                        " (expected " + SlipperItem.TrollHideCost + "), upgrades q2/q3/q4 " + upgrades + " (expected " +
                        SlipperItem.UpgradeCostPerItem + " each), name " + s.m_name + " = '" + shown + "' (expected '" +
                        ExpectedName + "')";
        ModLog.Verbose("InGameTests: World_SlipperItem " + detail);
        bool ok = s.m_itemType == ItemDrop.ItemData.ItemType.Utility &&
                  Mathf.Approximately(s.m_armor, SlipperItem.BaseArmor) && s.m_maxQuality == SlipperItem.MaxQuality &&
                  frost && recipe != null && hide == SlipperItem.TrollHideCost && upgradesOk && shown == ExpectedName &&
                  slippers == 2 && groundCol &&
                  prefab.transform.Find("attach_skin") !=
                  null; // D-168: the worn slippers (with the baked trolls) ARE the attach_skin mesh
        return (ok ? "PASS: " : "FAIL: ") + detail;
    }

    // D-075 / D-076 / D-080: the four costume items, their set, the plush feet, the ground looks and the club numbers.
    private static string World_CostumeItems()
    {
        ObjectDB db = ObjectDB.instance;
        if (db == null)
        {
            return "SKIP: no ObjectDB";
        }

        var sb = new StringBuilder();
        bool ok = true;
        foreach (string n in new[] { CostumeItems.HelmetName, CostumeItems.ChestName, CostumeItems.LegsName })
        {
            ItemDrop drop = db.GetItemPrefab(n) != null ? db.GetItemPrefab(n).GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                sb.Append(n + " missing; ");
                ok = false;
                continue;
            }

            ItemDrop.ItemData.SharedData sh = drop.m_itemData.m_shared;
            bool recipe = db.GetRecipe(drop.m_itemData) != null;
            bool col = drop.GetComponent<BoxCollider>() != null;
            bool good = Mathf.Approximately(sh.m_armor, CostumeItems.Armor) &&
                        Mathf.Approximately(sh.m_armorPerLevel, CostumeItems.ArmorPerLevel) &&
                        sh.m_maxQuality == CostumeItems.MaxQuality
                        && sh.m_setName == CostumeItems.SetName && sh.m_setSize == CostumeItems.SetSize &&
                        sh.m_setStatusEffect != null && recipe && col && sh.m_icons != null && sh.m_icons.Length > 0;
            if (n == CostumeItems.LegsName)
            {
                // 2026-11-04: the costume has no shoes - the legs piece carries the trousers only, the player's own legs / feet show (CostumeBareFeet)
                Transform skin = drop.transform.Find("attach_skin");
                good &= skin != null && skin.Find("CostumeFeet") == null;
            }

            sb.Append(n + " armor " + sh.m_armor + " +" + sh.m_armorPerLevel + " q" + sh.m_maxQuality + " set '" +
                      sh.m_setName + "' " + sh.m_setSize + " recipe " + recipe + " ground collider " + col +
                      (good ? " ok; " : " WRONG; "));
            ok &= good;
        }

        bool slippersInSet = SlipperItem.Shared != null && SlipperItem.Shared.m_setName == CostumeItems.SetName;
        ItemDrop club = db.GetItemPrefab(CostumeItems.ClubName) != null
            ? db.GetItemPrefab(CostumeItems.ClubName).GetComponent<ItemDrop>()
            : null;
        bool clubOk = club != null &&
                      Mathf.Approximately(club.m_itemData.m_shared.m_damages.m_blunt, CostumeItems.ClubBlunt) &&
                      Mathf.Approximately(club.m_itemData.m_shared.m_damages.m_pickaxe, CostumeItems.ClubPickaxe)
                      && club.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon
                      && club.m_itemData.m_shared.m_secondaryAttack != null &&
                      club.m_itemData.m_shared.m_secondaryAttack.m_attackAnimation ==
                      CostumeItems.ClubSecondaryAnimation
                      && Mathf.Approximately(club.m_itemData.m_shared.m_secondaryAttack.m_damageMultiplier,
                          CostumeItems.ClubSecondaryDamage) && club.transform.Find("attach/stt_club_model") != null &&
                      db.GetRecipe(club.m_itemData) != null;
        var se = db.GetStatusEffect(CostumeItems.SetEffectName.GetStableHashCode()) as SE_Stats;
        bool seOk = se != null && se.m_icon != null && Mathf.Approximately(se.m_speedModifier, CostumeItems.SetSpeed) &&
                    se.m_addMaxCarryWeight == 0f &&
                    Mathf.Approximately(se.m_runStaminaDrainModifier, CostumeItems.SetStamina) &&
                    Mathf.Approximately(se.m_staggerModifier, -CostumeItems.SetStaggerResistance); // D-125
        sb.Append("slippers in the set " + slippersInSet + ", club " + clubOk + ", set effect " + seOk);
        ModLog.Verbose("InGameTests: World_CostumeItems " + sb);
        return (ok && slippersInSet && clubOk && seOk ? "PASS: " : "FAIL: ") + sb;
    }

    // D-076: our five recipes show once the player knows ANY of their materials (the pure rule + the local player's known recipes).
    private static string World_RecipeUnlock()
    {
        bool rule = RecipeUnlockPatch.AnyKnown(new[] { "$item_trollhide", "$item_deerhide" },
                        new HashSet<string> { "$item_deerhide" })
                    && !RecipeUnlockPatch.AnyKnown(new[] { "$item_trollhide" }, new HashSet<string> { "$item_wood" });
        Player p = Player.m_localPlayer;
        ObjectDB db = ObjectDB.instance;
        if (p == null || db == null)
        {
            return (rule ? "SKIP: " : "FAIL: ") + "rule " + rule + ", no player / ObjectDB for the known recipes";
        }

        HashSet<string> known = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownMaterial")(p);
        bool station = (bool)AccessTools.Method(typeof(Player), "KnowStationLevel")
            .Invoke(p, new object[] { "$piece_workbench", 1 }); // private in 1.0.20
        var sb = new StringBuilder("rule " + rule + ", workbench known " + station + "; ");
        bool ok = rule;
        foreach (string n in new[]
                 {
                     SlipperItem.PrefabName, CostumeItems.HelmetName, CostumeItems.ChestName, CostumeItems.LegsName,
                     CostumeItems.ClubName
                 })
        {
            ItemDrop drop = db.GetItemPrefab(n) != null ? db.GetItemPrefab(n).GetComponent<ItemDrop>() : null;
            Recipe recipe = drop != null ? db.GetRecipe(drop.m_itemData) : null;
            if (recipe == null)
            {
                sb.Append(n + " no recipe; ");
                ok = false;
                continue;
            }

            var mats = new List<string>();
            foreach (Piece.Requirement r in recipe.m_resources)
            {
                if (r != null && r.m_resItem != null)
                {
                    mats.Add(r.m_resItem.m_itemData.m_shared.m_name);
                }
            }

            bool any = RecipeUnlockPatch.AnyKnown(mats, known);
            bool isKnown = p.IsRecipeKnown(drop.m_itemData.m_shared.m_name);
            bool good = !station || !any ||
                        isKnown; // with the workbench known and a material known the recipe must be known
            sb.Append(n + " material known " + any + ", recipe known " + isKnown + (good ? "; " : " WRONG; "));
            ok &= good;
        }

        ModLog.Verbose("InGameTests: World_RecipeUnlock " + sb);
        return (ok ? "PASS: " : "FAIL: ") + sb;
    }

    // D-095 / D-097 (TEST-041: the level op was looked for in the wrong list and never found; TEST-040: the floor lift was measured as
    // 0.77 m): the game's level-ground op is found, the depot floors are lifted a few cm at most, the depot ring is 15 .. 50 m.
    private static string World_DepotGround()
    {
        bool level = DepotGround.CanLevel;
        float lift = DepotPrefab.Lift;
        float min = TrollDepot.MinFromBed, max = TrollDepot.MaxFromBed;
        bool ok = level && lift >= 0f && lift <= 0.15f && min < max;
        string detail = "level op found " + level + ", floor lift " + lift.ToString("F3") +
                        " m (max 0.15), depot ring " + min.ToString("0") + " .. " + max.ToString("0") +
                        " m, home radius "
                        + ModSettings.HomeRadius.Value.ToString("0") + " m, no-hit radius " +
                        ModSettings.NoHitRadius.Value.ToString("0") + " m";
        ModLog.Verbose("InGameTests: World_DepotGround " + detail);
        return (ok ? "PASS: " : "FAIL: ") + detail;
    }

    // D-085 (TEST-037: TrollZones passed a HashSet<int> where the game wants HashSet<SectorIndex> - an exception every frame): every method
    // the mod calls through reflection is called once here with the real argument types.
    private static string World_ReflectionCalls()
    {
        string zones = TrollZones.SelfCheck();
        bool pins = typeof(Minimap).GetMethod("AddPin") != null ||
                    AccessTools.Method(typeof(Minimap), "AddPin") != null;
        bool setCount = AccessTools.Method(typeof(Humanoid), "GetSetCount") != null;
        bool station = AccessTools.Method(typeof(Player), "KnowStationLevel") != null;
        bool areas = AccessTools.Field(typeof(MineRock5), "m_hitAreas") != null;
        string detail = "TrollZones " + zones + "; Minimap.AddPin " + pins + ", Humanoid.GetSetCount " + setCount +
                        ", Player.KnowStationLevel " + station + ", MineRock5.m_hitAreas " + areas;
        ModLog.Verbose("InGameTests: World_ReflectionCalls " + detail);
        bool ok = !zones.StartsWith("FAIL") && pins && setCount && station && areas;
        return (ok ? zones.StartsWith("SKIP") ? "SKIP: " : "PASS: " : "FAIL: ") + detail;
    }

    // TEST-012 lesson: the patch class's reflection lookups (static m_tameDeltaTime) must load without an exception.
    /// D-169: the game runs the Architecture V2 build (not the pre-refactor DLL), and the V2 movement adapter found the game's private BaseAI.MoveTo / LookAt.
    private static string Startup_Architecture()
    {
        bool v2 = Architecture.Version == 2 && Architecture.Marker == "STT-ARCH-V2";
        bool moves = ValheimTrollMovement.SelfCheck();
        ModLog.Verbose("InGameTests: architecture V" + Architecture.Version + " " + Architecture.Marker +
                       ", movement adapter delegates found " + moves);
        if (!v2)
        {
            return "FAIL: architecture marker V" + Architecture.Version + " " + Architecture.Marker +
                   " (expected V2 STT-ARCH-V2)";
        }

        return moves
            ? "PASS: architecture V2 (" + Architecture.Marker + "), movement adapter ok"
            : "FAIL: architecture V2 but ValheimTrollMovement did not find BaseAI.MoveTo / LookAt";
    }

    private static string Startup_TamingReflection()
    {
        try
        {
            string r = TrollTamingPatch.SelfCheck();
            bool speed = TamedTrollSpeed.SelfCheck(),
                stats = TamedTrollStats.SelfCheck(),
                defend = TrollDefend.SelfCheck(),
                farm = TrollFarming.SelfCheck(),
                store = BackpackStore.SelfCheck();
            bool keep = TrollWaterKeep.SelfCheck(); // D-131
            string more = ", TamedTrollSpeed " + speed + ", TamedTrollStats " + stats + ", TrollDefend " + defend +
                          ", TrollFarming " + farm + ", BackpackStore " + store + ", TrollWaterKeep " + keep;
            return r.StartsWith("PASS") && speed && stats && defend && farm && store && keep
                ? r + more
                : "FAIL: " + r + more;
        }
        catch (TypeInitializationException e)
        {
            return "FAIL: " + (e.InnerException != null ? e.InnerException.Message : e.Message);
        }
    }

    // D-050: a farming search around the player sees the sources (buffer not full) and ranks a log / rock before a standing tree.
    private static string World_FarmScan()
    {
        Player p = Player.m_localPlayer;
        if (p == null)
        {
            return "SKIP: no local player";
        }

        Collider hit;
        GameObject best = TrollFarming.Find(p.transform.position, p.transform.position, new TrollFocus.Goal("Wood"), 10,
            ModSettings.FarmRange.Value, out hit);
        string detail = "Wood within " + ModSettings.FarmRange.Value + " m: " + TrollFarming.LastScan;
        ModLog.Verbose("InGameTests: World_FarmScan " + detail);
        if (TrollFarming.LastScanFull)
        {
            return "FAIL: " + detail;
        }

        return (best == null ? "SKIP: " : "PASS: ") + detail;
    }

    // Long-range farm search (FarmLongRange, 500 m default): the zone scan runs through the reflected private ZDOMan.FindObjects without an
    // exception, finds sources beyond the near range and stays cheap (one scan, not per frame).
    private static string World_FarmFar()
    {
        Player p = Player.m_localPlayer;
        if (p == null)
        {
            return "SKIP: no local player";
        }

        Vector3 pos;
        string item;
        long t0 = Stopwatch.GetTimestamp();
        bool found = TrollFarming.FindFar(p.transform.position, new TrollFarming.SearchRange(ModSettings.FarmLongRange.Value, ModSettings.FarmMaxRange.Value), 10,
            new[] { new TrollFocus.Goal("Wood") }, null, out pos, out item);
        double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        string detail = "Wood between " + ModSettings.FarmMaxRange.Value.ToString("0") + " and " +
                        ModSettings.FarmLongRange.Value.ToString("0") + " m: " + TrollFarming.LastFar + "; " +
                        ms.ToString("0.0") + " ms (max 250)";
        ModLog.Verbose("InGameTests: World_FarmFar " + detail);
        if (ms > 250.0 || (TrollFarming.LastFar ?? "").StartsWith("FAILED"))
        {
            return "FAIL: " + detail;
        }

        if (!found)
        {
            return "SKIP: " + detail;
        }

        float d = Vector2.Distance(new Vector2(pos.x, pos.z),
            new Vector2(p.transform.position.x, p.transform.position.z));
        bool inRange = d >= ModSettings.FarmMaxRange.Value - 1f && d <= ModSettings.FarmLongRange.Value + 1f;
        return (inRange ? "PASS: " : "FAIL: ") + detail + ", nearest " + item + " at " + d.ToString("0") + " m";
    }

    // T-008a: the game's Troll prefab carries a Tameable with the taming time and E commands (follow / stay).
    private static string World_TrollTameable()
    {
        GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(TrollTaming.TrollPrefab) : null;
        Tameable t = prefab != null ? prefab.GetComponent<Tameable>() : null;
        if (t == null)
        {
            return "FAIL: " + TrollTaming.TrollPrefab + " " + (prefab != null ? "has no Tameable" : "not in ZNetScene");
        }

        bool effects = t.m_tamedEffect != null && t.m_petEffect != null && t.m_sootheEffect != null;
        string detail = "taming " + t.m_tamingTime + " s (expected " + ModSettings.TameSeconds.Value +
                        "), commandable " + t.m_commandable + ", effects " + effects
                        + ", range " + ModSettings.TameRange.Value + " m, own troll limit " +
                        ModSettings.OwnTrollRange.Value + " m";
        ModLog.Verbose("InGameTests: World_TrollTameable " + detail);
        bool ok = Mathf.Approximately(t.m_tamingTime, ModSettings.TameSeconds.Value) && t.m_commandable && effects;
        return (ok ? "PASS: " : "FAIL: ") + detail;
    }

    /// D-035: upgrade amounts of the slippers' TrollHide and Blueberries at quality 2, 3 and 4 (through the patched GetAmount).
    private static string UpgradeAmounts(Recipe recipe, out bool ok)
    {
        ok = recipe != null && recipe.m_resources != null;
        var sb = new StringBuilder();
        if (!ok)
        {
            return "no recipe";
        }

        foreach (Piece.Requirement r in recipe.m_resources)
        {
            if (r == null || r.m_resItem == null || r.m_amountPerLevel <= 0)
            {
                continue;
            }

            sb.Append(r.m_resItem.name).Append(' ');
            for (int q = 2; q <= SlipperItem.MaxQuality; q++)
            {
                int a = r.GetAmount(q);
                sb.Append(a).Append(q < SlipperItem.MaxQuality ? "/" : " ");
                ok &= a == SlipperItem.UpgradeCostPerItem;
            }
        }

        ModLog.Verbose("InGameTests: slipper upgrade amounts " + sb + "ok " + ok);
        return sb.ToString().Trim();
    }

    /// D-034: TrollHide amount in the slippers' recipe (-1 = no recipe / no TrollHide line).
    private static int TrollHideAmount(Recipe recipe)
    {
        int amount = -1;
        if (recipe != null && recipe.m_resources != null)
        {
            foreach (Piece.Requirement r in recipe.m_resources)
            {
                if (r != null && r.m_resItem != null && r.m_resItem.name == "TrollHide")
                {
                    amount = r.m_amount;
                }
            }
        }

        ModLog.Verbose("InGameTests: slipper recipe TrollHide amount " + amount);
        return amount;
    }
}
