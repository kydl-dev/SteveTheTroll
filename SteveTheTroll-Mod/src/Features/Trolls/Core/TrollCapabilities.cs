using SteveTheTroll.Features.Trolls.Contracts;

namespace SteveTheTroll.Features.Trolls.Core;

/// <summary>
///     Architecture V2 (D-169): the capabilities one troll offers, each behind an interface (composition, no troll base
///     class). Interfaces are added when something uses them,
///     not ahead; D-173 (P6) added the backpack.
/// </summary>
internal sealed class TrollCapabilities
{
    internal TrollCapabilities(ITrollMovement movement, ITrollCombat combat, ITrollInventory inventory)
    {
        Movement = movement;
        Combat = combat;
        Inventory = inventory;
    }

    internal ITrollMovement Movement { get; }
    internal ITrollCombat Combat { get; }

    /// D-173 (P6): the backpack (BackpackStore through ValheimTrollInventory).
    internal ITrollInventory Inventory { get; }

    /// Every capability is present. Expected outcome: false for a context built on a destroyed / incomplete troll.
    internal bool Complete => Movement != null && Combat != null && Inventory != null;
}
