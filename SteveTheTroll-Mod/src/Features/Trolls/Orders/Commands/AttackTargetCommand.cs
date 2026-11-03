using KydlCore.Core;

namespace SteveTheTroll.Features.Trolls.Orders.Commands;

/// <summary>D-170 (D-065): attack this creature.</summary>
internal sealed class AttackTargetCommand : ICommand<bool>
{
    internal AttackTargetCommand(Character troll, Character target)
    {
        Troll = troll;
        Target = target;
    }

    internal Character Troll { get; }
    internal Character Target { get; }
}
