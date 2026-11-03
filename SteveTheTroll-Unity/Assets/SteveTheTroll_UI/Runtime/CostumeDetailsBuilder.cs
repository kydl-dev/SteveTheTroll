using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SteveTheTroll.Ui;

/// <summary>
///     D-080 Costume tab: detail blocks - the set (worn x / 4, effects), one block per piece (slot, armor at quality 1 and
///     max, worn, recipe,
///     each upgrade) and the Troll Club (damage at quality 1 and max, recipe, each upgrade). Pure logic (unit-tested:
///     CostumeDetailsBuilderTests).
/// </summary>
internal static class CostumeDetailsBuilder
{
    /// Expected outcome: 1 set block + one per piece + 1 club block; an empty list when there is no data.
    internal static List<DetailGroup> Build(CostumeData d, Func<string, string> text)
    {
        var groups = new List<DetailGroup>();
        if (d == null || text == null)
        {
            return groups;
        }

        var set = new DetailGroup(text("stevethetroll_details_set") + ": " + d.SetName);
        set.Add(text("stevethetroll_details_set_worn"), d.SetWorn.ToString(CultureInfo.InvariantCulture) + " / " +
                                                        d.SetSize.ToString(CultureInfo.InvariantCulture)
                                                        + (d.SetWorn >= d.SetSize
                                                            ? " - " + text("stevethetroll_details_set_active")
                                                            : ""));
        set.Add(text("stevethetroll_details_set_effects"),
            d.SetEffects.Count > 0 ? string.Join(", ", d.SetEffects.ToArray()) : "-");
        groups.Add(set);

        foreach (CostumeData.Piece p in d.Pieces)
        {
            int maxQ = Math.Max(1, p.MaxQuality);
            var g = new DetailGroup(p.Name);
            g.Add(text("stevethetroll_details_slot"), text(p.SlotKey));
            g.Add(text("stevethetroll_details_armor"), N(Armor(p, 1)), maxQ > 1 ? N(Armor(p, maxQ)) : "");
            g.Add(text("stevethetroll_details_worn"),
                p.WornQuality > 0
                    ? text("stevethetroll_details_quality") + " " + p.WornQuality
                    : text("stevethetroll_details_not_worn"));
            g.Add(text("stevethetroll_details_recipe"), Lines(p.Recipe, false));
            if (maxQ > 1)
            {
                g.Add(text("stevethetroll_details_upgrade"), Lines(p.Recipe, true));
            }

            groups.Add(g);
        }

        int clubQ = Math.Max(1, d.ClubMaxQuality);
        var club = new DetailGroup(d.ClubName);
        club.Add(text("stevethetroll_details_blunt"), N(d.ClubBlunt),
            clubQ > 1 ? N(d.ClubBlunt + d.ClubBluntPerLevel * (clubQ - 1)) : "");
        club.Add(text("stevethetroll_details_chop"), N(d.ClubChop),
            clubQ > 1 ? N(d.ClubChop + d.ClubChopPerLevel * (clubQ - 1)) : "");
        club.Add(text("stevethetroll_details_pickaxe"), N(d.ClubPickaxe),
            clubQ > 1 ? N(d.ClubPickaxe + d.ClubPickaxePerLevel * (clubQ - 1)) : "");
        club.Add(text("stevethetroll_details_recipe"), Lines(d.ClubRecipe, false));
        if (clubQ > 1)
        {
            club.Add(text("stevethetroll_details_upgrade"), Lines(d.ClubRecipe, true));
        }

        club.Add(text("stevethetroll_details_station"),
            d.Station + " (" + text("stevethetroll_details_level") + " " + d.StationLevel + ")");
        groups.Add(club);
        return groups;
    }

    /// Armor at that quality (1 = base): base + perLevel x (quality - 1).
    internal static float Armor(CostumeData.Piece p, int quality)
    {
        return p.Armor + p.ArmorPerLevel * Math.Max(0, quality - 1);
    }

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
