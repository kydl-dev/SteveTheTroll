using System.Collections.Generic;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// The farming state of one tamed troll (owner client; T-008d, D-048..D-051). Search (D-051, user): quick check within QuickRange of the
    /// troll - the focus item first, then the other allowed items ("Farming now" changes); none -> FarmRange, then MaxRange; nothing within
    /// MaxRange -> the troll walks to the owner's bed and the owner is told. Every new search tries the focus item first again, so the troll goes
    /// back to it as soon as one is found. Hitting (D-051): a try = up to SwingsPerTry swings; no damage -> step StepMetres forward and try
    /// again, Tries tries in all, then give the source up (skipped BadSeconds). Blocked while already close (< reach + CloseSlack) counts as
    /// in range. A follow order or a dead troll ends farming; a fight (target creature) hands the AI back for that frame.
    /// </summary>
    internal sealed class TrollFarmer : MonoBehaviour
    {
        const float SearchSeconds = 3f, LogSeconds = 5f, LookAngle = 25f, MinAttackGap = 0.3f, StuckSeconds = 8f, BadSeconds = 120f;
        const float StepMetres = 2f, StepSeconds = 2.5f, CloseSlack = 3f, HomeMetres = 4f;
        const int SwingsPerTry = 2, Tries = 4;
        readonly Dictionary<GameObject, float> _bad = new Dictionary<GameObject, float>();
        string _focus = "";
        Vector3 _centre;
        GameObject _target;
        Collider _hit;
        float _nextSearch, _nextAttack, _nextLog, _stuckSeconds, _lastDist = float.MaxValue, _stepUntil;
        Vector3 _stepTo;
        int _attacks, _swingsThisTry, _try = 1, _dry;
        uint _lastRevision;
        bool _home;

        internal string CurrentItem { get; private set; } = "";

        internal void Setup(string focus, Vector3 centre)
        {
            _focus = focus ?? ""; _centre = centre; _target = null; _hit = null; _nextSearch = 0f; CurrentItem = ""; _home = false;
            ResetTries();
        }

        /// One AI step. Expected outcome: returns whether farming drove the troll this frame (true = the game's AI is skipped: no idling,
        /// no feeding animation, D-048); false = fighting / done -> the game's AI runs.
        internal bool Step(MonsterAI ai, float dt, System.Func<BaseAI, float, Vector3, float, bool, bool> moveTo, System.Action<BaseAI, Vector3> lookAt)
        {
            var me = GetComponent<Humanoid>();
            if (me == null || me.IsDead()) { Destroy(this); return me != null && !me.IsDead(); }
            if (ai.GetFollowTarget() != null) { ModLog.Info("TrollFarmer: " + name + " got a follow order - stops farming"); Destroy(this); return ai.GetFollowTarget() == null; }
            if (ai.GetTargetCreature() != null) return ai.GetTargetCreature() == null;      // fighting (D-042 defend): the game's AI drives
            if (_home) return GoHome(ai, dt, moveTo);
            var weapon = Weapon(me);
            if (weapon == null) { if (Time.time >= _nextLog) { _nextLog = Time.time + LogSeconds; ModLog.Warn("TrollFarmer: " + name + " has no melee item with chop / pickaxe damage"); } return weapon != null; }
            if ((_target == null || _hit == null) && Time.time >= _nextSearch) Search(weapon);
            if (_home) return true;
            if (_target == null || _hit == null) return _target != null;

            var point = AimPoint();
            var flat = point - transform.position; flat.y = 0f;
            float d = flat.magnitude;
            bool tree = _target.GetComponent<TreeBase>() != null;
            float reach = tree ? ModSettings.FarmReach.Value : Mathf.Min(Mathf.Max(1.5f, weapon.m_shared.m_aiAttackRange), ModSettings.FarmReach.Value + 1f);
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + LogSeconds;
                ModLog.Verbose("TrollFarmer: " + name + " -> " + _target.name + " (" + TrollFarming.KindOf(_target) + ", " + CurrentItem + ") " + d.ToString("F1") + " m, reach " + reach.ToString("F1")
                    + " m, height diff " + (point.y - transform.position.y).ToString("F1") + " m, try " + _try + "/" + Tries + " (" + _swingsThisTry + " swing(s), " + _dry + " dry), attacks " + _attacks + ", stuck " + _stuckSeconds.ToString("F0") + " s");
            }
            // a step forward after a dry try (D-051)
            if (Time.time < _stepUntil) { moveTo(ai, dt, _stepTo, 0.3f, false); return true; }
            if (d > reach)
            {
                if (d < _lastDist - 0.2f) { _lastDist = d; _stuckSeconds = 0f; } else _stuckSeconds += dt;
                bool close = d <= reach + CloseSlack;
                if (_stuckSeconds <= StuckSeconds) { moveTo(ai, dt, point, reach * 0.7f, false); return true; }
                if (!close) { GiveUp("cannot reach it (" + d.ToString("F1") + " m)"); return true; }
                // TEST-022: "cannot reach it (2.7 m)" - blocked by the log / trunk itself while already close: swing from here
            }
            _stuckSeconds = 0f; _lastDist = float.MaxValue;
            ai.StopMoving();
            lookAt(ai, point);
            if (me.InAttack() || Time.time < _nextAttack || !ai.IsLookingAt(point, LookAngle, false)) return true;
            uint rev = Revision();
            if (_swingsThisTry > 0) { if (rev == _lastRevision) _dry++; else { _dry = 0; _try = 1; } }
            _lastRevision = rev;
            if (_swingsThisTry >= SwingsPerTry && _dry >= SwingsPerTry)
            {
                if (_try >= Tries) { GiveUp(Tries + " tries of " + SwingsPerTry + " swings without damage (height diff " + (point.y - transform.position.y).ToString("F1") + " m)"); return true; }
                _try++;
                _swingsThisTry = 0; _dry = 0;
                _stepTo = transform.position + flat.normalized * StepMetres;
                _stepUntil = Time.time + StepSeconds;
                ModLog.Info("TrollFarmer: " + name + " no damage on " + _target.name + " - steps " + StepMetres + " m forward, try " + _try + "/" + Tries);
                return true;
            }
            if (!me.IsItemEquiped(weapon)) me.EquipItem(weapon, false);
            if (me.StartAttack(null, false))
            {
                _attacks++; _swingsThisTry++;
                _nextAttack = Time.time + MinAttackGap;       // D-048: keep swinging
            }
            return true;
        }

        // Trees: the trunk (tree position) at chest height; logs / rocks / others: the nearest point of the collider bounds, at chest height.
        Vector3 AimPoint()
        {
            var b = _hit.bounds;
            Vector3 p = _target.GetComponent<TreeBase>() != null ? _target.transform.position : b.ClosestPoint(transform.position);
            p.y = Mathf.Clamp(transform.position.y + 1.5f, b.min.y, b.max.y);
            return p;
        }

        uint Revision()
        {
            var v = _target != null ? _target.GetComponent<ZNetView>() : null;
            var z = v != null && v.IsValid() ? v.GetZDO() : null;
            return z != null ? z.DataRevision : 0u;
        }

        void ResetTries() { _try = 1; _swingsThisTry = 0; _dry = 0; _lastRevision = 0u; _stuckSeconds = 0f; _lastDist = float.MaxValue; _stepUntil = 0f; }

        void GiveUp(string why)
        {
            ModLog.Info("TrollFarmer: " + name + " gives up " + (_target != null ? _target.name : "?") + ": " + why + " - tries another source");
            if (_target != null) _bad[_target] = Time.time + BadSeconds;
            _target = null; _hit = null;
            ResetTries();
        }

        bool IsBad(GameObject g)
        {
            float until;
            return g != null && _bad.TryGetValue(g, out until) && Time.time < until;
        }

        // D-051: focus first, then the other allowed items, within QuickRange -> FarmRange -> MaxRange of the troll; nothing -> go home.
        void Search(ItemDrop.ItemData weapon)
        {
            _nextSearch = Time.time + SearchSeconds;
            ResetTries();
            int tier = weapon.m_shared.m_toolTier;
            var items = new List<string>();
            if (_focus.Length > 0) items.Add(_focus);
            foreach (var it in TrollFarming.Priority) if (it != _focus) items.Add(it);
            foreach (var range in new[] { ModSettings.FarmQuickRange.Value, ModSettings.FarmRange.Value, ModSettings.FarmMaxRange.Value })
            {
                foreach (var item in items)
                {
                    _target = TrollFarming.Find(transform.position, transform.position, item, tier, range, out _hit, IsBad);
                    if (_target == null) continue;
                    string was = CurrentItem;
                    CurrentItem = item;
                    ModLog.Info("TrollFarmer: " + name + " farms " + item + (_focus.Length > 0 && item != _focus ? " (no " + _focus + " found, farming something else)" : "") + " from "
                        + _target.name + " (" + TrollFarming.KindOf(_target) + ") within " + range + " m" + (was != item && was.Length > 0 ? ", was " + was : ""));
                    return;
                }
            }
            CurrentItem = "";
            _home = true;
            string shown = Shown();
            ModLog.Info("TrollFarmer: " + name + " finds nothing to farm within " + ModSettings.FarmMaxRange.Value + " m - goes to the owner's bed");
            if (TrollTaming.Owner(GetComponent<Character>()) == (Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : -1L))
                Ui.Notify.Show(Loc.T("stevethetroll_farm_nothing", shown, ModSettings.FarmMaxRange.Value.ToString("0")), Ui.Notify.Kind.Farm);
        }

        bool GoHome(MonsterAI ai, float dt, System.Func<BaseAI, float, Vector3, float, bool, bool> moveTo)
        {
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (profile == null || !profile.HaveCustomSpawnPoint())
            {
                ModLog.Info("TrollFarmer: " + name + " has no bed to go to - stays here");
                ai.SetPatrolPoint();
                Destroy(this);
                return false;
            }
            var bed = profile.GetCustomSpawnPoint();
            float d = Vector3.Distance(transform.position, bed);
            if (d > HomeMetres) { moveTo(ai, dt, bed, HomeMetres * 0.7f, false); return true; }
            ai.StopMoving();
            ai.SetPatrolPoint();
            ModLog.Info("TrollFarmer: " + name + " is at the owner's bed (" + d.ToString("F1") + " m) - waits");
            Ui.Notify.Show(Loc.T("stevethetroll_farm_home", Shown()), Ui.Notify.Kind.Farm);
            Destroy(this);
            return true;
        }

        string Shown()
        {
            var t = GetComponent<Tameable>();
            string n = t != null ? t.GetText() : "";
            return string.IsNullOrEmpty(n) ? name : n;
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
