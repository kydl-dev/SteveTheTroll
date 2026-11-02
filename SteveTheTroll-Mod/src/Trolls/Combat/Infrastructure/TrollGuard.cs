using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Combat.Domain;
using SteveTheTroll.Trolls.Farming.Infrastructure;
using SteveTheTroll.Trolls.Home.Infrastructure;
using SteveTheTroll.Trolls.Orders.Infrastructure;
using SteveTheTroll.Trolls.Taming.Domain;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Trolls.Combat.Infrastructure
{
    /// <summary>
    /// D-065 (user: "if a mob, any mob is attacking either the tamed troll or the player, instantly target that mob"): 4 times a second, on
    /// the client that owns it, a tamed troll looks for creatures within DefendRange that target its owner or itself. One or a few -> it
    /// attacks the nearest of them at once (and keeps that one while it still attacks). CrowdCount or more -> TrollSlam (4 ground slams),
    /// then the nearest one left; again at most every SlamCooldown seconds.
    /// </summary>
    internal static class TrollGuard
    {
        const float TickSeconds = 0.25f;
        static readonly System.Action<MonsterAI, Character> SetTarget = AccessTools.MethodDelegate<System.Action<MonsterAI, Character>>(AccessTools.Method(typeof(MonsterAI), "SetTarget"));
        static readonly AccessTools.FieldRef<MonsterAI, Character> Target = AccessTools.FieldRefAccess<MonsterAI, Character>("m_targetCreature");
        // D-066 (TEST-029: the target was set again 4x a second - the game's AI dropped it for its own choice / out of sight): the creature
        // the troll must fight, re-applied before each AI step (Keep) until it is dead, tamed or further than KeepRange.
        static readonly Dictionary<int, Character> Forced = new Dictionary<int, Character>();
        const float KeepRange = 70f;
        static readonly List<Character> Attackers = new List<Character>();
        static float _next;

        /// Plugin.Update. Expected outcome: how many trolls changed their target this time.
        internal static int Tick()
        {
            if (Time.time < _next || !ModSettings.On) return 0;
            _next = Time.time + TickSeconds;
            var me = Player.m_localPlayer;
            if (me == null) return 0;
            int changed = 0;
            // D-141 (performance review): ONE pass over all characters per tick finds both the owner's trolls and the creatures that go for the owner; every troll then
            // checks only that short list (it used to walk the whole character list again for each troll: 7 passes x 4 a second with six trolls).
            Mine.Clear(); Hunters.Clear();
            long myId = me.GetPlayerID();
            var all = Character.GetAllCharacters();
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            foreach (var c in all)
            {
                if (c == null || c.IsDead()) continue;
                if (c.IsTamed())
                {
                    if (!TrollTaming.IsTroll(c) || TrollTaming.Owner(c) != myId) continue;
                    var view = c.GetComponent<ZNetView>();
                    var ai = c.GetComponent<MonsterAI>();
                    if (ai == null || view == null || !view.IsValid() || !view.IsOwner() || c.GetComponent<TrollSlam>() != null) continue;
                    Mine.Add(new KeyValuePair<Character, MonsterAI>(c, ai));
                    continue;
                }
                if (c.IsPlayer()) continue;
                var mai = c.GetComponent<MonsterAI>();
                if (mai != null && Target(mai) == me) Hunters.Add(c);          // D-084: creatures that go for the troll itself: TrollCounter (on its hits)
            }
            if (Hunters.Count > 0) foreach (var m in Mine) if (Guard(m.Key, m.Value, me)) changed++;
            _ticks++; _chars += all.Count; _trolls += Mine.Count; _hunters += Hunters.Count;
            _scanTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            if (ModLog.DebugOn && Time.time >= _nextStats)
            {
                _nextStats = Time.time + 5f;
                ModLog.Verbose("TrollGuard: last 5 s - " + _ticks + " tick(s), avg " + (_ticks > 0 ? _chars / _ticks : 0) + " character(s), " + (_ticks > 0 ? _trolls / (float)_ticks : 0).ToString("F1") + " own troll(s), "
                    + (_ticks > 0 ? _hunters / (float)_ticks : 0).ToString("F1") + " creature(s) after the owner; one pass took " + (_ticks > 0 ? _scanTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / _ticks : 0).ToString("F3") + " ms on average");
                _ticks = _chars = _trolls = _hunters = 0; _scanTicks = 0;
            }
            return changed;
        }

        static readonly List<KeyValuePair<Character, MonsterAI>> Mine = new List<KeyValuePair<Character, MonsterAI>>();
        static readonly List<Character> Hunters = new List<Character>();
        static int _ticks, _chars, _trolls, _hunters;
        static long _scanTicks;
        static float _nextStats;

        /// The attack key (TrollOrders) or a hit on the troll: attack this creature now. Expected outcome: whether the target was set.
        internal static bool Order(Character troll, Character creature)
        {
            var ai = troll != null ? troll.GetComponent<MonsterAI>() : null;
            var view = troll != null ? troll.GetComponent<ZNetView>() : null;
            if (ai == null || creature == null || view == null || !view.IsValid() || !view.IsOwner()) return false;
            Trim();
            Forced[troll.GetInstanceID()] = creature;
            if (Target(ai) == creature) return false;
            SetTarget(ai, creature);
            ai.Alert();
            ModLog.Info("TrollGuard: " + TrollTaming.Tag(troll) + " ordered / hit - attacks " + creature.name + " (" + Vector3.Distance(troll.transform.position, creature.transform.position).ToString("F1") + " m)");
            return true;
        }

        /// D-084: the troll's forced target is dropped (its counter is over / it ignores hits). Expected outcome: whether one was dropped.
        // D-171 (P9 audit): entries of destroyed trolls were never removed; past MaxForced the map starts again (a live forced target is set again within a tick by Guard / Order).
        const int MaxForced = 200;
        static int Trim()
        {
            if (Forced.Count <= MaxForced) return Forced.Count;
            ModLog.Verbose("TrollGuard: " + Forced.Count + " forced targets kept - cleared (stale entries of removed trolls)");
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
            if (ai == null || Target(ai) == null) return false;
            Target(ai) = null;
            return true;
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
            if (id == 0 || !Forced.TryGetValue(id, out t)) return false;
            if (t == null || t.IsDead() || t.IsTamed() || Vector3.Distance(t.transform.position, ai.transform.position) > KeepRange)
            {
                Forced.Remove(id);
                ModLog.Verbose("TrollGuard: " + TrollTaming.Tag(ai) + " forced target gone (" + (t == null ? "removed" : t.IsDead() ? "dead" : "tamed / far") + ")");
                return false;
            }
            if (Target(ai) != t) Target(ai) = t;
            return true;
        }

        static bool Guard(Character troll, MonsterAI ai, Player owner)
        {
            float range = ModSettings.DefendRange.Value;
            bool wears = TrollTaming.Wears(owner), raid = !wears && TrollHome.Raid();
            // D-078 (TEST-032: Spencer at the house stayed 'fighting' while the player hunted 80 m away - a target near the player was set,
            // dropped as too far (KeepRange) and set again 4x a second, so he never farmed): the player's attackers count only while the troll
            // itself is within DefendRange of the player (as TrollDefend), and no attacker further than KeepRange from the troll
            bool ownerNear = Vector3.Distance(owner.transform.position, troll.transform.position) <= range;
            Attackers.Clear();
            var trollPos = troll.transform.position;
            var ownerPos = owner.transform.position;
            float keep2 = KeepRange * KeepRange, range2 = range * range, guard2 = ModSettings.GuardOwnerRange.Value * ModSettings.GuardOwnerRange.Value;
            bool defends = TamingRules.DefendsOwner(wears, raid);
            foreach (var m in Hunters)
            {
                if (m == null || m.IsDead() || m.IsTamed()) continue;        // Hunters were collected this tick; one may have died / been tamed since
                // T-008e: slippers off -> it defends the player only during a raid; D-078: only while the troll is near the player; D-081 (TEST-034: a farming Steve ran 60 m back
                // home to a greydwarf that only aimed at the player): a creature that goes for the player is taken on only when it is near the troll. D-171: GuardRules decides.
                var mp = m.transform.position;
                if (!GuardRules.Eligible(defends, ownerNear, (mp - trollPos).sqrMagnitude, (mp - ownerPos).sqrMagnitude, guard2, keep2, range2)) continue;
                Attackers.Add(m);
            }
            if (Attackers.Count == 0) return false;
            int id = troll.GetInstanceID();
            // D-084: no crowd slams here any more (TrollCounter answers hits on the troll, TrollRage hits on the player)
            Character cur;
            Forced.TryGetValue(id, out cur);
            if (cur != null && Attackers.Contains(cur)) return false;     // keeps the one it fights while that one still attacks
            Character nearest = null;
            float best = float.MaxValue;
            foreach (var m in Attackers)
            {
                float d = Vector3.Distance(m.transform.position, troll.transform.position);
                if (d < best) { best = d; nearest = m; }
            }
            Trim();
            Forced[id] = nearest;
            SetTarget(ai, nearest);
            ai.Alert();
            var farmer = troll.GetComponent<TrollFarmer>();
            ModLog.Info("TrollGuard: " + TrollTaming.Tag(troll) + " attacks " + nearest.name + " (" + best.ToString("F1") + " m) - it targets " + (Target(nearest.GetComponent<MonsterAI>()) == troll ? "the troll" : owner.GetPlayerName())
                + ", " + Attackers.Count + " attacker(s)" + (farmer != null ? ", farming paused" : ""));
            TrollDefend.UnderAttack(Target(nearest.GetComponent<MonsterAI>()) == troll ? TrollOrders.Shown(troll) : owner.GetPlayerName());     // D-068: the one that is attacked
            return true;
        }
    }
}
