using System;
using KydlCore.Core;
using SteveTheTroll.Features.Hubs.Domain;

namespace SteveTheTroll.Features.Hubs.Application;

/// <summary>
///     D-173 (P7): the facts for "which hub does this troll belong to" (read by HubRegistry.For). Readonly struct,
///     game-free.
/// </summary>
internal readonly struct HubAssignmentInput
{
    internal bool HubsOn { get; }
    internal long Owner { get; }

    /// The hub number chosen by hand (0 = none).
    internal int Manual { get; }

    /// Flat metres to that hub (-1 = it does not exist).
    internal float ManualDistance { get; }

    internal int NearestNumber { get; }
    internal float NearestDistance { get; }
    internal bool Auto { get; }

    internal HubAssignmentInput(bool hubsOn, long owner, int manual, float manualDistance, int nearestNumber,
        float nearestDistance, bool auto)
    {
        HubsOn = hubsOn;
        Owner = owner;
        Manual = manual;
        ManualDistance = manualDistance;
        NearestNumber = nearestNumber;
        NearestDistance = nearestDistance;
        Auto = auto;
    }
}

/// <summary>
///     D-173 (Architecture V2, P7, spec "HubAssignmentPipeline"): HubAssignmentInput -> validation (hubs switched off / no
///     owner: Ignored) -> HubRules.Pick (D-135 / D-159:
///     the hand-picked hub within ManualReach, else the nearest within AutoReach when auto-assign is on) ->
///     OperationResponse of the hub number (0 = the bed). Game-free.
/// </summary>
internal sealed class HubAssignmentPipeline
{
    internal const string HubsOff = "Troll Hubs are switched off", NoOwner = "the troll has no owner";

    private readonly OperationPipeline<HubAssignmentInput, int> _pipeline;

    internal HubAssignmentPipeline(Action<string> trace = null)
    {
        _pipeline = new OperationPipeline<HubAssignmentInput, int>("HubAssignmentPipeline",
                i => OperationResponse<int>.Success(HubRules.Pick(i.Manual, i.ManualDistance, i.NearestNumber,
                    i.NearestDistance, i.Auto)))
            .Validate("hubs on", i => ValidationResult.IgnoreUnless(i.HubsOn, HubsOff))
            .Validate("owner", i => ValidationResult.IgnoreUnless(i.Owner != 0L, NoOwner));
        _pipeline.Trace = trace;
    }

    /// Expected outcome: Success with the hub number (0 = no hub: the bed), Ignored when hubs are off or the troll has no owner.
    internal OperationResponse<int> Evaluate(HubAssignmentInput input)
    {
        return _pipeline.Run(input);
    }
}
