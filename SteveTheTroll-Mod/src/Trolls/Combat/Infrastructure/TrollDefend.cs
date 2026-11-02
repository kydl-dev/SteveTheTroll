using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Taming;
using UnityEngine;

namespace SteveTheTroll.Trolls.Combat.Infrastructure
{
    /// <summary>
    /// D-042: when a player is hit by a creature, that player's own tamed trolls within DefendRange drop follow / farm and attack the attacker
    /// (MonsterAI: a target creature wins over the follow target in UpdateAI). Runs where the player is damaged (the player's own client);
    /// a troll is steered only by the client that owns its ZDO (logged when it is another one).
    /// </summary>
    internal static class TrollDefend
    {
        static readonly System.Action<MonsterAI, Character> SetTarget = AccessTools.MethodDelegate<System.Action<MonsterAI, Character>>(AccessTools.Method(typeof(MonsterAI), "SetTarget"));
        static readonly List<Character> Near = new List<Character>();

        /// In-game test Startup_TamingReflection: runs this class's reflection lookup. Expected outcome: whether it was found.
        internal static bool SelfCheck() { return SetTarget != null; }

        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
        static class PlayerHit
        {
            static void Postfix(Character __instance, HitData hit)
            {
                var player = __instance as Player;
                if (player == null || hit == null || !ModSettings.On) return;
                var attacker = hit.GetAttacker();
                if (attacker == null || attacker == __instance || attacker.IsPlayer() || attacker.IsTamed()) return;
                TrollRage.OwnerHit(player, attacker);       // D-082: trolls within 10 m go into rage
                Defend(player, attacker);
            }
        }

        static readonly System.Collections.Generic.Dictionary<string, float> LastWarn = new System.Collections.Generic.Dictionary<string, float>();
        const float WarnEverySeconds = 10f;

        /// D-048: "X is under attack. Attacking back." (red, left), at most every WarnEverySeconds per name. Expected outcome: whether shown.
        internal static bool UnderAttack(string who)
        {
            float last;
            if (LastWarn.TryGetValue(who, out last) && Time.time - last < WarnEverySeconds) return false;
            LastWarn[who] = Time.time;
            return Ui.Notify.Show(Loc.T("stevethetroll_under_attack", who), Ui.Notify.Kind.Attack);
        }

        /// Expected outcome: returns how many trolls now target the attacker.
        internal static int Defend(Player owner, Character attacker)
        {
            long id = owner.GetPlayerID();
            if (!TrollTaming.Wears(owner) && !TrollHome.Raid()) { ModLog.Verbose("TrollDefend: " + owner.GetPlayerName() + " has no slippers on and there is no raid - trolls stay at the bed"); return 0; }
            Near.Clear();
            foreach (var c in Character.GetAllCharacters())
                if (c != null && !c.IsDead() && TamedTrollStats.IsTamedTroll(c) && TrollTaming.Owner(c) == id) Near.Add(c);
            int set = 0;
            foreach (var troll in Near)
            {
                float d = Vector3.Distance(troll.transform.position, owner.transform.position);
                if (!TamingRules.Defends(d, ModSettings.DefendRange.Value)) { ModLog.Verbose("TrollDefend: " + TrollTaming.Tag(troll) + " is " + d.ToString("F1") + " m from " + owner.GetPlayerName() + " (range " + ModSettings.DefendRange.Value + ") - stays"); continue; }
                var view = troll.GetComponent<ZNetView>();
                var ai = troll.GetComponent<MonsterAI>();
                if (ai == null || view == null || !view.IsValid() || !view.IsOwner()) { ModLog.Verbose("TrollDefend: " + TrollTaming.Tag(troll) + " is owned by another client - not steered here"); continue; }
                // D-082 (TEST-035: Steve 1 stood 2.5 m from two wild trolls hitting the player and did nothing - the game's AI dropped / ignored the
                // target): the attacker becomes the troll's FORCED target (TrollGuard.Keep puts it back before every AI step)
                if (!TrollGuard.Order(troll, attacker)) { SetTarget(ai, attacker); ai.Alert(); }
                set++;
                ModLog.Info("TrollDefend: " + owner.GetPlayerName() + " hit by " + attacker.name + " - " + TrollTaming.Tag(troll) + " (" + d.ToString("F1") + " m) attacks it");
            }
            ModLog.Verbose("TrollDefend: " + Near.Count + " own tamed troll(s) loaded, " + set + " defending");
            if (set > 0 && owner == Player.m_localPlayer) UnderAttack(owner.GetPlayerName());
            return set;
        }
    }
}
