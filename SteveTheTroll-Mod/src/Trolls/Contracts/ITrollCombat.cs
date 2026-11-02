using UnityEngine;

namespace SteveTheTroll.Trolls.Contracts
{
    /// <summary>Architecture V2 (D-169): what a troll's fighting body can do (attack animations, stagger, one swing at a target). Decisions about WHAT to hit live elsewhere.</summary>
    internal interface ITrollCombat
    {
        /// An attack animation runs (movement is not expected meanwhile).
        bool InAttack { get; }
        bool IsStaggering { get; }
        bool IsDead { get; }
        /// One swing at `target` with the troll's own weapon (log / fists), a downward swing for a `low` target. Expected outcome: whether the game started the attack.
        bool SwingAt(GameObject target, bool low);
    }
}
