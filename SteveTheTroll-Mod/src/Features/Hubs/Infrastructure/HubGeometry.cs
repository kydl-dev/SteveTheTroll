using System.Collections.Generic;

namespace SteveTheTroll.Hubs.Infrastructure
{
    /// <summary>D-171 (P7): the two Vector3 helpers of the hub rules (nearest point, flat distance), moved out of the game-free HubRules (Domain may not name Unity types).</summary>
    internal static class HubGeometry
    {
        /// Index of the point nearest to `at` (-1 for an empty list); `distance` = flat metres to it (-1).
        internal static int Nearest(IList<UnityEngine.Vector3> points, UnityEngine.Vector3 at, out float distance)
        {
            int best = -1; distance = -1f;
            if (points == null) return best;
            for (int i = 0; i < points.Count; i++)
            {
                float d = Flat(points[i] - at);
                if (best < 0 || d < distance) { best = i; distance = d; }
            }
            return best;
        }

        internal static float Flat(UnityEngine.Vector3 v) { v.y = 0f; return v.magnitude; }
    }
}
