using System;
using System.Collections.Generic;

namespace SteveTheTroll.Taming
{
    internal enum RecoveryStage { Travel, Rage, Leg }

    /// <summary>What the game side has to do now.</summary>
    internal enum RecoveryCmd { None, StartRage, EndRage, StartLeg, EndLeg, CycleFailed, CycleReset }

    /// <summary>
    /// D-167: the escalation ladder of ONE complete recovery cycle (spec 1 / 2): rage against a destructible obstruction, a first detour (turn 90 degrees, walk up to 10 m), a second
    /// detour (L shape: 90 degrees one way 10 m, then back to the intended direction 10 m), then the cycle has failed. Game-free; the caller passes the facts and executes the command.
    /// A cycle that starts after a failed one turns the other way first (spec "change the detour direction on the second complete recovery cycle").
    /// </summary>
    internal sealed class RecoveryMachine
    {
        float _stageStart;

        internal RecoveryStage Stage { get; private set; }
        /// Rage was used in this cycle (once per cycle).
        internal bool Raged { get; private set; }
        /// Detours finished in this cycle (0, 1, 2).
        internal int DetoursDone { get; private set; }
        /// The detour that runs now (1 or 2; 0 = none).
        internal int DetourNumber { get; private set; }
        /// 0 = the first leg of the running detour, 1 = its second leg (detour 2 only).
        internal int Leg { get; private set; }
        /// The running detour is the head-on turn of spec 4 (always to the right, one leg).
        internal bool HeadOn { get; private set; }
        internal bool RightFirst { get; private set; }

        internal RecoveryMachine() { BeginCycle(0); }

        /// Anything done in this cycle yet (a reset is only worth reporting then).
        internal bool Dirty { get { return Raged || DetoursDone > 0 || Stage != RecoveryStage.Travel; } }

        /// A manoeuvre was cut short (a fight, a new order): back to Travel, the ladder keeps what it has done. Expected outcome: Stage Travel.
        internal void Abort() { Stage = RecoveryStage.Travel; DetourNumber = 0; Leg = 0; HeadOn = false; }

        /// A new cycle: even failure counts turn right first, odd ones left first. Expected outcome: Stage Travel, nothing used.
        internal void BeginCycle(int failedCycles)
        {
            Stage = RecoveryStage.Travel; Raged = false; DetoursDone = 0; DetourNumber = 0; Leg = 0; HeadOn = false;
            RightFirst = failedCycles % 2 == 0;
        }

        /// The side of the leg that runs now (relative to the intended direction): the first leg of a detour goes to the first side, the second leg back the other way.
        internal Side LegSide
        {
            get
            {
                Side first = HeadOn || RightFirst ? Side.Right : Side.Left;
                return Leg == 0 ? first : StuckRules.Opposite(first);
            }
        }

        /// One frame. stuck = seconds without progress, sustained = seconds of continuous progress, rageable = the obstruction may be attacked, headOn = a troll faces me in my way,
        /// legDone = the running leg is over (StuckRules.LegDone / HeadOnClear). Expected outcome: the command the game side must execute (None most frames).
        internal RecoveryCmd Step(float now, float stuck, float sustained, bool rageable, bool headOn, bool legDone)
        {
            switch (Stage)
            {
                case RecoveryStage.Rage:
                    if (now - _stageStart < StuckRules.RageSeconds) return RecoveryCmd.None;
                    Stage = RecoveryStage.Travel;
                    return RecoveryCmd.EndRage;
                case RecoveryStage.Leg:
                    if (!legDone) return RecoveryCmd.None;
                    if (DetourNumber == 2 && Leg == 0) { Leg = 1; _stageStart = now; return RecoveryCmd.StartLeg; }
                    DetoursDone = DetourNumber; DetourNumber = 0; Leg = 0; HeadOn = false;
                    Stage = RecoveryStage.Travel;
                    return RecoveryCmd.EndLeg;
            }
            if (sustained >= StuckRules.SustainedReset) return Dirty ? RecoveryCmd.CycleReset : RecoveryCmd.None;
            if (stuck < StuckRules.StuckTrigger) return RecoveryCmd.None;
            if (rageable && !Raged) { Raged = true; Stage = RecoveryStage.Rage; _stageStart = now; return RecoveryCmd.StartRage; }
            if (DetoursDone >= 2) return RecoveryCmd.CycleFailed;
            DetourNumber = DetoursDone + 1; Leg = 0; HeadOn = headOn && DetoursDone == 0;
            Stage = RecoveryStage.Leg; _stageStart = now;
            return RecoveryCmd.StartLeg;
        }
    }

    /// <summary>
    /// D-167 (spec "failure counter attached to each troll-resource pair"): per troll and per resource (its network identity, never the prefab) how many complete recovery cycles failed,
    /// and until when the resource is unavailable to that troll. Lives in a static registry, not in a movement component, so a re-created farmer cannot erase it.
    /// </summary>
    internal sealed class RecoveryBook
    {
        sealed class Entry { internal int Failed; internal float BlacklistedUntil; }

        struct Key : IEquatable<Key>
        {
            internal string Troll; internal long A, B;
            public bool Equals(Key o) { return Troll == o.Troll && A == o.A && B == o.B; }
            public override bool Equals(object o) { return o is Key && Equals((Key)o); }
            public override int GetHashCode() { unchecked { return ((Troll != null ? Troll.GetHashCode() : 0) * 397 ^ A.GetHashCode()) * 397 ^ B.GetHashCode(); } }
        }

        readonly Dictionary<Key, Entry> _entries = new Dictionary<Key, Entry>();
        const int MaxEntries = 2000;

        static Key K(string troll, long a, long b) { return new Key { Troll = troll ?? "", A = a, B = b }; }

        Entry Get(string troll, long a, long b, bool create)
        {
            Entry e;
            if (_entries.TryGetValue(K(troll, a, b), out e)) return e;
            if (!create) return null;
            if (_entries.Count > MaxEntries) _entries.Clear();
            e = new Entry();
            _entries[K(troll, a, b)] = e;
            return e;
        }

        internal int Failed(string troll, long a, long b) { var e = Get(troll, a, b, false); return e != null ? e.Failed : 0; }

        internal bool IsBlacklisted(string troll, long a, long b, float now)
        {
            var e = Get(troll, a, b, false);
            return e != null && now < e.BlacklistedUntil;
        }

        /// A complete cycle failed. Expected outcome: true when this was the failure that blacklists the resource (only when `canBlacklist`: a hub is never abandoned; its counter
        /// keeps counting so the detour direction keeps alternating). The counter starts again after a blacklisting.
        internal bool CycleFailed(string troll, long a, long b, float now, bool canBlacklist)
        {
            var e = Get(troll, a, b, true);
            e.Failed++;
            bool blacklist = canBlacklist && e.Failed >= StuckRules.MaxFailedCycles;
            if (blacklist) { e.BlacklistedUntil = now + StuckRules.BlacklistSeconds; e.Failed = 0; }
            return blacklist;
        }

        /// The troll recovered (reached the resource, or made continuous progress for StuckRules.SustainedReset): the counter starts from zero. A running blacklist stays.
        internal void Recovered(string troll, long a, long b)
        {
            var e = Get(troll, a, b, false);
            if (e != null) e.Failed = 0;
        }

        /// How many entries are kept (diagnostics).
        internal int Count { get { return _entries.Count; } }

        /// Drops entries that are neither counting nor blacklisted any more.
        internal int Prune(float now)
        {
            var dead = new List<Key>();
            foreach (var kv in _entries) if (kv.Value.Failed == 0 && now >= kv.Value.BlacklistedUntil) dead.Add(kv.Key);
            foreach (var k in dead) _entries.Remove(k);
            return dead.Count;
        }
    }
}
