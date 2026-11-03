namespace SteveTheTroll.Features.Trolls.Home.Domain
{
    /// <summary>D-171 (P7): the game-free home rules - bed / hub heal range (D-085 / D-135) and "stand still while the player looks at me" (D-063 / D-077).</summary>
    /// <summary>D-173: what a troll does right after it emptied its backpack at the home depot.</summary>
    internal enum AfterUnload
    {
        /// The depot had no room for everything: the farmer stops (the idle check tries again).
        Stop,
        /// Back to the remembered farm spot (user 2026-10-31).
        ResumeSpot,
        /// The owner wears no slippers: it stays home - walks on to the home radius and waits (user 2026-11-02: "go home and unload there").
        StayHome,
    }

    internal static class HomeRules
    {
        /// D-173 (user 2026-11-02, asked whether a troll that fills up on a slipper-less walk home should go home or unload: "go home and unload there"): it keeps walking home,
        /// unloads at the home depot, and with no slippers stays home instead of walking back to its farm spot. Expected outcome: Stop when items are left over.
        internal static AfterUnload AfterUnloading(int left, bool ownerWears) { return left > 0 ? AfterUnload.Stop : (ownerWears ? AfterUnload.ResumeSpot : AfterUnload.StayHome); }

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
