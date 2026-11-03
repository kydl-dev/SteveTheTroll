using System;
using BepInEx.Configuration;
using KydlCore.Common;
using KydlCore.Features.Hub.Contracts;
using SteveTheTroll.Bootstrap;
using SteveTheTroll.Configuration;
using SteveTheTroll.Presentation.Hub.Pages;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Presentation.Hub.Registration;

/// <summary>
///     What SteveTheTroll declares to the KY hub of KydlCore (F6, MIGRATION_DECISIONS B11 / A6). There is NO menu key,
///     window or input lock in this mod: KydlCore
///     owns them (D-040). Row 1 of the hub shows this mod (SetModTitle), row 2 its tabs. The Settings tab is always there
///     (the player config with the
///     switches); every other tab belongs to a feature and is added by that feature's Start and removed by its Stop (A4:
///     switching a feature off in the config
///     removes its tabs at once, no stale tab stays): Slippers (SlippersFeature), Costume (CostumeFeature), My trolls /
///     Troll work / Trolls (TrollsFeature),
///     Troll Hub (HubsFeature). WHAT a tab shows is described here and in Presentation/Hub/Pages (plain data); HOW it is
///     drawn is MainMenuView and its siblings
///     in the Unity project (SteveTheTroll-Unity/Assets/SteveTheTroll_UI/Runtime, previewed there).
/// </summary>
internal static class HubTabs
{
    internal const string SlippersId = "slippers",
        CostumeId = "costume",
        MyTrollsId = "mytrolls",
        WorkId = "work",
        TrollsId = "trolls",
        SettingsId = "settings",
        HubId = "trollhub";

    /// The mod's title and its Settings tab (Bootstrap, once). Expected outcome: the settings tab is registered.
    internal static bool RegisterModPage(ConfigFile config)
    {
        KyHub.SetModTitle(Plugin.ModGuid, Plugin.ModName);
        KyTab tab = KyHub.AddTab(Plugin.ModGuid, SettingsId, Loc.T("stevethetroll_tab_settings"),
            () => KyHub.ConfigEntries(config), 10);
        ModLog.Verbose("HubTabs: mod title + settings tab " + (tab != null ? "registered" : "FAILED"));
        return tab != null && KyHub.HasTab(Plugin.ModGuid, SettingsId);
    }

    /// Removes the Settings tab (mod unload). Expected outcome: it is gone.
    internal static bool UnregisterModPage()
    {
        KyHub.RemoveTab(Plugin.ModGuid, SettingsId);
        return !KyHub.HasTab(Plugin.ModGuid, SettingsId);
    }

    /// The Slippers tab (SlippersFeature.Start). Expected outcome: it is registered.
    internal static bool AddSlippers()
    {
        return AddPage(SlippersId, "stevethetroll_tab_slippers", ModelKyPages.Slippers, 0, 3);
    }

    /// The Costume tab (CostumeFeature.Start). Expected outcome: it is registered.
    internal static bool AddCostume()
    {
        return AddPage(CostumeId, "stevethetroll_tab_costume", ModelKyPages.Costume, 1, 3);
    }

    /// My trolls / Troll work / Trolls (TrollsFeature.Start; T-098: described, not built - KydlCore draws these). Expected outcome: all three are registered.
    internal static bool AddTrolls()
    {
        bool mine = AddPage(MyTrollsId, "stevethetroll_tab_mytrolls", TrollListKyPages.MyTrolls, 2, 2);
        bool work = AddPage(WorkId, "stevethetroll_tab_troll", TrollListKyPages.Work, 3, 2);
        bool all = AddPage(TrollsId, "stevethetroll_tab_trolls", TrollsKyPage.Build, 4, 2);
        return mine && work && all;
    }

    /// The Troll Hub tab (HubsFeature.Start). Expected outcome: it is registered.
    internal static bool AddHub()
    {
        HubTab.Tab = KyHub.AddPage(Plugin.ModGuid, HubId, Loc.T("stevethetroll_hub_tab"), HubTab.Build, 5);
        ModLog.Verbose("HubTabs: tab " + HubId + " " + (HubTab.Tab != null ? "added" : "FAILED"));
        return HubTab.Tab != null;
    }

    /// Removes the given tabs (a feature's Stop). Expected outcome: none of them is registered any more.
    internal static bool Remove(params string[] ids)
    {
        int left = 0;
        foreach (string id in ids)
        {
            KyHub.RemoveTab(Plugin.ModGuid, id);
            if (id == HubId)
            {
                HubTab.Tab = null;
            }

            if (KyHub.HasTab(Plugin.ModGuid, id))
            {
                left++;
            }
        }

        ModLog.Verbose("HubTabs: removed " + string.Join(", ", ids) +
                       (left > 0 ? " - " + left + " STILL registered" : ""));
        return left == 0;
    }

    private static bool AddPage(string id, string titleKey, Func<KyPage> build, int order, int refreshSeconds)
    {
        KyTab tab = KyHub.AddPage(Plugin.ModGuid, id, Loc.T(titleKey), build, order, refreshSeconds);
        ModLog.Verbose("HubTabs: tab " + id + " " + (tab != null ? "added" : "FAILED"));
        return tab != null;
    }

    /// The views' studio light from the Tunables (F7), read when a page is built.
    internal static ViewerSettings Viewer()
    {
        var v = new ViewerSettings
        {
            KeyLight = ModSettings.ViewerKeyLight.Value,
            FillLight = ModSettings.ViewerFillLight.Value,
            Ambient = ModSettings.ViewerAmbient.Value
        };
        return v;
    }
}
