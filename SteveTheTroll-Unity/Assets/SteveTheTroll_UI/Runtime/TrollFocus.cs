using System.Collections.Generic;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// Troll work tab (D-039): what the player's tamed troll focus-farms. "" = no focus (the D-038 priority: copper vein, pine, birch, tin, the
    /// rest). Values are the game's item prefab names. Pure logic (unit-tested: TrollFocusTests).
    /// </summary>
    internal static class TrollFocus
    {
        internal const string None = "";
        /// Choices in menu order: value, text key.
        internal static readonly IList<KeyValuePair<string, string>> Choices = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>(None, "stevethetroll_focus_none"),
            new KeyValuePair<string, string>("Wood", "stevethetroll_focus_wood"),
            new KeyValuePair<string, string>("FineWood", "stevethetroll_focus_finewood"),
            new KeyValuePair<string, string>("RoundLog", "stevethetroll_focus_corewood"),
            new KeyValuePair<string, string>("CopperOre", "stevethetroll_focus_copper"),
            new KeyValuePair<string, string>("TinOre", "stevethetroll_focus_tin"),
        };

        /// A stored value that is not one of the choices (old save, typo) becomes None.
        internal static string Normalize(string value)
        {
            foreach (var c in Choices) if (c.Key == (value ?? None)) return c.Key;
            return None;
        }

        /// Text key of a value (unknown values: the None key).
        internal static string KeyOf(string value)
        {
            string v = Normalize(value);
            foreach (var c in Choices) if (c.Key == v) return c.Value;
            return Choices[0].Value;
        }
    }
}
