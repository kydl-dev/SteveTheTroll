using System;
using System.Collections.Generic;
using System.Globalization;
using Kydl.Ui;
using UnityEngine.UIElements;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// D-119 (user 2026-10-28: "another tab ... list of trolls, next to each a button for follow / kill and show distance from player"; answers:
    /// Kill asks first, Follow brings a far troll to you, the button always says Follow, tab "My trolls" before the other troll tabs):
    /// one row per tamed troll - name | distance | Follow | Kill. Kill turns the row's buttons into "Kill Steve 3? Yes / No". Distances follow
    /// the live values every second. Built in the Unity project (MenuPreview shows it) and compiled into the mod.
    /// </summary>
    internal static class MyTrollsTab
    {
        const float NameWidth = 170f, DistanceWidth = 90f, ButtonWidth = 90f;

        internal static VisualElement Build(Func<string, string> text, IList<TrollInfo> trolls, TrollActions actions)
        {
            var tab = new VisualElement { name = "stevethetroll-mytrolls-tab" };
            tab.style.flexShrink = 0;
            tab.Add(UiControls.Heading(text("stevethetroll_mytrolls_heading")));
            tab.Add(UiControls.Note(text("stevethetroll_mytrolls_note")));
            if (trolls == null || trolls.Count == 0) { tab.Add(UiControls.Note(text("stevethetroll_troll_none"))); return tab; }
            tab.Add(Row(Cell(text("stevethetroll_troll_name"), NameWidth, true), Cell(text("stevethetroll_troll_distance"), DistanceWidth, true)));
            int rows = 0;
            foreach (var t in trolls) { tab.Add(TrollRow(text, t, actions)); rows++; }
            UiLog.Verbose("MyTrollsTab: built, " + rows + " troll row(s), actions " + (actions != null ? "follow " + (actions.Follow != null) + ", kill " + (actions.Kill != null) + ", distance " + (actions.Distance != null) : "none"));
            return tab;
        }

        static VisualElement TrollRow(Func<string, string> text, TrollInfo t, TrollActions actions)
        {
            var troll = t;
            var distance = Cell(DistanceText(Distance(actions, troll)), DistanceWidth, false);
            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row; buttons.style.alignItems = Align.Center; buttons.style.flexGrow = 1;
            var row = Row(Cell(troll.Shown(text), NameWidth, false), distance, buttons);
            Action normal = null;
            normal = () =>
            {
                buttons.Clear();
                if (troll.DiedTicks > 0) { buttons.Add(Cell(text("stevethetroll_mytrolls_dead"), -1f, false)); return; }
                buttons.Add(Fixed(UiControls.Button(text("stevethetroll_follow"), () =>
                {
                    UiLog.Verbose("MyTrollsTab: Follow pressed for " + troll.Shown(null) + " [" + troll.Id + "], " + (troll.Loaded ? "loaded" : "far"));
                    if (actions != null && actions.Follow != null) actions.Follow(troll.Id);
                })));
                buttons.Add(Fixed(UiControls.Button(text("stevethetroll_kill"), () =>
                {
                    UiLog.Verbose("MyTrollsTab: Kill pressed for " + troll.Shown(null) + " [" + troll.Id + "] - asking");
                    buttons.Clear();
                    buttons.Add(Cell(string.Format(text("stevethetroll_kill_confirm"), troll.Shown(text)), -1f, false));
                    buttons.Add(Fixed(UiControls.Button(text("stevethetroll_yes"), () =>
                    {
                        UiLog.Verbose("MyTrollsTab: Kill confirmed for " + troll.Shown(null) + " [" + troll.Id + "]");
                        if (actions != null && actions.Kill != null) actions.Kill(troll.Id);
                        normal();
                    }, true)));
                    buttons.Add(Fixed(UiControls.Button(text("stevethetroll_no"), () => { UiLog.Verbose("MyTrollsTab: Kill cancelled for " + troll.Shown(null)); normal(); })));
                })));
            };
            normal();
            bool dead = troll.DiedTicks > 0;
            distance.schedule.Execute(() =>
            {
                string now = DistanceText(Distance(actions, troll));
                if (distance.text != now) distance.text = now;
                if (dead != troll.DiedTicks > 0) { dead = troll.DiedTicks > 0; UiLog.Verbose("MyTrollsTab: " + troll.Shown(null) + " died - buttons off"); normal(); }
            }).Every(1000);
            return row;
        }

        static float Distance(TrollActions actions, TrollInfo t)
        {
            return actions != null && actions.Distance != null ? actions.Distance(t.Id) : -1f;
        }

        /// Pure: "-" when unknown (negative / NaN), else whole metres ("0 m", "153 m"); 1000 m and more in km with one decimal. Expected outcome: the text.
        internal static string DistanceText(float metres)
        {
            if (float.IsNaN(metres) || metres < 0f) return "-";
            if (metres >= 1000f) return (metres / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " km";
            return Math.Round(metres).ToString("0", CultureInfo.InvariantCulture) + " m";
        }

        static Button Fixed(Button b) { b.style.width = ButtonWidth; b.style.flexShrink = 0; b.style.marginRight = 4; return b; }

        static VisualElement Row(params VisualElement[] cells)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0;
            r.style.marginTop = 3; r.style.marginBottom = 3;
            foreach (var c in cells) r.Add(c);
            return r;
        }

        static Label Cell(string content, float width, bool header)
        {
            var l = UiKit.Label(content, header ? UiKit.SmallSize : UiKit.FontSize, header ? UiKit.TextDim : UiKit.Text);
            l.style.whiteSpace = WhiteSpace.Normal;
            if (width >= 0f) { l.style.width = width; l.style.flexShrink = 0; }
            else { l.style.flexGrow = 1; l.style.flexShrink = 1; }
            return l;
        }
    }
}
