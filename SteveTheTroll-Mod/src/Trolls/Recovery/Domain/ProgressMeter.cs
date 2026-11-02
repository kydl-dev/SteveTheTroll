using System;

namespace SteveTheTroll.Trolls.Recovery.Domain
{
    /// <summary>
    /// Measures whether a troll really gets closer to its goal (spec 1a / 5): the timer only runs while movement is expected (not during attack animations, knockback,
    /// deliberate waits) and restarts when the distance beats the best one so far.
    /// </summary>
    internal sealed class ProgressMeter
    {
        float _best = float.MaxValue, _sinceGain, _sustained;

        /// Seconds without a gain (the stuck timer).
        internal float StuckSeconds { get { return _sinceGain; } }
        /// Seconds of continuous progress.
        internal float SustainedSeconds { get { return _sustained; } }
        internal float Best { get { return _best; } }

        /// New goal or new position after a manoeuvre: measure from here.
        internal void Reset(float distance) { _best = distance; _sinceGain = 0f; _sustained = 0f; }

        /// One frame. `expectMovement` false = an intentional pause: nothing runs, nothing is reset.
        internal void Update(float distance, float dt, bool expectMovement)
        {
            if (!expectMovement || dt <= 0f) return;
            if (distance < _best - StuckRules.MinGain) { _best = distance; _sinceGain = 0f; }
            else _sinceGain += dt;
            _sustained = _sinceGain <= StuckRules.GainWindow ? _sustained + dt : 0f;
        }
    }
}
