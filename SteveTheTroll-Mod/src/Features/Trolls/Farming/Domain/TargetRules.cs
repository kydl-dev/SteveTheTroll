using System;

namespace SteveTheTroll.Features.Trolls.Farming.Domain;

/// <summary>
///     D-151 (user 2026-10-31): two trolls must not go for the same tree (one waiting behind the other), and a troll with
///     a friendly troll right in front of it steps around it
///     instead of pushing. The decisions are game-free (unit-tested); TrollFarmer reads the game and acts on them. Flat
///     distances in metres (x / z).
/// </summary>
internal static class TargetRules
{
    /// How far ahead (m) and how wide (m, each side of the walking line) a troll counts as "in front of me".
    // D-180 (fix12 farm-friend-detour: passing a standing troll the walker never got more than 1.4 m beside the line - the 2.8 m waypoint is inside the two bodies, so it pushed
    // the friend ahead of it up to the tree): the check starts 5 m ahead and the waypoint lies 4 m beside the friend (two troll bodies and a margin)
    internal const float Ahead = 5f, HalfWidth = 1.8f;

    /// How far beside the other troll (m) the detour point lies.
    internal const float Side = 4f;

    /// A reservation of a target is kept this long (s) after its holder last refreshed it.
    internal const float HoldSeconds = 6f;

    /// D-174 (spot-return, made to wait for the arrival): a troll walking back to its REMEMBERED farm spot (D-163, user: "remember the spot where they've farmed") does not
    /// take a source near the way - the near search took over 5 m from the depot and the spot was never reached. Within SpotSearchMetres of the spot (= TrollFarmer's arrival
    /// distance: run 172537 showed a 30 m zone still stopping the troll short of the spot) it searches as usual;
    /// a long-range search walk (no remembered spot) keeps taking the first source in range on the way. Expected outcome: true = run the near search now.
    internal const float SpotSearchMetres = 12f;

    /// Another troll stands on the line from me to the goal, in front of me, and not at the goal itself. Expected outcome: false when it is behind, beside, beyond or AT the goal.
    internal static bool Blocked(GroundPoint me, GroundPoint goal, GroundPoint other, float ahead, float halfWidth)
    {
        float mx = me.X, mz = me.Z, gx = goal.X, gz = goal.Z, ox = other.X, oz = other.Z;
        float dx = gx - mx, dz = gz - mz;
        float len = (float)Math.Sqrt(dx * dx + dz * dz);
        if (len < 0.5f)
        {
            return false;
        }

        dx /= len;
        dz /= len;
        float rx = ox - mx, rz = oz - mz;
        float along = rx * dx + rz * dz;
        if (along <= 0f || along > ahead || along >= len - 0.5f)
        {
            return false;
        }

        float lateral = dx * rz - dz * rx;
        return Math.Abs(lateral) <= halfWidth;
    }

    /// The point beside the other troll, on the side AWAY from the line it stands on (exactly on the line: the walker's right), and a metre past it.
    /// Two trolls meeting head on each pick their own right, so they pass each other.
    internal static GroundPoint Waypoint(GroundPoint me, GroundPoint goal, GroundPoint other, float side)
    {
        float mx = me.X, mz = me.Z, gx = goal.X, gz = goal.Z, ox = other.X, oz = other.Z;
        float dx = gx - mx, dz = gz - mz;
        float len = (float)Math.Sqrt(dx * dx + dz * dz);
        if (len < 0.0001f)
        {
            return other;
        }

        dx /= len;
        dz /= len;
        float lateral = dx * (oz - mz) - dz * (ox - mx);
        float sgn = lateral > 0f ? -1f : 1f;
        return new GroundPoint(ox + -dz * sgn * side + dx, oz + dx * sgn * side + dz);
    }

    /// A target reserved by ANOTHER troll that refreshed it recently. holder 0 = nobody.
    internal static bool HeldByOther(int holder, float heldAt, int me, float now, float ttl)
    {
        return holder != 0 && holder != me && now - heldAt < ttl;
    }

    internal static bool NearSearchWhileTravelling(bool toRememberedSpot, float metresToSpot)
    {
        return !toRememberedSpot || metresToSpot <= SpotSearchMetres;
    }
}
