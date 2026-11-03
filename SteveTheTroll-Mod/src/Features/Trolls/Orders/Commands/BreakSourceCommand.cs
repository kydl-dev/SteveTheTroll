using KydlCore.Core;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Orders.Commands;

/// <summary>D-170 (T-008d / D-065): break THIS source first, then farm on with the focus.</summary>
internal sealed class BreakSourceCommand : ICommand<bool>
{
    internal BreakSourceCommand(Character troll, GameObject source, string focus)
    {
        Troll = troll;
        Source = source;
        Focus = focus ?? "";
    }

    internal Character Troll { get; }
    internal GameObject Source { get; }
    internal string Focus { get; }
}
