using System;
using KydlCore.Core;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Farming.Domain;

namespace SteveTheTroll.Features.Trolls.Farming.Execution;

/// <summary>
///     D-170: the actions a farmer binds once (method groups). Each returns whether it drove the troll this frame
///     (true = the game's AI is skipped).
/// </summary>
internal sealed class FarmingActions
{
    internal Func<bool> Stop, StopFollow, YieldToFight, RecoveryDrives, CheckFull, GoHome, NoWeapon, Work;
    internal Func<bool> Swing, StepForward, GiveUp;
}

/// <summary>
///     D-170, farming slice: performs a FarmAction / SwingAction through the farmer's bound actions. Ownership is checked
///     here (not owner = Rejected, nothing performed).
///     Success(value) = the action ran; value = whether it drove the troll. Game-free.
/// </summary>
internal sealed class FarmingExecutor
{
    internal const string NotOwner = "not the owner - nothing performed";
    private readonly FarmingActions _a;

    private readonly ITrollOwnership _owner;

    internal FarmingExecutor(ITrollOwnership owner, FarmingActions actions)
    {
        _owner = owner;
        _a = actions ?? new FarmingActions();
    }

    private bool Mine => _owner != null && _owner.IsOwner;

    /// Expected outcome: Success(drove) when the bound action ran, Rejected when not the owner, Ignored when nothing is bound for it.
    internal OperationResponse<bool> Execute(FarmAction action)
    {
        switch (action)
        {
            case FarmAction.Stop: return Run(_a.Stop, action);
            case FarmAction.StopFollow: return Run(_a.StopFollow, action);
            case FarmAction.YieldToFight: return Run(_a.YieldToFight, action);
            case FarmAction.RecoveryDrives: return Run(_a.RecoveryDrives, action);
            case FarmAction.CheckFull: return Run(_a.CheckFull, action);
            case FarmAction.GoHome: return Run(_a.GoHome, action);
            case FarmAction.NoWeapon: return Run(_a.NoWeapon, action);
            default: return Run(_a.Work, action);
        }
    }

    /// Expected outcome: as Execute, for the swing ladder's answer.
    internal OperationResponse<bool> Execute(SwingAction action)
    {
        switch (action)
        {
            case SwingAction.StepForward: return Run(_a.StepForward, action);
            case SwingAction.GiveUp: return Run(_a.GiveUp, action);
            default: return Run(_a.Swing, action);
        }
    }

    // two overloads instead of one taking object: no boxing of the enum on the per-frame path (its name is only built when nothing is bound)
    private OperationResponse<bool> Run(Func<bool> f, FarmAction a)
    {
        return f == null && Mine ? OperationResponse<bool>.Ignored("no action bound for " + a) : Run(f);
    }

    private OperationResponse<bool> Run(Func<bool> f, SwingAction a)
    {
        return f == null && Mine ? OperationResponse<bool>.Ignored("no action bound for " + a) : Run(f);
    }

    private OperationResponse<bool> Run(Func<bool> f)
    {
        if (!Mine)
        {
            return OperationResponse<bool>.Rejected(NotOwner);
        }

        return OperationResponse<bool>.Success(f());
    }
}
