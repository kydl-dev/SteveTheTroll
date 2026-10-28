using System.Collections.Generic;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// D-080: what the F6 Costume tab shows (T-009e: "F6 tab 1 shows the costume"): the three costume pieces, the set "Steve The Troll" and the
    /// Troll Club. Plain data, no Unity / Valheim types: the game fills it from the items (src/Ui/CostumeMenuData.cs), the Unity preview uses
    /// Sample. CostumeDetailsBuilder turns it into detail blocks.
    /// </summary>
    internal sealed class CostumeData
    {
        /// One armor piece of the set.
        internal sealed class Piece
        {
            internal string Name = "";
            /// Text key of the slot (stevethetroll_details_slot_helmet / _chest / _legs).
            internal string SlotKey = "";
            internal float Armor;
            internal float ArmorPerLevel;
            internal int MaxQuality = 1;
            /// Quality worn now (0 = not worn).
            internal int WornQuality;
            internal readonly List<SlipperData.Ingredient> Recipe = new List<SlipperData.Ingredient>();
            public override string ToString() { return Name + " (armor " + Armor + " +" + ArmorPerLevel + "/q, max q" + MaxQuality + ", worn q" + WornQuality + ", recipe " + Recipe.Count + ")"; }
        }

        internal readonly List<Piece> Pieces = new List<Piece>();
        internal string SetName = "";
        internal string SetDescription = "";
        /// Shown effect lines of the set ("+50 % movement speed", ...), already localized.
        internal readonly List<string> SetEffects = new List<string>();
        /// Set pieces worn now (3 costume pieces + the slippers) and the set size.
        internal int SetWorn;
        internal int SetSize = 4;

        internal string ClubName = "";
        internal string ClubDescription = "";
        internal float ClubBlunt, ClubChop, ClubPickaxe, ClubBluntPerLevel, ClubChopPerLevel, ClubPickaxePerLevel;
        internal int ClubMaxQuality = 1;
        internal string Station = "";
        internal int StationLevel = 1;
        internal readonly List<SlipperData.Ingredient> ClubRecipe = new List<SlipperData.Ingredient>();

        /// The numbers of the real items (D-070 .. D-077, club D-113, carry D-112), for the Unity preview; a unit test keeps them equal to the game's.
        internal static CostumeData Sample()
        {
            var d = new CostumeData
            {
                SetName = "Steve The Troll",
                SetDescription = "The troll rush runs through your whole body.",
                SetWorn = 3,
                ClubName = "Troll Club", ClubDescription = "A whole tree trunk with a rope grip.",
                ClubBlunt = 150, ClubChop = 50, ClubPickaxe = 50, ClubBluntPerLevel = 35, ClubChopPerLevel = 20, ClubPickaxePerLevel = 20, ClubMaxQuality = 4,
                Station = "Workbench", StationLevel = 1,
            };
            foreach (var p in new[] { new[] { "Troll Mascot Head", "stevethetroll_details_slot_helmet" }, new[] { "Troll Mascot Suit", "stevethetroll_details_slot_chest" }, new[] { "Troll Mascot Trousers", "stevethetroll_details_slot_legs" } })
            {
                var piece = new Piece { Name = p[0], SlotKey = p[1], Armor = 20, ArmorPerLevel = 5, MaxQuality = 4, WornQuality = p[0].EndsWith("Head") ? 0 : 1 };
                piece.Recipe.Add(new SlipperData.Ingredient("Troll hide", 5, 2));
                piece.Recipe.Add(new SlipperData.Ingredient("Deer hide", 4, 0));
                piece.Recipe.Add(new SlipperData.Ingredient("Blueberries", 6, 2));
                d.Pieces.Add(piece);
            }
            d.SetEffects.Add("+50 % movement speed");
            d.SetEffects.Add("+25 % carry weight");
            d.SetEffects.Add("-50 % stamina use");
            d.SetEffects.Add("Up to 5 tamed trolls");
            d.ClubRecipe.Add(new SlipperData.Ingredient("Core wood", 15, 5));
            d.ClubRecipe.Add(new SlipperData.Ingredient("Fine wood", 15, 5));
            d.ClubRecipe.Add(new SlipperData.Ingredient("Thistle", 10, 0));
            d.ClubRecipe.Add(new SlipperData.Ingredient("Troll hide", 2, 0));
            return d;
        }

        public override string ToString() { return "costume " + Pieces.Count + " piece(s), set '" + SetName + "' " + SetWorn + "/" + SetSize + " worn, " + SetEffects.Count + " effect(s), club '" + ClubName + "' " + ClubBlunt + "/" + ClubChop + "/" + ClubPickaxe; }
    }
}
