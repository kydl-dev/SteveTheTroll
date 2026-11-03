namespace SteveTheTroll.Features.Trolls.Combat.Domain;

/// <summary>D-171: how a troll answers a hit on itself (D-084).</summary>
internal enum CounterResponse
{
    None,
    Busy,
    Ignore,
    Slam,
    Chase
}

/// <summary>
///     D-171 (P5): the game-free decision of TrollCounter (D-084, user: "When trolls are getting attacked, they should do
///     3 hits overhead into the ground and then go back to
///     farming ... if they get hit again after the quiet time, they go for the attacker").
/// </summary>
internal static class CounterRules
{
    /// Expected outcome: Busy while a rage / slam / chase runs; Ignore inside the ignore window; Slam for the first hit (or after the calm time); else Chase.
    internal static CounterResponse Respond(bool busy, float now, float ignoreUntil, float lastResponse,
        bool slammedBefore, float calmSeconds)
    {
        if (busy)
        {
            return CounterResponse.Busy;
        }

        if (now < ignoreUntil)
        {
            return CounterResponse.Ignore;
        }

        bool calm = now - lastResponse > calmSeconds;
        return !slammedBefore || calm ? CounterResponse.Slam : CounterResponse.Chase;
    }
}
