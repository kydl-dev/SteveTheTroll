using System.Collections.Generic;

namespace SteveTheTroll.Features.Hubs.Domain;

/// <summary>
///     D-135 (user 2026-10-30): the pure rules of the Troll Hubs, unit-tested (HubRulesTests). Distances are flat metres.
///     Max 10 hubs per player; hubs at least 1000 m apart; a hub serves a 500 m circle; inside a served circle tamed
///     trolls are 50 % faster than vanilla
///     (instead of 25 %). D-159 (user 2026-10-31): a troll auto-assigns to and goes to any hub within AutoReach (1000 m);
///     with no hub within FarRadius (1250 m)
///     it works at half speed; it does not go further than FarmLeash (950 m) from its hub to farm (only following the
///     player, or the F6 teleport, takes it further).
/// </summary>
internal static class HubRules
{
    internal const int MaxHubs = 10;

    internal const float MinSpacing = 1000f,
        ServeRadius = 500f,
        FarRadius = 1250f,
        ManualReach = 1000f,
        AutoReach = 1000f,
        FarmLeash = 950f;

    internal const float FarSpeedShare = 0.5f, HubSpeedFactor = 1.5f;

    /// May the player build another hub? `own` = hubs the player has, `nearestOwn` = flat distance to the nearest hub of ANY player (-1 = none).
    /// Expected outcome: TooMany at 10, TooClose below 1000 m (exactly 1000 m is fine), else Ok.
    internal static Verdict Check(int own, float nearest)
    {
        if (own >= MaxHubs)
        {
            return Verdict.TooMany;
        }

        if (nearest >= 0f && nearest < MinSpacing)
        {
            return Verdict.TooClose;
        }

        return Verdict.Ok;
    }

    /// The smallest number from 1 that is not used yet. Expected outcome: 1 for none, 3 for {1, 2}, 2 for {1, 3}.
    internal static int NextNumber(ICollection<int> used)
    {
        int n = 1;
        while (used != null && used.Contains(n))
        {
            n++;
        }

        return n;
    }

    internal static string DefaultName(int number)
    {
        return "HUB " + number;
    }


    /// Is a troll at `distance` inside the circle a hub serves? Expected outcome: true at exactly the radius.
    internal static bool Serves(float distance, float radius)
    {
        return distance >= 0f && distance <= radius;
    }

    /// The movement factor of a tamed troll. `tamed` = the usual tamed factor (1.25). Hubs off or no set bonus -> `tamed`; inside a served circle ->
    /// HubSpeedFactor; no hub within FarRadius (nearest = -1: no hub at all) -> tamed x FarSpeedShare; in between -> `tamed`.
    internal static float SpeedFactor(float tamed, bool hubsOn, bool setActive, float nearest)
    {
        if (!hubsOn || !setActive)
        {
            return tamed;
        }

        if (nearest >= 0f && Serves(nearest, ServeRadius))
        {
            return HubSpeedFactor;
        }

        if (nearest >= 0f && nearest <= FarRadius)
        {
            return tamed;
        }

        return tamed * FarSpeedShare;
    }

    /// Which hub a troll uses. `manual` = the hub number it was sent to (0 = none), `manualDistance` = flat distance to that hub (-1 = hub gone),
    /// `nearestNumber` = the nearest hub (0 = none) at flat distance `nearestDistance` (-1 = none). Expected outcome: the manual hub while it is within ManualReach,
    /// else the nearest one when it is within AutoReach (and `auto` is on, or a hub was chosen by hand), else 0 (the bed is used).
    internal static int Pick(int manual, float manualDistance, int nearestNumber, float nearestDistance, bool auto)
    {
        if (manual > 0 && manualDistance >= 0f && manualDistance <= ManualReach)
        {
            return manual;
        }

        if (!(auto || manual > 0) || nearestNumber <= 0)
        {
            return 0;
        }

        return nearestDistance >= 0f && nearestDistance <= AutoReach ? nearestNumber : 0;
    }

    /// May a troll whose hub is `hubDistance` (flat metres from the hub, -1 = it has no hub) farm at a spot `spotToHub` metres from that hub? A troll without a hub is not
    /// limited; with one, never beyond FarmLeash (exactly 950 m is fine). Expected outcome: true when allowed.
    /// D-160: the index of `owner`'s hub nearest to the troll (hubs of other owners and negative distances are ignored; ties: the first); -1 = none. `distance` = its distance (-1).
    internal static int NearestOwned(long owner, long[] owners, float[] distances, out float distance)
    {
        int best = -1;
        distance = -1f;
        for (int i = 0; i < owners.Length && i < distances.Length; i++)
        {
            if (owners[i] != owner || distances[i] < 0f)
            {
                continue;
            }

            if (best < 0 || distances[i] < distance)
            {
                best = i;
                distance = distances[i];
            }
        }

        return best;
    }

    /// D-162 (user 2026-10-31): should a troll without a follow order walk back to its hub? It starts when it is further than AutoReach (1000 m) from the hub and keeps going
    /// (`returning`) until it is inside FarmLeash (950 m) - the gap stops it from turning round at the line. Expected outcome: false without a hub (`distance` -1).
    internal static bool ShouldReturn(bool hasHub, float distance, bool returning)
    {
        if (!hasHub || distance < 0f)
        {
            return false;
        }

        return returning ? distance > FarmLeash : distance > AutoReach;
    }

    /// D-173 (P7): a troll STARTS walking back when ShouldReturn says so and nothing else has it: no follow order, not arriving at its tamer, the owner wears the slippers,
    /// not fighting / raging, not on its way to unload (the old checks of TrollReturn.Check, in their order). Expected outcome: true = start now.
    internal static bool ShouldStartReturn(bool hasHub, float distance, bool followOrder, bool arriving,
        bool ownerWears, bool fighting, bool unloading)
    {
        if (!ShouldReturn(hasHub, distance, false))
        {
            return false;
        }

        return !followOrder && !arriving && ownerWears && !fighting && !unloading;
    }

    /// D-162: what a walking-back troll does this step. A new follow order ends the job (Cancel); a fight - the troll or the player (in range) under attack - gives the
    /// control to the combat / rage code (Yield, the job stays); inside FarmLeash it is Done (it farms again); else Walk.
    internal static ReturnStep ReturnNext(bool followOrder, bool underAttack, float distance)
    {
        if (followOrder)
        {
            return ReturnStep.Cancel;
        }

        if (underAttack)
        {
            return ReturnStep.Yield;
        }

        if (distance >= 0f && distance <= FarmLeash)
        {
            return ReturnStep.Done;
        }

        return ReturnStep.Walk;
    }

    internal static bool FarmAllowed(bool hasHub, float spotToHub)
    {
        return !hasHub || (spotToHub >= 0f && spotToHub <= FarmLeash);
    }

    internal enum Verdict
    {
        Ok,
        TooMany,
        TooClose
    }

    internal enum ReturnStep
    {
        Walk,
        Yield,
        Done,
        Cancel
    }
}
