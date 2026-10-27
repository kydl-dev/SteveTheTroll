using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
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
                Defend(player, attacker);
            }
        }

        /// Expected outcome: returns how many trolls now target the attacker.
        internal static int Defend(Player owner, Character attacker)
        {
            long id = owner.GetPlayerID();
            Near.Clear();
            foreach (var c in Character.GetAllCharacters())
                if (c != null && !c.IsDead() && TamedTrollStats.IsTamedTroll(c) && TrollTaming.Owner(c) == id) Near.Add(c);
            int set = 0;
            foreach (var troll in Near)
            {
                float d = Vector3.Distance(troll.transform.position, owner.transform.position);
                if (!TamingRules.Defends(d, ModSettings.DefendRange.Value)) { ModLog.Verbose("TrollDefend: " + troll.name + " is " + d.ToString("F1") + " m from " + owner.GetPlayerName() + " (range " + ModSettings.DefendRange.Value + ") - stays"); continue; }
                var view = troll.GetComponent<ZNetView>();
                var ai = troll.GetComponent<MonsterAI>();
                if (ai == null || view == null || !view.IsValid() || !view.IsOwner()) { ModLog.Verbose("TrollDefend: " + troll.name + " is owned by another client - not steered here"); continue; }
                SetTarget(ai, attacker);
                ai.Alert();
                set++;
                ModLog.Info("TrollDefend: " + owner.GetPlayerName() + " hit by " + attacker.name + " - " + troll.name + " (" + d.ToString("F1") + " m) attacks it");
            }
            ModLog.Verbose("TrollDefend: " + Near.Count + " own tamed troll(s) loaded, " + set + " defending");
            return set;
        }
    }
}
