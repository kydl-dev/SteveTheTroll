using NUnit.Framework;

namespace SteveTheTroll.Tests.Unit;

/// <summary>D-153: farming trolls leave saplings and bushes alone (meadows / black forest), but not the yggdrasil shoot.</summary>
[TestFixture]
public class YoungGrowthTests
{
    static bool Young(string name) { return (bool)ModAssembly.Call("SteveTheTroll.Features.Trolls.Taming.Domain.TamingRules", "IsYoungGrowth", name); }

    [TestCase("Bush01")] [TestCase("Bush01_heath")] [TestCase("Bush02_en")] [TestCase("shrub_2")] [TestCase("shrub_2_heath")]
    [TestCase("Beech_small1")] [TestCase("Beech_small2")] [TestCase("FirTree_small")] [TestCase("FirTree_small_dead")] [TestCase("Birch_sapling")] [TestCase("bush01")]
    public void YoungGrowth_IsSkipped(string name) { Assert.That(Young(name), Is.True); }

    [TestCase("Beech1")] [TestCase("Oak1")] [TestCase("Pinetree_01")] [TestCase("FirTree")] [TestCase("Birch1")] [TestCase("beech_log")] [TestCase("SwampTree1")]
    [TestCase("YggaShoot1")] [TestCase("YggaShoot_small1")] [TestCase("rock4_copper")] [TestCase("MineRock_Tin")]
    public void FullTreesLogsRocksAndShoots_AreKept(string name) { Assert.That(Young(name), Is.False); }

    [TestCase(null)] [TestCase("")]
    public void Blank_IsNotYoung(string name) { Assert.That(Young(name), Is.False); }
}
