using System;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-064: the troll depot from the user's dump (23:07): 6 poles + 2 floors, 4 reinforced chests mirrored, stack limit 5.</summary>
    [TestFixture]
    public class DepotDataTests
    {
        const string T = "SteveTheTroll.Backpack.DepotData";
        static Array Get(string n) { return (Array)ModAssembly.GetStatic(T, n); }
        static object F(object o, string name) { return o.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).GetValue(o); }

        [Test]
        public void SixPolesTwoFloors_NoStoneFloors()
        {
            int poles = 0, floors = 0;
            foreach (var p in Get("Frame"))
            {
                string n = (string)F(p, "Prefab");
                Assert.That(n, Is.Not.EqualTo("stone_floor_2x2"));
                if (n == "wood_pole") poles++; else if (n == "wood_floor") floors++;
            }
            Assert.That(poles, Is.EqualTo(6));
            Assert.That(floors, Is.EqualTo(2));
        }

        [Test]
        public void FourChests_MirroredAndOnTheGround()
        {
            var chests = Get("Chests");
            Assert.That(chests.Length, Is.EqualTo(4));
            foreach (var c in chests)
            {
                Assert.That((string)F(c, "Prefab"), Is.EqualTo("piece_chest"));
                Assert.That((float)F(c, "Y"), Is.EqualTo(0f));
                float x = (float)F(c, "X"), z = (float)F(c, "Z");
                bool mirror = false;
                foreach (var m in chests) if (Math.Abs((float)F(m, "X") + x) < 1e-4 && Math.Abs((float)F(m, "Z") - z) < 1e-4) mirror = true;
                Assert.That(mirror, Is.True, "chest at x " + x + " has a partner at -x");
            }
        }

        [Test]
        public void StackLimitFive() { Assert.That((int)ModAssembly.GetStatic(T, "MaxStack"), Is.EqualTo(5)); }
    }
}
