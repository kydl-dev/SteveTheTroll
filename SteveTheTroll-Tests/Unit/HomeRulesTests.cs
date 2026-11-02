using NUnit.Framework;
using SteveTheTroll.Trolls.Home.Domain;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-171 P7: the game-free home rules (heal range D-085 / D-135, stand still while looked at D-077).</summary>
    [TestFixture]
    public class HomeRulesTests
    {
        [Test] public void HealRange() { Assert.That(HomeRules.InHealRange(30f, 40f, 2500f), Is.True); Assert.That(HomeRules.InHealRange(30f, 40.1f, 2500f), Is.False); }
        [Test] public void Holds_OnlyWhileLookedAtLately()
        {
            Assert.That(HomeRules.Holds(4f, false, false, 3f, true, 1f, 3f), Is.True);
            Assert.That(HomeRules.Holds(4f, false, false, 3f, true, 3.1f, 3f), Is.False, "grace over");
            Assert.That(HomeRules.Holds(4f, false, false, 3f, false, 0f, 3f), Is.False, "never looked at");
        }
        [Test] public void Holds_NotWhenOff_Following_Fighting_OrFar()
        {
            Assert.That(HomeRules.Holds(0f, false, false, 1f, true, 0f, 3f), Is.False);
            Assert.That(HomeRules.Holds(4f, true, false, 1f, true, 0f, 3f), Is.False);
            Assert.That(HomeRules.Holds(4f, false, true, 1f, true, 0f, 3f), Is.False);
            Assert.That(HomeRules.Holds(4f, false, false, 4.1f, true, 0f, 3f), Is.False);
        }
    }
}
