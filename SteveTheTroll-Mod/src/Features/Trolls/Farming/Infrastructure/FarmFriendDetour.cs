using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Farming.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Farming.Infrastructure;

/// <summary>
///     D-151 (user: "there is a friendly troll right in front of me, let's re-route next to it"): every 0.25 s the own
///     tamed trolls are checked; one standing on the line to the
///     target, in front, makes this troll walk to a point beside it for DetourSeconds, on the side away from the line (two
///     trolls head on pass each other). One per farmer.
///     D-170: moved out of TrollFarmer unchanged (class size).
/// </summary>
internal sealed class FarmFriendDetour
{
    // 2026-11-05: two legs (TargetRules.SideStep, then Waypoint), each at most DetourSeconds; a leg ends early within ArriveMetres of its point
    private const float DetourSeconds = 1.5f, DetourCheckSeconds = 0.25f, LogSeconds = 10f, ArriveMetres = 0.8f;
    private int _detours;
    private Vector3 _detourTo, _secondLeg;
    private bool _onFirstLeg;
    private float _detourUntil, _nextCheck, _nextLog, _nextNearLog;

    /// Expected outcome: true = walk to `around` this frame (a friendly troll stands in the way, or the detour still runs).
    internal bool Check(GameObject self, Vector3 goal, string targetName, out Vector3 around)
    {
        around = goal;
        if (Time.time < _detourUntil)
        {
            Vector3 left = _detourTo - self.transform.position;
            left.y = 0f;
            if (_onFirstLeg && left.magnitude <= ArriveMetres)
            {
                _onFirstLeg = false; // sideways step done: now parallel past the friend
                _detourTo = _secondLeg;
                _detourUntil = Time.time + DetourSeconds;
            }
            else if (!_onFirstLeg && left.magnitude <= ArriveMetres)
            {
                _detourUntil = 0f; // past the friend: straight on to the goal
                return false;
            }

            around = _detourTo;
            return true;
        }

        if (_onFirstLeg)
        {
            // the sideways leg ran out of time before it arrived: the second leg still follows, so the walker never turns back into the friend
            _onFirstLeg = false;
            _detourTo = _secondLeg;
            _detourUntil = Time.time + DetourSeconds;
            around = _detourTo;
            return true;
        }

        if (Time.time < _nextCheck)
        {
            return Time.time >= _nextCheck;
        }

        _nextCheck = Time.time + DetourCheckSeconds;
        Vector3 p = self.transform.position;
        foreach (Character c in Character.GetAllCharacters())
        {
            if (c == null || c.gameObject == self || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c))
            {
                continue;
            }

            Vector3 o = c.transform.position;
            var me = new GroundPoint(p.x, p.z);
            var goalPoint = new GroundPoint(goal.x, goal.z);
            var other = new GroundPoint(o.x, o.z);
            bool blocked = TargetRules.Blocked(me, goalPoint, other, TargetRules.Ahead, TargetRules.HalfWidth);
            Near(self, c, p, goal, o, blocked);
            if (!blocked)
            {
                continue;
            }

            GroundPoint step = TargetRules.SideStep(me, goalPoint, other, TargetRules.Side);
            GroundPoint way = TargetRules.Waypoint(me, goalPoint, other, TargetRules.Side);
            _detourTo = new Vector3(step.X, p.y, step.Z);
            _secondLeg = new Vector3(way.X, p.y, way.Z);
            _onFirstLeg = true;
            _detourUntil = Time.time + DetourSeconds;
            _detours++;
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + LogSeconds;
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(self.transform) + " steps around " + TrollTaming.Tag(c) +
                            " standing " + new Vector2(o.x - p.x, o.z - p.z).magnitude.ToString("F1") +
                            " m ahead on the way to "
                            + targetName + " - sideways " + TargetRules.Side.ToString("0") + " m, then past it (" + _detours + " detour(s) so far)");
            }

            around = _detourTo;
            return around == _detourTo;
        }

        return around != goal;
    }

    /// D-178 (fix11 farm-friend-detour: the farmer passed a friend standing on its line 7 m wide and this never fired - nothing said where the friend was): once a second,
    /// a tamed troll within 6 m is logged with how far ahead (along the line to the goal) and how far beside it stands, and whether that counts as in the way.
    private void Near(GameObject self, Character c, Vector3 p, Vector3 goal, Vector3 o, bool blocked)
    {
        Vector3 rel = o - p;
        rel.y = 0f;
        if (rel.magnitude > 6f || Time.time < _nextNearLog)
        {
            return;
        }

        _nextNearLog = Time.time + 1f;
        Vector3 line = goal - p;
        line.y = 0f;
        float len = line.magnitude;
        Vector3 dir = len > 0.01f ? line / len : Vector3.forward;
        float along = Vector3.Dot(rel, dir), beside = dir.x * rel.z - dir.z * rel.x;
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(self.transform) + " near " + TrollTaming.Tag(c) + " - " + along.ToString("F1") + " m ahead, " + beside.ToString("F1") +
                    " m beside the line to the goal (" + len.ToString("F1") + " m away; in the way when 0 .. " + TargetRules.Ahead + " m ahead and within " + TargetRules.HalfWidth +
                    " m beside): " + (blocked ? "IN THE WAY" : "not in the way"));
    }
}
