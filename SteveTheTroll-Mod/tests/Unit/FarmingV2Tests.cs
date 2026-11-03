using System.Collections.Generic;
using System;
using NUnit.Framework;
using Kydl.Core.Results;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Farming.Application;
using SteveTheTroll.Features.Trolls.Farming.Domain;
using SteveTheTroll.Features.Trolls.Farming.Execution;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-170 farming slice: the gate's priority order, the approach decision, the dry-swing ladder and the search order decide exactly what TrollFarmer decided
    /// before (parity with the old inline code, copied here as the reference), and the executor performs nothing for a client that does not own the troll.</summary>
    [TestFixture]
    public class FarmingV2Tests
    {
        // ---- FarmGateRules: every branch in priority order ----
        static FarmGateInput G(bool alive = true, bool owner = true, bool follow = false, bool unloading = false, bool fighting = false, bool recovery = false, bool fullDue = false, bool home = false, bool weapon = true)
        {
            return new FarmGateInput(alive, owner, follow, unloading, fighting, recovery, fullDue, home, weapon);
        }

        [Test] public void Gate_Dead_Stops_BeforeEverything() { Assert.That(FarmGateRules.Decide(G(alive: false, follow: true, fighting: true, recovery: true)), Is.EqualTo(FarmAction.Stop)); }
        [Test] public void Gate_FollowOrder_Stops_UnlessUnloading()
        {
            Assert.That(FarmGateRules.Decide(G(follow: true, fighting: true)), Is.EqualTo(FarmAction.StopFollow));
            Assert.That(FarmGateRules.Decide(G(follow: true, unloading: true, home: true)), Is.EqualTo(FarmAction.GoHome), "D-149: a full troll on its way to the depot ignores the follow order");
        }
        [Test] public void Gate_Fight_BeforeRecovery() { Assert.That(FarmGateRules.Decide(G(fighting: true, recovery: true)), Is.EqualTo(FarmAction.YieldToFight)); }
        [Test] public void Gate_Recovery_BeforeFullCheck() { Assert.That(FarmGateRules.Decide(G(recovery: true, fullDue: true)), Is.EqualTo(FarmAction.RecoveryDrives)); }
        [Test] public void Gate_FullCheck_BeforeHome_D159() { Assert.That(FarmGateRules.Decide(G(fullDue: true, home: true)), Is.EqualTo(FarmAction.CheckFull)); }
        [Test] public void Gate_Home_BeforeWeapon() { Assert.That(FarmGateRules.Decide(G(home: true, weapon: false)), Is.EqualTo(FarmAction.GoHome)); }
        [Test] public void Gate_NoWeapon_ThenWork() { Assert.That(FarmGateRules.Decide(G(weapon: false)), Is.EqualTo(FarmAction.NoWeapon)); Assert.That(FarmGateRules.Decide(G()), Is.EqualTo(FarmAction.Work)); }
        [Test] public void Gate_FallThrough_ClearsOnlyThatFlag()
        {
            var i = G(recovery: true, fullDue: true, home: true);
            Assert.That(FarmGateRules.MayFallThrough(FarmAction.RecoveryDrives), Is.True);
            Assert.That(FarmGateRules.MayFallThrough(FarmAction.GoHome), Is.False);
            var j = i.Without(FarmAction.RecoveryDrives);
            Assert.That(FarmGateRules.Decide(j), Is.EqualTo(FarmAction.CheckFull));
            Assert.That(FarmGateRules.Decide(j.Without(FarmAction.CheckFull)), Is.EqualTo(FarmAction.GoHome));
        }

        // the old TrollFarmer.Step order, as the reference (each "return" of the old code = one action)
        static FarmAction OldOrder(bool alive, bool follow, bool homeAndFull, bool fighting, bool recoveryDrove, bool fullNow, bool home, bool weapon)
        {
            if (!alive) return FarmAction.Stop;
            if (follow && !homeAndFull) return FarmAction.StopFollow;
            if (fighting) return FarmAction.YieldToFight;
            if (recoveryDrove) return FarmAction.RecoveryDrives;
            if (fullNow) return FarmAction.CheckFull;
            if (home) return FarmAction.GoHome;
            return weapon ? FarmAction.Work : FarmAction.NoWeapon;
        }

        [Test] public void Gate_EqualsTheOldOrder_ForEveryCombination()
        {
            for (int bits = 0; bits < 256; bits++)
            {
                bool alive = (bits & 1) != 0, follow = (bits & 2) != 0, unloading = (bits & 4) != 0, fighting = (bits & 8) != 0, recovery = (bits & 16) != 0, fullDue = (bits & 32) != 0, home = (bits & 64) != 0, weapon = (bits & 128) != 0;
                if (unloading && !home) continue;            // unloading = home && full
                var input = G(alive, true, follow, unloading, fighting, recovery, fullDue, home, weapon);
                // the new loop: falls through RecoveryDrives / CheckFull when they do not drive (here: recovery always drives, the full check finds "full")
                Assert.That(FarmGateRules.Decide(input), Is.EqualTo(OldOrder(alive, follow, unloading, fighting, recovery, fullDue, home, weapon)), "bits " + bits);
            }
        }

        // ---- ApproachRules ----
        [Test] public void Approach_Stepping_First() { Assert.That(ApproachRules.Decide(true, 10f, 2f, 0f), Is.EqualTo(ApproachAction.Stepping)); }
        [Test] public void Approach_InReach_AtTarget() { Assert.That(ApproachRules.Decide(false, 2f, 2f, 0f), Is.EqualTo(ApproachAction.AtTarget)); }
        [Test] public void Approach_Far_Walks_EvenWhenStuck_D063() { Assert.That(ApproachRules.Decide(false, 5f, 2f, 99f), Is.EqualTo(ApproachAction.Walk)); }
        [Test] public void Approach_RightAtIt_WalksHalfASecond_ThenSwings_D062()
        {
            Assert.That(ApproachRules.Decide(false, 2.8f, 2f, 0.5f), Is.EqualTo(ApproachAction.Walk));
            Assert.That(ApproachRules.Decide(false, 2.8f, 2f, 0.51f), Is.EqualTo(ApproachAction.AtTarget));
        }
        [Test] public void ApproachMeter_ResetsOnRealGain()
        {
            var m = new ApproachMeter();
            m.Update(10f, 0.1f); m.Update(9.9f, 0.5f);
            Assert.That(m.StuckSeconds, Is.EqualTo(0.5f).Within(1e-4));
            m.Update(9.7f, 0.1f);
            Assert.That(m.StuckSeconds, Is.EqualTo(0f));
            Assert.That(m.Reset(), Is.EqualTo(0f));
            Assert.That(m.LastDistance, Is.EqualTo(float.MaxValue));
        }

        // ---- SwingLadder: parity with the old counters ----
        sealed class OldLadder
        {
            int _try = 1, _swings, _dry; uint _last;
            internal SwingAction Next(uint rev)
            {
                if (_swings > 0) { if (rev == _last) _dry++; else { _dry = 0; _try = 1; } }
                _last = rev;
                if (_swings >= 2 && _dry >= 2)
                {
                    if (_try >= 4) { _try = 1; _swings = 0; _dry = 0; _last = 0; return SwingAction.GiveUp; }   // GiveUp -> ResetTries
                    _try++; _swings = 0; _dry = 0;
                    return SwingAction.StepForward;
                }
                return SwingAction.Swing;
            }
            internal void Swung() { _swings++; }
        }

        [Test] public void Ladder_AlwaysDry_TwoSwingsPerTry_FourTries_ThenGiveUp()
        {
            var l = new SwingLadder();
            var seen = new List<SwingAction>();
            for (int i = 0; i < 20 && (seen.Count == 0 || seen[seen.Count - 1] != SwingAction.GiveUp); i++)
            {
                var a = l.Next(7u);
                seen.Add(a);
                if (a == SwingAction.Swing) l.Swung();
            }
            Assert.That(seen.FindAll(a => a == SwingAction.StepForward).Count, Is.EqualTo(3));
            Assert.That(seen[seen.Count - 1], Is.EqualTo(SwingAction.GiveUp));
        }
        [Test] public void Ladder_DamageResetsToTryOne()
        {
            var l = new SwingLadder();
            l.Next(1u); l.Swung(); l.Next(1u); l.Swung(); l.Next(1u);      // dry 2 -> step
            Assert.That(l.Try, Is.EqualTo(2));
            l.Next(5u); l.Swung(); l.Next(9u);                              // the source changed: damage
            Assert.That(l.Try, Is.EqualTo(1));
            Assert.That(l.Dry, Is.EqualTo(0));
        }
        [Test] public void Ladder_EqualsTheOldCounters_OnRandomRuns()
        {
            var rnd = new Random(170);
            for (int run = 0; run < 300; run++)
            {
                var neu = new SwingLadder(); var old = new OldLadder();
                uint rev = 0;
                for (int step = 0; step < 40; step++)
                {
                    if (rnd.NextDouble() < 0.3) rev++;                       // sometimes a hit lands
                    var a = neu.Next(rev); var b = old.Next(rev);
                    Assert.That(a, Is.EqualTo(b), "run " + run + " step " + step);
                    if (a == SwingAction.GiveUp) neu.Reset();                // TrollFarmer.GiveUp -> ResetTries
                    bool started = rnd.NextDouble() < 0.9;                   // StartAttack can fail
                    if (a == SwingAction.Swing && started) { neu.Swung(); old.Swung(); }
                }
            }
        }

        // ---- TargetSelection: range first, then focus order ----
        [Test] public void Selection_SmallestRangeWins_ThenFocusOrder()
        {
            var ranges = new[] { 20f, 50f, 150f };
            var goals = new[] { "Wood", "Stone" };
            // Stone exists at 20 m, Wood only at 50 m: Stone within the quick range wins (same as the old nested loops)
            var hit = TargetSelection.FirstHit(ranges, goals, (r, g) => (g == "Stone" && r >= 20f) || (g == "Wood" && r >= 50f));
            Assert.That(hit.IsSuccess, Is.True);
            Assert.That(goals[hit.Value.GoalIndex], Is.EqualTo("Stone"));
            Assert.That(hit.Value.RangeIndex, Is.EqualTo(0));
            var both = TargetSelection.FirstHit(ranges, goals, (r, g) => r >= 50f);
            Assert.That(goals[both.Value.GoalIndex], Is.EqualTo("Wood"), "same range: focus first");
        }
        [Test] public void Selection_Nothing_Ignored() { var r = TargetSelection.FirstHit(new[] { 1f }, new[] { "x" }, (a, b) => a < 0f); Assert.That(r.Status, Is.EqualTo(ResponseStatus.Ignored)); Assert.That(r.Reason, Is.EqualTo(TargetSelection.NothingFound)); }
        [Test] public void Selection_MissingArgs_Failed() { Assert.That(TargetSelection.FirstHit<string>(null, new[] { "x" }, (a, b) => true).Status, Is.EqualTo(ResponseStatus.Failed)); }
        [Test] public void Selection_StopsAtTheFirstHit()
        {
            int calls = 0;
            TargetSelection.FirstHit(new[] { 1f, 2f }, new[] { "a", "b" }, (r, g) => { calls++; return g == "b"; });
            Assert.That(calls, Is.EqualTo(2));
        }

        // ---- pipeline + executor ----
        sealed class Owner : ITrollOwnership { public bool IsValid { get { return Mine; } } public bool IsOwner { get { return Mine; } } internal bool Mine = true; }

        [Test] public void Pipeline_NotOwner_Rejected() { var r = new FarmingPipeline().Evaluate(G(owner: false)); Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected)); Assert.That(r.Reason, Is.EqualTo(FarmingPipeline.NotOwner)); }
        [Test] public void Pipeline_Owner_Decides() { Assert.That(new FarmingPipeline().Evaluate(G(home: true)).Value, Is.EqualTo(FarmAction.GoHome)); }

        [Test] public void Executor_MapsActions_AndReturnsWhetherItDrove()
        {
            var log = new List<string>();
            var x = new FarmingExecutor(new Owner(), new FarmingActions
            {
                Stop = () => { log.Add("stop"); return false; }, GoHome = () => { log.Add("home"); return true; }, Work = () => { log.Add("work"); return true; },
                Swing = () => { log.Add("swing"); return true; }, StepForward = () => { log.Add("step"); return true; }, GiveUp = () => { log.Add("giveup"); return true; },
            });
            Assert.That(x.Execute(FarmAction.Stop).Value, Is.False);
            Assert.That(x.Execute(FarmAction.GoHome).Value, Is.True);
            x.Execute(FarmAction.Work); x.Execute(SwingAction.Swing); x.Execute(SwingAction.StepForward); x.Execute(SwingAction.GiveUp);
            Assert.That(log, Is.EqualTo(new[] { "stop", "home", "work", "swing", "step", "giveup" }));
            Assert.That(x.Execute(FarmAction.NoWeapon).Status, Is.EqualTo(ResponseStatus.Ignored), "nothing bound");
        }
        [Test] public void Executor_NotOwner_PerformsNothing()
        {
            int ran = 0;
            var o = new Owner { Mine = false };
            var x = new FarmingExecutor(o, new FarmingActions { Work = () => { ran++; return true; }, Swing = () => { ran++; return true; } });
            Assert.That(x.Execute(FarmAction.Work).Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(x.Execute(SwingAction.Swing).Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(x.Execute(FarmAction.NoWeapon).Status, Is.EqualTo(ResponseStatus.Rejected), "not owner beats 'nothing bound'");
            Assert.That(ran, Is.EqualTo(0));
        }
    }
}
