using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Configuration;
using SteveTheTroll.Presentation.Hub.Registration;

namespace SteveTheTroll.Features.Trolls.Infrastructure;

/// <summary>
///     Taming and farming of real trolls with the slippers, their backpack and depots, orders, map pins and extra trolls
///     near the bed (MIGRATION_DECISIONS A4).
///     Gate: [General] EnableAll AND [Trolls] Enable, read live. The troll prefab changes (tameable, depot frame, level
///     op) are made at start (Plugin.Awake);
///     Start / Stop switch the troll ticks (<see cref="Running" />, read by Plugin.Update) and the My trolls / Troll work
///     / Trolls tabs live.
/// </summary>
internal sealed class TrollsFeature : IFeature
{
    internal const string FeatureId = "trolls";

    private readonly bool _installed;

    internal TrollsFeature(bool installedAtStart)
    {
        _installed = installedAtStart;
    }

    /// The per-frame troll work runs (Plugin.Update). False while the feature is off.
    internal static bool Running { get; private set; }

    public string Id => FeatureId;

    public bool IsEnabled()
    {
        return ModSettings.TrollsOn;
    }

    public bool Start()
    {
        if (!_installed)
        {
            ModLog.Warn(
                "TrollsFeature: switched on, but the trolls were not set up at start - restart the game to get them");
            return _installed;
        }

        bool tabs = HubTabs.AddTrolls();
        Running = true;
        ModLog.Verbose("TrollsFeature: started (tabs " + tabs + ", ticks on)");
        return tabs && Running;
    }

    public bool Stop()
    {
        Running = false;
        bool tabs = HubTabs.Remove(HubTabs.MyTrollsId, HubTabs.WorkId, HubTabs.TrollsId);
        ModLog.Verbose("TrollsFeature: stopped (tabs removed " + tabs + ", ticks off)");
        return tabs && !Running;
    }
}
