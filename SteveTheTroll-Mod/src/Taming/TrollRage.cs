using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Trolls.Farming.Infrastructure;
using SteveTheTroll.Trolls.Orders.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-082 enrage (user: "if I am within 10m of them and attacked then they should go in enrage mode and hit everything and anything in their
    /// path until I am no longer attacked (no ... finding target, attack continuously until I am no longer attacked)"): when the player is hit,
    /// every own tamed troll within RageRange of the player (steered by this client) swings without pause - wide horizontal swings at the
    /// nearest hostile creature around it (running to it when out of reach; at the player's side when none is left) - until the player has
    /// not been hit for RageCalmSeconds. The game's AI is skipped meanwhile (MonsterAI.UpdateAI prefix, TrollFarming.Steer).
    /// </summary>
    internal sealed class TrollRage : MonoBehaviour
    {
        const float SeekRange = 15f, Gap = 0.05f, LookAngle = 40f, LogSeconds = 2f, WeaponReach = 2.5f;
        static readonly Dictionary<long, float> HitAt = new Dictionary<long, float>();
        static readonly Dictionary<long, Character> Attacker = new Dictionary<long, Character>();
        long _owner;
        float _start, _next, _nextLog;
        int _swings;

        /// The player was hit (TrollDefend.PlayerHit, the player's client). Expected outcome: how many trolls are enraged now.
        internal static int OwnerHit(Player owner, Character attacker)
        {
            if (owner == null || !ModSettings.On || ModSettings.RageRange.Value <= 0f) return 0;
            long id = owner.GetPlayerID();
            HitAt[id] = Time.time;
            if (attacker != null) Attacker[id] = attacker;
            int n = 0;
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || !TamedTrollStats.IsTamedTroll(c) || TrollTaming.Owner(c) != id) continue;
                if (Vector3.Distance(c.transform.position, owner.transform.position) > ModSettings.RageRange.Value) continue;
                var view = c.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner()) continue;
                var r = c.GetComponent<TrollRage>();
                if (r == null)
                {
                    r = c.gameObject.AddComponent<TrollRage>();
                    r._owner = id; r._start = Time.time;
                    ModLog.Info("TrollRage: " + TrollOrders.Shown(c) + " is ENRAGED - " + owner.GetPlayerName() + " was hit by " + (attacker != null ? attacker.name : "?") + " " + Vector3.Distance(c.transform.position, owner.transform.position).ToString("F1") + " m away");
                }
                n++;
            }
            return n;
        }

        /// One AI step. Expected outcome: true = the rage drove the troll (the game's AI is skipped); false = it calmed down.
        internal bool Step(MonsterAI ai, float dt, System.Func<BaseAI, float, Vector3, float, bool, bool> moveTo, System.Action<BaseAI, Vector3> lookAt)
        {
            var me = GetComponent<Humanoid>();
            float hit;
            if (me == null || me.IsDead() || !HitAt.TryGetValue(_owner, out hit) || Time.time - hit > ModSettings.RageCalmSeconds.Value)
            {
                if (me != null) ModLog.Info("TrollRage: " + TrollOrders.Shown(me) + " calms down after " + (Time.time - _start).ToString("F0") + " s, " + _swings + " swing(s)");
                Destroy(this);
                return false;
            }
            Player owner = null;
            foreach (var p in Player.GetAllPlayers()) if (p != null && p.GetPlayerID() == _owner) owner = p;
            var target = Nearest(me, owner);
            Vector3 point = target != null ? target.transform.position : (owner != null ? owner.transform.position : transform.position + transform.forward);
            var flat = point - transform.position; flat.y = 0f;
            float d = flat.magnitude;
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + LogSeconds;
                ModLog.Verbose("TrollRage: " + TrollTaming.Tag(this) + " -> " + (target != null ? target.name + " " + d.ToString("F1") + " m" : "no creature near - stays by the player, swinging") + ", " + _swings + " swing(s)");
            }
            // D-084 (TEST-036: 0 swings - a wild troll's centre stays 5-9 m away, the old 3.5 m reach was never met): reach = both body radii + the weapon
            float reach = target != null ? ReachTo(me, target) : 2.5f;
            if (d > reach) moveTo(ai, dt, point, reach * 0.8f, true);
            else ai.StopMoving();
            lookAt(ai, point);
            if (me.InAttack() || Time.time < _next || d > reach + 1f) return true;
            if (target != null && !ai.IsLookingAt(point, LookAngle, false)) return true;
            var weapon = TrollWeapons.Pick(me, Attack.AttackType.Horizontal, false) ?? TrollWeapons.Pick(me, Attack.AttackType.Vertical, false);
            if (weapon == null) return true;
            if (!me.IsItemEquiped(weapon)) me.EquipItem(weapon, false);
            if (me.StartAttack(target, false)) { _swings++; _next = Time.time + Gap; }
            return true;
        }

        /// D-084: centre-to-centre distance from which the troll's swing reaches the other creature (both body radii + WeaponReach).
        internal static float ReachTo(Character me, Character other)
        {
            return (me != null ? me.GetRadius() : 1f) + (other != null ? other.GetRadius() : 0.5f) + WeaponReach;
        }

        // The nearest living hostile creature within SeekRange of the troll; the player's last attacker when nothing else is near.
        Character Nearest(Character me, Player owner)
        {
            Character best = null;
            float bestD = SeekRange * SeekRange;
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c == me || c.IsDead() || c.IsPlayer() || c.IsTamed()) continue;
                if (!BaseAI.IsEnemy(me, c) && (c.GetComponent<MonsterAI>() == null || (c.GetComponent<MonsterAI>().GetTargetCreature() != owner && c.GetComponent<MonsterAI>().GetTargetCreature() != me))) continue;
                float d = (c.transform.position - me.transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            Character last;
            if (best == null && Attacker.TryGetValue(_owner, out last) && last != null && !last.IsDead()) best = last;
            return best;
        }
    }
}
