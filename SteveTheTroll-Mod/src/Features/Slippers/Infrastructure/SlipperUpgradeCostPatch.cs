using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Backpack.Domain;

namespace SteveTheTroll.Items.Slippers
{
    /// <summary>
    /// D-035: constant upgrade cost for the slippers and (D-070 / D-071) the costume pieces and the club (UpgradeCost). Postfix on Piece.Requirement.GetAmount, which the crafting GUI,
    /// Player.HaveRequirementItems and Player.ConsumeResources all use (1.0.20), so the shown cost and the paid cost stay the same.
    /// The slippers' Requirement objects are found by reference in ObjectDB's recipe (re-read when ObjectDB or its recipe count changes);
    /// every other recipe keeps the vanilla amounts.
    /// </summary>
    internal static class SlipperUpgradeCostPatch
    {
        static readonly HashSet<Piece.Requirement> Ours = new HashSet<Piece.Requirement>();
        /// Items whose upgrades cost the same at every quality: the slippers (D-035) and the costume pieces + club (D-070 / D-071).
        static readonly HashSet<string> Items = new HashSet<string> { SlipperItem.PrefabName };
        static int _expected = SlipperItem.RequirementCount;

        /// Adds an item (its prefab name and how many requirements its recipe has) to the constant-upgrade-cost list.
        internal static void Track(string prefabName, int requirements)
        {
            if (Items.Add(prefabName)) { _expected += requirements; _db = null; }
        }
        static ObjectDB _db;
        static int _recipeCount = -1;
        static int _logs;

        /// Re-reads the slippers' requirements when ObjectDB changed. Expected outcome: returns how many requirements are tracked (3 in a world).
        static int Refresh()
        {
            var db = ObjectDB.instance;
            int count = db != null && db.m_recipes != null ? db.m_recipes.Count : -1;
            if (db == _db && count == _recipeCount) return Ours.Count;
            _db = db;
            _recipeCount = count;
            Ours.Clear();
            if (db == null || db.m_recipes == null) { ModLog.Verbose("SlipperUpgradeCostPatch: no ObjectDB recipes yet"); return Ours.Count; }
            foreach (var r in db.m_recipes)
            {
                if (r == null || r.m_item == null || !Items.Contains(r.m_item.name) || r.m_resources == null) continue;
                foreach (var req in r.m_resources) if (req != null) Ours.Add(req);
            }
            ModLog.Verbose("SlipperUpgradeCostPatch: ObjectDB recipes " + count + ", requirements tracked " + Ours.Count + " for " + string.Join(", ", Items));
            if (count > 0) ModLog.Check("SlipperUpgradeCostPatch: constant-cost requirements", Ours.Count, _expected, 0, "");
            return Ours.Count;
        }

        // Harmony hook: src/Integrations/Harmony/SlippersPatches.cs, SlipperUpgradeCostPatch_GetAmount (MIGRATION_DECISIONS A5)
        internal static class GetAmount
        {
            internal static void Postfix(Piece.Requirement __instance, int qualityLevel, ref int __result)
            {
                if (qualityLevel <= 1 || __instance == null) return;
                if (Refresh() == 0 || !Ours.Contains(__instance)) return;
                int vanilla = __result;
                __result = UpgradeCost.Amount(qualityLevel, __instance.m_amount, __instance.m_amountPerLevel, __instance.m_upgraderResource);
                if (_logs < 12 && vanilla != __result)
                {
                    _logs++;
                    ModLog.Verbose("SlipperUpgradeCostPatch: " + (__instance.m_resItem != null ? __instance.m_resItem.name : "?") + " for quality " + qualityLevel + ": " + __result + " (vanilla " + vanilla + ")");
                }
            }
        }
    }
}
