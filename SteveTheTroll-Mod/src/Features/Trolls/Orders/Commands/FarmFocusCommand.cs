using KydlCore.Core;

namespace SteveTheTroll.Features.Trolls.Orders.Commands;

/// <summary>
///     D-170: "go farm" - the troll farms its focus (or the default priority). `Announce` (D-058): it started on its
///     own, its owner gets a message with the item found.
/// </summary>
internal sealed class FarmFocusCommand : ICommand<bool>
{
    internal FarmFocusCommand(Character troll, string focus, bool announce = false)
    {
        Troll = troll;
        Focus = focus ?? "";
        Announce = announce;
    }

    internal Character Troll { get; }
    internal string Focus { get; }
    internal bool Announce { get; }
}
