using SteveTheTroll.Features.Hubs.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>User, 2026-11-04: the Troll Hub shows in the build menu only after the costume set bonus has been active once.</summary>
[TestFixture]
public class HubUnlockRulesTests
{
    private const string Hub = "STT_TrollHub";

    [TestCase("STT_TrollHub", true)]
    [TestCase("STT_TrollHub(Clone)", true)]
    [TestCase("piece_workbench", false)]
    [TestCase("STT_TrollSlippers", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void IsHub_OnlyTheHubPiece(string name, bool want)
    {
        Assert.That(HubUnlockRules.IsHub(name, Hub), Is.EqualTo(want));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void KnownAnswer_HubHiddenBeforeTheSetBonus_EvenWhenTheGameWouldShowIt(bool game)
    {
        Assert.That(HubUnlockRules.KnownAnswer(game, true, true, false), Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void KnownAnswer_HubShownAfterTheSetBonus_EvenWhenTheGameWouldHideIt(bool game)
    {
        Assert.That(HubUnlockRules.KnownAnswer(game, true, true, true), Is.True);
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void KnownAnswer_OtherPiecesKeepTheGamesAnswer(bool game, bool unlocked)
    {
        Assert.That(HubUnlockRules.KnownAnswer(game, false, true, unlocked), Is.EqualTo(game));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void KnownAnswer_OtherRequirementModesAreNotTouched(bool game)
    {
        Assert.That(HubUnlockRules.KnownAnswer(game, true, false, false), Is.EqualTo(game));
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, true)]
    [TestCase(true, true, false)]
    [TestCase(true, false, false)]
    public void ShouldUnlock_OnlyTheFirstTimeTheSetBonusIsActive(bool already, bool active, bool want)
    {
        Assert.That(HubUnlockRules.ShouldUnlock(already, active), Is.EqualTo(want));
    }
}
