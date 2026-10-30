using System;
using BepInEx.Configuration;
using Kydl.Common;
using Kydl.Ui;
using UnityEngine.UIElements;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// The mod's menu = tabs in the KY hub of KydlCore (F6). There is NO menu key, window or input lock in this mod: KydlCore owns them (D-040).
    /// Row 1 of the hub shows this mod (named by SetModTitle), row 2 the tabs registered here: Slippers when [Slippers] Enable is on, Costume when
    /// [Costume] Enable is on, My trolls / Troll work / Trolls when [Trolls] Enable is on (until the player has a living tamed troll they show a
    /// note), and Settings (the player config with the switches). What a tab shows is built by MainMenuView and its siblings in the Unity project
    /// (SteveTheTroll-Unity/Assets/SteveTheTroll_UI/Runtime, previewed there); the game side only hands them data. A mod that registers any tab
    /// loses the automatic "Settings" tab, so the settings tab is registered on purpose.
    /// </summary>
    internal static class HubTabs
    {
        internal const string SlippersId = "slippers", CostumeId = "costume", MyTrollsId = "mytrolls", WorkId = "work", TrollsId = "trolls", SettingsId = "settings";

        /// Called once from Plugin.Awake (after the config is bound and the text is registered). Expected outcome: the settings tab is registered, and every tab of a switched-on part.
        internal static void Register(ConfigFile config)
        {
            KydlCore.KyHub.SetModTitle(Plugin.ModGuid, Plugin.ModName);
            int n = 0;
            if (ModSettings.SlippersOn) { Add(SlippersId, "stevethetroll_tab_slippers", SlippersPage, 0); n++; }
            if (ModSettings.CostumeOn) { Add(CostumeId, "stevethetroll_tab_costume", CostumePage, 1); n++; }
            if (ModSettings.TrollsOn)
            {
                Add(MyTrollsId, "stevethetroll_tab_mytrolls", MyTrollsPage, 2);
                Add(WorkId, "stevethetroll_tab_troll", WorkPage, 3);
                Add(TrollsId, "stevethetroll_tab_trolls", TrollsPage, 4);
                n += 3;
            }
            KydlCore.KyHub.AddTab(Plugin.ModGuid, SettingsId, Loc.T("stevethetroll_tab_settings"), () => KydlCore.KyHub.ConfigEntries(config), 10);
            ModLog.Verbose("HubTabs: registered " + n + " page tab(s) + settings (slippers " + ModSettings.SlippersOn + ", costume " + ModSettings.CostumeOn + ", trolls " + ModSettings.TrollsOn + ")");
        }

        static void Add(string id, string titleKey, Func<VisualElement> page, int order)
        {
            var tab = KydlCore.KyHub.AddTab(Plugin.ModGuid, id, Loc.T(titleKey), page, order);
            ModLog.Verbose("HubTabs: tab '" + id + "' " + (tab != null ? "registered" : "NOT registered"));
        }

        static VisualElement SlippersPage()
        {
            var viewer = Viewer();
            var data = SlipperMenuData.Read();
            ModLog.Verbose("HubTabs: slippers page built (data " + (data != null) + ")");
            return MainMenuView.SlippersTab(k => Loc.T(k), data, data != null ? () => ViewerScene.Make(SlipperMenuData.MakeModel, viewer) : (Func<IModelScene>)null, viewer);
        }

        static VisualElement CostumePage()
        {
            var viewer = Viewer();
            var costume = CostumeMenuData.Read();
            ModLog.Verbose("HubTabs: costume page built (data " + (costume != null) + ")");
            if (costume == null) return UiControls.Note(Loc.T("stevethetroll_details_unavailable"));
            return MainMenuView.CostumeTab(k => Loc.T(k), costume, () => ViewerScene.Make(CostumeMenuData.MakeModel, viewer, 1, true), viewer);
        }

        static VisualElement MyTrollsPage()
        {
            var trolls = Taming.TrollRegistry.Trolls();
            ModLog.Verbose("HubTabs: my trolls page built (" + trolls.Count + " troll(s))");
            return AnyLiving(trolls) ? MyTrollsTab.Build(k => Loc.T(k), trolls, TrollCommands.Actions()) : NoTroll();
        }

        static VisualElement WorkPage()
        {
            var trolls = Taming.TrollRegistry.Trolls();
            ModLog.Verbose("HubTabs: troll work page built (" + trolls.Count + " troll(s))");
            return AnyLiving(trolls) ? MainMenuView.WorkTab(k => Loc.T(k), trolls, (id, v) => SlipperMenuData.FocusChosen(id, v), id => SlipperMenuData.GoFarm(id)) : NoTroll();
        }

        static VisualElement TrollsPage()
        {
            var trolls = Taming.TrollRegistry.Trolls();
            ModLog.Verbose("HubTabs: trolls page built (" + trolls.Count + " troll(s))");
            return AnyLiving(trolls) ? MainMenuView.TrollsTab(k => Loc.T(k), trolls, DateTime.UtcNow.Ticks) : NoTroll();
        }

        // D-046: the troll pages are only worth showing while the player has at least one living tamed troll (near or far)
        static bool AnyLiving(System.Collections.Generic.IList<TrollInfo> trolls)
        {
            if (trolls != null) foreach (var t in trolls) if (t.DiedTicks <= 0) return true;
            return false;
        }

        static VisualElement NoTroll() { return UiControls.Note(Loc.T("stevethetroll_troll_none")); }

        /// The views' studio light from the Tunables (F7), read when a page is built.
        static ViewerSettings Viewer()
        {
            var v = new ViewerSettings
            {
                KeyLight = ModSettings.ViewerKeyLight.Value,
                FillLight = ModSettings.ViewerFillLight.Value,
                Ambient = ModSettings.ViewerAmbient.Value,
            };
            return v;
        }
    }
}
