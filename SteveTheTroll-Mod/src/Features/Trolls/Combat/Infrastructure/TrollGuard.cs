using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Combat.Domain;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Orders.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Combat.Infrastructure;

/// <summary>
///     D-065 (user: "if a mob, any mob is attacking either the tamed troll or the player, instantly target that mob"): 4
///     times a second, on
///     the client that owns it, a tamed troll looks for creatures within DefendRange that target its owner or itself. One
///     or a few -> it
///     attacks the nearest of them at once (and keeps that one while it still attacks). CrowdCount or more -> TrollSlam (4
///     ground slams),
///     then the nearest one left; again at most every SlamCooldown seconds.
/// </summary>
internal static class TrollGuard
{
    private const float TickSeconds = 0.25f;
    private const float KeepRange = 70f;

    /// D-084: the troll's forced target is dropped (its counter is over / it ignores hits). Expected outcome: whether one was dropped.
    // D-171 (P9 audit): entries of destroyed trolls were never removed; past MaxForced the map starts again (a live forced target is set again within a tick by Guard / Order).
    private const int MaxForced = 200;

    private static readonly Action<MonsterAI, Character> SetTarget =
        AccessTools.MethodDelegate<Action<MonsterAI, Character>>(AccessTools.Method(typeof(MonsterAI), "SetTarget"));

    private static readonly AccessTools.FieldRef<MonsterAI, Character> Target =
        AccessTools.FieldRefAccess<MonsterAI, Character>("m_targetCreature");

    // D-066 (TEST-029: the target was set again 4x a second - the game's AI dropped it for its own choice / out of sight): the creature
    // the troll must fight, re-applied before each AI step (Keep) until it is dead, tamed or further than KeepRange.
    private static readonly Dictionary<int, Character> Forced = new();
    private static readonly List<Character> Attackers = new();
    private static float _next, _nextDropLog;
    private static int _dropped;

    private static readonly List<KeyValuePair<Character, MonsterAI>> Mine = new();
    private static readonly List<Character> Hunters = new();
    private static int _ticks, _chars, _trolls, _hunters;
    private static long _scanTicks;
    private static float _nextStats;

    /// Plugin.Update. Expected outcome: how many trolls changed their target this time.
    internal static int Tick()
    {
        if (Time.time < _next || !ModSettings.On)
        {
            return 0;
        }

        _next = Time.time + TickSeconds;
        Player me = Player.m_localPlayer;
        if (me == null)
        {
            return 0;
        }

        int changed = 0;
        // D-141 (performance review): ONE pass over all characters per tick finds both the owner's trolls and the creatures that go for the owner; every troll then
        // checks only that short list (it used to walk the whole character list again for each troll: 7 passes x 4 a second with six trolls).
        Mine.Clear();
        Hunters.Clear();
        long myId = me.GetPlayerID();
        List<Character> all = Character.GetAllCharacters();
        long t0 = Stopwatch.GetTimestamp();
        foreach (Character c in all)
        {
            if (c == null || c.IsDead())
            {
                continue;
            }

            if (c.IsTamed())
            {
                if (!TrollTaming.IsTroll(c) || TrollTaming.Owner(c) != myId)
                {
                    continue;
                }

                ZNetView view = c.GetComponent<ZNetView>();
                MonsterAI ai = c.GetComponent<MonsterAI>();
                if (ai == null || view == null || !view.IsValid() || !view.IsOwner() ||
                    c.GetComponent<TrollSlam>() != null)
                {
                    continue;
                }

                Mine.Add(new KeyValuePair<Character, MonsterAI>(c, ai));
                continue;
            }

            if (c.IsPlayer())
            {
                continue;
            }

            MonsterAI mai = c.GetComponent<MonsterAI>();
            if (mai != null && Target(mai) == me)
            {
                Hunters.Add(c); // D-084: creatures that go for the troll itself: TrollCounter (on its hits)
            }
        }

        if (Hunters.Count > 0)
        {
            foreach (KeyValuePair<Character, MonsterAI> m in Mine)
            {
                if (Guard(m.Key, m.Value, me))
                {
                    changed++;
                }
            }
        }

        _ticks++;
        _chars += all.Count;
        _trolls += Mine.Count;
        _hunters += Hunters.Count;
        _scanTicks += Stopwatch.GetTimestamp() - t0;
        if (ModLog.DebugOn && Time.time >= _nextStats)
        {
            _nextStats = Time.time + 5f;
            ModLog.Verbose("TrollGuard: last 5 s - " + _ticks + " tick(s), avg " + (_ticks > 0 ? _chars / _ticks : 0) +
                           " character(s), " + (_ticks > 0 ? _trolls / (float)_ticks : 0).ToString("F1") +
                           " own troll(s), "
                           + (_ticks > 0 ? _hunters / (float)_ticks : 0).ToString("F1") +
                           " creature(s) after the owner; one pass took " +
                           (_ticks > 0 ? _scanTicks * 1000.0 / Stopwatch.Frequency / _ticks : 0).ToString("F3") +
                           " ms on average");
            _ticks = _chars = _trolls = _hunters = 0;
            _scanTicks = 0;
        }

        return changed;
    }

    /// The attack key (TrollOrders) or a hit on the troll: attack this creature now. Expected outcome: whether the target was set.
    internal static bool Order(Character troll, Character creature)
    {
        MonsterAI ai = troll != null ? troll.GetComponent<MonsterAI>() : null;
        ZNetView view = troll != null ? troll.GetComponent<ZNetView>() : null;
        if (ai == null || creature == null || view == null || !view.IsValid() || !view.IsOwner())
        {
            return false;
        }

        Trim();
        Forced[troll.GetInstanceID()] = creature;
        if (Target(ai) == creature)
        {
            Engage(troll, creature); // D-176: already its target (TrollGuard got there first) is a success - the order must still end in a fight
            return true;
        }

        bool applied = ApplyTarget(ai, creature);
        if (applied)
        {
            Engage(troll, creature);
        }

        ModLog.Info("TrollGuard: " + TrollTaming.Tag(troll) + " ordered / hit - attacks " + creature.name + " (" +
                    Vector3.Distance(troll.transform.position, creature.transform.position).ToString("F1") + " m)" +
                    (applied ? "" : " - the AI did not take the target"));
        return applied;
    }

    /// D-176 (guard-mob-kills: a farming troll with the target set stood next to the mob and never swung; the same for an attack-key order): ONE way from "has the target" to "fights":
    /// a farming troll walks to the creature and swings itself (TrollChase). Not inside the bed no-hit zone (TrollNoHit: the troll cannot swing there; the troll or the creature in the
    /// circle) and not when a chase for that creature runs already. Expected outcome: true when a chase was started.
    internal static bool Engage(Character troll, Character creature)
    {
        if (troll == null || creature == null || troll.GetComponent<TrollFarmer>() == null)
        {
            return false;
        }

        if (TrollNoHit.InZone(troll.transform.position) || TrollNoHit.InZone(creature.transform.position))
        {
            return false;
        }

        TrollChase running = troll.GetComponent<TrollChase>();
        if (running != null && running.Target == creature)
        {
            return false;
        }

        TrollChase.Begin(troll, creature, true);
        return true;
    }

    /// D-175 (review of the flaky guard-mob run): SetTarget can refuse in some AI states (alerted / sight) and Alert does not check anything, so a target was reported as taken
    /// that the AI never held. The field is set directly when SetTarget refuses (as Keep does before every AI step) and the result is verified. Expected outcome: true when the AI targets `target` now.
    private static bool ApplyTarget(MonsterAI ai, Character target)
    {
        if (ai == null || target == null)
        {
            return false;
        }

        SetTarget(ai, target);
        if (Target(ai) != target)
        {
            Target(ai) = target;
        }

        ai.Alert();
        return Target(ai) == target;
    }

    private static int Trim()
    {
        if (Forced.Count <= MaxForced)
        {
            return Forced.Count;
        }

        ModLog.Verbose("TrollGuard: " + Forced.Count +
                       " forced targets kept - cleared (stale entries of removed trolls)");
        Forced.Clear();
        return Forced.Count;
    }

    internal static bool Forget(Character troll)
    {
        return troll != null && Forced.Remove(troll.GetInstanceID());
    }

    /// D-084: the game AI's target is cleared (the troll keeps farming). Expected outcome: whether it had one.
    internal static bool ClearTarget(MonsterAI ai)
    {
        if (ai == null || Target(ai) == null)
        {
            return false;
        }

        Target(ai) = null;
        return true;
    }

    /// The name of the forced target ("" = none). Only TrollGuard (a creature that hunts the owner) and an order (attack key / hit) set one - the game's own AI never does.
    internal static string ForcedName(Character troll)
    {
        Character f;
        return troll != null && Forced.TryGetValue(troll.GetInstanceID(), out f) && f != null ? Utils.GetPrefabName(f.gameObject) : ""; // the prefab name, as stt.targetOf reports it (f.name is "Greydwarf(Clone)")
    }

    /// D-176 (diagnostics): what the troll holds now - the game AI's target, the forced target and the running chase, side by side (a lost target shows which part lost it).
    internal static string Describe(Character troll)
    {
        MonsterAI ai = troll != null ? troll.GetComponent<MonsterAI>() : null;
        Character ait = ai != null ? Target(ai) : null;
        Character f2 = null;
        if (troll != null)
        {
            Forced.TryGetValue(troll.GetInstanceID(), out f2);
        }

        TrollChase chase = troll != null ? troll.GetComponent<TrollChase>() : null;
        return "AI target " + (ait != null ? ait.name : "none") + ", forced target " + (f2 != null ? f2.name : "none") + ", chase " +
               (chase != null && chase.Target != null ? chase.Target.name + " (" + chase.Swings + " swing(s))" : "none") + ", farmer " +
               (troll != null && troll.GetComponent<TrollFarmer>() != null);
    }

    /// D-084: the troll has a forced target from the player's defence (TrollDefend / the attack key). Expected outcome: true when it has one.
    internal static bool HasForced(Character troll)
    {
        Character t;
        return troll != null && Forced.TryGetValue(troll.GetInstanceID(), out t) && t != null && !t.IsDead();
    }

    /// Before each AI step of the troll (TrollFarming.Steer, owner): put the forced target back. Expected outcome: true while it has one.
    internal static bool Keep(MonsterAI ai)
    {
        Character t;
        int id = ai.GetComponent<Character>() != null ? ai.GetComponent<Character>().GetInstanceID() : 0;
        if (id == 0 || !Forced.TryGetValue(id, out t))
        {
            return false;
        }

        if (t == null || t.IsDead() || t.IsTamed() ||
            Vector3.Distance(t.transform.position, ai.transform.position) > KeepRange)
        {
            Forced.Remove(id);
            ModLog.Verbose("TrollGuard: " + TrollTaming.Tag(ai) + " forced target gone (" +
                           (t == null ? "removed" : t.IsDead() ? "dead" : "tamed / far") + ")");
            return false;
        }

        if (Target(ai) != t)
        {
            // D-176 (attack-key-creature, run 213113: the target was gone a few frames after it was set): the game's AI dropped the forced target between two steps - counted and logged
            _dropped++;
            if (Time.time >= _nextDropLog)
            {
                _nextDropLog = Time.time + 5f;
                ModLog.Info("TrollGuard: " + TrollTaming.Tag(ai) + " the game's AI dropped the forced target " + t.name + " (" + (Target(ai) == null ? "none" : Target(ai).name) +
                            " instead; " + _dropped + " time(s) so far) - applied again");
            }

            Target(ai) = t;
        }

        return true;
    }

    private static bool Guard(Character troll, MonsterAI ai, Player owner)
    {
        float range = ModSettings.DefendRange.Value;
        bool wears = TrollTaming.Wears(owner), raid = !wears && TrollHome.Raid();
        // D-078 (TEST-032: Spencer at the house stayed 'fighting' while the player hunted 80 m away - a target near the player was set,
        // dropped as too far (KeepRange) and set again 4x a second, so he never farmed): the player's attackers count only while the troll
        // itself is within DefendRange of the player (as TrollDefend), and no attacker further than KeepRange from the troll
        bool ownerNear = Vector3.Distance(owner.transform.position, troll.transform.position) <= range;
        Attackers.Clear();
        Vector3 trollPos = troll.transform.position;
        Vector3 ownerPos = owner.transform.position;
        float keep2 = KeepRange * KeepRange,
            range2 = range * range,
            guard2 = ModSettings.GuardOwnerRange.Value * ModSettings.GuardOwnerRange.Value;
        bool defends = TamingRules.DefendsOwner(wears, raid);
        foreach (Character m in Hunters)
        {
            if (m == null || m.IsDead() || m.IsTamed())
            {
                continue; // Hunters were collected this tick; one may have died / been tamed since
            }

            // T-008e: slippers off -> it defends the player only during a raid; D-078: only while the troll is near the player; D-081 (TEST-034: a farming Steve ran 60 m back
            // home to a greydwarf that only aimed at the player): a creature that goes for the player is taken on only when it is near the troll. D-171: GuardRules decides.
            Vector3 mp = m.transform.position;
            if (!GuardRules.Eligible(defends, ownerNear, (mp - trollPos).sqrMagnitude, (mp - ownerPos).sqrMagnitude,
                    guard2, keep2, range2))
            {
                continue;
            }

            Attackers.Add(m);
        }

        if (Attackers.Count == 0)
        {
            return false;
        }

        int id = troll.GetInstanceID();
        // D-084: no crowd slams here any more (TrollCounter answers hits on the troll, TrollRage hits on the player)
        Character cur;
        Forced.TryGetValue(id, out cur);
        if (cur != null && Attackers.Contains(cur))
        {
            return false; // keeps the one it fights while that one still attacks
        }

        Character nearest = null;
        float best = float.MaxValue;
        foreach (Character m in Attackers)
        {
            float d = Vector3.Distance(m.transform.position, troll.transform.position);
            if (d < best)
            {
                best = d;
                nearest = m;
            }
        }

        Trim();
        Forced[id] = nearest;
        if (!ApplyTarget(ai, nearest))
        {
            ModLog.Warn("TrollGuard: " + TrollTaming.Tag(troll) + " - the AI did not take " + nearest.name +
                        " as its target");
            return false;
        }

        TrollFarmer farmer = troll.GetComponent<TrollFarmer>();
        // D-176: inside the bed no-hit zone (TrollNoHit) the troll cannot swing, so there is nothing to chase for - the troll or the creature in the circle: the guard target stays (as before), no chase
        bool noHit = TrollNoHit.InZone(troll.transform.position) || TrollNoHit.InZone(nearest.transform.position);
        if (farmer != null)
        {
            Engage(troll, nearest); // until it is dead / gone; the end hands the troll back to the farmer
        }

        ModLog.Info("TrollGuard: " + TrollTaming.Tag(troll) + " attacks " + nearest.name + " (" + best.ToString("F1") +
                    " m) - it targets " + (Target(nearest.GetComponent<MonsterAI>()) == troll
                        ? "the troll"
                        : owner.GetPlayerName())
                    + ", " + Attackers.Count + " attacker(s)" + (farmer != null ? ", farming paused" : "") + (farmer != null && noHit ? ", no chase (bed no-hit zone)" : ""));
        TrollDefend.UnderAttack(Target(nearest.GetComponent<MonsterAI>()) == troll
            ? TrollOrders.Shown(troll)
            : owner.GetPlayerName()); // D-068: the one that is attacked
        return true;
    }
}
