using NUnit.Framework;
using SteveTheTroll.Core.Results;
using SteveTheTroll.Trolls.Combat.Application;
using SteveTheTroll.Trolls.Combat.Domain;
using SteveTheTroll.Trolls.Combat.Execution;
using SteveTheTroll.Trolls.Contracts;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-175 (Architecture V2, P5 combat slice): DefencePipeline answers what TrollDefend.Defend decided before (slippers or raid, range, owner - in that order) and
    /// CombatExecutor forces the target first, falls back to a plain target, and does nothing for a client that does not own the troll.</summary>
    [TestFixture]
    public class CombatV2Tests
    {
        sealed class Own : ITrollOwnership { internal bool Owner = true; public bool IsValid { get { return true; } } public bool IsOwner { get { return Owner; } } }
        static DefenceInput In(bool owner = true, bool wears = true, bool raid = false, float d = 5f, float range = 10f) { return new DefenceInput(owner, wears, raid, d, range); }

        [Test] public void NearWithSlippers_Defends() { var r = new DefencePipeline().Evaluate(In()); Assert.That(r.IsSuccess && r.Value, Is.True); Assert.That(DefencePolicy.WhyNot(In()), Is.EqualTo("")); }
        [Test] public void NoSlippersNoRaid_Ignored() { var r = new DefencePipeline().Evaluate(In(wears: false)); Assert.That(r.Status, Is.EqualTo(ResponseStatus.Ignored)); Assert.That(r.Reason, Is.EqualTo(DefencePolicy.NoSlippers)); }
        [Test] public void NoSlippersButRaid_Defends() { Assert.That(new DefencePipeline().Evaluate(In(wears: false, raid: true)).IsSuccess, Is.True); }
        [Test] public void TooFar_Ignored() { var r = new DefencePipeline().Evaluate(In(d: 10.1f)); Assert.That(r.Reason, Is.EqualTo(DefencePolicy.TooFar)); }
        [Test] public void ExactlyAtRange_Defends() { Assert.That(new DefencePipeline().Evaluate(In(d: 10f)).IsSuccess, Is.True); }
        [Test] public void NotOwner_Rejected() { var r = new DefencePipeline().Evaluate(In(owner: false)); Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected)); Assert.That(r.Reason, Is.EqualTo(DefencePolicy.NotOwner)); }
        [Test] public void SameOrderAsThePolicy()
        {
            var p = new DefencePipeline();
            foreach (var i in new[] { In(owner: false, wears: false, d: 50f), In(owner: false, d: 50f), In(owner: false) })
                Assert.That(p.Evaluate(i).Reason, Is.EqualTo(DefencePolicy.WhyNot(i)));
        }

        [Test] public void Executor_ForcedFirst() { int set = 0; var r = new CombatExecutor(() => true, () => set++).Attack(new Own()); Assert.That(r.Value, Is.EqualTo(CombatExecutor.Forced)); Assert.That(set, Is.EqualTo(0)); }
        [Test] public void Executor_FallsBackToPlainTarget() { int set = 0; var r = new CombatExecutor(() => false, () => set++).Attack(new Own()); Assert.That(r.Value, Is.EqualTo(CombatExecutor.Plain)); Assert.That(set, Is.EqualTo(1)); }
        [Test] public void Executor_NotOwner_Nothing()
        {
            int ran = 0; var r = new CombatExecutor(() => { ran++; return true; }, () => ran++).Attack(new Own { Owner = false });
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected)); Assert.That(ran, Is.EqualTo(0));
        }
    }
}
