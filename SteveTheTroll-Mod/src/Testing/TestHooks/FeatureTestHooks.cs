using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Hubs.Domain;
using SteveTheTroll.Hubs.Infrastructure;
using SteveTheTroll.Integrations.KydlCoreItems;
using SteveTheTroll.Items.Costume;

namespace SteveTheTroll.Testing
{
    /// <summary>
    /// TestLab hooks of the KydlDev spec port (MIGRATION_DECISIONS A4, B11, B13): which features run, which hub tabs and KY-Items entries SteveTheTroll has now.
    /// They read the REAL registry / KydlCore state.
    /// </summary>
    internal static class FeatureTestHooks
    {
        /// The mod's own InGameTests (startup-clean, MIGRATION_DECISIONS C21). Expected outcome: a report with PASS lines and no FAIL.
        [TestHook("stt.selftest")]
        static string SelfTest() { return TestLabSupport.RunInGameTests(typeof(InGameTests)); }

        /// Is the feature running in the FeatureRegistry ("slippers", "costume", "trolls", "hubs")?
        [TestHook("stt.featureRunning")]
        static bool FeatureRunning(string id) { return Bootstrap.ModInstaller.Current != null && Bootstrap.ModInstaller.Current.Features.IsRunning(id); }

        /// Does the KY hub list this tab of SteveTheTroll ("slippers", "costume", "mytrolls", "work", "trolls", "trollhub", "settings")?
        [TestHook("stt.hasTab")]
        static bool HasTab(string id) { return KydlCore.KyHub.HasTab(Plugin.ModGuid, id); }

        /// Is "stevethetroll:&lt;item&gt;" listed in KY-Items ("slippers", "costume-helmet", "costume-chest", "costume-legs", "club", "hub")?
        [TestHook("stt.kyItem")]
        static bool KyItem(string item) { return KyItemsContributions.Listed(item); }

        /// Applies the features' switches now (the config watcher does this on every change; a test calls it right after setConfig). Expected outcome: "ok" or the reason.
        [TestHook("stt.refreshFeatures")]
        static string RefreshFeatures()
        {
            var i = Bootstrap.ModInstaller.Current;
            if (i == null) return "no installer";
            bool ok = i.Refresh();
            ModLog.Verbose("FeatureTestHooks: refresh " + ok);
            return ok ? "ok" : "failed";
        }

        // ---- T-200 / T-201 ---------------------------------------------------------------------------------------------------------------------------------

        static readonly string[] CostumePrefabs = { CostumeItems.HelmetName, CostumeItems.ChestName, CostumeItems.LegsName, CostumeItems.ClubName };

        /// T-200: the name of the Troll Hub piece's build menu icon ("stevethetroll_hub_icon" = the HUB plaque; anything else = the fallback).
        [TestHook("stt.hubIcon")]
        static string HubIcon()
        {
            var go = Jotunn.Managers.PrefabManager.Instance.GetPrefab(HubData.PrefabName);
            var piece = go != null ? go.GetComponent<Piece>() : null;
            return piece == null ? "no hub piece" : piece.m_icon == null ? "no icon" : piece.m_icon.name;
        }

        /// T-201: has the local character worn the slippers once (the mark saved with the character)?
        [TestHook("stt.slippersWornOnce")]
        static bool SlippersWornOnce() { return CostumeUnlock.WornOnce(Player.m_localPlayer); }

        /// T-201: how many of the 4 costume recipes (head, suit, trousers, club) the local character knows.
        [TestHook("stt.costumeRecipesKnown")]
        static int CostumeRecipesKnown()
        {
            var p = Player.m_localPlayer;
            if (p == null || ObjectDB.instance == null) return -1;
            CostumeUnlock.RefreshRecipes(p);
            int n = 0;
            foreach (var prefab in CostumePrefabs)
            {
                var go = ObjectDB.instance.GetItemPrefab(prefab);
                var drop = go != null ? go.GetComponent<ItemDrop>() : null;
                if (drop != null && p.IsRecipeKnown(drop.m_itemData.m_shared.m_name)) n++;
            }
            ModLog.Verbose("FeatureTestHooks: " + n + " of " + CostumePrefabs.Length + " costume recipes known, slippers worn once " + CostumeUnlock.WornOnce(p));
            return n;
        }

        /// T-201 test setup: the character has NOT worn the slippers yet, knows no costume recipe, but knows every material of them (so only the T-201 rule keeps
        /// them hidden). Expected outcome: "forgotten: 4 recipe(s) removed, N material(s) known".
        [TestHook("stt.forgetCostume")]
        static string ForgetCostume()
        {
            var p = Player.m_localPlayer;
            if (p == null || ObjectDB.instance == null) return "no player";
            var recipes = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownRecipes")(p);
            var materials = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownMaterial")(p);
            CostumeUnlock.Forget(p);
            int removed = 0, mats = 0;
            foreach (var prefab in CostumePrefabs)
            {
                var go = ObjectDB.instance.GetItemPrefab(prefab);
                var drop = go != null ? go.GetComponent<ItemDrop>() : null;
                if (drop == null) continue;
                if (recipes.Remove(drop.m_itemData.m_shared.m_name)) removed++;
                var recipe = ObjectDB.instance.GetRecipe(drop.m_itemData);
                if (recipe == null || recipe.m_resources == null) continue;
                foreach (var r in recipe.m_resources)
                    if (r != null && r.m_resItem != null && materials.Add(r.m_resItem.m_itemData.m_shared.m_name)) mats++;
            }
            CostumeUnlock.RefreshRecipes(p);
            return "forgotten: " + removed + " recipe(s) removed, " + mats + " material(s) newly known, worn once " + CostumeUnlock.WornOnce(p);
        }
    }
}
