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
