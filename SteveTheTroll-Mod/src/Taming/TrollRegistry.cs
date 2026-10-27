using System;
using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-043: the local player's tamed trolls for the F6 tabs, kept in the player's profile (Player.m_customData, one TrollInfo line each) so
    /// far-away trolls show their last known values (user answer 2026-10-27). Every RefreshSeconds the loaded own trolls are read live; a
    /// focus chosen while a troll is far (or owned by another client) waits in PendingFocus and is written when this client owns its ZDO.
    /// Troll id = our own id in the troll's ZDO (stt_id, made at taming or on first sight) - stable across reloads.
    /// </summary>
    internal static class TrollRegistry
    {
        public const string DataKey = "stevethetroll_trolls";
        public const string IdKey = "stt_id";
        public const string FocusKey = "stt_focus";
        const float RefreshSeconds = 2f;
        static readonly List<TrollInfo> Known = new List<TrollInfo>();
        static readonly Dictionary<string, string> PendingFocus = new Dictionary<string, string>();
        static long _loadedFor;
        static float _next;
        static int _lastLiveCount = -1;

        /// The list (refreshed first). Expected outcome: never null.
        internal static List<TrollInfo> Trolls()
        {
            Refresh(true);
            return Known;
        }

        /// Plugin.Update. Expected outcome: returns whether a refresh ran this frame.
        internal static bool Tick()
        {
            if (Time.time < _next || Player.m_localPlayer == null || !ModSettings.On) return false;
            _next = Time.time + RefreshSeconds;
            return Refresh(false);
        }

        /// Our id of a troll (made when missing, owner only). "" when it has none and this client cannot write it.
        internal static string IdOf(Character troll)
        {
            var zdo = TrollTaming.Zdo(troll);
            if (zdo == null) return "";
            string id = zdo.GetString(IdKey, "");
            var view = troll.GetComponent<ZNetView>();
            if (id.Length == 0 && view != null && view.IsOwner())
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 12);
                zdo.Set(IdKey, id);
                ModLog.Verbose("TrollRegistry: " + troll.name + " got id " + id);
            }
            return id;
        }

        /// Expected outcome: the troll's focus is stored (now, or pending until this client owns it); returns whether it was written now.
        internal static bool SetFocus(string id, string focus)
        {
            Load();
            var t = Known.Find(k => k.Id == id);
            if (t != null) t.Focus = focus;
            var live = FindLoaded(id);
            var view = live != null ? live.GetComponent<ZNetView>() : null;
            bool now = view != null && view.IsValid() && view.IsOwner();
            if (now) { view.GetZDO().Set(FocusKey, focus); PendingFocus.Remove(id); }
            else PendingFocus[id] = focus;
            Save();
            ModLog.Info("TrollRegistry: focus of " + (t != null ? t.Shown(null) : id) + " -> '" + focus + "' (" + (now ? "written to the troll" : "pending: troll " + (live != null ? "owned by another client" : "not loaded")) + ")");
            return now;
        }

        /// The loaded troll with our id (null when far).
        internal static Character FindLoaded(string id)
        {
            foreach (var c in Character.GetAllCharacters())
                if (c != null && TrollTaming.IsTroll(c) && c.IsTamed() && TrollTaming.Zdo(c) != null && TrollTaming.Zdo(c).GetString(IdKey, "") == id) return c;
            return null;
        }

        static bool Refresh(bool force)
        {
            var p = Player.m_localPlayer;
            if (p == null) return false;
            Load();
            long me = p.GetPlayerID();
            int live = 0;
            bool changed = false;
            foreach (var t in Known) t.Loaded = false;     // Read sets it again for every troll seen now
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || !TrollTaming.IsTroll(c) || !c.IsTamed() || TrollTaming.Owner(c) != me) continue;
                string id = IdOf(c);
                if (id.Length == 0) continue;
                var t = Known.Find(k => k.Id == id);
                if (t == null) { t = new TrollInfo { Id = id }; Known.Add(t); changed = true; ModLog.Info("TrollRegistry: new troll " + id + " in the list (" + Known.Count + " now)"); }
                Read(c, t);
                live++;
            }
            for (int i = 0; i < Known.Count; i++) Known[i].Number = i + 1;
            if (live != _lastLiveCount || force || changed)
            {
                _lastLiveCount = live;
                ModLog.Verbose("TrollRegistry: " + Known.Count + " troll(s), " + live + " loaded" + (PendingFocus.Count > 0 ? ", " + PendingFocus.Count + " focus change(s) pending" : ""));
            }
            Save();
            return true;
        }

        static void Read(Character c, TrollInfo t)
        {
            var zdo = TrollTaming.Zdo(c);
            var view = c.GetComponent<ZNetView>();
            string pending;
            if (PendingFocus.TryGetValue(t.Id, out pending) && view != null && view.IsOwner())
            {
                zdo.Set(FocusKey, pending);
                PendingFocus.Remove(t.Id);
                ModLog.Info("TrollRegistry: pending focus '" + pending + "' written to " + c.name + " [" + t.Id + "]");
            }
            var tame = c.GetComponent<Tameable>();
            t.Loaded = true;
            t.Name = tame != null ? tame.GetText() ?? "" : "";
            t.Status = StatusOf(c);
            t.Focus = zdo != null ? zdo.GetString(FocusKey, t.Focus) : t.Focus;
            t.Farming = "";                                  // T-008d (gathering) not built yet
            t.Health = c.GetHealth();
            t.MaxHealth = c.GetMaxHealth();
            t.Stars = Math.Max(0, c.GetLevel() - 1);
            t.DamageFactor = ModSettings.TamedDamageFactor.Value;
            var pos = c.transform.position;
            t.X = pos.x; t.Y = pos.y; t.Z = pos.z;
            t.BedDistance = BedDistance(pos);
            t.LastSeenTicks = DateTime.UtcNow.Ticks;
        }

        static string StatusOf(Character c)
        {
            if (c.IsDead()) return "stevethetroll_status_dead";
            var ai = c.GetComponent<MonsterAI>();
            if (ai == null) return "stevethetroll_status_unknown";
            if (ai.GetTargetCreature() != null) return "stevethetroll_status_fighting";
            if (c.GetComponent<TrollArrival>() != null) return "stevethetroll_status_coming";
            return ai.GetFollowTarget() != null ? "stevethetroll_status_following" : "stevethetroll_status_staying";
        }

        /// Metres from the local player's bed (spawn point), -1 without one.
        internal static float BedDistance(Vector3 pos)
        {
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (profile == null || !profile.HaveCustomSpawnPoint()) return -1f;
            return Vector3.Distance(pos, profile.GetCustomSpawnPoint());
        }

        internal static void MarkDead(string id)
        {
            Load();
            var t = Known.Find(k => k.Id == id);
            if (t == null) return;
            t.Status = "stevethetroll_status_dead"; t.Health = 0f; t.Loaded = false; t.LastSeenTicks = DateTime.UtcNow.Ticks;
            Save();
            ModLog.Info("TrollRegistry: " + t.Shown(null) + " [" + id + "] died");
        }

        // The list belongs to the local player: (re)read it when the player changed (character switch).
        static void Load()
        {
            var p = Player.m_localPlayer;
            if (p == null || _loadedFor == p.GetPlayerID()) return;
            _loadedFor = p.GetPlayerID();
            Known.Clear();
            PendingFocus.Clear();
            string data;
            int bad = 0;
            if (p.m_customData.TryGetValue(DataKey, out data))
                foreach (var line in data.Split('\n'))
                {
                    if (line.Length == 0) continue;
                    var t = TrollInfo.Decode(line);
                    if (t != null) { t.Loaded = false; Known.Add(t); } else bad++;
                }
            ModLog.Verbose("TrollRegistry: loaded " + Known.Count + " troll(s) of " + p.GetPlayerName() + " from the profile" + (bad > 0 ? ", " + bad + " bad line(s) dropped" : ""));
            ModLog.Check("TrollRegistry: bad profile lines", bad, 0, 0, "");
        }

        static void Save()
        {
            var p = Player.m_localPlayer;
            if (p == null) return;
            var lines = new List<string>(Known.Count);
            foreach (var t in Known) lines.Add(t.Encode());
            p.m_customData[DataKey] = string.Join("\n", lines.ToArray());
        }

        [HarmonyPatch(typeof(Tameable), "OnDeath")]
        static class Death
        {
            static void Postfix(Tameable __instance)
            {
                var c = __instance != null ? __instance.GetComponent<Character>() : null;
                if (c == null || !TrollTaming.IsTroll(c) || Player.m_localPlayer == null || TrollTaming.Owner(c) != Player.m_localPlayer.GetPlayerID()) return;
                var zdo = TrollTaming.Zdo(c);
                string id = zdo != null ? zdo.GetString(IdKey, "") : "";
                if (id.Length > 0) MarkDead(id);
            }
        }
    }
}
