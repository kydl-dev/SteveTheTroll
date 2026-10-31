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

        /// D-149: a depot chest reserved by ANOTHER troll (holder != me, not expired) is not offered. holder 0 = nobody.
        internal static bool ReservedByOther(int holder, float reservedAt, int me, float now, float ttl)
        {
            return holder != 0 && holder != me && now - reservedAt < ttl;
        }

        /// D-155 (user 2026-10-31: troll-built depot structures disappear as soon as they are emptied): the structure goes when it has chests and ALL of them are empty,
        /// nobody has one open, no troll is on its way to one, and no other structure stands on top of it. Expected outcome: false with no chest found.
        internal static bool Dismantle(int chests, int emptyChests, bool inUse, bool reserved, bool frameAbove)
        {
            return Lifecycle(chests, emptyChests, inUse, reserved, frameAbove) == Verdict.Remove;
        }

        internal enum Verdict { Keep, Wait, Remove }

        /// D-160: the full lifecycle decision. Keep = something is still inside (or no chest found); Wait = empty but blocked (window open, troll on its way, frame above) -
        /// checked again later (TrollDepot.Tick); Remove = go now.
        internal static Verdict Lifecycle(int chests, int emptyChests, bool inUse, bool reserved, bool frameAbove)
        {
            if (chests <= 0 || emptyChests != chests) return Verdict.Keep;
            return inUse || reserved || frameAbove ? Verdict.Wait : Verdict.Remove;
        }

        /// Far enough from another stack. Expected outcome: true at exactly `spacing`.
        internal static bool FarEnough(float distance, float spacing) { return distance >= spacing; }
    }
}
