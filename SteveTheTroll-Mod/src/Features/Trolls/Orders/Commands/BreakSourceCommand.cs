using Kydl.Core.Commands;
using UnityEngine;

namespace SteveTheTroll.Trolls.Orders.Commands
{
    /// <summary>D-170 (T-008d / D-065): break THIS source first, then farm on with the focus.</summary>
    internal sealed class BreakSourceCommand : ICommand<bool>
    {
        internal Character Troll { get; }
        internal GameObject Source { get; }
        internal string Focus { get; }
        internal BreakSourceCommand(Character troll, GameObject source, string focus) { Troll = troll; Source = source; Focus = focus ?? ""; }
    }
}
