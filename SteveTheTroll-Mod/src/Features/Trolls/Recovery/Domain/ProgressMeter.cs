namespace SteveTheTroll.Features.Trolls.Recovery.Domain;

/// <summary>
///     Measures whether a troll really gets closer to its goal (spec 1a / 5): the timer only runs while movement is
///     expected (not during attack animations, knockback,
///     deliberate waits) and restarts when the distance beats the best one so far.
/// </summary>
internal sealed class ProgressMeter
{
    // D-175 (run 181331: with the D-174 long detour legs the walled-tree troll walked 20 - 30 m away and back, the walk back counted as 10 s of progress, the failure
    // counter was reset again and again and the walled tree was never given up): sustained progress is measured against the CLOSEST distance of the whole attempt
    // (the series), not against the distance after the last manoeuvre - only getting closer than ever before resets the failures.
    private float _sinceSeries;

    /// Seconds without a gain (the stuck timer; measured from the last manoeuvre).
    internal float StuckSeconds { get; private set; }

    /// Seconds of continuous progress beyond the closest distance of the attempt (what may reset the failure counter).
    internal float SustainedSeconds { get; private set; }

    internal float Best { get; private set; } = float.MaxValue;

    internal float SeriesBest { get; private set; } = float.MaxValue;

    /// New position after a manoeuvre: the stuck timer measures from here; the attempt's closest distance is kept.
    internal void Reset(float distance)
    {
        Best = distance;
        StuckSeconds = 0f;
        SustainedSeconds = 0f;
        if (SeriesBest >= float.MaxValue)
        {
            SeriesBest = distance;
            _sinceSeries = 0f;
        }
    }

    /// D-175: a new goal (or the failure counter was reset): the attempt starts again from here.
    internal void NewAttempt(float distance)
    {
        SeriesBest = float.MaxValue;
        _sinceSeries = 0f;
        Reset(distance);
    }

    /// One frame. `expectMovement` false = an intentional pause: nothing runs, nothing is reset.
    internal void Update(float distance, float dt, bool expectMovement)
    {
        if (!expectMovement || dt <= 0f)
        {
            return;
        }

        if (distance < Best - StuckRules.MinGain)
        {
            Best = distance;
            StuckSeconds = 0f;
        }
        else
        {
            StuckSeconds += dt;
        }

        if (distance < SeriesBest - StuckRules.MinGain)
        {
            SeriesBest = distance;
            _sinceSeries = 0f;
        }
        else
        {
            _sinceSeries += dt;
        }

        SustainedSeconds = _sinceSeries <= StuckRules.GainWindow ? SustainedSeconds + dt : 0f;
    }
}
