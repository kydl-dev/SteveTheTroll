using Kydl.Core.Commands;
using Kydl.Core.Features;
using Kydl.Core.Identity;
using Kydl.Core.Pipeline;
using Kydl.Core.Results;
using Kydl.Core.Time;
using System;
using SteveTheTroll.Features.Trolls.Backpack.Domain;

namespace SteveTheTroll.Features.Trolls.Backpack.Application
{
    /// <summary>
    /// D-173 (Architecture V2, P6 pickup slice): PickupInput -> validation (another client owns the troll: Rejected; a chest window is open, the backpack is full or the tick's
    /// budget is used: Ignored) -> PickupRules.ForDrop -> OperationResponse of PickupAction. Built once (static in BackpackPickup), no allocation per run. Game-free.
    /// </summary>
    internal sealed class PickupPipeline
    {
        internal const string NotOwner = "another client owns the troll", ChestOpen = "a backpack chest is open", Full = "the backpack is full", Budget = "the tick's time budget is used";

        readonly OperationPipeline<PickupInput, PickupAction> _pipeline;

        internal PickupPipeline(Action<string> trace = null)
        {
            _pipeline = new OperationPipeline<PickupInput, PickupAction>("PickupPipeline", i => OperationResponse<PickupAction>.Success(PickupRules.ForDrop(i.DropValid, i.DropOwned)))
                .Validate("troll owner", i => ValidationResult.RejectUnless(i.TrollOwner, NotOwner))
                .Validate("chest open", i => ValidationResult.IgnoreUnless(!i.ChestOpen, ChestOpen))
                .Validate("full", i => ValidationResult.IgnoreUnless(!i.Full, Full))
                .Validate("budget", i => ValidationResult.IgnoreUnless(!i.BudgetUsed, Budget));
            _pipeline.Trace = trace;
        }

        /// Expected outcome: Success with the action for this drop; Rejected / Ignored with the reason when the troll does not collect now.
        internal OperationResponse<PickupAction> Evaluate(PickupInput input) { return _pipeline.Run(input); }
    }
}
