using System.Collections.Generic;
using System.Text;
using KydlCore.Common;
using KydlCore.Features.Hub.Contracts;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Presentation.Hub.Pages;

/// <summary>
///     D-135 (user: "players will have a list of hubs and which trolls go to each hub", "a pen symbol that opens the whole
///     name / colour / emoji
///     interface"): the Troll Hub tab of the F6 hub. Per hub: its name (as the sign shows it), the distance, a pen button
///     (the sign editor works
///     from anywhere, the hub does not have to be loaded) and the trolls that go there. Below: each troll's hub (Nearest
///     hub or a chosen one).
/// </summary>
internal static class HubTab
{
    internal static KyTab Tab;

    /// <summary>
    ///     T-098: the hub list as a described page (hub name | distance | pen, the trolls of each hub, the troll -> hub
    ///     assignment). Expected outcome: notes when there is no player / hubs are off / none built yet.
    /// </summary>
    internal static KyPage Build()
    {
        var page = new KyPage();
        KySection head = page.Section(Loc.T("stevethetroll_hub_tab_heading"));
        head.Note(Loc.T("stevethetroll_hub_tab_note"));
        Player p = Player.m_localPlayer;
        if (p == null)
        {
            page.State = "noplayer";
            return page;
        }

        if (!ModSettings.HubsOn)
        {
            head.Note(Loc.T("stevethetroll_hub_tab_hubs_off"));
            page.State = "off";
            return page;
        }

        var state = new StringBuilder("hubs|");
        List<HubInfo> hubs = HubRegistry.Own(p.GetPlayerID());
        head.Note(Loc.T("stevethetroll_hub_tab_count", hubs.Count.ToString(), HubRules.MaxHubs.ToString()));
        if (hubs.Count == 0)
        {
            head.Note(Loc.T(HubPlacement.Unlocked(p) ? "stevethetroll_hub_tab_none" : "stevethetroll_hub_tab_locked"));
            page.State = "none|" + HubPlacement.Unlocked(p);
            return page;
        }

        List<TrollInfo> trolls = TrollRegistry.Trolls();
        var byHub = new Dictionary<int, List<string>>();
        foreach (TrollInfo t in trolls)
        {
            if (t.DiedTicks > 0)
            {
                continue;
            }

            HubInfo h = HubRegistry.For(p.GetPlayerID(), new Vector3(t.X, t.Y, t.Z), Assigned(t));
            if (h == null)
            {
                continue;
            }

            List<string> l;
            if (!byHub.TryGetValue(h.Number, out l))
            {
                byHub[h.Number] = l = new List<string>();
            }

            l.Add(t.Shown(k => Loc.T(k)));
        }

        KySection hubSection = page.Section("");
        foreach (HubInfo h in hubs)
        {
            HubInfo hub = h;
            string styled = Styled(hub);
            var rows = new List<IList<KyCell>>
            {
                new List<KyCell>
                {
                    KyCell.Rich(styled),
                    KyCell.Label(Loc.T("stevethetroll_hub_tab_where",
                        HubGeometry.Flat(hub.Pos - p.transform.position).ToString("0"))),
                    KyCell.Button("✎", () => HubSignMenu.Open(hub))
                }
            };
            hubSection.Table(new string[0], rows);
            List<string> names;
            string trollText = Loc.T("stevethetroll_hub_tab_trolls") + ": " + (byHub.TryGetValue(hub.Number, out names)
                ? string.Join(", ", names.ToArray())
                : Loc.T("stevethetroll_hub_tab_no_trolls"));
            hubSection.Note(trollText);
            state.Append(hub.Number).Append(':').Append(styled).Append(':').Append(trollText).Append('|');
        }

        KySection assign = page.Section(Loc.T("stevethetroll_hub_tab_assign"));
        var options = new List<KyChoice> { new("0", Loc.T("stevethetroll_hub_tab_auto")) };
        foreach (HubInfo h in hubs)
        {
            options.Add(new KyChoice(h.Number.ToString(), HubRegistry.NameOf(h.Zdo, h.Number)));
        }

        foreach (TrollInfo t in trolls)
        {
            if (t.DiedTicks > 0)
            {
                continue;
            }

            TrollInfo troll = t;
            string current = Assigned(t).ToString();
            assign.Choice(t.Shown(k => Loc.T(k)), options, current, o => Assign(troll, int.Parse(o)));
            state.Append(t.Id).Append('=').Append(current).Append('|');
        }

        foreach (KyChoice o in options)
        {
            state.Append(o.Text).Append(',');
        }

        page.State = state.ToString();
        ModLog.Verbose("HubTab: page built, " + hubs.Count + " hub(s), " + trolls.Count + " troll(s)");
        return page;
    }

    private static string Styled(HubInfo h)
    {
        var style = SignStyle.Parse(h.Zdo != null ? h.Zdo.GetString(HubRegistry.StyleKey) : "");
        return style.RenderForUi(h.Name);
    }

    private static ZDO ZdoOf(TrollInfo t)
    {
        string[] parts = (t.Id ?? "").Split(':');
        long user;
        uint id;
        if (parts.Length != 2 || !long.TryParse(parts[0], out user) || !uint.TryParse(parts[1], out id) ||
            ZDOMan.instance == null)
        {
            return null;
        }

        return ZDOMan.instance.GetZDO(new ZDOID(user, id));
    }

    private static int Assigned(TrollInfo t)
    {
        ZDO z = ZdoOf(t);
        return z != null ? z.GetInt(HubRegistry.AssignKey) : 0;
    }

    private static void Assign(TrollInfo t, int number)
    {
        ZDO z = ZdoOf(t);
        if (z == null)
        {
            ModLog.Warn("HubTab: troll " + t.Id + " is not known to the world - cannot assign");
            return;
        }

        ZNetView nv = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(z) : null;
        if (nv != null)
        {
            nv.ClaimOwnership();
        }
        else
        {
            z.SetOwner(ZDOMan.GetSessionID());
        }

        z.Set(HubRegistry.AssignKey, number);
        ModLog.Info("HubTab: " + t.Shown(null) + " now uses " + (number == 0 ? "the nearest hub" : "HUB " + number));
        if (Tab != null)
        {
            Tab.Refresh();
        }
    }
}
