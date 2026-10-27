using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-063 (user: "hard to interact with the backpack, as the troll keeps moving"): a tamed troll that is not told to follow and not
    /// fighting stands still while a player is within HoldRange (4 m) - farming / walking home pause and go on when the player steps away.
    /// Checked first in the MonsterAI.UpdateAI prefix (TrollFarming.Steer).
    /// </summary>
    internal static class TrollHold
    {
        static float _nextLog;

        /// Expected outcome: true = the troll stands still this AI step.
        internal static bool Holds(MonsterAI ai)
        {
            float range = ModSettings.HoldNearPlayerRange.Value;
            if (range <= 0f || ai.GetFollowTarget() != null || ai.GetTargetCreature() != null) return false;
            var c = ai.GetComponent<Character>();
            if (c == null || !c.IsTamed() || !TrollTaming.IsTroll(c)) return false;
            var near = Player.GetClosestPlayer(c.transform.position, range);
            if (near == null) return false;
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + 10f;
                ModLog.Verbose("TrollHold: " + c.name + " stands still - " + near.GetPlayerName() + " is " + Vector3.Distance(near.transform.position, c.transform.position).ToString("F1") + " m away");
            }
            return true;
        }
    }
}
