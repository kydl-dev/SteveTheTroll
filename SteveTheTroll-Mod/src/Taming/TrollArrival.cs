using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-043: a newly tamed troll walks to the player who tamed it (follow) and, once within ArriveMetres, stays there and waits for orders
    /// (follow off, patrol point here) with a message to that player. Added on the client that tamed it (owner of the troll's ZDO); gives up
    /// after GiveUpSeconds. Lost when the troll unloads before it arrives (it then simply follows).
    /// D-058: after arriving it keeps watching: no order (Go farm / E follow) within AutoFarmSeconds (2 min) -> it farms the default priority
    /// on its own and its owner gets a message with the item.
    /// </summary>
    internal sealed class TrollArrival : MonoBehaviour
    {
        const float ArriveMetres = 5f, GiveUpSeconds = 180f, CheckSeconds = 0.5f;
        Player _player;
        MonsterAI _ai;
        float _start, _next, _waitStart = -1f;

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
            if (_waitStart >= 0f) { Wait(); return; }
            float d = Vector3.Distance(transform.position, _player.transform.position);
            bool late = Time.time - _start > GiveUpSeconds;
            if (d > ArriveMetres && !late) return;
            _ai.SetFollowTarget(null);
            _ai.SetPatrolPoint();
            ModLog.Info("TrollArrival: " + name + (late ? " gave up walking after " + GiveUpSeconds + " s" : " arrived (" + d.ToString("F1") + " m)") + " - stays and waits for orders");
            if (!late && _player == Player.m_localPlayer) Ui.Notify.Show(Loc.T("stevethetroll_troll_waits", Shown()), Ui.Notify.Kind.Good);
            _waitStart = Time.time;
        }

        // D-058: waiting for orders. Expected outcome: ends on an order (follow / farming) or starts farming after AutoFarmSeconds.
        void Wait()
        {
            if (_ai.GetFollowTarget() != null || GetComponent<TrollFarmer>() != null) { ModLog.Verbose("TrollArrival: " + name + " got an order - stops waiting"); Destroy(this); return; }
            if (Time.time - _waitStart < ModSettings.AutoFarmSeconds.Value) return;
            ModLog.Info("TrollArrival: " + name + " got no order within " + ModSettings.AutoFarmSeconds.Value.ToString("0") + " s - farms the default priority on its own");
            var troll = GetComponent<Character>();
            Destroy(this);
            TrollFarming.Begin(troll, "", true);
        }
    }
}
