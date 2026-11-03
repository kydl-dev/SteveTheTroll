using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Presentation.Hub.Commands
{
    /// <summary>
    /// D-119 (user 2026-10-28): the game side of the F6 "My trolls" tab - Follow, Kill and the distance of each own tamed troll.
    ///  - Follow: a troll near the player follows at once; a troll outside the player's active area ("far away (not loaded around you)" -
    ///    also one kept loaded by TrollZones) is put next to the player first (user: "Teleport to you"), then follows.
    ///  - Kill (the tab asked Yes / No): a lethal hit with no attacker (true damage, so the elemental immunity does not matter; no troll
    ///    turns on anyone); a far troll is brought next to the player first and dies there once it is loaded (its backpack drops here).
    ///  - Distance: from the player to the troll's live position, or its last known one.
    /// A troll whose data another player's game runs (multiplayer, it is near them) cannot be moved from here: the player is told.
    /// </summary>
    internal static class TrollCommands
    {
        const float BehindMetres = 4f, SideMetres = 2f, PendingSeconds = 20f;
        static readonly Dictionary<string, float> PendingKill = new Dictionary<string, float>();     // id -> give-up time

        /// The tab's hooks. Expected outcome: all three set.
        internal static TrollActions Actions()
        {
            return new TrollActions { Follow = id => Follow(id), Kill = id => Kill(id), Distance = Distance };
        }

        static string Shown(string id)
        {
            var info = TrollRegistry.Peek().Find(t => t.Id == id);
            return info != null ? info.Shown(k => Loc.T(k)) : id;
        }

        /// Expected outcome: the troll now follows the player (or was brought next to the player with its follow target set).
        internal static bool Follow(string id)
        {
            var p = Player.m_localPlayer;
            if (p == null) return false;
            string shown = Shown(id);
            var troll = TrollRegistry.FindLoaded(id);
            if (troll != null && ZNetScene.InActiveArea(troll.transform.position, p.transform.position))
            {
                var ai = troll.GetComponent<MonsterAI>();
                if (ai == null) return false;
                var view = troll.GetComponent<ZNetView>();
                if (view == null || !view.IsOwner()) return Other(shown);
                ai.SetFollowTarget(p.gameObject);
                Notify.Show(Loc.T("stevethetroll_follow_now", shown), Notify.Kind.Good);
                ModLog.Info("TrollCommands: " + shown + " follows " + p.GetPlayerName() + " (near, " + Vector3.Distance(troll.transform.position, p.transform.position).ToString("F0") + " m)");
                return ai.GetFollowTarget() == p.gameObject;
            }
            bool moved = Bring(id, p, shown, true);
            if (moved) Notify.Show(Loc.T("stevethetroll_follow_brought", shown), Notify.Kind.Good);
            return moved;
        }

        /// Expected outcome: the troll was hit for its death now, or is on its way here to die (pending).
        internal static bool Kill(string id)
        {
            var p = Player.m_localPlayer;
            if (p == null) return false;
            string shown = Shown(id);
            var troll = TrollRegistry.FindLoaded(id);
            if (troll != null) return Die(troll, shown);
            if (!Bring(id, p, shown, false)) return false;
            PendingKill[id] = Time.time + PendingSeconds;
            Notify.Show(Loc.T("stevethetroll_kill_brought", shown), Notify.Kind.Death);
            return PendingKill.ContainsKey(id);
        }

        static bool Die(Character troll, string shown)
        {
            var hit = new HitData { m_point = troll.transform.position, m_dir = Vector3.down };
            float before = troll.GetHealth();
            hit.m_damage.m_damage = Mathf.Max(troll.GetMaxHealth(), before) * 10f;
            troll.Damage(hit);
            Notify.Show(Loc.T("stevethetroll_kill_done", shown), Notify.Kind.Death);
            ModLog.Info("TrollCommands: " + shown + " killed from the My trolls tab (health " + before.ToString("F0") + ", hit " + hit.m_damage.m_damage.ToString("F0") + " true damage)");
            return hit.m_damage.m_damage > before;
        }

        /// A troll that is not near: its saved data is put next to the player (follow set, or nothing for a kill); a loaded one far away is
        /// moved as an object. Expected outcome: whether it was moved.
        static bool Bring(string id, Player p, string shown, bool follow)
        {
            var spot = Spot(p);
            var troll = TrollRegistry.FindLoaded(id);
            if (troll != null)
            {
                var view = troll.GetComponent<ZNetView>();
                if (view == null || !view.IsOwner()) return Other(shown);
                var was = troll.transform.position;
                troll.transform.position = spot;
                var body = troll.GetComponent<Rigidbody>(); if (body != null) body.position = spot;
                view.GetZDO().SetPosition(spot);
                var ai = troll.GetComponent<MonsterAI>();
                if (follow && ai != null) ai.SetFollowTarget(p.gameObject);
                TrollRegistry.MovedTo(id, spot);
                ModLog.Info("TrollCommands: " + shown + " (loaded, outside the player's area) moved from " + was.ToString("F0") + " to " + spot.ToString("F1") + (follow ? ", follows" : ", to die"));
                return true;
            }
            var z = Find(id);
            if (z == null) { Notify.Show(Loc.T("stevethetroll_order_unknown"), Notify.Kind.Warn); ModLog.Warn("TrollCommands: no troll data with id " + id + " in this world"); return false; }
            if (z.HasOwner() && !z.IsOwner()) return Other(shown);
            var from = z.GetPosition();
            z.SetPosition(spot);
            z.Set(ZDOVars.s_follow, follow ? p.GetPlayerName() : "");
            z.Set(ZDOVars.s_patrol, !follow);
            if (!follow) z.Set(ZDOVars.s_patrolPoint, spot);
            TrollRegistry.MovedTo(id, spot);
            ModLog.Info("TrollCommands: " + shown + " (not loaded) moved from " + from.ToString("F0") + " (" + Vector3.Distance(from, p.transform.position).ToString("F0") + " m) to " + spot.ToString("F1")
                + (follow ? ", follow target '" + p.GetPlayerName() + "'" : ", to die when loaded"));
            return true;
        }

        static bool Other(string shown)
        {
            Notify.Show(Loc.T("stevethetroll_order_other", shown), Notify.Kind.Warn);
            ModLog.Info("TrollCommands: " + shown + " is run by another client - not moved");
            return false;
        }

        /// Next to the player: a few metres behind and to the side, on the ground.
        static Vector3 Spot(Player p)
        {
            var t = p.transform;
            var s = t.position - t.forward * BehindMetres + t.right * SideMetres;
            float ground = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(s) : s.y;
            s.y = Mathf.Max(ground, t.position.y - 2f) + 0.2f;
            return s;
        }

        static ZDO Find(string id)
        {
            var zm = ZDOMan.instance;
            if (zm == null) return null;
            var list = new List<ZDO>();
            int index = 0, guard = 0;
            while (!zm.GetAllZDOsWithPrefabIterative(TrollTaming.TrollPrefab, list, ref index) && guard++ < 10000) { }
            foreach (var z in list) if (z != null && z.GetString(TrollRegistry.IdKey, "") == id) return z;
            return null;
        }

        /// Metres from the player to the troll (live position when loaded, else the last known one); -1 unknown.
        internal static float Distance(string id)
        {
            var p = Player.m_localPlayer;
            if (p == null) return -1f;
            var troll = TrollRegistry.FindLoaded(id);
            if (troll != null) return Vector3.Distance(troll.transform.position, p.transform.position);
            var info = TrollRegistry.Peek().Find(t => t.Id == id);
            return info != null ? Vector3.Distance(new Vector3(info.X, info.Y, info.Z), p.transform.position) : -1f;
        }

        /// Plugin.Update: far trolls sent here to die are killed as soon as they are loaded. Expected outcome: how many are still pending.
        internal static int Tick()
        {
            if (PendingKill.Count == 0) return 0;
            var done = new List<string>();
            foreach (var kv in PendingKill)
            {
                var troll = TrollRegistry.FindLoaded(kv.Key);
                if (troll != null) { if (!troll.IsDead()) Die(troll, Shown(kv.Key)); done.Add(kv.Key); continue; }
                if (Time.time > kv.Value) { ModLog.Warn("TrollCommands: " + Shown(kv.Key) + " was not loaded within " + PendingSeconds + " s - not killed"); done.Add(kv.Key); }
            }
            foreach (var id in done) PendingKill.Remove(id);
            return PendingKill.Count;
        }
    }
}
