using SteveTheTroll.Core.Commands;
using UnityEngine;

namespace SteveTheTroll.Trolls.Orders.Commands
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
