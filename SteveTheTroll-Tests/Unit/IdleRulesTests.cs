using NUnit.Framework;
using SteveTheTroll.Trolls.Farming.Domain;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-144: a full backpack must send the troll to the depot by itself - also right after a login, with no order.</summary>
    [TestFixture]
    public class IdleRulesTests
    {
        [Test] public void FullTroll_WithoutAnyJob_GoesToTheDepot() { Assert.That(IdleRules.Gate(null, true, false), Is.EqualTo(IdleGate.Unload)); }
        [Test] public void FullTroll_SentJustBefore_IsNotSentAgain() { Assert.That(IdleRules.Gate(null, true, true), Is.EqualTo(IdleGate.Skip)); }
        [Test] public void FullTroll_WithAFarmJob_GoesToTheDepotAnyway() { Assert.That(IdleRules.Gate(IdleRules.FarmReason, true, false), Is.EqualTo(IdleGate.Unload)); }
        [Test] public void FullTroll_AlreadyUnloading_IsNotRestarted() { Assert.That(IdleRules.Gate("TrollFarmer is unloading", true, false), Is.EqualTo(IdleGate.Skip)); }
        [Test] public void FullTroll_Fighting_IsLeftAlone() { Assert.That(IdleRules.Gate("fighting", true, false), Is.EqualTo(IdleGate.Skip)); }
        [Test] public void FullTroll_Following_GoesToTheDepotAnyway() { Assert.That(IdleRules.Gate(IdleRules.FollowReason, true, false), Is.EqualTo(IdleGate.Unload)); }
        [Test] public void FullTroll_FollowingButJustSent_IsNotSentAgain() { Assert.That(IdleRules.Gate(IdleRules.FollowReason, true, true), Is.EqualTo(IdleGate.Skip)); }
        [Test] public void TrollWithRoom_Following_IsLeftAlone() { Assert.That(IdleRules.Gate(IdleRules.FollowReason, false, false), Is.EqualTo(IdleGate.Skip)); }
        [TestCase(0f, true)] [TestCase(12f, true)] [TestCase(-12f, true)] [TestCase(12.1f, false)] [TestCase(-34f, false)]
        public void Reachable(float dy, bool expected) { Assert.That(IdleRules.Reachable(dy), Is.EqualTo(expected)); }
        [Test] public void TrollWithRoom_GoesOnToTheFarmDecision() { Assert.That(IdleRules.Gate(null, false, false), Is.EqualTo(IdleGate.Continue)); }
        [Test] public void TrollWithRoomAndAJob_IsLeftAlone() { Assert.That(IdleRules.Gate("TrollFarmer is active", false, false), Is.EqualTo(IdleGate.Skip)); }

        [TestCase(false, 40f, 10f, true)]      // Steve 1: no job, 40 m in 10 s
        [TestCase(false, 3f, 10f, false)]      // idles a little
        [TestCase(true, 40f, 10f, false)]      // farming / following: walking is the job
        [TestCase(false, 40f, 4f, false)]      // window not complete
        public void Wanders(bool hasJob, float metres, float seconds, bool expected) { Assert.That(IdleRules.Wanders(hasJob, metres, seconds), Is.EqualTo(expected)); }

        // D-159: the full-backpack decision for every job a troll can have (full, sent recently?) - a full troll is delivered unless it fights, is already unloading or was just sent
        [TestCase(null, true, false, "Unload")]
        [TestCase(IdleRules.FarmReason, true, false, "Unload")]
        [TestCase(IdleRules.FollowReason, true, false, "Unload")]
        [TestCase("fighting", true, false, "Skip")]
        [TestCase("TrollFarmer is unloading", true, false, "Skip")]
        [TestCase(IdleRules.FarmReason, true, true, "Skip")]
        [TestCase(IdleRules.FarmReason, false, false, "Skip")]
        [TestCase(null, false, false, "Continue")]
        public void FullBackpack_Matrix(string busy, bool full, bool triedRecently, string expected) { Assert.That(IdleRules.Gate(busy, full, triedRecently).ToString(), Is.EqualTo(expected)); }
    }
}
