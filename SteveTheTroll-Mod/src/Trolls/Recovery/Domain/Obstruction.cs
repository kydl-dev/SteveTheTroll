namespace SteveTheTroll.Trolls.Recovery.Domain
{
    /// <summary>What blocks a troll's way (decided from game facts by TrollRecovery; the decision itself is here and unit-tested).</summary>
    internal enum Obstruction { None, Destructible, Indestructible, PlayerBuilt, Protected }
}
