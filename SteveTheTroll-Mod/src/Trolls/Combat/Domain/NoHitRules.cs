namespace SteveTheTroll.Trolls.Combat.Domain
{
    /// <summary>D-171 (P5): D-097 (user: "set no hit zone 15m from bed") - the flat circle around the bed where own tamed trolls never swing. Game-free.</summary>
    internal static class NoHitRules
    {
        /// (x, z) lies within `radius` m of the bed (bx, bz); radius 0 = off. Expected outcome: false when off.
        internal static bool Inside(float bx, float bz, float x, float z, float radius)
        {
            if (radius <= 0f) return radius > 0f;
            float dx = x - bx, dz = z - bz;
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
