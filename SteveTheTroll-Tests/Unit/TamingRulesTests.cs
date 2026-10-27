namespace SteveTheTroll.Tests.Unit
{
    /// <summary>
    /// SteveTheTroll.Taming.TamingRules (T-008a, D-037 / D-038): a wild troll counts down only with a slipper wearer within the taming
    /// range (150 m) and no own tamed troll of that wearer within 300 m; with one, the own troll is called instead. 60 s in total.
    /// </summary>
    [TestFixture]
    public class TamingRulesTests
    {
        const string T = "SteveTheTroll.Taming.TamingRules";

        static string Decide(float wearer, float own)
        {
            return ModAssembly.Call(T, "DecideWild", wearer, 150f, own, 300f).ToString();
        }

        [Test] public void NoWearer_Nothing() { Assert.That(Decide(-1f, -1f), Is.EqualTo("Nothing")); }
        [Test] public void WearerOutOfRange_Nothing() { Assert.That(Decide(150.1f, -1f), Is.EqualTo("Nothing")); }
        [Test] public void WearerAtRangeEdge_CountsDown() { Assert.That(Decide(150f, -1f), Is.EqualTo("CountDown")); }
        [Test] public void WearerClose_NoOwnTroll_CountsDown() { Assert.That(Decide(10f, -1f), Is.EqualTo("CountDown")); }
        [Test] public void OwnTrollWithin300_CallsIt() { Assert.That(Decide(10f, 300f), Is.EqualTo("CallOwnTroll")); }
        [Test] public void OwnTrollBeyond300_CountsDown() { Assert.That(Decide(10f, 300.5f), Is.EqualTo("CountDown")); }
        [Test] public void OwnTrollNear_ButWearerOutOfRange_Nothing() { Assert.That(Decide(200f, 5f), Is.EqualTo("Nothing")); }

        [Test]
        public void CountDown_SubtractsAndStopsAtZero()
        {
            Assert.That((float)ModAssembly.Call(T, "CountDown", 60f, 3f), Is.EqualTo(57f).Within(1e-4));
            Assert.That((float)ModAssembly.Call(T, "CountDown", 2f, 3f), Is.EqualTo(0f));
            Assert.That((float)ModAssembly.Call(T, "CountDown", 10f, -5f), Is.EqualTo(10f));
        }

        [Test]
        public void SixtySecondsOfTicks_Tames()
        {
            float left = 60f;
            int ticks = 0;
            while (!(bool)ModAssembly.Call(T, "Done", left) && ticks < 1000) { left = (float)ModAssembly.Call(T, "CountDown", left, 3f); ticks++; }
            Assert.That(ticks, Is.EqualTo(20), "60 s in 3 s ticks");
        }

        // D-042: tamed trolls are at least Resistant (DamageModifier ints of 1.0.20)
        [TestCase(0, 1)] [TestCase(2, 1)] [TestCase(6, 1)] [TestCase(7, 1)] [TestCase(8, 1)]
        [TestCase(1, 1)] [TestCase(3, 3)] [TestCase(4, 4)] [TestCase(5, 5)] [TestCase(42, 42)]
        public void AtLeastResistant(int before, int after) { Assert.That((int)ModAssembly.Call(T, "AtLeastResistant", before), Is.EqualTo(after)); }

        [Test]
        public void Defends_Within30Only()
        {
            Assert.That((bool)ModAssembly.Call(T, "Defends", 30f, 30f), Is.True);
            Assert.That((bool)ModAssembly.Call(T, "Defends", 30.1f, 30f), Is.False);
            Assert.That((bool)ModAssembly.Call(T, "Defends", -1f, 30f), Is.False);
        }

        [Test]
        public void SpawnBoost_TryEveryNSeconds_FullChance()
        {
            Assert.That((float)ModAssembly.Call(T, "BoostedInterval", 4000f, 120f), Is.EqualTo(120f), "the game's troll entry: 4000 s -> 120 s");
            Assert.That((float)ModAssembly.Call(T, "BoostedInterval", 60f, 120f), Is.EqualTo(60f), "a shorter game interval stays");
            Assert.That((float)ModAssembly.Call(T, "BoostedInterval", 4000f, 0.2f), Is.EqualTo(1f), "never below 1 s");
            Assert.That((float)ModAssembly.Call(T, "BoostedInterval", 4000f, 0f), Is.EqualTo(4000f), "0 = off");
            Assert.That((float)ModAssembly.Call(T, "BoostedChance", 5f, 120f), Is.EqualTo(100f));
            Assert.That((float)ModAssembly.Call(T, "BoostedChance", 5f, 0f), Is.EqualTo(5f), "0 = off");
        }

        [Test]
        public void SegmentT_NearestPointOnALog()
        {
            Assert.That((float)ModAssembly.Call(T, "SegmentT", 0f, 0f, 10f, 0f, 3f, 5f), Is.EqualTo(0.3f).Within(1e-4), "beside the log");
            Assert.That((float)ModAssembly.Call(T, "SegmentT", 0f, 0f, 10f, 0f, -4f, 1f), Is.EqualTo(0f), "before its start");
            Assert.That((float)ModAssembly.Call(T, "SegmentT", 0f, 0f, 10f, 0f, 14f, -2f), Is.EqualTo(1f), "past its end");
            Assert.That((float)ModAssembly.Call(T, "SegmentT", 2f, 2f, 2f, 2f, 9f, 9f), Is.EqualTo(0f), "zero length");
        }

        [Test]
        public void FarmDamage_HigherOfChopAndPickaxe()
        {
            Assert.That((float)ModAssembly.Call(T, "FarmDamage", 120f, 0f), Is.EqualTo(120f));
            Assert.That((float)ModAssembly.Call(T, "FarmDamage", 30f, 90f), Is.EqualTo(90f));
            Assert.That((float)ModAssembly.Call(T, "FarmDamage", 0f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void NextName_LowestFree()
        {
            Assert.That(ModAssembly.Call(T, "NextName", "Steve", new string[0]), Is.EqualTo("Steve 1"));
            Assert.That(ModAssembly.Call(T, "NextName", "Steve", new[] { "Steve 1", "Steve 3", "Spencer 2" }), Is.EqualTo("Steve 2"));
            Assert.That(ModAssembly.Call(T, "NextName", "Spencer", new[] { "Steve 1", "Spencer 1" }), Is.EqualTo("Spencer 2"));
        }

        // D-050: a fallen log / ore rock always beats a standing tree, a tree always beats a stump / sapling; distance only inside a tier
        [Test]
        public void SourceScore_TierBeforeDistance()
        {
            float log = (float)ModAssembly.Call(T, "SourceScore", 0, 49f), tree = (float)ModAssembly.Call(T, "SourceScore", 1, 1f), other = (float)ModAssembly.Call(T, "SourceScore", 2, 0f);
            Assert.That(log, Is.LessThan(tree), "a log 49 m away before a tree 1 m away");
            Assert.That(tree, Is.LessThan(other));
            Assert.That((float)ModAssembly.Call(T, "SourceScore", 0, 3f), Is.LessThan((float)ModAssembly.Call(T, "SourceScore", 0, 5f)));
            Assert.That((float)ModAssembly.Call(T, "SourceScore", -1, 0f), Is.EqualTo(float.MaxValue));
        }

        [TestCase(true, false, false, 0.3f, 0)]
        [TestCase(false, true, false, 0.5f, 0)]
        [TestCase(false, false, true, 0.2f, 1)]
        [TestCase(false, false, false, 2f, 2)]
        [TestCase(false, false, false, 0.8f, -1)]
        public void Tier(bool log, bool rock, bool tree, float height, int expected)
        {
            Assert.That((int)ModAssembly.Call(T, "Tier", log, rock, tree, height, 1.2f), Is.EqualTo(expected));
        }

        [Test]
        public void Percent_Clamped()
        {
            Assert.That((int)ModAssembly.Call(T, "Percent", 30f, 60f), Is.EqualTo(50));
            Assert.That((int)ModAssembly.Call(T, "Percent", 90f, 60f), Is.EqualTo(0));
            Assert.That((int)ModAssembly.Call(T, "Percent", -5f, 60f), Is.EqualTo(100));
            Assert.That((int)ModAssembly.Call(T, "Percent", 5f, 0f), Is.EqualTo(100));
        }
    }
}
