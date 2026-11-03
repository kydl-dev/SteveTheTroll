
namespace SteveTheTroll.Features.Trolls.Farming.Domain
{
    /// <summary>D-170: what a farming troll does first in one AI frame (the priority order of TrollFarmer.Step since D-048 .. D-167).</summary>
    internal enum FarmAction
    {
        /// The troll is dead / gone: the farmer removes itself, the game's AI runs.
        Stop,
        /// A follow order (and not on its way to unload): the farmer removes itself.
        StopFollow,
        /// A fight (target creature): the game's AI drives this frame.
        YieldToFight,
        /// A running rage / detour of the shared recovery drives first.
        RecoveryDrives,
        /// The full-backpack check is due (D-159: BEFORE the home branch).
        CheckFull,
        /// Walking home / to the depot.
        GoHome,
        /// No melee item with chop / pickaxe damage (D-149).
        NoWeapon,
        /// Search / travel / walk to the target / swing.
        Work,
    }

    /// <summary>D-170: the facts the gate needs, read by TrollFarmer. Readonly struct, game-free.</summary>
    internal readonly struct FarmGateInput
    {
        internal bool Alive { get; }
        internal bool IsOwner { get; }
        internal bool FollowOrder { get; }
        /// On its way to unload (home and full): a follow order does not stop it (D-149).
        internal bool Unloading { get; }
        internal bool Fighting { get; }
        internal bool RecoveryBusy { get; }
        /// The full check is due and the troll is not full yet.
        internal bool FullCheckDue { get; }
        internal bool Home { get; }
        internal bool HasWeapon { get; }

        internal FarmGateInput(bool alive, bool isOwner, bool followOrder, bool unloading, bool fighting, bool recoveryBusy, bool fullCheckDue, bool home, bool hasWeapon)
        {
            Alive = alive; IsOwner = isOwner; FollowOrder = followOrder; Unloading = unloading; Fighting = fighting;
            RecoveryBusy = recoveryBusy; FullCheckDue = fullCheckDue; Home = home; HasWeapon = hasWeapon;
        }

        /// The same facts with a fall-through flag cleared (RecoveryDrives / CheckFull that did not drive). Expected outcome: a copy with that flag false.
        internal FarmGateInput Without(FarmAction done)
        {
            return new FarmGateInput(Alive, IsOwner, FollowOrder, Unloading, Fighting, done == FarmAction.RecoveryDrives ? false : RecoveryBusy, done == FarmAction.CheckFull ? false : FullCheckDue, Home, HasWeapon);
        }

        public override string ToString()
        {
            return "alive " + Alive + ", owner " + IsOwner + ", follow " + FollowOrder + ", unloading " + Unloading + ", fighting " + Fighting + ", recovery " + RecoveryBusy + ", full check " + FullCheckDue
                + ", home " + Home + ", weapon " + HasWeapon;
        }
    }

    /// <summary>D-170: the priority order, game-free and unit-tested (FarmGateRulesTests).</summary>
    internal static class FarmGateRules
    {
        /// Expected outcome: the first action in priority order whose condition holds; Work when none does.
        internal static FarmAction Decide(FarmGateInput i)
        {
            if (!i.Alive) return FarmAction.Stop;
            if (i.FollowOrder && !i.Unloading) return FarmAction.StopFollow;
            if (i.Fighting) return FarmAction.YieldToFight;
            if (i.RecoveryBusy) return FarmAction.RecoveryDrives;
            if (i.FullCheckDue) return FarmAction.CheckFull;
            if (i.Home) return FarmAction.GoHome;
            return i.HasWeapon ? FarmAction.Work : FarmAction.NoWeapon;
        }

        /// RecoveryDrives and CheckFull may not drive (no manoeuvre ran / not full): the gate is asked again without them.
        internal static bool MayFallThrough(FarmAction a) { return a == FarmAction.RecoveryDrives || a == FarmAction.CheckFull; }
    }
}
