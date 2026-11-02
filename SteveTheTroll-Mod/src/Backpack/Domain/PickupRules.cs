namespace SteveTheTroll.Backpack.Domain
{
    /// <summary>D-173 (Architecture V2, P6 pickup slice): what the pickup does with one dropped item next to a collecting troll.</summary>
    internal enum PickupAction
    {
        /// The drop is gone / has no valid network object: nothing.
        Skip,
        /// Another client owns the drop: ask for it (taken on a later tick).
        RequestOwnership,
        /// This client owns the drop: into the backpack.
        Take,
    }

    /// <summary>D-173 (P6): the facts about one troll + one drop the pickup decides on (read by BackpackPickup). Readonly struct, game-free.</summary>
    internal readonly struct PickupInput
    {
        /// This client owns the troll.
        internal bool TrollOwner { get; }
        /// One of the troll's chests is open in a window (no pickup into it meanwhile, T-008c).
        internal bool ChestOpen { get; }
        /// No chest has an empty slot (D-062).
        internal bool Full { get; }
        /// The tick's time budget is used up (D-142).
        internal bool BudgetUsed { get; }
        /// The drop still exists with a valid network object.
        internal bool DropValid { get; }
        /// This client owns the drop.
        internal bool DropOwned { get; }

        internal PickupInput(bool trollOwner, bool chestOpen, bool full, bool budgetUsed, bool dropValid, bool dropOwned)
        {
            TrollOwner = trollOwner; ChestOpen = chestOpen; Full = full; BudgetUsed = budgetUsed; DropValid = dropValid; DropOwned = dropOwned;
        }
    }

    /// <summary>D-173 (P6): the game-free pickup decision for one drop.</summary>
    internal static class PickupRules
    {
        /// Expected outcome: Skip for a vanished drop, RequestOwnership when another client owns it, else Take.
        internal static PickupAction ForDrop(bool dropValid, bool dropOwned)
        {
            if (!dropValid) return PickupAction.Skip;
            return dropOwned ? PickupAction.Take : PickupAction.RequestOwnership;
        }
    }
}
