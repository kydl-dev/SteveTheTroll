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
            { "stevethetroll_focus_note", "Pick what your tamed troll gathers first. No focus: copper, pine, birch, tin, then anything else near your bed or the troll." },
            { "stevethetroll_focus_label", "Farm" },
            { "stevethetroll_focus_none", "No focus" },
            { "stevethetroll_focus_wood", "Wood" },
            { "stevethetroll_focus_finewood", "Fine wood" },
            { "stevethetroll_focus_corewood", "Core wood" },
            { "stevethetroll_focus_copper", "Copper ore" },
            { "stevethetroll_focus_tin", "Tin ore" },
        };


        /// English text (the Unity preview; the game reads the same table through Jotunn).
        internal static string English(string key)
        {
            string s;
            return En.TryGetValue(key, out s) ? s : key;
        }
    }
}
