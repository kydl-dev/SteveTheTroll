using System;
using BepInEx.Configuration;
using KydlCore.Common;
using KydlCore.Features.Hub.Contracts;
using SteveTheTroll.Bootstrap;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
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

    // User 2026-11-04 (tab gating): Costume appears once the costume set has been active for the first time; Troll Hub once the first hub is
    // built; My trolls / Troll work / Trolls once the first troll is tamed. The marks are kept on the character (Player.m_customData), so
    // they survive a relog. A feature's Start only says the tab is WANTED; Tick() adds it as soon as its gate is open.
    internal const string HubBuiltKey = "stt_hub_built";
    private static bool _wantCostume, _wantTrolls, _wantHub;
    private static float _nextTick;

    internal static bool CostumeGateOpen(Player p)
    {
        return HubPlacement.Unlocked(p);        // the set bonus has been active once (HubPlacement.Tick marks it)
    }

    internal static bool TrollsGateOpen(Player p)
    {
        return p != null && p.m_customData != null && p.m_customData.ContainsKey(TrollTamingPatch.FirstTameKey);
    }

    internal static bool HubGateOpen(Player p)
    {
        return p != null && p.m_customData != null && p.m_customData.ContainsKey(HubBuiltKey);
    }

    /// Plugin.Update (every 2 s): marks the first hub built, then adds the wanted tabs whose gate is open. Expected outcome: true when a tab was added now.
    internal static bool Tick()
    {
        if (UnityEngine.Time.time < _nextTick)
        {
            return false;
        }

        _nextTick = UnityEngine.Time.time + 2f;
        Player p = Player.m_localPlayer;
        if (p == null || p.m_customData == null)
        {
            return false;
        }

        if (!HubGateOpen(p) && ModSettings.HubsOn && HubRegistry.Own(p.GetPlayerID()).Count > 0)
        {
            p.m_customData[HubBuiltKey] = "1";
            ModLog.Info("HubTabs: the first hub is built - the Troll Hub tab opens");
        }

        bool added = false;
        if (_wantCostume && CostumeGateOpen(p) && !KyHub.HasTab(Plugin.ModGuid, CostumeId)) { added |= AddPage(CostumeId, "stevethetroll_tab_costume", ModelKyPages.Costume, 1, 3); }
        if (_wantTrolls && TrollsGateOpen(p) && !KyHub.HasTab(Plugin.ModGuid, TrollsId)) { added |= AddTrollPages(); }
        if (_wantHub && HubGateOpen(p) && !KyHub.HasTab(Plugin.ModGuid, HubId)) { added |= AddHubPage(); }
        return added;
    }

    /// D-181 - TEST RESET ONLY (stt.reset; fix13: two tests that built a hub opened the Troll Hub gate for the rest of the run, and kydl-spec-port / feature-toggle then saw
    /// 3 tabs instead of a fresh character's 2): the three first-time gates are closed on the character and the gated tabs are taken out of the hub. The features still WANT
    /// their tabs, so Tick adds each again as soon as its gate opens. Expected outcome: how many gated tabs were removed.
    internal static int ResetGatesForTests(Player p)
    {
        if (p != null && p.m_customData != null)
        {
            p.m_customData.Remove(HubBuiltKey);
            p.m_customData.Remove(HubPlacement.UnlockKey);
            p.m_customData.Remove(TrollTamingPatch.FirstTameKey);
        }

        int n = 0;
        foreach (string id in new[] { CostumeId, MyTrollsId, WorkId, TrollsId, HubId })
        {
            if (KyHub.HasTab(Plugin.ModGuid, id))
            {
                KyHub.RemoveTab(Plugin.ModGuid, id);
                n++;
            }
        }

        HubTab.Tab = null;
        return n;
    }

    /// The Slippers tab (SlippersFeature.Start). Expected outcome: it is registered.
    internal static bool AddSlippers()
    {
        return AddPage(SlippersId, "stevethetroll_tab_slippers", ModelKyPages.Slippers, 0, 3);
    }

    /// The Costume tab (CostumeFeature.Start): registered now when the costume set has been active once, else when it first is (Tick). Expected outcome: true (wanted).
    internal static bool AddCostume()
    {
        _wantCostume = true;
        Player p = Player.m_localPlayer;
        return !CostumeGateOpen(p) || AddPage(CostumeId, "stevethetroll_tab_costume", ModelKyPages.Costume, 1, 3);
    }

    /// My trolls / Troll work / Trolls (TrollsFeature.Start; T-098: described, not built - KydlCore draws these): registered once a troll has been tamed. Expected outcome: true (wanted).
    internal static bool AddTrolls()
    {
        _wantTrolls = true;
        Player p = Player.m_localPlayer;
        return !TrollsGateOpen(p) || AddTrollPages();
    }

    private static bool AddTrollPages()
    {
        bool mine = AddPage(MyTrollsId, "stevethetroll_tab_mytrolls", TrollListKyPages.MyTrolls, 2, 2);
        bool work = AddPage(WorkId, "stevethetroll_tab_troll", TrollListKyPages.Work, 3, 2);
        bool all = AddPage(TrollsId, "stevethetroll_tab_trolls", TrollsKyPage.Build, 4, 2);
        return mine && work && all;
    }

    /// The Troll Hub tab (HubsFeature.Start): registered once the first hub is built. Expected outcome: true (wanted).
    internal static bool AddHub()
    {
        _wantHub = true;
        Player p = Player.m_localPlayer;
        return !HubGateOpen(p) || AddHubPage();
    }

    private static bool AddHubPage()
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
            if (id == CostumeId) { _wantCostume = false; }
            if (id == TrollsId) { _wantTrolls = false; }
            if (id == HubId) { _wantHub = false; }
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
