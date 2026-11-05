using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Movement.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>D-179: which blocker a troll pushes through, how a feeler heading scores, how long a bad spot counts.</summary>
[TestFixture]
public class PathRulesTests
{
    [TestCase("Beech_small1", false, true)]
    [TestCase("Beech_small2", false, true)]
    [TestCase("Bush01", false, true)]
    [TestCase("FirTree_small", false, true)]
    [TestCase("beech_log", true, true)]
    [TestCase("Birch1", true, true)]
    [TestCase("Pinetree_01_Stub", false, true)]
    [TestCase("Beech1", false, false)]
    [TestCase("Oak1", false, false)]
    public void PushThrough_small_natural_things_only(string prefab, bool treeLog, bool expected)
    {
        Assert.That(PathRules.PushThrough(prefab, true, false, false, false, treeLog), Is.EqualTo(expected));
    }

    [Test]
    public void PushThrough_never_pieces_creatures_rocks_or_unbreakable_things()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PathRules.PushThrough("Beech_small1", false, false, false, false, false), Is.False, "not destructible");
            Assert.That(PathRules.PushThrough("wood_log_small", true, true, false, false, false), Is.False, "a piece");
            Assert.That(PathRules.PushThrough("Bush_small", true, false, true, false, false), Is.False, "a creature");
            Assert.That(PathRules.PushThrough("rock_small", true, false, false, true, false), Is.False, "a rock");
            Assert.That(PathRules.PushThrough("", true, false, false, false, true), Is.False, "no name");
        });
    }

    [Test]
    public void FeelerScore_prefers_free_runs_towards_the_goal()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PathRules.FeelerScore(10f, 0f, false), Is.GreaterThan(PathRules.FeelerScore(10f, 40f, false)), "straight beats an angle at the same free run");
            Assert.That(PathRules.FeelerScore(10f, 40f, false), Is.GreaterThan(PathRules.FeelerScore(4f, 0f, false)), "a long free angle beats a short straight run");
            Assert.That(PathRules.FeelerScore(2.9f, 0f, false), Is.EqualTo(float.MinValue), "under MinUsefulRun is useless");
            Assert.That(PathRules.FeelerScore(10f, 90f, false), Is.GreaterThan(PathRules.FeelerScore(2f, 0f, false)), "a free side heading beats a blocked straight one");
            Assert.That(PathRules.FeelerScore(25f, 0f, false), Is.EqualTo(PathRules.FeelerScore(PathRules.FeelerLength, 0f, false)), "capped at FeelerLength");
        });
    }

    [Test]
    public void FeelerScore_a_bad_spot_costs_the_penalty()
    {
        Assert.That(PathRules.FeelerScore(10f, 0f, false) - PathRules.FeelerScore(10f, 0f, true), Is.EqualTo(PathRules.BadSpotPenalty).Within(0.001f));
    }

    [Test]
    public void StillBad_for_BadSpotSeconds()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PathRules.StillBad(100f, 100f + PathRules.BadSpotSeconds - 0.1f), Is.True);
            Assert.That(PathRules.StillBad(100f, 100f + PathRules.BadSpotSeconds), Is.False);
        });
    }

    [Test]
    public void FeelerAngles_start_straight_and_are_symmetric()
    {
        float sum = 0f;
        foreach (float a in PathRules.FeelerAngles)
        {
            sum += a;
        }

        Assert.Multiple(() =>
        {
            Assert.That(PathRules.FeelerAngles[0], Is.EqualTo(0f));
            Assert.That(sum, Is.EqualTo(0f));
        });
    }
}