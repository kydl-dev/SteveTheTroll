using Kydl.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Home.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Home.Infrastructure
{
    /// <summary>
    /// D-063 (user: "hard to interact with the backpack, as the troll keeps moving"): a tamed troll that is not told to follow and not
    /// fighting stands still while the local player within HoldRange (4 m) looks at it or its backpack (D-077; + 3 s) - farming / walking home
    /// pause and go on when the player looks away / steps away.
    /// Checked first in the MonsterAI.UpdateAI prefix (TrollFarming.Steer).
    /// </summary>
    internal static class TrollHold
    {
        static float _nextLog;
        const float HoldGraceSeconds = 3f;
        static readonly System.Collections.Generic.Dictionary<int, float> LastLook = new System.Collections.Generic.Dictionary<int, float>();

        /// Expected outcome: true = the troll stands still this AI step.
        internal static bool Holds(MonsterAI ai)
        {
            float range = ModSettings.HoldNearPlayerRange.Value;
            if (range <= 0f || ai.GetFollowTarget() != null || ai.GetTargetCreature() != null) return range < 0f;
            var c = ai.GetComponent<Character>();
            if (c == null || !c.IsTamed() || !TrollTaming.IsTroll(c)) return false;
            // D-077 (TEST-032: Spencer stood at the bed for minutes while the player was near): only while the local player within
            // range LOOKS at the troll or its backpack (hover), + HoldGraceSeconds after that - not just for standing near it
            var near = Player.m_localPlayer;
            if (near == null || Vector3.Distance(near.transform.position, c.transform.position) > range) return false;
            var hover = near.GetHoverObject();
            int id = c.GetInstanceID();
            if (LastLook.Count > 200) LastLook.Clear();          // D-171 (P9 audit): entries of removed trolls were never dropped
            if (hover != null && hover.transform.IsChildOf(c.transform)) LastLook[id] = Time.time;
            float seen;
            bool looked = LastLook.TryGetValue(id, out seen);
            // D-171: the decision is game-free (range / follow / fight were checked above to skip the work; HomeRules repeats them for the record)
            if (!HomeRules.Holds(range, false, false, Vector3.Distance(near.transform.position, c.transform.position), looked, Time.time - seen, HoldGraceSeconds)) return looked && !looked;
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + 10f;
                ModLog.Verbose("TrollHold: " + TrollTaming.Tag(c) + " stands still - " + near.GetPlayerName() + " looks at it, " + Vector3.Distance(near.transform.position, c.transform.position).ToString("F1") + " m away");
            }
            return true;
        }
    }
}
