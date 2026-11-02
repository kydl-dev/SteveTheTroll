using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Trolls.Farming.Domain;
using UnityEngine;

namespace SteveTheTroll.Trolls.Farming.Infrastructure
{
    /// <summary>
    /// D-151 (user: "not have 2 that go for the same tree and one waits behind the first"): the reservation of the source a troll works on. Per client, which holds all own trolls
    /// it can see; kept while the holder refreshes it every step, gone TargetRules.HoldSeconds after the last refresh or at once when it gives up / is removed.
    /// D-170: moved out of TrollFarmer unchanged (class size).
    /// </summary>
    internal static class FarmReservations
    {
        const int MaxEntries = 200;
        static readonly Dictionary<int, KeyValuePair<int, float>> Held = new Dictionary<int, KeyValuePair<int, float>>();

        /// Another farmer than `me` holds `g` and refreshed it lately. Expected outcome: false for a free source or my own.
        internal static bool HeldByOther(GameObject g, int me)
        {
            KeyValuePair<int, float> h;
            return g != null && Held.TryGetValue(g.GetInstanceID(), out h) && TargetRules.HeldByOther(h.Key, h.Value, me, Time.time, TargetRules.HoldSeconds);
        }

        /// `me` holds `g` from now. Expected outcome: g is held by me.
        internal static bool Hold(GameObject g, int me)
        {
            if (g == null) return g != null;
            if (Held.Count > MaxEntries)
            {
                ModLog.Verbose("FarmReservations: " + Held.Count + " reservations - cleared (stale ones from destroyed sources)");
                Held.Clear();
            }
            Held[g.GetInstanceID()] = new KeyValuePair<int, float>(me, Time.time);
            return Held.ContainsKey(g.GetInstanceID());
        }

        /// Drops my reservation of `g`. Expected outcome: g is not held by me.
        internal static bool Release(GameObject g, int me)
        {
            if (g == null) return g == null;
            KeyValuePair<int, float> h;
            if (Held.TryGetValue(g.GetInstanceID(), out h) && h.Key == me) Held.Remove(g.GetInstanceID());
            return !Held.TryGetValue(g.GetInstanceID(), out h) || h.Key != me;
        }
    }
}
