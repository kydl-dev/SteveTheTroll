using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Combat.Domain;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Trolls.Combat.Infrastructure
{
    /// <summary>
    /// D-097 (user: "set no hit zone 15m from bed"): the local player's own tamed trolls never swing within NoHitRadius of the player's bed
    /// (flat distance) - no farming, slams, rage or the game's own attacks there - and nothing within that circle is picked as a farm source.
    /// Wild trolls are not touched.
    /// </summary>
    internal static class TrollNoHit
    {
        static float _nextLog;
        static int _blocked;

        /// The local player's bed (false when none is set).
        internal static bool Bed(out Vector3 bed)
        {
            bed = Vector3.zero;
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (profile == null || !profile.HaveCustomSpawnPoint()) return false;
            bed = profile.GetCustomSpawnPoint();
            return true;
        }

        /// Pure: inside the no-hit circle around the bed (flat, inclusive). Expected outcome: false for radius 0 (off).
        internal static bool Inside(Vector3 bed, Vector3 pos, float radius)
        {
            return NoHitRules.Inside(bed.x, bed.z, pos.x, pos.z, radius);      // D-171: the rule is game-free
        }

        /// The position lies in the local player's no-hit circle.
        internal static bool InZone(Vector3 pos)
        {
            Vector3 bed;
            return ModSettings.On && Bed(out bed) && Inside(bed, pos, ModSettings.NoHitRadius.Value);
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        static class NoSwing
        {
            static bool Prefix(Humanoid __instance, ref bool __result)
            {
                if (__instance == null || !__instance.IsTamed() || !TrollTaming.IsTroll(__instance)) return true;
                var p = Player.m_localPlayer;
                if (p == null || TrollTaming.Owner(__instance) != p.GetPlayerID() || !InZone(__instance.transform.position)) return true;
                __result = false;
                _blocked++;
                if (Time.time >= _nextLog)
                {
                    _nextLog = Time.time + 10f;
                    ModLog.Verbose("TrollNoHit: " + TrollTaming.Tag(__instance) + " is within " + ModSettings.NoHitRadius.Value.ToString("0") + " m of the bed - no swing (" + _blocked + " swing(s) stopped so far)");
                }
                return false;
            }
        }
    }
}
