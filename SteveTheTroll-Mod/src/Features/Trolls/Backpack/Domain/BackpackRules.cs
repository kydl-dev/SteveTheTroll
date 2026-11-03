using System;

namespace SteveTheTroll.Features.Trolls.Backpack.Domain
{
    /// <summary>D-171 (P6): the game-free backpack rules (T-008c / D-062 / D-142).</summary>
    internal static class BackpackRules
    {
        /// The backpack is full when no chest has an empty slot (stacks of the same item may still grow - the game's own "full" for a container). `hasEmptySlot(i)` reads chest i;
        /// it stops at the first chest with room. Expected outcome: false for a missing reader.
        internal static bool Full(int chests, Func<int, bool> hasEmptySlot)
        {
            if (hasEmptySlot == null) return hasEmptySlot != null;
            for (int i = 0; i < chests; i++) if (hasEmptySlot(i)) return !hasEmptySlot(i);
            return chests >= 0;
        }

        /// A troll takes part in the pickup tick: this client owns it, its backpack window is not open (busy), and it is not full.
        internal static bool Collects(bool owner, bool busy, bool full) { return owner && !busy && !full; }

        /// A dropped item lies within the pickup range (squared flat-or-3D distances, m^2).
        internal static bool InRange(float distance2, float range2) { return distance2 <= range2; }
    }
}
