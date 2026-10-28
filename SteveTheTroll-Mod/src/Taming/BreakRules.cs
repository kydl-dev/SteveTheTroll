namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-120 .. D-126 (user 2026-10-28 batch, all 1.0.0): pure rules (no Unity / Valheim types, unit-tested) for what the Troll Club and
    /// tamed trolls may break, the boosted spawns' item set, the global taming cap, the set's stagger resistance and the tamed trolls' swim speed.
    /// </summary>
    public static class BreakRules
    {
        /// D-120: prefab name starts the Troll Club breaks (case-insensitive): Yggdrasil shoots, scorched (Ashlands) trees, giant remains,
        /// petrified Gammeltrolls. Timberwood trees are found by their drops (TimberDrop) because their prefab names vary.
        public static readonly string[] ClubPrefixes = { "YggaShoot", "yggashoot_log", "AshlandsTree", "giant_skull", "giant_ribs", "DN_gammeltroll" };

        /// D-124: Ashlands fortress wall pieces a 2-star tamed troll may break (WearNTear). The Troll Club never damages them.
        public static readonly string[] FortressWallPrefixes = { "Ashlands_Wall", "Ashlands_WallBlock", "Ashlands_Fortress_Wall" };

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

        /// A drop item name that is Timberwood (or another "timber" material). Expected outcome: true for "Timberwood", false for "Wood".
        public static bool TimberDrop(string item) { return !string.IsNullOrEmpty(item) && item.IndexOf("timber", System.StringComparison.OrdinalIgnoreCase) >= 0; }

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

        /// D-122 (user: "up to 300% of base run speed while swimming; when following a slower player keep up with the player's speed"):
        /// the swim speed of a tamed troll. Not following: baseRun x maxFactor. Following: the player's speed, at least the vanilla swim speed,
        /// at most baseRun x maxFactor. Expected outcome: a speed in m/s.
        public static float SwimSpeed(float baseSwim, float baseRun, float maxFactor, bool following, float playerSpeed)
        {
            float max = baseRun * maxFactor;
            if (max < baseSwim) max = baseSwim;
            if (!following) return max;
            float s = playerSpeed > baseSwim ? playerSpeed : baseSwim;
            return s < max ? s : max;
        }
    }
}
