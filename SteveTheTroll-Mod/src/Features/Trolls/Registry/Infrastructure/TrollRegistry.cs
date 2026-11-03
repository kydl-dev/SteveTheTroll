using System;
using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Registry.Infrastructure
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

        /// D-081: the list as the last refresh left it (no refresh, no log - for the map pins every 2 s). Expected outcome: never null.
        internal static List<TrollInfo> Peek()
        {
            Load();
            return Known;
        }

        /// D-084: the player's living tamed trolls in total (near or far, from the saved list). Expected outcome: 0 or more.
        internal static int LivingCount()
        {
            Load();
            int n = 0;
            foreach (var t in Known) if (t.DiedTicks <= 0) n++;
            return n;
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
                ModLog.Verbose("TrollRegistry: " + TrollTaming.Tag(troll) + " got id " + id);
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
        /// D-126: the game's biome name at a position ("" before the world generator exists).
        internal static string BiomeAt(Vector3 pos)
        {
            return WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(pos).ToString() : "";
        }

        /// D-081: a troll moved without being loaded (logout to the bed): its last known position becomes the new one (map pin, F6).
        internal static void MovedTo(string id, Vector3 pos)
        {
            if (string.IsNullOrEmpty(id)) return;
            Load();
            var t = Known.Find(k => k.Id == id);
            if (t == null) return;
            t.X = pos.x; t.Y = pos.y; t.Z = pos.z;
            SetDestination(t, pos, HubRegistry.For(Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L, pos, 0));
            t.Biome = BiomeAt(pos);
            t.LastSeenTicks = DateTime.UtcNow.Ticks;
            Save();
        }

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
            _forceRead = force;
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
                if (t.Name.Length == 0 && c.GetComponent<ZNetView>() != null && c.GetComponent<ZNetView>().IsOwner() && c.GetComponent<Tameable>() != null)
                {
                    string n = NameFor(c);
                    c.GetComponent<Tameable>().SetText(n);
                    ModLog.Info("TrollRegistry: unnamed tamed troll [" + id + "] named '" + n + "' (D-047: never 'Troll')");
                }
                Read(c, t);
                live++;
            }
            int gone = RemoveDead();
            if (gone > 0) changed = true;
            for (int i = 0; i < Known.Count; i++) Known[i].Number = i + 1;
            if (live != _lastLiveCount || force || changed)
            {
                _lastLiveCount = live;
                ModLog.Verbose("TrollRegistry: " + Known.Count + " troll(s), " + live + " loaded" + (PendingFocus.Count > 0 ? ", " + PendingFocus.Count + " focus change(s) pending" : ""));
            }
            Save();
            return true;
        }

        // D-142 (test run 235053: the refresh took 20 - 38 ms every 2 s with six trolls): the cheap values (health, position, status, focus, name) are read every refresh, the
        // costly ones only now and then - attack / resistance texts (localised strings) every SlowSeconds, the backpack fill every BackpackSeconds, the biome when the troll moved
        // BiomeMoveMeters. Opening a tab (force) reads everything.
        const float SlowSeconds = 30f, BackpackSeconds = 6f, BiomeMoveMeters = 48f;
        static readonly Dictionary<string, float> SlowAt = new Dictionary<string, float>(), PackAt = new Dictionary<string, float>();
        static readonly Dictionary<string, Vector3> BiomeFrom = new Dictionary<string, Vector3>();
        static bool _forceRead;

        static void Read(Character c, TrollInfo t)
        {
            var zdo = TrollTaming.Zdo(c);
            var view = c.GetComponent<ZNetView>();
            string pending;
            if (PendingFocus.TryGetValue(t.Id, out pending) && view != null && view.IsOwner())
            {
                zdo.Set(FocusKey, pending);
                PendingFocus.Remove(t.Id);
                ModLog.Info("TrollRegistry: pending focus '" + pending + "' written to " + TrollTaming.Tag(c));
            }
            var tame = c.GetComponent<Tameable>();
            t.Loaded = true;
            t.Name = tame != null ? tame.GetText() ?? "" : "";
            t.Status = StatusOf(c);
            t.Focus = zdo != null ? zdo.GetString(FocusKey, t.Focus) : t.Focus;
            t.Farming = TrollFarming.Farming(c);
            float now = Time.time, last;
            if (_forceRead || !SlowAt.TryGetValue(t.Id, out last) || now - last >= SlowSeconds)
            {
                SlowAt[t.Id] = now;
                t.Attacks = AttacksOf(c);
                t.Resists = ResistsOf(c);
            }
            t.Health = c.GetHealth();
            t.MaxHealth = c.GetMaxHealth();
            t.Stars = Math.Max(0, c.GetLevel() - 1);
            t.DamageFactor = ModSettings.TamedDamageFactor.Value;
            var pos = c.transform.position;
            t.X = pos.x; t.Y = pos.y; t.Z = pos.z;
            SetDestination(t, pos, HubRegistry.For(c));
            Vector3 from;
            if (_forceRead || t.Biome.Length == 0 || !BiomeFrom.TryGetValue(t.Id, out from) || (pos - from).sqrMagnitude > BiomeMoveMeters * BiomeMoveMeters)
            {
                t.Biome = BiomeAt(pos);                                                     // D-126: the focus drop-down's biome items
                BiomeFrom[t.Id] = pos;
            }
            if (_forceRead || !PackAt.TryGetValue(t.Id, out last) || now - last >= BackpackSeconds)
            {
                PackAt[t.Id] = now;
                BackpackStore.Fill(zdo, out t.BackpackUsed, out t.BackpackSlots);     // D-096
            }
            t.LastSeenTicks = DateTime.UtcNow.Ticks;
        }

        static string StatusOf(Character c)
        {
            if (c.IsDead()) return "stevethetroll_status_dead";
            var ai = c.GetComponent<MonsterAI>();
            if (ai == null) return "stevethetroll_status_unknown";
            if (ai.GetTargetCreature() != null) return "stevethetroll_status_fighting";
            if (c.GetComponent<TrollArrival>() != null) return "stevethetroll_status_coming";
            if (c.GetComponent<TrollFarmer>() != null) return "stevethetroll_status_farming";
            return ai.GetFollowTarget() != null ? "stevethetroll_status_following" : "stevethetroll_status_staying";
        }

        /// D-162 (user: "in the page where it says troll distance to bed, say hub when there are hubs"): the distance is to the troll's hub (named) when it has one, else to the bed.
        static void SetDestination(TrollInfo t, Vector3 pos, HubInfo hub)
        {
            if (hub != null) { t.Dest = hub.Name; t.BedDistance = HubGeometry.Flat(hub.Pos - pos); }
            else { t.Dest = ""; t.BedDistance = BedDistance(pos); }
        }

        /// Metres from the local player's bed (spawn point), -1 without one.
        internal static float BedDistance(Vector3 pos)
        {
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (profile == null || !profile.HaveCustomSpawnPoint()) return -1f;
            return Vector3.Distance(pos, profile.GetCustomSpawnPoint());
        }

        /// D-044: the owner is told, the entry stays DeadKeepSeconds (status Dead), then it is removed everywhere (list, pending focus) and its
        /// name is free again.
        internal static void MarkDead(string id)
        {
            Load();
            var t = Known.Find(k => k.Id == id);
            if (t == null) return;
            t.Status = "stevethetroll_status_dead"; t.Health = 0f; t.Loaded = false; t.LastSeenTicks = DateTime.UtcNow.Ticks; t.DiedTicks = t.LastSeenTicks;
            Save();
            string shown = t.Shown(k => Loc.T(k));
            Notify.Show(Loc.T("stevethetroll_troll_died", shown), Notify.Kind.Death);
            ModLog.Info("TrollRegistry: " + shown + " [" + id + "] died - removed in " + ModSettings.DeadKeepSeconds.Value + " s");
        }

        static int RemoveDead()
        {
            long keep = (long)(ModSettings.DeadKeepSeconds.Value * TimeSpan.TicksPerSecond), now = DateTime.UtcNow.Ticks;
            int removed = 0;
            for (int i = Known.Count - 1; i >= 0; i--)
            {
                var t = Known[i];
                if (t.DiedTicks <= 0 || now - t.DiedTicks < keep) continue;
                Known.RemoveAt(i);
                PendingFocus.Remove(t.Id);
                removed++;
                ModLog.Info("TrollRegistry: " + t.Shown(null) + " [" + t.Id + "] removed (dead for " + ModSettings.DeadKeepSeconds.Value + " s), name '" + t.Name + "' is free");
            }
            return removed;
        }

        /// D-044: the name a newly tamed troll gets: "Steve N" with a club (log), "Spencer N" without; N = lowest free among living trolls.
        internal static string NameFor(Character troll)
        {
            Load();
            var names = new List<string>();
            foreach (var t in Known) if (t.Name.Length > 0) names.Add(t.Name);     // a dead troll's name is free once its entry is removed
            var h = troll as Humanoid;
            bool club = false;
            if (h != null) foreach (var it in h.GetInventory().GetAllItems()) if (it.m_dropPrefab != null && it.m_dropPrefab.name.StartsWith("troll_log")) club = true;
            string name = TamingRules.NextName(club ? "Steve" : "Spencer", names);
            ModLog.Verbose("TrollRegistry: name for " + TrollTaming.Tag(troll) + ": club " + club + ", taken [" + string.Join(", ", names.ToArray()) + "] -> " + name);
            return name;
        }

        static readonly string[] DamageNames = { "Blunt", "Slash", "Pierce", "Chop", "Pickaxe", "Fire", "Frost", "Lightning", "Poison", "Spirit" };

        // "name~Blunt=180,Chop=300~4;..." for the Trolls tab: each attack item's damage x the level and tamed factors, and its AI interval.
        static string AttacksOf(Character c)
        {
            var h = c as Humanoid;
            if (h == null) return "";
            float factor = (1f + Math.Max(0, c.GetLevel() - 1) * 0.5f) * ModSettings.TamedDamageFactor.Value;
            var parts = new List<string>();
            foreach (var it in h.GetInventory().GetAllItems())
            {
                var d = it.m_shared.m_damages;
                float[] v = { d.m_blunt, d.m_slash, d.m_pierce, d.m_chop, d.m_pickaxe, d.m_fire, d.m_frost, d.m_lightning, d.m_poison, d.m_spirit };
                var dmg = new List<string>();
                for (int i = 0; i < v.Length; i++) if (v[i] > 0f) dmg.Add(DamageNames[i] + "=" + (v[i] * factor).ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                if (dmg.Count == 0) continue;
                string name = Localization.instance != null ? Localization.instance.Localize(it.m_shared.m_name) : it.m_shared.m_name;
                parts.Add(name.Replace("~", "-").Replace(";", ",") + "~" + string.Join(",", dmg.ToArray()) + "~" + it.m_shared.m_aiAttackInterval.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            }
            return string.Join(";", parts.ToArray());
        }

        static string ResistsOf(Character c)
        {
            var m = c.GetDamageModifiers();
            HitData.DamageModifier[] v = { m.m_blunt, m.m_slash, m.m_pierce, m.m_chop, m.m_pickaxe, m.m_fire, m.m_frost, m.m_lightning, m.m_poison, m.m_spirit };
            var parts = new List<string>();
            for (int i = 0; i < v.Length; i++) parts.Add(DamageNames[i] + "=" + v[i]);
            return string.Join(",", parts.ToArray());
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
                    if (t != null && t.Status == "stevethetroll_status_dead" && t.DiedTicks <= 0) { t.DiedTicks = Math.Max(1L, t.LastSeenTicks); ModLog.Info("TrollRegistry: " + t.Shown(null) + " [" + t.Id + "] was dead without a death time (old entry) - removed with the next refresh"); }
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

        // Harmony hook: src/Integrations/Harmony/TrollsRegistryPatches.cs, TrollRegistry_Death (MIGRATION_DECISIONS A5)
        internal static class Death
        {
            internal static void Postfix(Tameable __instance)
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
