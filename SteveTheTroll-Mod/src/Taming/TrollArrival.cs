using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-043: a newly tamed troll walks to the player who tamed it (follow) and, once within ArriveMetres, stays there and waits for orders
    /// (follow off, patrol point here) with a message to that player. Added on the client that tamed it (owner of the troll's ZDO); gives up
    /// after GiveUpSeconds. Lost when the troll unloads before it arrives (it then simply follows).
    /// D-058 / D-062: after arriving it is idle - TrollIdle sends it farming after AutoFarmSeconds (2 min) without an order.
    /// </summary>
    internal sealed class TrollArrival : MonoBehaviour
    {
        const float ArriveMetres = 5f, GiveUpSeconds = 180f, CheckSeconds = 0.5f;
        Player _player;
        MonsterAI _ai;
        float _start, _next;

        internal static TrollArrival Begin(Character troll, Player player)
        {
            var ai = troll != null ? troll.GetComponent<MonsterAI>() : null;
            if (ai == null || player == null) { ModLog.Warn("TrollArrival: no AI / player - the troll stays where it is"); return null; }
            var a = troll.gameObject.AddComponent<TrollArrival>();
            a._player = player; a._ai = ai; a._start = Time.time;
            ai.SetFollowTarget(player.gameObject);
            ModLog.Info("TrollArrival: " + troll.name + " walks to " + player.GetPlayerName() + " (" + Vector3.Distance(troll.transform.position, player.transform.position).ToString("F1") + " m)");
            return a;
        }

        string Shown()
        {
            var t = GetComponent<Tameable>();
            string n = t != null ? t.GetText() : "";
            return string.IsNullOrEmpty(n) ? name : n;
        }

        void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + CheckSeconds;
            if (_player == null || _ai == null) { ModLog.Verbose("TrollArrival: " + name + " lost its player / AI - done"); Destroy(this); return; }
            float d = Vector3.Distance(transform.position, _player.transform.position);
            bool late = Time.time - _start > GiveUpSeconds;
            if (d > ArriveMetres && !late) return;
            _ai.SetFollowTarget(null);
            _ai.SetPatrolPoint();
            ModLog.Info("TrollArrival: " + name + (late ? " gave up walking after " + GiveUpSeconds + " s" : " arrived (" + d.ToString("F1") + " m)") + " - stays and waits for orders");
            if (!late && _player == Player.m_localPlayer) Ui.Notify.Show(Loc.T("stevethetroll_troll_waits", Shown()), Ui.Notify.Kind.Good);
            Destroy(this);
        }
    }
}
