using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Testing
{
    /// <summary>
    /// D-171 (user 2026-11-02: "from start spawn player at least 50m away from spawn. trolls keep getting stuck into the stones for boss trophies"): every stt.reset puts the
    /// player on the SAME open spot, ArenaMetres from the lab's start point, so (1) no troll walks into the boss-trophy stones / the lab house at the start, and (2) a test
    /// never inherits the position a previous test teleported the player to (the drift that broke recovery-walled-tree and the attack-key tests). The spot is chosen once:
    /// of 16 directions, the flattest dry 40 x 40 m area.
    /// </summary>
    internal static class TestArena
    {
        const float ArenaMetres = 70f, AreaHalf = 20f, MaxWater = 0.1f;
        static bool _known;
        static Vector3 _start, _spot;

        /// Moves the player to the arena spot (chosen on the first call). Expected outcome: the spot (the player stands there).
        internal static Vector3 MovePlayer(Player p)
        {
            if (!_known)
            {
                _known = true;
                _start = p.transform.position;
                _spot = Choose(_start);
                ModLog.Info("TestArena: lab start " + _start.ToString("F0") + " -> test spot " + _spot.ToString("F0") + " (" + Flat(_spot - _start).ToString("F0") + " m away, flattest dry area of 16 directions)");
            }
            p.TeleportTo(_spot, p.transform.rotation, false);
            p.transform.position = _spot;
            ModLog.Verbose("TestArena: player moved to " + _spot.ToString("F0"));
            return _spot;
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        static Vector3 Choose(Vector3 start)
        {
            var zs = ZoneSystem.instance;
            Vector3 best = start + new Vector3(ArenaMetres, 0f, 0f);
            float bestScore = float.MaxValue;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f;
                var c = start + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ArenaMetres;
                float lo = float.MaxValue, hi = float.MinValue;
                bool wet = false;
                for (float x = -AreaHalf; x <= AreaHalf; x += 5f)
                    for (float z = -AreaHalf; z <= AreaHalf; z += 5f)
                    {
                        var q = c + new Vector3(x, 0f, z);
                        float h = zs.GetGroundHeight(q);
                        lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
                        if (Floating.GetLiquidLevel(new Vector3(q.x, h, q.z)) - h > MaxWater) wet = true;
                    }
                float score = (hi - lo) + (wet ? 1000f : 0f);
                ModLog.Verbose("TestArena: direction " + k + " uneven " + (hi - lo).ToString("F1") + " m, water " + wet);
                if (score < bestScore) { bestScore = score; best = new Vector3(c.x, zs.GetGroundHeight(c) + 0.2f, c.z); }
            }
            return best;
        }
    }
}
