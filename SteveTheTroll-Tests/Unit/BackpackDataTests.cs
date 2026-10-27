using System;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-057: the user's backpack construction (stt_dumpbuild 20:51): 41 pieces (2 stone floors left out), 4 reinforced chests, struts at the pivot.</summary>
    [TestFixture]
    public class BackpackDataTests
    {
        const string T = "SteveTheTroll.Backpack.BackpackData";

        static Array Pieces() { return (Array)ModAssembly.GetStatic(T, "Pieces"); }
        static object F(object o, string name) { return o.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).GetValue(o); }

        [Test]
        public void FortyOnePieces_FourReinforcedChests_NoStoneFloors()
        {
            var p = Pieces();
            Assert.That(p.Length, Is.EqualTo(41));
            foreach (var o in p) Assert.That(F(o, "Prefab"), Is.Not.EqualTo("stone_floor_2x2"), "support floors are not part of the backpack");
            var chests = (int[])ModAssembly.GetStatic(T, "Chests");
            Assert.That(chests.Length, Is.EqualTo(4));
            foreach (var i in chests) Assert.That(F(p.GetValue(i), "Prefab"), Is.EqualTo("piece_chest"));
        }

        [Test]
        public void StrutsAtPivot_CageReachesAway()
        {
            float maxZ = -1e9f, minZ = 1e9f;
            int struts = 0;
            foreach (var o in Pieces())
            {
                float y = (float)F(o, "Y"), z = (float)F(o, "Z");
                if ((string)F(o, "Prefab") == "woodiron_beam_45") { struts++; Assert.That(z, Is.EqualTo(0f).Within(1e-4), "struts at the pivot plane"); Assert.That(Math.Abs(y), Is.LessThan(0.02f)); }
                maxZ = Math.Max(maxZ, z); minZ = Math.Min(minZ, z);
            }
            Assert.That(struts, Is.EqualTo(2));
            Assert.That(minZ, Is.GreaterThanOrEqualTo(-1e-4f), "nothing sits inside the troll (behind the struts)");
            Assert.That(maxZ, Is.GreaterThan(3f), "the cage reaches ~4 m away from the troll's back");
        }

        [Test]
        public void CagePieces_OnTheOneMetreGrid()
        {
            // D-057: in pivot space the cage grid is x = n, z = n + 0.0 (dump x n + 0.02, z n + 0.325, pivot 0.02 / -2.675).
            foreach (var o in Pieces())
            {
                if ((string)F(o, "Prefab") == "piece_chest") continue;
                float x = (float)F(o, "X"), z = (float)F(o, "Z");
                Assert.That(Math.Abs(x - Math.Round(x)), Is.LessThan(1e-3), (string)F(o, "Prefab") + " x " + x);
                Assert.That(Math.Abs(z - Math.Round(z)), Is.LessThan(1e-3), (string)F(o, "Prefab") + " z " + z);
            }
        }

        [Test]
        public void ChestsAndStraps_MirroredAboutTheTrollsSpine()
        {
            // D-055 / D-057: every chest / strut / strap beam has a mirror partner at -X (same prefab, height, depth); pivot X 0 = the troll's spine.
            var all = Pieces();
            int checkedPieces = 0;
            foreach (var o in all)
            {
                string prefab = (string)F(o, "Prefab");
                float x = (float)F(o, "X"), y = (float)F(o, "Y"), z = (float)F(o, "Z");
                bool strap = prefab == "woodiron_beam_45" || (prefab == "woodiron_beam" && Math.Abs(z) < 1e-4);
                if (prefab != "piece_chest" && !strap) continue;
                checkedPieces++;
                bool found = false;
                foreach (var m in all)
                    if ((string)F(m, "Prefab") == prefab && Math.Abs((float)F(m, "X") + x) < 1e-3 && Math.Abs((float)F(m, "Y") - y) < 1e-3 && Math.Abs((float)F(m, "Z") - z) < 1e-3) found = true;
                Assert.That(found, Is.True, prefab + " at x " + x + " has no mirror partner");
            }
            Assert.That(checkedPieces, Is.EqualTo(8), "4 chests + 2 struts + 2 strap beams");
        }
    }
}
