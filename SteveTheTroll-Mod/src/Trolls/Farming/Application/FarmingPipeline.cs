using System;
using SteveTheTroll.Core.Pipeline;
using SteveTheTroll.Core.Results;
using SteveTheTroll.Trolls.Farming.Domain;

namespace SteveTheTroll.Trolls.Farming.Application
{
    /// <summary>
    /// D-170, farming slice: FarmGateInput -> validation (this client owns the troll: else Rejected, the game's AI runs) -> FarmGateRules -> OperationResponse of FarmAction.
    /// One per farmer, built once. Game-free.
    /// </summary>
    internal sealed class FarmingPipeline
    {
        internal const string NotOwner = "another client owns the troll";

        readonly OperationPipeline<FarmGateInput, FarmAction> _pipeline;

        internal FarmingPipeline(Action<string> trace = null)
        {
            _pipeline = new OperationPipeline<FarmGateInput, FarmAction>("FarmingPipeline", i => OperationResponse<FarmAction>.Success(FarmGateRules.Decide(i)))
                .Validate("owner", i => ValidationResult.RejectUnless(i.IsOwner, NotOwner));
            _pipeline.Trace = trace;
        }

        /// Expected outcome: Success with the gate's action, Rejected when another client owns the troll.
        internal OperationResponse<FarmAction> Evaluate(FarmGateInput input) { return _pipeline.Run(input); }
    }
}
