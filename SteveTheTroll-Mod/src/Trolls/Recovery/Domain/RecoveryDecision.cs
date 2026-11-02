namespace SteveTheTroll.Trolls.Recovery.Domain
{
    /// <summary>
    /// Architecture V2 (D-169): what RecoveryMachine decided in one frame - the command and the state of the ladder the executor needs to carry it out (which detour, which leg,
    /// which side, head-on or not). Typed data only: no delegates, no game references (the executor owns the delegates). Readonly struct.
    /// </summary>
    internal readonly struct RecoveryDecision
    {
        internal RecoveryCmd Command { get; }
        /// The stage AFTER the step.
        internal RecoveryStage Stage { get; }
        /// The detour that runs now (1 / 2; 0 = none).
        internal int DetourNumber { get; }
        /// 0 = first leg, 1 = second leg (detour 2 only).
        internal int Leg { get; }
        /// The preferred side of the running leg (the adapter may still switch sides when the ground is bad: StuckRules.ChooseSide).
        internal Side LegSide { get; }
        internal bool HeadOn { get; }
        internal bool Raged { get; }
        internal int DetoursDone { get; }
        internal bool RightFirst { get; }

        RecoveryDecision(RecoveryCmd command, RecoveryMachine m)
        {
            Command = command;
            Stage = m.Stage;
            DetourNumber = m.DetourNumber;
            Leg = m.Leg;
            LegSide = m.LegSide;
            HeadOn = m.HeadOn;
            Raged = m.Raged;
            DetoursDone = m.DetoursDone;
            RightFirst = m.RightFirst;
        }

        /// The decision of `command` with the machine's state after the step. Expected outcome: Command == command, Stage == m.Stage.
        internal static RecoveryDecision Of(RecoveryCmd command, RecoveryMachine m) { return new RecoveryDecision(command, m); }

        /// Nothing to execute this frame (keep walking / keep the manoeuvre going).
        internal bool IsNone { get { return Command == RecoveryCmd.None; } }

        public override string ToString()
        {
            return Command + " (stage " + Stage + ", detour " + DetourNumber + " leg " + Leg + " " + LegSide + (HeadOn ? ", head-on" : "") + ", raged " + Raged + ", detours done " + DetoursDone + ")";
        }
    }
}
