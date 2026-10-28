using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
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
                    if (ai.GetFollowTarget() != null) { TrollTaming.StopFollow(ai); ModLog.Info("TrollHome: " + c.name + " was told to follow without slippers - goes back to the bed"); Sent.Remove(id); }
                    else continue;
                }
                Sent.Add(id);
                TrollTaming.StopFollow(ai);
                if (TrollFarming.SendHome(c)) { sent++; Ui.Notify.Show(Loc.T("stevethetroll_home_noslippers", TrollOrders.Shown(c)), Ui.Notify.Kind.Good); }
            }
            return sent;
        }

        static readonly HashSet<ZDOID> Done = new HashSet<ZDOID>();

        /// D-081 (TEST-034: 2 of 5 trolls, 110 m and 180 m from the bed, stayed where they were): own tamed trolls that are not loaded around the
        /// player are moved through their saved data (ZDO position, follow cleared, patrol point at the bed) - the next load puts them at the bed.
        /// Only the trolls this client may write (single player / own). Expected outcome: how many were moved.
        static int Unloaded(long me, Vector3 bed, int already)
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
                var spot = bed + new Vector3(2f + (already + moved) * 3f, 0.5f, 2f);
                var was = z.GetPosition();
                z.SetPosition(spot);
                z.Set(ZDOVars.s_follow, "");
                z.Set(ZDOVars.s_patrolPoint, spot);
                z.Set(ZDOVars.s_patrol, true);
                TrollRegistry.MovedTo(z.GetString(TrollRegistry.IdKey, ""), spot);
                moved++;
                ModLog.Info("TrollHome: logout - unloaded " + z.GetString(ZDOVars.s_tamedName, "?") + " moved from " + was.ToString("F0") + " (" + Vector3.Distance(was, bed).ToString("F0") + " m from the bed) to " + spot.ToString("F1"));
            }
            ModLog.Verbose("TrollHome: logout - " + list.Count + " troll ZDO(s) known here, " + moved + " unloaded own one(s) moved");
            return moved;
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        static class Logout
        {
            static void Prefix()
            {
                var p = Player.m_localPlayer;
                var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
                if (p == null || profile == null || !profile.HaveCustomSpawnPoint()) return;
                var bed = profile.GetCustomSpawnPoint();
                int moved = 0;
                foreach (var c in Character.GetAllCharacters())
                {
                    if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c) || TrollTaming.Owner(c) != p.GetPlayerID()) continue;
                    var view = c.GetComponent<ZNetView>();
                    var ai = c.GetComponent<MonsterAI>();
                    if (ai == null || view == null || !view.IsValid() || !view.IsOwner()) continue;
                    var f = c.GetComponent<TrollFarmer>(); if (f != null) Object.Destroy(f);
                    var spot = bed + new Vector3(2f + moved * 3f, 0.5f, 2f);
                    TrollTaming.StopFollow(ai);
                    c.transform.position = spot;
                    var body = c.GetComponent<Rigidbody>(); if (body != null) body.position = spot;
                    view.GetZDO().SetPosition(spot);
                    if (SetPatrolAt != null) SetPatrolAt(ai, spot);
                    moved++;
                    ModLog.Info("TrollHome: logout - " + c.name + " put at the bed " + spot.ToString("F1"));
                    var z = view.GetZDO(); if (z != null) Done.Add(z.m_uid);
                }
                moved += Unloaded(p.GetPlayerID(), bed, moved);
                Done.Clear();
            }
        }
    }
}
