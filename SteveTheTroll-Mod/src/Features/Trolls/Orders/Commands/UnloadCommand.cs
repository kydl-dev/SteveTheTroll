using KydlCore.Core;

namespace SteveTheTroll.Features.Trolls.Orders.Commands;

/// <summary>D-170 (D-143): walk to the hub / bed and empty the backpack into the depot.</summary>
internal sealed class UnloadCommand : ICommand<bool>
{
    internal UnloadCommand(Character troll)
    {
        Troll = troll;
    }

    internal Character Troll { get; }
}
