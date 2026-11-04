using System;

namespace SteveTheTroll.Features.Trolls.Backpack.Domain;

/// <summary>Pure ground-levelling geometry (game-free, so a test can load it without any Valheim type).</summary>
internal static class LevelRules
{
    /// Half side of an axis-aligned square that holds a rectangle of half sizes halfX / halfZ turned any way (= its half diagonal).
    internal static float Radius(float halfX, float halfZ)
    {
        return (float)Math.Sqrt(halfX * halfX + halfZ * halfZ);
    }
}