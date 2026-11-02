using System;
using SteveTheTroll.Core.Pipeline;
using SteveTheTroll.Core.Results;
using SteveTheTroll.Trolls.Recovery.Domain;

namespace SteveTheTroll.Trolls.Recovery.Application
{
    /// <summary>
    /// Architecture V2 (D-169), recovery slice: RecoveryInput -> validation (troll alive: else Ignored; this client owns it: else Rejected) -> RecoveryMachine.Step ->
    /// OperationResponse of RecoveryDecision. Success with Command None = nothing to do this frame. One pipeline per troll, built once (no allocation per frame). Game-free.
    /// </summary>
    internal sealed class RecoveryPipeline
    {
        internal const string NotAlive = "the troll is dead or gone", NotOwner = "another client owns the troll";

        readonly RecoveryMachine _machine = new RecoveryMachine();
        readonly OperationPipeline<RecoveryInput, RecoveryDecision> _pipeline;

        internal RecoveryPipeline(Action<string> trace = null)
        {
            _pipeline = new OperationPipeline<RecoveryInput, RecoveryDecision>("RecoveryPipeline", Decide)
                .Validate("alive", i => ValidationResult.IgnoreUnless(i.IsAlive, NotAlive))
                .Validate("owner", i => ValidationResult.RejectUnless(i.IsOwner, NotOwner));
            _pipeline.Trace = trace;
        }

        /// The ladder's state (read by the adapter for logs and leg planning; changed only through BeginCycle / Abort / Evaluate).
        internal RecoveryMachine Machine { get { return _machine; } }
        internal RecoveryStage Stage { get { return _machine.Stage; } }

        /// One frame. Expected outcome: Success with the decision (Command None most frames); Ignored for a dead troll, Rejected when another client owns it (the machine is untouched).
        internal OperationResponse<RecoveryDecision> Evaluate(RecoveryInput input) { return _pipeline.Run(input); }

        /// A new cycle after `failedCycles` failures (even: right first). Expected outcome: Stage Travel.
        internal RecoveryStage BeginCycle(int failedCycles) { _machine.BeginCycle(failedCycles); return _machine.Stage; }

        /// A running manoeuvre is cut short. Expected outcome: Stage Travel.
        internal RecoveryStage Abort() { _machine.Abort(); return _machine.Stage; }

        OperationResponse<RecoveryDecision> Decide(RecoveryInput i)
        {
            var cmd = _machine.Step(i.Now, i.StuckSeconds, i.SustainedSeconds, i.Rageable, i.HeadOn, i.LegDone);
            return OperationResponse<RecoveryDecision>.Success(RecoveryDecision.Of(cmd, _machine));
        }
    }
}
