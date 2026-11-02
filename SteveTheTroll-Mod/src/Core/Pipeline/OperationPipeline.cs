using System;
using System.Collections.Generic;
using SteveTheTroll.Core.Results;

namespace SteveTheTroll.Core.Pipeline
{
    /// <summary>
    /// Architecture V2 (D-169): input -> validation steps (in order, the first that is not valid stops the run with its status) -> the deciding step -> typed response.
    /// The executor that acts on the response is NOT part of the pipeline (the domain decides, the execution layer performs). Built once, run every frame: Run allocates nothing
    /// unless a trace sink is set (debug only). Game-free.
    /// </summary>
    internal sealed class OperationPipeline<TInput, TOutput>
    {
        readonly string _name;
        readonly List<string> _names = new List<string>();
        readonly List<Func<TInput, ValidationResult>> _checks = new List<Func<TInput, ValidationResult>>();
        readonly Func<TInput, OperationResponse<TOutput>> _decide;

        /// Trace sink (ModLog.Verbose in game); null = silent.
        internal Action<string> Trace { get; set; }

        internal OperationPipeline(string name, Func<TInput, OperationResponse<TOutput>> decide)
        {
            if (decide == null) throw new ArgumentNullException("decide");
            _name = name ?? "pipeline";
            _decide = decide;
        }

        internal OperationPipeline(string name, IOperationStep<TInput, TOutput> step) : this(name, step != null ? (Func<TInput, OperationResponse<TOutput>>)step.Run : null) { }

        internal int StepCount { get { return _checks.Count; } }

        /// Adds a validation step (runs in the order added). Expected outcome: the pipeline has one more step; returns itself for chaining.
        internal OperationPipeline<TInput, TOutput> Validate(string name, Func<TInput, ValidationResult> check)
        {
            if (check == null) throw new ArgumentNullException("check");
            _names.Add(name ?? ("check " + (_checks.Count + 1)));
            _checks.Add(check);
            return this;
        }

        /// One run. Expected outcome: the first non-valid check's status (Rejected / Ignored) and reason (the step name goes to the trace only: no allocation), else the deciding step's response.
        internal OperationResponse<TOutput> Run(TInput input)
        {
            for (int i = 0; i < _checks.Count; i++)
            {
                var v = _checks[i](input);
                if (v.IsValid) continue;
                var stop = new OperationResponse<TOutput>(v.Status, default(TOutput), v.Reason);
                if (Trace != null) Trace(_name + ": stopped at " + _names[i] + " - " + stop);
                return stop;
            }
            var r = _decide(input);
            if (Trace != null) Trace(_name + ": " + _checks.Count + " checks passed - " + r);
            return r;
        }
    }
}
