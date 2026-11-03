using System;
using Kydl.Core.Pipeline;
using Kydl.Core.Results;
using SteveTheTroll.Trolls.Combat.Domain;

namespace SteveTheTroll.Trolls.Combat.Application
{
    /// <summary>
    /// D-175 (Architecture V2, P5 combat slice): DefenceInput -> validation (no slippers and no raid: Ignored; too far from the owner: Ignored; another client owns the troll:
    /// Rejected) -> Success(true) = take the attacker on. One pipeline for TrollDefend (owner hit) and TrollGuard (a creature goes for the owner). Game-free.
    /// </summary>
    internal sealed class DefencePipeline
    {
        readonly OperationPipeline<DefenceInput, bool> _pipeline;

        internal DefencePipeline(Action<string> trace = null)
        {
            _pipeline = new OperationPipeline<DefenceInput, bool>("DefencePipeline", i => OperationResponse<bool>.Success(true))
                .Validate("slippers or raid", i => ValidationResult.IgnoreUnless(i.OwnerWears || i.Raid, DefencePolicy.NoSlippers))
                .Validate("near the owner", i => ValidationResult.IgnoreUnless(i.ToOwner <= i.Range, DefencePolicy.TooFar))
                .Validate("troll owner", i => ValidationResult.RejectUnless(i.TrollOwner, DefencePolicy.NotOwner));
            _pipeline.Trace = trace;
        }

        internal OperationResponse<bool> Evaluate(DefenceInput input) { return _pipeline.Run(input); }
    }
}
