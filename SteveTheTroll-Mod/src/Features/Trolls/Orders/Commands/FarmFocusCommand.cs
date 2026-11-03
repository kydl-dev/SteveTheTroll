using Kydl.Core.Commands;
using Kydl.Core.Features;
using Kydl.Core.Identity;
using Kydl.Core.Pipeline;
using Kydl.Core.Results;
using Kydl.Core.Time;

namespace SteveTheTroll.Features.Trolls.Orders.Commands
{
    /// <summary>D-170: "go farm" - the troll farms its focus (or the default priority). `Announce` (D-058): it started on its own, its owner gets a message with the item found.</summary>
    internal sealed class FarmFocusCommand : ICommand<bool>
    {
        internal Character Troll { get; }
        internal string Focus { get; }
        internal bool Announce { get; }
        internal FarmFocusCommand(Character troll, string focus, bool announce = false) { Troll = troll; Focus = focus ?? ""; Announce = announce; }
    }
}
