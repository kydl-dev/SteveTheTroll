namespace SteveTheTroll.Trolls.Recovery.Domain
{
    /// <summary>D-167: where one recovery cycle stands (travel = no manoeuvre runs).</summary>
    internal enum RecoveryStage { Travel, Rage, Leg }

    /// <summary>D-167: what the game side has to do now (RecoveryDecision.Command; RecoveryExecutor maps each to its action).</summary>
    internal enum RecoveryCmd { None, StartRage, EndRage, StartLeg, EndLeg, CycleFailed, CycleReset }
}
