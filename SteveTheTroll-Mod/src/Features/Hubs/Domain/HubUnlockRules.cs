using System;

namespace SteveTheTroll.Features.Hubs.Domain;

/// <summary>
///     The pure rules of when the Troll Hub shows in the build menu (user, 2026-11-04: only after the costume set bonus has been
///     active). No game types, so unit tests prove them (HubUnlockRulesTests); HubPlacement feeds them with the game's state.
/// </summary>
internal static class HubUnlockRules
{
    /// Is this piece (prefab name, with or without "(Clone)") the Troll Hub? Expected outcome: true for "STT_TrollHub" and "STT_TrollHub(Clone)".
    internal static bool IsHub(string prefabName, string hubPrefab)
    {
        return prefabName != null && prefabName.StartsWith(hubPrefab, StringComparison.Ordinal);
    }

    /// The game asks "does the character know this piece?" (Player.HaveRequirements, IsKnown): the answer for one piece.
    /// Expected outcome: the game's own answer for other pieces / other modes; for the hub exactly "the set bonus has been active once".
    internal static bool KnownAnswer(bool gameAnswer, bool isHub, bool isKnownMode, bool unlocked)
    {
        if (!isHub || !isKnownMode)
        {
            return gameAnswer;
        }

        return unlocked;
    }

    /// Should the hub be unlocked now? Expected outcome: true only the first time the set bonus is active.
    internal static bool ShouldUnlock(bool alreadyUnlocked, bool setBonusActive)
    {
        return !alreadyUnlocked && setBonusActive;
    }
}
