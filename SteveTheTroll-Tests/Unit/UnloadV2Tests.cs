using System.Collections.Generic;
using NUnit.Framework;
using SteveTheTroll.Backpack.Application;
using SteveTheTroll.Backpack.Domain;
using SteveTheTroll.Core.Results;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-173 (Architecture V2, P6 unload slice): DepotPlanner picks what TrollDepot.Plan picked before (nearest chest with room, nobody else at / walking to it, else
    /// a new depot that avoids busy stacks), UnloadRules / UnloadPipeline decide per stack (Put / BuildDepot up to the limit / Leave) and refuse a client that does not own the troll.</summary>
    [TestFixture]
    public class UnloadV2Tests
    {
        static DepotCandidate C(bool room, float d, bool crowded = false, bool reserved = false) { return new DepotCandidate(room, crowded, reserved, d); }

        [Test] public void Planner_NearestChestWithRoom()
        {
            var p = DepotPlanner.Choose(new List<DepotCandidate> { C(true, 12f), C(true, 5f), C(false, 1f) });
            Assert.That(p.Chosen, Is.EqualTo(1)); Assert.That(p.BuildNew, Is.False); Assert.That(p.Skipped, Is.EqualTo(0));
        }
        [Test] public void Planner_SkipsCrowdedAndReserved()
        {
            var p = DepotPlanner.Choose(new List<DepotCandidate> { C(true, 2f, crowded: true), C(true, 3f, reserved: true), C(true, 9f) });
            Assert.That(p.Chosen, Is.EqualTo(2)); Assert.That(p.Skipped, Is.EqualTo(2));
        }
        [Test] public void Planner_AllBusy_BuildsAwayFromThem()
        {
            var p = DepotPlanner.Choose(new List<DepotCandidate> { C(true, 2f, crowded: true), C(true, 3f, reserved: true) });
            Assert.That(p.BuildNew, Is.True); Assert.That(p.AvoidBusy, Is.True);
        }
        [Test] public void Planner_NoRoomAnywhere_BuildsWithoutAvoiding()
        {
            var p = DepotPlanner.Choose(new List<DepotCandidate> { C(false, 2f, crowded: true), C(false, 3f) });
            Assert.That(p.BuildNew, Is.True); Assert.That(p.AvoidBusy, Is.False); Assert.That(p.Skipped, Is.EqualTo(0));
        }
        [Test] public void Planner_NoChests_BuildNew() { Assert.That(DepotPlanner.Choose(new List<DepotCandidate>()).BuildNew, Is.True); Assert.That(DepotPlanner.Choose(null).BuildNew, Is.True); }
        [Test] public void Planner_EqualDistance_FirstWins() { Assert.That(DepotPlanner.Choose(new List<DepotCandidate> { C(true, 4f), C(true, 4f) }).Chosen, Is.EqualTo(0)); }

        [TestCase(true, 0, 3, "Put")] [TestCase(true, 3, 3, "Put")] [TestCase(false, 0, 3, "BuildDepot")] [TestCase(false, 2, 3, "BuildDepot")] [TestCase(false, 3, 3, "Leave")]
        public void Rules_ForStack(bool room, int built, int max, string expected) { Assert.That(UnloadRules.ForStack(room, built, max).ToString(), Is.EqualTo(expected)); }

        [Test] public void Pipeline_NotOwner_Rejected() { var r = new UnloadPipeline().Evaluate(new UnloadInput(false, true, true, 0, 3)); Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected)); Assert.That(r.Reason, Is.EqualTo(UnloadPipeline.NotOwner)); }
        [Test] public void Pipeline_NoData_Ignored() { var r = new UnloadPipeline().Evaluate(new UnloadInput(true, false, true, 0, 3)); Assert.That(r.Status, Is.EqualTo(ResponseStatus.Ignored)); Assert.That(r.Reason, Is.EqualTo(UnloadPipeline.NoData)); }
        [Test] public void Pipeline_Owner_DecidesTheStack() { var r = new UnloadPipeline().Evaluate(new UnloadInput(true, true, false, 1, 3)); Assert.That(r.IsSuccess, Is.True); Assert.That(r.Value, Is.EqualTo(UnloadStep.BuildDepot)); }
    }
}
