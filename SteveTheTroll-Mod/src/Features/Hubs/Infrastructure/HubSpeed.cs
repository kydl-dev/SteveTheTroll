using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Costume.Infrastructure;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using UnityEngine;

namespace SteveTheTroll.Features.Hubs.Infrastructure
{
    /// <summary>
    /// D-135 (user 2026-10-30): with Troll Hubs on and the set bonus active, a tamed troll with no hub within 1250 m works at half its usual tamed
    /// speed (the player is told), and inside a hub's 500 m circle it is 50 % faster than vanilla instead of 25 %. The numbers are HubRules.SpeedFactor;
    /// this reads the world. The distance to the nearest hub is looked up once a second per troll.
    /// </summary>
    internal static class HubSpeed
    {
        sealed class Seen { public float At, Nearest; }
        static readonly Dictionary<int, Seen> Cache = new Dictionary<int, Seen>();
        static float _nextNotice, _nextTick;

        static bool SetActiveFor(long owner)
        {
            int h = CostumeItems.SetEffectHash;
            if (h == 0) return false;
            foreach (var p in Player.GetAllPlayers())
                if (p != null && p.GetPlayerID() == owner) return p.GetSEMan() != null && p.GetSEMan().HaveStatusEffect(h);
            return false;
        }

        /// Flat metres from the troll to the nearest hub of its owner (-1 = none), refreshed once a second.
        internal static float Nearest(Character c)
        {
            int id = c.GetInstanceID();
            Seen s;
            if (!Cache.TryGetValue(id, out s)) { s = new Seen { At = -10f, Nearest = -1f }; Cache[id] = s; }
            if (Time.time - s.At >= 1f)
            {
                s.At = Time.time;
                float d;
                HubRegistry.Nearest(TrollTaming.Owner(c), c.transform.position, out d);
                s.Nearest = d;
            }
            return s.Nearest;
        }

        /// The movement / animation factor of a tamed troll. Expected outcome: `tamed` unless hubs are on, the owner has the set bonus and
        /// the troll is inside a hub circle (1.5) or has no hub within 1250 m (half).
        internal static float Factor(Character c, float tamed)
        {
            if (c == null || !ModSettings.HubsOn) return tamed;
            long owner = TrollTaming.Owner(c);
            if (owner == 0L || !SetActiveFor(owner)) return tamed;
            return HubRules.SpeedFactor(tamed, true, true, Nearest(c));
        }

        /// Plugin.Update (every 5 s): tells the player once in a while when own trolls work at half speed for lack of a hub.
        internal static int Tick()
        {
            Notify.TickTimed();
            if (Time.time < _nextTick || !ModSettings.HubsOn) return 0;
            _nextTick = Time.time + 2f;
            var p = Player.m_localPlayer;
            if (p == null) return 0;
            long me = p.GetPlayerID();
            int slow = 0;
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c) || TrollTaming.Owner(c) != me) continue;
                if (Factor(c, ModSettings.TamedSpeedFactor.Value) < ModSettings.TamedSpeedFactor.Value - 0.001f) slow++;
            }
            if (slow > 0 && Time.time >= _nextNotice)
            {
                _nextNotice = Time.time + 30f;
                Notify.Timed(Loc.T("stevethetroll_hub_msg_half", HubRules.FarRadius.ToString("0")), Notify.Kind.Bad, 5f);
                ModLog.Info("HubSpeed: " + slow + " own troll(s) work at half speed - no Troll Hub within " + HubRules.FarRadius + " m");
            }
            if (slow == 0) _nextNotice = 0f;
            return slow;
        }
    }
}
