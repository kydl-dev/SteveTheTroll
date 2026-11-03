using KydlCore.Core;

namespace SteveTheTroll.Features.Trolls.Orders.Commands;

/// <summary>D-170 (T-008e): walk to the owner's bed / hub and wait there (silent).</summary>
internal sealed class SendHomeCommand : ICommand<bool>
{
    internal SendHomeCommand(Character troll)
    {
        Troll = troll;
    }

    internal Character Troll { get; }
}
