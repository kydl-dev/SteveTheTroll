namespace SteveTheTroll.Features.Trolls.Orders.Domain
{
    /// <summary>D-170: what the attack key means (T-008d / D-065 / D-124).</summary>
    internal enum OrderKind
    {
        /// The player looks at an enemy creature: the nearest own troll attacks it.
        Attack,
        /// A fortress wall (D-124: only 2-star trolls; TrollWalls decides that).
        BreakWall,
        /// A tree / log / rock / vein / destructible: break THIS one, then farm on.
        BreakSource,
        /// Nothing of that kind: farm the troll's focus / the default priority.
        FarmFocus,
    }

    /// <summary>D-170: the facts of one attack-key frame, read by TrollOrders. Readonly struct (read every frame), game-free.</summary>
    internal readonly struct OrderInput
    {
        internal bool KeyPressed { get; }
        internal bool PlayerAlive { get; }
        /// A menu, the KY hub, chat, the console or a text input is open.
        internal bool UiBlocked { get; }
        /// An own tamed troll is within the order range.
        internal bool HasTroll { get; }
        /// This client owns (steers) that troll now (after taking it over, D-066).
        internal bool CanSteer { get; }
        internal bool HasCreature { get; }
        internal bool HasSource { get; }
        internal bool SourceIsWall { get; }

        internal OrderInput(bool keyPressed, bool playerAlive, bool uiBlocked, bool hasTroll, bool canSteer, bool hasCreature, bool hasSource, bool sourceIsWall)
        {
            KeyPressed = keyPressed; PlayerAlive = playerAlive; UiBlocked = uiBlocked; HasTroll = hasTroll; CanSteer = canSteer;
            HasCreature = hasCreature; HasSource = hasSource; SourceIsWall = sourceIsWall;
        }

        public override string ToString()
        {
            return "key " + KeyPressed + ", alive " + PlayerAlive + ", ui blocked " + UiBlocked + ", troll " + HasTroll + ", steer " + CanSteer + ", creature " + HasCreature + ", source " + HasSource + ", wall " + SourceIsWall;
        }
    }

    /// <summary>D-170: the order a valid attack-key press gives. Game-free, unit-tested.</summary>
    internal static class OrderRules
    {
        /// Expected outcome: Attack for a creature (it beats a source behind it), BreakWall / BreakSource for a source, else FarmFocus.
        internal static OrderKind Decide(OrderInput i)
        {
            if (i.HasCreature) return OrderKind.Attack;
            if (i.HasSource) return i.SourceIsWall ? OrderKind.BreakWall : OrderKind.BreakSource;
            return OrderKind.FarmFocus;
        }
    }
}
