using NUnit.Framework;
using System.IO;
using System.Runtime.CompilerServices;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>
    /// T-098 (f): the pages that are DESCRIBED to KydlCore (KyPage) say what to show and never how it looks. Scans their sources: no font size, colour or kit label in them.
    /// (HubTab.Build is cut out of HubTab.cs below: its file also holds sign-editor helpers.)
    /// </summary>
    [TestFixture]
    public class PageSourcesTests
    {
        static string Src([CallerFilePath] string here = "") { return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", "..", "SteveTheTroll-Mod", "src", "Ui")); }

        [TestCase("TrollsKyPage.cs")]
        [TestCase("TrollListKyPages.cs")]
        [TestCase("ModelKyPages.cs")]
        public void DescribedPage_SetsNoLook(string file)
        {
            string text = File.ReadAllText(Path.Combine(Src(), file));
            foreach (var word in new[] { "fontSize", "FontSize", "UiKit.Label", "style.color", "UiKit.Text", "SmallSize", "NameSize" })
                Assert.That(text, Does.Not.Contain(word), file + " must not set how it looks (" + word + ")");
        }

        [Test]
        public void HubPage_SetsNoLook()
        {
            string text = File.ReadAllText(Path.Combine(Src(), "HubTab.cs"));
            string build = text.Substring(text.IndexOf("internal static KyPage Build()"), text.IndexOf("static string Styled") - text.IndexOf("internal static KyPage Build()"));
            foreach (var word in new[] { "fontSize", "FontSize", "UiKit.", "style." })
                Assert.That(build, Does.Not.Contain(word), "HubTab.Build must not set how it looks (" + word + ")");
        }
    }
}
