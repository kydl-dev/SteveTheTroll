using System.Collections.Generic;
using NUnit.Framework;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Trolls.Backpack.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>D-160: the depot structure's life over time - an empty depot that is blocked (window open, reservation, frame above) is waited for and goes as soon as it can.</summary>
[TestFixture]
public class DepotLifecycleTests
{
    static string V(int chests, int empty, bool inUse, bool reserved, bool above) { return DepotRules.Lifecycle(chests, empty, inUse, reserved, above).ToString(); }

    [Test] public void AllFourEmpty_Removes() { Assert.That(V(4, 4, false, false, false), Is.EqualTo("Remove")); }
    [Test] public void OneStillHoldsAnItem_Keeps() { Assert.That(V(4, 3, false, false, false), Is.EqualTo("Keep")); }
    [Test] public void NoChestFound_Keeps() { Assert.That(V(0, 0, false, false, false), Is.EqualTo("Keep")); }
    [Test] public void OnlyThreeChestsFoundAllEmpty_StillCountsTheChestsFound() { Assert.That(V(3, 3, false, false, false), Is.EqualTo("Remove")); }
    [Test] public void EmptyButOpen_Waits() { Assert.That(V(4, 4, true, false, false), Is.EqualTo("Wait")); }
    [Test] public void EmptyButReserved_Waits() { Assert.That(V(4, 4, false, true, false), Is.EqualTo("Wait")); }
    [Test] public void EmptyButFrameAbove_Waits() { Assert.That(V(4, 4, false, false, true), Is.EqualTo("Wait")); }
    [Test] public void NotEmptyAndOpen_Keeps_NotWaits() { Assert.That(V(4, 2, true, false, false), Is.EqualTo("Keep")); }

    // the sequence in time: the last item leaves while the window is open; the window closes; a reservation runs out; the structure goes exactly once
    sealed class Sim
    {
        public int Chests = 4, Empty = 3; public bool InUse = true; public float ReservedUntil = 100f, Now; public bool Above; public int Removed;
        public readonly HashSet<int> Retry = new HashSet<int>();
        public void Check()
        {
            if (Removed > 0) return;                                  // already gone
            var v = DepotRules.Lifecycle(Chests, Empty, InUse, Now < ReservedUntil, Above);
            if (v == DepotRules.Verdict.Remove) Removed++;
            else if (v == DepotRules.Verdict.Wait) Retry.Add(1); else Retry.Remove(1);
        }
        public void Tick(float dt) { Now += dt; if (Retry.Count > 0) { Retry.Clear(); Check(); } }      // TrollDepot.Tick: everything waiting is checked again
    }

    [Test] public void LastItemTakenWhileOpen_GoesAfterTheWindowClosesAndTheReservationEnds()
    {
        var s = new Sim();
        s.Empty = 4; s.Check();                                       // Container.Save with the window open: waits
        using (Assert.EnterMultipleScope())
        {
            Assert.That(s.Removed, Is.EqualTo(0)); Assert.That(s.Retry.Count, Is.EqualTo(1));
        }
        s.InUse = false; s.Check();                                   // SetInUse(false): still reserved by a troll
        Assert.That(s.Removed, Is.EqualTo(0));
        for (int i = 0; i < 25; i++) s.Tick(5f);                      // the reservation (until t = 100 s) runs out while Tick keeps asking
        Assert.That(s.Removed, Is.EqualTo(1));
        for (int i = 0; i < 5; i++) { s.Tick(5f); s.Check(); }
        Assert.That(s.Removed, Is.EqualTo(1), "removal must not run twice");
    }

    [Test] public void ItemPutBackBeforeTheRetry_CancelsTheWait()
    {
        var s = new Sim { ReservedUntil = 0f, InUse = true, Empty = 4 };
        s.Check(); Assert.That(s.Retry.Count, Is.EqualTo(1));
        s.Empty = 3; s.InUse = false; s.Tick(5f);
        Assert.That(s.Removed, Is.EqualTo(0));
    }

    [Test] public void FrameAboveRemovedLater_ThenTheLowerOneGoes()
    {
        var s = new Sim { ReservedUntil = 0f, InUse = false, Empty = 4, Above = true };
        s.Check(); Assert.That(s.Removed, Is.EqualTo(0));
        s.Above = false; s.Tick(5f);
        Assert.That(s.Removed, Is.EqualTo(1));
    }
}
