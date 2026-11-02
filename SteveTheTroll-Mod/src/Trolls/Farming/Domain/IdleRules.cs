
namespace SteveTheTroll.Trolls.Farming.Domain
{
    internal enum IdleGate { Skip, Unload, Continue }

    /// <summary>D-144: the game-free decisions of the idle check (unit-tested; TrollIdle only reads the game and acts on the answer).</summary>
    internal static class IdleRules
    {
        internal const float WanderSeconds = 10f, WanderMetres = 6f;
        /// The busy reason a follow order gives (TrollIdle.BusyReason). A full backpack beats it (D-149).
        internal const string FollowReason = "has a follow target";
        /// D-159: an ordinary farm job. A full backpack beats it too (the logs of 2026-10-31 showed full trolls left alone for minutes: "not sent because TrollFarmer is active").
        internal const string FarmReason = "TrollFarmer is active";
        /// D-149: a target this far above / below the troll is not worth the walk (test run 011447: a tree 34 m higher, 182 m away, "blocked").
        internal const float MaxHeightDiff = 12f;

        /// Whether a farming target at this height difference (metres, either way) is considered reachable.
        internal static bool Reachable(float heightDiff) { return System.Math.Abs(heightDiff) <= MaxHeightDiff; }

        /// A troll with a job (busyReason != null) is left alone. A full backpack is a job of its own: to the depot, unless it was sent
        /// within the retry time (a full depot must not loop). Everything else goes on to the farm decision.
        internal static IdleGate Gate(string busyReason, bool full, bool triedRecently)
        {
            // D-149 (user: "full backpack - depot delivery takes priority over ordinary following"): only fighting / farming / arriving / no slippers keep a full troll away
            if (busyReason != null && !(full && (busyReason == FollowReason || busyReason == FarmReason))) return IdleGate.Skip;
            if (!full) return IdleGate.Continue;
            return triedRecently ? IdleGate.Skip : IdleGate.Unload;
        }

        /// A troll that has no job and still travelled far in the watch window is wandering (the 9 minutes of Steve 1 after the logout).
        internal static bool Wanders(bool hasJob, float movedMetres, float seconds)
        {
            return !hasJob && seconds >= WanderSeconds && movedMetres > WanderMetres;
        }
    }
}
