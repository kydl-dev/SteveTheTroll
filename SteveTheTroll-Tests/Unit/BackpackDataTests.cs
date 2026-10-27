using System;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-053: the user's backpack construction (stt_dumpbuild 19:51): 23 pieces, 2 chests, a 2 x 2 x 2 m cage, struts at the pivot.</summary>
    [TestFixture]
    public class BackpackDataTests
    {
        const string T = "SteveTheTroll.Backpack.BackpackData";

        static Array Pieces() { return (Array)ModAssembly.GetStatic(T, "Pieces"); }
        static object F(object o, string name) { return o.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).GetValue(o); }

        [Test]
        public void TwentyThreePieces_TwoChests()
        {
            var p = Pieces();
            Assert.That(p.Length, Is.EqualTo(23));
            var chests = (int[])ModAssembly.GetStatic(T, "Chests");
            Assert.That(chests.Length, Is.EqualTo(2));
            foreach (var i in chests) Assert.That(F(p.GetValue(i), "Prefab"), Is.EqualTo("piece_chest"));
        }

        [Test]
        public void Cage_IsTwoMetres_StrutsAtPivot()
        {
            float minX = 1e9f, maxX = -1e9f, minY = 1e9f, maxY = -1e9f, maxZ = -1e9f;
            int struts = 0;
            foreach (var o in Pieces())
            {
                float x = (float)F(o, "X"), y = (float)F(o, "Y"), z = (float)F(o, "Z");
                string prefab = (string)F(o, "Prefab");
                if (prefab == "woodiron_beam_45") { struts++; Assert.That(z, Is.EqualTo(0f).Within(1e-4), "struts at the pivot plane"); Assert.That(y, Is.EqualTo(0f).Within(1e-4)); }
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); maxZ = Math.Max(maxZ, z);
            }
            Assert.That(struts, Is.EqualTo(2));
            Assert.That(maxX - minX, Is.EqualTo(2f).Within(1e-3), "poles 2 m apart");
            Assert.That(maxY - minY, Is.EqualTo(2f).Within(1e-3), "floor to roof 2 m");
            Assert.That(maxZ, Is.GreaterThan(2.9f), "the cage reaches ~3 m away from the troll's back");
        }
    }
}
