using SteveTheTroll.Core.Commands;

namespace SteveTheTroll.Trolls.Orders.Commands
{
    /// <summary>D-170 (D-065): attack this creature.</summary>
    internal sealed class AttackTargetCommand : ICommand<bool>
    {
        internal Character Troll { get; }
        internal Character Target { get; }
        internal AttackTargetCommand(Character troll, Character target) { Troll = troll; Target = target; }
    }
}
