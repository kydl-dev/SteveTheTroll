using Kydl.Core.Commands;
using Kydl.Core.Features;
using Kydl.Core.Identity;
using Kydl.Core.Pipeline;
using Kydl.Core.Results;
using Kydl.Core.Time;
using System;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Contracts;

namespace SteveTheTroll.Features.Trolls.Backpack.Execution
{
    /// <summary>
    /// D-173 (Architecture V2, P6 pickup slice): performs a PickupAction on the drop the adapter is working on. The delegates are bound once by BackpackPickup to its own
    /// methods (they act on its current drop: no closure per item). Ownership of the TROLL is checked here, at the execution boundary. Game-free, unit-tested.
    /// </summary>
    internal sealed class PickupExecutor
    {
        internal const string NotOwner = "not the troll's owner - nothing performed", Skipped = "nothing to do", NoRoom = "no room in any chest";

        readonly Action _requestOwnership;
        readonly Func<bool> _take;

        /// `take` puts the current drop into the backpack and removes it from the world; false = no chest had room.
        internal PickupExecutor(Action requestOwnership, Func<bool> take)
        {
            _requestOwnership = requestOwnership;
            _take = take;
        }

        /// Expected outcome: Success(Take) when the item went in, Rejected(NoRoom) when no chest had room (the caller stops for this troll), Success(RequestOwnership) when asked,
        /// Ignored for Skip, Rejected(NotOwner) when this client does not own the troll (nothing ran).
        internal OperationResponse<PickupAction> Execute(ITrollOwnership troll, PickupAction action)
        {
            if (action == PickupAction.Skip) return OperationResponse<PickupAction>.Ignored(Skipped);
            if (troll == null || !troll.IsOwner) return OperationResponse<PickupAction>.Rejected(NotOwner);
            if (action == PickupAction.RequestOwnership)
            {
                if (_requestOwnership != null) _requestOwnership();
                return OperationResponse<PickupAction>.Success(action);
            }
            bool taken = _take != null && _take();
            return taken ? OperationResponse<PickupAction>.Success(action) : OperationResponse<PickupAction>.Rejected(NoRoom);
        }
    }
}
