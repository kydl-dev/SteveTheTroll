using System;

namespace SteveTheTroll.Ui;

/// <summary>
///     D-119: what the "My trolls" tab can do with a troll, given by the game (src/Ui/TrollCommands.cs); the Unity preview
///     passes logging stubs.
///     Plain delegates, no Unity / Valheim types. A null delegate = that button does nothing (logged).
/// </summary>
internal sealed class TrollActions
{
    /// Troll id -> metres from the player now (negative = unknown).
    internal Func<string, float> Distance;

    /// Troll id -> it follows the player (a far, not loaded troll is brought next to the player first, user 2026-10-28: "Teleport to you").
    internal Action<string> Follow;

    /// Troll id -> it dies (after the tab's own Yes / No question).
    internal Action<string> Kill;
}
