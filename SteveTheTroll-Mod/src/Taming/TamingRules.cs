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

        /// D-076: as DecideWild, with a troll limit: ownNear = the wearer's own tamed trolls within OwnTrollRange, limit = how many it may have
        /// (1, or 5 with the full costume set). Expected outcome: CountDown while ownNear is below the limit, else Nothing (D-084: no own troll is called to follow any more).
        public static Step DecideWildLimit(float wearerDistance, float tameRange, int ownNear, int limit)
        {
            if (wearerDistance < 0f || wearerDistance > tameRange) return Step.Nothing;
            return ownNear >= (limit < 1 ? 1 : limit) ? Step.Nothing : Step.CountDown;     // D-084: at the limit the wild troll is left alone (no follow call)
        }

        /// D-084 boosted troll spawns around the wearer's bed: the game's max spawned for a zone within BedRadius of the bed. 0 = no boost
        /// (vanilla rates and limits): slippers with 0 or 1 tamed troll(s) in total -> 2; the whole set (3 pieces + slippers) with at most
        /// 5 tamed trolls in total -> 6. Expected outcome: the cap (2 / 6) or 0.
        public static int SpawnCap(bool slippers, bool fullSet, int tamedTotal, float zoneToBed, float bedRadius)
        {
            if (!slippers || zoneToBed < 0f || zoneToBed > bedRadius) return 0;
            if (fullSet && tamedTotal <= 5) return 6;
            return tamedTotal <= 1 ? 2 : 0;
        }

        /// D-094 (user: "yes, but not closer than 200m"): a boosted troll may spawn at this flat distance from the bed - from minRadius to
        /// bedRadius, both inclusive. Expected outcome: false nearer the house, beyond the radius or with no bed (negative distance).
        public static bool InSpawnRing(float toBed, float minRadius, float bedRadius)
        {
            return toBed >= 0f && toBed >= minRadius && toBed <= bedRadius;
        }

        /// D-085: one more troll may spawn around the bed while the trolls counted there (wild + tamed) are fewer than the cap.
        /// Expected outcome: false at / over the cap or with no count yet (negative).
        public static bool SpawnAllowed(int trollsAroundBed, int cap)
        {
            return trollsAroundBed >= 0 && trollsAroundBed < cap;
        }

        /// D-085: healing of a tamed troll near the bed for dt seconds (rate HP/s, never above the max). Expected outcome: the HP to add (0 or more).
        public static float BedHeal(float health, float maxHealth, float rate, float dt)
        {
            if (health <= 0f || health >= maxHealth || rate <= 0f || dt <= 0f) return 0f;
            float add = rate * dt;
            return add < maxHealth - health ? add : maxHealth - health;
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

        /// D-059 troll spawn boost: a spawn try at least every `seconds` (the game's interval when it is shorter; never below 1 s), each try
        /// with 100 % chance (BoostedChance). seconds &lt;= 0 = off (the game's values). The old x-factor (D-042 / D-052: interval 4000 s ->
        /// 800 s, chance 5 % -> 25 %) still meant about one troll per 53 minutes.
        public static float BoostedInterval(float interval, float seconds)
        {
            if (seconds <= 0f) return interval;
            float v = seconds < 1f ? 1f : seconds;
            return interval < v ? interval : v;
        }

        public static float BoostedChance(float chancePercent, float seconds) { return seconds <= 0f ? chancePercent : 100f; }

        /// D-059: what a farm hit of a tamed troll carries: the higher of chop / pickaxe in both (so trees, logs, rocks and ore veins all take
        /// the full damage). Expected outcome: (chop, pickaxe) both = max(chop, pickaxe).
        public static float FarmDamage(float chop, float pickaxe) { return chop > pickaxe ? chop : pickaxe; }

        /// D-068 (user: "25% for 1 star and 2 star (each) and 50% for 0 star"): stars of a boosted troll spawn from a roll 0 ..&lt; 1.
        /// The game's own roll: 10 % per extra level (about 9 % one star, 1 % two stars).
        public static int BoostedStars(float roll)
        {
            if (roll < 0.5f) return 0;
            return roll < 0.75f ? 1 : 2;
        }

        /// D-065: a crowd (at least `crowd` attackers) gets the ground slams, at most every `cooldown` seconds (`sinceLast` &lt; 0 = never slammed).
        public static bool Slam(int attackers, int crowd, float sinceLast, float cooldown)
        {
            return attackers >= crowd && (sinceLast < 0f || sinceLast > cooldown);
        }

        /// T-008e: the troll defends its owner when the owner wears the slippers, or during a raid.
        public static bool DefendsOwner(bool wears, bool raid) { return wears || raid; }

        /// D-059: where on the segment a..b (x / z, metres) the point p is nearest, as 0..1 (0 for a zero-length segment).
        public static float SegmentT(float ax, float az, float bx, float bz, float px, float pz)
        {
            float dx = bx - ax, dz = bz - az, len2 = dx * dx + dz * dz;
            if (len2 < 1e-6f) return 0f;
            float t = ((px - ax) * dx + (pz - az) * dz) / len2;
            return t < 0f ? 0f : (t > 1f ? 1f : t);
        }

        /// D-059: a tamed troll's farm hits count as this tool tier (any tree / rock / vein of the game needs less).
        public const int TamedToolTier = 100;

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
