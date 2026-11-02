using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Combat.Domain;
using SteveTheTroll.Trolls.Farming.Infrastructure;
using SteveTheTroll.Trolls.Orders.Commands;
using SteveTheTroll.Trolls.Orders.Infrastructure;
using SteveTheTroll.Trolls.Registry.Infrastructure;
using SteveTheTroll.Trolls.Taming.Domain;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Trolls.Combat.Infrastructure
{
    /// <summary>
    /// D-124 (user 2026-10-28: "2 star break Ashlands fortress walls"; "They can go for the walls if they consider an enemy behind / on top of
    /// the wall as a target and the wall is in the way, also if the player is attacking the walls ... or with mouse4"):
    ///  - a hit of a tamed troll with 2 stars (level 3+) on an Ashlands fortress wall piece (BreakRules.FortressWall) counts as tool tier
    ///    TamingRules.TamedToolTier with the highest of its blunt / chop / pickaxe as chop and pickaxe - its own swings at a wall in its
    ///    way to an enemy break it;
    ///  - TrollOrders (mouse4) sends a 2-star troll at a fortress wall the player looks at (Order);
    ///  - the local player hitting a fortress wall sends the own 2-star trolls within DefendRange that are not fighting at it (Assist).
    /// The player's own hits (Troll Club included) are not changed: the club still cannot damage these walls.
    /// </summary>
    internal static class TrollWalls
    {
        const float AssistSeconds = 5f;
        static float _nextAssist, _nextLog;

        /// The troll may break fortress walls (tamed, level 3+). Expected outcome: true for a tamed 2-star troll.
        internal static bool Breaks(Character troll)
        {
            return troll != null && TrollTaming.IsTroll(troll) && troll.IsTamed() && BreakRules.BreaksWalls(troll.GetLevel());
        }

        /// The object is an Ashlands fortress wall piece (a WearNTear). Expected outcome: true for Ashlands_Wall_2x2 and the like.
        internal static bool IsWall(GameObject g)
        {
            return g != null && g.GetComponent<WearNTear>() != null && BreakRules.FortressWall(g.name);
        }

        /// Mouse4 on a wall: the troll walks to it and swings until it breaks. Expected outcome: whether the troll took the order.
        internal static bool Order(Character troll, GameObject wall, string focus)
        {
            if (!Breaks(troll) || !IsWall(wall)) { ModLog.Info("TrollWalls: " + (troll != null ? TrollTaming.Tag(troll) : "?") + " (level " + (troll != null ? troll.GetLevel() : 0) + ") cannot break " + (wall != null ? wall.name : "?") + " - only 2-star trolls break fortress walls"); return false; }
            bool ok = TrollOrderService.Send(new BreakSourceCommand(troll, wall, focus));
            ModLog.Info("TrollWalls: " + TrollTaming.Tag(troll) + " sent at fortress wall " + wall.name + " " + ok);
            return ok;
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
        static class WallHit
        {
            static void Prefix(WearNTear __instance, HitData hit)
            {
                if (hit == null || __instance == null || !ModSettings.On || !BreakRules.FortressWall(__instance.name)) return;
                var attacker = hit.GetAttacker();
                if (attacker == null) return;
                if (attacker == Player.m_localPlayer) { Assist(__instance.gameObject); return; }
                if (!Breaks(attacker)) return;
                short before = hit.m_toolTier;
                float dmg = BreakRules.BreakDamage(hit.m_damage.m_blunt, hit.m_damage.m_chop, hit.m_damage.m_pickaxe);
                hit.m_toolTier = (short)TamingRules.TamedToolTier;
                hit.m_damage.m_chop = dmg;
                hit.m_damage.m_pickaxe = dmg;
                if (Time.time >= _nextLog)
                {
                    _nextLog = Time.time + 3f;
                    ModLog.Info("TrollWalls: " + attacker.name + " (level " + attacker.GetLevel() + ") hits fortress wall " + __instance.name + ": tool tier " + before + " -> " + hit.m_toolTier + ", chop / pickaxe " + dmg.ToString("0"));
                }
            }
        }

        // The local player hits a fortress wall: own 2-star trolls near the player that are not fighting go for it (at most every 5 s).
        static int Assist(GameObject wall)
        {
            if (Time.time < _nextAssist) return 0;
            _nextAssist = Time.time + AssistSeconds;
            var p = Player.m_localPlayer;
            int sent = 0;
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || !Breaks(c) || TrollTaming.Owner(c) != p.GetPlayerID()) continue;
                if (Vector3.Distance(c.transform.position, p.transform.position) > ModSettings.DefendRange.Value) continue;
                var ai = c.GetComponent<MonsterAI>();
                var view = c.GetComponent<ZNetView>();
                if (ai == null || ai.GetTargetCreature() != null || view == null || !view.IsValid() || !view.IsOwner()) continue;
                string focus = Ui.TrollFocus.Normalize(view.GetZDO().GetString(TrollRegistry.FocusKey, ""));
                if (TrollOrderService.Send(new BreakSourceCommand(c, wall, focus))) sent++;
            }
            if (sent > 0) ModLog.Info("TrollWalls: " + p.GetPlayerName() + " hits fortress wall " + wall.name + " - " + sent + " own 2-star troll(s) join in");
            return sent;
        }
    }
}
