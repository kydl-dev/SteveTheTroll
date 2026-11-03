using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Orders.Commands;
using SteveTheTroll.Features.Trolls.Orders.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Home.Infrastructure
{
    /// <summary>
    /// T-008e (D-038 / D-065, user: "slippers off or logout -> the troll waits at the bed, but defends during a raid"): while its owner does
    /// not wear the slippers, an own tamed troll stops following / farming, walks to the owner's bed and waits there (an E follow order is
    /// undone; no auto-farming). It defends the owner only during a raid (TrollGuard / TrollDefend); it always fights back when hit.
    /// On logout every own loaded tamed troll is put at the bed at once (nothing can walk after the player has left).
    /// </summary>
    internal static class TrollHome
    {
        static readonly System.Action<BaseAI, Vector3> SetPatrolAt = AccessTools.MethodDelegate<System.Action<BaseAI, Vector3>>(AccessTools.Method(typeof(BaseAI), "SetPatrolPoint", new[] { typeof(Vector3) }));
        static readonly HashSet<int> Sent = new HashSet<int>();
        static float _next;
        static bool _wore = true;

        /// The owner wears the slippers (no owner online here -> true: nothing to decide on this client).
        internal static bool OwnerWears(Character troll)
        {
            var p = Player.m_localPlayer;
            if (p == null || TrollTaming.Owner(troll) != p.GetPlayerID()) return true;
            return TrollTaming.Wears(p);
        }

        /// A raid (the game's random event) is running. Expected outcome: true during a raid.
        internal static bool Raid() { return RandEventSystem.HaveActiveEvent(); }

        /// Plugin.Update, once a second. Expected outcome: how many trolls were sent to the bed this time.
        internal static int Tick()
        {
            if (Time.time < _next || !ModSettings.On) return 0;
            _next = Time.time + 1f;
            var p = Player.m_localPlayer;
            if (p == null) return 0;
            bool wears = TrollTaming.Wears(p);
            if (wears != _wore) { _wore = wears; ModLog.Info("TrollHome: " + p.GetPlayerName() + (wears ? " wears the slippers again - trolls work as usual" : " took the slippers off - own trolls go to the bed")); if (wears) TrollIdle.ResumeNow("slippers on again"); }
            if (wears) { Sent.Clear(); return 0; }
            int sent = 0;
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c) || TrollTaming.Owner(c) != p.GetPlayerID()) continue;
                var view = c.GetComponent<ZNetView>();
                var ai = c.GetComponent<MonsterAI>();
                if (ai == null || view == null || !view.IsValid() || !view.IsOwner()) continue;
                int id = c.GetInstanceID();
                if (Sent.Contains(id))
                {
                    if (ai.GetFollowTarget() != null) { TrollTaming.StopFollow(ai); ModLog.Info("TrollHome: " + TrollTaming.Tag(c) + " was told to follow without slippers - goes back to the bed"); Sent.Remove(id); }
                    else continue;
                }
                Sent.Add(id);
                TrollTaming.StopFollow(ai);
                if (TrollOrderService.Send(new SendHomeCommand(c))) { sent++; Notify.Show(Loc.T("stevethetroll_home_noslippers", TrollOrders.Shown(c)), Notify.Kind.Good); }
            }
            return sent;
        }

        static readonly HashSet<ZDOID> Done = new HashSet<ZDOID>();

        /// D-081 (TEST-034: 2 of 5 trolls, 110 m and 180 m from the bed, stayed where they were): own tamed trolls that are not loaded around the
        /// player are moved through their saved data (ZDO position, follow cleared, patrol point at the bed) - the next load puts them at the bed.
        /// Only the trolls this client may write (single player / own). Expected outcome: how many were moved.
        static int Unloaded(long me, Vector3 bed, bool hasBed, int already)
        {
            var zm = ZDOMan.instance;
            if (zm == null) return 0;
            var list = new List<ZDO>();
            int index = 0, guard = 0;
            while (!zm.GetAllZDOsWithPrefabIterative(TrollTaming.TrollPrefab, list, ref index) && guard++ < 10000) { }
            int moved = 0;
            foreach (var z in list)
            {
                if (z == null || Done.Contains(z.m_uid) || !z.GetBool(ZDOVars.s_tamed, false) || z.GetLong(TrollTaming.OwnerKey, 0L) != me) continue;
                if (z.HasOwner() && !z.IsOwner()) { ModLog.Info("TrollHome: logout - unloaded troll " + z.GetString(ZDOVars.s_tamedName, "?") + " is run by another client - left"); continue; }
                if (z.GetFloat(ZDOVars.s_health, 1f) <= 0f) continue;
                var was = z.GetPosition();
                var hub = HubRegistry.For(me, was, z.GetInt(HubRegistry.AssignKey, 0));       // D-135: its Troll Hub, else the bed
                if (hub == null && !hasBed) continue;
                var anchor = hub != null ? hub.Pos : bed;
                var spot = TrollHomeSpot.Spot(anchor, already + moved);       // D-097: 15 m out, not in the house
                z.SetPosition(spot);
                z.Set(ZDOVars.s_follow, "");
                z.Set(ZDOVars.s_patrolPoint, spot);
                z.Set(ZDOVars.s_patrol, false);        // D-144: no patrol order - an unloaded troll stands at its spot (user saw Steve 1 run around the hub for 9 min); a full backpack goes to the depot by TrollIdle
                TrollRegistry.MovedTo(z.GetString(TrollRegistry.IdKey, ""), spot);
                moved++;
                ModLog.Info("TrollHome: logout - unloaded " + z.GetString(ZDOVars.s_tamedName, "?") + " moved from " + was.ToString("F0") + " (" + Vector3.Distance(was, anchor).ToString("F0") + " m from the " + (hub != null ? hub.Name : "bed") + ") to " + spot.ToString("F1"));
            }
            ModLog.Verbose("TrollHome: logout - " + list.Count + " troll ZDO(s) known here, " + moved + " unloaded own one(s) moved");
            return moved;
        }

        // Harmony hook: src/Integrations/Harmony/TrollsHomePatches.cs, TrollHome_Logout (MIGRATION_DECISIONS A5)
        internal static class Logout
        {
            internal static void Prefix()
            {
                var p = Player.m_localPlayer;
                var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
                bool hasBed = profile != null && profile.HaveCustomSpawnPoint();
                if (p == null || (!hasBed && !ModSettings.HubsOn)) return;
                var bed = hasBed ? profile.GetCustomSpawnPoint() : Vector3.zero;
                int moved = 0;
                foreach (var c in Character.GetAllCharacters())
                {
                    if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c) || TrollTaming.Owner(c) != p.GetPlayerID()) continue;
                    var view = c.GetComponent<ZNetView>();
                    var ai = c.GetComponent<MonsterAI>();
                    if (ai == null || view == null || !view.IsValid() || !view.IsOwner()) continue;
                    var f = c.GetComponent<TrollFarmer>(); if (f != null) Object.Destroy(f);
                    Vector3 anchor;
                    if (!HubRegistry.Anchor(c, bed, out anchor) && !hasBed) continue;     // D-135: Troll Hub first, else the bed
                    var spot = TrollHomeSpot.Spot(anchor, moved);              // D-097: 15 m out, not in the house
                    TrollTaming.StopFollow(ai);
                    c.transform.position = spot;
                    var body = c.GetComponent<Rigidbody>(); if (body != null) body.position = spot;
                    view.GetZDO().SetPosition(spot);
                    if (SetPatrolAt != null) SetPatrolAt(ai, spot);
                    moved++;
                    ModLog.Info("TrollHome: logout - " + TrollTaming.Tag(c) + " put near the bed at " + spot.ToString("F1") + " (" + Vector3.Distance(spot, bed).ToString("F0") + " m)");
                    var z = view.GetZDO(); if (z != null) Done.Add(z.m_uid);
                }
                moved += Unloaded(p.GetPlayerID(), bed, hasBed, moved);
                Done.Clear();
            }
        }
    }
}
