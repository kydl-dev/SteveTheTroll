using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-065 (user: "if a mob, any mob is attacking either the tamed troll or the player, instantly target that mob"): 4 times a second, on
    /// the client that owns it, a tamed troll looks for creatures within DefendRange that target its owner or itself. One or a few -> it
    /// attacks the nearest of them at once (and keeps that one while it still attacks). CrowdCount or more -> TrollSlam (4 ground slams),
    /// then the nearest one left; again at most every SlamCooldown seconds.
    /// </summary>
    internal static class TrollGuard
    {
        const float TickSeconds = 0.25f, SlamCooldown = 15f;
        static readonly System.Action<MonsterAI, Character> SetTarget = AccessTools.MethodDelegate<System.Action<MonsterAI, Character>>(AccessTools.Method(typeof(MonsterAI), "SetTarget"));
        static readonly AccessTools.FieldRef<MonsterAI, Character> Target = AccessTools.FieldRefAccess<MonsterAI, Character>("m_targetCreature");
        static readonly Dictionary<int, float> LastSlam = new Dictionary<int, float>();
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
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c) || TrollTaming.Owner(c) != me.GetPlayerID()) continue;
                var view = c.GetComponent<ZNetView>();
                var ai = c.GetComponent<MonsterAI>();
                if (ai == null || view == null || !view.IsValid() || !view.IsOwner() || c.GetComponent<TrollSlam>() != null) continue;
                if (Guard(c, ai, me)) changed++;
            }
            return changed;
        }

        /// The attack key (TrollOrders) or a hit on the troll: attack this creature now. Expected outcome: whether the target was set.
        internal static bool Order(Character troll, Character creature)
        {
            var ai = troll != null ? troll.GetComponent<MonsterAI>() : null;
            var view = troll != null ? troll.GetComponent<ZNetView>() : null;
            if (ai == null || creature == null || view == null || !view.IsValid() || !view.IsOwner()) return false;
            if (Target(ai) == creature) return false;
            SetTarget(ai, creature);
            ai.Alert();
            ModLog.Info("TrollGuard: " + troll.name + " ordered / hit - attacks " + creature.name + " (" + Vector3.Distance(troll.transform.position, creature.transform.position).ToString("F1") + " m)");
            return true;
        }

        static bool Guard(Character troll, MonsterAI ai, Player owner)
        {
            float range = ModSettings.DefendRange.Value;
            bool wears = TrollTaming.Wears(owner), raid = !wears && TrollHome.Raid();
            Attackers.Clear();
            Vector3 sum = Vector3.zero;
            foreach (var m in Character.GetAllCharacters())
            {
                if (m == null || m.IsDead() || m.IsPlayer() || m.IsTamed() || m == troll) continue;
                var mai = m.GetComponent<MonsterAI>();
                if (mai == null) continue;
                var t = Target(mai);
                if (t != owner && t != troll) continue;
                if (t == owner && !TamingRules.DefendsOwner(wears, raid)) continue;            // T-008e: slippers off -> it defends the player only during a raid
                if (Vector3.Distance(m.transform.position, troll.transform.position) > range && Vector3.Distance(m.transform.position, owner.transform.position) > range) continue;
                Attackers.Add(m);
                sum += m.transform.position;
            }
            if (Attackers.Count == 0) return false;
            int id = troll.GetInstanceID();
            float last;
            if (TamingRules.Slam(Attackers.Count, ModSettings.CrowdCount.Value, LastSlam.TryGetValue(id, out last) ? Time.time - last : -1f, SlamCooldown))
            {
                LastSlam[id] = Time.time;
                if (TrollSlam.Begin(troll, owner, sum / Attackers.Count, Attackers.Count) != null) return true;
            }
            var cur = Target(ai);
            if (cur != null && Attackers.Contains(cur)) return false;     // keeps the one it fights while that one still attacks
            Character nearest = null;
            float best = float.MaxValue;
            foreach (var m in Attackers)
            {
                float d = Vector3.Distance(m.transform.position, troll.transform.position);
                if (d < best) { best = d; nearest = m; }
            }
            SetTarget(ai, nearest);
            ai.Alert();
            var farmer = troll.GetComponent<TrollFarmer>();
            ModLog.Info("TrollGuard: " + troll.name + " attacks " + nearest.name + " (" + best.ToString("F1") + " m) - it targets " + (Target(nearest.GetComponent<MonsterAI>()) == troll ? "the troll" : owner.GetPlayerName())
                + ", " + Attackers.Count + " attacker(s)" + (farmer != null ? ", farming paused" : ""));
            if (Attackers.Count > 0) TrollDefend.UnderAttack(owner.GetPlayerName());
            return true;
        }
    }
}
