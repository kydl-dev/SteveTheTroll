using System;
using KydlCore.Core;
using SteveTheTroll.Features.Trolls.Backpack.Domain;

namespace SteveTheTroll.Features.Trolls.Backpack.Application;

/// <summary>D-173 (P6): the facts for one stack of an unload (read by TrollDepot.Unload). Readonly struct, game-free.</summary>
internal readonly struct UnloadInput
{
    internal bool TrollOwner { get; }
    internal bool HasBackpackData { get; }
    internal bool ChestWithRoom { get; }
    internal int Built { get; }
    internal int MaxNew { get; }

    internal UnloadInput(bool trollOwner, bool hasBackpackData, bool chestWithRoom, int built, int maxNew)
    {
        TrollOwner = trollOwner;
        HasBackpackData = hasBackpackData;
        ChestWithRoom = chestWithRoom;
        Built = built;
        MaxNew = maxNew;
    }
}

/// <summary>
///     D-173 (Architecture V2, P6 unload slice): UnloadInput -> validation (another client owns the troll: Rejected - its
///     chests are written only by the owner; no backpack data:
///     Ignored) -> UnloadRules.ForStack -> OperationResponse of UnloadStep. Built once, no allocation per run. Game-free.
/// </summary>
internal sealed class UnloadPipeline
{
    internal const string NotOwner = "another client owns the troll", NoData = "the troll has no backpack data";

    private readonly OperationPipeline<UnloadInput, UnloadStep> _pipeline;

    internal UnloadPipeline(Action<string> trace = null)
    {
        _pipeline = new OperationPipeline<UnloadInput, UnloadStep>("UnloadPipeline",
                i => OperationResponse<UnloadStep>.Success(UnloadRules.ForStack(i.ChestWithRoom, i.Built, i.MaxNew)))
            .Validate("troll owner", i => ValidationResult.RejectUnless(i.TrollOwner, NotOwner))
            .Validate("backpack data", i => ValidationResult.IgnoreUnless(i.HasBackpackData, NoData));
        _pipeline.Trace = trace;
    }

    internal OperationResponse<UnloadStep> Evaluate(UnloadInput input)
    {
        return _pipeline.Run(input);
    }
}
