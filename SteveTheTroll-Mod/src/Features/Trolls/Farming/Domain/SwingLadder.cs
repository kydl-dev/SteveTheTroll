namespace SteveTheTroll.Features.Trolls.Farming.Domain;

/// <summary>D-170: what the next swing moment means.</summary>
internal enum SwingAction
{
    /// Swing (equip the weapon, StartAttack).
    Swing,

    /// Two dry swings in this try: step StepMetres forward and start the next try.
    StepForward,

    /// The last try was dry too: give the source up.
    GiveUp
}

/// <summary>
///     D-051 / D-170: the dry-swing ladder of one farm target, game-free. A try is up to SwingsPerTry swings; a swing
///     counts as dry when the source's network data revision did not
///     change since the swing before (no damage reached it); a revision change resets the ladder to try 1. Two dry swings
///     in a try -> step forward and try again; Tries tries -> give up.
///     Exactly the counters TrollFarmer had before (_try / _swingsThisTry / _dry / _lastRevision).
/// </summary>
internal sealed class SwingLadder
{
    internal const int SwingsPerTry = 2, Tries = 4;

    internal int Try { get; private set; } = 1;
    internal int SwingsThisTry { get; private set; }
    internal int Dry { get; private set; }
    internal uint LastRevision { get; private set; }

    /// The troll is ready to swing; `revision` = the source's data revision now. Expected outcome: Swing, StepForward (the next try is set up) or GiveUp.
    internal SwingAction Next(uint revision)
    {
        if (SwingsThisTry > 0)
        {
            if (revision == LastRevision)
            {
                Dry++;
            }
            else
            {
                Dry = 0;
                Try = 1;
            }
        }

        LastRevision = revision;
        if (SwingsThisTry < SwingsPerTry || Dry < SwingsPerTry)
        {
            return SwingAction.Swing;
        }

        if (Try >= Tries)
        {
            return SwingAction.GiveUp;
        }

        Try++;
        SwingsThisTry = 0;
        Dry = 0;
        return SwingAction.StepForward;
    }

    /// The game started the attack. Expected outcome: one more swing in this try.
    internal int Swung()
    {
        SwingsThisTry++;
        return SwingsThisTry;
    }

    /// A new target / a new search. Expected outcome: try 1, nothing swung.
    internal int Reset()
    {
        Try = 1;
        SwingsThisTry = 0;
        Dry = 0;
        LastRevision = 0u;
        return Try;
    }

    public override string ToString()
    {
        return "try " + Try + "/" + Tries + " (" + SwingsThisTry + " swing(s), " + Dry + " dry)";
    }
}
