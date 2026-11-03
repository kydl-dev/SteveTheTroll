using Kydl.Core.Commands;
using Kydl.Core.Features;
using Kydl.Core.Identity;
using Kydl.Core.Pipeline;
using Kydl.Core.Results;
using Kydl.Core.Time;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Orders.Commands
{
    /// <summary>D-170 (D-124): break a fortress wall (2-star trolls only - TrollWalls decides).</summary>
    internal sealed class BreakWallCommand : ICommand<bool>
    {
        internal Character Troll { get; }
        internal GameObject Wall { get; }
        internal string Focus { get; }
        internal BreakWallCommand(Character troll, GameObject wall, string focus) { Troll = troll; Wall = wall; Focus = focus ?? ""; }
    }
}
