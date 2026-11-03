using System.Collections.Generic;

namespace SteveTheTroll.Features.Trolls.Combat.Domain;

/// <summary>
///     D-171 (P5): the game-free decisions of TrollGuard (D-065 / D-078 / D-081): which creature that goes for the owner a
///     troll takes on, and which one of those it fights.
///     Squared flat distances in m^2 (the caller has them already, no square roots per creature).
/// </summary>
internal static class GuardRules
{
    /// A creature after the owner counts for this troll: the troll defends at all (slippers on or a raid), the troll is near its owner (D-078), the creature is within the
    /// guard range and the keep range of the troll (D-081), and within the defend range of the troll or of the owner.
    internal static bool Eligible(bool defends, bool ownerNear, float toTroll2, float toOwner2, float guard2,
        float keep2, float range2)
    {
        if (!defends || !ownerNear)
        {
            return defends && ownerNear;
        }

        if (toTroll2 > guard2 || toTroll2 > keep2)
        {
            return toTroll2 <= guard2 && toTroll2 <= keep2;
        }

        return toTroll2 <= range2 || toOwner2 <= range2;
    }

    /// The index of the nearest distance (-1 for none). Expected outcome: the smallest value's index, the first one on a tie.
    internal static int Nearest(IList<float> distances)
    {
        int best = -1;
        for (int i = 0; i < (distances != null ? distances.Count : 0); i++)
        {
            if (best < 0 || distances[i] < distances[best])
            {
                best = i;
            }
        }

        return best;
    }
}
