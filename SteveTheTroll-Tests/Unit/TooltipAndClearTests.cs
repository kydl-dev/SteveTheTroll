using UnityEngine;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-091 one stamina line in the set tooltip; D-090 depot floor lift; D-093 stt_clear circle.</summary>
    [TestFixture]
    public class TooltipAndClearTests
    {
        const string Tip = "SteveTheTroll.Item.SetTooltipPatch", Depot = "SteveTheTroll.Backpack.DepotData", Clear = "SteveTheTroll.Testing.ClearCommand";

        // the 1.0.20 SE_Stats lines of our set (speed, carry and the 8 stamina modifiers)
        const string Game = "$stevethetroll_se_set_tooltip\n$se_runstamina: <color=orange>-50%</color>\n$se_max_carryweight: <color=orange>+75</color>\n"
            + "$se_movespeed: <color=orange>+50%</color>\n$se_jumpstamina: <color=orange>-50%</color>\n$se_attackstamina: <color=orange>-50%</color>\n"
            + "$se_blockstamina: <color=orange>-50%</color>\n$se_dodgestamina: <color=orange>-50%</color>\n$se_swimstamina: <color=orange>-50%</color>\n"
            + "$base_item_modifier: <color=orange>-50%</color>\n$se_sneakstamina: <color=orange>-50%</color>\n";

        static string Compact(string s) { return (string)ModAssembly.Call(Tip, "Compact", s, "$stevethetroll_se_stamina", -0.5f); }

        [Test]
        public void SetTooltip_OneStaminaLine_InPlaceOfTheFirst()
        {
            Assert.That(Compact(Game), Is.EqualTo("$stevethetroll_se_set_tooltip\n$stevethetroll_se_stamina: <color=orange>-50%</color>\n"
                + "$se_max_carryweight: <color=orange>+75</color>\n$se_movespeed: <color=orange>+50%</color>\n"));
        }

        [Test] public void SetTooltip_NoStamina_Unchanged() { Assert.That(Compact("a\nb: +1"), Is.EqualTo("a\nb: +1")); }
        [Test] public void SetTooltip_Empty_NoThrow() { Assert.That(Compact(null), Is.EqualTo("")); Assert.That(Compact(""), Is.EqualTo("")); }
        [Test] public void SetTooltip_PositiveModifier_HasPlus() { Assert.That((string)ModAssembly.Call(Tip, "Compact", "Run stamina usage: x", "L", 0.25f), Is.EqualTo("L: <color=orange>+25%</color>")); }

        [TestCase("$se_runstamina: -50%", true)]
        [TestCase("Block stamina cost: -50%", true)]
        [TestCase("$base_item_modifier: -50%", true)]
        [TestCase("$se_movespeed: +50%", false)]
        [TestCase(null, false)]
        public void StaminaLines(string line, bool expected) { Assert.That((bool)ModAssembly.Call(Tip, "IsStaminaLine", line), Is.EqualTo(expected)); }

        // floor top face 1.123 m (TEST-040): a lid at the same height needs 2 cm; a lid inside the slab (below the top face) needs nothing
        [TestCase(1.123f, 1.123f, 0.02f, 0.02f)]
        [TestCase(1.13f, 1.123f, 0.02f, 0.027f)]
        [TestCase(1.00f, 1.123f, 0.02f, 0f)]
        [TestCase(1.103f, 1.123f, 0.02f, 0f)]
        public void FloorLift(float chestTop, float floorTop, float clearance, float expected)
        {
            Assert.That((float)ModAssembly.Call(Depot, "FloorLift", chestTop, floorTop, clearance), Is.EqualTo(expected).Within(1e-4));
        }

        // D-095: level ops cover the whole depot footprint (+ margin): spacing never above the op's radius, corners included
        [TestCase(3.1f, 2.1f, 2f)]
        [TestCase(3.1f, 2.1f, 1f)]
        [TestCase(0.5f, 0.5f, 4f)]
        public void LevelGrid_CoversTheFootprint(float halfX, float halfZ, float radius)
        {
            var pts = (System.Collections.Generic.List<Vector3>)ModAssembly.Call("SteveTheTroll.Backpack.DepotGround", "Grid", halfX, halfZ, radius);
            // every point of the rectangle is within `radius` (square ops: per axis) of some op centre
            for (float x = -halfX; x <= halfX + 1e-3f; x += 0.1f)
                for (float z = -halfZ; z <= halfZ + 1e-3f; z += 0.1f)
                {
                    bool covered = false;
                    foreach (var p in pts) if (System.Math.Abs(p.x - x) <= radius + 1e-3f && System.Math.Abs(p.z - z) <= radius + 1e-3f) { covered = true; break; }
                    Assert.That(covered, Is.True, "(" + x + ", " + z + ") not levelled");
                }
            Assert.That(pts, Has.Some.Matches<Vector3>(p => System.Math.Abs(p.x + halfX) < 1e-3 && System.Math.Abs(p.z + halfZ) < 1e-3), "corner");
            Assert.That(pts.Count, Is.LessThanOrEqualTo(60), "not hundreds of terrain ops");
        }

        [Test]
        public void ClearCircle_FlatDistance_Inclusive()
        {
            var c = new Vector3(100f, 30f, -50f);
            Assert.That((bool)ModAssembly.Call(Clear, "Within", c, new Vector3(400f, 500f, 350f), 500f), Is.True, "300 / 400 = exactly 500 m flat, height ignored");
            Assert.That((bool)ModAssembly.Call(Clear, "Within", c, new Vector3(400.1f, 30f, 350f), 500f), Is.False);
            Assert.That((bool)ModAssembly.Call(Clear, "Within", c, c, 1f), Is.True);
        }
    }
}
