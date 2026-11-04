using System;
using System.Collections.Generic;
using System.Text;
using KydlCore.Common;
using KydlCore.Features.Hub.Contracts;
using SteveTheTroll.Presentation.Hub.Registration;
using SteveTheTroll.Presentation.Hub.ViewModels;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Presentation.Hub.Pages;

/// <summary>
///     T-098: the Slippers and Costume tabs as described pages: the 3D view is the one hosted element (KydlCore adds it
///     and sets the text size), everything else is rows.
///     The State is the shown text, so the view is built again (and loses its rotation) only when a shown value changed,
///     e.g. after a settings change.
/// </summary>
internal static class ModelKyPages
{
    private static void Details(KyPage page, IList<DetailGroup> groups, string q1, string qMax, StringBuilder state)
    {
        if (groups == null)
        {
            return;
        }

        foreach (DetailGroup g in groups)
        {
            KySection s = page.Section(g.Title);
            state.Append(g.Title).Append('|');
            bool hasMax = false;
            foreach (DetailRow r in g.Rows)
            {
                if (r.HasMax)
                {
                    hasMax = true;
                    break;
                }
            }

            if (hasMax)
            {
                s.Columns(q1, qMax);
            }

            foreach (DetailRow r in g.Rows)
            {
                s.Value(r.Label, r.Value, hasMax && r.HasMax ? r.MaxValue : "");
                state.Append(r.Label).Append('=').Append(r.Value).Append('/').Append(r.MaxValue).Append('|');
            }
        }
    }

    /// <summary>
    ///     Expected outcome: the 3D view, the name, the description and the detail blocks; a note when the item data is
    ///     not available.
    /// </summary>
    internal static KyPage Slippers()
    {
        Func<string, string> text = k => Loc.T(k);
        ViewerSettings viewer = HubTabs.Viewer();
        SlipperData d = SlipperMenuData.Read();
        var page = new KyPage();
        var state = new StringBuilder("slippers|");
        KySection top = page.Section("");
        top.Hosted(() => ModelViewportView.Build(d != null ? d.Name : "", MainMenuView.SlippersModel,
            d != null ? () => ViewerScene.Make(SlipperMenuData.MakeModel, viewer) : null, viewer,
            text("stevethetroll_viewer_hint"), text("stevethetroll_viewer_reset"),
            text("stevethetroll_viewer_unavailable")));
        if (d == null)
        {
            top.Note(text("stevethetroll_details_unavailable"));
            page.State = "none";
            return page;
        }

        KySection head = page.Section(d.Name);
        head.Note(d.Description);
        state.Append(d.Name).Append('|').Append(d.Description).Append('|');
        Details(page, SlipperDetailsBuilder.Build(d, text), text("stevethetroll_details_q1"),
            text("stevethetroll_details_qmax") + " " + d.MaxQuality, state);
        page.State = state.ToString();
        return page;
    }

    /// <summary>
    ///     Expected outcome: the 3D view of the mascot, the set name and description and the detail blocks; a note when
    ///     the costume data is not available.
    /// </summary>
    internal static KyPage Costume()
    {
        Func<string, string> text = k => Loc.T(k);
        ViewerSettings viewer = HubTabs.Viewer();
        CostumeData d = CostumeMenuData.Read();
        var page = new KyPage();
        if (d == null)
        {
            // in a world but the set is not crafted yet (D-088): say that, not "load a world first"
            bool inWorld = Player.m_localPlayer != null && ObjectDB.instance != null;
            page.Section("").Note(text(inWorld ? "stevethetroll_costume_locked" : "stevethetroll_details_unavailable"));
            page.State = inWorld ? "locked" : "none";
            return page;
        }

        var state = new StringBuilder("costume|");
        page.Section("").Hosted(() => ModelViewportView.Build(d.SetName, MainMenuView.CostumeModel,
            () => ViewerScene.Make(CostumeMenuData.MakeModel, viewer, 1, true), viewer,
            text("stevethetroll_viewer_hint"), text("stevethetroll_viewer_reset"),
            text("stevethetroll_viewer_unavailable")));
        page.Section(d.SetName).Note(d.SetDescription);
        state.Append(d.SetName).Append('|').Append(d.SetDescription).Append('|');
        Details(page, CostumeDetailsBuilder.Build(d, text), text("stevethetroll_details_q1"),
            text("stevethetroll_details_qmax") + " 4", state);
        page.State = state.ToString();
        return page;
    }
}
