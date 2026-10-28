using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;

namespace SteveTheTroll.Item
{
    /// <summary>
    /// D-076 (user 2026-10-28: "all the items have to get unlocked when Troll Hide (or any of the other items in the recipes) is being
    /// collected"): the game shows a recipe only once the player knows EVERY material in it (Player.HaveRequirementItems with discover = true,
    /// 1.0.20). For our items (Fluffy Slippers, the three costume pieces, the Troll Club) knowing ANY one of their materials is enough.
    /// The crafting station rule stays the game's (a workbench must be known). Paying for the item is unchanged (discover = false).
    /// </summary>
    [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
    internal static class RecipeUnlockPatch
    {
        static readonly AccessTools.FieldRef<Player, HashSet<string>> Known = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownMaterial");
        static readonly HashSet<string> Ours = new HashSet<string>
        {
            SlipperItem.PrefabName, CostumeItems.HelmetName, CostumeItems.ChestName, CostumeItems.LegsName, CostumeItems.ClubName,
        };
        static readonly HashSet<string> Logged = new HashSet<string>();

        /// Pure rule: a recipe is discovered when any of its materials is known. Expected outcome: true for one known material.
        internal static bool AnyKnown(IEnumerable<string> materials, ICollection<string> known)
        {
            if (materials == null || known == null) return false;
            foreach (var m in materials) if (m != null && known.Contains(m)) return true;
            return false;
        }

        static void Postfix(Player __instance, Recipe piece, bool discover, ref bool __result)
        {
            if (!discover || __result || piece == null || piece.m_item == null || !Ours.Contains(piece.m_item.name) || piece.m_resources == null) return;
            var known = Known(__instance);
            var names = new List<string>();
            foreach (var r in piece.m_resources) if (r != null && r.m_resItem != null) names.Add(r.m_resItem.m_itemData.m_shared.m_name);
            if (!AnyKnown(names, known)) return;
            __result = true;
            if (Logged.Add(piece.m_item.name))
                ModLog.Verbose("RecipeUnlockPatch: " + piece.m_item.name + " discovered by " + __instance.GetPlayerName() + " (knows " + string.Join(", ", names.FindAll(known.Contains)) + " of " + string.Join(", ", names) + ")");
        }
    }
}
