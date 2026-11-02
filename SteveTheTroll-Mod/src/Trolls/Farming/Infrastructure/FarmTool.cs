using Kydl.Common;
using SteveTheTroll.Trolls.Combat.Infrastructure;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Trolls.Farming.Infrastructure
{
    /// <summary>
    /// The farming troll's tool (D-061 / D-149), moved out of TrollFarmer unchanged (D-170, class size). One per farmer.
    /// A melee troll attack with chop or pickaxe damage (punch / log swing), not the throw. D-061 (user: Steve's log "went through" logs without hitting): the game's melee hit
    /// check of a HORIZONTAL swing runs at the attack height (chest), so it passes over a log lying on the ground; low targets (everything but a standing tree) get the VERTICAL
    /// swing, standing trees the horizontal one.
    /// </summary>
    internal sealed class FarmTool
    {
        const float GiveUpNoWeaponSeconds = 15f;
        readonly Component _troll;
        ItemDrop.ItemData _last;
        float _noWeaponSince = -1f;
        bool _triedDefaults;

        internal FarmTool(Component troll) { _troll = troll; }

        /// The tool for `target` (null target: the last tool, else the horizontal one). Expected outcome: null when the troll carries no fitting melee item.
        internal ItemDrop.ItemData For(Humanoid me, GameObject target)
        {
            return target != null ? Pick(me, target.GetComponent<TreeBase>() == null) : (_last ?? Pick(me, false));
        }

        /// The troll has a tool again: the no-weapon timer stops.
        internal void Found() { _noWeaponSince = -1f; }

        ItemDrop.ItemData Pick(Humanoid me, bool low)
        {
            var want = low ? Attack.AttackType.Vertical : Attack.AttackType.Horizontal;
            var best = TrollWeapons.Pick(me, want, true);
            if (best != null && best != _last)
            {
                _last = best;
                ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(_troll) + " swings " + best.m_shared.m_name + " (" + best.m_shared.m_attack.m_attackType + ", wanted " + want + " for a "
                    + (low ? "low target" : "standing tree") + ")");
            }
            return best;
        }

        /// D-149 (test run 011447: Steve 1 had no club, the farmer stayed attached for 10 minutes and the idle check saw "a job"): list what the troll carries, try the game's
        /// default items ONCE, and after GiveUpNoWeaponSeconds without a tool the farmer stops (the idle check tries again after its normal wait).
        /// Expected outcome: true = keep the farmer (it waits for a tool this frame); false = the farmer must remove itself.
        internal bool Wait(Humanoid me)
        {
            if (_noWeaponSince < 0f) _noWeaponSince = Time.time;
            if (!_triedDefaults)
            {
                _triedDefaults = true;
                var items = new System.Text.StringBuilder();
                foreach (var it in me.GetInventory().GetAllItems())
                {
                    items.Append(it.m_shared.m_name).Append(" (chop ").Append(it.m_shared.m_damages.m_chop.ToString("F0")).Append(", pickaxe ").Append(it.m_shared.m_damages.m_pickaxe.ToString("F0"))
                        .Append(", attack ").Append(it.m_shared.m_attack != null ? it.m_shared.m_attack.m_attackType.ToString() : "none").Append("); ");
                }
                ModLog.Warn("TrollFarmer: " + TrollTaming.Tag(_troll) + " has no melee item with chop / pickaxe damage - carries: " + (items.Length > 0 ? items.ToString() : "nothing") + " - asks the game for its default items");
                try { me.GiveDefaultItems(); }
                catch (System.Exception e) { ModLog.Warn("TrollFarmer: " + TrollTaming.Tag(_troll) + " GiveDefaultItems failed: " + e.GetType().Name); }       // last resort: the game's own item setup
                return _triedDefaults;
            }
            bool keep = Time.time - _noWeaponSince < GiveUpNoWeaponSeconds;
            if (!keep) ModLog.Warn("TrollFarmer: " + TrollTaming.Tag(_troll) + " still has no tool after " + GiveUpNoWeaponSeconds.ToString("0") + " s - stops farming (tries again later)");
            return keep;
        }
    }
}
