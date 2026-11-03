using Kydl.Core.Commands;
using Kydl.Core.Features;
using Kydl.Core.Identity;
using Kydl.Core.Pipeline;
using Kydl.Core.Results;
using Kydl.Core.Time;

namespace SteveTheTroll.Features.Trolls.Orders.Commands
{
    /// <summary>D-170 (T-008e): walk to the owner's bed / hub and wait there (silent).</summary>
    internal sealed class SendHomeCommand : ICommand<bool>
    {
        internal Character Troll { get; }
        internal SendHomeCommand(Character troll) { Troll = troll; }
    }
}
