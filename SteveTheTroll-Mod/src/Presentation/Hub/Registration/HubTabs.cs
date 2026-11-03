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
        internal const string SlippersId = "slippers", CostumeId = "costume", MyTrollsId = "mytrolls", WorkId = "work", TrollsId = "trolls", SettingsId = "settings", HubId = "trollhub";

        /// Called once from Plugin.Awake (after the config is bound and the text is registered). Expected outcome: the settings tab is registered, and every tab of a switched-on part.
        internal static void Register(ConfigFile config)
        {
            KydlCore.KyHub.SetModTitle(Plugin.ModGuid, Plugin.ModName);
            int n = 0;
            if (ModSettings.SlippersOn) { KydlCore.KyHub.AddPage(Plugin.ModGuid, SlippersId, Loc.T("stevethetroll_tab_slippers"), ModelKyPages.Slippers, 0, 3); n++; }
            if (ModSettings.CostumeOn) { KydlCore.KyHub.AddPage(Plugin.ModGuid, CostumeId, Loc.T("stevethetroll_tab_costume"), ModelKyPages.Costume, 1, 3); n++; }
            if (ModSettings.TrollsOn)
            {
                // T-098: described, not built - KydlCore draws these (this mod sets no font on them)
                KydlCore.KyHub.AddPage(Plugin.ModGuid, MyTrollsId, Loc.T("stevethetroll_tab_mytrolls"), TrollListKyPages.MyTrolls, 2);
                KydlCore.KyHub.AddPage(Plugin.ModGuid, WorkId, Loc.T("stevethetroll_tab_troll"), TrollListKyPages.Work, 3);
                KydlCore.KyHub.AddPage(Plugin.ModGuid, TrollsId, Loc.T("stevethetroll_tab_trolls"), TrollsKyPage.Build, 4);
                n += 3;
                if (ModSettings.HubsOn) { HubTab.Tab = KydlCore.KyHub.AddPage(Plugin.ModGuid, HubId, Loc.T("stevethetroll_hub_tab"), HubTab.Build, 5); n++; }
            }
            KydlCore.KyHub.AddTab(Plugin.ModGuid, SettingsId, Loc.T("stevethetroll_tab_settings"), () => KydlCore.KyHub.ConfigEntries(config), 10);
            ModLog.Verbose("HubTabs: registered " + n + " page tab(s) + settings (slippers " + ModSettings.SlippersOn + ", costume " + ModSettings.CostumeOn + ", trolls " + ModSettings.TrollsOn + ")");
        }

        /// The views' studio light from the Tunables (F7), read when a page is built.
        internal static ViewerSettings Viewer()
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
