namespace SteveTheTroll.Features.Trolls.Taming.Domain
{
    /// <summary>D-160: the game-free decisions of the runtime health monitor (unit-tested; RuntimeHealth reads the game and reports). Problems are only reported, never "fixed".</summary>
    internal static class HealthRules
    {
        /// A full backpack that has not started an unload this long is a problem (the idle check sends it within seconds; a full depot retries every RetryFullSeconds).
        internal const float FullGraceSeconds = 90f;
        /// Farming further from the hub than the 950 m leash plus this slack (a troll finishing its last swing, a target moving) is a problem.
        internal const float LeashSlack = 30f;
        /// A depot structure that is empty but still waiting to be removed this long is a problem.
        internal const float DepotWaitSeconds = 120f;

        internal static bool FullNotUnloading(bool full, bool unloading, bool fighting, float fullForSeconds)
        {
            return full && !unloading && !fighting && fullForSeconds > FullGraceSeconds;
        }

        internal static bool FarmingBeyondLeash(bool farming, bool following, bool hasHub, float trollToHub, float leash)
        {
            return farming && !following && hasHub && trollToHub > leash + LeashSlack;
        }

        /// A troll whose saved hub number points at a hub that no longer exists (or belongs to someone else).
        internal static bool StaleAssignment(int assigned, bool hubExists) { return assigned > 0 && !hubExists; }

        internal static bool DepotStuck(float emptyWaitingSeconds) { return emptyWaitingSeconds > DepotWaitSeconds; }

    }
}
