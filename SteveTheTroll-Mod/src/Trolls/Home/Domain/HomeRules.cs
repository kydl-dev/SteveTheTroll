namespace SteveTheTroll.Trolls.Home.Domain
{
    /// <summary>D-171 (P7): the game-free home rules - bed / hub heal range (D-085 / D-135) and "stand still while the player looks at me" (D-063 / D-077).</summary>
    internal static class HomeRules
    {
        /// A point (flat offset dx, dz from the bed or a hub) is inside the heal range (squared, m^2).
        internal static bool InHealRange(float dx, float dz, float range2) { return dx * dx + dz * dz <= range2; }

        /// D-077: a tamed troll without a follow order and without a fight stands still while the local player within `range` looked at it (or its backpack) less than `grace`
        /// seconds ago; range 0 = off. Expected outcome: false when off, following, fighting, too far, or not looked at lately.
        internal static bool Holds(float range, bool follows, bool fights, float distance, bool lookedAtEver, float sinceLook, float grace)
        {
            if (range <= 0f || follows || fights || distance > range) return range > 0f && !follows && !fights && distance <= range && lookedAtEver && sinceLook <= grace;
            return lookedAtEver && sinceLook <= grace;
        }
    }
}
