namespace SteveTheTroll.Trolls.Combat.Domain
{
    /// <summary>D-171 (P5): the game-free decisions of TrollSlam (D-065 / D-084).</summary>
    internal static class SlamRules
    {
        /// All slams done and the last one finished, or the time is up. Expected outcome: true = the slam series ends.
        internal static bool Done(int left, bool inAttack, float elapsed, float maxSeconds) { return (left <= 0 && !inAttack) || elapsed > maxSeconds; }

        /// Ready for the next slam: slams left, no attack running, the gap passed, facing the aim.
        internal static bool Ready(int left, bool inAttack, bool gapPassed, bool facing) { return left > 0 && !inAttack && gapPassed && facing; }
    }
}
