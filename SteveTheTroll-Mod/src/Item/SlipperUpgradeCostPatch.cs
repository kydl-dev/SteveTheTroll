using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;

namespace SteveTheTroll.Item
{
    /// <summary>
    /// D-035: constant upgrade cost for the slippers only (UpgradeCost). Postfix on Piece.Requirement.GetAmount, which the crafting GUI,
    /// Player.HaveRequirementItems and Player.ConsumeResources all use (1.0.20), so the shown cost and the paid cost stay the same.
    /// The slippers' Requirement objects are found by reference in ObjectDB's recipe (re-read when ObjectDB or its recipe count changes);
    /// every other recipe keeps the vanilla amounts.
    /// </summary>
    internal static class SlipperUpgradeCostPatch
    {
        static readonly HashSet<Piece.Requirement> Ours = new HashSet<Piece.Requirement>();
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
                if (r == null || r.m_item == null || r.m_item.name != SlipperItem.PrefabName || r.m_resources == null) continue;
                foreach (var req in r.m_resources) if (req != null) Ours.Add(req);
            }
            ModLog.Verbose("SlipperUpgradeCostPatch: ObjectDB recipes " + count + ", slipper requirements tracked " + Ours.Count);
            if (count > 0) ModLog.Check("SlipperUpgradeCostPatch: slipper requirements", Ours.Count, SlipperItem.RequirementCount, 0, "");
            return Ours.Count;
        }

        [HarmonyPatch(typeof(Piece.Requirement), nameof(Piece.Requirement.GetAmount))]
        static class GetAmount
        {
            static void Postfix(Piece.Requirement __instance, int qualityLevel, ref int __result)
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
