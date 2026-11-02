using Kydl.Common;
using SteveTheTroll.Hub;
using SteveTheTroll.Trolls.Recovery.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-162 (user 2026-10-31): a tamed troll that ends up further than 1000 m from its hub with no follow order (the player let it go far away) walks back to the hub until it is
    /// inside the 950 m farm zone, then farms again. Only two things stop the walk: the troll or the player (in range) is attacked - the combat / rage code drives (at the 50 %
    /// speed of HubSpeed when the hub is out of reach) and the walk goes on afterwards - and a new follow order. The decisions are HubRules.ShouldReturn / ReturnNext.
    /// </summary>
    internal sealed class TrollReturn : MonoBehaviour
    {
        const float WaypointMetres = 30f;
        HubInfo _hub;
        float _nextLog, _start;
        static readonly System.Collections.Generic.Dictionary<int, float> Said = new System.Collections.Generic.Dictionary<int, float>();

        /// The hub the troll should walk back to: the one chosen by hand if it still exists, else its owner's nearest (any distance). `distance` = flat metres (-1 = none).
        internal static HubInfo Target(Character c, out float distance)
        {
            distance = -1f;
            var z = TrollTaming.Zdo(c);
            if (z == null || !ModSettings.HubsOn) return null;
            long owner = TrollTaming.Owner(c);
            if (owner == 0L) return null;
            HubInfo h = null;
            int manual = z.GetInt(HubRegistry.AssignKey, 0);
            if (manual > 0) h = HubRegistry.Find(owner, manual);
            if (h == null) h = HubRegistry.Nearest(owner, c.transform.position, out distance);
            if (h != null) distance = HubRules.Flat(h.Pos - c.transform.position);
            return h;
        }

        /// Called once a second for each own troll (TrollIdle). Expected outcome: true = the troll walks back (already, or it starts now) and must not be given another job.
        internal static bool Check(Character c, MonsterAI ai)
        {
            if (c == null || ai == null) return false;
            var running = c.GetComponent<TrollReturn>();
            float d;
            var hub = Target(c, out d);
            if (running != null)
            {
                running._hub = hub;
                return true;
            }
            if (!HubRules.ShouldReturn(hub != null, d, false)) return false;
            if (ai.GetFollowTarget() != null || c.GetComponent<TrollArrival>() != null || !TrollHome.OwnerWears(c)) return false;
            if (ai.GetTargetCreature() != null || c.GetComponent<TrollRage>() != null) return false;
            var farmer = c.GetComponent<TrollFarmer>();
            if (farmer != null && farmer.Unloading) return false;
            if (farmer != null) Destroy(farmer);
            var r = c.gameObject.AddComponent<TrollReturn>();
            r._hub = hub; r._start = Time.time;
            ai.SetPatrolPoint();
            ModLog.Info("TrollReturn: " + TrollTaming.Tag(c) + " is " + d.ToString("F0") + " m from " + hub.Name + " (more than " + HubRules.AutoReach.ToString("0") + " m) and has no follow order - walks back to the " + HubRules.FarmLeash.ToString("0") + " m farm zone");
            if (Player.m_localPlayer != null && TrollTaming.Owner(c) == Player.m_localPlayer.GetPlayerID())
                Ui.Notify.Show(Loc.T("stevethetroll_hub_return", TrollTaming.ShownName(r), d.ToString("0"), hub.Name, HubRules.FarmLeash.ToString("0")), Ui.Notify.Kind.Farm);
            return true;
        }

        /// One AI step (MonsterAI.UpdateAI prefix). Expected outcome: true = the walk drove the troll this frame (the game's AI is skipped).
        internal bool Step(MonsterAI ai, float dt, System.Func<BaseAI, float, Vector3, float, bool, bool> moveTo, System.Action<BaseAI, Vector3> lookAt)
        {
            var c = GetComponent<Character>();
            if (c == null || c.IsDead()) { Destroy(this); return false; }
            var rec = TrollRecovery.Of(this);                                                  // D-167: a running rage / detour drives first
            if (rec.Busy && rec.Continue(dt)) return true;
            float d = _hub != null ? HubRules.Flat(_hub.Pos - transform.position) : -1f;
            if (_hub == null) { ModLog.Info("TrollReturn: " + TrollTaming.Tag(c) + " lost its hub - stops walking back"); Destroy(this); return false; }
            bool fighting = ai.GetTargetCreature() != null || GetComponent<TrollRage>() != null || GetComponent<TrollChase>() != null || GetComponent<TrollSlam>() != null;
            switch (HubRules.ReturnNext(ai.GetFollowTarget() != null, fighting, d))
            {
                case HubRules.ReturnStep.Cancel:
                    ModLog.Info("TrollReturn: " + TrollTaming.Tag(c) + " got a follow order - stops walking back");
                    Destroy(this);
                    return false;
                case HubRules.ReturnStep.Yield:
                    return false;
                case HubRules.ReturnStep.Done:
                    Arrived(c, ai, d);
                    return true;
            }
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + 30f;
                ModLog.Verbose("TrollReturn: " + TrollTaming.Tag(c) + " " + d.ToString("F0") + " m from " + _hub.Name + ", walking back for " + (Time.time - _start).ToString("F0") + " s");
            }
            // A hub 1 km away is far outside the loaded area (the game's path search fails for it): walk to a point a short way ahead, a new one every step.
            var dir = _hub.Pos - transform.position; dir.y = 0f;
            var ahead = transform.position + dir.normalized * Mathf.Min(WaypointMetres, dir.magnitude);
            // D-167: the walk back had no stuck detection at all; a hub is never abandoned, so a failed cycle only starts the next one (turning the other way first)
            long ka, kb;
            TrollRecovery.KeyOfHub(_hub.Pos, out ka, out kb);
            if (rec.Tick(dt, _hub.Pos, ka, kb, false, true, null)) return true;
            moveTo(ai, dt, ahead, 0.5f, true);
            return true;
        }

        void Arrived(Character c, MonsterAI ai, float d)
        {
            ai.StopMoving();
            ai.SetPatrolPoint();
            { var rr = GetComponent<TrollRecovery>(); if (rr != null) rr.Reached(); }
            ModLog.Info("TrollReturn: " + TrollTaming.Tag(c) + " is back inside the farm zone of " + _hub.Name + " (" + d.ToString("F0") + " m, " + (Time.time - _start).ToString("F0") + " s) - farms again");
            string name = _hub.Name;
            if (Player.m_localPlayer != null && TrollTaming.Owner(c) == Player.m_localPlayer.GetPlayerID())
                Ui.Notify.Show(Loc.T("stevethetroll_hub_return_done", TrollTaming.ShownName(this), name), Ui.Notify.Kind.Farm);
            var z = TrollTaming.Zdo(c);
            string focus = z != null ? Ui.TrollFocus.Normalize(z.GetString(TrollRegistry.FocusKey, "")) : "";
            bool full = z != null && Backpack.BackpackStore.IsFullCached(z);
            Destroy(this);
            if (full) TrollFarming.BeginUnload(c);
            else TrollFarming.Begin(c, focus, true);
        }
    }
}
