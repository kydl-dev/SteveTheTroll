using System;
using KydlCore.Core;
using SteveTheTroll.Features.Hubs.Domain;

namespace SteveTheTroll.Features.Hubs.Application;

/// <summary>D-173 (P7): the facts for the walk back to the hub (D-162), read by TrollReturn. Readonly struct, game-free.</summary>
internal struct HubReturnInput
{
    internal bool TrollOwner { get; set; }
    internal bool HasHub { get; set; }

    /// Flat metres to the hub (-1 = no hub).
    internal float Distance { get; set; }

    internal bool FollowOrder { get; set; }
    internal bool Arriving { get; set; }
    internal bool OwnerWears { get; set; }
    internal bool Fighting { get; set; }
    internal bool Unloading { get; set; }

}

/// <summary>
///     D-173 (Architecture V2, P7, spec "HubReturnPipeline"): two decisions of TrollReturn, both owner-gated (Rejected for
///     another client) - Start: should a troll without a
///     job walk back (HubRules.ShouldStartReturn), Step: what a walking-back troll does this step (HubRules.ReturnNext; no
///     hub any more: Ignored). Game-free.
/// </summary>
internal sealed class HubReturnPipeline
{
    internal const string NotOwner = "another client owns the troll", NoHub = "the troll has no hub";

    private readonly OperationPipeline<HubReturnInput, bool> _start;
    private readonly OperationPipeline<HubReturnInput, HubRules.ReturnStep> _step;

    internal HubReturnPipeline(Action<string> trace = null)
    {
        _start = new OperationPipeline<HubReturnInput, bool>("HubReturnPipeline.Start",
                i => OperationResponse<bool>.Success(HubRules.ShouldStartReturn(i.HasHub, i.Distance, i.FollowOrder,
                    i.Arriving, i.OwnerWears, i.Fighting, i.Unloading)))
            .Validate("troll owner", i => ValidationResult.RejectUnless(i.TrollOwner, NotOwner));
        _step = new OperationPipeline<HubReturnInput, HubRules.ReturnStep>("HubReturnPipeline.Step",
                i => OperationResponse<HubRules.ReturnStep>.Success(HubRules.ReturnNext(i.FollowOrder, i.Fighting,
                    i.Distance)))
            .Validate("troll owner", i => ValidationResult.RejectUnless(i.TrollOwner, NotOwner))
            .Validate("hub", i => ValidationResult.IgnoreUnless(i.HasHub, NoHub));
        _start.Trace = trace;
        _step.Trace = trace;
    }

    /// Expected outcome: Success(true) = start walking back now.
    internal OperationResponse<bool> Start(HubReturnInput input)
    {
        return _start.Run(input);
    }

    /// Expected outcome: Success with Walk / Yield / Done / Cancel.
    internal OperationResponse<HubRules.ReturnStep> Step(HubReturnInput input)
    {
        return _step.Run(input);
    }
}
