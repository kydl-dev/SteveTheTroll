namespace SteveTheTroll.Backpack
{
    /// <summary>
    /// TEST-043 (user: "why are they that close"): pure placement rules for troll depots (unit-tested). Distances in metres, flat.
    /// </summary>
    internal static class DepotRules
    {
        /// Free space kept around a new depot to any building or other depot (a troll and a player fit through).
        internal const float Gap = 2f;
        /// Smallest distance between two depot stacks, centre to centre (depot 4.6 x 2.6 m + Gap on each side; D-107: 12 m so a troll
        /// unloading 8 - 10 m from one stack rarely makes the next one busy too).
        internal const float StackSpacing = 12f;

        /// The stack stands in the depot ring around the bed (both edges count). Expected outcome: false for a negative distance.
        internal static bool InRing(float toBed, float min, float max) { return toBed >= 0f && toBed >= min && toBed <= max; }

        /// D-107: another troll this close (flat) makes a depot busy - the next troll goes elsewhere. Expected outcome: true at exactly `radius`.
        internal static bool Crowded(float distance, float radius) { return distance >= 0f && distance <= radius; }

        /// Far enough from another stack. Expected outcome: true at exactly `spacing`.
        internal static bool FarEnough(float distance, float spacing) { return distance >= spacing; }
    }
}
