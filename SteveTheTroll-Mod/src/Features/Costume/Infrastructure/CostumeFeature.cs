using KydlCore.Common;
using KydlCore.Core;
using KydlCore.Features.Items.Contracts;
using SteveTheTroll.Configuration;
using SteveTheTroll.Integrations.KydlCore;
using SteveTheTroll.Presentation.Hub.Registration;

namespace SteveTheTroll.Features.Costume.Infrastructure;

/// <summary>
///     The troll costume (head, suit, trousers), the Troll Club and the set effects (MIGRATION_DECISIONS A4). Gate:
///     [General] EnableAll AND [Costume] Enable,
///     read live. The items are added to the game at start (Plugin.Awake); Start / Stop switch the Costume tab and the
///     four KY-Items entries live.
/// </summary>
internal sealed class CostumeFeature : IFeature
{
    internal const string FeatureId = "costume";

    private static readonly string[][] Items =
    {
        new[] { "costume-helmet", CostumeItems.HelmetName }, new[] { "costume-chest", CostumeItems.ChestName },
        new[] { "costume-legs", CostumeItems.LegsName }, new[] { "club", CostumeItems.ClubName }
    };

    private readonly bool _installed;

    internal CostumeFeature(bool installedAtStart)
    {
        _installed = installedAtStart;
    }

    public string Id => FeatureId;

    public bool IsEnabled()
    {
        return ModSettings.CostumeOn;
    }

    public bool Start()
    {
        if (!_installed)
        {
            ModLog.Warn(
                "CostumeFeature: switched on, but the costume was not added to the game at start - restart the game to get it");
            return _installed;
        }

        bool tab = HubTabs.AddCostume();
        int wanted = 0;
        foreach (string[] i in Items)
        {
            if (KyItemsContributions.Want(i[0], i[1], KyKind.Item))
            {
                wanted++;
            }
        }

        ModLog.Verbose("CostumeFeature: started (tab " + tab + ", KY-Items " + wanted + "/" + Items.Length + ")");
        return tab && wanted == Items.Length;
    }

    public bool Stop()
    {
        bool tab = HubTabs.Remove(HubTabs.CostumeId);
        int dropped = 0;
        foreach (string[] i in Items)
        {
            if (KyItemsContributions.Drop(i[0]))
            {
                dropped++;
            }
        }

        ModLog.Verbose("CostumeFeature: stopped (tab removed " + tab + ", KY-Items dropped " + dropped + "/" +
                       Items.Length + ")");
        return tab && dropped == Items.Length;
    }
}
