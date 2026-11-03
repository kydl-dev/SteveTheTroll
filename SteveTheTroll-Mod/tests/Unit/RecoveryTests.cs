using NUnit.Framework;
using SteveTheTroll.Trolls.Recovery.Domain;
using SteveTheTroll.Trolls.Taming.Domain;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-167: the escalation ladder of one recovery cycle and the failure counter / blacklist per troll + resource (the user's spec, section by section).</summary>
    [TestFixture]
    public class RecoveryMachineTests
    {
        const float Stuck = StuckRules.StuckTrigger;

        static RecoveryCmd Step(RecoveryMachine m, float now, float stuck = 0f, float sustained = 0f, bool rageable = false, bool headOn = false, bool legDone = false)
        {
            return m.Step(now, stuck, sustained, rageable, headOn, legDone);
        }

        [Test] public void NotStuckYet_Nothing() { var m = new RecoveryMachine(); Assert.That(Step(m, 0f, Stuck - 0.1f), Is.EqualTo(RecoveryCmd.None)); Assert.That(m.Stage, Is.EqualTo(RecoveryStage.Travel)); }

        // ---- spec 1: destructible obstruction -> rage 5 s ----
        [Test] public void StuckBehindADestructible_RagesFor5Seconds()
        {
            var m = new RecoveryMachine();
            Assert.That(Step(m, 10f, Stuck, rageable: true), Is.EqualTo(RecoveryCmd.StartRage));
            Assert.That(m.Stage, Is.EqualTo(RecoveryStage.Rage));
            Assert.That(Step(m, 14.9f, 0f), Is.EqualTo(RecoveryCmd.None));
            Assert.That(m.Stage, Is.EqualTo(RecoveryStage.Rage));
            Assert.That(Step(m, 15f, 0f), Is.EqualTo(RecoveryCmd.EndRage));
            Assert.That(m.Stage, Is.EqualTo(RecoveryStage.Travel));
        }
        [Test] public void StuckBehindAnIndestructibleOrPlayerBuiltThing_SkipsRage_GoesStraightToTheDetour()
        {
            var m = new RecoveryMachine();
            Assert.That(Step(m, 0f, Stuck, rageable: false), Is.EqualTo(RecoveryCmd.StartLeg));
            Assert.That(m.Stage, Is.EqualTo(RecoveryStage.Leg));
            Assert.That(m.Raged, Is.False);
            Assert.That(m.DetourNumber, Is.EqualTo(1));
        }
        [Test] public void RageHappensOncePerCycle()
        {
            var m = new RecoveryMachine();
            Step(m, 0f, Stuck, rageable: true);
            Step(m, 5f);                                                              // rage over
            Assert.That(Step(m, 6f, Stuck, rageable: true), Is.EqualTo(RecoveryCmd.StartLeg), "stuck again after the rage: first detour, no second rage");
        }
        [Test] public void AfterTheRage_ObstructionGoneAndProgressResumed_NothingMoreHappens()
        {
            var m = new RecoveryMachine();
            Step(m, 0f, Stuck, rageable: true);
            Step(m, 5f);
            Assert.That(Step(m, 6f, 0.5f, 3f), Is.EqualTo(RecoveryCmd.None));
            Assert.That(m.DetoursDone, Is.EqualTo(0));
        }

        // ---- spec 1 / 2: first detour, second (L-shaped) detour ----
        [Test] public void FirstDetour_TurnsRight_OneLeg_ThenRecalculates()
        {
            var m = new RecoveryMachine();
            Step(m, 0f, Stuck);
            Assert.That(m.LegSide, Is.EqualTo(Side.Right));
            Assert.That(Step(m, 1f, legDone: false), Is.EqualTo(RecoveryCmd.None));
            Assert.That(Step(m, 2f, legDone: true), Is.EqualTo(RecoveryCmd.EndLeg));
            Assert.That(m.Stage, Is.EqualTo(RecoveryStage.Travel));
            Assert.That(m.DetoursDone, Is.EqualTo(1));
        }
        [Test] public void StuckAgainAfterTheFirstDetour_RunsTheSecondDetour_TwoLegs()
        {
            var m = new RecoveryMachine();
            Step(m, 0f, Stuck); Step(m, 2f, legDone: true);
            Assert.That(Step(m, 8f, Stuck), Is.EqualTo(RecoveryCmd.StartLeg));
            Assert.That(m.DetourNumber, Is.EqualTo(2));
            Assert.That(m.Leg, Is.EqualTo(0));
            Assert.That(m.LegSide, Is.EqualTo(Side.Right));
            Assert.That(Step(m, 9f, legDone: true), Is.EqualTo(RecoveryCmd.StartLeg), "first leg over: the second leg starts");
            Assert.That(m.Leg, Is.EqualTo(1));
            Assert.That(m.LegSide, Is.EqualTo(Side.Left), "the turn back (left of the first leg) = the intended direction again");
            Assert.That(Step(m, 10f, legDone: false), Is.EqualTo(RecoveryCmd.None));
            Assert.That(Step(m, 11f, legDone: true), Is.EqualTo(RecoveryCmd.EndLeg));
            Assert.That(m.DetoursDone, Is.EqualTo(2));
        }
        [Test] public void StuckAgainAfterTheSecondDetour_TheCycleFails()
        {
            var m = new RecoveryMachine();
            Step(m, 0f, Stuck); Step(m, 1f, legDone: true);
            Step(m, 5f, Stuck); Step(m, 6f, legDone: true); Step(m, 7f, legDone: true);
            Assert.That(Step(m, 12f, Stuck), Is.EqualTo(RecoveryCmd.CycleFailed));
        }
        [Test] public void FullLadder_WithRage_Rage_Detour1_Detour2_Fail()
        {
            var m = new RecoveryMachine();
            var seq = new System.Collections.Generic.List<RecoveryCmd>();
            seq.Add(Step(m, 0f, Stuck, rageable: true));
            seq.Add(Step(m, 5f));
            seq.Add(Step(m, 9f, Stuck, rageable: true));
            seq.Add(Step(m, 10f, legDone: true));
            seq.Add(Step(m, 14f, Stuck, rageable: true));
            seq.Add(Step(m, 15f, legDone: true));
            seq.Add(Step(m, 16f, legDone: true));
            seq.Add(Step(m, 20f, Stuck, rageable: true));
            Assert.That(seq, Is.EqualTo(new[] { RecoveryCmd.StartRage, RecoveryCmd.EndRage, RecoveryCmd.StartLeg, RecoveryCmd.EndLeg, RecoveryCmd.StartLeg, RecoveryCmd.StartLeg, RecoveryCmd.EndLeg, RecoveryCmd.CycleFailed }));
        }
        [Test] public void ALegThatIsNotOver_KeepsRunning_NoMatterHowStuck()
        {
            var m = new RecoveryMachine();
            Step(m, 0f, Stuck);
            Assert.That(Step(m, 1f, 99f, legDone: false), Is.EqualTo(RecoveryCmd.None));
            Assert.That(m.Stage, Is.EqualTo(RecoveryStage.Leg));
        }

        // ---- cycle direction alternates (spec "final recommendation") ----
        [Test] public void FirstCycle_TurnsRightFirst() { var m = new RecoveryMachine(); m.BeginCycle(0); Assert.That(m.RightFirst, Is.True); }
        [Test] public void SecondCycle_TurnsLeftFirst_BothLegs()
        {
            var m = new RecoveryMachine();
            m.BeginCycle(1);
            Step(m, 0f, Stuck);
            Assert.That(m.LegSide, Is.EqualTo(Side.Left));
            Step(m, 1f, legDone: true);
            Step(m, 5f, Stuck);
            Assert.That(m.LegSide, Is.EqualTo(Side.Left), "detour 2, leg 1: left 10 m");
            Step(m, 6f, legDone: true);
            Assert.That(m.LegSide, Is.EqualTo(Side.Right), "detour 2, leg 2: right 10 m");
        }
        [Test] public void ThirdCycle_TurnsRightFirstAgain_ForAHubThatKeepsFailing() { var m = new RecoveryMachine(); m.BeginCycle(2); Assert.That(m.RightFirst, Is.True); m.BeginCycle(3); Assert.That(m.RightFirst, Is.False); }
        [Test] public void BeginCycle_ClearsEverything()
        {
            var m = new RecoveryMachine();
            Step(m, 0f, Stuck, rageable: true); Step(m, 5f); Step(m, 6f, Stuck); Step(m, 7f, legDone: true);
            Assert.That(m.Dirty, Is.True);
            m.BeginCycle(0);
            Assert.That(m.Raged, Is.False); Assert.That(m.DetoursDone, Is.EqualTo(0)); Assert.That(m.Stage, Is.EqualTo(RecoveryStage.Travel)); Assert.That(m.Dirty, Is.False);
        }

        // ---- spec 4: head on ----
        [Test] public void HeadOn_FirstDetourIsTheTurnToTheRight_EvenInALeftFirstCycle()
        {
            var m = new RecoveryMachine();
            m.BeginCycle(1);
            Step(m, 0f, Stuck, headOn: true);
            Assert.That(m.HeadOn, Is.True);
            Assert.That(m.LegSide, Is.EqualTo(Side.Right));
        }
        [Test] public void HeadOn_OneLegOnly()
        {
            var m = new RecoveryMachine();
            Step(m, 0f, Stuck, headOn: true);
            Assert.That(Step(m, 1f, legDone: true), Is.EqualTo(RecoveryCmd.EndLeg));
            Assert.That(m.HeadOn, Is.False);
        }
        [Test] public void HeadOn_OnlyAnswersTheFirstStuck_TheSecondIsTheNormalLShape()
        {
            var m = new RecoveryMachine();
            Step(m, 0f, Stuck, headOn: true); Step(m, 1f, legDone: true);
            Step(m, 5f, Stuck, headOn: true);
            Assert.That(m.HeadOn, Is.False);
            Assert.That(m.DetourNumber, Is.EqualTo(2));
        }
        [Test] public void HeadOn_NeverRages_ATamedTrollIsNotRageable_ButTheLadderStillStartsWithTheTurn()
        {
            var m = new RecoveryMachine();
            Assert.That(Step(m, 0f, Stuck, rageable: false, headOn: true), Is.EqualTo(RecoveryCmd.StartLeg));
            Assert.That(m.Raged, Is.False);
        }

        // ---- reset by sustained progress ----
        [Test] public void SustainedProgress_ResetsADirtyCycle()
        {
            var m = new RecoveryMachine();
            Step(m, 0f, Stuck); Step(m, 1f, legDone: true);
            Assert.That(Step(m, 5f, 0f, StuckRules.SustainedReset), Is.EqualTo(RecoveryCmd.CycleReset));
        }
        [Test] public void SustainedProgress_OnACleanCycle_NothingToReport() { var m = new RecoveryMachine(); Assert.That(Step(m, 5f, 0f, 30f), Is.EqualTo(RecoveryCmd.None)); }
        [Test] public void ProgressForLessThanTheSustainedTime_KeepsTheLadder()
        {
            var m = new RecoveryMachine();
            Step(m, 0f, Stuck); Step(m, 1f, legDone: true);
            Assert.That(Step(m, 4f, 0f, StuckRules.SustainedReset - 1f), Is.EqualTo(RecoveryCmd.None));
            Assert.That(m.DetoursDone, Is.EqualTo(1));
            Assert.That(Step(m, 8f, Stuck), Is.EqualTo(RecoveryCmd.StartLeg));
            Assert.That(m.DetourNumber, Is.EqualTo(2), "briefly moved, stuck again: the cycle goes on with the next rung");
        }

        [Test] public void Abort_LeavesTheManoeuvre_KeepsWhatWasDone()
        {
            var m = new RecoveryMachine();
            Step(m, 0f, Stuck, rageable: true);
            m.Abort();
            Assert.That(m.Stage, Is.EqualTo(RecoveryStage.Travel));
            Assert.That(m.Raged, Is.True);
        }
    }

    [TestFixture]
    public class RecoveryBookTests
    {
        const string Steve1 = "Steve 1", Steve2 = "Steve 2";
        static void Fail(RecoveryBook b, string troll, long res, float now, bool can = true) { b.CycleFailed(troll, 1L, res, now, can); }

        [Test] public void NothingKnown_NotBlacklisted_NoFailures() { var b = new RecoveryBook(); Assert.That(b.IsBlacklisted(Steve1, 1L, 7L, 0f), Is.False); Assert.That(b.Failed(Steve1, 1L, 7L), Is.EqualTo(0)); }
        [Test] public void OneFailedCycle_IsCounted_NotBlacklisted()
        {
            var b = new RecoveryBook();
            Assert.That(b.CycleFailed(Steve1, 1L, 7L, 0f, true), Is.False);
            Assert.That(b.Failed(Steve1, 1L, 7L), Is.EqualTo(1));
            Assert.That(b.IsBlacklisted(Steve1, 1L, 7L, 1f), Is.False);
        }
        [Test] public void TwoFailedCycles_BlacklistTheResourceFor120Seconds()
        {
            var b = new RecoveryBook();
            b.CycleFailed(Steve1, 1L, 7L, 100f, true);
            Assert.That(b.CycleFailed(Steve1, 1L, 7L, 130f, true), Is.True);
            Assert.That(b.IsBlacklisted(Steve1, 1L, 7L, 130f), Is.True);
            Assert.That(b.IsBlacklisted(Steve1, 1L, 7L, 249.9f), Is.True);
            Assert.That(b.IsBlacklisted(Steve1, 1L, 7L, 250f), Is.False, "after 120 s the resource is available again");
        }
        [Test] public void TheBlacklistIsPerTroll_AnotherTrollMayStillGoThere()
        {
            var b = new RecoveryBook();
            Fail(b, Steve1, 7L, 0f); Fail(b, Steve1, 7L, 1f);
            Assert.That(b.IsBlacklisted(Steve1, 1L, 7L, 2f), Is.True);
            Assert.That(b.IsBlacklisted(Steve2, 1L, 7L, 2f), Is.False);
        }
        [Test] public void TheBlacklistIsPerResource_AnotherTreeOfTheSameKindStaysAvailable()
        {
            var b = new RecoveryBook();
            Fail(b, Steve1, 7L, 0f); Fail(b, Steve1, 7L, 1f);
            Assert.That(b.IsBlacklisted(Steve1, 1L, 8L, 2f), Is.False);
            Assert.That(b.Failed(Steve1, 1L, 8L), Is.EqualTo(0));
        }
        [Test] public void FailuresOfTwoTrollsOnTheSameResource_AreCountedApart()
        {
            var b = new RecoveryBook();
            Fail(b, Steve1, 7L, 0f); Fail(b, Steve2, 7L, 1f);
            Assert.That(b.Failed(Steve1, 1L, 7L), Is.EqualTo(1));
            Assert.That(b.Failed(Steve2, 1L, 7L), Is.EqualTo(1));
            Assert.That(b.IsBlacklisted(Steve1, 1L, 7L, 2f), Is.False);
        }
        [Test] public void ResourceIdentityIncludesBothParts_SameIdOtherOwnerIsAnotherResource()
        {
            var b = new RecoveryBook();
            b.CycleFailed(Steve1, 1L, 7L, 0f, true); b.CycleFailed(Steve1, 1L, 7L, 1f, true);
            Assert.That(b.IsBlacklisted(Steve1, 2L, 7L, 2f), Is.False);
        }
        [Test] public void AfterTheBlacklist_TheCounterStartsAgain_TwoMoreFailuresNeeded()
        {
            var b = new RecoveryBook();
            Fail(b, Steve1, 7L, 0f); Fail(b, Steve1, 7L, 1f);
            Assert.That(b.Failed(Steve1, 1L, 7L), Is.EqualTo(0));
            Assert.That(b.CycleFailed(Steve1, 1L, 7L, 300f, true), Is.False);
            Assert.That(b.CycleFailed(Steve1, 1L, 7L, 310f, true), Is.True);
        }
        [Test] public void AHub_IsNeverBlacklisted_ButTheFailuresKeepCounting_SoTheDirectionAlternates()
        {
            var b = new RecoveryBook();
            for (int i = 1; i <= 6; i++)
            {
                Assert.That(b.CycleFailed(Steve1, -2L, 55L, i, false), Is.False);
                Assert.That(b.Failed(Steve1, -2L, 55L), Is.EqualTo(i));
            }
            Assert.That(b.IsBlacklisted(Steve1, -2L, 55L, 7f), Is.False);
        }
        [Test] public void Recovered_ResetsTheCounter()
        {
            var b = new RecoveryBook();
            Fail(b, Steve1, 7L, 0f);
            b.Recovered(Steve1, 1L, 7L);
            Assert.That(b.Failed(Steve1, 1L, 7L), Is.EqualTo(0));
            Assert.That(b.CycleFailed(Steve1, 1L, 7L, 1f, true), Is.False, "one failure again, not two");
        }
        [Test] public void Recovered_DoesNotLiftARunningBlacklist()
        {
            var b = new RecoveryBook();
            Fail(b, Steve1, 7L, 0f); Fail(b, Steve1, 7L, 1f);
            b.Recovered(Steve1, 1L, 7L);
            Assert.That(b.IsBlacklisted(Steve1, 1L, 7L, 5f), Is.True);
        }
        [Test] public void Recovered_ForAnUnknownPair_IsHarmless() { var b = new RecoveryBook(); b.Recovered(Steve1, 1L, 99L); Assert.That(b.Count, Is.EqualTo(0)); }
        [Test] public void Prune_DropsFinishedEntries_KeepsCountingAndBlacklisted()
        {
            var b = new RecoveryBook();
            Fail(b, Steve1, 1L, 0f); Fail(b, Steve1, 1L, 1f);            // blacklisted until 121
            Fail(b, Steve1, 2L, 0f);                                      // counting (1)
            Fail(b, Steve1, 3L, 0f); b.Recovered(Steve1, 1L, 3L);         // finished
            Assert.That(b.Prune(50f), Is.EqualTo(1));
            Assert.That(b.Prune(200f), Is.EqualTo(1), "the blacklist ran out; the counting entry stays");
            Assert.That(b.Count, Is.EqualTo(1));
        }
        [Test] public void UnknownTrollName_IsAnEmptyKey_NotACrash() { var b = new RecoveryBook(); b.CycleFailed(null, 1L, 7L, 0f, true); Assert.That(b.Failed(null, 1L, 7L), Is.EqualTo(1)); Assert.That(b.Failed("", 1L, 7L), Is.EqualTo(1)); }

        // ---- the whole spec in one run: machine + meter + book, a troll that never gets anywhere ----
        [Test] public void Scenario_ATrollThatNeverMakesProgress_FailsTwoCycles_ThenTheResourceIsBlacklisted()
        {
            var book = new RecoveryBook();
            var machine = new RecoveryMachine();
            var meter = new ProgressMeter();
            meter.Reset(30f);
            float now = 0f;
            int cycles = 0; bool blacklisted = false, raged = false;
            for (int tick = 0; tick < 10000 && !blacklisted; tick++)
            {
                now += 0.1f;
                bool inLeg = machine.Stage == RecoveryStage.Leg;
                if (machine.Stage == RecoveryStage.Travel) meter.Update(30f, 0.1f, true);
                var cmd = machine.Step(now, meter.StuckSeconds, meter.SustainedSeconds, !raged && cycles == 0, false, inLeg && (tick % 20 == 0));
                if (cmd == RecoveryCmd.StartRage) raged = true;
                if (cmd == RecoveryCmd.EndRage || cmd == RecoveryCmd.EndLeg) meter.Reset(30f);
                if (cmd == RecoveryCmd.CycleFailed)
                {
                    cycles++;
                    blacklisted = book.CycleFailed("Steve 1", 1L, 42L, now, true);
                    machine.BeginCycle(book.Failed("Steve 1", 1L, 42L));
                    meter.Reset(30f);
                }
            }
            Assert.That(cycles, Is.EqualTo(2));
            Assert.That(blacklisted, Is.True);
            Assert.That(book.IsBlacklisted("Steve 1", 1L, 42L, now + 1f), Is.True);
            Assert.That(book.IsBlacklisted("Steve 1", 1L, 43L, now + 1f), Is.False);
            Assert.That(book.IsBlacklisted("Steve 1", 1L, 42L, now + StuckRules.BlacklistSeconds + 1f), Is.False);
        }
    }
}
