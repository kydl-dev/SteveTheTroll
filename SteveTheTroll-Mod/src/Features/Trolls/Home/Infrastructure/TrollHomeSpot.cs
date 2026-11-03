using Kydl.Common;
using SteveTheTroll.Configuration;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Home.Infrastructure
{
    /// <summary>
    /// D-097 (user: "when you log out, they are brought as close as 15m from the bed, not right on top of the bed (I had to break down
    /// parts of my house cause the trolls were inside it)"): where a troll is put / waits near the bed - on a ring HomeRadius (15 m) around
    /// it, further rings 3 m apart when the near spots are taken, a free spot on the ground with nothing built above it (when the bed area is
    /// loaded; far away only the world's ground height is known).
    /// </summary>
    internal static class TrollHomeSpot
    {
        internal const int PerRing = 24, Rings = 6;
        internal const float RingStep = 3f, Lift = 0.5f, FarLift = 1.5f, TrollRadius = 1.6f;
        static int _pieceMask, _roofMask;

        /// Pure: the n-th candidate around the bed (0 = first), flat offset from the bed. Ring k = n / PerRing at radius + k * RingStep;
        /// rings are turned by half a step against each other. Expected outcome: length radius + k * RingStep.
        internal static Vector3 Candidate(int n, float radius)
        {
            if (n < 0) n = 0;
            int ring = n / PerRing, k = n % PerRing;
            float a = (k + (ring % 2) * 0.5f) * Mathf.PI * 2f / PerRing;
            float r = radius + ring * RingStep;
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        /// The `index`-th free spot (0 = first troll) around the bed. Expected outcome: a position on the ground (never inside a house when the
        /// area is loaded); the plain ring point when no free spot is found.
        internal static Vector3 Spot(Vector3 bed, int index)
        {
            float radius = ModSettings.HomeRadius.Value;
            int found = 0;
            for (int n = 0; n < PerRing * Rings; n++)
            {
                Vector3 at;
                if (!Free(bed + Candidate(n, radius), out at)) continue;
                if (found++ == index) return at;
            }
            var plain = bed + Candidate(index, radius);
            plain.y = Ground(plain, out bool _) + FarLift;
            ModLog.Warn("TrollHomeSpot: no free spot around the bed for troll " + (index + 1) + " - plain ring point " + plain.ToString("F1"));
            return plain;
        }

        static bool Free(Vector3 p, out Vector3 at)
        {
            bool loaded;
            float h = Ground(p, out loaded);
            at = new Vector3(p.x, h + (loaded ? Lift : FarLift), p.z);
            if (!loaded) return true;                                          // far away: nothing to check against
            if (_pieceMask == 0) { _pieceMask = LayerMask.GetMask("piece", "piece_nonsolid", "static_solid", "Default_small"); _roofMask = LayerMask.GetMask("piece"); }
            if (Physics.CheckSphere(at + Vector3.up * (TrollRadius + 0.2f), TrollRadius, _pieceMask)) return false;
            if (Physics.Raycast(at + Vector3.up * 1f, Vector3.up, 40f, _roofMask)) return false;     // under a roof = inside a house
            return true;
        }

        // The ground where it is loaded (terrain changes included), else the world generator's height.
        static float Ground(Vector3 p, out bool loaded)
        {
            float h = 0f;
            loaded = ZoneSystem.instance != null && ZoneSystem.instance.IsZoneLoaded(p) && ZoneSystem.instance.GetGroundHeight(p, out h);
            if (loaded) return h;
            return WorldGenerator.instance != null ? WorldGenerator.instance.GetHeight(p.x, p.z) : p.y;
        }
    }
}
