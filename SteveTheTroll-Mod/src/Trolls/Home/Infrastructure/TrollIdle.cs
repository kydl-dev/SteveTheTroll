using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Backpack.Infrastructure;
using SteveTheTroll.Hubs.Infrastructure;
using SteveTheTroll.Trolls.Farming.Domain;
using SteveTheTroll.Trolls.Farming.Infrastructure;
using SteveTheTroll.Trolls.Orders.Commands;
using SteveTheTroll.Trolls.Registry.Infrastructure;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Trolls.Home.Infrastructure
{
    /// <summary>
    /// D-062 (user: the 2-minute check "for any moment when no job was given and the backpack storages are not full"): on the client that
    /// owns it, a tamed troll that is not farming, not walking to its tamer, not following and not fighting, with room in its backpack, starts
    /// farming on its own after AutoFarmSeconds (its focus item first, then the default priority, D-058) - with the dark-yellow message.
    /// </summary>
    internal static class TrollIdle
    {
        static readonly Dictionary<int, float> Since = new Dictionary<int, float>();
        static readonly Dictionary<int, float> FullTried = new Dictionary<int, float>();   // D-144: a full troll is sent to the depot once per RetryFullSeconds (a full depot must not loop)
        const float RetryFullSeconds = 120f;
        static readonly Dictionary<int, float> WhySaid = new Dictionary<int, float>();
        static float _next, _dueUntil;
        static Player _player;
        const float ResumeWindowSeconds = 60f;

        /// D-077 (TEST-032: "after I logged back in, Spencer has not gone back to farming"): for the next minute idle trolls start farming at
        /// once instead of after AutoFarmSeconds. Called when the owner wears the slippers again (TrollHome) and after a login (Tick).
        internal static void ResumeNow(string why)
        {
            _dueUntil = Time.time + ResumeWindowSeconds;
            FullTried.Clear();
            ModLog.Info("TrollIdle: " + why + " - idle own trolls start farming at once (next " + ResumeWindowSeconds.ToString("0") + " s)");
        }

        /// D-160: a full troll that found no place for a depot tries again in 20 s, not after the whole RetryFullSeconds (a full depot still must not loop).
        internal static void RetrySoon(Character c)
        {
            if (c != null) FullTried[c.GetInstanceID()] = Time.time - RetryFullSeconds + 20f;
        }

        /// Plugin.Update. Expected outcome: how many trolls were sent farming this time.
        internal static int Tick()
        {
            if (Time.time < _next || !ModSettings.On) return 0;
            _next = Time.time + 1f;
            if (Player.m_localPlayer != _player) { _player = Player.m_localPlayer; if (_player != null) ResumeNow("login of " + _player.GetPlayerName()); }
            int sent = 0;
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c)) continue;
                var view = c.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner()) continue;
                int id = c.GetInstanceID();
                TrollRegistry.IdOf(c);       // D-145: every own troll has its permanent id (normally given at taming)
                if (TrollReturn.Check(c, c.GetComponent<MonsterAI>())) { Since.Remove(id); continue; }      // D-162: beyond the hub's reach -> walks back first
                string why = BusyReason(c);
                Watch(c, id, why == null);
                bool full = BackpackStore.IsFullCached(view.GetZDO());
                float tried;
                bool triedRecently = FullTried.TryGetValue(id, out tried) && Time.time - tried < RetryFullSeconds;
                var gate = IdleRules.Gate(why, full, triedRecently);
                if (gate != IdleGate.Continue) Since.Remove(id);
                if (gate == IdleGate.Skip)
                {
                    // D-144: a FULL troll that is not sent says why (the log must show it, not guesswork)
                    float said;
                    if (full && why != null && (!WhySaid.TryGetValue(id, out said) || Time.time - said > 30f))
                    { WhySaid[id] = Time.time; ModLog.Info("TrollIdle: " + Shown(c) + " has a full backpack but is not sent to the depot: " + why); }
                    continue;
                }
                if (gate == IdleGate.Unload)
                {
                    FullTried[id] = Time.time;
                    if (TrollOrderService.Send(new UnloadCommand(c))) { sent++; ModLog.Info("TrollIdle: " + Shown(c) + " has a full backpack - walks to the depot on its own"); break; }
                    continue;
                }
                float since;
                if (!Since.TryGetValue(id, out since)) { Since[id] = Time.time; continue; }
                bool resume = Time.time < _dueUntil;
                // D-150: a troll that was farming when its object was re-created (far zones) goes back to work at once, it does not wait AutoFarmSeconds
                bool wasFarming = view.GetZDO().GetBool(TrollFarmer.FarmingKey, false);
                if (!resume && !wasFarming && Time.time - since < ModSettings.AutoFarmSeconds.Value) continue;
                Since.Remove(id);
                // D-149 (user): a login / slippers-on with a PARTLY filled backpack goes straight on farming; only a FULL backpack is delivered (gate above)
                string focus = Ui.TrollFocus.Normalize(view.GetZDO().GetString(TrollRegistry.FocusKey, ""));
                ModLog.Info("TrollIdle: " + TrollTaming.Tag(c) + (resume ? " goes back to work (login / slippers on)" : wasFarming ? " was farming before it was re-created - goes back to work" : " had no job for " + ModSettings.AutoFarmSeconds.Value.ToString("0") + " s") + " - farms on its own (focus '" + focus + "')");
                if (TrollOrderService.Send(new FarmFocusCommand(c, focus, true))) sent++;
                break;       // D-142: Begin searches the surroundings (5 - 18 ms); one troll per tick, the others follow a second later (test run 235053: 17 ms at login)
            }
            if (Since.Count > 200) Since.Clear();
            if (FullTried.Count > 200) FullTried.Clear();
            if (WhySaid.Count > 200) WhySaid.Clear();
            return sent;
        }

        static string Shown(Character c) { return TrollTaming.Tag(c); }

        static readonly Dictionary<int, KeyValuePair<float, Vector3>> Seen = new Dictionary<int, KeyValuePair<float, Vector3>>();

        /// D-144: every own troll is watched; one that travels with no job (no farm, follow, fight, arrival) is reported by name with where it is and what it holds.
        static void Watch(Character c, int id, bool noJob)
        {
            KeyValuePair<float, Vector3> was;
            if (!Seen.TryGetValue(id, out was)) { Seen[id] = new KeyValuePair<float, Vector3>(Time.time, c.transform.position); return; }
            float dt = Time.time - was.Key;
            if (dt < IdleRules.WanderSeconds) return;
            var d = c.transform.position - was.Value; d.y = 0f;
            Seen[id] = new KeyValuePair<float, Vector3>(Time.time, c.transform.position);
            if (Seen.Count > 200) Seen.Clear();
            if (IdleRules.Wanders(!noJob, d.magnitude, dt))
            {
                var z = c.GetComponent<ZNetView>() != null ? c.GetComponent<ZNetView>().GetZDO() : null;
                bool hasPatrol = z != null && z.GetBool(ZDOVars.s_patrol, false);
                ModLog.Warn("TrollIdle: " + Shown(c) + " wanders - moved " + d.magnitude.ToString("F0") + " m in " + dt.ToString("F0") + " s with no job (at " + c.transform.position.ToString("F0")
                    + ", patrol point " + (hasPatrol ? "set" : "none") + ", slippers " + TrollHome.OwnerWears(c) + ")");
            }
        }

        // A job: farming, walking to its tamer, following, fighting. (A full backpack is not busy: it gets the depot job, see Tick.)
        static string BusyReason(Character c)
        {
            var ai = c.GetComponent<MonsterAI>();
            if (ai == null) return "no MonsterAI";
            var farmer = c.GetComponent<TrollFarmer>();
            if (farmer != null) return farmer.Unloading ? "TrollFarmer is unloading" : IdleRules.FarmReason;      // D-159: a farm job gives way to a full backpack, an unload in progress does not restart
            if (c.GetComponent<TrollArrival>() != null) return "still walking to its tamer (TrollArrival)";
            if (!TrollHome.OwnerWears(c)) return "owner does not wear the slippers";
            if (ai.GetFollowTarget() != null) return IdleRules.FollowReason;
            if (ai.GetTargetCreature() != null) return "fighting";
            return null;
        }
    }
}
