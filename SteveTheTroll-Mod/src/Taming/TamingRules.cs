namespace SteveTheTroll.Taming
{
    /// <summary>
    /// T-008a pure taming rules (no game types; unit-tested). D-037 / D-038: a wild troll is tamed after TameSeconds while a slipper wearer is
    /// within TameRange; it fights normally meanwhile. A wearer whose OWN tamed troll is within OwnTrollRange cannot tame another one:
    /// that troll is called to the wearer instead. Distances in metres; a negative distance means "none / unknown".
    /// </summary>
    internal static class TamingRules
    {
        /// What a troll does in one taming tick.
        internal enum Step { Nothing, CountDown, CallOwnTroll }

        /// Decision for a WILD troll: wearerDistance = nearest slipper wearer (or -1), ownTrollDistance = that wearer's own tamed troll from
        /// the wearer (or -1). Expected outcome: CountDown only with a wearer in range and no own troll near; CallOwnTroll when one blocks.
        public static Step DecideWild(float wearerDistance, float tameRange, float ownTrollDistance, float ownTrollRange)
        {
            if (wearerDistance < 0f || wearerDistance > tameRange) return Step.Nothing;
            bool blocked = ownTrollDistance >= 0f && ownTrollDistance <= ownTrollRange;
            return blocked ? Step.CallOwnTroll : Step.CountDown;
        }

        /// Seconds left after one tick (never below 0).
        public static float CountDown(float remaining, float dt)
        {
            float left = remaining - (dt > 0f ? dt : 0f);
            return left > 0f ? left : 0f;
        }

        /// Taming done when no time is left.
        public static bool Done(float remaining)
        {
            return remaining <= 0f;
        }

        /// Progress 0..100 % for logs (total &lt;= 0 counts as done).
        public static int Percent(float remaining, float total)
        {
            if (total <= 0f) return 100;
            float p = (1f - remaining / total) * 100f;
            if (p < 0f) p = 0f;
            if (p > 100f) p = 100f;
            return (int)p;
        }
    }
}
