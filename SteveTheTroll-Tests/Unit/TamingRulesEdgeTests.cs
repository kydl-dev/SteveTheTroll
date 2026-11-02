
namespace SteveTheTroll.Tests.Unit
{
    /// <summary>
    /// D-086: boundary cases of SteveTheTroll.Taming.TamingRules found by Stryker.NET (mutants that survived TamingRulesTests): exact
    /// range edges, zero distances and arithmetic that a "nice" input could not tell apart. Mutants left alive on purpose are equivalent
    /// (e.g. "dt &lt;= 0" vs "dt &lt; 0" in BedHeal: both give 0 HP).
    /// </summary>
    [TestFixture]
    public class TamingRulesEdgeTests
    {
        const string T = "SteveTheTroll.Taming.TamingRules";

        static object Call(string m, params object[] a) { return ModAssembly.Call(T, m, a); }

        // DecideWild: a wearer standing ON the troll (0 m) tames; an own troll at 0 m blocks
        [Test] public void Wild_WearerAtZero_CountsDown() { Assert.That(Call("DecideWild", 0f, 150f, -1f, 300f).ToString(), Is.EqualTo("CountDown")); }
        [Test] public void Wild_OwnTrollAtZero_Blocks() { Assert.That(Call("DecideWild", 10f, 150f, 0f, 300f).ToString(), Is.EqualTo("CallOwnTroll")); }

        // DecideWildLimit: range edges and limit 0 (= 1)
        [Test] public void Limit_WearerAtZero_CountsDown() { Assert.That(Call("DecideWildLimit", 0f, 150f, 0, 1).ToString(), Is.EqualTo("CountDown")); }
        [Test] public void Limit_WearerAtRangeEdge_CountsDown() { Assert.That(Call("DecideWildLimit", 150f, 150f, 0, 1).ToString(), Is.EqualTo("CountDown")); }
        [Test] public void Limit_ZeroLimit_NoneNear_CountsDown() { Assert.That(Call("DecideWildLimit", 10f, 150f, 0, 0).ToString(), Is.EqualTo("CountDown")); }

        // SpawnCap: the bed zone itself (0 m) and the radius edge both count
        [Test] public void Cap_AtBed_Two() { Assert.That((int)Call("SpawnCap", true, false, 0, 0f, 500f), Is.EqualTo(2)); }
        [Test] public void Cap_AtRadiusEdge_Six() { Assert.That((int)Call("SpawnCap", true, true, 0, 500f, 500f), Is.EqualTo(6)); }

        // D-094: boosted trolls spawn 200 .. 500 m from the bed (both edges count), never at the house, never without a bed
        [TestCase(199.9f, false)]
        [TestCase(200f, true)]
        [TestCase(350f, true)]
        [TestCase(500f, true)]
        [TestCase(500.1f, false)]
        [TestCase(0f, false)]
        [TestCase(-1f, false)]
        public void SpawnRing(float toBed, bool expected) { Assert.That((bool)Call("InSpawnRing", toBed, 200f, 500f), Is.EqualTo(expected)); }
        [Test] public void SpawnRing_NoMinimum_BedItselfCounts() { Assert.That((bool)Call("InSpawnRing", 0f, 0f, 500f), Is.True); }

        // SpawnAllowed: an empty bed area may spawn
        [Test] public void Spawn_NoneYet_Allowed() { Assert.That((bool)Call("SpawnAllowed", 0, 2), Is.True); }

        // BedHeal: rate x dt (not rate / dt) for a half-second tick
        [Test] public void Heal_HalfSecond_Half() { Assert.That((float)Call("BedHeal", 1000f, 5400f, 50f, 0.5f), Is.EqualTo(25f).Within(1e-4)); }

        // Defends: owner on top of the troll
        [Test] public void Defends_AtZero() { Assert.That((bool)Call("Defends", 0f, 10f), Is.True); }

        // Slam: exactly at the cooldown is still too early; 0 s since the last slam is too early
        [Test] public void Slam_AtCooldown_No() { Assert.That((bool)Call("Slam", 3, 3, 10f, 10f), Is.False); }
        [Test] public void Slam_JustSlammed_No() { Assert.That((bool)Call("Slam", 3, 3, 0f, 10f), Is.False); }

        // SegmentT: an off-axis point (every sign and term matters): a (1,2), b (5,5), p (3,7) -> 23 / 25
        [Test] public void SegmentT_OffAxis() { Assert.That((float)Call("SegmentT", 1f, 2f, 5f, 5f, 3f, 7f), Is.EqualTo(0.92f).Within(1e-4)); }

        // SourceScore: a negative (unknown) distance counts as 0 inside its tier
        [Test] public void Score_NegativeDistance_TierOnly() { Assert.That((float)Call("SourceScore", 1, -5f), Is.EqualTo(1000f)); }

        // Tier: exactly minHeight counts
        [Test] public void Tier_AtMinHeight_Other() { Assert.That((int)Call("Tier", false, false, false, 1.2f, 1.2f), Is.EqualTo(2)); }

        // TEST-048 / TEST-049: the found ZDOs (already disjoint from the game's list) are added once each
        static int AddDistinct(System.Collections.Generic.List<int> into, System.Collections.Generic.List<int> found)
        {
            var m = ModAssembly.Type(T).GetMethod("AddDistinct", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).MakeGenericMethod(typeof(int));
            return (int)m.Invoke(null, new object[] { into, found, new System.Collections.Generic.HashSet<int>() });
        }

        [Test]
        public void AddDistinct_AddsEachFoundOnce()
        {
            var into = new System.Collections.Generic.List<int> { 1, 2 };
            int added = AddDistinct(into, new System.Collections.Generic.List<int> { 4, 4, 5, 4 });
            Assert.That(added, Is.EqualTo(2));
            Assert.That(into, Is.EqualTo(new[] { 1, 2, 4, 5 }));
        }

        [Test]
        public void AddDistinct_NothingFound_AddsNothing()
        {
            var into = new System.Collections.Generic.List<int> { 7, 8 };
            Assert.That(AddDistinct(into, new System.Collections.Generic.List<int>()), Is.EqualTo(0));
            Assert.That(into.Count, Is.EqualTo(2));
        }
    }
}
