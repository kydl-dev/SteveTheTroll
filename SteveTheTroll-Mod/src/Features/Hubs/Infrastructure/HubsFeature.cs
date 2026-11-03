using Kydl.Core.Commands;
using Kydl.Core.Features;
using Kydl.Core.Identity;
using Kydl.Core.Pipeline;
using Kydl.Core.Results;
using Kydl.Core.Time;
using Kydl.Common;
using KydlCore;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Integrations.KydlCore;
using SteveTheTroll.Presentation.Hub.Registration;

namespace SteveTheTroll.Features.Hubs.Infrastructure
{
    /// <summary>
    /// The Troll Hub building (D-135, MIGRATION_DECISIONS A4). Gate: [General] EnableAll AND [Trolls] Enable AND [TrollHub] Enable, read live (ModSettings.HubsOn,
    /// which the hub code reads too). The piece is added to the game at start (Plugin.Awake); Start / Stop switch the Troll Hub tab and the KY-Items entry
    /// "stevethetroll:hub" live.
    /// </summary>
    internal sealed class HubsFeature : IFeature
    {
        internal const string FeatureId = "hubs";
        readonly bool _installed;
        internal HubsFeature(bool installedAtStart) { _installed = installedAtStart; }

        public string Id { get { return FeatureId; } }
        public bool IsEnabled() { return ModSettings.HubsOn; }

        public bool Start()
        {
            if (!_installed) { ModLog.Warn("HubsFeature: switched on, but the Troll Hub was not added to the game at start - restart the game to get it"); return _installed; }
            bool tab = HubTabs.AddHub();
            bool item = KyItemsContributions.Want("hub", HubData.PrefabName, KyKind.Piece);
            ModLog.Verbose("HubsFeature: started (tab " + tab + ", KY-Items " + item + ")");
            return tab && item;
        }

        public bool Stop()
        {
            bool tab = HubTabs.Remove(HubTabs.HubId);
            bool item = KyItemsContributions.Drop("hub");
            ModLog.Verbose("HubsFeature: stopped (tab removed " + tab + ", KY-Items dropped " + item + ")");
            return tab && item;
        }
    }
}
