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

        // HitData.DamageModifier values in 1.0.20 (Cecil): Normal 0, Resistant 1, Weak 2, Immune 3, Ignore 4, VeryResistant 5, VeryWeak 6,
        // SlightlyResistant 7, SlightlyWeak 8.
        public const int ModResistant = 1;

        /// D-042: a tamed troll is at least Resistant: Normal / Weak / VeryWeak / SlightlyWeak / SlightlyResistant become Resistant; Resistant,
        /// VeryResistant, Immune and Ignore stay. Unknown values stay as they are.
        public static int AtLeastResistant(int modifier)
        {
            switch (modifier)
            {
                case 0: case 2: case 6: case 7: case 8: return ModResistant;
                default: return modifier;
            }
        }

        /// D-042: the troll defends its owner when the owner is hit within range (metres; a negative distance = unknown = no).
        public static bool Defends(float trollToOwner, float range)
        {
            return trollToOwner >= 0f && trollToOwner <= range;
        }

        /// D-042 troll spawn boost: interval / factor (never below 1 s), chance x factor (never above 100 %). factor &lt;= 1 changes nothing.
        public static float BoostedInterval(float interval, float factor)
        {
            if (factor <= 1f) return interval;
            float v = interval / factor;
            return v < 1f ? 1f : v;
        }

        public static float BoostedChance(float chancePercent, float factor)
        {
            if (factor <= 1f) return chancePercent;
            float v = chancePercent * factor;
            return v > 100f ? 100f : v;
        }

        /// D-044: "Steve 1", "Spencer 2", ...: the lowest number of `baseName` not used by a living troll's name in `taken`.
        public static string NextName(string baseName, System.Collections.Generic.IEnumerable<string> taken)
        {
            var used = new System.Collections.Generic.HashSet<string>();
            if (taken != null) foreach (var t in taken) if (t != null) used.Add(t);
            for (int i = 1; i < 10000; i++)
            {
                string n = baseName + " " + i;
                if (!used.Contains(n)) return n;
            }
            return baseName;
        }

        // D-050 source tiers: 0 = fallen logs and ore rocks, 1 = standing trees, 2 = anything else (stumps, saplings). -1 = not farmed.
        public const int TierLog = 0, TierTree = 1, TierOther = 2, TierNone = -1;
        const float TierStep = 1000f;

        /// D-050 (user: "focus the fallen trees before going for trees that are still up"): a lower tier ALWAYS wins, distance (m) only decides
        /// inside a tier. Expected outcome: the score (lower = better), or MaxValue for TierNone.
        public static float SourceScore(int tier, float distance)
        {
            if (tier < 0) return float.MaxValue;
            return tier * TierStep + (distance < 0f ? 0f : distance);
        }

        /// D-050: "other" sources (Destructible stumps, saplings, bushes) only count when at least minHeight metres tall - bushes and ground
        /// debris made the troll slap the ground (TEST-021). Logs, rocks and trees always count.
        public static int Tier(bool log, bool rock, bool tree, float height, float minHeight)
        {
            if (log || rock) return TierLog;
            if (tree) return TierTree;
            return height >= minHeight ? TierOther : TierNone;
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
