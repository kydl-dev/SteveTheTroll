using Kydl.Common;
using SteveTheTroll.Backpack.Infrastructure;
using SteveTheTroll.Hubs.Domain;
using SteveTheTroll.Hubs.Infrastructure;
using SteveTheTroll.Trolls.Home.Domain;
using SteveTheTroll.Trolls.Home.Infrastructure;
using SteveTheTroll.Trolls.Recovery.Infrastructure;
using SteveTheTroll.Trolls.Registry.Infrastructure;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Trolls.Farming.Infrastructure
{
    /// <summary>
    /// The way home of a farming troll: to the bed / Troll Hub (D-135), with a full backpack on to a depot (D-064) and into its chests (D-106), afterwards back to the remembered
    /// farm spot (D-163). D-170: moved out of TrollFarmer unchanged (class size); one per farmer, it drives through the farmer's movement capability.
    /// </summary>
    internal sealed class FarmHomeTrip
    {
        const float UnloadMetres = 10f, UnloadStopMetres = 8f, UnloadWalkSeconds = 60f, StuckHomeSeconds = 8f, SpotMinMetres = 40f;
        readonly TrollFarmer _f;
        bool _unloading;
        Vector3 _unloadAt;
        float _unloadStart, _homeBest = float.MaxValue, _homeBestAt;

        internal FarmHomeTrip(TrollFarmer farmer) { _f = farmer; }

        const float WaypointMetres = 30f;

        /// A point WaypointMetres ahead on the straight line to `to` (the game's path search fails for a far destination outside the loaded area; same idea as TrollReturn, D-164).
        internal static Vector3 Waypoint(Vector3 from, Vector3 to)
        {
            var dir = to - from; dir.y = 0f;
            return from + dir.normalized * Mathf.Min(WaypointMetres, dir.magnitude);
        }

        /// A new home trip (after ResumeAtSpot). Expected outcome: not unloading, no best distance.
        internal bool Reset() { _unloading = false; _homeBest = float.MaxValue; return !_unloading; }

        /// One frame home. Expected outcome: whether the trip drove the troll (false = the farmer removed itself, the game's AI runs).
        internal bool GoHome(MonsterAI ai, float dt)
        {
            var mv = _f.Movement;
            var me = _f.GetComponent<Character>();
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            bool hasBed = profile != null && profile.HaveCustomSpawnPoint();
            // D-135: a Troll Hub (its owner's, chosen by hand or the nearest) replaces the bed as the place to go to, unload and rest
            Vector3 bed;
            bool atHub = HubRegistry.Anchor(me, hasBed ? profile.GetCustomSpawnPoint() : Vector3.zero, out bed);
            if (!hasBed && !atHub)
            {
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " has no bed or Troll Hub to go to - stays here");
                ai.SetPatrolPoint();
                _f.Remove();
                return hasBed || atHub;
            }
            if (_unloading) return Unload(ai, dt, bed);
            float d = Vector3.Distance(_f.transform.position, bed);
            // D-095 (user: "5-25m from the bed ... don't just keep walking trying to get closer without being able to do so"): a full troll unloads as soon as it is within the depot
            // ring (25 m); any troll counts as home once within 25 m when it stops getting closer (bed inside a house it cannot enter) for StuckHomeSeconds.
            if (d < _homeBest - 0.5f) { _homeBest = d; _homeBestAt = Time.time; }
            bool stuck = d <= TrollDepot.MaxFromBed && Time.time - _homeBestAt > StuckHomeSeconds;
            // D-097 (user: "as close as 15m from the bed, not right on top of the bed"): home = within HomeRadius, it waits there
            float home = ModSettings.HomeRadius.Value;
            bool there = d <= home + 1f || (_f.IsFull && d <= TrollDepot.MaxFromBed) || stuck;
            if (!there)
            {
                // a far walk goes to a point 30 m ahead, renewed every step, and runs (D-164); D-167: stuck detection, rage and detours are TrollRecovery, a hub is never abandoned
                long ka, kb;
                TrollRecovery.KeyOfHub(bed, out ka, out kb);
                var rec = TrollRecovery.Of(_f);
                if (rec.Tick(dt, bed, ka, kb, false, true, null)) return true;
                if (d > TrollFarmer.FarWalkMetres) mv.MoveTo(dt, Waypoint(_f.transform.position, bed), 0.5f, true);
                else mv.MoveTo(dt, bed, home, false);
                return !there;
            }
            { var rh = _f.GetComponent<TrollRecovery>(); if (rh != null) rh.Reached(); }          // D-167: at the hub: its counter starts again
            if (stuck && d > home + 1f && !_f.IsFull) ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " gets no closer to the bed than " + d.ToString("F1") + " m - counts as home");
            if (_f.IsFull)
            {
                // D-064: at the bed with a full backpack -> to a troll depot with room (built now when there is none)
                Vector3 at;
                if (TrollDepot.Plan(me, bed, TrollTaming.Owner(me), out at)) { _unloading = true; _unloadAt = at; _unloadStart = Time.time; return _unloading; }
                TrollIdle.RetrySoon(me);
                if (_f.IsMine()) Ui.Notify.Show(Loc.T("stevethetroll_depot_nospot", _f.Shown(), HubRegistry.DestinationName(me)), Ui.Notify.Kind.Warn);
            }
            mv.Stop();
            ai.SetPatrolPoint();
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " is at its destination (" + HubRegistry.DestinationName(me) + ", " + d.ToString("F1") + " m) - waits");
            if (!_f.Auto) Ui.Notify.Show(Loc.T("stevethetroll_farm_home", _f.Shown(), HubRegistry.DestinationName(me)), Ui.Notify.Kind.Farm);
            _f.Remove();
            return there;
        }

        // D-064: walk to the depot (within UnloadMetres), then everything goes into its chests; gives up walking after UnloadWalkSeconds.
        bool Unload(MonsterAI ai, float dt, Vector3 bed)
        {
            var flat = _unloadAt - _f.transform.position; flat.y = 0f;
            bool late = Time.time - _unloadStart > UnloadWalkSeconds;
            // D-106 (user: "empty their backpacks when they are 5-10m away from the depot, so that they don't all stumble into each other"): unloads within UnloadMetres, walks only
            // until UnloadStopMetres, never into the depot
            if (flat.magnitude > UnloadMetres && !late) { _f.Movement.MoveTo(dt, _unloadAt, UnloadStopMetres, false); return _unloading; }
            _f.Movement.Stop();
            ai.SetPatrolPoint();
            var me = _f.GetComponent<Character>();
            int left;
            int moved = TrollDepot.Unload(me, bed, TrollTaming.Owner(me), out left);
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " unloaded " + moved + " stack(s) at the depot (" + flat.magnitude.ToString("F1") + " m" + (late ? ", walked too long" : "") + "), " + left + " left");
            if (_f.IsMine()) Ui.Notify.Show(left > 0 ? Loc.T("stevethetroll_depot_full", _f.Shown(), left.ToString()) : Loc.T("stevethetroll_depot_done", _f.Shown(), moved.ToString()), left > 0 ? Ui.Notify.Kind.Warn : Ui.Notify.Kind.Farm);
            switch (HomeRules.AfterUnloading(left, TrollHome.OwnerWears(me)))
            {
                case AfterUnload.ResumeSpot:
                    if (ResumeAtSpot()) return true;
                    break;
                case AfterUnload.StayHome:
                    // D-173: no slippers - home is where it waits; GoHome walks it on to the home radius (not full any more) and the farmer ends there
                    _f.IsFull = false; _unloading = false;
                    ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " emptied its backpack at home - the owner wears no slippers, it stays home");
                    return true;
            }
            _f.Remove();
            return moved >= 0;
        }

        // User 2026-10-31 ("have trolls remember the spot where they've farmed before coming home to empty the backpack"): an emptied troll walks back to where it last worked and farms
        // on from there (focus from its ZDO). Too close to bother (SpotMinMetres), out of the hub's farm leash or no spot known -> false (the idle check sends it farming from here).
        bool ResumeAtSpot()
        {
            Vector3 spot;
            if (!_f.Spots.Spot(out spot)) return spot != Vector3.zero;
            var away = spot - _f.transform.position; away.y = 0f;
            if (away.magnitude < SpotMinMetres) return away.magnitude >= SpotMinMetres;
            var me = _f.GetComponent<Character>();
            var hub = HubRegistry.For(me);
            if (hub != null && !HubRules.FarmAllowed(true, HubGeometry.Flat(spot - hub.Pos))) return hub == null;
            var z = TrollTaming.Zdo(me);
            string focus = z != null ? Ui.TrollFocus.Normalize(z.GetString(TrollRegistry.FocusKey, "")) : "";
            _f.Setup(focus, spot, false);
            _f.IsFull = false; Reset(); _f.Auto = true;
            _f.StartTravel(spot, true);
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " emptied its backpack - walks " + away.magnitude.ToString("F0") + " m back to where it farmed (" + spot.ToString("F0") + ")" + (focus.Length > 0 ? ", focus '" + focus + "'" : ""));
            return _f.Travelling;
        }
    }
}
