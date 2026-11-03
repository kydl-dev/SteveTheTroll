using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Farming.Infrastructure
{
    /// <summary>
    /// User 2026-10-31 (D-163): (1) beyond FarmMaxRange the troll walks to the nearest source the game knows of, (2) it remembers where it farmed last (ZDO key stt_spot, so the
    /// memory survives a re-created troll and a new farmer component) and goes back there after emptying its backpack. Far places it could not reach are skipped for a while.
    /// D-170: moved out of TrollFarmer unchanged (class size). One per farmer.
    /// </summary>
    internal sealed class FarmSpotMemory
    {
        internal const string SpotKey = "stt_spot";
        internal const float FarBadSeconds = 120f, FarBadRadius = 30f;
        readonly Component _troll;
        readonly List<KeyValuePair<Vector3, float>> _badFar = new List<KeyValuePair<Vector3, float>>();

        internal FarmSpotMemory(Component troll) { _troll = troll; }

        /// The place of the last source this troll worked on (its ZDO). Expected outcome: false when it has none.
        internal bool Spot(out Vector3 spot)
        {
            spot = Vector3.zero;
            var view = _troll.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return spot != Vector3.zero;
            spot = view.GetZDO().GetVec3(SpotKey, Vector3.zero);
            return spot != Vector3.zero;
        }

        /// Remembers `at` (owner only - never a network write on another client). Expected outcome: whether it was written.
        internal bool Remember(Vector3 at)
        {
            var view = _troll.GetComponent<ZNetView>();
            bool mine = view != null && view.IsValid() && view.IsOwner();
            if (mine) view.GetZDO().Set(SpotKey, at);
            else ModLog.Verbose("FarmSpotMemory: " + TrollTaming.Tag(_troll) + " spot " + at.ToString("F0") + " not written (not the owner)");
            return mine;
        }

        /// `p` is within FarBadRadius of a far place given up less than FarBadSeconds ago. Expected outcome: false for a fresh place.
        internal bool IsBad(Vector3 p)
        {
            for (int i = _badFar.Count - 1; i >= 0; i--)
            {
                if (Time.time >= _badFar[i].Value) { _badFar.RemoveAt(i); continue; }
                var d = _badFar[i].Key - p; d.y = 0f;
                if (d.sqrMagnitude <= FarBadRadius * FarBadRadius) return d.sqrMagnitude <= FarBadRadius * FarBadRadius;
            }
            return _badFar.Count < 0;
        }

        /// D-163: the nearest source beyond FarmMaxRange (up to FarmLongRange) the game knows of, outside the bed's no-hit ring, inside the hub's farm leash, not a given-up place.
        /// Expected outcome: true with its position and item when one exists.
        internal bool FindFar(Vector3 from, int tier, IList<Ui.TrollFocus.Goal> goals, HubInfo hub, out Vector3 pos, out string item)
        {
            Vector3 bedPos;
            bool hasBedPos = TrollNoHit.Bed(out bedPos);
            float noHitR = ModSettings.NoHitRadius.Value;
            System.Func<Vector3, bool> farSkip = p => IsBad(p)
                || (ModSettings.On && hasBedPos && noHitR > 0f && TrollNoHit.Inside(bedPos, p, noHitR))
                || (hub != null && !HubRules.FarmAllowed(true, HubGeometry.Flat(p - hub.Pos)));
            bool found = TrollFarming.FindFar(from, ModSettings.FarmLongRange.Value, ModSettings.FarmMaxRange.Value, tier, goals, farSkip, out pos, out item);
            ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(_troll) + " long-range search: " + TrollFarming.LastFar);
            return found;
        }

        /// Gives a far place up for FarBadSeconds. Expected outcome: one more bad place.
        internal int GiveUp(Vector3 p)
        {
            _badFar.Add(new KeyValuePair<Vector3, float>(p, Time.time + FarBadSeconds));
            ModLog.Verbose("FarmSpotMemory: " + TrollTaming.Tag(_troll) + " skips " + p.ToString("F0") + " for " + FarBadSeconds.ToString("0") + " s (" + _badFar.Count + " bad far place(s))");
            return _badFar.Count;
        }
    }
}
