using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SteveTheTroll.Ui;

/// <summary>
///     Slippers tab (D-039): the detail blocks General (slot, armor at quality 1 and max, frost, worn quality) and
///     Crafting (station, recipe, cost
///     of each upgrade). Pure logic, no Unity / Valheim types (unit-tested: SlipperDetailsBuilderTests). `text` turns a
///     key into the shown text.
/// </summary>
internal static class SlipperDetailsBuilder
{
    /// Expected outcome: two groups (General, Crafting); an empty list when there is no data.
    internal static List<DetailGroup> Build(SlipperData d, Func<string, string> text)
    {
        var groups = new List<DetailGroup>();
        if (d == null || text == null)
        {
            return groups;
        }

        int maxQ = Math.Max(1, d.MaxQuality);

        var general = new DetailGroup(text("stevethetroll_details_general"));
        general.Add(text("stevethetroll_details_slot"), text("stevethetroll_details_slot_utility"));
        general.Add(text("stevethetroll_details_armor"), N(Armor(d, 1)), maxQ > 1 ? N(Armor(d, maxQ)) : "");
        general.Add(text("stevethetroll_details_frost"),
            text(d.FrostResistant ? "stevethetroll_details_resistant" : "stevethetroll_details_none"));
        general.Add(text("stevethetroll_details_worn"),
            d.WornQuality > 0
                ? text("stevethetroll_details_quality") + " " + d.WornQuality
                : text("stevethetroll_details_not_worn"));
        groups.Add(general);

        var crafting = new DetailGroup(text("stevethetroll_details_crafting"));
        crafting.Add(text("stevethetroll_details_station"),
            d.Station + " (" + text("stevethetroll_details_level") + " " + d.StationLevel + ")");
        crafting.Add(text("stevethetroll_details_recipe"), Lines(d.Recipe, false));
        if (maxQ > 1)
        {
            crafting.Add(text("stevethetroll_details_upgrade"), Lines(d.Recipe, true));
        }

        crafting.Add(text("stevethetroll_details_max_quality"), maxQ.ToString(CultureInfo.InvariantCulture));
        groups.Add(crafting);
        return groups;
    }

    /// Armor at that quality (1 = base): base + perLevel x (quality - 1).
    internal static float Armor(SlipperData d, int quality)
    {
        return d.BaseArmor + d.ArmorPerLevel * Math.Max(0, quality - 1);
    }

    // "5 Troll hide, 4 Deer hide" (craft) or the per-upgrade amounts; ingredients with nothing to pay are left out.
    private static string Lines(List<SlipperData.Ingredient> recipe, bool upgrade)
    {
        var sb = new StringBuilder();
        foreach (SlipperData.Ingredient i in recipe)
        {
            int n = upgrade ? i.PerUpgrade : i.Amount;
            if (n <= 0)
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(n.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(i.Name);
        }

        return sb.Length > 0 ? sb.ToString() : "-";
    }

    private static string N(float v)
    {
        return v.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
