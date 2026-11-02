using Kydl.Common;
using SteveTheTroll.Trolls.Combat.Domain;
using SteveTheTroll.Trolls.Taming.Domain;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using SteveTheTroll.Trolls.Core;
using UnityEngine;

namespace SteveTheTroll.Trolls.Combat.Infrastructure
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
        int _count = SlamCount;
        bool _counter;                                  // D-084: started by TrollCounter (3 slams, then back to farming)
        const float Gap = 0.1f, LookAngle = 30f, MaxSeconds = 16f, PlayerAimRange = 15f;
        int _left = SlamCount;
        float _start, _next;
        Vector3 _aim;
        Player _towards;

        /// Expected outcome: the started slam (null when one runs already).
        internal static TrollSlam Begin(Character troll, Player towards, Vector3 crowd, int mobs, int count = SlamCount, bool counter = false)
        {
            if (troll.GetComponent<TrollSlam>() != null) return null;
            var s = troll.gameObject.AddComponent<TrollSlam>();
            s._start = Time.time;
            s._count = count; s._left = count; s._counter = counter;
            bool club = TrollWeapons.HasClub(troll as Humanoid);
            s._towards = club && towards != null && Vector3.Distance(towards.transform.position, troll.transform.position) <= PlayerAimRange ? towards : null;
            s._aim = crowd;
            ModLog.Info("TrollSlam: " + TrollTaming.Tag(troll) + " - " + mobs + " mobs on " + (towards != null ? towards.GetPlayerName() : "?") + " / the troll: " + count + " ground slams " + (club ? "towards the player" : "into the crowd"));
            return s;
        }

        /// One AI step. Expected outcome: true = the slam drove the troll (the game's AI is skipped).
        // D-175 (Architecture V2): turning goes through the troll's movement capability (TrollContext), built once
        TrollContext _ctx;

        internal bool Step(MonsterAI ai)
        {
            var me = GetComponent<Humanoid>();
            if (me == null || me.IsDead()) { Destroy(this); return false; }
            bool late = Time.time - _start > MaxSeconds;
            if (SlamRules.Done(_left, me.InAttack(), Time.time - _start, MaxSeconds))
            {
                ModLog.Info("TrollSlam: " + TrollTaming.Tag(this) + " done (" + (_count - _left) + " slam(s)" + (late ? ", time up" : "") + ")");
                if (_counter) TrollCounter.Done(me);
                Destroy(this);
                return false;
            }
            ai.StopMoving();
            var point = _towards != null ? _towards.transform.position : _aim;
            if (_ctx == null) _ctx = new TrollContext(GetComponent<Character>());
            var mv = _ctx.Capabilities.Movement;
            if (mv != null) mv.LookAt(point);
            if (!SlamRules.Ready(_left, me.InAttack(), Time.time >= _next, ai.IsLookingAt(point, LookAngle, false))) return true;
            var weapon = TrollWeapons.Pick(me, Attack.AttackType.Vertical, false);
            if (weapon == null) { ModLog.Warn("TrollSlam: " + TrollTaming.Tag(this) + " has no melee attack"); Destroy(this); return false; }
            if (!me.IsItemEquiped(weapon)) me.EquipItem(weapon, false);
            if (me.StartAttack(null, false)) { _left--; _next = Time.time + Gap; ModLog.Verbose("TrollSlam: " + TrollTaming.Tag(this) + " slam " + (_count - _left) + " / " + _count + " (" + weapon.m_shared.m_name + ")"); }
            return true;
        }
    }
}
