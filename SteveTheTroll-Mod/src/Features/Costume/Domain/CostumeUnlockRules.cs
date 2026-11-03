namespace SteveTheTroll.Features.Costume.Domain;

/// <summary>
///     T-201 (user 2026-11-03: "costume recipes learned only after first equipping the slippers"): the forced path is
///     craft the slippers -> equip them -> the
///     costume recipes (head, suit, trousers, Troll Club) appear; never the costume first. Pure rules (no game types),
///     unit tested; the game side is
///     CostumeUnlock (marks the character when the slippers are equipped) and RecipeUnlockPatch (asks
///     <see cref="Discoverable" />).
/// </summary>
internal static class CostumeUnlockRules
{
    /// The key in the character's custom data (saved with the character): the slippers were equipped at least once.
    internal const string WornKey = "stt_slippers_worn";

    /// May the game show a costume recipe now? Expected outcome: true only when the slippers were worn once AND the recipe would be shown otherwise (the
    /// game's own rule said yes, or one of its materials is known - D-076).
    internal static bool Discoverable(bool gameSays, bool anyMaterialKnown, bool slippersWornOnce)
    {
        bool materials = gameSays || anyMaterialKnown;
        bool shown = slippersWornOnce && materials;
        return shown;
    }

    /// Should this equip mark the character? Expected outcome: true for the local player putting on the slippers (the equip succeeded) the first time.
    internal static bool ShouldMark(bool localPlayer, bool isSlippers, bool equipped, bool alreadyMarked)
    {
        bool mark = localPlayer && isSlippers && equipped && !alreadyMarked;
        return mark;
    }
}
