using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// T-008d step 1: the farming state of one tamed troll (owner client). Each AI step: no target -> search (every SearchSeconds, focus item
    /// or the D-038 priority, around the farm centre then the owner's bed); far -> walk; close -> face it and attack with a melee item that
    /// has chop / pickaxe damage. A follow order or a dead / missing troll AI ends farming; a fight (target creature) pauses it.
    /// </summary>
    internal sealed class TrollFarmer : MonoBehaviour
    {
        const float SearchSeconds = 3f, LogSeconds = 5f, LookAngle = 25f, MinAttackGap = 0.3f, StuckSeconds = 8f;
        const int MaxMisses = 4;
        int _missed;
        uint _lastRevision;
        float _stuckSeconds, _lastDist = float.MaxValue;
        GameObject _bad;
        string _focus = "";
        Vector3 _centre;
        GameObject _target;
        Collider _hit;
        float _nextSearch, _nextAttack, _nextLog;
        int _attacks;

        internal string CurrentItem { get; private set; } = "";

        internal void Setup(string focus, Vector3 centre)
        {
            _focus = focus ?? ""; _centre = centre; _target = null; _hit = null; _nextSearch = 0f; CurrentItem = "";
        }

        /// One AI step. Expected outcome: returns whether farming drove the troll this frame (true = the game's AI is skipped: no idling,
        /// no feeding animation, D-048); false = fighting / nothing to farm / done -> the game's AI runs.
        internal bool Step(MonsterAI ai, float dt, System.Func<BaseAI, float, Vector3, float, bool, bool> moveTo, System.Action<BaseAI, Vector3> lookAt)
        {
            var me = GetComponent<Humanoid>();
            if (me == null || me.IsDead()) { Destroy(this); return me != null && !me.IsDead(); }
            if (ai.GetFollowTarget() != null) { ModLog.Info("TrollFarmer: " + name + " got a follow order - stops farming"); Destroy(this); return ai.GetFollowTarget() == null; }
            if (ai.GetTargetCreature() != null) return ai.GetTargetCreature() == null;      // fighting (D-042 defend): the game's AI drives
            var weapon = Weapon(me);
            if (weapon == null) { if (Time.time >= _nextLog) { _nextLog = Time.time + LogSeconds; ModLog.Warn("TrollFarmer: " + name + " has no melee item with chop / pickaxe damage"); } return weapon != null; }
            if ((_target == null || _hit == null) && Time.time >= _nextSearch) Search(me, weapon);
            if (_target == null || _hit == null) return _target != null;

            var point = _hit.bounds.ClosestPoint(transform.position);      // bounds: Collider.ClosestPoint is wrong on non-convex meshes (TEST-018)
            point.y = Mathf.Clamp(transform.position.y + 1.5f, _hit.bounds.min.y, _hit.bounds.max.y);   // hit the trunk / rock at chest height, not the ground
            float d = Vector3.Distance(transform.position, point);
            float reach = Mathf.Max(1.5f, weapon.m_shared.m_aiAttackRange);
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + LogSeconds;
                ModLog.Verbose("TrollFarmer: " + name + " -> " + _target.name + " (" + TrollFarming.KindOf(_target) + ", " + CurrentItem + ") " + d.ToString("F1") + " m, reach " + reach.ToString("F1")
                    + " m, height diff " + (point.y - transform.position.y).ToString("F1") + " m, weapon " + weapon.m_shared.m_name + ", attacks " + _attacks + " (" + _missed + " without damage), stuck " + _stuckSeconds.ToString("F0") + " s");
            }
            if (d > reach)
            {
                // no progress for StuckSeconds -> give this source up and look for another one
                if (d < _lastDist - 0.2f) { _lastDist = d; _stuckSeconds = 0f; } else _stuckSeconds += dt;
                if (_stuckSeconds > StuckSeconds) { GiveUp("cannot reach it (" + d.ToString("F1") + " m)"); return _target != null; }
                moveTo(ai, dt, point, reach * 0.8f, false);
                return true;
            }
            _stuckSeconds = 0f; _lastDist = float.MaxValue;
            ai.StopMoving();
            lookAt(ai, point);
            if (me.InAttack() || Time.time < _nextAttack || !ai.IsLookingAt(point, LookAngle, false)) return true;
            // TEST-019: a birch in a valley was swung at but never hit -> the source's ZDO must change after a few swings, else give up
            uint rev = Revision();
            if (_attacks > 0 && rev == _lastRevision) _missed++; else _missed = 0;
            _lastRevision = rev;
            if (_missed >= MaxMisses) { GiveUp(_missed + " swings without damage (height diff " + (point.y - transform.position.y).ToString("F1") + " m)"); return _target != null; }
            if (!me.IsItemEquiped(weapon)) me.EquipItem(weapon, false);
            if (me.StartAttack(null, false))
            {
                _attacks++;
                _nextAttack = Time.time + MinAttackGap;       // D-048: keep swinging (the game's AI interval of 4-6 s is not used while farming)
            }
            return true;
        }

        uint Revision()
        {
            var v = _target != null ? _target.GetComponent<ZNetView>() : null;
            var z = v != null && v.IsValid() ? v.GetZDO() : null;
            return z != null ? z.DataRevision : 0u;
        }

        void GiveUp(string why)
        {
            ModLog.Info("TrollFarmer: " + name + " gives up " + (_target != null ? _target.name : "?") + ": " + why + " - tries another source");
            _bad = _target; _target = null; _hit = null; _stuckSeconds = 0f; _lastDist = float.MaxValue; _missed = 0; _lastRevision = 0u;
        }

        void Search(Humanoid me, ItemDrop.ItemData weapon)
        {
            _nextSearch = Time.time + SearchSeconds;
            int tier = weapon.m_shared.m_toolTier;
            float range = ModSettings.FarmRange.Value;
            var items = _focus.Length > 0 ? new[] { _focus } : TrollFarming.Priority;
            float bed = TrollRegistry.BedDistance(transform.position);
            foreach (var item in items)
            {
                _target = TrollFarming.Find(_centre, transform.position, item, tier, range, out _hit, _bad);
                if (_target == null && bed >= 0f && Game.instance != null) _target = TrollFarming.Find(Game.instance.GetPlayerProfile().GetCustomSpawnPoint(), transform.position, item, tier, range, out _hit, _bad);
                if (_target != null) { CurrentItem = item; ModLog.Info("TrollFarmer: " + name + " farms " + item + " from " + _target.name + " (" + TrollFarming.KindOf(_target) + ", tier " + tier + ")"); return; }
            }
            CurrentItem = "";
            ModLog.Verbose("TrollFarmer: " + name + " finds nothing to farm ('" + (_focus.Length > 0 ? _focus : "priority") + "') within " + range + " m - waits");
        }

        // A melee troll attack with chop or pickaxe damage (punch / log swing), not the throw.
        static ItemDrop.ItemData Weapon(Humanoid me)
        {
            ItemDrop.ItemData best = null;
            foreach (var it in me.GetInventory().GetAllItems())
            {
                var s = it.m_shared;
                if (s.m_damages.m_chop + s.m_damages.m_pickaxe <= 0f || s.m_attack == null) continue;
                var type = s.m_attack.m_attackType;
                if (type == Attack.AttackType.Projectile || type == Attack.AttackType.TriggerProjectile) continue;
                if (best == null || type == Attack.AttackType.Horizontal || type == Attack.AttackType.Vertical) best = it;
            }
            return best;
        }

        void OnDestroy() { CurrentItem = ""; }
    }
}
