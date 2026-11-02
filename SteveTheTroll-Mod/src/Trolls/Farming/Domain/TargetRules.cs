
namespace SteveTheTroll.Trolls.Farming.Domain
{
    /// <summary>
    /// D-151 (user 2026-10-31): two trolls must not go for the same tree (one waiting behind the other), and a troll with a friendly troll right in front of it steps around it
    /// instead of pushing. The decisions are game-free (unit-tested); TrollFarmer reads the game and acts on them. Flat distances in metres (x / z).
    /// </summary>
    internal static class TargetRules
    {
        /// How far ahead (m) and how wide (m, each side of the walking line) a troll counts as "in front of me".
        internal const float Ahead = 3.5f, HalfWidth = 1.8f;
        /// How far beside the other troll (m) the detour point lies.
        internal const float Side = 2.8f;
        /// A reservation of a target is kept this long (s) after its holder last refreshed it.
        internal const float HoldSeconds = 6f;

        /// Another troll stands on the line from me to the goal, in front of me, and not at the goal itself. Expected outcome: false when it is behind, beside, beyond or AT the goal.
        internal static bool Blocked(float mx, float mz, float gx, float gz, float ox, float oz, float ahead, float halfWidth)
        {
            float dx = gx - mx, dz = gz - mz;
            float len = (float)System.Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.5f) return false;
            dx /= len; dz /= len;
            float rx = ox - mx, rz = oz - mz;
            float along = rx * dx + rz * dz;
            if (along <= 0f || along > ahead || along >= len - 0.5f) return false;
            float lateral = dx * rz - dz * rx;
            return System.Math.Abs(lateral) <= halfWidth;
        }

        /// The point beside the other troll, on the side AWAY from the line it stands on (exactly on the line: the walker's right), and a metre past it.
        /// Two trolls meeting head on each pick their own right, so they pass each other.
        internal static void Waypoint(float mx, float mz, float gx, float gz, float ox, float oz, float side, out float wx, out float wz)
        {
            float dx = gx - mx, dz = gz - mz;
            float len = (float)System.Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.0001f) { wx = ox; wz = oz; return; }
            dx /= len; dz /= len;
            float lateral = dx * (oz - mz) - dz * (ox - mx);
            float sgn = lateral > 0f ? -1f : 1f;
            wx = ox + (-dz) * sgn * side + dx;
            wz = oz + dx * sgn * side + dz;
        }

        /// A target reserved by ANOTHER troll that refreshed it recently. holder 0 = nobody.
        internal static bool HeldByOther(int holder, float heldAt, int me, float now, float ttl)
        {
            return holder != 0 && holder != me && now - heldAt < ttl;
        }
    }
}
