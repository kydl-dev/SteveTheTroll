namespace SteveTheTroll.Features.Trolls.Combat.Domain;

/// <summary>
///     D-175 (Architecture V2, P5 combat slice): the facts about one own troll when its owner is attacked
///     (TrollDefend) or hunted (TrollGuard). Readonly struct, game-free.
/// </summary>
internal readonly struct DefenceInput
{
    /// This client owns the troll's network object.
    internal bool TrollOwner { get; }

    /// The owner wears the Fluffy Slippers (T-008e).
    internal bool OwnerWears { get; }

    /// A raid is on (the trolls defend without slippers too).
    internal bool Raid { get; }

    /// Flat metres from the troll to its owner.
    internal float ToOwner { get; }

    /// TrollDefend's range (ModSettings.DefendRange).
    internal float Range { get; }

    internal DefenceInput(bool trollOwner, bool ownerWears, bool raid, float toOwner, float range)
    {
        TrollOwner = trollOwner;
        OwnerWears = ownerWears;
        Raid = raid;
        ToOwner = toOwner;
        Range = range;
    }
}

/// <summary>
///     D-175 (P5, spec "DefencePolicy"): whether an own troll takes on its owner's attacker - the old checks of
///     TrollDefend.Defend in their order. Game-free.
/// </summary>
internal static class DefencePolicy
{
    internal const string NoSlippers = "the owner has no slippers on and there is no raid - trolls stay at the bed",
        TooFar = "too far from its owner",
        NotOwner = "owned by another client - not steered here";

    /// Expected outcome: "" = it attacks, else why not (one of the constants above).
    internal static string WhyNot(DefenceInput i)
    {
        if (!(i.OwnerWears || i.Raid))
        {
            return NoSlippers;
        }

        if (!(i.ToOwner <= i.Range))
        {
            return TooFar;
        }

        if (!i.TrollOwner)
        {
            return NotOwner;
        }

        return "";
    }
}
