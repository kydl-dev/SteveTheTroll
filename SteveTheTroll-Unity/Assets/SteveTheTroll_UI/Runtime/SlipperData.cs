using System.Collections.Generic;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// What the Slippers tab shows about the item (D-039). Plain data, no Unity / Valheim types: the game fills it from the item and its recipe
    /// (src/Ui/MainMenu.cs), the Unity preview uses Sample. SlipperDetailsBuilder turns it into detail blocks.
    /// </summary>
    internal sealed class SlipperData
    {
        /// One recipe line: shown item name, amount to craft, amount for each upgrade (0 = not needed for upgrades).
        internal sealed class Ingredient
        {
            internal readonly string Name;
            internal readonly int Amount;
            internal readonly int PerUpgrade;
            internal Ingredient(string name, int amount, int perUpgrade) { Name = name ?? ""; Amount = amount; PerUpgrade = perUpgrade; }
            public override string ToString() { return Name + " x" + Amount + " (+" + PerUpgrade + "/upgrade)"; }
        }

        internal string Name = "";
        internal string Description = "";
        internal float BaseArmor;
        internal float ArmorPerLevel;
        internal int MaxQuality = 1;
        internal bool FrostResistant;
        internal string Station = "";
        internal int StationLevel = 1;
        /// Quality of the slippers the player wears now (0 = not worn).
        internal int WornQuality;
        internal readonly List<Ingredient> Recipe = new List<Ingredient>();

        /// The numbers of the real item (D-005, D-034, D-035), for the Unity preview.
        internal static SlipperData Sample()
        {
            var d = new SlipperData
            {
                Name = "Fluffy Slippers",
                Description = "Soft, fluffy plush slippers with a fur-lined rim. Warm toes, brave heart.",
                BaseArmor = 5f,
                ArmorPerLevel = 1f,
                MaxQuality = 4,
                FrostResistant = true,
                Station = "Workbench",
                StationLevel = 1,
                WornQuality = 2
            };
            d.Recipe.Add(new Ingredient("Troll hide", 5, 2));
            d.Recipe.Add(new Ingredient("Deer hide", 4, 0));
            d.Recipe.Add(new Ingredient("Blueberries", 6, 2));
            return d;
        }

        public override string ToString() { return Name + " (armor " + BaseArmor + " +" + ArmorPerLevel + "/q, max q" + MaxQuality + ", frost " + FrostResistant + ", recipe " + Recipe.Count + " line(s), worn q" + WornQuality + ")"; }
    }
}
