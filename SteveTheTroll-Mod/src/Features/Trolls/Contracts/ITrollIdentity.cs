namespace SteveTheTroll.Features.Trolls.Contracts
{
    /// <summary>Architecture V2 (D-169): who a troll is. Reading these never writes to the network (TrollContext.EnsureId is the one explicit write).</summary>
    internal interface ITrollIdentity
    {
        /// The mod's troll id (ZDO key stt_id); "" while the troll has none yet.
        string Id { get; }
        /// The troll's shown name.
        string Name { get; }
        /// Player id of the player who tamed it (0 = wild / unknown).
        long OwnerId { get; }
    }
}
