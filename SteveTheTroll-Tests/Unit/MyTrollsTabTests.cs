namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-119: the distance column of the F6 "My trolls" tab.</summary>
    [TestFixture]
    public class MyTrollsTabTests
    {
        static string D(float m) { return (string)ModAssembly.Call("SteveTheTroll.Ui.MyTrollsTab", "DistanceText", m); }

        [TestCase(-1f, "-")]
        [TestCase(float.NaN, "-")]
        [TestCase(0f, "0 m")]
        [TestCase(12.4f, "12 m")]
        [TestCase(12.6f, "13 m")]
        [TestCase(999.4f, "999 m")]
        [TestCase(1000f, "1.0 km")]
        [TestCase(1530f, "1.5 km")]
        public void DistanceText(float metres, string shown) { Assert.That(D(metres), Is.EqualTo(shown)); }
    }
}
