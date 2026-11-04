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

/// <summary>Part 2 of TrollRegistry (split out so the class stays under the size limit).</summary>
internal static partial class TrollRegistry
{
    private static string StatusOf(Character c)
    {
        if (c.IsDead())
        {
            return "stevethetroll_status_dead";
        }

        MonsterAI ai = c.GetComponent<MonsterAI>();
        if (ai == null)
        {
            return "stevethetroll_status_unknown";
        }

        if (ai.GetTargetCreature() != null)
        {
            return "stevethetroll_status_fighting";
        }

        if (c.GetComponent<TrollArrival>() != null)
        {
            return "stevethetroll_status_coming";
        }

        if (c.GetComponent<TrollFarmer>() != null)
        {
            return "stevethetroll_status_farming";
        }

        return ai.GetFollowTarget() != null ? "stevethetroll_status_following" : "stevethetroll_status_staying";
    }

    /// D-162 (user: "in the page where it says troll distance to bed, say hub when there are hubs"): the distance is to the troll's hub (named) when it has one, else to the bed.
    private static void SetDestination(TrollInfo t, Vector3 pos, HubInfo hub)
    {
        if (hub == null && ModSettings.HubsOn && Player.m_localPlayer != null)
        {
            // user 2026-11-04: once the first hub is built the Trolls tab says "from hub", never "from your bed" - also for a troll the
            // assignment rules leave without a hub (too far from every hub): the distance is then to the nearest own hub
            float nearestDist;
            hub = HubRegistry.Nearest(Player.m_localPlayer.GetPlayerID(), pos, out nearestDist);
        }

        if (hub != null)
        {
            t.Dest = hub.Name;
            t.BedDistance = HubGeometry.Flat(hub.Pos - pos);
        }
        else
        {
            t.Dest = "";
            t.BedDistance = BedDistance(pos);
        }
    }

    /// Metres from the local player's bed (spawn point), -1 without one.
    internal static float BedDistance(Vector3 pos)
    {
        PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
        if (profile == null || !profile.HaveCustomSpawnPoint())
        {
            return -1f;
        }

        return Vector3.Distance(pos, profile.GetCustomSpawnPoint());
    }

    /// D-044: the owner is told, the entry stays DeadKeepSeconds (status Dead), then it is removed everywhere (list, pending focus) and its
    /// name is free again.
    internal static void MarkDead(string id)
    {
        Load();
        TrollInfo t = Known.Find(k => k.Id == id);
        if (t == null)
        {
            return;
        }

        t.Status = "stevethetroll_status_dead";
        t.Health = 0f;
        t.Loaded = false;
        t.LastSeenTicks = DateTime.UtcNow.Ticks;
        t.DiedTicks = t.LastSeenTicks;
        Save();
        string shown = t.Shown(k => Loc.T(k));
        Notify.Show(Loc.T("stevethetroll_troll_died", shown), Notify.Kind.Death);
        ModLog.Info("TrollRegistry: " + shown + " [" + id + "] died - removed in " + ModSettings.DeadKeepSeconds.Value +
                    " s");
    }

    private static int RemoveDead()
    {
        long keep = (long)(ModSettings.DeadKeepSeconds.Value * TimeSpan.TicksPerSecond), now = DateTime.UtcNow.Ticks;
        int removed = 0;
        for (int i = Known.Count - 1; i >= 0; i--)
        {
            TrollInfo t = Known[i];
            if (t.DiedTicks <= 0 || now - t.DiedTicks < keep)
            {
                continue;
            }

            Known.RemoveAt(i);
            PendingFocus.Remove(t.Id);
            removed++;
            ModLog.Info("TrollRegistry: " + t.Shown(null) + " [" + t.Id + "] removed (dead for " +
                        ModSettings.DeadKeepSeconds.Value + " s), name '" + t.Name + "' is free");
        }

        return removed;
    }

    /// D-044: the name a newly tamed troll gets: "Steve N" with a club (log), "Spencer N" without; N = lowest free among living trolls.
    internal static string NameFor(Character troll)
    {
        Load();
        var names = new List<string>();
        foreach (TrollInfo t in Known)
        {
            if (t.Name.Length > 0)
            {
                names.Add(t.Name); // a dead troll's name is free once its entry is removed
            }
        }

        var h = troll as Humanoid;
        bool club = false;
        if (h != null)
        {
            foreach (ItemDrop.ItemData it in h.GetInventory().GetAllItems())
            {
                if (it.m_dropPrefab != null && it.m_dropPrefab.name.StartsWith("troll_log"))
                {
                    club = true;
                }
            }
        }

        string name = TamingRules.NextName(club ? "Steve" : "Spencer", names);
        ModLog.Verbose("TrollRegistry: name for " + TrollTaming.Tag(troll) + ": club " + club + ", taken [" +
                       string.Join(", ", names.ToArray()) + "] -> " + name);
        return name;
    }

    // "name~Blunt=180,Chop=300~4;..." for the Trolls tab: each attack item's damage x the level and tamed factors, and its AI interval.
    private static string AttacksOf(Character c)
    {
        var h = c as Humanoid;
        if (h == null)
        {
            return "";
        }

        float factor = (1f + Math.Max(0, c.GetLevel() - 1) * 0.5f) * ModSettings.TamedDamageFactor.Value;
        var parts = new List<string>();
        foreach (ItemDrop.ItemData it in h.GetInventory().GetAllItems())
        {
            HitData.DamageTypes d = it.m_shared.m_damages;
            float[] v =
            {
                d.m_blunt, d.m_slash, d.m_pierce, d.m_chop, d.m_pickaxe, d.m_fire, d.m_frost, d.m_lightning, d.m_poison,
                d.m_spirit
            };
            var dmg = new List<string>();
            for (int i = 0; i < v.Length; i++)
            {
                if (v[i] > 0f)
                {
                    dmg.Add(DamageNames[i] + "=" + (v[i] * factor).ToString("0", CultureInfo.InvariantCulture));
                }
            }

            if (dmg.Count == 0)
            {
                continue;
            }

            string name = Localization.instance != null
                ? Localization.instance.Localize(it.m_shared.m_name)
                : it.m_shared.m_name;
            parts.Add(name.Replace("~", "-").Replace(";", ",") + "~" + string.Join(",", dmg.ToArray()) + "~" +
                      it.m_shared.m_aiAttackInterval.ToString("0.#", CultureInfo.InvariantCulture));
        }

        return string.Join(";", parts.ToArray());
    }

    private static string ResistsOf(Character c)
    {
        HitData.DamageModifiers m = c.GetDamageModifiers();
        HitData.DamageModifier[] v =
        {
            m.m_blunt, m.m_slash, m.m_pierce, m.m_chop, m.m_pickaxe, m.m_fire, m.m_frost, m.m_lightning, m.m_poison,
            m.m_spirit
        };
        var parts = new List<string>();
        for (int i = 0; i < v.Length; i++)
        {
            parts.Add(DamageNames[i] + "=" + v[i]);
        }

        return string.Join(",", parts.ToArray());
    }

    // The list belongs to the local player: (re)read it when the player changed (character switch).
    private static void Load()
    {
        Player p = Player.m_localPlayer;
        if (p == null || _loadedFor == p.GetPlayerID())
        {
            return;
        }

        _loadedFor = p.GetPlayerID();
        Known.Clear();
        PendingFocus.Clear();
        string data;
        int bad = 0;
        if (p.m_customData.TryGetValue(DataKey, out data))
        {
            foreach (string line in data.Split('\n'))
            {
                if (line.Length == 0)
                {
                    continue;
                }

                var t = TrollInfo.Decode(line);
                if (t != null && t.Status == "stevethetroll_status_dead" && t.DiedTicks <= 0)
                {
                    t.DiedTicks = Math.Max(1L, t.LastSeenTicks);
                    ModLog.Info("TrollRegistry: " + t.Shown(null) + " [" + t.Id +
                                "] was dead without a death time (old entry) - removed with the next refresh");
                }

                if (t != null)
                {
                    t.Loaded = false;
                    Known.Add(t);
                }
                else
                {
                    bad++;
                }
            }
        }

        ModLog.Verbose("TrollRegistry: loaded " + Known.Count + " troll(s) of " + p.GetPlayerName() +
                       " from the profile" + (bad > 0 ? ", " + bad + " bad line(s) dropped" : ""));
        ModLog.Check("TrollRegistry: bad profile lines", bad, 0, 0, "");
    }

    private static void Save()
    {
        Player p = Player.m_localPlayer;
        if (p == null)
        {
            return;
        }

        var lines = new List<string>(Known.Count);
        foreach (TrollInfo t in Known)
        {
            lines.Add(t.Encode());
        }

        p.m_customData[DataKey] = string.Join("\n", lines.ToArray());
    }

    // Harmony hook: src/Integrations/Harmony/TrollsRegistryPatches.cs, TrollRegistry_Death (MIGRATION_DECISIONS A5)
    internal static class Death
    {
        internal static void Postfix(Tameable __instance)
        {
            Character c = __instance != null ? __instance.GetComponent<Character>() : null;
            if (c == null || !TrollTaming.IsTroll(c) || Player.m_localPlayer == null ||
                TrollTaming.Owner(c) != Player.m_localPlayer.GetPlayerID())
            {
                return;
            }

            ZDO zdo = TrollTaming.Zdo(c);
            string id = zdo != null ? zdo.GetString(IdKey) : "";
            if (id.Length > 0)
            {
                MarkDead(id);
            }
        }
    }
}
