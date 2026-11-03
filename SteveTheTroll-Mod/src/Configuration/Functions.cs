namespace SteveTheTroll.Configuration;

/// <summary>
///     Which functionalities of SteveTheTroll are on (PROJECT_RULES 11, user 2026-10-30). Pure: no game types, unit
///     tested.
///     A functionality works only when [General] EnableAll AND its own [Section] Enable are on. Off = it is not added to
///     the game / not shown.
///     Functionalities: Slippers (the item and the mini trolls on the feet), Costume (head, suit, trousers, club, set
///     effects) and
///     Trolls (taming and farming of real trolls, backpack and depots, map pins, extra trolls near the bed).
/// </summary>
internal static class Functions
{
    /// Expected outcome: true only when both switches are on.
    internal static bool IsOn(bool enableAll, bool own)
    {
        return enableAll && own;
    }
}
