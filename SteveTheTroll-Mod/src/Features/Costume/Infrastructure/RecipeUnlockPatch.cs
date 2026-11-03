using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Features.Costume.Domain;
using SteveTheTroll.Features.Slippers.Infrastructure;

namespace SteveTheTroll.Features.Costume.Infrastructure
{
    /// <summary>
    /// D-076 (user 2026-10-28: "all the items have to get unlocked when Troll Hide (or any of the other items in the recipes) is being
    /// collected"): the game shows a recipe only once the player knows EVERY material in it (Player.HaveRequirementItems with discover = true,
    /// 1.0.20). For our items (Fluffy Slippers, the three costume pieces, the Troll Club) knowing ANY one of their materials is enough.
    /// The crafting station rule stays the game's (a workbench must be known). Paying for the item is unchanged (discover = false).
    /// </summary>
    // Harmony hook: src/Integrations/Harmony/CostumePatches.cs, RecipeUnlockPatchHook (MIGRATION_DECISIONS A5)
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

        // T-201: the costume pieces (not the slippers) are shown only after the slippers were equipped once (CostumeUnlockRules)
        static readonly HashSet<string> CostumePieces = new HashSet<string> { CostumeItems.HelmetName, CostumeItems.ChestName, CostumeItems.LegsName, CostumeItems.ClubName };
        static readonly HashSet<string> Held = new HashSet<string>();

        internal static void Postfix(Player __instance, Recipe piece, bool discover, ref bool __result)
        {
            if (!discover || piece == null || piece.m_item == null || !Ours.Contains(piece.m_item.name) || piece.m_resources == null) return;
            bool costume = CostumePieces.Contains(piece.m_item.name);
            if (__result && !costume) return;
            var known = Known(__instance);
            var names = new List<string>();
            foreach (var r in piece.m_resources) if (r != null && r.m_resItem != null) names.Add(r.m_resItem.m_itemData.m_shared.m_name);
            bool any = AnyKnown(names, known);
            bool before = __result;
            if (costume)
            {
                bool worn = CostumeUnlock.WornOnce(__instance);
                __result = CostumeUnlockRules.Discoverable(before, any, worn);
                if (!__result && (before || any) && Held.Add(piece.m_item.name))
                    ModLog.Verbose("RecipeUnlockPatch: " + piece.m_item.name + " held back for " + __instance.GetPlayerName() + " - the slippers were never equipped (T-201; game " + before + ", a material known " + any + ")");
                if (__result) Held.Remove(piece.m_item.name);
            }
            else if (any) __result = true;
            if (__result && !before && Logged.Add(piece.m_item.name))
                ModLog.Verbose("RecipeUnlockPatch: " + piece.m_item.name + " discovered by " + __instance.GetPlayerName() + " (knows " + string.Join(", ", names.FindAll(known.Contains)) + " of " + string.Join(", ", names) + ")");
        }
    }
}
