using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SteveTheTroll.Tests.Integration
{
    /// <summary>
    /// D-172 (user 2026-11-02: "spawn trolls further from me (not behind me) so that they don't walk into player. Also, adjust all tests so that they take this extra distance in
    /// consideration"): every stt.spawnTroll in the TestLab suite is in front of the player (dz &gt; 0, stt.reset faces world +z) and at least 16 m away - exactly where the hook
    /// puts it, so no test depends on a silent push-out. stt.spawnTrollNear is only for the tests about a troll close to the player.
    /// </summary>
    [TestFixture]
    public class LabSuiteGeometryTests
    {
        const float MinFromPlayer = 16f;
        static readonly string[] NearAllowed = { "defend-rage", "hold-look", "nohit-zone" };
        static readonly Regex Spawn = new Regex(@"""name""\s*:\s*""(stt\.spawnTroll(?:Near)?)""\s*,\s*""args""\s*:\s*\{(?<args>[^}]*)\}");
        static readonly Regex Dx = new Regex(@"""dx""\s*:\s*(-?[0-9.]+)"), Dz = new Regex(@"""dz""\s*:\s*(-?[0-9.]+)");

        static string SuiteDir { get { return Path.Combine(TestPaths.ProjectRoot, "SteveTheTroll-Tests", "TestLab", "suites", "SteveTheTroll"); } }

        [Test] public void EveryTrollSpawn_IsInFront_AndFarEnough()
        {
            int seen = 0;
            foreach (var file in Directory.GetFiles(SuiteDir, "*.json"))
            {
                string test = Path.GetFileNameWithoutExtension(file);
                foreach (Match m in Spawn.Matches(File.ReadAllText(file)))
                {
                    seen++;
                    string args = m.Groups["args"].Value;
                    float dx = float.Parse(Dx.Match(args).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    float dz = float.Parse(Dz.Match(args).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    Assert.That(dz, Is.GreaterThan(0f), test + ": troll (" + dx + ", " + dz + ") is not in front of the player");
                    if (m.Groups[1].Value == "stt.spawnTroll")
                        Assert.That(System.Math.Sqrt(dx * dx + dz * dz), Is.GreaterThanOrEqualTo(MinFromPlayer - 0.01), test + ": troll (" + dx + ", " + dz + ") closer than " + MinFromPlayer + " m - the hook would push it out and move the test's geometry");
                    else
                        Assert.That(NearAllowed.Contains(test), Is.True, test + ": stt.spawnTrollNear is only for tests about a troll close to the player");
                }
            }
            Assert.That(seen, Is.GreaterThan(20), "the suite spawns trolls in many tests");
        }

        // D-174: every test in suite.json has its file and every test file is in the suite (a forgotten file never runs); renamed hooks are gone from the suite
        [Test] public void Suite_ListsEveryTestFile_AndOnlyThose()
        {
            string suite = File.ReadAllText(Path.Combine(SuiteDir, "suite.json"));
            var listed = Regex.Matches(Regex.Match(suite, @"""tests""\s*:\s*\[(?<t>[^\]]*)\]").Groups["t"].Value, @"""([^""]+)""").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            var files = Directory.GetFiles(SuiteDir, "*.json").Select(Path.GetFileNameWithoutExtension).Where(n => n != "suite").ToList();
            Assert.That(listed, Is.Unique);
            Assert.That(files.Except(listed), Is.Empty, "test files not in suite.json");
            Assert.That(listed.Except(files), Is.Empty, "suite.json names without a file");
        }

        // D-175 (DrakesTestLab authoring guide: "noErrors ... in every test"): every test asserts that SteveTheTroll logged no error
        [Test] public void EveryTest_AssertsNoErrors()
        {
            foreach (var f in Directory.GetFiles(SuiteDir, "*.json").Where(x => Path.GetFileName(x) != "suite.json"))
                Assert.That(Regex.IsMatch(File.ReadAllText(f), @"""check""\s*:\s*""noErrors""\s*,\s*""source""\s*:\s*""SteveTheTroll"""), Is.True, Path.GetFileName(f) + " has no noErrors assert for SteveTheTroll");
        }

        [Test] public void Suite_UsesNoRenamedHooks()
        {
            foreach (var f in Directory.GetFiles(SuiteDir, "*.json"))
                Assert.That(File.ReadAllText(f), Does.Not.Contain("\"stt.unloading\""), Path.GetFileName(f) + ": stt.unloading was renamed stt.returningToUnload (D-174)");
        }

        [Test] public void TheHook_UsesTheSameMinimum()
        {
            string hooks = File.ReadAllText(Path.Combine(TestPaths.ProjectRoot, "SteveTheTroll-Mod", "src", "Testing", "TestHooks.cs"));
            Assert.That(hooks, Does.Contain("const float MinFromPlayer = 16f;"));
            Assert.That(hooks, Does.Contain("is not in front of the player"));
        }
    }
}
