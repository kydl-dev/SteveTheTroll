using KydlCore.Core;
using NUnit.Framework;
using SteveTheTroll.Features.Hubs.Application;
using SteveTheTroll.Features.Hubs.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>D-173 (Architecture V2, P7): HubAssignmentPipeline answers what HubRegistry.For answered (hubs off / no owner: no hub; else HubRules.Pick), HubReturnPipeline
/// what TrollReturn decided (start checks in their old order, ReturnNext per step) and refuses a client that does not own the troll.</summary>
[TestFixture]
public class HubsV2Tests
{
    static HubAssignmentInput A(bool on = true, long owner = 7L, int manual = 0, float manualD = -1f, int nearest = 2, float nearestD = 300f, bool auto = true)
    { return new HubAssignmentInput(on, owner, manual, manualD, nearest, nearestD, auto); }

    [Test] public void Assign_NearestWithinReach() { var r = new HubAssignmentPipeline().Evaluate(A());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.IsSuccess, Is.True); Assert.That(r.Value, Is.EqualTo(2));
        }
    }
    [Test] public void Assign_SameAsPick()
    {
        var p = new HubAssignmentPipeline();
        foreach (var c in new[] { A(manual: 4, manualD: 50f), A(manual: 4, manualD: -1f), A(nearestD: HubRules.AutoReach + 1f), A(auto: false), A(nearest: 0) })
            Assert.That(p.Evaluate(c).Value, Is.EqualTo(HubRules.Pick(c.Manual, c.ManualDistance, c.NearestNumber, c.NearestDistance, c.Auto)));
    }
    [Test] public void Assign_HubsOff_Ignored() { var r = new HubAssignmentPipeline().Evaluate(A(on: false));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Ignored)); Assert.That(r.Reason, Is.EqualTo(HubAssignmentPipeline.HubsOff));
        }
    }
    [Test] public void Assign_NoOwner_Ignored() { Assert.That(new HubAssignmentPipeline().Evaluate(A(owner: 0L)).Reason, Is.EqualTo(HubAssignmentPipeline.NoOwner)); }

    static HubReturnInput R(bool owner = true, bool hub = true, float d = 1100f, bool follow = false, bool arriving = false, bool wears = true, bool fighting = false, bool unloading = false)
    {
        return new HubReturnInput
        {
            TrollOwner = owner, HasHub = hub, Distance = d, FollowOrder = follow, Arriving = arriving,
            OwnerWears = wears, Fighting = fighting, Unloading = unloading
        };
    }

    [Test] public void Start_FarFromHub_Starts() { var r = new HubReturnPipeline().Start(R()); Assert.That(r.IsSuccess && r.Value, Is.True); }
    [Test] public void Start_InsideReach_DoesNot() { Assert.That(new HubReturnPipeline().Start(R(d: 900f)).Value, Is.False); }
    [TestCase(true, false, true, false, false)] [TestCase(false, true, true, false, false)] [TestCase(false, false, false, false, false)] [TestCase(false, false, true, true, false)] [TestCase(false, false, true, false, true)]
    public void Start_SomethingElseHasIt_DoesNot(bool follow, bool arriving, bool wears, bool fighting, bool unloading)
    { Assert.That(new HubReturnPipeline().Start(R(follow: follow, arriving: arriving, wears: wears, fighting: fighting, unloading: unloading)).Value, Is.False); }
    [Test] public void Start_NotOwner_Rejected() { Assert.That(new HubReturnPipeline().Start(R(owner: false)).Status, Is.EqualTo(ResponseStatus.Rejected)); }
    [Test] public void Start_NoHub_DoesNot() { Assert.That(new HubReturnPipeline().Start(R(hub: false, d: -1f)).Value, Is.False); }

    [Test] public void Step_SameAsReturnNext()
    {
        var p = new HubReturnPipeline();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(p.Step(R(follow: true)).Value, Is.EqualTo(HubRules.ReturnStep.Cancel));
            Assert.That(p.Step(R(fighting: true)).Value, Is.EqualTo(HubRules.ReturnStep.Yield));
            Assert.That(p.Step(R(d: HubRules.FarmLeash)).Value, Is.EqualTo(HubRules.ReturnStep.Done));
            Assert.That(p.Step(R(d: 1000f)).Value, Is.EqualTo(HubRules.ReturnStep.Walk));
        }
    }
    [Test] public void Step_LostHub_Ignored() { var r = new HubReturnPipeline().Step(R(hub: false, d: -1f));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Ignored)); Assert.That(r.Reason, Is.EqualTo(HubReturnPipeline.NoHub));
        }
    }
    [Test] public void Step_NotOwner_Rejected() { Assert.That(new HubReturnPipeline().Step(R(owner: false)).Status, Is.EqualTo(ResponseStatus.Rejected)); }
}
