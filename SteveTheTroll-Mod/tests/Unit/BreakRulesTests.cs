using NUnit.Framework;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>
    /// D-120 .. D-125: BreakRules - what the Troll Club / 2-star trolls break, boosted spawn sets, the global taming cap,
    /// the set's stagger resistance and the tamed trolls' swim speed.
    /// </summary>
    [TestFixture]
    public class BreakRulesTests
    {
        // D-172 (user: "Protect player pieces"): a tamed troll's hit never damages a player-built piece; fortress walls (not player-built) and other attackers are untouched
        [TestCase(true, true, false)] [TestCase(true, false, true)] [TestCase(false, true, true)] [TestCase(false, false, true)]
        public void TrollHitDamagesPiece(bool tamedTroll, bool playerBuilt, bool expected) { Assert.That(SteveTheTroll.Features.Trolls.Combat.Domain.BreakRules.TrollHitDamagesPiece(tamedTroll, playerBuilt), Is.EqualTo(expected)); }

        const string T = "SteveTheTroll.Features.Trolls.Combat.Domain.BreakRules";
        static object Call(string m, params object[] a) { return ModAssembly.Call(T, m, a); }

        [TestCase("YggaShoot1(Clone)", true)]
        [TestCase("yggashoot_log", true)]
        [TestCase("AshlandsTree6_big", true)]
        [TestCase("giant_skull", true)]
        [TestCase("giant_skull_frac(Clone)", true)]
        [TestCase("giant_ribs_frac", true)]
        [TestCase("DN_gammeltrollFrac01", true)]
        [TestCase("dn_GAMMELTROLL02", true)]
        [TestCase("Beech1", false)]
        [TestCase("Ashlands_Wall_2x2", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void ClubTarget_ByName(string name, bool expected) { Assert.That((bool)Call("ClubTarget", name, false), Is.EqualTo(expected)); }

        [Test] public void TimberwoodItem_IsFrostwood() { Assert.That(ModAssembly.GetStatic(T, "TimberwoodItem"), Is.EqualTo("Frostwood")); }

        [Test] public void ClubTarget_TimberDrops_Counts() { Assert.That((bool)Call("ClubTarget", "SomeNorthTree", true), Is.True); }

        [TestCase("Frostwood", true)]
        [TestCase("frostwood", true)]
        [TestCase("Timberwood", true)]
        [TestCase("TIMBER", true)]
        [TestCase("Wood", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void TimberDrop(string item, bool expected) { Assert.That((bool)Call("TimberDrop", item), Is.EqualTo(expected)); }

        [TestCase("Ashlands_Wall_2x2(Clone)", true)]
        [TestCase("Ashlands_WallBlock", true)]
        [TestCase("Ashlands_Fortress_Wall_Pillar", true)]
        [TestCase("Ashlands_Ruins_Wall_Broken3_4x6", false)]
        [TestCase("stone_wall_4x2", false)]
        public void FortressWall(string name, bool expected) { Assert.That((bool)Call("FortressWall", name), Is.EqualTo(expected)); }

        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        public void BreaksWalls_TwoStarsUp(int level, bool expected) { Assert.That((bool)Call("BreaksWalls", level), Is.EqualTo(expected)); }

        [Test] public void PrefabName_StripsClone() { Assert.That(Call("PrefabName", " giant_ribs (Clone)"), Is.EqualTo("giant_ribs")); }
        [Test] public void PrefabName_Null_Empty() { Assert.That(Call("PrefabName", new object[] { null }), Is.EqualTo("")); }

        [TestCase(150f, 50f, 50f, 150f)]
        [TestCase(10f, 80f, 20f, 80f)]
        [TestCase(10f, 20f, 90f, 90f)]
        [TestCase(-5f, -1f, -2f, 0f)]
        public void BreakDamage_Highest(float a, float b, float c, float expected) { Assert.That((float)Call("BreakDamage", a, b, c), Is.EqualTo(expected)); }

        [TestCase(false, 0f, "Log")]
        [TestCase(false, 0.99f, "Log")]
        [TestCase(true, 0f, "Unarmed")]
        [TestCase(true, 0.4999f, "Unarmed")]
        [TestCase(true, 0.5f, "Log")]
        [TestCase(true, 0.99f, "Log")]
        public void BoostedSet(bool set, float roll, string expected) { Assert.That(Call("BoostedSet", set, roll), Is.EqualTo(expected)); }

        [TestCase(0, 15, true)]
        [TestCase(14, 15, true)]
        [TestCase(15, 15, false)]
        [TestCase(16, 15, false)]
        [TestCase(0, 0, true)]
        [TestCase(1, 0, false)]
        public void MayTameMore(int living, int cap, bool expected) { Assert.That((bool)Call("MayTameMore", living, cap), Is.EqualTo(expected)); }

        [TestCase(100f, 0.75f, 25f)]
        [TestCase(100f, 0f, 100f)]
        [TestCase(100f, 1f, 0f)]
        [TestCase(100f, -1f, 100f)]
        [TestCase(100f, 2f, 0f)]
        [TestCase(-10f, 0.5f, 0f)]
        public void StaggerTaken(float dmg, float res, float expected) { Assert.That((float)Call("StaggerTaken", dmg, res), Is.EqualTo(expected).Within(0.0001f)); }

        // vanilla troll: swim 1.5, run 6 (x1.25 tamed not included here)
        [TestCase(false, 0f, 18f)]
        [TestCase(true, 0f, 1.5f)]
        [TestCase(true, 1f, 1.5f)]
        [TestCase(true, 5f, 5f)]
        [TestCase(true, 18f, 18f)]
        [TestCase(true, 30f, 18f)]
        public void SwimSpeed(bool following, float player, float expected) { Assert.That((float)Call("SwimSpeed", 1.5f, 6f, 3f, following, player, 0f, 0f), Is.EqualTo(expected).Within(0.0001f)); }

        // D-130: following = the player's pace + advantage, rising to the full speed 3 - 8 m behind
        [TestCase(5f, 0f, 1.5f, 6.5f)]
        [TestCase(5f, 3f, 1.5f, 6.5f)]
        [TestCase(5f, 5.5f, 1.5f, 12.25f)]
        [TestCase(5f, 8f, 1.5f, 18f)]
        [TestCase(5f, 20f, 1.5f, 18f)]
        [TestCase(0f, 0f, 1.5f, 3f)]
        [TestCase(17f, 0f, 1.5f, 18f)]
        [TestCase(5f, 0f, -1f, 5f)]
        public void SwimSpeed_FollowAdvantage(float player, float distance, float advantage, float expected)
        {
            Assert.That((float)Call("SwimSpeed", 1.5f, 6f, 3f, true, player, distance, advantage), Is.EqualTo(expected).Within(0.0001f));
        }

        [Test] public void SwimSpeed_MaxBelowSwim_KeepsSwim() { Assert.That((float)Call("SwimSpeed", 2f, 1f, 1f, false, 0f, 0f, 0f), Is.EqualTo(2f)); }

        // D-129: wading from 1.5 m, leaving below 1.2 m, never while swimming
        [TestCase(false, 1.49f, false, false)]
        [TestCase(false, 1.5f, false, true)]
        [TestCase(false, 3f, false, true)]
        [TestCase(true, 1.25f, false, true)]
        [TestCase(true, 1.2f, false, true)]
        [TestCase(true, 1.19f, false, false)]
        [TestCase(false, 3f, true, false)]
        [TestCase(true, 3f, true, false)]
        [TestCase(false, -2f, false, false)]
        public void Wading(bool was, float depth, bool swimming, bool expected) { Assert.That((bool)Call("Wading", was, depth, 1.5f, swimming), Is.EqualTo(expected)); }

        [TestCase(true, 3f)]
        [TestCase(false, 1.25f)]
        public void MoveFactor(bool wading, float expected) { Assert.That((float)Call("MoveFactor", wading, 1.25f, 3f), Is.EqualTo(expected)); }

        // D-131: in water = swimming or deeper than knee deep (0.4 m); ring 15 m +- 1.5 m; catch-up counts the distance behind the ring
        [TestCase(0.41f, false, true)]
        [TestCase(0.4f, false, false)]
        [TestCase(0f, true, true)]
        [TestCase(-1f, false, false)]
        public void InWater(float depth, bool swimming, bool expected) { Assert.That((bool)Call("InWater", depth, swimming), Is.EqualTo(expected)); }

        [TestCase(13.49f, false)]
        [TestCase(13.5f, true)]
        [TestCase(15f, true)]
        [TestCase(16.5f, true)]
        [TestCase(16.51f, false)]
        [TestCase(3f, false)]
        public void HoldsRing(float distance, bool expected) { Assert.That((bool)Call("HoldsRing", distance, 15f), Is.EqualTo(expected)); }

        [TestCase(20f, 15f, 5f)]
        [TestCase(15f, 15f, 0f)]
        [TestCase(5f, 15f, 0f)]
        [TestCase(5f, -1f, 5f)]
        public void BehindRing(float distance, float keep, float expected) { Assert.That((float)Call("BehindRing", distance, keep), Is.EqualTo(expected)); }

        [Test] public void Constants()
        {
            Assert.That(ModAssembly.GetStatic(T, "GlobalTameCap"), Is.EqualTo(15));
            Assert.That(ModAssembly.GetStatic(T, "SetStaggerResistance"), Is.EqualTo(0.75f));
            Assert.That(ModAssembly.GetStatic(T, "WadeDepth"), Is.EqualTo(1.5f));
            Assert.That(ModAssembly.GetStatic(T, "WadeFactor"), Is.EqualTo(3f));
            Assert.That(ModAssembly.GetStatic(T, "WaterKeepDistance"), Is.EqualTo(15f));
        }
    }
}
