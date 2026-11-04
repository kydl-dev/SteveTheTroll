using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Combat.Domain;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Core;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Movement.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Orders.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Combat.Infrastructure;

/// <summary>
///     D-084 (user: "When trolls are getting attacked, they should do 3 hits overhead into the ground and then go back to
///     farming (no retreat,
///     just walk forward towards 1st tree/vein they find and farm there) and ignore any hits on themselves for next 15
///     seconds, just farm. If
///     after those 15 seconds they are hit again, then they should go to the enemy, do 3 hits (or until enemy is dead, if
///     enemy is strong) and
///     then go back to farming (and restart the 15 sec ignore timer)"). On the client that owns the troll:
///     - first hit after a calm spell: CounterSlams overhead slams towards the attacker (TrollSlam), then back to work;
///     - hits within CounterIgnoreSeconds after a response: ignored - the troll keeps farming (its game-AI target is
///     cleared every step);
///     - a hit after that window: TrollChase - to the attacker, swinging until it is dead (or gone / too far /
///     CounterChaseSeconds), then back.
///     After every response the ignore window starts again. CalmSeconds without hits -> the next response is the slams
///     again.
///     "Back to work": a farming troll searches again from where it stands (the nearest tree / vein), a following /
///     staying troll goes on.
///     Player defence (TrollRage within 10 m, TrollDefend within 50 m) is separate and wins over this.
/// </summary>
internal static class TrollCounter
{
    private const int CounterSlams = 3;
    private const float CalmSeconds = 60f;

    private static readonly Dictionary<int, State> States = new();

    private static State Of(Character troll)
    {
        State s;
        int id = troll.GetInstanceID();
        if (!States.TryGetValue(id, out s))
        {
            s = new State();
            States[id] = s;
            if (States.Count > 200)
            {
                States.Clear();
            }
        }

        return s;
    }

    /// The troll ignores hits now (keeps farming). Expected outcome: true inside the ignore window.
    internal static bool Ignoring(Character troll)
    {
        State s;
        return troll != null && States.TryGetValue(troll.GetInstanceID(), out s) && Time.time < s.IgnoreUntil;
    }

    /// A creature hit the troll (TamedTrollStats.HitLog, owner client). Expected outcome: what the troll does ("ignore", "slam", "chase", "busy").
    internal static string OnHit(Character troll, Character attacker)
    {
        if (troll == null || attacker == null || troll.IsDead())
        {
            return "none";
        }

        ZNetView view = troll.GetComponent<ZNetView>();
        if (view == null || !view.IsValid() || !view.IsOwner())
        {
            return "not owner";
        }

        bool busy = troll.GetComponent<TrollRage>() != null || troll.GetComponent<TrollSlam>() != null ||
                    troll.GetComponent<TrollChase>() != null;
        State s = Of(troll);
        float now = Time.time;
        CounterResponse
            answer = CounterRules.Respond(busy, now, s.IgnoreUntil, s.LastResponse, s.Slammed,
                CalmSeconds); // D-171: the decision is game-free
        if (answer == CounterResponse.Busy)
        {
            return "busy";
        }

        if (answer == CounterResponse.Ignore)
        {
            if (now - s.LastIgnoredLog > 5f)
            {
                s.LastIgnoredLog = now;
                ModLog.Verbose("TrollCounter: " + TrollOrders.Shown(troll) + " ignores the hit of " + attacker.name +
                               " - keeps working (" + (s.IgnoreUntil - now).ToString("F0") + " s left)");
            }

            return "ignore";
        }

        s.LastResponse = now;
        s.IgnoreUntil = float.MaxValue; // until the response is done (Done starts the window)
        if (answer == CounterResponse.Slam)
        {
            s.Slammed = true;
            if (TrollSlam.Begin(troll, null, attacker.transform.position, 1, CounterSlams, true) != null)
            {
                ModLog.Info("TrollCounter: " + TrollOrders.Shown(troll) + " hit by " + attacker.name + " - " +
                            CounterSlams + " ground slams, then back to work");
                return "slam";
            }
        }

        TrollChase.Begin(troll, attacker);
        ModLog.Info("TrollCounter: " + TrollOrders.Shown(troll) + " hit again by " + attacker.name +
                    " after the quiet time - goes for it");
        return "chase";
    }

    /// A response (slams / chase) is over: the ignore window starts and the troll goes back to work. Expected outcome: the window end time.
    internal static float Done(Character troll)
    {
        if (troll == null)
        {
            return 0f;
        }

        State s = Of(troll);
        s.IgnoreUntil = Time.time + ModSettings.CounterIgnoreSeconds.Value;
        TrollGuard.Forget(troll);
        MonsterAI ai = troll.GetComponent<MonsterAI>();
        if (ai != null)
        {
            TrollGuard.ClearTarget(ai);
        }

        TrollFarmer f = troll.GetComponent<TrollFarmer>();
        if (f != null)
        {
            f.Retarget();
        }

        ModLog.Info("TrollCounter: " + TrollOrders.Shown(troll) + " back to " +
                    (f != null ? "farming (nearest source from here)" : "what it did") + ", ignores hits for " +
                    ModSettings.CounterIgnoreSeconds.Value.ToString("0") + " s");
        return s.IgnoreUntil;
    }

    /// A guard chase (TrollGuard) is over: the forced target goes and the farmer looks for its nearest source again; no ignore window (a creature that still goes for the player is
    /// taken on again by the next guard scan). Expected outcome: true when the troll had a farmer to hand back to.
    internal static bool GuardDone(Character troll)
    {
        if (troll == null)
        {
            return false;
        }

        TrollGuard.Forget(troll);
        MonsterAI ai = troll.GetComponent<MonsterAI>();
        if (ai != null)
        {
            TrollGuard.ClearTarget(ai);
        }

        TrollFarmer f = troll.GetComponent<TrollFarmer>();
        if (f != null)
        {
            f.Retarget();
        }

        ModLog.Info("TrollCounter: " + TrollOrders.Shown(troll) + " guard chase over - back to " +
                    (f != null ? "farming (nearest source from here)" : "what it did"));
        return f != null;
    }

    private sealed class State
    {
        internal float IgnoreUntil, LastResponse = -999f, LastIgnoredLog = -999f;
        internal bool Slammed;
    }
}

/// <summary>
///     D-084: go to the creature and swing until it is dead (or gone, out of ChaseRange, or CounterChaseSeconds),
///     then TrollCounter.Done.
/// </summary>
internal sealed class TrollChase : MonoBehaviour
{
    private const float ChaseRange = 40f, Gap = 0.05f, LookAngle = 40f, RetryGap = 0.25f;

    /// One AI step. Expected outcome: true = the chase drove the troll (the game's AI is skipped).
    // D-175 (Architecture V2): the chase goes through the troll's movement capability (TrollContext), built once
    private TrollContext _ctx;

    private float _start, _next;
    private int _swings;
    private Character _target;
    private bool _guard;

    /// Swings the game accepted in this chase (diagnostics: stt.guardKill logs it).
    internal int Swings => _swings;

    /// The creature this chase goes for.
    internal Character Target => _target;

    // D-176 (guard-mob-kills in a forest, run 220216: the chase stood 12 - 15 m from the creature at 0.0 m/s for 14 s - something solid in the straight line, and the chase only walks straight):
    // no progress for StuckSeconds while out of reach -> a sidestep to alternating sides for DetourSeconds, then straight again.
    private const float StuckSpeed = 0.4f, StuckSeconds = 0.8f, DetourSeconds = 1.5f, DetourSide = 6f;
    private float _stuckFor, _detourUntil;
    private Vector3 _detourTo;
    private int _detours;
    private bool _lastMoveOk, _detourOpen;
    private float _detourFromDistance;

    // D-176 (fix9, guard-mob-kills in a forest: real speed read 0.5 - 0.9 m/s between 0.0 samples, so the speed timer never reached 0.8 s and the stall was noticed 7 - 11 s late): the stall
    // is ALSO judged by position - less than WindowMetres of net movement in WindowSeconds while out of reach is a stall, however the instantaneous speed looks.
    // The detour side is chosen by what is free (a body-wide cast to both candidate points) and a side whose detour did not bring the troll closer is avoided next time
    // (fix9: detour 1 to the right got closer, detour 2 to the left ran into Rock_4 1.1 m away and did not).
    private const float WindowSeconds = 2f, WindowMetres = 1f, CastRadius = 0.8f, FailedSidePenalty = 4f;
    private float _winStart, _windowMoved;
    private Vector3 _winPos;
    private bool _lastSideRight = true, _lastDetourFailed;

    /// The last reason the troll did not swing ("" = none yet).
    internal string LastBlocker { get; private set; } = "";

    /// `guard` = started by TrollGuard (a creature that goes for the player), not by a hit on the troll: the end does not start TrollCounter's ignore window.
    internal static TrollChase Begin(Character troll, Character target, bool guard = false)
    {
        TrollChase c = troll.GetComponent<TrollChase>() ?? troll.gameObject.AddComponent<TrollChase>();
        c._target = target;
        c._start = Time.time;
        c._swings = 0;
        c._guard = guard;
        c._detours = 0;
        c._stuckFor = 0f;
        c._detourUntil = 0f;
        c._winStart = Time.time;
        c._winPos = troll.transform.position;
        c._lastDetourFailed = false;
        c._detourOpen = false;
        ModLog.Info("TrollChase: " + TrollOrders.Shown(troll) + " starts (" + (guard ? "guard / order" : "answers a hit") + ") at " + (target != null ? target.name : "?") + ", " +
                    (target != null ? Vector3.Distance(troll.transform.position, target.transform.position).ToString("F1") : "?") + " m away, speed " + troll.GetVelocity().magnitude.ToString("F1") + " m/s, in no-hit zone " +
                    TrollNoHit.InZone(troll.transform.position));
        return c;
    }

    private ITrollMovement Mv()
    {
        if (_ctx == null)
        {
            _ctx = new TrollContext(GetComponent<Character>());
        }

        return _ctx.Capabilities.Movement;
    }

    internal bool Step(MonsterAI ai, float dt)
    {
        Humanoid me = GetComponent<Humanoid>();
        bool over = me == null || me.IsDead() || _target == null || _target.IsDead() ||
                    Time.time - _start > ModSettings.CounterChaseSeconds.Value
                    || Vector3.Distance(transform.position, _target.transform.position) > ChaseRange;
        if (over)
        {
            if (me != null)
            {
                ModLog.Info("TrollChase: " + TrollOrders.Shown(me) + " done (" +
                            (_target == null || _target.IsDead() ? "enemy dead" : "enemy gone / too long") + ", " +
                            _swings + " swing(s), " + _detours + " detour(s), " + (Time.time - _start).ToString("F1") + " s, last blocker '" + LastBlocker + "')");
            }

            if (me != null && _guard)
            {
                TrollCounter.GuardDone(me);
            }
            else if (me != null)
            {
                TrollCounter.Done(me);
            }

            Destroy(this);
            return false;
        }

        // D-176: a troll never swings within NoHitRadius of the bed (TrollNoHit, user: no hit zone 15 m from bed). A guard chase there can only stand next to the creature
        // (guard-mob-kills: 998 refused swings in 30 s), so it ends and the troll keeps the guard target as before; TrollGuard does not start one inside the zone.
        if (_guard && TrollNoHit.InZone(transform.position))
        {
            ModLog.Info("TrollChase: " + TrollOrders.Shown(me) + " is inside the bed no-hit zone - cannot swing at " + _target.name + ", the guard chase ends (" + _swings + " swing(s))");
            TrollCounter.GuardDone(me);
            Destroy(this);
            return false;
        }

        Vector3 point = _target.transform.position;
        Vector3 flat = point - transform.position;
        flat.y = 0f;
        float reach = TrollRage.ReachTo(me, _target);
        if (_detourOpen && Time.time >= _detourUntil)
        {
            _detourOpen = false;
            _lastDetourFailed = !(flat.magnitude < _detourFromDistance - 0.5f);
            ModLog.Info("TrollChase: " + TrollOrders.Shown(me) + " detour " + _detours + " over - " + _target.name + " was " + _detourFromDistance.ToString("F1") + " m away, is " + flat.magnitude.ToString("F1") +
                        " m now (" + (_lastDetourFailed ? "NOT closer - the other side is preferred next" : "closer") + ")");
        }

        bool detouring = Time.time < _detourUntil;
        if (flat.magnitude > reach)
        {
            _stuckFor = me.GetVelocity().magnitude < StuckSpeed ? _stuckFor + dt : 0f;
            bool windowStuck = false;
            if (detouring)
            {
                _winStart = Time.time;
                _winPos = transform.position;
            }
            else if (Time.time - _winStart >= WindowSeconds)
            {
                Vector3 moved = transform.position - _winPos;
                moved.y = 0f;
                _windowMoved = moved.magnitude;
                windowStuck = _windowMoved < WindowMetres;
                _winStart = Time.time;
                _winPos = transform.position;
            }

            if (!detouring && (_stuckFor >= StuckSeconds || windowStuck))
            {
                StartDetour(me, flat, point, windowStuck ? "moved only " + _windowMoved.ToString("F1") + " m in " + WindowSeconds.ToString("F0") + " s" : "speed under " + StuckSpeed.ToString("F1") + " m/s for " + StuckSeconds.ToString("F1") + " s");
                detouring = true;
            }

            // D-176: the answer of the game's MoveTo is kept (it is false when it did not move the troll) and shows in the stall log
            _lastMoveOk = detouring ? Mv().MoveTo(dt, _detourTo, 0.5f, true) : Mv().MoveTo(dt, point, reach * 0.8f, true);
        }
        else
        {
            _stuckFor = 0f;
            _winStart = Time.time;
            _winPos = transform.position;
            ai.StopMoving();
        }

        if (!detouring)
        {
            Mv().LookAt(point);
        }

        // D-176 (guard-mob-kills: the chase ran 60 s next to the mob with 0 swings): every reason the troll does not swing is named, once every 2 s
        string why = me.InAttack() ? "already in an attack" : TrollNoHit.InZone(transform.position) ? "inside the bed no-hit zone" : Time.time < _next ? "swing gap" :
            flat.magnitude > reach + 1f ? "out of reach" : !ai.IsLookingAt(point, LookAngle) ? "not facing it" : null;
        if (why != null)
        {
            Note(me, why, flat.magnitude, reach);
            return true;
        }

        ItemDrop.ItemData weapon = TrollWeapons.Pick(me, Attack.AttackType.Horizontal, false) ??
                                   TrollWeapons.Pick(me, Attack.AttackType.Vertical, false);
        if (weapon == null)
        {
            Note(me, "no melee item", flat.magnitude, reach);
            return true;
        }

        if (!me.IsItemEquiped(weapon) && !me.EquipItem(weapon, false) && !me.IsItemEquiped(weapon))
        {
            _next = Time.time + RetryGap;
            Note(me, "could not equip " + weapon.m_shared.m_name, flat.magnitude, reach);
            return true;
        }

        if (me.StartAttack(_target, false))
        {
            _swings++;
            _next = Time.time + Gap;
        }
        else
        {
            _next = Time.time + RetryGap; // D-176: a refused attack is tried again in a quarter of a second, not at every AI step
            Note(me, "StartAttack refused with " + weapon.m_shared.m_name, flat.magnitude, reach);
        }

        return true;
    }

    /// No progress towards the creature: names what is in the way and walks to a point beside the line (the side changes each time). Expected outcome: none.
    private void StartDetour(Humanoid me, Vector3 flat, Vector3 point, string trigger)
    {
        Vector3 dir = flat.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        Vector3 chest0 = transform.position + Vector3.up;
        float freeRight = FreeRun(chest0, right * DetourSide + dir * 2f), freeLeft = FreeRun(chest0, -right * DetourSide + dir * 2f);
        float scoreRight = freeRight - (_lastDetourFailed && _lastSideRight ? FailedSidePenalty : 0f), scoreLeft = freeLeft - (_lastDetourFailed && !_lastSideRight ? FailedSidePenalty : 0f);
        bool goRight = Mathf.Abs(scoreRight - scoreLeft) < 0.25f ? !_lastSideRight : scoreRight > scoreLeft;
        _lastSideRight = goRight;
        Vector3 side = right * (goRight ? 1f : -1f);
        Vector3 to = transform.position + side * DetourSide + dir * 2f;
        to.y = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(to) : to.y;
        _detourTo = to;
        _detourUntil = Time.time + DetourSeconds;
        _stuckFor = 0f;
        _detours++;
        _detourOpen = true;
        _detourFromDistance = flat.magnitude;
        RaycastHit hit;
        Vector3 chest = transform.position + Vector3.up;
        string blocker = Physics.Raycast(chest, dir, out hit, 4f)
            ? hit.collider.name + " of " + hit.collider.transform.root.name + " at " + hit.distance.ToString("F1") + " m"
            : Physics.SphereCast(chest, 0.7f, dir, out hit, 15f)
                ? "nothing within 4 m, a body-wide cast finds " + hit.collider.name + " of " + hit.collider.transform.root.name + " at " + hit.distance.ToString("F1") + " m"
                : "nothing in the straight line (4 m ray, 15 m body-wide cast)";
        Character body = me;
        ModLog.Info("TrollChase: " + TrollOrders.Shown(me) + " makes no progress towards " + _target.name + " (" + trigger + "; " + flat.magnitude.ToString("F1") + " m; troll at " + transform.position.ToString("F0") +
                    ", target at " + point.ToString("F0") + ") - " + blocker + " - the game's MoveTo reports arrived: " + _lastMoveOk + ", real speed " + body.GetVelocity().magnitude.ToString("F1") +
                    " m/s, in attack " + me.InAttack() + ", staggered " + body.IsStaggering() + ", " + TrollHold.Describe(body) + ", " + TrollMoveDiag.Describe(body, point) + " - detour " + _detours + " to the " + (goRight ? "right" : "left") +
                    " (free run right " + freeRight.ToString("F1") + " m, left " + freeLeft.ToString("F1") + " m" + (_lastDetourFailed ? ", the side of the last failed detour is penalised" : "") + "), walking to " + to.ToString("F0"));
    }

    /// How far a body-wide cast from `from` along `offset` runs before it hits something (the whole length when nothing is in the way). Expected outcome: metres.
    private static float FreeRun(Vector3 from, Vector3 offset)
    {
        float length = offset.magnitude;
        RaycastHit hit;
        return Physics.SphereCast(from, CastRadius, offset / length, out hit, length) ? hit.distance : length;
    }

    private float _nextNote;

    private void Note(Humanoid me, string why, float distance, float reach)
    {
        LastBlocker = why;
        if (Time.time < _nextNote)
        {
            return;
        }

        _nextNote = Time.time + 2f;
        ModLog.Info("TrollChase: " + TrollOrders.Shown(me) + " does not swing at " + (_target != null ? _target.name : "?") + " - " + why + " (" + distance.ToString("F1") +
                    " m, reach " + reach.ToString("F1") + " m, speed " + me.GetVelocity().magnitude.ToString("F1") + " m/s, " + _swings + " swing(s), " + _detours + " detour(s) so far)");
    }
}
