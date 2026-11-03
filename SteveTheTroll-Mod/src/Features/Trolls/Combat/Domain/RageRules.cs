namespace SteveTheTroll.Features.Trolls.Combat.Domain;

/// <summary>D-171 (P5): the game-free decisions of TrollRage (D-082 / D-084).</summary>
internal static class RageRules
{
    /// The weapon part of the rage reach (m), on top of both body radii.
    internal const float WeaponReach = 2.5f;

    /// A troll goes into rage when its owner is hit and it stands within `rageRange` (0 = rage off). Expected outcome: false for rage off.
    internal static bool Enrages(float distance, float rageRange)
    {
        return rageRange > 0f && distance <= rageRange;
    }

    /// The rage is over: the troll is gone / dead, the owner was never hit, or the last hit is older than the calm time.
    internal static bool CalmsDown(bool alive, bool ownerWasHit, float sinceHit, float calmSeconds)
    {
        return !alive || !ownerWasHit || sinceHit > calmSeconds;
    }

    /// D-084 (TEST-036: a wild troll's centre stays 5-9 m away): reach = both body radii + the weapon.
    internal static float Reach(float myRadius, float otherRadius)
    {
        return myRadius + otherRadius + WeaponReach;
    }
}
