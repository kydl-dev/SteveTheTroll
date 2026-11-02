using HarmonyLib;
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
        static readonly AccessTools.FieldRef<Character, float> MaxAirAltitude = AccessTools.FieldRefAccess<Character, float>("m_maxAirAltitude");

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
            Place(p, _spot, Quaternion.identity);         // D-172: always facing world +z - a test's dz > 0 is in front of the player
            if (!p.InGodMode()) p.SetGodMode(true);      // D-172 (user): god mode on, fly off - creatures still target the player, but no test dies by accident
            ModLog.Verbose("TestArena: player moved to " + _spot.ToString("F0") + ", god mode " + p.InGodMode());
            return _spot;
        }

        /// D-171: the player walks (teleports) `metres` further out, AWAY from the lab start along the start -> spot line (a plain dx teleport went back towards the lab house).
        /// The bed stays at the spot. Expected outcome: the new position.
        [TestHook("stt.arenaFurther")]
        static Vector3 Further(float metres)
        {
            var p = Player.m_localPlayer;
            if (!_known) MovePlayer(p);
            var dir = _spot - _start; dir.y = 0f; dir.Normalize();
            var to = _spot + dir * metres;
            to.y = ZoneSystem.instance.GetGroundHeight(to) + 0.2f;
            Place(p, to, Quaternion.identity);           // D-172: same facing as after stt.reset (world +z), so dz > 0 stays "in front"
            ModLog.Info("TestArena: player " + metres.ToString("F0") + " m further out at " + to.ToString("F0") + " (stands at " + p.transform.position.ToString("F0") + "), facing +z");
            return p.transform.position;
        }

        /// D-172 (user: god mode + the player died at stt.arenaFurther): god mode for the test player, on or off (defend-rage switches it off around stt.hitPlayer).
        /// Expected outcome: the god mode afterwards.
        [TestHook("stt.godMode")]
        static bool GodMode(bool on)
        {
            var p = Player.m_localPlayer;
            if (p.InGodMode() != on) p.SetGodMode(on);
            ModLog.Info("TestArena: god mode " + p.InGodMode());
            return p.InGodMode();
        }

        /// D-172: puts the player there AT ONCE. TeleportTo was the bug: its move finishes frames later (after the area loads), so the reset's teleport to the spot landed AFTER
        /// stt.arenaFurther and the tree / troll of the attack-key tests appeared at the bed; and setting the position by hand next to it kept the fall height, so a 9 m step
        /// down counted as a fall (the player died, 2026-11-02). The test spots are near the loaded area, so a direct move is safe; velocity and fall height are reset.
        static void Place(Player p, Vector3 to, Quaternion rot)
        {
            var body = p.GetComponent<Rigidbody>();
            if (body != null) { body.linearVelocity = Vector3.zero; body.position = to; body.rotation = rot; }
            p.transform.position = to;
            p.transform.rotation = rot;
            p.SetLookDir(rot * Vector3.forward);      // the look direction turns the body every frame - set it too, or the facing springs back
            MaxAirAltitude(p) = to.y;
            ZNet.instance.SetReferencePosition(to);
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
