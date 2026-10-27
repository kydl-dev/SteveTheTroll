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
            if (wears != _wore) { _wore = wears; ModLog.Info("TrollHome: " + p.GetPlayerName() + (wears ? " wears the slippers again - trolls work as usual" : " took the slippers off - own trolls go to the bed")); }
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
                    if (ai.GetFollowTarget() != null) { ai.SetFollowTarget(null); ModLog.Info("TrollHome: " + c.name + " was told to follow without slippers - goes back to the bed"); Sent.Remove(id); }
                    else continue;
                }
                Sent.Add(id);
                ai.SetFollowTarget(null);
                if (TrollFarming.SendHome(c)) { sent++; Ui.Notify.Show(Loc.T("stevethetroll_home_noslippers", TrollOrders.Shown(c)), Ui.Notify.Kind.Good); }
            }
            return sent;
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
                    ai.SetFollowTarget(null);
                    c.transform.position = spot;
                    var body = c.GetComponent<Rigidbody>(); if (body != null) body.position = spot;
                    view.GetZDO().SetPosition(spot);
                    if (SetPatrolAt != null) SetPatrolAt(ai, spot);
                    moved++;
                    ModLog.Info("TrollHome: logout - " + c.name + " put at the bed " + spot.ToString("F1"));
                }
            }
        }
    }
}
