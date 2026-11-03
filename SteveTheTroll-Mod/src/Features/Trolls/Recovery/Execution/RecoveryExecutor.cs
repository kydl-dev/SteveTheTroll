using Kydl.Core.Commands;
using Kydl.Core.Features;
using Kydl.Core.Identity;
using Kydl.Core.Pipeline;
using Kydl.Core.Results;
using Kydl.Core.Time;
using System;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Recovery.Domain;

namespace SteveTheTroll.Features.Trolls.Recovery.Execution
{
    /// <summary>
    /// Architecture V2 (D-169), recovery slice: turns a RecoveryDecision into the one side effect it stands for. It owns the delegates (bound once by TrollRecovery to its own
    /// methods: no closure per frame), the decision only carries data. Ownership is checked HERE, at the execution boundary: a client that does not own the troll performs nothing.
    /// Game-free (the delegates do the game work), so the mapping and the ownership gate are unit-tested.
    /// </summary>
    internal sealed class RecoveryExecutor
    {
        internal const string NothingToDo = "no command", NotOwner = "not the owner - nothing performed";

        readonly ITrollOwnership _owner;
        readonly Action _startRage, _endRage, _endLeg, _cycleFailed, _cycleReset;
        readonly Action<RecoveryDecision> _startLeg;

        internal RecoveryExecutor(ITrollOwnership owner, Action startRage, Action endRage, Action<RecoveryDecision> startLeg, Action endLeg, Action cycleFailed, Action cycleReset)
        {
            _owner = owner;
            _startRage = startRage;
            _endRage = endRage;
            _startLeg = startLeg;
            _endLeg = endLeg;
            _cycleFailed = cycleFailed;
            _cycleReset = cycleReset;
        }

        /// Expected outcome: Success(command) when its action ran; Ignored for None (or a command without an action); Rejected when this client is not the owner (nothing ran).
        /// Success means the action was ISSUED (a leg was started), not that the troll got anywhere.
        internal OperationResponse<RecoveryCmd> Execute(RecoveryDecision decision)
        {
            if (decision.IsNone) return OperationResponse<RecoveryCmd>.Ignored(NothingToDo);
            if (_owner == null || !_owner.IsOwner) return OperationResponse<RecoveryCmd>.Rejected(NotOwner);
            bool ran = Run(decision);
            return ran ? OperationResponse<RecoveryCmd>.Success(decision.Command) : OperationResponse<RecoveryCmd>.Ignored("no action bound for " + decision.Command);
        }

        bool Run(RecoveryDecision d)
        {
            switch (d.Command)
            {
                case RecoveryCmd.StartRage: return Invoke(_startRage);
                case RecoveryCmd.EndRage: return Invoke(_endRage);
                case RecoveryCmd.StartLeg:
                    if (_startLeg == null) return _startLeg != null;
                    _startLeg(d);
                    return _startLeg != null;
                case RecoveryCmd.EndLeg: return Invoke(_endLeg);
                case RecoveryCmd.CycleFailed: return Invoke(_cycleFailed);
                case RecoveryCmd.CycleReset: return Invoke(_cycleReset);
                default: return d.Command == RecoveryCmd.None;
            }
        }

        static bool Invoke(Action a)
        {
            if (a != null) a();
            return a != null;
        }
    }
}
