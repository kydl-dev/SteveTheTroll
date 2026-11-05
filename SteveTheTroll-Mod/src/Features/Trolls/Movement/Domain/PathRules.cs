using System;

namespace SteveTheTroll.Features.Trolls.Movement.Domain;

/// <summary>
///     D-179 (user 2026-11-05: pathing in cluttered spots, "do 1-2 and 4 now"): the pure rules of the troll's own path finding in clutter - which blocker a troll
///     simply pushes through (1), how a feeler heading is scored (2) and how long a remembered bad spot counts (4). Game-free and unit-tested; TrollPath reads the world.
/// </summary>
internal static class PathRules
{
    /// Feeler headings, degrees left (-) / right (+) of the straight line to the goal.
    internal static readonly float[] FeelerAngles = { 0f, -20f, 20f, -40f, 40f, -60f, 60f, -90f, 90f };

    internal const float FeelerLength = 10f, MinUsefulRun = 3f, BadSpotRadius = 4f, BadSpotPenalty = 6f, BadSpotSeconds = 120f;

    internal const int MaxBadSpots = 64;

    private static readonly string[] SmallWords = { "small", "bush", "sapling", "shrub", "stub", "log", "branch", "fern", "vines" };

    /// 1: a blocker the troll breaks with a swing and walks on - natural (no piece, no creature, no rock) and small: any log, stump, bush, sapling, or a tree whose
    /// prefab name says small. Big trees and rocks are not pushed through (a detour is cheaper). Expected outcome: true only for small natural things.
    internal static bool PushThrough(string prefab, bool destructible, bool isPiece, bool isCreature, bool isRock, bool isTreeLog)
    {
        if (!destructible || isPiece || isCreature || isRock || string.IsNullOrEmpty(prefab))
        {
            return false;
        }

        if (isTreeLog)
        {
            return true;
        }

        string n = prefab.ToLowerInvariant();
        foreach (string w in SmallWords)
        {
            if (n.Contains(w))
            {
                return true;
            }
        }

        return false;
    }

    /// 2: the score of one feeler heading - how far the troll can walk that way (capped at FeelerLength) times how much of it leads to the goal (cosine of the
    /// angle, at least 0.15 so a free side heading still beats a blocked straight one), minus the cost of a bad spot on the way. A heading shorter than
    /// MinUsefulRun scores nothing. Expected outcome: higher = better; float.MinValue = useless.
    internal static float FeelerScore(float freeMetres, float angleDegrees, bool passesBadSpot)
    {
        float free = Math.Min(Math.Max(freeMetres, 0f), FeelerLength);
        if (free < MinUsefulRun)
        {
            return float.MinValue;
        }

        double progress = Math.Cos(angleDegrees * Math.PI / 180.0);
        return (float)(free * Math.Max(progress, 0.15)) - (passesBadSpot ? BadSpotPenalty : 0f);
    }

    /// 4: a remembered bad spot still counts. Expected outcome: true for BadSpotSeconds after it was recorded.
    internal static bool StillBad(float recordedAt, float now)
    {
        return now - recordedAt < BadSpotSeconds;
    }
}
