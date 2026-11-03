using System.Collections.Generic;
using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Recovery.Domain;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-167: what blocks a troll, which way a detour turns, which ground is valid, when a leg is over, head-on trolls, and the progress measurement.</summary>
    [TestFixture]
    public class StuckRulesTests
    {
        // ---- obstruction classification (spec 1) ----
        [Test] public void Classify_PlayerBuilt_IsNeverAttacked_EvenWhenBreakable() { Assert.That(StuckRules.Classify(false, false, false, true, true), Is.EqualTo(Obstruction.PlayerBuilt)); }
        [Test] public void Classify_PlayerBuilt_WinsOverEverything() { Assert.That(StuckRules.Classify(true, false, false, true, true), Is.EqualTo(Obstruction.PlayerBuilt)); }
        [Test] public void Classify_TamedCreature_IsProtected() { Assert.That(StuckRules.Classify(true, false, true, false, true), Is.EqualTo(Obstruction.Protected)); }
        [Test] public void Classify_Player_IsProtected() { Assert.That(StuckRules.Classify(true, true, false, false, true), Is.EqualTo(Obstruction.Protected)); }
        [Test] public void Classify_WildCreature_IsDestructible() { Assert.That(StuckRules.Classify(true, false, false, false, true), Is.EqualTo(Obstruction.Destructible)); }
        [Test] public void Classify_BreakableTreeOrRock_IsDestructible() { Assert.That(StuckRules.Classify(false, false, false, false, true), Is.EqualTo(Obstruction.Destructible)); }
        [Test] public void Classify_NotBreakable_IsIndestructible() { Assert.That(StuckRules.Classify(false, false, false, false, false), Is.EqualTo(Obstruction.Indestructible)); }
        [Test] public void Rageable_OnlyDestructible()
        {
            Assert.That(StuckRules.Rageable(Obstruction.Destructible), Is.True);
            foreach (var o in new[] { Obstruction.None, Obstruction.Indestructible, Obstruction.PlayerBuilt, Obstruction.Protected }) Assert.That(StuckRules.Rageable(o), Is.False, o.ToString());
        }

        // ---- turn directions ----
        static float[] Dir(float ix, float iz, Side s) { float x, z; StuckRules.Direction(ix, iz, s, out x, out z); return new[] { x, z }; }
        [Test] public void Direction_IntendedNorth_RightIsEast() { var d = Dir(0f, 1f, Side.Right); Assert.That(d[0], Is.EqualTo(1f).Within(1e-5f)); Assert.That(d[1], Is.EqualTo(0f).Within(1e-5f)); }
        [Test] public void Direction_IntendedNorth_LeftIsWest() { var d = Dir(0f, 1f, Side.Left); Assert.That(d[0], Is.EqualTo(-1f).Within(1e-5f)); Assert.That(d[1], Is.EqualTo(0f).Within(1e-5f)); }
        [Test] public void Direction_IntendedEast_RightIsSouth() { var d = Dir(1f, 0f, Side.Right); Assert.That(d[0], Is.EqualTo(0f).Within(1e-5f)); Assert.That(d[1], Is.EqualTo(-1f).Within(1e-5f)); }
        [Test] public void Direction_IsAUnitVector_ForALongInput() { var d = Dir(30f, 40f, Side.Right); Assert.That(d[0] * d[0] + d[1] * d[1], Is.EqualTo(1f).Within(1e-5f)); }
        [Test] public void Direction_IsPerpendicularToTheIntendedDirection() { var d = Dir(3f, 4f, Side.Left); Assert.That(d[0] * 3f + d[1] * 4f, Is.EqualTo(0f).Within(1e-4f)); }
        [Test] public void Direction_ZeroInput_GivesZero() { var d = Dir(0f, 0f, Side.Right); Assert.That(d[0], Is.EqualTo(0f)); Assert.That(d[1], Is.EqualTo(0f)); }
        [Test] public void Opposite_Flips() { Assert.That(StuckRules.Opposite(Side.Right), Is.EqualTo(Side.Left)); Assert.That(StuckRules.Opposite(Side.Left), Is.EqualTo(Side.Right)); }

        // ---- valid ground (spec 2: never into a cliff, water or another obstacle) ----
        [Test] public void StepOk_FlatDryFree() { Assert.That(StuckRules.StepOk(0f, 0f, false), Is.True); }
        [Test] public void StepOk_BlockedByAnObstacle_No() { Assert.That(StuckRules.StepOk(0f, 0f, true), Is.False); }
        [Test] public void StepOk_CliffUp_No() { Assert.That(StuckRules.StepOk(StuckRules.StepMaxRise + 0.1f, 0f, false), Is.False); }
        [Test] public void StepOk_CliffDown_No() { Assert.That(StuckRules.StepOk(-(StuckRules.StepMaxRise + 0.1f), 0f, false), Is.False); }
        [Test] public void StepOk_SteepButWalkable_Yes() { Assert.That(StuckRules.StepOk(StuckRules.StepMaxRise, 0f, false), Is.True); }
        [Test] public void StepOk_DeepWater_No() { Assert.That(StuckRules.StepOk(0f, StuckRules.MaxWaterDepth + 0.1f, false), Is.False); }
        [Test] public void StepOk_ShallowWater_Yes() { Assert.That(StuckRules.StepOk(0f, StuckRules.MaxWaterDepth, false), Is.True); }

        [Test] public void PlanMetres_AllValid_IsTheLegLength() { Assert.That(StuckRules.PlanMetres(Steps(10, 10), 10f), Is.EqualTo(10f)); }
        [Test] public void PlanMetres_StopsAtTheFirstInvalidStep() { Assert.That(StuckRules.PlanMetres(new List<bool> { true, true, true, false, true, true }, 10f), Is.EqualTo(3f)); }
        [Test] public void PlanMetres_FirstStepInvalid_IsZero() { Assert.That(StuckRules.PlanMetres(new List<bool> { false, true, true }, 10f), Is.EqualTo(0f)); }
        [Test] public void PlanMetres_NeverMoreThanTheMaximum() { Assert.That(StuckRules.PlanMetres(Steps(20, 20), 10f), Is.EqualTo(10f)); }
        [Test] public void PlanMetres_ShortList_IsItsLength() { Assert.That(StuckRules.PlanMetres(Steps(4, 4), 10f), Is.EqualTo(4f)); }
        [Test] public void PlanMetres_NullOrEmpty_IsZero() { Assert.That(StuckRules.PlanMetres(null, 10f), Is.EqualTo(0f)); Assert.That(StuckRules.PlanMetres(new List<bool>(), 10f), Is.EqualTo(0f)); }
        static List<bool> Steps(int count, int valid) { var l = new List<bool>(); for (int i = 0; i < count; i++) l.Add(i < valid); return l; }

        // ---- which side a leg goes ----
        [Test] public void ChooseSide_PreferredHasRoom_KeepsIt() { float m; Assert.That(StuckRules.ChooseSide(Side.Right, 8f, 10f, out m), Is.EqualTo(Side.Right)); Assert.That(m, Is.EqualTo(8f)); }
        [Test] public void ChooseSide_PreferredBlocked_TakesTheOtherSide() { float m; Assert.That(StuckRules.ChooseSide(Side.Right, 0f, 6f, out m), Is.EqualTo(Side.Left)); Assert.That(m, Is.EqualTo(6f)); }
        [Test] public void ChooseSide_PreferredLeftBlocked_TakesRight() { float m; Assert.That(StuckRules.ChooseSide(Side.Left, 0.5f, 3f, out m), Is.EqualTo(Side.Right)); Assert.That(m, Is.EqualTo(3f)); }
        [Test] public void ChooseSide_NoRoomAnywhere_NothingToWalk() { float m; Assert.That(StuckRules.ChooseSide(Side.Right, 0f, 0.5f, out m), Is.EqualTo(Side.Right)); Assert.That(m, Is.EqualTo(0f)); }
        [Test] public void ChooseSide_ExactlyTheMinimum_CountsWhenTheOtherSideHasNoMore() { float m; StuckRules.ChooseSide(Side.Right, StuckRules.MinLegMetres, 0.5f, out m); Assert.That(m, Is.EqualTo(StuckRules.MinLegMetres)); }
        [Test] public void ChooseSide_PreferredHasLittleRoomOtherHasMuch_TakesTheOtherSide() { float m; Assert.That(StuckRules.ChooseSide(Side.Right, 1f, 10f, out m), Is.EqualTo(Side.Left)); Assert.That(m, Is.EqualTo(10f)); }
        [Test] public void ChooseSide_PreferredHasLittleRoomAndSoDoesTheOther_KeepsThePreferred() { float m; Assert.That(StuckRules.ChooseSide(Side.Left, 2f, 1.5f, out m), Is.EqualTo(Side.Left)); Assert.That(m, Is.EqualTo(2f)); }
        [Test] public void ChooseSide_PreferredExactlyGood_IsKept() { float m; Assert.That(StuckRules.ChooseSide(Side.Right, StuckRules.GoodLegMetres, 10f, out m), Is.EqualTo(Side.Right)); }

        // ---- when a leg is over ----
        [Test] public void LegDone_NothingToWalk() { Assert.That(StuckRules.LegDone(0f, 0f, 0f, 0f), Is.True); }
        [Test] public void LegDone_StillWalking_No() { Assert.That(StuckRules.LegDone(3f, 10f, 2f, 0f), Is.False); }
        [Test] public void LegDone_WalkedFarEnough() { Assert.That(StuckRules.LegDone(9.6f, 10f, 4f, 0f), Is.True); }
        [Test] public void LegDone_TookTooLong() { Assert.That(StuckRules.LegDone(3f, 10f, StuckRules.LegMaxSeconds, 0f), Is.True); }
        [Test] public void LegDone_Stalled_WalkedIntoSomething() { Assert.That(StuckRules.LegDone(3f, 10f, 3f, StuckRules.LegStallSeconds), Is.True); }
        [Test] public void LegDone_JustBeforeTheStallLimit_No() { Assert.That(StuckRules.LegDone(3f, 10f, 3f, StuckRules.LegStallSeconds - 0.1f), Is.False); }

        // ---- two trolls head on (spec 4) ----
        [Test] public void HeadOn_OtherInFrontFacingMe() { Assert.That(StuckRules.HeadOn(0f, 0f, 0f, 1f, 0f, 2.5f, 0f, -1f), Is.True); }
        [Test] public void HeadOn_OtherInFrontSlightlyAside_StillHeadOn() { Assert.That(StuckRules.HeadOn(0f, 0f, 0f, 1f, 1f, 2.5f, 0f, -1f), Is.True); }
        [Test] public void HeadOn_OtherWalksTheSameWay_No() { Assert.That(StuckRules.HeadOn(0f, 0f, 0f, 1f, 0f, 2.5f, 0f, 1f), Is.False); }
        [Test] public void HeadOn_OtherCrossing_No() { Assert.That(StuckRules.HeadOn(0f, 0f, 0f, 1f, 0f, 2.5f, 1f, 0f), Is.False); }
        [Test] public void HeadOn_OtherBeside_No() { Assert.That(StuckRules.HeadOn(0f, 0f, 0f, 1f, 4f, 2.5f, 0f, -1f), Is.False); }
        [Test] public void HeadOn_OtherBehind_No() { Assert.That(StuckRules.HeadOn(0f, 0f, 0f, 1f, 0f, -2.5f, 0f, -1f), Is.False); }
        [Test] public void HeadOn_OtherTooFar_No() { Assert.That(StuckRules.HeadOn(0f, 0f, 0f, 1f, 0f, 6f, 0f, -1f), Is.False); }
        [Test] public void HeadOnClear_FarEnoughApart() { Assert.That(StuckRules.HeadOnClear(StuckRules.HeadOnClearMetres), Is.True); Assert.That(StuckRules.HeadOnClear(StuckRules.HeadOnClearMetres - 0.1f), Is.False); }

        // ---- progress (spec 1a / 5) ----
        static ProgressMeter Walk(float start) { var m = new ProgressMeter(); m.Reset(start); return m; }

        [Test] public void Meter_WalkingTowardsTheGoal_NeverStuck()
        {
            var m = Walk(50f);
            float d = 50f;
            for (int i = 0; i < 200; i++) { d -= 0.1f; m.Update(d, 0.05f, true); }
            Assert.That(m.StuckSeconds, Is.LessThan(StuckRules.StuckTrigger));
            Assert.That(m.SustainedSeconds, Is.GreaterThan(5f));
        }
        [Test] public void Meter_StandingStill_BecomesStuckAfterTheTrigger()
        {
            var m = Walk(20f);
            for (int i = 0; i < 61; i++) m.Update(20f, 0.05f, true);
            Assert.That(m.StuckSeconds, Is.GreaterThanOrEqualTo(StuckRules.StuckTrigger));
        }
        [Test] public void Meter_WalkingSidewaysAgainstARock_IsStuck_EvenThoughItMoves()
        {
            var m = Walk(20f);
            for (int i = 0; i < 80; i++) m.Update(20f + (i % 2 == 0 ? 0.05f : -0.05f), 0.05f, true);      // legs move, the distance does not shrink by MinGain
            Assert.That(m.StuckSeconds, Is.GreaterThanOrEqualTo(StuckRules.StuckTrigger));
        }
        [Test] public void Meter_WalkingBackwards_IsNoProgress()
        {
            var m = Walk(20f);
            float d = 20f;
            for (int i = 0; i < 80; i++) { d += 0.1f; m.Update(d, 0.05f, true); }
            Assert.That(m.StuckSeconds, Is.GreaterThanOrEqualTo(StuckRules.StuckTrigger));
        }
        [Test] public void Meter_ASmallGainBelowTheMinimum_IsNotProgress()
        {
            var m = Walk(20f);
            m.Update(20f - StuckRules.MinGain * 0.5f, 1f, true);
            Assert.That(m.StuckSeconds, Is.EqualTo(1f));
        }
        [Test] public void Meter_ResumedProgress_ResetsTheTimer()
        {
            var m = Walk(20f);
            for (int i = 0; i < 40; i++) m.Update(20f, 0.05f, true);
            Assert.That(m.StuckSeconds, Is.GreaterThan(1.9f));
            m.Update(19f, 0.05f, true);
            Assert.That(m.StuckSeconds, Is.EqualTo(0f));
        }
        [Test] public void Meter_IntentionalPause_DoesNotCountAndDoesNotReset()
        {
            var m = Walk(20f);
            for (int i = 0; i < 40; i++) m.Update(20f, 0.05f, true);
            float before = m.StuckSeconds;
            for (int i = 0; i < 400; i++) m.Update(20f, 0.05f, false);      // attack animation, knockback, a deliberate wait: 20 s
            Assert.That(m.StuckSeconds, Is.EqualTo(before).Within(1e-4f));
        }
        [Test] public void Meter_ZeroOrNegativeDt_IsIgnored()
        {
            var m = Walk(20f);
            m.Update(20f, 0f, true); m.Update(20f, -1f, true);
            Assert.That(m.StuckSeconds, Is.EqualTo(0f));
        }
        [Test] public void Meter_Sustained_BuildsUpOnlyWithRegularGains_AndDropsAfterAGap()
        {
            var m = Walk(100f);
            float d = 100f;
            for (int i = 0; i < 100; i++) { d -= 0.2f; m.Update(d, 0.1f, true); }       // 10 s of steady progress
            Assert.That(m.SustainedSeconds, Is.GreaterThanOrEqualTo(StuckRules.SustainedReset - 0.2f));
            for (int i = 0; i < 20; i++) m.Update(d, 0.1f, true);                           // 2 s with no gain: longer than GainWindow
            Assert.That(m.SustainedSeconds, Is.EqualTo(0f));
        }
        [Test] public void Meter_Reset_StartsFromTheNewDistance()
        {
            var m = Walk(20f);
            for (int i = 0; i < 40; i++) m.Update(20f, 0.05f, true);
            m.Reset(35f);
            Assert.That(m.StuckSeconds, Is.EqualTo(0f));
            Assert.That(m.Best, Is.EqualTo(35f));
            m.Update(34f, 0.05f, true);
            Assert.That(m.Best, Is.EqualTo(34f));
        }
    }
}
