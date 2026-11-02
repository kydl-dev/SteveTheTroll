using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Combat.Domain;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-131 (user 2026-10-29: "when they enter water, they are to keep 15m away from the player at all times so that the player can have
    /// space to steer the boat, and they don't break the boat"):
    ///  - a tamed troll that follows a player and is in water (BreakRules.InWater: swimming or deeper than knee deep) keeps WaterKeepDistance
    ///    (15 m) from that player: BaseAI.Follow prefix - on the ring (+-1.5 m) it stands still, closer it backs off to the ring, farther it
    ///    goes to the ring point on its own side of the player (the D-130 swim catch-up counts the distance behind the ring). On land the
    ///    game's follow (stops at 3 m) runs unchanged;
    ///  - tamed trolls never damage a ship (WearNTear.Damage prefix: attacker a tamed troll, piece part of a Ship -> the hit is dropped).
    /// </summary>
    internal static class TrollWaterKeep
    {
        static readonly System.Func<BaseAI, float, Vector3, float, bool, bool> MoveTo =
            AccessTools.MethodDelegate<System.Func<BaseAI, float, Vector3, float, bool, bool>>(AccessTools.Method(typeof(BaseAI), "MoveTo"));
        static float _nextLog, _nextShipLog;
        static string _lastState = "";

        /// In-game test Startup_TamingReflection: BaseAI.MoveTo was found. Expected outcome: true.
        internal static bool SelfCheck() { return MoveTo != null; }

        /// The troll keeps its distance now: tamed troll, the rule on (distance &gt; 0), in water. Expected outcome: true while in water.
        internal static bool Keeps(Character c)
        {
            if (c == null || !ModSettings.On || ModSettings.WaterKeepDistance.Value <= 0f || !TamedTrollStats.IsTamedTroll(c)) return false;
            return BreakRules.InWater(c.GetLiquidLevel() - c.transform.position.y, c.IsSwimming());
        }

        /// The ring point: `keep` metres from the player towards the troll (flat), at the troll's height. Expected outcome: a world point.
        internal static Vector3 RingPoint(Vector3 troll, Vector3 player, float keep)
        {
            var dir = troll - player; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            var p = player + dir.normalized * keep;
            p.y = troll.y;
            return p;
        }

        [HarmonyPatch(typeof(BaseAI), "Follow")]
        static class KeepAway
        {
            static bool Prefix(BaseAI __instance, GameObject go, float dt)
            {
                if (go == null || MoveTo == null || go.GetComponent<Player>() == null) return true;
                var c = __instance.GetComponent<Character>();
                if (!Keeps(c)) return true;
                float keep = ModSettings.WaterKeepDistance.Value;
                var flat = c.transform.position - go.transform.position; flat.y = 0f;
                float d = flat.magnitude;
                string state;
                if (BreakRules.HoldsRing(d, keep)) { __instance.StopMoving(); state = "holds"; }
                else
                {
                    MoveTo(__instance, dt, RingPoint(c.transform.position, go.transform.position, keep), 0.5f, true);
                    state = d < keep ? "backs off" : "follows to";
                }
                if (state != _lastState || Time.time >= _nextLog)
                {
                    _lastState = state; _nextLog = Time.time + 5f;
                    ModLog.Verbose("TrollWaterKeep: " + TrollTaming.Tag(c) + " in water (swimming " + c.IsSwimming() + ") " + state + " the " + keep.ToString("0") + " m ring around "
                        + go.GetComponent<Player>().GetPlayerName() + " (now " + d.ToString("F1") + " m)");
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
        static class NoShipDamage
        {
            [HarmonyPriority(Priority.First)]
            static bool Prefix(WearNTear __instance, HitData hit)
            {
                if (hit == null || __instance == null || !ModSettings.On) return true;
                var a = hit.GetAttacker();
                if (a == null || !TamedTrollStats.IsTamedTroll(a) || __instance.GetComponentInParent<Ship>() == null) return true;
                if (Time.time >= _nextShipLog)
                {
                    _nextShipLog = Time.time + 3f;
                    ModLog.Info("TrollWaterKeep: " + a.name + " hit the ship " + __instance.GetComponentInParent<Ship>().name + " (" + __instance.name + ") - no damage (D-131)");
                }
                return false;
            }
        }
    }
}
