using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Combat.Domain;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Core;
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
    private const float ChaseRange = 40f, Gap = 0.05f, LookAngle = 40f;

    /// One AI step. Expected outcome: true = the chase drove the troll (the game's AI is skipped).
    // D-175 (Architecture V2): the chase goes through the troll's movement capability (TrollContext), built once
    private TrollContext _ctx;

    private float _start, _next;
    private int _swings;
    private Character _target;

    internal static TrollChase Begin(Character troll, Character target)
    {
        TrollChase c = troll.GetComponent<TrollChase>() ?? troll.gameObject.AddComponent<TrollChase>();
        c._target = target;
        c._start = Time.time;
        c._swings = 0;
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
                            _swings + " swing(s))");
            }

            if (me != null)
            {
                TrollCounter.Done(me);
            }

            Destroy(this);
            return false;
        }

        Vector3 point = _target.transform.position;
        Vector3 flat = point - transform.position;
        flat.y = 0f;
        float reach = TrollRage.ReachTo(me, _target);
        if (flat.magnitude > reach)
        {
            Mv().MoveTo(dt, point, reach * 0.8f, true);
        }
        else
        {
            ai.StopMoving();
        }

        Mv().LookAt(point);
        if (me.InAttack() || Time.time < _next || flat.magnitude > reach + 1f || !ai.IsLookingAt(point, LookAngle))
        {
            return true;
        }

        ItemDrop.ItemData weapon = TrollWeapons.Pick(me, Attack.AttackType.Horizontal, false) ??
                                   TrollWeapons.Pick(me, Attack.AttackType.Vertical, false);
        if (weapon == null)
        {
            return true;
        }

        if (!me.IsItemEquiped(weapon))
        {
            me.EquipItem(weapon, false);
        }

        if (me.StartAttack(_target, false))
        {
            _swings++;
            _next = Time.time + Gap;
        }

        return true;
    }
}
