
using SteveTheTroll;

namespace SteveTheTroll.Tests.Unit;

/// <summary>Which functionalities are on (PROJECT_RULES 11): the pure rule in Functions.</summary>
[TestFixture]
public class FunctionsTests
{
    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, false)]
    public void IsOn_NeedsEnableAllAndTheOwnSwitch(bool all, bool own, bool want)
    {
        Assert.That(SteveTheTroll.Configuration.Functions.IsOn(all, own), Is.EqualTo(want));
    }
}
