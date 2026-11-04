using System;
using System.Collections.Generic;
using System.Globalization;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Registry.Infrastructure;

/// <summary>
///     D-043: the local player's tamed trolls for the F6 tabs, kept in the player's profile (Player.m_customData, one
///     TrollInfo line each) so
///     far-away trolls show their last known values (user answer 2026-10-27). Every RefreshSeconds the loaded own trolls
///     are read live; a
///     focus chosen while a troll is far (or owned by another client) waits in PendingFocus and is written when this
///     client owns its ZDO.
///     Troll id = our own id in the troll's ZDO (stt_id, made at taming or on first sight) - stable across reloads.
/// </summary>
internal static partial class TrollRegistry
{
    public const string DataKey = "stevethetroll_trolls";
    public const string IdKey = "stt_id";
    public const string FocusKey = "stt_focus";
    private const float RefreshSeconds = 2f;

    // D-142 (test run 235053: the refresh took 20 - 38 ms every 2 s with six trolls): the cheap values (health, position, status, focus, name) are read every refresh, the
    // costly ones only now and then - attack / resistance texts (localised strings) every SlowSeconds, the backpack fill every BackpackSeconds, the biome when the troll moved
    // BiomeMoveMeters. Opening a tab (force) reads everything.
    private const float SlowSeconds = 30f, BackpackSeconds = 6f, BiomeMoveMeters = 48f;
    private static readonly List<TrollInfo> Known = new();
    private static readonly Dictionary<string, string> PendingFocus = new();
    private static long _loadedFor;
    private static float _next;
    private static int _lastLiveCount = -1;
    private static readonly Dictionary<string, float> SlowAt = new(), PackAt = new();
    private static readonly Dictionary<string, Vector3> BiomeFrom = new();
    private static bool _forceRead;

    private static readonly string[] DamageNames =
        { "Blunt", "Slash", "Pierce", "Chop", "Pickaxe", "Fire", "Frost", "Lightning", "Poison", "Spirit" };

    /// The list (refreshed first). Expected outcome: never null.
    internal static List<TrollInfo> Trolls()
    {
        Refresh(true);
        return Known;
    }

    /// D-081: the list as the last refresh left it (no refresh, no log - for the map pins every 2 s). Expected outcome: never null.
    internal static List<TrollInfo> Peek()
    {
        Load();
        return Known;
    }

    /// D-084: the player's living tamed trolls in total (near or far, from the saved list). Expected outcome: 0 or more.
    internal static int LivingCount()
    {
        Load();
        int n = 0;
        foreach (TrollInfo t in Known)
        {
            if (t.DiedTicks <= 0)
            {
                n++;
            }
        }

        return n;
    }

    /// Plugin.Update. Expected outcome: returns whether a refresh ran this frame.
    internal static bool Tick()
    {
        if (Time.time < _next || Player.m_localPlayer == null || !ModSettings.On)
        {
            return false;
        }

        _next = Time.time + RefreshSeconds;
        return Refresh(false);
    }

    /// Our id of a troll (made when missing, owner only). "" when it has none and this client cannot write it.
    internal static string IdOf(Character troll)
    {
        ZDO zdo = TrollTaming.Zdo(troll);
        if (zdo == null)
        {
            return "";
        }

        string id = zdo.GetString(IdKey);
        ZNetView view = troll.GetComponent<ZNetView>();
        if (id.Length == 0 && view != null && view.IsOwner())
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 12);
            zdo.Set(IdKey, id);
            ModLog.Verbose("TrollRegistry: " + TrollTaming.Tag(troll) + " got id " + id);
        }

        return id;
    }

    /// Expected outcome: the troll's focus is stored (now, or pending until this client owns it); returns whether it was written now.
    internal static bool SetFocus(string id, string focus)
    {
        Load();
        TrollInfo t = Known.Find(k => k.Id == id);
        if (t != null)
        {
            t.Focus = focus;
        }

        Character live = FindLoaded(id);
        ZNetView view = live != null ? live.GetComponent<ZNetView>() : null;
        bool now = view != null && view.IsValid() && view.IsOwner();
        if (now)
        {
            view.GetZDO().Set(FocusKey, focus);
            PendingFocus.Remove(id);
        }
        else
        {
            PendingFocus[id] = focus;
        }

        Save();
        ModLog.Info("TrollRegistry: focus of " + (t != null ? t.Shown(null) : id) + " -> '" + focus + "' (" +
                    (now
                        ? "written to the troll"
                        : "pending: troll " + (live != null ? "owned by another client" : "not loaded")) + ")");
        return now;
    }

    /// The loaded troll with our id (null when far).
    /// D-126: the game's biome name at a position ("" before the world generator exists).
    internal static string BiomeAt(Vector3 pos)
    {
        return WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(pos).ToString() : "";
    }

    /// D-081: a troll moved without being loaded (logout to the bed): its last known position becomes the new one (map pin, F6).
    internal static void MovedTo(string id, Vector3 pos)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        Load();
        TrollInfo t = Known.Find(k => k.Id == id);
        if (t == null)
        {
            return;
        }

        t.X = pos.x;
        t.Y = pos.y;
        t.Z = pos.z;
        SetDestination(t, pos,
            HubRegistry.For(Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L, pos, 0));
        t.Biome = BiomeAt(pos);
        t.LastSeenTicks = DateTime.UtcNow.Ticks;
        Save();
    }

    internal static Character FindLoaded(string id)
    {
        foreach (Character c in Character.GetAllCharacters())
        {
            if (c != null && TrollTaming.IsTroll(c) && c.IsTamed() && TrollTaming.Zdo(c) != null &&
                TrollTaming.Zdo(c).GetString(IdKey) == id)
            {
                return c;
            }
        }

        return null;
    }

    private static bool Refresh(bool force)
    {
        Player p = Player.m_localPlayer;
        if (p == null)
        {
            return false;
        }

        Load();
        _forceRead = force;
        long me = p.GetPlayerID();
        int live = 0;
        bool changed = false;
        foreach (TrollInfo t in Known)
        {
            t.Loaded = false; // Read sets it again for every troll seen now
        }

        foreach (Character c in Character.GetAllCharacters())
        {
            if (c == null || !TrollTaming.IsTroll(c) || !c.IsTamed() || TrollTaming.Owner(c) != me)
            {
                continue;
            }

            string id = IdOf(c);
            if (id.Length == 0)
            {
                continue;
            }

            TrollInfo t = Known.Find(k => k.Id == id);
            if (t == null)
            {
                t = new TrollInfo { Id = id };
                Known.Add(t);
                changed = true;
                ModLog.Info("TrollRegistry: new troll " + id + " in the list (" + Known.Count + " now)");
            }

            if (t.Name.Length == 0 && c.GetComponent<ZNetView>() != null && c.GetComponent<ZNetView>().IsOwner() &&
                c.GetComponent<Tameable>() != null)
            {
                string n = NameFor(c);
                c.GetComponent<Tameable>().SetText(n);
                ModLog.Info("TrollRegistry: unnamed tamed troll [" + id + "] named '" + n + "' (D-047: never 'Troll')");
            }

            Read(c, t);
            live++;
        }

        int gone = RemoveDead();
        if (gone > 0)
        {
            changed = true;
        }

        for (int i = 0; i < Known.Count; i++)
        {
            Known[i].Number = i + 1;
        }

        if (live != _lastLiveCount || force || changed)
        {
            _lastLiveCount = live;
            ModLog.Verbose("TrollRegistry: " + Known.Count + " troll(s), " + live + " loaded" +
                           (PendingFocus.Count > 0 ? ", " + PendingFocus.Count + " focus change(s) pending" : ""));
        }

        Save();
        return true;
    }

    private static void Read(Character c, TrollInfo t)
    {
        ZDO zdo = TrollTaming.Zdo(c);
        ZNetView view = c.GetComponent<ZNetView>();
        string pending;
        if (PendingFocus.TryGetValue(t.Id, out pending) && view != null && view.IsOwner())
        {
            zdo.Set(FocusKey, pending);
            PendingFocus.Remove(t.Id);
            ModLog.Info("TrollRegistry: pending focus '" + pending + "' written to " + TrollTaming.Tag(c));
        }

        Tameable tame = c.GetComponent<Tameable>();
        t.Loaded = true;
        t.Name = tame != null ? tame.GetText() ?? "" : "";
        t.Status = StatusOf(c);
        t.Focus = zdo != null ? zdo.GetString(FocusKey, t.Focus) : t.Focus;
        t.Farming = TrollFarming.Farming(c);
        float now = Time.time, last;
        if (_forceRead || !SlowAt.TryGetValue(t.Id, out last) || now - last >= SlowSeconds)
        {
            SlowAt[t.Id] = now;
            t.Attacks = AttacksOf(c);
            t.Resists = ResistsOf(c);
        }

        t.Health = c.GetHealth();
        t.MaxHealth = c.GetMaxHealth();
        t.Stars = Math.Max(0, c.GetLevel() - 1);
        t.DamageFactor = ModSettings.TamedDamageFactor.Value;
        Vector3 pos = c.transform.position;
        t.X = pos.x;
        t.Y = pos.y;
        t.Z = pos.z;
        SetDestination(t, pos, HubRegistry.For(c));
        Vector3 from;
        if (_forceRead || t.Biome.Length == 0 || !BiomeFrom.TryGetValue(t.Id, out from) ||
            (pos - from).sqrMagnitude > BiomeMoveMeters * BiomeMoveMeters)
        {
            t.Biome = BiomeAt(pos); // D-126: the focus drop-down's biome items
            BiomeFrom[t.Id] = pos;
        }

        if (_forceRead || !PackAt.TryGetValue(t.Id, out last) || now - last >= BackpackSeconds)
        {
            PackAt[t.Id] = now;
            BackpackStore.Fill(zdo, out t.BackpackUsed, out t.BackpackSlots); // D-096
        }

        t.LastSeenTicks = DateTime.UtcNow.Ticks;
    }
}
