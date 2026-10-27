using System;
using System.Collections.Generic;
using Kydl.Ui;
using UnityEngine.UIElements;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// WHAT the mod's menu shows (UI Toolkit, shared Kydl.Ui look; D-039): tab 1 Slippers = a 3D view of the slippers + their details
    /// (later the troll costume), tab 2 Troll work = what the tamed troll focus-farms. Compiled into the mod AND shown by the Unity preview
    /// (Assets/SteveTheTroll_UI/Preview/MenuPreview.cs). Keep game types OUT of this folder; the game side is src/Ui/MainMenu.cs.
    /// </summary>
    internal static class MainMenuView
    {
        internal const string SlippersModel = "slippers";

        /// `slippers` = the item's data, `makeScene` = the 3D scene (null = name only), `focus` = the stored troll focus, `focusChosen` = called
        /// with the new value; `startTab` 0 Slippers, 1 Troll work (the preview can start on 1).
        internal static VisualElement Build(Func<string, string> text, Action onClose, SlipperData slippers, Func<IModelScene> makeScene, ViewerSettings viewer,
            string focus, Action<string> focusChosen, int startTab = 0)
        {
            var w = UiKit.Window(text("stevethetroll_menu_title"), text("stevethetroll_menu_footer"), onClose, out UiScroll body);
            var tabs = new[] { SlippersTab(text, slippers, makeScene, viewer), FocusTab(text, focus, focusChosen) };
            foreach (var t in tabs) body.Content.Add(t);
            var bar = new TabBar(new[] { text("stevethetroll_tab_slippers"), text("stevethetroll_tab_troll") }, startTab, index => ShowTab(tabs, index));
            ShowTab(tabs, bar.Selected);
            w.Insert(1, bar.Root);               // between the title bar and the scrolling body: the tabs stay put while the content scrolls
            UiLog.Verbose("MainMenuView: built (" + tabs.Length + " tabs, start tab " + bar.Selected + ", slippers " + (slippers != null ? slippers.ToString() : "none")
                + ", 3D scene " + (makeScene != null ? "yes" : "no") + ", focus '" + focus + "')");
            return w;
        }

        static VisualElement SlippersTab(Func<string, string> text, SlipperData d, Func<IModelScene> makeScene, ViewerSettings viewer)
        {
            var tab = new VisualElement { name = "stevethetroll-slippers-tab" };
            tab.style.flexShrink = 0;
            string name = d != null ? d.Name : "";
            tab.Add(ModelViewportView.Build(name, SlippersModel, makeScene, viewer, text("stevethetroll_viewer_hint"), text("stevethetroll_viewer_reset"), text("stevethetroll_viewer_unavailable")));
            if (d == null) { tab.Add(UiControls.Note(text("stevethetroll_details_unavailable"))); return tab; }
            tab.Add(UiControls.Heading(d.Name));
            tab.Add(UiControls.Note(d.Description));
            tab.Add(DetailsView.Build(SlipperDetailsBuilder.Build(d, text), text("stevethetroll_details_q1"), text("stevethetroll_details_qmax") + " " + d.MaxQuality));
            return tab;
        }

        static VisualElement FocusTab(Func<string, string> text, string focus, Action<string> chosen)
        {
            var tab = new VisualElement { name = "stevethetroll-focus-tab" };
            tab.style.flexShrink = 0;
            tab.Add(UiControls.Heading(text("stevethetroll_focus_heading")));
            tab.Add(UiControls.Note(text("stevethetroll_focus_note")));
            var values = new List<string>();
            foreach (var c in TrollFocus.Choices) values.Add(c.Key);
            string current = TrollFocus.Normalize(focus);
            tab.Add(UiControls.Row(text("stevethetroll_focus_label"), UiControls.Dropdown(values, current, v => text(TrollFocus.KeyOf(v)), v =>
            {
                string n = TrollFocus.Normalize(v);
                UiLog.Verbose("MainMenuView: troll focus chosen '" + n + "' (" + text(TrollFocus.KeyOf(n)) + ")");
                if (chosen != null) chosen(n);
            })));
            return tab;
        }

        static void ShowTab(VisualElement[] tabs, int index)
        {
            for (int i = 0; i < tabs.Length; i++) tabs[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
            UiLog.Verbose("MainMenuView: tab " + index + " shown");
        }
    }
}
