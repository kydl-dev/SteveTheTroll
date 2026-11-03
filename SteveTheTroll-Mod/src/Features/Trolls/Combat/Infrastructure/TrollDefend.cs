using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Combat.Application;
using SteveTheTroll.Features.Trolls.Combat.Domain;
using SteveTheTroll.Features.Trolls.Combat.Execution;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Combat.Infrastructure
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

        // Harmony hook: src/Integrations/Harmony/TrollsCombatPatches.cs, TrollDefend_PlayerHit (MIGRATION_DECISIONS A5)
        internal static class PlayerHit
        {
            internal static void Postfix(Character __instance, HitData hit)
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
            return Notify.Show(Loc.T("stevethetroll_under_attack", who), Notify.Kind.Attack);
        }

        static readonly DefencePipeline Defence = new DefencePipeline();
        static readonly CombatExecutor Executor = new CombatExecutor(ForceCurrent, SetCurrent);
        static Character _troll, _attacker;
        static MonsterAI _ai;
        static bool ForceCurrent() { return TrollGuard.Order(_troll, _attacker); }
        static void SetCurrent() { SetTarget(_ai, _attacker); _ai.Alert(); }

        /// ITrollOwnership over one troll's ZNetView (D-175).
        internal readonly struct ViewOwner : ITrollOwnership
        {
            readonly ZNetView _v;
            internal ViewOwner(ZNetView v) { _v = v; }
            public bool IsValid { get { return _v != null && _v.IsValid(); } }
            public bool IsOwner { get { return IsValid && _v.IsOwner(); } }
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
            bool wears = TrollTaming.Wears(owner), raid = !wears && TrollHome.Raid();
            foreach (var troll in Near)
            {
                float d = Vector3.Distance(troll.transform.position, owner.transform.position);
                var view = troll.GetComponent<ZNetView>();
                var ai = troll.GetComponent<MonsterAI>();
                // D-175 (Architecture V2, P5): DefencePipeline decides (slippers / raid, range, owner), CombatExecutor performs (owner-gated): the attacker becomes the troll's
                // FORCED target (D-082: TrollGuard.Keep puts it back before every AI step), else the game's SetTarget + Alert
                var decision = Defence.Evaluate(new DefenceInput(ai != null && view != null && view.IsValid() && view.IsOwner(), wears, raid, d, ModSettings.DefendRange.Value));
                if (!decision.IsSuccess) { ModLog.Verbose("TrollDefend: " + TrollTaming.Tag(troll) + " (" + d.ToString("F1") + " m from " + owner.GetPlayerName() + ") does not defend - " + decision.Reason); continue; }
                _troll = troll; _ai = ai; _attacker = attacker;
                var done = Executor.Attack(new ViewOwner(view));
                _troll = null; _ai = null; _attacker = null;
                if (!done.IsSuccess) continue;
                set++;
                ModLog.Info("TrollDefend: " + owner.GetPlayerName() + " hit by " + attacker.name + " - " + TrollTaming.Tag(troll) + " (" + d.ToString("F1") + " m) attacks it (" + done.Value + ")");
            }
            ModLog.Verbose("TrollDefend: " + Near.Count + " own tamed troll(s) loaded, " + set + " defending");
            if (set > 0 && owner == Player.m_localPlayer) UnderAttack(owner.GetPlayerName());
            return set;
        }
    }
}
