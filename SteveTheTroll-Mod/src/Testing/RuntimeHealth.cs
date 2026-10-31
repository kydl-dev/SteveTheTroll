using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Taming;
using UnityEngine;

namespace SteveTheTroll.Testing
{
    /// <summary>
    /// D-160: read-only runtime health monitor. Every 10 s it looks at the local player's own trolls and reports what is wrong, by troll name (full backpack not unloading, farming
    /// beyond the hub leash, a hub assignment that points nowhere) with a 60 s rate limit per troll and problem. It never changes a troll. The counts are available to the probe
    /// (World_RuntimeHealth) so a test report says how many problems were seen.
    /// </summary>
    internal static class RuntimeHealth
    {
        const float EverySeconds = 10f, RepeatSeconds = 60f;
        static float _next, _depotSaid = -999f;
        static readonly Dictionary<int, float> FullSince = new Dictionary<int, float>();
        static readonly Dictionary<string, float> Said = new Dictionary<string, float>();
        internal static int Checked, Problems, LastProblems;
        internal static string LastText = "";

        /// D-160 (test hooks): one pass now, whatever the timer says.
        internal static void RunNow() { _next = 0f; Tick(); }

        internal static void Tick()
        {
            if (Time.time < _next || !ModSettings.TrollsOn) return;
            _next = Time.time + EverySeconds;
            var p = Player.m_localPlayer;
            if (p == null) return;
            long me = p.GetPlayerID();
            int checkedNow = 0, problems = 0;
            var text = new System.Text.StringBuilder();
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c) || TrollTaming.Owner(c) != me) continue;
                var view = c.GetComponent<ZNetView>();
                if (view == null || !view.IsValid()) continue;
                checkedNow++;
                var zdo = view.GetZDO();
                int id = c.GetInstanceID();
                var ai = c.GetComponent<MonsterAI>();
                var farmer = c.GetComponent<TrollFarmer>();
                bool full = Backpack.BackpackStore.IsFullCached(zdo);
                bool fighting = ai != null && ai.GetTargetCreature() != null;
                bool following = ai != null && ai.GetFollowTarget() != null;
                float since;
                if (full) { if (!FullSince.TryGetValue(id, out since)) { since = Time.time; FullSince[id] = since; } }
                else { FullSince.Remove(id); since = Time.time; }
                var hub = Hub.HubRegistry.For(c);
                if (HealthRules.FullNotUnloading(full, farmer != null && farmer.Unloading, fighting, Time.time - since))
                    problems += Report(c, "full-not-unloading", "backpack full for " + (Time.time - since).ToString("F0") + " s but not unloading (" + (farmer != null ? "farming" : "no job") + ", follow " + following + ", hub " + (hub != null ? hub.Name : "none") + ")", text);
                if (hub != null && HealthRules.FarmingBeyondLeash(farmer != null && !farmer.Unloading, following, true, Hub.HubRules.Flat(hub.Pos - c.transform.position), Hub.HubRules.FarmLeash))
                    problems += Report(c, "farm-leash", "farming " + Hub.HubRules.Flat(hub.Pos - c.transform.position).ToString("F0") + " m from " + hub.Name + " (leash " + Hub.HubRules.FarmLeash.ToString("F0") + " m)", text);
                int assigned = zdo.GetInt(Hub.HubRegistry.AssignKey, 0);
                if (assigned > 0 && HealthRules.StaleAssignment(assigned, Hub.HubRegistry.Find(me, assigned) != null))
                    problems += Report(c, "stale-hub", "assigned to hub " + assigned + " which does not exist", text);
            }
            foreach (var f in Figure.TrollFigure.All)
            {
                if (f == null || float.IsNaN(f.LastBelowSole)) continue;
                // the figure's lowest vertex 3 cm or more below the slipper's bottom, or tilted over 60 deg = it hangs under the slipper
                if (HealthRules.FigureBad(f.LastBelowSole, 0.03f, f.LastTilt, 60f))
                    problems += Report("fig" + f.GetInstanceID(), f.name, "figure under / inside the slipper: lowest vertex " + f.LastBelowSole.ToString("F3") + " m below the slipper bottom, head " + f.LastHeadAboveTop.ToString("F3") + " m above its top, tilt " + f.LastTilt.ToString("F0") + " deg", text);
            }
            float wait = Backpack.TrollDepot.LongestWait();
            if (HealthRules.DepotStuck(wait) && Time.time - _depotSaid > RepeatSeconds)
            {
                _depotSaid = Time.time; problems++;
                ModLog.Warn("RuntimeHealth: an empty troll depot has waited " + wait.ToString("F0") + " s to be removed (window open / reservation / frame above?)");
                text.Append("empty depot waiting ").Append(wait.ToString("F0")).Append(" s; ");
            }
            if (FullSince.Count > 200) FullSince.Clear();
            Checked = checkedNow; LastProblems = problems; Problems += problems; LastText = text.ToString();
        }

        static int Report(Character c, string kind, string what, System.Text.StringBuilder text) { return Report(c.GetInstanceID() + kind, TrollTaming.Tag(c), what, text); }

        static int Report(string key, string tag, string what, System.Text.StringBuilder text)
        {
            float at;
            text.Append(tag).Append(": ").Append(what).Append("; ");
            if (Said.TryGetValue(key, out at) && Time.time - at < RepeatSeconds) return 1;
            Said[key] = Time.time;
            if (Said.Count > 300) Said.Clear();
            ModLog.Warn("RuntimeHealth: " + tag + " - " + what);
            return 1;
        }
    }
}
