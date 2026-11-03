using Kydl.Common;
using SteveTheTroll.Trolls.Contracts;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Trolls.Combat.Infrastructure
{
    /// <summary>Architecture V2 (D-169): ITrollCombat on the game's Humanoid - the troll's own weapons (TrollWeapons.Pick), equip, StartAttack.</summary>
    internal sealed class ValheimTrollCombat : ITrollCombat
    {
        readonly Humanoid _me;

        internal ValheimTrollCombat(Humanoid me) { _me = me; }

        public bool InAttack { get { return _me.InAttack(); } }
        public bool IsStaggering { get { return _me.IsStaggering(); } }
        public bool IsDead { get { return _me == null || _me.IsDead(); } }

        /// Same weapon choice as D-167 rage: a vertical swing for a low target (horizontal as fall-back), else horizontal (vertical as fall-back).
        public bool SwingAt(GameObject target, bool low)
        {
            if (target == null) return target != null;
            var weapon = TrollWeapons.Pick(_me, low ? Attack.AttackType.Vertical : Attack.AttackType.Horizontal, true)
                ?? TrollWeapons.Pick(_me, low ? Attack.AttackType.Horizontal : Attack.AttackType.Vertical, false);
            if (weapon == null)
            {
                ModLog.Verbose("ValheimTrollCombat: " + TrollTaming.Tag(_me) + " has no weapon for a " + (low ? "low" : "high") + " swing at " + target.name);
                return weapon != null;
            }
            if (!_me.IsItemEquiped(weapon)) _me.EquipItem(weapon, false);
            bool started = _me.StartAttack(target.GetComponentInParent<Character>(), false);
            ModLog.Verbose("ValheimTrollCombat: " + TrollTaming.Tag(_me) + " swings (" + (low ? "low" : "high") + ") at " + target.name + " - started " + started);
            return started;
        }
    }
}
