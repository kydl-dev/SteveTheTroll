using KydlCore.Core;
using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Backpack.Application;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Backpack.Execution;
using SteveTheTroll.Features.Trolls.Contracts;

namespace SteveTheTroll.Tests.Unit;

/// <summary>D-173 (Architecture V2, P6 pickup slice): the pipeline decides what BackpackPickup decided before (owner, chest open, full, budget, drop ownership) and the
/// executor performs nothing for a client that does not own the troll.</summary>
[TestFixture]
public class PickupV2Tests
{
    sealed class Own : ITrollOwnership { internal bool Owner = true; public bool IsValid { get { return true; } } public bool IsOwner { get { return Owner; } } }

    static PickupInput In(bool owner = true, bool open = false, bool full = false, bool budget = false, bool valid = true, bool owned = true)
    { return new PickupInput(owner, open, full, budget, valid, owned); }

    [Test] public void OwnedDrop_Take() { var r = new PickupPipeline().Evaluate(In());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.IsSuccess, Is.True); Assert.That(r.Value, Is.EqualTo(PickupAction.Take));
        }
    }
    [Test] public void ForeignDrop_RequestOwnership() { Assert.That(new PickupPipeline().Evaluate(In(owned: false)).Value, Is.EqualTo(PickupAction.RequestOwnership)); }
    [Test] public void VanishedDrop_Skip() { Assert.That(new PickupPipeline().Evaluate(In(valid: false, owned: false)).Value, Is.EqualTo(PickupAction.Skip)); }
    [Test] public void NotTheTrollsOwner_Rejected() { var r = new PickupPipeline().Evaluate(In(owner: false));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected)); Assert.That(r.Reason, Is.EqualTo(PickupPipeline.NotOwner));
        }
    }
    [Test] public void ChestOpen_Ignored() { var r = new PickupPipeline().Evaluate(In(open: true));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Ignored)); Assert.That(r.Reason, Is.EqualTo(PickupPipeline.ChestOpen));
        }
    }
    [Test] public void Full_Ignored() { var r = new PickupPipeline().Evaluate(In(full: true));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Ignored)); Assert.That(r.Reason, Is.EqualTo(PickupPipeline.Full));
        }
    }
    [Test] public void BudgetUsed_Ignored() { var r = new PickupPipeline().Evaluate(In(budget: true));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Ignored)); Assert.That(r.Reason, Is.EqualTo(PickupPipeline.Budget));
        }
    }
    [Test] public void OwnerCheckComesFirst() { Assert.That(new PickupPipeline().Evaluate(In(owner: false, open: true, full: true, budget: true)).Status, Is.EqualTo(ResponseStatus.Rejected)); }

    [Test] public void Executor_Take_RunsTake()
    {
        int took = 0; var e = new PickupExecutor(null, () => { took++; return true; });
        var r = e.Execute(new Own(), PickupAction.Take);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.IsSuccess, Is.True); Assert.That(took, Is.EqualTo(1));
        }
    }
    [Test] public void Executor_NoRoom_Rejected()
    {
        var r = new PickupExecutor(null, () => false).Execute(new Own(), PickupAction.Take);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.Status, Is.EqualTo(ResponseStatus.Rejected)); Assert.That(r.Reason, Is.EqualTo(PickupExecutor.NoRoom));
        }
    }
    [Test] public void Executor_Request_RunsRequest()
    {
        int asked = 0; var r = new PickupExecutor(() => asked++, () => true).Execute(new Own(), PickupAction.RequestOwnership);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.IsSuccess, Is.True); Assert.That(asked, Is.EqualTo(1));
        }
    }
    [Test] public void Executor_NotOwner_PerformsNothing()
    {
        int ran = 0; var e = new PickupExecutor(() => ran++, () => { ran++; return true; });
        using (Assert.EnterMultipleScope())
        {
            Assert.That(e.Execute(new Own { Owner = false }, PickupAction.Take).Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(e.Execute(new Own { Owner = false }, PickupAction.RequestOwnership).Status, Is.EqualTo(ResponseStatus.Rejected));
            Assert.That(ran, Is.EqualTo(0));
        }
    }
    [Test] public void Executor_Skip_Ignored() { Assert.That(new PickupExecutor(null, null).Execute(new Own(), PickupAction.Skip).Status, Is.EqualTo(ResponseStatus.Ignored)); }
}
