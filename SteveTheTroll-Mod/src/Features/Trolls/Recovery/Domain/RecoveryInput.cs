namespace SteveTheTroll.Features.Trolls.Recovery.Domain;

/// <summary>
///     Architecture V2 (D-169): the facts of ONE recovery frame, read from the game by TrollRecovery and decided on by
///     RecoveryPipeline. Immutable, game-free (no Unity types:
///     the goal and positions stay in the adapter; the domain only needs times and yes/no facts). A readonly struct: built
///     every AI frame without allocating.
/// </summary>
internal struct RecoveryInput
{
    /// Game time in seconds.
    internal float Now { get; set; }

    /// Seconds without progress towards the goal (ProgressMeter).
    internal float StuckSeconds { get; set; }

    /// Seconds of continuous progress.
    internal float SustainedSeconds { get; set; }

    /// The obstruction ahead may be attacked (StuckRules.Rageable).
    internal bool Rageable { get; set; }

    /// A tamed troll stands in front, facing me (StuckRules.HeadOn).
    internal bool HeadOn { get; set; }

    /// The running detour leg is over (StuckRules.LegDone / HeadOnClear).
    internal bool LegDone { get; set; }

    internal bool IsAlive { get; set; }

    /// This client owns the troll (only the owner decides and acts).
    internal bool IsOwner { get; set; }


    public override string ToString()
    {
        return "t " + Now.ToString("F1") + " s, stuck " + StuckSeconds.ToString("F1") + " s, progress " +
               SustainedSeconds.ToString("F1") + " s, rageable " + Rageable + ", head-on " + HeadOn
               + ", leg done " + LegDone + ", alive " + IsAlive + ", owner " + IsOwner;
    }
}
