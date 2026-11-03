namespace SteveTheTroll.Trolls.Combat.Domain
{
    /// <summary>
    /// D-120 .. D-126 (user 2026-10-28 batch, all 1.0.0): pure rules (no Unity / Valheim types, unit-tested) for what the Troll Club and
    /// tamed trolls may break, the boosted spawns' item set, the global taming cap, the set's stagger resistance and the tamed trolls' swim speed.
    /// </summary>
    public static class BreakRules
    {
        /// D-120: prefab name starts the Troll Club breaks (case-insensitive): Yggdrasil shoots, scorched (Ashlands) trees, giant remains,
        /// petrified Gammeltrolls. Timberwood trees are found by their drops (TimberDrop) because their prefab names vary.
        /// Timberwood's item prefab is "Frostwood" (user 2026-10-28: "Timberwood has prefab: Frostwood").
        public const string TimberwoodItem = "Frostwood";
        public static readonly string[] ClubPrefixes = { "YggaShoot", "yggashoot_log", "AshlandsTree", "giant_skull", "giant_ribs", "DN_gammeltroll" };

        /// D-124: Ashlands fortress wall pieces a 2-star tamed troll may break (WearNTear). The Troll Club never damages them.
        public static readonly string[] FortressWallPrefixes = { "Ashlands_Wall", "Ashlands_WallBlock", "Ashlands_Fortress_Wall" };

        /// D-172 (user 2026-11-02: "Protect player pieces" - run 151111: rage swings at world objects next to a player-built wall ring damaged 5 walls by 38-48 %):
        /// a tamed troll's hit never damages a piece a player built (Ashlands fortress walls are not player-built, so a 2-star troll still breaks those, D-124).
        /// Expected outcome: false = the hit is dropped.
        public static bool TrollHitDamagesPiece(bool attackerIsTamedTroll, bool placedByPlayer) { return !(attackerIsTamedTroll && placedByPlayer); }

        /// The prefab part of an instance name ("YggaShoot1(Clone)" -> "YggaShoot1"). Expected outcome: never null.
        public static string PrefabName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            int i = name.IndexOf('(');
            return (i >= 0 ? name.Substring(0, i) : name).Trim();
        }

        static bool StartsWithAny(string name, string[] prefixes)
        {
            string n = PrefabName(name);
            if (n.Length == 0) return false;
            foreach (var p in prefixes) if (n.StartsWith(p, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// A drop item name that is Timberwood: the prefab "Frostwood" (or a "timber" material). Expected outcome: true for "Frostwood", false for "Wood".
        public static bool TimberDrop(string item)
        {
            if (string.IsNullOrEmpty(item)) return false;
            return string.Equals(item, TimberwoodItem, System.StringComparison.OrdinalIgnoreCase) || item.IndexOf("timber", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// D-120: the Troll Club hit on this object gets its full chop / pickaxe at any tool tier. Expected outcome: true for the listed
        /// prefabs and for a tree / log that drops timber.
        public static bool ClubTarget(string name, bool dropsTimber) { return dropsTimber || StartsWithAny(name, ClubPrefixes); }

        /// D-124: an Ashlands fortress wall piece. Expected outcome: true for "Ashlands_Wall_2x2(Clone)", false for "Ashlands_Ruins_Wall".
        public static bool FortressWall(string name) { return StartsWithAny(name, FortressWallPrefixes); }

        /// D-124: tamed trolls with 2 stars (level 3) break fortress walls. Expected outcome: level 3 or more.
        public static bool BreaksWalls(int level) { return level >= 3; }

        /// The hit damage used for chop and pickaxe on a special target: the highest of the given damages. Expected outcome: >= 0.
        public static float BreakDamage(float a, float b, float c)
        {
            float m = a > b ? a : b;
            m = m > c ? m : c;
            return m > 0f ? m : 0f;
        }

        public const string LogSet = "Log", UnarmedSet = "Unarmed";

        /// D-121 (user: with 0-1 tamed trolls and no set power every mod-spawned troll has a club; with the set power 50 % unarmed / 50 % club):
        /// the item set of a boosted spawn from a roll 0 ..&lt; 1. Expected outcome: "Log" or "Unarmed".
        public static string BoostedSet(bool setActive, float roll)
        {
            if (!setActive) return LogSet;
            return roll < 0.5f ? UnarmedSet : LogSet;
        }

        public const int GlobalTameCap = 15;

        /// D-123 (user: "no more than 15 trolls tamed globally per player"): another troll may be tamed. Expected outcome: living &lt; cap.
        public static bool MayTameMore(int living, int cap) { return living < (cap < 1 ? 1 : cap); }

        public const float SetStaggerResistance = 0.75f;

        /// D-125: stagger damage the set wearer takes (resistance 0 .. 1). Expected outcome: damage x (1 - resistance), never negative.
        public static float StaggerTaken(float damage, float resistance)
        {
            if (resistance < 0f) resistance = 0f;
            if (resistance > 1f) resistance = 1f;
            float d = damage * (1f - resistance);
            return d > 0f ? d : 0f;
        }

        public const float WadeDepth = 1.5f, WadeFactor = 3f, WadeLeaveMargin = 0.3f;

        public const float WaterKeepDistance = 15f, WaterKeepBand = 1.5f, InWaterDepth = 0.4f;

        /// D-131 (user: "when they enter water, they are to keep 15m away from the player at all times"): the troll counts as in water while the
        /// game has it swimming or the water at its feet is deeper than InWaterDepth (knee deep). Expected outcome: true in water.
        public static bool InWater(float depth, bool swimming) { return swimming || depth > InWaterDepth; }

        /// D-131: the troll stands still on the ring when it is within WaterKeepBand of the keep distance (no back-and-forth); otherwise it
        /// moves to the ring point (away when closer, after the player when farther). Expected outcome: true = hold.
        public static bool HoldsRing(float distance, float keep) { return distance >= keep - WaterKeepBand && distance <= keep + WaterKeepBand; }

        /// D-131: how far the troll is behind the ring (for the D-130 swim catch-up). Expected outcome: distance - keep, never below 0.
        public static float BehindRing(float distance, float keep) { float d = distance - (keep > 0f ? keep : 0f); return d > 0f ? d : 0f; }

        /// D-129 (user: "make them walk at 3x speed when they are in 1.5m into the water"): the troll wades now. Starts at `wadeDepth` of water,
        /// stops below wadeDepth - WadeLeaveMargin (no flapping at the edge); never while the game has it swimming. Expected outcome: wading or not.
        public static bool Wading(bool wasWading, float depth, float wadeDepth, bool swimming)
        {
            if (swimming) return false;
            return depth >= (wasWading ? wadeDepth - WadeLeaveMargin : wadeDepth);
        }

        /// D-129: the movement factor on the vanilla speeds - `wadeFactor` while wading ("3x faster than normal walking speed"), else the tamed
        /// factor (D-111). Expected outcome: wadeFactor or tamedFactor.
        public static float MoveFactor(bool wading, float tamedFactor, float wadeFactor) { return wading ? wadeFactor : tamedFactor; }

        /// D-122 (user: "up to 300% of base run speed while swimming; when following a slower player keep up with the player's speed") + D-130
        /// (user 2026-10-29: "a minimum speed advantage" over the followed player): the swim speed of a tamed troll. Not following: baseRun x
        /// maxFactor. Following: the player's speed (at least the vanilla swim speed) + `advantage`, rising towards the full speed when the
        /// troll is 3 - 8 m behind (`distance`); never above baseRun x maxFactor. With distance 0 and advantage 0 this is the D-122 rule.
        /// Expected outcome: a speed in m/s.
        public static float SwimSpeed(float baseSwim, float baseRun, float maxFactor, bool following, float playerSpeed, float distance = 0f, float advantage = 0f)
        {
            float max = baseRun * maxFactor;
            if (max < baseSwim) max = baseSwim;
            if (!following) return max;
            float pace = (playerSpeed > baseSwim ? playerSpeed : baseSwim) + (advantage > 0f ? advantage : 0f);
            float t = (distance - 3f) / 5f;
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            float s = pace + (max - pace) * t;
            return s < max ? s : max;
        }
    }
}
