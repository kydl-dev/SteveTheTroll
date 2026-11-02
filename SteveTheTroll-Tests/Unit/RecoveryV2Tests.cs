using System.Collections.Generic;
using NUnit.Framework;
using SteveTheTroll.Core.Results;
using SteveTheTroll.Trolls.Contracts;
using SteveTheTroll.Trolls.Recovery.Application;
using SteveTheTroll.Trolls.Recovery.Domain;
using SteveTheTroll.Trolls.Recovery.Execution;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-169 Recovery V2: the pipeline decides exactly what RecoveryMachine decided before (behaviour unchanged), checks alive / owner first, and the executor maps
    /// each command to its action and performs nothing for a client that does not own the troll.</summary>
    [TestFixture]
    public class RecoveryV2Tests
    {
        const float Stuck = StuckRules.StuckTrigger;

        static RecoveryInput In(float now, float stuck = 0f, float sustained = 0f, bool rageable = false, bool headOn = false, bool legDone = false, bool alive = true, bool owner = true)
        {
            return new RecoveryInput(now, stuck, sustained, rageable, headOn, legDone, alive, owner);
        }

        // ---- the pipeline ----
        [Test] public void NotStuck_SuccessWithNone() { var r = new RecoveryPipeline().Evaluate(In(0f, Stuck - 0.1f)); Assert.That(r.IsSuccess, Is.True); Assert.That(r.Value.IsNone, Is.True); }
        [Test] public void DeadTroll_Ignored_MachineUntouched()
        {
            var p = new RecoveryPipeline();
            var r = p.Evaluate(In(0f, Stuck, rageable: true, alive: false));
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Ignored));
            Assert.That(r.Reason, Is.EqualTo(RecoveryPipeline.NotAlive));
            Assert.That(p.Stage, Is.EqualTo(RecoveryStage.Travel));
            Assert.That(p.Machine.Raged, Is.False);
        }
        [Test] public void NotOwner_Rejected_MachineUntouched()
        {
            var p = new RecoveryPipeline();
            var r = p.Evaluate(In(0f, Stuck, rageable: true, owner: false));
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(r.Reason, Is.EqualTo(RecoveryPipeline.NotOwner));
            Assert.That(p.Machine.Raged, Is.False);
        }
        [Test] public void Rage_DecisionCarriesTheStage()
        {
            var p = new RecoveryPipeline();
            var d = p.Evaluate(In(10f, Stuck, rageable: true)).Value;
            Assert.That(d.Command, Is.EqualTo(RecoveryCmd.StartRage));
            Assert.That(d.Stage, Is.EqualTo(RecoveryStage.Rage));
            Assert.That(d.Raged, Is.True);
            Assert.That(p.Evaluate(In(15f)).Value.Command, Is.EqualTo(RecoveryCmd.EndRage));
        }
        [Test] public void Detour_DecisionCarriesDetourLegAndSide()
        {
            var p = new RecoveryPipeline();
            var d1 = p.Evaluate(In(0f, Stuck)).Value;
            Assert.That(d1.Command, Is.EqualTo(RecoveryCmd.StartLeg));
            Assert.That(d1.DetourNumber, Is.EqualTo(1));
            Assert.That(d1.Leg, Is.EqualTo(0));
            Assert.That(d1.LegSide, Is.EqualTo(Side.Right));
            Assert.That(p.Evaluate(In(1f, legDone: true)).Value.Command, Is.EqualTo(RecoveryCmd.EndLeg));
            var d2 = p.Evaluate(In(2f, Stuck)).Value;
            Assert.That(d2.DetourNumber, Is.EqualTo(2));
            var leg2 = p.Evaluate(In(3f, legDone: true)).Value;
            Assert.That(leg2.Command, Is.EqualTo(RecoveryCmd.StartLeg));
            Assert.That(leg2.Leg, Is.EqualTo(1));
            Assert.That(leg2.LegSide, Is.EqualTo(Side.Left), "the second leg of the L turns back");
        }
        [Test] public void AfterAFailedCycle_TurnsLeftFirst() { var p = new RecoveryPipeline(); p.BeginCycle(1); Assert.That(p.Evaluate(In(0f, Stuck)).Value.LegSide, Is.EqualTo(Side.Left)); }
        [Test] public void HeadOn_FirstDetourTurnsRight_EvenAfterAFailure()
        {
            var p = new RecoveryPipeline(); p.BeginCycle(1);
            var d = p.Evaluate(In(0f, Stuck, headOn: true)).Value;
            Assert.That(d.HeadOn, Is.True);
            Assert.That(d.LegSide, Is.EqualTo(Side.Right));
        }

        // behaviour unchanged: a scripted frame sequence gives the same commands through the pipeline as through the bare machine of D-167
        [Test] public void Pipeline_EqualsTheBareMachine_OnAFullLadder()
        {
            var frames = new List<RecoveryInput>
            {
                In(0f, 1f), In(1f, Stuck, rageable: true), In(3f), In(6f), In(7f, Stuck), In(8f), In(9f, legDone: true), In(10f, Stuck), In(11f, legDone: true), In(12f, legDone: true),
                In(13f, Stuck), In(14f, 0f, StuckRules.SustainedReset), In(15f, Stuck, headOn: true), In(16f, legDone: true),
            };
            var m = new RecoveryMachine();
            var p = new RecoveryPipeline();
            foreach (var f in frames)
            {
                var expected = m.Step(f.Now, f.StuckSeconds, f.SustainedSeconds, f.Rageable, f.HeadOn, f.LegDone);
                var got = p.Evaluate(f);
                Assert.That(got.IsSuccess, Is.True);
                Assert.That(got.Value.Command, Is.EqualTo(expected), "frame " + f);
                Assert.That(p.Stage, Is.EqualTo(m.Stage), "frame " + f);
            }
        }
        [Test] public void Abort_BackToTravel() { var p = new RecoveryPipeline(); p.Evaluate(In(0f, Stuck)); Assert.That(p.Abort(), Is.EqualTo(RecoveryStage.Travel)); }

        // ---- the executor ----
        sealed class Owner : ITrollOwnership { public bool IsValid { get { return Mine; } } public bool IsOwner { get { return Mine; } } internal bool Mine = true; }

        static RecoveryExecutor Exec(Owner o, List<string> log)
        {
            return new RecoveryExecutor(o, () => log.Add("rage"), () => log.Add("endRage"), d => log.Add("leg" + d.DetourNumber + "." + d.Leg), () => log.Add("endLeg"), () => log.Add("failed"), () => log.Add("reset"));
        }

        static RecoveryDecision Decide(RecoveryPipeline p, RecoveryInput i) { return p.Evaluate(i).Value; }

        [Test] public void Executor_MapsEachCommandToItsAction()
        {
            var log = new List<string>();
            var x = Exec(new Owner(), log);
            var p = new RecoveryPipeline();
            Assert.That(x.Execute(Decide(p, In(0f, Stuck, rageable: true))).Value, Is.EqualTo(RecoveryCmd.StartRage));
            x.Execute(Decide(p, In(5f)));
            x.Execute(Decide(p, In(6f, Stuck)));
            x.Execute(Decide(p, In(7f, legDone: true)));
            x.Execute(Decide(p, In(8f, Stuck)));
            x.Execute(Decide(p, In(9f, legDone: true)));
            x.Execute(Decide(p, In(10f, legDone: true)));
            x.Execute(Decide(p, In(11f, Stuck)));
            Assert.That(log, Is.EqualTo(new[] { "rage", "endRage", "leg1.0", "endLeg", "leg2.0", "leg2.1", "endLeg", "failed" }));
            p.BeginCycle(0);
            p.Evaluate(In(12f, Stuck));
            p.Evaluate(In(13f, legDone: true));
            Assert.That(x.Execute(Decide(p, In(14f, 0f, StuckRules.SustainedReset))).Value, Is.EqualTo(RecoveryCmd.CycleReset));
            Assert.That(log[log.Count - 1], Is.EqualTo("reset"));
        }
        [Test] public void Executor_None_Ignored_NothingRuns()
        {
            var log = new List<string>();
            var r = Exec(new Owner(), log).Execute(Decide(new RecoveryPipeline(), In(0f)));
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Ignored));
            Assert.That(log, Is.Empty);
        }
        // the plan's key test: an executor must not act when the troll is no longer owned by the local client
        [Test] public void Executor_NotOwner_Rejected_NothingRuns()
        {
            var log = new List<string>();
            var o = new Owner();
            var x = Exec(o, log);
            var d = Decide(new RecoveryPipeline(), In(0f, Stuck, rageable: true));
            o.Mine = false;                         // ownership moved to another peer between the decision and the execution
            var r = x.Execute(d);
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(r.Reason, Is.EqualTo(RecoveryExecutor.NotOwner));
            Assert.That(log, Is.Empty);
        }
        [Test] public void Executor_NullOwner_Rejected() { var log = new List<string>(); Assert.That(new RecoveryExecutor(null, null, null, null, null, null, null).Execute(Decide(new RecoveryPipeline(), In(0f, Stuck))).Status, Is.EqualTo(ResponseStatus.Rejected)); }
        [Test] public void Executor_MissingAction_Ignored()
        {
            var r = new RecoveryExecutor(new Owner(), null, null, null, null, null, null).Execute(Decide(new RecoveryPipeline(), In(0f, Stuck)));
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Ignored));
        }
    }
}
