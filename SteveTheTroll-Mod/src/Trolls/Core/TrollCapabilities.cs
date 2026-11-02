using SteveTheTroll.Trolls.Contracts;

namespace SteveTheTroll.Trolls.Core
{
    /// <summary>
    /// Architecture V2 (D-169): the capabilities one troll offers, each behind an interface (composition, no troll base class). Inventory / farming join here when their
    /// features migrate (phases 4 / 6); interfaces are added when something uses them, not ahead.
    /// </summary>
    internal sealed class TrollCapabilities
    {
        internal ITrollMovement Movement { get; }
        internal ITrollCombat Combat { get; }

        internal TrollCapabilities(ITrollMovement movement, ITrollCombat combat)
        {
            Movement = movement;
            Combat = combat;
        }

        /// Every capability is present. Expected outcome: false for a context built on a destroyed / incomplete troll.
        internal bool Complete { get { return Movement != null && Combat != null; } }
    }
}
