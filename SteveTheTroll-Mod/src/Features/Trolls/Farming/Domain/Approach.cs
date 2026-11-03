
namespace SteveTheTroll.Features.Trolls.Farming.Domain
{
    /// <summary>D-170: how the troll gets to its target this frame.</summary>
    internal enum ApproachAction
    {
        /// A step forward after a dry try is still running (D-051).
        Stepping,
        /// Walk towards the target (recovery, friendly-troll detour and the walk itself).
        Walk,
        /// Close enough: stop, face, swing.
        AtTarget,
    }

    /// <summary>
    /// D-170: the approach decision of TrollFarmer (D-051 step forward, D-062 "right at it and not getting closer for 0.5 s -> swing from here", D-063: further away the
    /// troll does not swing at whatever blocks it). Game-free.
    /// </summary>
    internal static class ApproachRules
    {
        /// Right at it = within reach + this (m).
        internal const float AtItSlack = 1f;
        /// Not getting closer this long while right at it = swing from here (s).
        internal const float CloseWaitSeconds = 0.5f;
        /// A distance must beat the best one by this much to count as getting closer (m).
        internal const float CloserBy = 0.2f;

        /// Expected outcome: Stepping while a step runs; Walk while out of reach (unless right at it and stuck longer than CloseWaitSeconds); else AtTarget.
        internal static ApproachAction Decide(bool stepping, float distance, float reach, float stuckSeconds)
        {
            if (stepping) return ApproachAction.Stepping;
            if (distance <= reach) return ApproachAction.AtTarget;
            bool atIt = distance <= reach + AtItSlack;
            return !atIt || stuckSeconds <= CloseWaitSeconds ? ApproachAction.Walk : ApproachAction.AtTarget;
        }
    }

    /// <summary>D-170: the "getting closer?" timer of the approach (the old _lastDist / _stuckSeconds of TrollFarmer). Game-free.</summary>
    internal sealed class ApproachMeter
    {
        internal float LastDistance { get; private set; } = float.MaxValue;
        internal float StuckSeconds { get; private set; }

        /// One frame out of reach. Expected outcome: StuckSeconds 0 after a gain of more than CloserBy, else grown by dt.
        internal float Update(float distance, float dt)
        {
            if (distance < LastDistance - ApproachRules.CloserBy) { LastDistance = distance; StuckSeconds = 0f; }
            else StuckSeconds += dt;
            return StuckSeconds;
        }

        /// At the target / a new target. Expected outcome: StuckSeconds 0, no best distance.
        internal float Reset() { StuckSeconds = 0f; LastDistance = float.MaxValue; return StuckSeconds; }
    }
}
