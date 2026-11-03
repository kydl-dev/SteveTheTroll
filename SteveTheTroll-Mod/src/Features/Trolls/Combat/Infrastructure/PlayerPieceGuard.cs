using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Combat.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;

namespace SteveTheTroll.Features.Trolls.Combat.Infrastructure
{
    /// <summary>
    /// D-172 (user 2026-11-02: "Protect player pieces"): a tamed troll never damages a piece a player built. The troll's swings hit everything in their area - a rage
    /// at a rock next to a player's wall damaged the wall (run 151111). WearNTear.Damage is where every hit on a building piece arrives (before the RPC to the piece's
    /// owner), so a troll's hit on a player-built piece is dropped there. The decision is game-free (BreakRules.TrollHitDamagesPiece).
    /// </summary>
    internal static class PlayerPieceGuard
    {
        static readonly HashSet<int> Said = new HashSet<int>();

        // Harmony hook: src/Integrations/Harmony/TrollsCombatPatches.cs, PlayerPieceGuard_GuardDamage (MIGRATION_DECISIONS A5)
        internal static class GuardDamage
        {
            internal static bool Prefix(WearNTear __instance, HitData hit)
            {
                if (hit == null || __instance == null || !ModSettings.On) return true;
                var attacker = hit.GetAttacker();
                bool troll = attacker != null && attacker.IsTamed() && TrollTaming.IsTroll(attacker);
                if (!troll) return true;
                var piece = __instance.GetComponent<Piece>();
                bool playerBuilt = piece != null && piece.IsPlacedByPlayer();
                if (BreakRules.TrollHitDamagesPiece(troll, playerBuilt)) return true;
                if (Said.Count > 500) Said.Clear();
                if (Said.Add(__instance.GetInstanceID()))
                    ModLog.Info("PlayerPieceGuard: " + TrollTaming.Tag(attacker) + " hit the player-built " + __instance.name + " - no damage (D-172)");
                return false;
            }
        }
    }
}
