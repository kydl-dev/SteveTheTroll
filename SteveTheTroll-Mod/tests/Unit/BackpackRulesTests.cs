using System.Collections.Generic;
using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Backpack.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>D-171 P6: the game-free backpack rules (full = no chest with an empty slot; who collects; pickup range).</summary>
[TestFixture]
public class BackpackRulesTests
{
    [Test] public void Full_WhenNoChestHasRoom() { Assert.That(BackpackRules.Full(4, i => false), Is.True); }
    [Test] public void NotFull_WhenAnyChestHasRoom() { Assert.That(BackpackRules.Full(4, i => i == 3), Is.False); }
    [Test] public void Full_StopsAtTheFirstChestWithRoom()
    {
        var read = new List<int>();
        BackpackRules.Full(4, i => { read.Add(i); return i == 1; });
        using (Assert.EnterMultipleScope())
        {
            Assert.That(read[0], Is.EqualTo(0));
            Assert.That(read.Contains(2), Is.False, "chests after the first one with room are not read");
        }
    }
    [Test] public void Full_NoReader_NotFull() { Assert.That(BackpackRules.Full(4, null), Is.False); }
    [Test] public void Collects_OnlyOwnerNotBusyNotFull()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BackpackRules.Collects(true, false, false), Is.True);
            Assert.That(BackpackRules.Collects(false, false, false), Is.False);
            Assert.That(BackpackRules.Collects(true, true, false), Is.False);
            Assert.That(BackpackRules.Collects(true, false, true), Is.False);
        }
    }
    [Test] public void InRange() {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BackpackRules.InRange(2500f, 2500f), Is.True); Assert.That(BackpackRules.InRange(2500.1f, 2500f), Is.False);
        }
    }
}
