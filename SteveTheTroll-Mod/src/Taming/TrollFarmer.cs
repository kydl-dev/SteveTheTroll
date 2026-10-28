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
        const float StepMetres = 2f, StepSeconds = 2.5f, CloseWaitSeconds = 0.5f, FullCheckSeconds = 2f, AtItSlack = 1f, UnloadMetres = 4f, UnloadWalkSeconds = 60f, StuckHomeSeconds = 8f;
        float _homeBest = float.MaxValue, _homeBestAt;
        const int SwingsPerTry = 2, Tries = 4;
        readonly Dictionary<GameObject, float> _bad = new Dictionary<GameObject, float>();
        string _focus = "";
        Vector3 _centre;
        GameObject _target;
        Collider _hit;
        readonly List<TrollFarming.Candidate> _cands = new List<TrollFarming.Candidate>();
        float _nextSearch, _nextAttack, _nextLog, _stuckSeconds, _lastDist = float.MaxValue, _stepUntil;
        Vector3 _stepTo;
        int _attacks, _swingsThisTry, _try = 1, _dry;
        uint _lastRevision;
        bool _home, _announce, _auto, _full, _unloading;
        Vector3 _unloadAt;
        float _unloadStart;
        float _nextFullCheck;
        ItemDrop.ItemData _lastWeapon;

        internal string CurrentItem { get; private set; } = "";

        /// `announce` (D-058): the troll starts on its own after waiting without orders - its owner gets a message with the item it found.
        internal void Setup(string focus, Vector3 centre, bool announce = false)
        {
            _focus = focus ?? ""; _centre = centre; _target = null; _hit = null; _nextSearch = 0f; CurrentItem = ""; _home = false; _announce = announce; _auto = announce;
            ResetTries();
        }

        /// T-008e: no farming, straight to the bed (silent).
        /// D-084: after a fight - look for the nearest source again from where the troll stands now. Expected outcome: none (the next step searches).
        internal void Retarget()
        {
            _target = null; _hit = null; _nextSearch = 0f; _home = _full;
            ResetTries();
            _centre = transform.position;
        }

        internal void SetupHome()
        {
            Setup("", transform.position, true);
            _home = true;
        }

        /// D-065: the attack key named this source. Expected outcome: whether it became the target (it must be breakable for the troll).
        internal bool Force(GameObject source)
        {
            var col = source != null ? TrollFarming.Biggest(source) : null;
            if (col == null) { ModLog.Warn("TrollFarmer: " + name + " cannot farm " + (source != null ? source.name : "?") + " (no collider)"); return false; }
            string item = Utils.GetPrefabName(source);      // shown as is when it yields none of the known items
            foreach (var g in Ui.TrollFocus.DefaultOrder) if (TrollFarming.Yields(source, g.Item)) { item = g.Item; break; }
            _target = source; _hit = col; CurrentItem = item; _home = false;
            ResetTries();
            _nextSearch = Time.time + SearchSeconds;
            ModLog.Info("TrollFarmer: " + name + " ordered to break " + source.name + " (" + TrollFarming.KindOf(source) + ", " + item + ")");
            return true;
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
            if (Time.time >= _nextFullCheck && Full()) return true;
            var weapon = _target != null ? Weapon(me, _target.GetComponent<TreeBase>() == null) : (_lastWeapon ?? Weapon(me, false));
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
                // D-062 (TEST-026: waited ~8 s at 2.3 m in front of a trunk): right at it (reach + 1 m) and not getting closer for 0.5 s ->
                // swing from here. D-063 (TEST-027: it hit rocks in its way): further away and stuck -> it does NOT swing at whatever blocks it,
                // it gives this source up and takes another one.
                bool atIt = d <= reach + AtItSlack;
                if (!atIt || _stuckSeconds <= CloseWaitSeconds)
                {
                    if (_stuckSeconds <= StuckSeconds) { moveTo(ai, dt, point, reach * 0.7f, false); return true; }
                    if (!atIt) { GiveUp("cannot reach it (" + d.ToString("F1") + " m, blocked)"); return true; }
                }
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

        // Trees: the trunk (tree position) at chest height. Logs and other long sources: the nearest point on the collider's long axis
        // (D-059, TEST-025: a lying log's world bounds are a big box - the troll stood inside it, "0.0 m" away, and swung at nothing).
        // Others: the nearest point of the collider bounds (its centre when the troll stands inside them). Always at chest height.
        Vector3 AimPoint()
        {
            var area = TrollFarming.LiveArea(_target, transform.position);          // D-082: a vein / big rock: its nearest live hit area
            if (area != null && area != _hit) { _hit = area; }
            var b = _hit.bounds;
            var me = transform.position;
            Vector3 p, a, e;
            if (_target.GetComponent<TreeBase>() != null) p = _target.transform.position;
            else if (LongAxis(_hit, out a, out e)) p = Vector3.Lerp(a, e, TamingRules.SegmentT(a.x, a.z, e.x, e.z, me.x, me.z));
            else { p = b.ClosestPoint(me); p.y = me.y; if ((p - me).sqrMagnitude < 0.01f) p = b.center; }
            p.y = Mathf.Clamp(me.y + 1.5f, b.min.y, b.max.y);
            return p;
        }

        // The long axis of an elongated collider (box / capsule / mesh), as two world points. Expected outcome: false when it is not
        // clearly longer in one direction (then the bounds are used).
        static bool LongAxis(Collider c, out Vector3 a, out Vector3 b)
        {
            a = b = Vector3.zero;
            Vector3 centre, size;
            var box = c as BoxCollider; var cap = c as CapsuleCollider; var mesh = c as MeshCollider;
            if (box != null) { centre = box.center; size = box.size; }
            else if (cap != null) { centre = cap.center; size = Vector3.one * cap.radius * 2f; size[cap.direction] = Mathf.Max(cap.height, cap.radius * 2f); }
            else if (mesh != null && mesh.sharedMesh != null) { centre = mesh.sharedMesh.bounds.center; size = mesh.sharedMesh.bounds.size; }
            else return false;
            var ls = c.transform.lossyScale;
            var world = new Vector3(Mathf.Abs(size.x * ls.x), Mathf.Abs(size.y * ls.y), Mathf.Abs(size.z * ls.z));
            int k = world.x >= world.y && world.x >= world.z ? 0 : (world.y >= world.z ? 1 : 2);
            float longest = world[k], other = Mathf.Max(world[(k + 1) % 3], world[(k + 2) % 3]);
            if (longest < 1.5f * other) return false;
            var dir = Vector3.zero; dir[k] = Mathf.Max(0f, size[k] - Mathf.Min(size[(k + 1) % 3], size[(k + 2) % 3])) * 0.5f;
            a = c.transform.TransformPoint(centre - dir);
            b = c.transform.TransformPoint(centre + dir);
            return true;
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
            int tier = TamingRules.TamedToolTier;      // D-059: tamed trolls break any tree / rock (TrollFarmHits)
            var goals = Ui.TrollFocus.SearchOrder(_focus);         // D-058: focus item first, then the default priority
            TrollFarming.Gather(transform.position, ModSettings.FarmMaxRange.Value, _cands);   // D-084: one pass over the scene per search
            foreach (var range in new[] { ModSettings.FarmQuickRange.Value, ModSettings.FarmRange.Value, ModSettings.FarmMaxRange.Value })
            {
                foreach (var goal in goals)
                {
                    string item = goal.Item;
                    _target = TrollFarming.FindIn(_cands, transform.position, transform.position, goal, tier, range, out _hit, IsBad);
                    if (_target == null) continue;
                    string was = CurrentItem;
                    CurrentItem = item;
                    ModLog.Info("TrollFarmer: " + name + " farms " + item + (_focus.Length > 0 && item != _focus ? " (no " + _focus + " found, farming something else)" : "") + " from "
                        + _target.name + " (" + TrollFarming.KindOf(_target) + ") within " + range + " m" + (was != item && was.Length > 0 ? ", was " + was : ""));
                    if (_announce && IsMine()) Ui.Notify.Show(Loc.T("stevethetroll_farm_auto", Shown(), Loc.T(Ui.TrollFocus.KeyOf(item))), Ui.Notify.Kind.Farm);
                    _announce = false;
                    return;
                }
            }
            CurrentItem = "";
            _home = true;
            _announce = false;
            string shown = Shown();
            ModLog.Info("TrollFarmer: " + name + " finds nothing to farm within " + ModSettings.FarmMaxRange.Value + " m - goes to the owner's bed");
            if (IsMine() && !_auto)      // D-062: a troll that went farming on its own does not report "nothing found" every 2 minutes
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
            if (_unloading) return Unload(ai, dt, moveTo, bed);
            float d = Vector3.Distance(transform.position, bed);
            // D-095 (user: "5-25m from the bed ... don't just keep walking trying to get closer without being able to do so"): a full troll
            // unloads as soon as it is within the depot ring (25 m); any troll counts as home once within 25 m when it stops getting closer
            // (bed inside a house it cannot enter) for StuckHomeSeconds.
            if (d < _homeBest - 0.5f) { _homeBest = d; _homeBestAt = Time.time; }
            bool stuck = d <= Backpack.TrollDepot.MaxFromBed && Time.time - _homeBestAt > StuckHomeSeconds;
            // D-097 (user: "as close as 15m from the bed, not right on top of the bed"): home = within HomeRadius, it waits there
            float home = ModSettings.HomeRadius.Value;
            bool there = d <= home + 1f || (_full && d <= Backpack.TrollDepot.MaxFromBed) || stuck;
            if (!there) { moveTo(ai, dt, bed, home, false); return true; }
            if (stuck && d > home + 1f && !_full) ModLog.Info("TrollFarmer: " + name + " gets no closer to the bed than " + d.ToString("F1") + " m - counts as home");
            if (_full)
            {
                // D-064: at the bed with a full backpack -> to a troll depot with room (built now when there is none)
                Vector3 at;
                if (Backpack.TrollDepot.Plan(GetComponent<Character>(), bed, TrollTaming.Owner(GetComponent<Character>()), out at)) { _unloading = true; _unloadAt = at; _unloadStart = Time.time; return true; }
                if (IsMine()) Ui.Notify.Show(Loc.T("stevethetroll_depot_nospot", Shown()), Ui.Notify.Kind.Warn);
            }
            ai.StopMoving();
            ai.SetPatrolPoint();
            ModLog.Info("TrollFarmer: " + name + " is at the owner's bed (" + d.ToString("F1") + " m) - waits");
            if (!_auto) Ui.Notify.Show(Loc.T("stevethetroll_farm_home", Shown()), Ui.Notify.Kind.Farm);
            Destroy(this);
            return true;
        }

        // D-064: walk to the depot (within UnloadMetres), then everything goes into its chests; gives up walking after UnloadWalkSeconds.
        bool Unload(MonsterAI ai, float dt, System.Func<BaseAI, float, Vector3, float, bool, bool> moveTo, Vector3 bed)
        {
            var flat = _unloadAt - transform.position; flat.y = 0f;
            bool late = Time.time - _unloadStart > UnloadWalkSeconds;
            if (flat.magnitude > UnloadMetres && !late) { moveTo(ai, dt, _unloadAt, UnloadMetres * 0.7f, false); return true; }
            ai.StopMoving();
            ai.SetPatrolPoint();
            var me = GetComponent<Character>();
            int left;
            int moved = Backpack.TrollDepot.Unload(me, bed, TrollTaming.Owner(me), out left);
            ModLog.Info("TrollFarmer: " + name + " unloaded " + moved + " stack(s) at the depot (" + flat.magnitude.ToString("F1") + " m" + (late ? ", walked too long" : "") + "), " + left + " left");
            if (IsMine()) Ui.Notify.Show(left > 0 ? Loc.T("stevethetroll_depot_full", Shown(), left.ToString()) : Loc.T("stevethetroll_depot_done", Shown(), moved.ToString()), left > 0 ? Ui.Notify.Kind.Warn : Ui.Notify.Kind.Farm);
            Destroy(this);
            return true;
        }

        // D-062: a full backpack ends farming - the troll walks to the owner's bed (T-008e). Expected outcome: true = it is full.
        bool Full()
        {
            _nextFullCheck = Time.time + FullCheckSeconds;
            var view = GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !Backpack.BackpackStore.IsFull(view.GetZDO())) return false;
            ModLog.Info("TrollFarmer: " + name + " backpack is full - goes to the owner's bed");
            _home = true; _auto = true; _full = true; _target = null; _hit = null; CurrentItem = "";
            if (IsMine()) Ui.Notify.Show(Loc.T("stevethetroll_backpack_full", Shown()), Ui.Notify.Kind.Farm);
            return true;
        }

        bool IsMine() { return TrollTaming.Owner(GetComponent<Character>()) == (Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : -1L); }

        string Shown()
        {
            var t = GetComponent<Tameable>();
            string n = t != null ? t.GetText() : "";
            return string.IsNullOrEmpty(n) ? name : n;
        }

        // A melee troll attack with chop or pickaxe damage (punch / log swing), not the throw. D-061 (user: Steve's log "went through" logs
        // without hitting): the game's melee hit check of a HORIZONTAL swing runs at the attack height (chest), so it passes over a log lying
        // on the ground; low targets (everything but a standing tree) get the VERTICAL swing, standing trees the horizontal one.
        ItemDrop.ItemData Weapon(Humanoid me, bool low)
        {
            var want = low ? Attack.AttackType.Vertical : Attack.AttackType.Horizontal;
            var best = TrollWeapons.Pick(me, want, true);
            if (best != null && best != _lastWeapon)
            {
                _lastWeapon = best;
                ModLog.Verbose("TrollFarmer: " + name + " swings " + best.m_shared.m_name + " (" + best.m_shared.m_attack.m_attackType + ", wanted " + want + " for a " + (low ? "low target" : "standing tree") + ")");
            }
            return best;
        }

        void OnDestroy() { CurrentItem = ""; }
    }
}
