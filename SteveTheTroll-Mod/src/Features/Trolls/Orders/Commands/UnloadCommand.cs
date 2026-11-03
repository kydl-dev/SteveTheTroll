using Kydl.Core.Commands;
using Kydl.Core.Features;
using Kydl.Core.Identity;
using Kydl.Core.Pipeline;
using Kydl.Core.Results;
using Kydl.Core.Time;

namespace SteveTheTroll.Features.Trolls.Orders.Commands
{
    /// <summary>D-170 (D-143): walk to the hub / bed and empty the backpack into the depot.</summary>
    internal sealed class UnloadCommand : ICommand<bool>
    {
        internal Character Troll { get; }
        internal UnloadCommand(Character troll) { Troll = troll; }
    }
}
