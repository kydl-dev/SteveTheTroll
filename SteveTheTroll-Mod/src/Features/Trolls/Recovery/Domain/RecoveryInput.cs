namespace SteveTheTroll.Features.Trolls.Recovery.Domain
{
    /// <summary>
    /// Architecture V2 (D-169): the facts of ONE recovery frame, read from the game by TrollRecovery and decided on by RecoveryPipeline. Immutable, game-free (no Unity types:
    /// the goal and positions stay in the adapter; the domain only needs times and yes/no facts). A readonly struct: built every AI frame without allocating.
    /// </summary>
    internal readonly struct RecoveryInput
    {
        /// Game time in seconds.
        internal float Now { get; }
        /// Seconds without progress towards the goal (ProgressMeter).
        internal float StuckSeconds { get; }
        /// Seconds of continuous progress.
        internal float SustainedSeconds { get; }
        /// The obstruction ahead may be attacked (StuckRules.Rageable).
        internal bool Rageable { get; }
        /// A tamed troll stands in front, facing me (StuckRules.HeadOn).
        internal bool HeadOn { get; }
        /// The running detour leg is over (StuckRules.LegDone / HeadOnClear).
        internal bool LegDone { get; }
        internal bool IsAlive { get; }
        /// This client owns the troll (only the owner decides and acts).
        internal bool IsOwner { get; }

        internal RecoveryInput(float now, float stuckSeconds, float sustainedSeconds, bool rageable, bool headOn, bool legDone, bool isAlive, bool isOwner)
        {
            Now = now;
            StuckSeconds = stuckSeconds;
            SustainedSeconds = sustainedSeconds;
            Rageable = rageable;
            HeadOn = headOn;
            LegDone = legDone;
            IsAlive = isAlive;
            IsOwner = isOwner;
        }

        public override string ToString()
        {
            return "t " + Now.ToString("F1") + " s, stuck " + StuckSeconds.ToString("F1") + " s, progress " + SustainedSeconds.ToString("F1") + " s, rageable " + Rageable + ", head-on " + HeadOn
                + ", leg done " + LegDone + ", alive " + IsAlive + ", owner " + IsOwner;
        }
    }
}
