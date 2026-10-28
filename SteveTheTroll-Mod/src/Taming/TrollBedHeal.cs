using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-085 (user: "when trolls are within 50m of bed, have their hp regen 50HP/s"): every second, the local player's own tamed trolls that
    /// this client runs and that stand within BedHealRange of that player's bed get BedHealPerSecond HP (TamingRules.BedHeal, never above max).
    /// </summary>
    internal static class TrollBedHeal
    {
        const float TickSeconds = 1f;
        static float _next, _nextLog;

        /// Plugin.Update. Expected outcome: how many trolls were healed this second (-1 = nothing done this frame).
        internal static int Tick()
        {
            if (Time.time < _next) return -1;
            const float dt = TickSeconds;
            _next = Time.time + TickSeconds;
            var p = Player.m_localPlayer;
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (p == null || profile == null || !profile.HaveCustomSpawnPoint() || ModSettings.BedHealPerSecond.Value <= 0f) return 0;
            var bed = profile.GetCustomSpawnPoint();
            long me = p.GetPlayerID();
            float range2 = ModSettings.BedHealRange.Value * ModSettings.BedHealRange.Value;
            int healed = 0;
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || !TamedTrollStats.IsTamedTroll(c) || TrollTaming.Owner(c) != me) continue;
                var view = c.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner()) continue;
                var d = c.transform.position - bed; d.y = 0f;
                if (d.sqrMagnitude > range2) continue;
                float add = TamingRules.BedHeal(c.GetHealth(), c.GetMaxHealth(), ModSettings.BedHealPerSecond.Value, dt);
                if (add <= 0f) continue;
                c.Heal(add, false);
                healed++;
                if (Time.time >= _nextLog && ModLog.DebugOn) { _nextLog = Time.time + 10f; ModLog.Verbose("TrollBedHeal: " + TrollOrders.Shown(c) + " +" + add.ToString("F0") + " HP at the bed (" + c.GetHealth().ToString("F0") + " / " + c.GetMaxHealth().ToString("F0") + ")"); }
            }
            return healed;
        }
    }
}
