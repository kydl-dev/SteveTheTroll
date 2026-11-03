using Kydl.Core.Commands;
using Kydl.Core.Features;
using Kydl.Core.Identity;
using Kydl.Core.Pipeline;
using Kydl.Core.Results;
using Kydl.Core.Time;
using Kydl.Common;
using KydlCore;
using SteveTheTroll.Configuration;
using SteveTheTroll.Integrations.KydlCore;
using SteveTheTroll.Presentation.Hub.Registration;

namespace SteveTheTroll.Features.Slippers.Infrastructure
{
    /// <summary>
    /// The Fluffy Slippers (MIGRATION_DECISIONS A4). Gate: [General] EnableAll AND [Slippers] Enable, read live. The item itself is added to the game at start
    /// (Jotunn clones the prefab once, Plugin.Awake), so it exists only when the feature was on then; Start / Stop switch what the feature contributes live:
    /// the Slippers tab of the KY hub and the KY-Items entry "stevethetroll:slippers".
    /// </summary>
    internal sealed class SlippersFeature : IFeature
    {
        internal const string FeatureId = "slippers";
        readonly bool _installed;
        internal SlippersFeature(bool installedAtStart) { _installed = installedAtStart; }

        public string Id { get { return FeatureId; } }
        public bool IsEnabled() { return ModSettings.SlippersOn; }

        public bool Start()
        {
            if (!_installed) { ModLog.Warn("SlippersFeature: switched on, but the slippers were not added to the game at start - restart the game to get them"); return _installed; }
            bool tab = HubTabs.AddSlippers();
            bool item = KyItemsContributions.Want("slippers", SlipperItem.PrefabName, KyKind.Item);
            ModLog.Verbose("SlippersFeature: started (tab " + tab + ", KY-Items " + item + ")");
            return tab && item;
        }

        public bool Stop()
        {
            bool tab = HubTabs.Remove(HubTabs.SlippersId);
            bool item = KyItemsContributions.Drop("slippers");
            ModLog.Verbose("SlippersFeature: stopped (tab removed " + tab + ", KY-Items dropped " + item + "; the item stays in the game until restart)");
            return tab && item;
        }
    }
}
