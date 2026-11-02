namespace SteveTheTroll.Trolls.Contracts
{
    /// <summary>Architecture V2 (spec section 4, P6 - D-173): the troll's backpack as the behaviour sees it (the four chests live in the troll's ZDO, BackpackStore).</summary>
    internal interface ITrollInventory
    {
        /// No chest has an empty slot (D-062; cached for a few seconds, D-144).
        bool IsFull { get; }
        /// At least one chest has an empty slot.
        bool HasSpace { get; }
    }
}
