using NUnit.Framework;
using SteveTheTroll.Core.Results;
using SteveTheTroll.Trolls.Orders.Application;
using SteveTheTroll.Trolls.Orders.Domain;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-170 orders slice: the attack key's meaning (same branches as the old TrollOrders.Tick) and the validation order of the pipeline.</summary>
    [TestFixture]
    public class OrdersV2Tests
    {
        static OrderInput I(bool key = true, bool alive = true, bool blocked = false, bool troll = true, bool steer = true, bool creature = false, bool source = false, bool wall = false)
        {
            return new OrderInput(key, alive, blocked, troll, steer, creature, source, wall);
        }

        [Test] public void Rules_CreatureBeatsSource() { Assert.That(OrderRules.Decide(I(creature: true, source: true)), Is.EqualTo(OrderKind.Attack)); }
        [Test] public void Rules_Wall_Source_Nothing()
        {
            Assert.That(OrderRules.Decide(I(source: true, wall: true)), Is.EqualTo(OrderKind.BreakWall));
            Assert.That(OrderRules.Decide(I(source: true)), Is.EqualTo(OrderKind.BreakSource));
            Assert.That(OrderRules.Decide(I()), Is.EqualTo(OrderKind.FarmFocus));
        }

        [Test] public void Pipeline_NoKey_Ignored_Silently() { var r = new OrderPipeline().Evaluate(I(key: false, troll: false)); Assert.That(r.Status, Is.EqualTo(ResponseStatus.Ignored)); Assert.That(r.Reason, Is.EqualTo(OrderPipeline.NoKey)); }
        [Test] public void Pipeline_DeadOrMenu_Ignored()
        {
            Assert.That(new OrderPipeline().Evaluate(I(alive: false)).Reason, Is.EqualTo(OrderPipeline.Dead));
            Assert.That(new OrderPipeline().Evaluate(I(blocked: true)).Reason, Is.EqualTo(OrderPipeline.Blocked));
            Assert.That(new OrderPipeline().Evaluate(I(blocked: true)).Status, Is.EqualTo(ResponseStatus.Ignored));
        }
        [Test] public void Pipeline_NoTroll_Rejected_BeforeSteer()
        {
            var r = new OrderPipeline().Evaluate(I(troll: false, steer: false));
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(r.Reason, Is.EqualTo(OrderPipeline.NoTroll));
            Assert.That(new OrderPipeline().Evaluate(I(steer: false)).Reason, Is.EqualTo(OrderPipeline.NoSteer));
        }
        [Test] public void Pipeline_Valid_Decides() { var r = new OrderPipeline().Evaluate(I(source: true)); Assert.That(r.IsSuccess, Is.True); Assert.That(r.Value, Is.EqualTo(OrderKind.BreakSource)); }

        // the old Tick, as the reference: each early "return false" = not Success; otherwise the branch taken
        static string Old(bool key, bool alive, bool blocked, bool troll, bool steer, bool creature, bool source, bool wall)
        {
            if (!key) return "none";
            if (!alive || blocked) return "none";
            if (!troll) return "notroll";
            if (!steer) return "nosteer";
            if (creature) return "Attack";
            if (source) return wall ? "BreakWall" : "BreakSource";
            return "FarmFocus";
        }

        [Test] public void Pipeline_EqualsTheOldTick_ForEveryCombination()
        {
            var p = new OrderPipeline();
            for (int b = 0; b < 256; b++)
            {
                bool key = (b & 1) != 0, alive = (b & 2) != 0, blocked = (b & 4) != 0, troll = (b & 8) != 0, steer = (b & 16) != 0, creature = (b & 32) != 0, source = (b & 64) != 0, wall = (b & 128) != 0;
                if (wall && !source) continue;
                var r = p.Evaluate(I(key, alive, blocked, troll, steer, creature, source, wall));
                string got = r.IsSuccess ? r.Value.ToString() : r.Reason == OrderPipeline.NoTroll ? "notroll" : r.Reason == OrderPipeline.NoSteer ? "nosteer" : "none";
                Assert.That(got, Is.EqualTo(Old(key, alive, blocked, troll, steer, creature, source, wall)), "bits " + b);
            }
        }
    }
}
