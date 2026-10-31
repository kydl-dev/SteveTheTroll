using System;
using System.Collections.Generic;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// Troll work tab (D-039, D-058): what the player's tamed troll focus-farms. "" = no focus: the default priority (D-058, user 2026-10-27).
    /// Values are the game's item prefab names; the menu shows the item's English name (what is farmed, not where it comes from).
    /// Pure logic (unit-tested: TrollFocusTests).
    /// </summary>
    internal static class TrollFocus
    {
        internal const string None = "";

        /// Choices in menu order (the default priority order, each item once): value, text key.
        internal static readonly IList<KeyValuePair<string, string>> Choices = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>(None, "stevethetroll_focus_none"),
            new KeyValuePair<string, string>("YggdrasilWood", "stevethetroll_focus_yggdrasilwood"),
            new KeyValuePair<string, string>("ElderBark", "stevethetroll_focus_elderbark"),
            new KeyValuePair<string, string>("IronScrap", "stevethetroll_focus_ironscrap"),
            new KeyValuePair<string, string>("FineWood", "stevethetroll_focus_finewood"),
            new KeyValuePair<string, string>("SilverOre", "stevethetroll_focus_silver"),
            new KeyValuePair<string, string>("CopperOre", "stevethetroll_focus_copper"),
            new KeyValuePair<string, string>("RoundLog", "stevethetroll_focus_corewood"),
            new KeyValuePair<string, string>("TinOre", "stevethetroll_focus_tin"),
            new KeyValuePair<string, string>("Wood", "stevethetroll_focus_wood"),
        };

        /// D-126 (user: "make this stuff be visible in the drop down menu if the trolls are in those biomes"): items offered only while the
        /// troll is in the biome (the game's Heightmap.Biome name). Value, text key, biome. Not in the default priority (D-058 stays).
        internal static readonly IList<string[]> BiomeChoices = new List<string[]>
        {
            new[] { "Blackwood", "stevethetroll_focus_blackwood", "AshLands" },      // scorched trees
            new[] { "FlametalOreNew", "stevethetroll_focus_flametal", "AshLands" },  // D-153: Flametal (item prefab FlametalOreNew; the old FlametalOre can no longer be obtained) from the Ashlands veins
            new[] { "BlackMarble", "stevethetroll_focus_blackmarble", "Mistlands" },  // giant remains (skull / ribs -> frac)
            new[] { "Frostwood", "stevethetroll_focus_timberwood", "DeepNorth" },    // Timberwood (item prefab Frostwood) from Deep North trees
            new[] { "GoldOre", "stevethetroll_focus_goldore", "DeepNorth" },         // petrified Gammeltroll (DN_gammeltrollFrac*)
        };

        /// The drop-down values for a troll in `biome`: every Choice, then the biome's items, then `current` when it is a biome item of
        /// another biome (so a troll that walked out keeps showing its focus). Expected outcome: never empty, no duplicates.
        internal static List<string> ChoicesFor(string biome, string current)
        {
            var list = new List<string>();
            foreach (var c in Choices) list.Add(c.Key);
            foreach (var b in BiomeChoices) if (string.Equals(b[2], biome, StringComparison.OrdinalIgnoreCase) && !list.Contains(b[0])) list.Add(b[0]);
            string cur = Normalize(current);
            if (cur.Length > 0 && !list.Contains(cur)) list.Add(cur);
            return list;
        }

        /// One step of the default priority: the item and the source prefabs it is taken from (name starts with one of them, any case;
        /// empty = any source that drops the item).
        internal struct Goal
        {
            internal readonly string Item;
            internal readonly string[] Sources;
            internal Goal(string item, params string[] sources) { Item = item; Sources = sources ?? new string[0]; }
        }

        /// D-058: no focus -> the first of these found in range (user 2026-10-27, in this order).
        internal static readonly Goal[] DefaultOrder =
        {
            new Goal("YggdrasilWood"),                      // Yggdrasil shoot
            new Goal("ElderBark"),                          // Ancient tree
            new Goal("IronScrap", "mudpile"),               // mudpile_beacon / mudpile_frac (and the plain muddy scrap piles)
            new Goal("FineWood", "Birch"),                  // birch trees and their logs
            new Goal("SilverOre", "silvervein"),            // silvervein / silvervein_frac
            new Goal("CopperOre", "rock4_copper"),          // rock4_copper / rock4_copper_frac
            new Goal("RoundLog", "Pinetree"),               // pine trees (core wood)
            new Goal("FineWood", "Oak"),                    // oak trees and their logs
            new Goal("TinOre", "MineRock_Tin"),             // tin deposits
            new Goal("Wood"),                               // wood from any source
        };

        /// D-062 (user spec): what a tamed troll's backpack picks up within 50 m - wood, ores, scrap iron, black metal scrap (every farmed item).
        internal static readonly string[] PickupItems =
        {
            "Wood", "FineWood", "RoundLog", "ElderBark", "YggdrasilWood", "Blackwood",
            "CopperOre", "TinOre", "IronOre", "SilverOre", "CopperScrap", "IronScrap", "BlackMetalScrap", "FlametalOre", "FlametalOreNew",
            "BlackMarble", "Frostwood", "GoldOre",                                    // D-126: the biome focus items (Frostwood = Timberwood)
        };

        /// The source prefab (name without "(Clone)") counts for the goal. Expected outcome: true when the goal has no source list or the
        /// name starts with one of its sources (any case).
        internal static bool SourceMatches(Goal goal, string prefabName)
        {
            if (goal.Sources.Length == 0) return true;
            if (string.IsNullOrEmpty(prefabName)) return false;
            foreach (var s in goal.Sources) if (prefabName.StartsWith(s, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// The search order of a troll: its focus item first (any source), then the default order. Expected outcome: never empty.
        internal static List<Goal> SearchOrder(string focus)
        {
            var list = new List<Goal>();
            string f = Normalize(focus);
            if (f.Length > 0) list.Add(new Goal(f));
            list.AddRange(DefaultOrder);
            return list;
        }

        /// A stored value that is not one of the choices (old save, typo) becomes None.
        internal static string Normalize(string value)
        {
            foreach (var c in Choices) if (c.Key == (value ?? None)) return c.Key;
            foreach (var b in BiomeChoices) if (b[0] == value) return b[0];
            return None;
        }

        /// Text key of a value (unknown values: the None key).
        internal static string KeyOf(string value)
        {
            string v = Normalize(value);
            foreach (var c in Choices) if (c.Key == v) return c.Value;
            foreach (var b in BiomeChoices) if (b[0] == v) return b[1];
            return Choices[0].Value;
        }

        /// What the "Farming now" cells show: "-" when nothing is farmed, the item's English name (text key looked up by `text`) when it is
        /// one of the choices, else the raw value.
        internal static string Shown(string farming, Func<string, string> text)
        {
            if (string.IsNullOrEmpty(farming)) return "-";
            string v = Normalize(farming);
            return v.Length > 0 ? text(KeyOf(v)) : farming;
        }
    }
}
