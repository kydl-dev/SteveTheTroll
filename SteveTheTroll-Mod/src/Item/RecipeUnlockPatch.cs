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
        // made on first use, not in the type initializer: the pure rules below must work without the game (G14 coverage run: the field
        // accessor failed to build under the coverage tool and took the whole class down with a TypeInitializationException)
        static AccessTools.FieldRef<Player, HashSet<string>> _known;
        static HashSet<string> Known(Player p)
        {
            if (_known == null) _known = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownMaterial");
            return _known(p);
        }
        static readonly HashSet<string> Ours = new HashSet<string>
        {
            SlipperItem.PrefabName, CostumeItems.HelmetName, CostumeItems.ChestName, CostumeItems.LegsName, CostumeItems.ClubName,
        };
        static readonly HashSet<string> Logged = new HashSet<string>();

        /// D-088 pure rule: every one of `items` is known (an empty / null list counts as not all known). Expected outcome: true only when all are.
        internal static bool AllKnown(ICollection<string> items, ICollection<string> known)
        {
            if (items == null || known == null || items.Count == 0) return false;
            foreach (var m in items) if (m == null || !known.Contains(m)) return false;
            return true;
        }

        /// D-088 (user: "the costume tab should only be visible after all parts of the costume are crafted"): the player has had all four
        /// costume items (head, suit, trousers, club) in the inventory - crafting puts them there; the game remembers it per character
        /// (m_knownMaterial, saved with the character). Expected outcome: true when all four are known.
        internal static bool CostumeComplete(Player p, ObjectDB db)
        {
            if (p == null || db == null) return false;
            var names = new List<string>();
            foreach (var prefab in new[] { CostumeItems.HelmetName, CostumeItems.ChestName, CostumeItems.LegsName, CostumeItems.ClubName })
            {
                var go = db.GetItemPrefab(prefab);
                var drop = go != null ? go.GetComponent<ItemDrop>() : null;
                names.Add(drop != null ? drop.m_itemData.m_shared.m_name : null);
            }
            var known = Known(p);
            bool all = AllKnown(names, known);
            string missing = all ? "" : string.Join(", ", names.FindAll(n => n == null || known == null || !known.Contains(n)).ConvertAll(n => n ?? "(item missing)").ToArray());
            if (missing != _lastMissing)                                  // asked by every tooltip frame: log only changes
            {
                _lastMissing = missing;
                ModLog.Verbose("RecipeUnlockPatch: costume " + (all ? "complete" : "not complete") + " for " + p.GetPlayerName() + (all ? "" : " - not known yet: " + missing));
            }
            return all;
        }
        static string _lastMissing;

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
