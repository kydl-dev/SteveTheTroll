using System;
using System.Collections.Generic;
using KydlCore.Ui;
using UnityEngine.UIElements;

namespace SteveTheTroll.Ui;

/// <summary>
///     WHAT the mod's menu shows (UI Toolkit, shared KydlCore.Ui look; D-039 / D-043): tab 1 Slippers = a 3D view of the
///     slippers + their details,
///     Costume (D-080) = the mascot set, its pieces and the club, My trolls (D-119) = follow / kill / distance per troll,
///     Troll work = every tamed troll, what it does and its focus, tab 3 Trolls = one troll's stats and position. Compiled
///     into the mod AND shown by the Unity preview
///     (Assets/SteveTheTroll_UI/Preview/MenuPreview.cs). Keep game types OUT of this folder; the game side is
///     src/Ui/MainMenu.cs.
/// </summary>
internal static class MainMenuView
{
    internal const string SlippersModel = "slippers";
    internal const string CostumeModel = "costume";

    private const float
        NameWidth = 140f, StatusWidth = 150f, FocusWidth = 160f, GoWidth = 110f; // px at the 1080p reference

    /// `slippers` = the item's data, `makeScene` = the 3D scene (null = name only), `trolls` = the player's tamed trolls (D-043, live or last
    /// seen), `focusChosen(id, value)` = a troll's new focus; `startTab` 0 Slippers, 1 Troll work, 2 Trolls (the preview can start on any).
    internal static VisualElement Build(Func<string, string> text, Action onClose, SlipperData slippers,
        Func<IModelScene> makeScene, ViewerSettings viewer,
        IList<TrollInfo> trolls, Action<string, string> focusChosen, Action<string> goFarm, int startTab = 0,
        long nowTicks = 0,
        CostumeData costume = null, Func<IModelScene> makeCostumeScene = null, TrollActions actions = null)
    {
        VisualElement w = UiKit.Window(text("stevethetroll_menu_title"), text("stevethetroll_menu_footer"), onClose,
            out UiScroll body);
        // D-046: the two troll tabs only exist while the player has at least one living tamed troll (near or far)
        bool anyTroll = false;
        if (trolls != null)
        {
            foreach (TrollInfo t in trolls)
            {
                if (t.DiedTicks <= 0)
                {
                    anyTroll = true;
                }
            }
        }

        // D-080: tab 2 Costume (the set, its pieces and the club) whenever the costume items exist
        var tabList = new List<VisualElement> { SlippersTab(text, slippers, makeScene, viewer) };
        var nameList = new List<string> { text("stevethetroll_tab_slippers") };
        if (costume != null)
        {
            tabList.Add(CostumeTab(text, costume, makeCostumeScene, viewer));
            nameList.Add(text("stevethetroll_tab_costume"));
        }

        if (anyTroll)
        {
            tabList.Add(MyTrollsTab.Build(text, trolls, actions));
            nameList.Add(text("stevethetroll_tab_mytrolls")); // D-119: first troll tab
            tabList.Add(WorkTab(text, trolls, focusChosen, goFarm));
            nameList.Add(text("stevethetroll_tab_troll"));
            tabList.Add(TrollsTab(text, trolls, nowTicks));
            nameList.Add(text("stevethetroll_tab_trolls"));
        }

        VisualElement[] tabs = tabList.ToArray();
        foreach (VisualElement t in tabs)
        {
            body.Content.Add(t);
        }

        string[] names = nameList.ToArray();
        var bar = new TabBar(names, startTab, index => ShowTab(tabs, index));
        ShowTab(tabs, bar.Selected);
        w.Insert(1, bar.Root); // between the title bar and the scrolling body: the tabs stay put while the content scrolls
        UiLog.Verbose("MainMenuView: built (" + tabs.Length + " tabs, start tab " + bar.Selected + ", slippers " +
                      (slippers != null ? slippers.ToString() : "none")
                      + ", 3D scene " + (makeScene != null ? "yes" : "no") + ", costume " +
                      (costume != null ? costume.ToString() : "none") + ", trolls " +
                      (trolls != null ? trolls.Count : 0) + ", troll tabs " +
                      (anyTroll ? "shown" : "hidden (no living tamed troll)") + ")");
        return w;
    }

    internal static VisualElement SlippersTab(Func<string, string> text, SlipperData d, Func<IModelScene> makeScene,
        ViewerSettings viewer)
    {
        var tab = new VisualElement { name = "stevethetroll-slippers-tab" };
        tab.style.flexShrink = 0;
        string name = d != null ? d.Name : "";
        tab.Add(ModelViewportView.Build(name, SlippersModel, makeScene, viewer, text("stevethetroll_viewer_hint"),
            text("stevethetroll_viewer_reset"), text("stevethetroll_viewer_unavailable")));
        if (d == null)
        {
            tab.Add(UiControls.Note(text("stevethetroll_details_unavailable")));
            return tab;
        }

        tab.Add(UiControls.Heading(d.Name));
        tab.Add(UiControls.Note(d.Description));
        tab.Add(DetailsView.Build(SlipperDetailsBuilder.Build(d, text), text("stevethetroll_details_q1"),
            text("stevethetroll_details_qmax") + " " + d.MaxQuality));
        return tab;
    }

    // D-080 Costume tab: a 3D view of the whole mascot + the set / pieces / club details.
    internal static VisualElement CostumeTab(Func<string, string> text, CostumeData d, Func<IModelScene> makeScene,
        ViewerSettings viewer)
    {
        var tab = new VisualElement { name = "stevethetroll-costume-tab" };
        tab.style.flexShrink = 0;
        tab.Add(ModelViewportView.Build(d.SetName, CostumeModel, makeScene, viewer, text("stevethetroll_viewer_hint"),
            text("stevethetroll_viewer_reset"), text("stevethetroll_viewer_unavailable")));
        tab.Add(UiControls.Heading(d.SetName));
        tab.Add(UiControls.Note(d.SetDescription));
        tab.Add(DetailsView.Build(CostumeDetailsBuilder.Build(d, text), text("stevethetroll_details_q1"),
            text("stevethetroll_details_qmax") + " 4"));
        return tab;
    }

    // Tab 2 (D-043): one row per tamed troll - name | what it does | focus drop-down | what it farms now.
    internal static VisualElement WorkTab(Func<string, string> text, IList<TrollInfo> trolls,
        Action<string, string> chosen, Action<string> goFarm)
    {
        var tab = new VisualElement { name = "stevethetroll-work-tab" };
        tab.style.flexShrink = 0;
        tab.Add(UiControls.Heading(text("stevethetroll_focus_heading")));
        tab.Add(UiControls.Note(text("stevethetroll_focus_note")));
        if (trolls == null || trolls.Count == 0)
        {
            tab.Add(UiControls.Note(text("stevethetroll_troll_none")));
            return tab;
        }

        tab.Add(Row(Cell(text("stevethetroll_troll_name"), NameWidth, true),
            Cell(text("stevethetroll_troll_doing"), StatusWidth, true),
            Cell(text("stevethetroll_focus_label"), FocusWidth, true), Cell("", GoWidth, true),
            Cell(text("stevethetroll_troll_farming"), -1f, true)));
        foreach (TrollInfo t in trolls)
        {
            TrollInfo troll = t;
            string doing = text(t.Status) + (t.Loaded ? "" : " *");
            // D-126: the choices + the items of the biome the troll is in
            Button drop = UiControls.Dropdown(TrollFocus.ChoicesFor(t.Biome, t.Focus), TrollFocus.Normalize(t.Focus),
                v => text(TrollFocus.KeyOf(v)), v =>
                {
                    string n = TrollFocus.Normalize(v);
                    UiLog.Verbose("MainMenuView: focus of " + troll.Shown(null) + " [" + troll.Id + "] -> '" + n + "'");
                    troll.Focus = n;
                    if (chosen != null)
                    {
                        chosen(troll.Id, n);
                    }
                });
            drop.style.width = FocusWidth;
            drop.style.flexShrink = 0;
            Button go = UiControls.Button(text("stevethetroll_gofarm"), () =>
            {
                UiLog.Verbose("MainMenuView: Go farm pressed for " + troll.Shown(null) + " [" + troll.Id +
                              "], focus '" + troll.Focus + "'");
                if (goFarm != null)
                {
                    goFarm(troll.Id);
                }
            });
            go.style.width = GoWidth;
            go.style.flexShrink = 0;
            Label doingCell = Cell(doing, StatusWidth, false);
            Label farmCell = Cell(TrollFocus.Shown(t.Farming, text), -1f, false);
            tab.Add(Row(Cell(t.Shown(text), NameWidth, false), doingCell, drop, go,
                farmCell)); // D-101: backpack fill only on the Trolls page
            // TEST-019: "Go farm" looked like it only worked after closing the menu - the row now follows the live values every second
            doingCell.schedule.Execute(() =>
            {
                string now = text(troll.Status) + (troll.Loaded ? "" : " *");
                string farm = TrollFocus.Shown(troll.Farming, text);
                if (doingCell.text != now)
                {
                    UiLog.Verbose("MainMenuView: " + troll.Shown(null) + " now '" + now + "'");
                    doingCell.text = now;
                }

                if (farmCell.text != farm)
                {
                    farmCell.text = farm;
                }
            }).Every(1000);
        }

        tab.Add(UiControls.Note(text("stevethetroll_troll_lastseen_note")));
        return tab;
    }

    // Tab 3 (D-043): pick a troll, see its stats, stars, position and distance from your bed.
    internal static VisualElement TrollsTab(Func<string, string> text, IList<TrollInfo> trolls, long nowTicks)
    {
        var tab = new VisualElement { name = "stevethetroll-trolls-tab" };
        tab.style.flexShrink = 0;
        if (trolls == null || trolls.Count == 0)
        {
            tab.Add(UiControls.Note(text("stevethetroll_troll_none")));
            return tab;
        }

        var ids = new List<string>();
        foreach (TrollInfo t in trolls)
        {
            ids.Add(t.Id);
        }

        var details = new VisualElement { name = "stevethetroll-troll-details" };
        details.style.flexShrink = 0;
        string current = ids[0], shownState = null;
        long opened = DateTime.UtcNow.Ticks;
        Action<string, bool> build = (id, quiet) =>
        {
            TrollInfo pick = null;
            foreach (TrollInfo t in trolls)
            {
                if (t.Id == id)
                {
                    pick = t;
                }
            }

            // D-096: the page follows the live values (backpack, health, doing ...) - rebuilt only when something shown changed
            string state = pick != null ? pick.Encode() + pick.Loaded : "";
            if (quiet && state == shownState)
            {
                return;
            }

            shownState = state;
            current = id;
            details.Clear();
            details.Add(DetailsView.Build(
                TrollDetailsBuilder.Build(pick, text, nowTicks + (DateTime.UtcNow.Ticks - opened)), "", ""));
            if (!quiet)
            {
                UiLog.Verbose("MainMenuView: troll details " + (pick != null ? pick.ToString() : "none for " + id));
            }
        };
        Action<string> show = id => build(id, false);
        details.schedule.Execute(() => build(current, true)).Every(2000);
        tab.Add(UiControls.Row(text("stevethetroll_troll_pick"), UiControls.Dropdown(ids, ids[0], id =>
        {
            foreach (TrollInfo t in trolls)
            {
                if (t.Id == id)
                {
                    return t.Shown(text);
                }
            }

            return id;
        }, show)));
        tab.Add(details);
        show(ids[0]);
        return tab;
    }

    private static VisualElement Row(params VisualElement[] cells)
    {
        var r = new VisualElement();
        r.style.flexDirection = FlexDirection.Row;
        r.style.alignItems = Align.Center;
        r.style.flexShrink = 0;
        r.style.marginTop = 3;
        r.style.marginBottom = 3;
        foreach (VisualElement c in cells)
        {
            r.Add(c);
        }

        return r;
    }

    private static Label Cell(string content, float width, bool header)
    {
        Label l = UiKit.Label(content, header ? UiKit.SmallSize : UiKit.FontSize, header ? UiKit.TextDim : UiKit.Text);
        l.style.whiteSpace = WhiteSpace.Normal;
        if (width >= 0f)
        {
            l.style.width = width;
            l.style.flexShrink = 0;
        }
        else
        {
            l.style.flexGrow = 1;
            l.style.flexShrink = 1;
        }

        return l;
    }

    private static void ShowTab(VisualElement[] tabs, int index)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
        }

        UiLog.Verbose("MainMenuView: tab " + index + " shown");
    }
}
