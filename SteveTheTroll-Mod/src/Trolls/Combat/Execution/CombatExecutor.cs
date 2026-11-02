using System;
using SteveTheTroll.Core.Results;
using SteveTheTroll.Trolls.Contracts;

namespace SteveTheTroll.Trolls.Combat.Execution
{
    /// <summary>
    /// D-175 (Architecture V2, P5 combat slice): performs "attack this creature" for the troll / creature the adapter is working on. `forceTarget` = TrollGuard.Order (the
    /// FORCED target TrollGuard.Keep puts back before every AI step, D-082); when that refuses, `setTarget` = the game's SetTarget + Alert. Ownership of the troll is checked
    /// HERE. The delegates are bound once (no closure per call). Game-free, unit-tested.
    /// </summary>
    internal sealed class CombatExecutor
    {
        internal const string NotOwner = "not the troll's owner - nothing performed", Forced = "forced target", Plain = "target set";

        readonly Func<bool> _forceTarget;
        readonly Action _setTarget;

        internal CombatExecutor(Func<bool> forceTarget, Action setTarget) { _forceTarget = forceTarget; _setTarget = setTarget; }

        /// Expected outcome: Success("forced target" / "target set") when issued, Rejected when this client does not own the troll (nothing ran).
        internal OperationResponse<string> Attack(ITrollOwnership troll)
        {
            if (troll == null || !troll.IsOwner) return OperationResponse<string>.Rejected(NotOwner);
            if (_forceTarget != null && _forceTarget()) return OperationResponse<string>.Success(Forced);
            if (_setTarget != null) _setTarget();
            return OperationResponse<string>.Success(Plain);
        }
    }
}
