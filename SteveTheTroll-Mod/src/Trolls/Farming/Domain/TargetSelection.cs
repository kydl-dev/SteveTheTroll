using System.Collections.Generic;
using System;
using SteveTheTroll.Core.Results;

namespace SteveTheTroll.Trolls.Farming.Domain
{
    /// <summary>D-170: which range and which goal the search found something for.</summary>
    internal readonly struct SelectionHit
    {
        internal int RangeIndex { get; }
        internal int GoalIndex { get; }
        internal SelectionHit(int rangeIndex, int goalIndex) { RangeIndex = rangeIndex; GoalIndex = goalIndex; }
        public override string ToString() { return "range #" + RangeIndex + ", goal #" + GoalIndex; }
    }

    /// <summary>
    /// D-051 / D-058 / D-170: the search ORDER of a farming troll, game-free - every range from the smallest (quick -> farm -> max range), and in each range every goal in the
    /// focus order (focus first, then the default priority). The first range / goal for which `tryFind` finds a source wins, so a focus source within the quick range beats
    /// a closer other item only within the same range. `tryFind` does the game work (and keeps what it found).
    /// </summary>
    internal static class TargetSelection
    {
        internal const string NothingFound = "nothing in any range";

        /// Expected outcome: Success with the first hit in range-then-goal order; Ignored when nothing was found (Failed for missing arguments).
        internal static OperationResponse<SelectionHit> FirstHit<TGoal>(IList<float> ranges, IList<TGoal> goals, Func<float, TGoal, bool> tryFind)
        {
            if (ranges == null || goals == null || tryFind == null) return OperationResponse<SelectionHit>.Failed("ranges, goals and the finder are needed");
            for (int r = 0; r < ranges.Count; r++)
            {
                for (int g = 0; g < goals.Count; g++)
                {
                    if (tryFind(ranges[r], goals[g])) return OperationResponse<SelectionHit>.Success(new SelectionHit(r, g));
                }
            }
            return OperationResponse<SelectionHit>.Ignored(NothingFound);
        }
    }
}
