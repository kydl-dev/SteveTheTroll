using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-065 (user: "if a lot of mobs are targeting the player / troll ... do 4 hits in fast succession to the ground and then target any mobs
    /// left. If the troll is with a club ... hits to the ground in the direction of the player"): SlamCount overhead swings as fast as the
    /// animation allows; Steve faces the player (the mobs between them get hit; the game's tamed attacks never hurt players), Spencer faces
    /// the middle of the crowd. Driven from the MonsterAI.UpdateAI prefix on the troll's owner; ends after the swings or MaxSeconds.
    /// </summary>
    internal sealed class TrollSlam : MonoBehaviour
    {
        const int SlamCount = 4;
        const float Gap = 0.1f, LookAngle = 30f, MaxSeconds = 8f;
        int _left = SlamCount;
        float _start, _next;
        Vector3 _aim;
        Player _towards;

        /// Expected outcome: the started slam (null when one runs already).
        internal static TrollSlam Begin(Character troll, Player towards, Vector3 crowd, int mobs)
        {
            if (troll.GetComponent<TrollSlam>() != null) return null;
            var s = troll.gameObject.AddComponent<TrollSlam>();
            s._start = Time.time;
            bool club = TrollWeapons.HasClub(troll as Humanoid);
            s._towards = club ? towards : null;
            s._aim = crowd;
            ModLog.Info("TrollSlam: " + troll.name + " - " + mobs + " mobs on " + (towards != null ? towards.GetPlayerName() : "?") + " / the troll: " + SlamCount + " ground slams " + (club ? "towards the player" : "into the crowd"));
            return s;
        }

        /// One AI step. Expected outcome: true = the slam drove the troll (the game's AI is skipped).
        internal bool Step(MonsterAI ai, System.Action<BaseAI, Vector3> lookAt)
        {
            var me = GetComponent<Humanoid>();
            if (me == null || me.IsDead()) { Destroy(this); return false; }
            bool late = Time.time - _start > MaxSeconds;
            if ((_left <= 0 && !me.InAttack()) || late)
            {
                ModLog.Info("TrollSlam: " + name + " done (" + (SlamCount - _left) + " slam(s)" + (late ? ", time up" : "") + ")");
                Destroy(this);
                return false;
            }
            ai.StopMoving();
            var point = _towards != null ? _towards.transform.position : _aim;
            lookAt(ai, point);
            if (_left <= 0 || me.InAttack() || Time.time < _next || !ai.IsLookingAt(point, LookAngle, false)) return true;
            var weapon = TrollWeapons.Pick(me, Attack.AttackType.Vertical, false);
            if (weapon == null) { ModLog.Warn("TrollSlam: " + name + " has no melee attack"); Destroy(this); return false; }
            if (!me.IsItemEquiped(weapon)) me.EquipItem(weapon, false);
            if (me.StartAttack(null, false)) { _left--; _next = Time.time + Gap; ModLog.Verbose("TrollSlam: " + name + " slam " + (SlamCount - _left) + " / " + SlamCount + " (" + weapon.m_shared.m_name + ")"); }
            return true;
        }
    }
}
