using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Presentation.Hub.Commands;

/// <summary>
///     D-119 (user 2026-10-28): the game side of the F6 "My trolls" tab - Follow, Kill and the distance of each own tamed
///     troll.
///     - Follow: a troll near the player follows at once; a troll outside the player's active area ("far away (not loaded
///     around you)" -
///     also one kept loaded by TrollZones) is put next to the player first (user: "Teleport to you"), then follows.
///     - Kill (the tab asked Yes / No): a lethal hit with no attacker (true damage, so the elemental immunity does not
///     matter; no troll
///     turns on anyone); a far troll is brought next to the player first and dies there once it is loaded (its backpack
///     drops here).
///     - Distance: from the player to the troll's live position, or its last known one.
///     A troll whose data another player's game runs (multiplayer, it is near them) cannot be moved from here: the player
///     is told.
/// </summary>
internal static class TrollCommands
{
    private const float BehindMetres = 4f, SideMetres = 2f, PendingSeconds = 20f;
    private static readonly Dictionary<string, float> PendingKill = new(); // id -> give-up time

    /// The tab's hooks. Expected outcome: all three set.
    internal static TrollActions Actions()
    {
        return new TrollActions { Follow = id => Follow(id), Kill = id => Kill(id), Distance = Distance };
    }

    private static string Shown(string id)
    {
        TrollInfo info = TrollRegistry.Peek().Find(t => t.Id == id);
        return info != null ? info.Shown(k => Loc.T(k)) : id;
    }

    /// Expected outcome: the troll now follows the player (or was brought next to the player with its follow target set).
    internal static bool Follow(string id)
    {
        Player p = Player.m_localPlayer;
        if (p == null)
        {
            return false;
        }

        string shown = Shown(id);
        Character troll = TrollRegistry.FindLoaded(id);
        if (troll != null && ZNetScene.InActiveArea(troll.transform.position, p.transform.position))
        {
            MonsterAI ai = troll.GetComponent<MonsterAI>();
            if (ai == null)
            {
                return false;
            }

            ZNetView view = troll.GetComponent<ZNetView>();
            if (view == null || !view.IsOwner())
            {
                return Other(shown);
            }

            ai.SetFollowTarget(p.gameObject);
            Notify.Show(Loc.T("stevethetroll_follow_now", shown), Notify.Kind.Good);
            ModLog.Info("TrollCommands: " + shown + " follows " + p.GetPlayerName() + " (near, " +
                        Vector3.Distance(troll.transform.position, p.transform.position).ToString("F0") + " m)");
            return ai.GetFollowTarget() == p.gameObject;
        }

        bool moved = Bring(id, p, shown, true);
        if (moved)
        {
            Notify.Show(Loc.T("stevethetroll_follow_brought", shown), Notify.Kind.Good);
        }

        return moved;
    }

    /// Expected outcome: the troll was hit for its death now, or is on its way here to die (pending).
    internal static bool Kill(string id)
    {
        Player p = Player.m_localPlayer;
        if (p == null)
        {
            return false;
        }

        string shown = Shown(id);
        Character troll = TrollRegistry.FindLoaded(id);
        if (troll != null)
        {
            return Die(troll, shown);
        }

        if (!Bring(id, p, shown, false))
        {
            return false;
        }

        PendingKill[id] = Time.time + PendingSeconds;
        Notify.Show(Loc.T("stevethetroll_kill_brought", shown), Notify.Kind.Death);
        return PendingKill.ContainsKey(id);
    }

    private static bool Die(Character troll, string shown)
    {
        var hit = new HitData { m_point = troll.transform.position, m_dir = Vector3.down };
        float before = troll.GetHealth();
        hit.m_damage.m_damage = Mathf.Max(troll.GetMaxHealth(), before) * 10f;
        troll.Damage(hit);
        Notify.Show(Loc.T("stevethetroll_kill_done", shown), Notify.Kind.Death);
        ModLog.Info("TrollCommands: " + shown + " killed from the My trolls tab (health " + before.ToString("F0") +
                    ", hit " + hit.m_damage.m_damage.ToString("F0") + " true damage)");
        return hit.m_damage.m_damage > before;
    }

    /// A troll that is not near: its saved data is put next to the player (follow set, or nothing for a kill); a loaded one far away is
    /// moved as an object. Expected outcome: whether it was moved.
    private static bool Bring(string id, Player p, string shown, bool follow)
    {
        Vector3 spot = Spot(p);
        Character troll = TrollRegistry.FindLoaded(id);
        if (troll != null)
        {
            ZNetView view = troll.GetComponent<ZNetView>();
            if (view == null || !view.IsOwner())
            {
                return Other(shown);
            }

            Vector3 was = troll.transform.position;
            troll.transform.position = spot;
            Rigidbody body = troll.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = spot;
            }

            view.GetZDO().SetPosition(spot);
            MonsterAI ai = troll.GetComponent<MonsterAI>();
            if (follow && ai != null)
            {
                ai.SetFollowTarget(p.gameObject);
            }

            TrollRegistry.MovedTo(id, spot);
            ModLog.Info("TrollCommands: " + shown + " (loaded, outside the player's area) moved from " +
                        was.ToString("F0") + " to " + spot.ToString("F1") + (follow ? ", follows" : ", to die"));
            return true;
        }

        ZDO z = Find(id);
        if (z == null)
        {
            Notify.Show(Loc.T("stevethetroll_order_unknown"), Notify.Kind.Warn);
            ModLog.Warn("TrollCommands: no troll data with id " + id + " in this world");
            return false;
        }

        if (z.HasOwner() && !z.IsOwner())
        {
            return Other(shown);
        }

        Vector3 from = z.GetPosition();
        z.SetPosition(spot);
        z.Set(ZDOVars.s_follow, follow ? p.GetPlayerName() : "");
        z.Set(ZDOVars.s_patrol, !follow);
        if (!follow)
        {
            z.Set(ZDOVars.s_patrolPoint, spot);
        }

        TrollRegistry.MovedTo(id, spot);
        ModLog.Info("TrollCommands: " + shown + " (not loaded) moved from " + from.ToString("F0") + " (" +
                    Vector3.Distance(from, p.transform.position).ToString("F0") + " m) to " + spot.ToString("F1")
                    + (follow ? ", follow target '" + p.GetPlayerName() + "'" : ", to die when loaded"));
        return true;
    }

    private static bool Other(string shown)
    {
        Notify.Show(Loc.T("stevethetroll_order_other", shown), Notify.Kind.Warn);
        ModLog.Info("TrollCommands: " + shown + " is run by another client - not moved");
        return false;
    }

    /// Next to the player: a few metres behind and to the side, on the ground.
    private static Vector3 Spot(Player p)
    {
        Transform t = p.transform;
        Vector3 s = t.position - t.forward * BehindMetres + t.right * SideMetres;
        float ground = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(s) : s.y;
        s.y = Mathf.Max(ground, t.position.y - 2f) + 0.2f;
        return s;
    }

    private static ZDO Find(string id)
    {
        ZDOMan zm = ZDOMan.instance;
        if (zm == null)
        {
            return null;
        }

        var list = new List<ZDO>();
        int index = 0, guard = 0;
        while (!zm.GetAllZDOsWithPrefabIterative(TrollTaming.TrollPrefab, list, ref index) && guard++ < 10000)
        {
        }

        foreach (ZDO z in list)
        {
            if (z != null && z.GetString(TrollRegistry.IdKey) == id)
            {
                return z;
            }
        }

        return null;
    }

    /// Metres from the player to the troll (live position when loaded, else the last known one); -1 unknown.
    internal static float Distance(string id)
    {
        Player p = Player.m_localPlayer;
        if (p == null)
        {
            return -1f;
        }

        Character troll = TrollRegistry.FindLoaded(id);
        if (troll != null)
        {
            return Vector3.Distance(troll.transform.position, p.transform.position);
        }

        TrollInfo info = TrollRegistry.Peek().Find(t => t.Id == id);
        return info != null ? Vector3.Distance(new Vector3(info.X, info.Y, info.Z), p.transform.position) : -1f;
    }

    /// Plugin.Update: far trolls sent here to die are killed as soon as they are loaded. Expected outcome: how many are still pending.
    internal static int Tick()
    {
        if (PendingKill.Count == 0)
        {
            return 0;
        }

        var done = new List<string>();
        foreach (KeyValuePair<string, float> kv in PendingKill)
        {
            Character troll = TrollRegistry.FindLoaded(kv.Key);
            if (troll != null)
            {
                if (!troll.IsDead())
                {
                    Die(troll, Shown(kv.Key));
                }

                done.Add(kv.Key);
                continue;
            }

            if (Time.time > kv.Value)
            {
                ModLog.Warn("TrollCommands: " + Shown(kv.Key) + " was not loaded within " + PendingSeconds +
                            " s - not killed");
                done.Add(kv.Key);
            }
        }

        foreach (string id in done)
        {
            PendingKill.Remove(id);
        }

        return PendingKill.Count;
    }
}
