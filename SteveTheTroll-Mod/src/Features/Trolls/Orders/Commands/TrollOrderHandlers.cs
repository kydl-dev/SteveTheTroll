using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Orders.Commands;

/// <summary>
///     D-170: the handlers of the troll orders - the bodies of the old TrollFarming.Begin / BeginOn / SendHome /
///     BeginUnload (T-008d / T-008e / D-065 / D-143) unchanged, now
///     answering with a typed response: Success(true) = the troll now does it; Rejected = it is not this client's troll to
///     steer (owner check BEFORE any write) or it cannot do it.
/// </summary>
internal static class TrollOrderHandlers
{
    internal const string NotSteerable = "no AI / not the owner of the troll's network object";

    private static bool Steerable(Character troll, out MonsterAI ai)
    {
        ZNetView view = troll != null ? troll.GetComponent<ZNetView>() : null;
        ai = troll != null ? troll.GetComponent<MonsterAI>() : null;
        return ai != null && view != null && view.IsValid() && view.IsOwner();
    }

    private static TrollFarmer Farmer(Character troll)
    {
        TrollArrival arrival = troll.GetComponent<TrollArrival>();
        if (arrival != null)
        {
            Object.Destroy(arrival);
        }

        TrollFarmer f = troll.GetComponent<TrollFarmer>();
        return f != null ? f : troll.gameObject.AddComponent<TrollFarmer>();
    }

    internal sealed class FarmFocus : ICommandHandler<FarmFocusCommand, bool>
    {
        public OperationResponse<bool> Handle(FarmFocusCommand c)
        {
            MonsterAI ai;
            if (!Steerable(c.Troll, out ai))
            {
                ZNetView view = c.Troll != null ? c.Troll.GetComponent<ZNetView>() : null;
                ModLog.Warn("TrollFarming: cannot send " + (c.Troll != null ? TrollTaming.Tag(c.Troll) : "?") +
                            " (ai " + (ai != null) + ", owner " + (view != null && view.IsOwner()) + ")");
                return OperationResponse<bool>.Rejected(NotSteerable);
            }

            TrollTaming.StopFollow(ai);
            ai.SetPatrolPoint();
            TrollFarmer farmer = Farmer(c.Troll);
            farmer.Setup(c.Focus, c.Troll.transform.position, c.Announce);
            if (c.Announce)
            {
                farmer.ResumeSpotWalk(); // D-175: an idle restart (no player order) resumes a walk to the remembered spot that a reload cut off
            }

            ModLog.Info("TrollFarming: " + TrollTaming.Tag(c.Troll) + " farms '" +
                        (c.Focus.Length > 0 ? c.Focus : "default priority") +
                        (c.Announce ? " (no orders for a while)" : "") + "' around " +
                        c.Troll.transform.position.ToString("F0"));
            return OperationResponse<bool>.Success(c.Troll.GetComponent<TrollFarmer>() != null);
        }
    }

    internal sealed class BreakSource : ICommandHandler<BreakSourceCommand, bool>
    {
        private readonly FarmFocus _farm = new();

        public OperationResponse<bool> Handle(BreakSourceCommand c)
        {
            OperationResponse<bool> r = _farm.Handle(new FarmFocusCommand(c.Troll, c.Focus));
            if (!r.IsSuccess || !r.Value)
            {
                return r;
            }

            TrollFarmer f = c.Troll.GetComponent<TrollFarmer>();
            bool forced = f != null && f.Force(c.Source);
            return forced
                ? OperationResponse<bool>.Success(forced)
                : OperationResponse<bool>.Rejected("the troll cannot break " +
                                                   (c.Source != null ? c.Source.name : "that"));
        }
    }

    internal sealed class BreakWall : ICommandHandler<BreakWallCommand, bool>
    {
        public OperationResponse<bool> Handle(BreakWallCommand c)
        {
            bool ok = TrollWalls.Order(c.Troll, c.Wall, c.Focus);
            return ok
                ? OperationResponse<bool>.Success(ok)
                : OperationResponse<bool>.Rejected("the troll does not go for that wall (D-124: 2-star trolls only)");
        }
    }

    internal sealed class AttackTarget : ICommandHandler<AttackTargetCommand, bool>
    {
        public OperationResponse<bool> Handle(AttackTargetCommand c)
        {
            bool ordered = TrollGuard.Order(c.Troll, c.Target);
            ModLog.Verbose("TrollOrderHandlers: " + TrollTaming.Tag(c.Troll) + " attack order on " +
                           (c.Target != null ? c.Target.name : "?") + " - TrollGuard took it " + ordered);
            return OperationResponse<bool>.Success(ordered);
        }
    }

    internal sealed class SendHome : ICommandHandler<SendHomeCommand, bool>
    {
        public OperationResponse<bool> Handle(SendHomeCommand c)
        {
            MonsterAI ai;
            if (!Steerable(c.Troll, out ai))
            {
                return OperationResponse<bool>.Rejected(NotSteerable);
            }

            Farmer(c.Troll).SetupHome();
            ModLog.Info("TrollFarming: " + TrollTaming.Tag(c.Troll) + " walks to the owner's bed and waits");
            return OperationResponse<bool>.Success(c.Troll.GetComponent<TrollFarmer>() != null);
        }
    }

    internal sealed class Unload : ICommandHandler<UnloadCommand, bool>
    {
        public OperationResponse<bool> Handle(UnloadCommand c)
        {
            MonsterAI ai;
            if (!Steerable(c.Troll, out ai))
            {
                return OperationResponse<bool>.Rejected(NotSteerable);
            }

            Farmer(c.Troll).SetupUnload();
            ModLog.Info("TrollFarming: " + TrollTaming.Tag(c.Troll) + " goes to empty its backpack at the depot");
            return OperationResponse<bool>.Success(c.Troll.GetComponent<TrollFarmer>() != null);
        }
    }
}
