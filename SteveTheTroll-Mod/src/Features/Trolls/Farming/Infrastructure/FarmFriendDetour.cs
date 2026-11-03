using Kydl.Common;
using SteveTheTroll.Features.Trolls.Farming.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Farming.Infrastructure
{
    /// <summary>
    /// D-151 (user: "there is a friendly troll right in front of me, let's re-route next to it"): every 0.25 s the own tamed trolls are checked; one standing on the line to the
    /// target, in front, makes this troll walk to a point beside it for DetourSeconds, on the side away from the line (two trolls head on pass each other). One per farmer.
    /// D-170: moved out of TrollFarmer unchanged (class size).
    /// </summary>
    internal sealed class FarmFriendDetour
    {
        const float DetourSeconds = 1.2f, DetourCheckSeconds = 0.25f, LogSeconds = 10f;
        float _detourUntil, _nextCheck, _nextLog;
        Vector3 _detourTo;
        int _detours;

        /// Expected outcome: true = walk to `around` this frame (a friendly troll stands in the way, or the detour still runs).
        internal bool Check(GameObject self, Vector3 goal, string targetName, out Vector3 around)
        {
            around = goal;
            if (Time.time < _detourUntil) { around = _detourTo; return Time.time < _detourUntil; }
            if (Time.time < _nextCheck) return Time.time >= _nextCheck;
            _nextCheck = Time.time + DetourCheckSeconds;
            var p = self.transform.position;
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.gameObject == self || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c)) continue;
                var o = c.transform.position;
                if (!TargetRules.Blocked(p.x, p.z, goal.x, goal.z, o.x, o.z, TargetRules.Ahead, TargetRules.HalfWidth)) continue;
                float wx, wz;
                TargetRules.Waypoint(p.x, p.z, goal.x, goal.z, o.x, o.z, TargetRules.Side, out wx, out wz);
                _detourTo = new Vector3(wx, p.y, wz);
                _detourUntil = Time.time + DetourSeconds;
                _detours++;
                if (Time.time >= _nextLog)
                {
                    _nextLog = Time.time + LogSeconds;
                    ModLog.Info("TrollFarmer: " + TrollTaming.Tag(self.transform) + " steps around " + TrollTaming.Tag(c) + " standing " + new Vector2(o.x - p.x, o.z - p.z).magnitude.ToString("F1") + " m ahead on the way to "
                        + targetName + " (" + _detours + " detour(s) so far)");
                }
                around = _detourTo;
                return around == _detourTo;
            }
            return around != goal;
        }
    }
}
