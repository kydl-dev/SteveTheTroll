using System.Collections.Generic;

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// Player-facing text of the mod, English only (user rule: no other languages). Shared by the game (registered with Jotunn in Plugin.Awake through Kydl.Common.Loc)
    /// and the Unity preview (English). Every key starts with "stevethetroll_" so it never clashes with another mod.
    /// </summary>
    internal static class UiText
    {
        internal static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            { "stevethetroll_menu_title", "SteveTheTroll" },
            { "stevethetroll_menu_footer", "Esc to close" },
            { "stevethetroll_slippers", "Fluffy Slippers" },
            { "stevethetroll_slippers_desc", "Soft, fluffy plush slippers with a fur-lined rim. Warm toes, brave heart." },
            { "stevethetroll_se_frost", "Fluffy feet" },
            { "stevethetroll_se_frost_tooltip", "Warm, fluffy slippers: resistant to frost." },
            { "stevethetroll_tab_slippers", "Slippers" },
            { "stevethetroll_tab_troll", "Troll work" },
            { "stevethetroll_viewer_hint", "Drag: turn - right-drag: move - wheel: zoom" },
            { "stevethetroll_viewer_reset", "Reset" },
            { "stevethetroll_viewer_unavailable", "3D view not available here" },
            { "stevethetroll_details_unavailable", "No item data (load a world first)." },
            { "stevethetroll_details_general", "General" },
            { "stevethetroll_details_crafting", "Crafting" },
            { "stevethetroll_details_slot", "Slot" },
            { "stevethetroll_details_slot_utility", "Utility" },
            { "stevethetroll_details_armor", "Armor" },
            { "stevethetroll_details_frost", "Frost" },
            { "stevethetroll_details_resistant", "Resistant" },
            { "stevethetroll_details_none", "-" },
            { "stevethetroll_details_worn", "Worn now" },
            { "stevethetroll_details_quality", "Quality" },
            { "stevethetroll_details_not_worn", "Not worn" },
            { "stevethetroll_details_station", "Crafted at" },
            { "stevethetroll_details_level", "level" },
            { "stevethetroll_details_recipe", "Recipe" },
            { "stevethetroll_details_upgrade", "Each upgrade" },
            { "stevethetroll_details_max_quality", "Max quality" },
            { "stevethetroll_details_q1", "Quality 1" },
            { "stevethetroll_details_qmax", "Quality" },
            { "stevethetroll_focus_heading", "Focus farming" },
            { "stevethetroll_focus_note", "Pick what each tamed troll gathers first. No focus: Yggdrasil wood, Ancient bark, Iron scrap, Fine wood, Silver ore, Copper ore, Core wood, Fine wood, Tin ore, then Wood - the first one found nearby." },
            { "stevethetroll_focus_label", "Farm" },
            { "stevethetroll_focus_none", "No focus" },
            { "stevethetroll_focus_wood", "Wood" },
            { "stevethetroll_focus_finewood", "Fine wood" },
            { "stevethetroll_focus_corewood", "Core wood" },
            { "stevethetroll_focus_copper", "Copper ore" },
            { "stevethetroll_focus_tin", "Tin ore" },
            { "stevethetroll_focus_yggdrasilwood", "Yggdrasil wood" },
            { "stevethetroll_focus_elderbark", "Ancient bark" },
            { "stevethetroll_focus_ironscrap", "Iron scrap" },
            { "stevethetroll_focus_silver", "Silver ore" },
            { "stevethetroll_tab_trolls", "Trolls" },
            { "stevethetroll_troll_default", "Troll" },
            { "stevethetroll_troll_none", "No tamed troll yet. Wear the slippers near a wild troll for a minute." },
            { "stevethetroll_troll_name", "Troll" },
            { "stevethetroll_troll_doing", "Doing" },
            { "stevethetroll_troll_farming", "Farming now" },
            { "stevethetroll_troll_lastseen_note", "* = far away: last seen values; a new focus is used when the troll is near you again." },
            { "stevethetroll_troll_pick", "Troll" },
            { "stevethetroll_troll_general", "Troll" },
            { "stevethetroll_troll_status", "Doing" },
            { "stevethetroll_troll_lastseen", "last seen" },
            { "stevethetroll_troll_stars", "Stars" },
            { "stevethetroll_troll_health", "Health" },
            { "stevethetroll_troll_damage", "Damage" },
            { "stevethetroll_troll_position", "Position" },
            { "stevethetroll_troll_bed", "Distance from your bed" },
            { "stevethetroll_troll_nobed", "no bed set" },
            { "stevethetroll_status_unknown", "Unknown" },
            { "stevethetroll_status_following", "Following you" },
            { "stevethetroll_status_staying", "Staying" },
            { "stevethetroll_status_fighting", "Fighting" },
            { "stevethetroll_status_coming", "Coming to you" },
            { "stevethetroll_status_dead", "Dead" },
            { "stevethetroll_troll_attacks", "Damage" },
            { "stevethetroll_troll_resists", "Resistances" },
            { "stevethetroll_troll_every", "every" },
            { "stevethetroll_troll_noattacks", "not known yet (seen when the troll is near you)" },
            { "stevethetroll_troll_died", "{0} has died." },
            { "stevethetroll_gofarm", "Go farm" },
            { "stevethetroll_gofarm_nofocus", "Pick what {0} should farm first." },
            { "stevethetroll_gofarm_far", "{0} is too far away to get orders." },
            { "stevethetroll_gofarm_started", "{0} goes to farm {1}." },
            { "stevethetroll_status_farming", "Farming" },
            { "stevethetroll_troll_waits", "{0} is here and waits for your orders." },
            { "stevethetroll_troll_coming", "{0} is coming!" },
            { "stevethetroll_farm_auto", "{0} got no orders and goes farming {1}." },
            { "stevethetroll_farm_nothing", "{0} found nothing to farm within {1} m and walks back to your bed." },
            { "stevethetroll_farm_home", "{0} is back at your bed and waits." },
            { "stevethetroll_under_attack", "{0} is under attack. Attacking back." },
            { "stevethetroll_focus_following", "Your troll is following you. Press E on it so it stays, or press Go farm - then it farms {0}." },
        };


        /// English text (the Unity preview; the game reads the same table through Jotunn).
        internal static string English(string key)
        {
            string s;
            return En.TryGetValue(key, out s) ? s : key;
        }
    }
}
