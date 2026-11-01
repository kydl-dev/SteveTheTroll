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
        const float SearchSeconds = 3f, LogSeconds = 5f, LookAngle = 25f, MinAttackGap = 0.3f, BadSeconds = 120f;
        const float StepMetres = 2f, StepSeconds = 2.5f, CloseWaitSeconds = 0.5f, FullCheckSeconds = 2f, AtItSlack = 1f, UnloadMetres = 10f, UnloadStopMetres = 8f, UnloadWalkSeconds = 60f, StuckHomeSeconds = 8f;
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

        // User 2026-10-31: (1) beyond FarmMaxRange the troll walks to the nearest source the game knows of (FarmLongRange, 500 m), (2) it remembers where it farmed last and goes back there
        // after emptying its backpack. SpotKey is in the troll's ZDO so the memory survives a re-created troll and the TrollIdle path (a new farmer component).
        internal const string SpotKey = "stt_spot";
        const float TravelArriveMetres = 12f, TravelMaxSeconds = 420f, SpotMinMetres = 40f, FarBadSeconds = 120f, FarBadRadius = 30f;
        bool _travelling;
        Vector3 _travelTo;
        float _travelStart;
        readonly List<KeyValuePair<Vector3, float>> _badFar = new List<KeyValuePair<Vector3, float>>();

        /// Walking to a far source or back to the remembered spot (test hooks and the status text).
        internal bool Travelling { get { return _travelling; } }
        /// The remembered farm spot, distance in flat metres from the troll (-1 = none). Test hook.
        internal int SpotDistance { get { Vector3 s; if (!Spot(out s)) return -1; s.y = transform.position.y; return Mathf.RoundToInt(Vector3.Distance(s, transform.position)); } }
        /// Test hook: stores a spot as if the troll had farmed there.
        internal void RememberSpotForTest(Vector3 at) { RememberSpot(at); }

        internal string CurrentItem { get; private set; } = "";

        /// The place of the last source this troll worked on (its ZDO), or false when it has none.
        bool Spot(out Vector3 spot)
        {
            spot = Vector3.zero;
            var view = GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return false;
            var z = view.GetZDO();
            spot = z.GetVec3(SpotKey, Vector3.zero);
            return spot != Vector3.zero;
        }

        void RememberSpot(Vector3 at)
        {
            var view = GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.IsOwner()) view.GetZDO().Set(SpotKey, at);
        }

        bool BadFar(Vector3 p)
        {
            for (int i = _badFar.Count - 1; i >= 0; i--)
            {
                if (Time.time >= _badFar[i].Value) { _badFar.RemoveAt(i); continue; }
                var d = _badFar[i].Key - p; d.y = 0f;
                if (d.sqrMagnitude <= FarBadRadius * FarBadRadius) return true;
            }
            return false;
        }

        void StartTravel(Vector3 to)
        {
            _travelling = true; _travelTo = to; _travelStart = Time.time;
            _nextSearch = Time.time + SearchSeconds;      // the normal search runs every SearchSeconds on the way: the zone loads, the near sources show up
        }

        void EndTravel(bool failed)
        {
            if (failed) _badFar.Add(new KeyValuePair<Vector3, float>(_travelTo, Time.time + FarBadSeconds));
            _travelling = false;
            _nextSearch = failed ? Time.time + NothingWaitSeconds : 0f;
        }

        // Walking to a far source / back to the farm spot. Expected outcome: true = the walk drove the troll this frame.
        bool Travel(MonsterAI ai, float dt, System.Func<BaseAI, float, Vector3, float, bool, bool> moveTo, System.Action<BaseAI, Vector3> lookAt)
        {
            var flat = _travelTo - transform.position; flat.y = 0f;
            float d = flat.magnitude;
            if (d <= TravelArriveMetres)
            {
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " arrived at the far farm place " + _travelTo.ToString("F0") + " (" + (Time.time - _travelStart).ToString("F0") + " s) - searches there");
                var rr = GetComponent<TrollRecovery>(); if (rr != null) rr.Reached();
                EndTravel(false);
                return true;
            }
            bool late = Time.time - _travelStart > TravelMaxSeconds;
            if (late)
            {
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " does not get to " + _travelTo.ToString("F0") + " (" + d.ToString("F0") + " m left, walked too long) - gives that place up for " + FarBadSeconds.ToString("0") + " s");
                EndTravel(true);
                return true;
            }
            // D-167: stuck detection, rage and detours are TrollRecovery; a far place that fails two complete cycles is given up (spec 3: 120 s)
            long ka, kb;
            TrollRecovery.KeyOfPlace(_travelTo, out ka, out kb);
            var rec = TrollRecovery.Of(this);
            if (rec.Tick(ai, dt, moveTo, lookAt, _travelTo, ka, kb, true, true, null)) return true;
            if (rec.TakeAbandon())
            {
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " does not get to " + _travelTo.ToString("F0") + " (" + d.ToString("F0") + " m left, two complete recovery cycles failed) - gives that place up for " + FarBadSeconds.ToString("0") + " s");
                EndTravel(true);
                return true;
            }
            moveTo(ai, dt, d > FarWalkMetres ? Waypoint(transform.position, _travelTo) : _travelTo, TravelArriveMetres * 0.5f, true);
            return true;
        }

        const float FarWalkMetres = 80f, WaypointMetres = 30f;

        /// A point `WaypointMetres` ahead on the straight line to `to` (the game's path search fails for a far destination outside the loaded area; same idea as TrollReturn).
        internal static Vector3 Waypoint(Vector3 from, Vector3 to)
        {
            var dir = to - from; dir.y = 0f;
            return from + dir.normalized * Mathf.Min(WaypointMetres, dir.magnitude);
        }

        /// `announce` (D-058): the troll starts on its own after waiting without orders - its owner gets a message with the item it found.
        internal void Setup(string focus, Vector3 centre, bool announce = false)
        {
            _focus = focus ?? ""; _centre = centre; _target = null; _hit = null; _nextSearch = 0f; CurrentItem = ""; _home = false; _announce = announce; _auto = announce; _travelling = false;
            ResetTries();
            MarkFarming(true);
        }

        /// D-150 (test run 014829: the game destroyed and re-created far trolls, the farmer component is gone with the object, and the troll walked back to its patrol point
        /// "wandering" for the 90 s of the idle wait): the ZDO remembers that this troll was farming, so the idle check puts it back to work at once.
        internal const string FarmingKey = "stt_farming";
        void MarkFarming(bool on)
        {
            var view = GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.IsOwner()) view.GetZDO().Set(FarmingKey, on);
        }

        /// T-008e: no farming, straight to the bed (silent).
        /// D-084: after a fight - look for the nearest source again from where the troll stands now. Expected outcome: none (the next step searches).
        internal void Retarget()
        {
            _target = null; _hit = null; _nextSearch = 0f; _home = _full;
            ResetTries();
            _centre = transform.position;
        }

        /// D-143 (user 2026-10-31: after the logout Steve 1 ran around the base until he got an order instead of emptying his backpack): to the hub / bed and into the depot, the
        /// same way as a full backpack, then it is done (the next idle check sends it farming). Expected outcome: none.
        internal void SetupUnload()
        {
            Setup("", transform.position, true);
            MarkFarming(false);
            _home = true; _full = true;
        }

        /// D-159: on its way to unload (hub / bed -> depot).
        internal bool Unloading { get { return _home && _full; } }

        internal void SetupHome()
        {
            Setup("", transform.position, true);
            MarkFarming(false);
            _home = true;
        }

        /// D-065: the attack key named this source. Expected outcome: whether it became the target (it must be breakable for the troll).
        internal bool Force(GameObject source)
        {
            var col = source != null ? TrollFarming.Biggest(source) : null;
            if (col == null) { ModLog.Warn("TrollFarmer: " + TrollTaming.Tag(this) + " cannot farm " + (source != null ? source.name : "?") + " (no collider)"); return false; }
            string item = Utils.GetPrefabName(source);      // shown as is when it yields none of the known items
            foreach (var g in Ui.TrollFocus.DefaultOrder) if (TrollFarming.Yields(source, g.Item)) { item = g.Item; break; }
            _target = source; _hit = col; CurrentItem = item; _home = false; _travelling = false;
            RememberSpot(source.transform.position);
            ResetTries();
            _nextSearch = Time.time + SearchSeconds;
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " ordered to break " + source.name + " (" + TrollFarming.KindOf(source) + ", " + item + ")");
            return true;
        }

        /// One AI step. Expected outcome: returns whether farming drove the troll this frame (true = the game's AI is skipped: no idling,
        /// no feeding animation, D-048); false = fighting / done -> the game's AI runs.
        internal bool Step(MonsterAI ai, float dt, System.Func<BaseAI, float, Vector3, float, bool, bool> moveTo, System.Action<BaseAI, Vector3> lookAt)
        {
            var me = GetComponent<Humanoid>();
            if (me == null || me.IsDead()) { Destroy(this); return me != null && !me.IsDead(); }
            if (ai.GetFollowTarget() != null && !(_home && _full)) { ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " got a follow order - stops farming"); Destroy(this); return ai.GetFollowTarget() == null; }
            if (ai.GetTargetCreature() != null) return ai.GetTargetCreature() == null;      // fighting (D-042 defend): the game's AI drives
            var rec = GetComponent<TrollRecovery>();                                           // D-167: a running rage / detour of the shared recovery drives first
            if (rec != null && rec.Busy && rec.Continue(ai, dt, moveTo, lookAt)) return true;
            // D-159: the full-backpack check comes BEFORE the home branch - a troll heading home for another reason (nothing left to farm, ordered home) is promoted to the unload
            if (Time.time >= _nextFullCheck && !_full && Full()) return true;
            if (_home) return GoHome(ai, dt, moveTo, lookAt);
            var weapon = _target != null ? Weapon(me, _target.GetComponent<TreeBase>() == null) : (_lastWeapon ?? Weapon(me, false));
            if (weapon == null) return NoWeapon(me);
            _noWeaponSince = -1f;
            if ((_target == null || _hit == null) && Time.time >= _nextSearch) Search(weapon);
            if (_home) return true;
            if (_target == null || _hit == null) return _target != null || (_travelling && Travel(ai, dt, moveTo, lookAt));

            Hold(_target);
            var point = AimPoint();
            var flat = point - transform.position; flat.y = 0f;
            float d = flat.magnitude;
            bool tree = _target.GetComponent<TreeBase>() != null;
            float reach = tree ? ModSettings.FarmReach.Value : Mathf.Min(Mathf.Max(1.5f, weapon.m_shared.m_aiAttackRange), ModSettings.FarmReach.Value + 1f);
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + LogSeconds;
                ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(this) + " -> " + _target.name + " (" + TrollFarming.KindOf(_target) + ", " + CurrentItem + ") " + d.ToString("F1") + " m, reach " + reach.ToString("F1")
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
                    // D-167 (user spec): stuck detection, rage, detours and the 120 s blacklist per troll + resource are TrollRecovery (replaces the 8 s give-up of D-063)
                    long ka, kb;
                    TrollRecovery.KeyOf(_target, out ka, out kb);
                    rec = TrollRecovery.Of(this);
                    if (rec.Tick(ai, dt, moveTo, lookAt, point, ka, kb, true, true, _target)) return true;
                    if (rec.TakeAbandon()) { GiveUp("cannot reach it (" + d.ToString("F1") + " m, two complete recovery cycles failed)"); return true; }
                    Vector3 around;
                    if (FriendInTheWay(point, d, out around)) { moveTo(ai, dt, around, 0.6f, false); return true; }     // D-151: around it, not into it
                    moveTo(ai, dt, point, reach * 0.7f, false);
                    return true;
                }
            }
            _stuckSeconds = 0f; _lastDist = float.MaxValue;
            if (rec != null) rec.Reached();          // D-167: at the resource: its failure counter starts again
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
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " no damage on " + _target.name + " - steps " + StepMetres + " m forward, try " + _try + "/" + Tries);
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
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " gives up " + (_target != null ? _target.name : "?") + ": " + why + " - tries another source");
            if (_target != null) _bad[_target] = Time.time + BadSeconds;
            Release();
            _target = null; _hit = null;
            ResetTries();
        }

        bool IsBad(GameObject g)
        {
            float until;
            if (g != null && !IdleRules.Reachable(g.transform.position.y - transform.position.y)) return true;      // D-149: not across cliffs
            if (g != null && HeldByOther(g)) return true;                                                          // D-151: another troll already works on it
            if (g != null && TrollRecovery.IsBlacklisted(GetComponent<Character>(), g)) return true;                // D-167: two failed recovery cycles for this troll + this resource: 120 s
            return g != null && _bad.TryGetValue(g, out until) && Time.time < until;
        }

        // D-149 (test run 011447: Steve 1 had no club, the farmer stayed attached for 10 minutes and the idle check saw "a job"): list what the troll carries, try the game's default
        // items ONCE, and after GiveUpNoWeaponSeconds without a tool the farmer removes itself (the idle check tries again after its normal wait).
        // D-151: reservation of the source a troll works on (user: "not have 2 that go for the same tree and one waits behind the first"). Per client, which holds all own trolls
        // it can see; kept while the holder refreshes it every step, gone HoldSeconds after the last refresh or at once when it gives up / is removed.
        static readonly System.Collections.Generic.Dictionary<int, KeyValuePair<int, float>> Held = new System.Collections.Generic.Dictionary<int, KeyValuePair<int, float>>();
        int Me { get { return GetInstanceID(); } }

        bool HeldByOther(GameObject g)
        {
            KeyValuePair<int, float> h;
            return Held.TryGetValue(g.GetInstanceID(), out h) && TargetRules.HeldByOther(h.Key, h.Value, Me, Time.time, TargetRules.HoldSeconds);
        }

        void Hold(GameObject g)
        {
            if (g == null) return;
            if (Held.Count > 200) Held.Clear();
            Held[g.GetInstanceID()] = new KeyValuePair<int, float>(Me, Time.time);
        }

        void Release()
        {
            if (_target == null) return;
            KeyValuePair<int, float> h;
            if (Held.TryGetValue(_target.GetInstanceID(), out h) && h.Key == Me) Held.Remove(_target.GetInstanceID());
        }

        // D-151 (user: "there is a friendly troll right in front of me, let's re-route next to it"): every 0.25 s the own tamed trolls are checked; one standing on the line to the
        // target, in front, makes this troll walk to a point beside it for DetourSeconds, on the side away from the line (two trolls head on pass each other).
        const float DetourSeconds = 1.2f, DetourCheckSeconds = 0.25f;
        float _detourUntil, _nextDetourCheck, _nextDetourLog;
        Vector3 _detourTo;
        int _detours;

        bool FriendInTheWay(Vector3 goal, float distance, out Vector3 around)
        {
            around = goal;
            if (Time.time < _detourUntil) { around = _detourTo; return true; }
            if (Time.time < _nextDetourCheck) return false;
            _nextDetourCheck = Time.time + DetourCheckSeconds;
            var p = transform.position;
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.gameObject == gameObject || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c)) continue;
                var o = c.transform.position;
                if (!TargetRules.Blocked(p.x, p.z, goal.x, goal.z, o.x, o.z, TargetRules.Ahead, TargetRules.HalfWidth)) continue;
                float wx, wz;
                TargetRules.Waypoint(p.x, p.z, goal.x, goal.z, o.x, o.z, TargetRules.Side, out wx, out wz);
                _detourTo = new Vector3(wx, p.y, wz);
                _detourUntil = Time.time + DetourSeconds;
                _detours++;
                if (Time.time >= _nextDetourLog)
                {
                    _nextDetourLog = Time.time + 10f;
                    ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " steps around " + TrollTaming.Tag(c) + " standing " + new Vector2(o.x - p.x, o.z - p.z).magnitude.ToString("F1") + " m ahead on the way to " + _target.name + " (" + _detours + " detour(s) so far)");
                }
                around = _detourTo;
                return true;
            }
            return false;
        }

        const float GiveUpNoWeaponSeconds = 15f;
        float _noWeaponSince = -1f;
        bool _triedDefaults;

        bool NoWeapon(Humanoid me)
        {
            if (_noWeaponSince < 0f) _noWeaponSince = Time.time;
            if (!_triedDefaults)
            {
                _triedDefaults = true;
                var items = new System.Text.StringBuilder();
                foreach (var it in me.GetInventory().GetAllItems()) items.Append(it.m_shared.m_name).Append(" (chop ").Append(it.m_shared.m_damages.m_chop.ToString("F0")).Append(", pickaxe ").Append(it.m_shared.m_damages.m_pickaxe.ToString("F0")).Append(", attack ").Append(it.m_shared.m_attack != null ? it.m_shared.m_attack.m_attackType.ToString() : "none").Append("); ");
                ModLog.Warn("TrollFarmer: " + TrollTaming.Tag(this) + " has no melee item with chop / pickaxe damage - carries: " + (items.Length > 0 ? items.ToString() : "nothing") + " - asks the game for its default items");
                try { me.GiveDefaultItems(); } catch (System.Exception e) { ModLog.Warn("TrollFarmer: " + TrollTaming.Tag(this) + " GiveDefaultItems failed: " + e.GetType().Name); }
                return true;
            }
            if (Time.time - _noWeaponSince < GiveUpNoWeaponSeconds) return true;
            ModLog.Warn("TrollFarmer: " + TrollTaming.Tag(this) + " still has no tool after " + GiveUpNoWeaponSeconds.ToString("0") + " s - stops farming (tries again later)");
            Destroy(this);
            return false;
        }

        // D-051: focus first, then the other allowed items, within QuickRange -> FarmRange -> MaxRange of the troll; nothing -> go home.
        void Search(ItemDrop.ItemData weapon)
        {
            _nextSearch = Time.time + SearchSeconds;
            ResetTries();
            int tier = TamingRules.TamedToolTier;      // D-059: tamed trolls break any tree / rock (TrollFarmHits)
            var goals = Ui.TrollFocus.SearchOrder(_focus);         // D-058: focus item first, then the default priority
            TrollFarming.Gather(transform.position, ModSettings.FarmMaxRange.Value, _cands);   // D-084: one pass over the scene per search
            // D-159: a troll with a hub does not farm further than HubRules.FarmLeash (950 m) from it (it only goes further when followed / teleported, which are no farm orders)
            var hub = Hub.HubRegistry.For(GetComponent<Character>());
            System.Func<GameObject, bool> skip = hub == null ? (System.Func<GameObject, bool>)IsBad : (g => IsBad(g) || !Hub.HubRules.FarmAllowed(true, Hub.HubRules.Flat(g.transform.position - hub.Pos)));
            foreach (var range in new[] { ModSettings.FarmQuickRange.Value, ModSettings.FarmRange.Value, ModSettings.FarmMaxRange.Value })
            {
                foreach (var goal in goals)
                {
                    string item = goal.Item;
                    _target = TrollFarming.FindIn(_cands, transform.position, transform.position, goal, tier, range, out _hit, skip);
                    if (_target == null) continue;
                    string was = CurrentItem;
                    CurrentItem = item;
                    _travelling = false;
                    RememberSpot(_target.transform.position);
                    Hold(_target);
                    ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " farms " + item + (_focus.Length > 0 && item != _focus ? " (no " + _focus + " found, farming something else)" : "") + " from "
                        + _target.name + " (" + TrollFarming.KindOf(_target) + ") within " + range + " m" + (was != item && was.Length > 0 ? ", was " + was : ""));
                    if (_announce && IsMine()) Ui.Notify.Show(Loc.T("stevethetroll_farm_auto", Shown(), Loc.T(Ui.TrollFocus.KeyOf(item))), Ui.Notify.Kind.Farm);
                    _announce = false;
                    return;
                }
            }
            // User 2026-10-31: nothing within FarmMaxRange -> the long-range search (FarmLongRange, 500 m) walks to the nearest source the game knows of; on the way the near search above
            // runs again every SearchSeconds (the zone loads around the troll) and takes over as soon as a source is in range.
            if (_travelling) { _target = null; _hit = null; return; }
            float longRange = ModSettings.FarmLongRange.Value;
            if (longRange > ModSettings.FarmMaxRange.Value)
            {
                Vector3 bedPos;
                bool hasBedPos = TrollNoHit.Bed(out bedPos);
                float noHitR = ModSettings.NoHitRadius.Value;
                System.Func<Vector3, bool> farSkip = p => BadFar(p)
                    || (ModSettings.On && hasBedPos && noHitR > 0f && TrollNoHit.Inside(bedPos, p, noHitR))
                    || (hub != null && !Hub.HubRules.FarmAllowed(true, Hub.HubRules.Flat(p - hub.Pos)));
                Vector3 farPos; string farItem;
                bool found = TrollFarming.FindFar(transform.position, longRange, ModSettings.FarmMaxRange.Value, tier, goals, farSkip, out farPos, out farItem);
                ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(this) + " long-range search: " + TrollFarming.LastFar);
                if (found)
                {
                    CurrentItem = farItem;
                    var away = farPos - transform.position; away.y = 0f;
                    ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " finds nothing within " + ModSettings.FarmMaxRange.Value + " m - walks " + away.magnitude.ToString("F0") + " m to a " + farItem + " source at " + farPos.ToString("F0"));
                    if (_announce && IsMine()) Ui.Notify.Show(Loc.T("stevethetroll_farm_auto", Shown(), Loc.T(Ui.TrollFocus.KeyOf(farItem))), Ui.Notify.Kind.Farm);
                    _announce = false;
                    StartTravel(farPos);
                    return;
                }
            }
            CurrentItem = "";
            _announce = false;
            _nextSearch = Time.time + NothingWaitSeconds;
            // D-149 (user: "when no suitable resource exists, wait and search again" - not home, and not to the depot): stays where it is
            if (Time.time >= _nextNothingLog)
            {
                _nextNothingLog = Time.time + 60f;
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " finds nothing to farm within " + ModSettings.FarmMaxRange.Value + " m - waits here and searches again in " + NothingWaitSeconds.ToString("0") + " s");
            }
            if (IsMine() && !_auto && !_saidNothing)      // D-062: a troll that went farming on its own does not report "nothing found" every search; a ordered one says it once
            {
                _saidNothing = true;
                Ui.Notify.Show(Loc.T("stevethetroll_farm_nothing", Shown(), Mathf.Max(ModSettings.FarmMaxRange.Value, ModSettings.FarmLongRange.Value).ToString("0")), Ui.Notify.Kind.Farm);
            }
        }
        const float NothingWaitSeconds = 20f;
        float _nextNothingLog;
        bool _saidNothing;

        bool GoHome(MonsterAI ai, float dt, System.Func<BaseAI, float, Vector3, float, bool, bool> moveTo, System.Action<BaseAI, Vector3> lookAt)
        {
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            bool hasBed = profile != null && profile.HaveCustomSpawnPoint();
            // D-135: a Troll Hub (its owner's, chosen by hand or the nearest) replaces the bed as the place to go to, unload and rest
            Vector3 bed;
            bool atHub = Hub.HubRegistry.Anchor(GetComponent<Character>(), hasBed ? profile.GetCustomSpawnPoint() : Vector3.zero, out bed);
            if (!hasBed && !atHub)
            {
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " has no bed or Troll Hub to go to - stays here");
                ai.SetPatrolPoint();
                Destroy(this);
                return false;
            }
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
            if (!there)
            {
                // User 2026-10-31 (Steve 3, ~400 m from the hub, did not move): the game's path search fails for a destination far outside the loaded area (D-162 found it for the 1000 m
                // walk back), so a far walk goes to a point 30 m ahead, renewed every step, and runs.
                // D-167 (user spec: "a troll must never abandon its assigned hub because of a blocked route"): stuck detection, rage and detours are TrollRecovery; the old 45 s give-up
                // (FarHomeStuckSeconds, RetrySoon) is gone. Within the depot ring the D-095 rule above still counts a troll that gets no closer as home.
                long ka, kb;
                TrollRecovery.KeyOfHub(bed, out ka, out kb);
                var rec = TrollRecovery.Of(this);
                if (rec.Tick(ai, dt, moveTo, lookAt, bed, ka, kb, false, true, null)) return true;
                if (d > FarWalkMetres) moveTo(ai, dt, Waypoint(transform.position, bed), 0.5f, true);
                else moveTo(ai, dt, bed, home, false);
                return true;
            }
            { var rh = GetComponent<TrollRecovery>(); if (rh != null) rh.Reached(); }          // D-167: at the hub: its counter starts again
            if (stuck && d > home + 1f && !_full) ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " gets no closer to the bed than " + d.ToString("F1") + " m - counts as home");
            if (_full)
            {
                // D-064: at the bed with a full backpack -> to a troll depot with room (built now when there is none)
                Vector3 at;
                if (Backpack.TrollDepot.Plan(GetComponent<Character>(), bed, TrollTaming.Owner(GetComponent<Character>()), out at)) { _unloading = true; _unloadAt = at; _unloadStart = Time.time; return true; }
                TrollIdle.RetrySoon(GetComponent<Character>());
                if (IsMine()) Ui.Notify.Show(Loc.T("stevethetroll_depot_nospot", Shown(), Hub.HubRegistry.DestinationName(GetComponent<Character>())), Ui.Notify.Kind.Warn);
            }
            ai.StopMoving();
            ai.SetPatrolPoint();
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " is at its destination (" + Hub.HubRegistry.DestinationName(GetComponent<Character>()) + ", " + d.ToString("F1") + " m) - waits");
            if (!_auto) Ui.Notify.Show(Loc.T("stevethetroll_farm_home", Shown(), Hub.HubRegistry.DestinationName(GetComponent<Character>())), Ui.Notify.Kind.Farm);
            Destroy(this);
            return true;
        }

        // D-064: walk to the depot (within UnloadMetres), then everything goes into its chests; gives up walking after UnloadWalkSeconds.
        bool Unload(MonsterAI ai, float dt, System.Func<BaseAI, float, Vector3, float, bool, bool> moveTo, Vector3 bed)
        {
            var flat = _unloadAt - transform.position; flat.y = 0f;
            bool late = Time.time - _unloadStart > UnloadWalkSeconds;
            // D-106 (user: "empty their backpacks when they are 5-10m away from the depot, so that they don't all stumble into each other"):
            // a troll unloads as soon as it is within UnloadMetres (10 m); it walks only until UnloadStopMetres (8 m), never into the depot
            if (flat.magnitude > UnloadMetres && !late) { moveTo(ai, dt, _unloadAt, UnloadStopMetres, false); return true; }
            ai.StopMoving();
            ai.SetPatrolPoint();
            var me = GetComponent<Character>();
            int left;
            int moved = Backpack.TrollDepot.Unload(me, bed, TrollTaming.Owner(me), out left);
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " unloaded " + moved + " stack(s) at the depot (" + flat.magnitude.ToString("F1") + " m" + (late ? ", walked too long" : "") + "), " + left + " left");
            if (IsMine()) Ui.Notify.Show(left > 0 ? Loc.T("stevethetroll_depot_full", Shown(), left.ToString()) : Loc.T("stevethetroll_depot_done", Shown(), moved.ToString()), left > 0 ? Ui.Notify.Kind.Warn : Ui.Notify.Kind.Farm);
            if (left == 0 && ResumeAtSpot()) return true;
            Destroy(this);
            return true;
        }

        // User 2026-10-31 ("have trolls remember the spot where they've farmed before coming home to empty the backpack"): an emptied troll walks back to where it last worked and farms on
        // from there (focus from its ZDO). Too close to bother (SpotMinMetres), out of the hub's farm leash or no spot known -> false (the idle check sends it farming from here).
        bool ResumeAtSpot()
        {
            Vector3 spot;
            if (!Spot(out spot)) return false;
            var away = spot - transform.position; away.y = 0f;
            if (away.magnitude < SpotMinMetres) return false;
            var me = GetComponent<Character>();
            var hub = Hub.HubRegistry.For(me);
            if (hub != null && !Hub.HubRules.FarmAllowed(true, Hub.HubRules.Flat(spot - hub.Pos))) return false;
            var z = TrollTaming.Zdo(me);
            string focus = z != null ? Ui.TrollFocus.Normalize(z.GetString(TrollRegistry.FocusKey, "")) : "";
            Setup(focus, spot, false);
            _full = false; _unloading = false; _homeBest = float.MaxValue; _auto = true;
            StartTravel(spot);
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " emptied its backpack - walks " + away.magnitude.ToString("F0") + " m back to where it farmed (" + spot.ToString("F0") + ")" + (focus.Length > 0 ? ", focus '" + focus + "'" : ""));
            return true;
        }

        // D-062: a full backpack ends farming - the troll walks to the owner's bed (T-008e). Expected outcome: true = it is full.
        bool Full()
        {
            _nextFullCheck = Time.time + FullCheckSeconds;
            var view = GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !Backpack.BackpackStore.IsFull(view.GetZDO())) return false;
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " backpack is full - goes to " + Hub.HubRegistry.DestinationName(GetComponent<Character>()));
            _home = true; _auto = true; _full = true; _target = null; _hit = null; CurrentItem = ""; _travelling = false;
            if (IsMine()) Ui.Notify.Show(Loc.T("stevethetroll_backpack_full", Shown(), Hub.HubRegistry.DestinationName(GetComponent<Character>())), Ui.Notify.Kind.Farm);
            return true;
        }

        bool IsMine() { return TrollTaming.Owner(GetComponent<Character>()) == (Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : -1L); }

        string Shown() { return TrollTaming.ShownName(this); }

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
                ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(this) + " swings " + best.m_shared.m_name + " (" + best.m_shared.m_attack.m_attackType + ", wanted " + want + " for a " + (low ? "low target" : "standing tree") + ")");
            }
            return best;
        }

        void OnDestroy() { CurrentItem = ""; Release(); var rc = GetComponent<TrollRecovery>(); if (rc != null) rc.Forget(); }
    }
}
