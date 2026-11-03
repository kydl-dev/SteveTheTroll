using BepInEx.Configuration;
using Kydl.Common;
using Kydl.Core.Features;
using Kydl.Core.Results;
using SteveTheTroll.Features.Costume.Infrastructure;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Slippers.Infrastructure;
using SteveTheTroll.Features.Trolls.Infrastructure;
using SteveTheTroll.Presentation.Hub.Registration;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Bootstrap
{
    /// <summary>
    /// Composition root (MIGRATION_DECISIONS A4): the mod's KY hub page (title + Settings tab) and its four features in the FeatureRegistry - Slippers, Costume,
    /// Trolls, Troll Hub. Refresh runs after Install and on every config change, so a feature switched in the config starts / stops at once.
    /// </summary>
    internal sealed class ModInstaller
    {
        internal static ModInstaller Current { get; private set; }
        readonly ConfigFile _config;
        internal FeatureRegistry Features { get; }

        internal ModInstaller(ConfigFile config) { _config = config; Features = new FeatureRegistry(line => ModLog.Verbose(line)); }

        /// Expected outcome: true when the mod page is registered and every switched-on feature started.
        internal bool Install(bool slippers, bool costume, bool trolls, bool hubs)
        {
            Current = this;
            bool page = HubTabs.RegisterModPage(_config);
            int added = 0;
            foreach (IFeature f in new IFeature[] { new SlippersFeature(slippers), new CostumeFeature(costume), new TrollsFeature(trolls), new HubsFeature(hubs) })
            {
                OperationResponse<int> r = Features.Add(f);
                if (r.IsSuccess) added++; else ModLog.Error("ModInstaller: " + r.Reason);
            }
            ModLog.Check("ModInstaller: features added", added, 4, 0, "");
            return page && added == 4 && Refresh();
        }

        /// Starts / stops features whose switch changed. Expected outcome: true when every switch could be applied.
        internal bool Refresh()
        {
            OperationResponse<int> r = Features.Refresh();
            if (r.Status == ResponseStatus.Failed) ModLog.Warn("ModInstaller: " + r.Reason); else ModLog.Verbose("ModInstaller: refresh - " + r);
            return r.IsSuccess;
        }

        /// Mod unload. Expected outcome: every feature stopped and the mod page removed.
        internal bool Uninstall()
        {
            OperationResponse<int> r = Features.StopAll();
            bool gone = HubTabs.UnregisterModPage();
            if (Current == this) Current = null;
            return r.IsSuccess && gone;
        }
    }
}
