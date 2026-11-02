using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SteveTheTroll.Tests.Integration
{
    /// <summary>
    /// D-169 Architecture V2 rules, checked on the sources: game-free layers (src/Core, every Domain / Application / Execution folder) reference no Unity, Valheim, Harmony,
    /// BepInEx or Jotunn; the refactor keeps the hard KydlCore dependency (user 2026-11-02: "not decoupling from KydlCore"); the V2 marker is present.
    /// </summary>
    [TestFixture]
    public class ArchitectureRulesTests
    {
        static readonly Regex ForbiddenUsing = new Regex(@"^\s*using\s+(UnityEngine|HarmonyLib|BepInEx|Jotunn|Kydl\.Common)\b", RegexOptions.Multiline);
        // game / engine types a pure layer must not name (word match, comments stripped first)
        static readonly Regex ForbiddenType = new Regex(@"\b(GameObject|Component|MonoBehaviour|Transform|Vector3|Quaternion|Character|Humanoid|MonsterAI|BaseAI|ZNetView|ZDO|ZNet|ZNetScene|Player|Time\.time|Physics|ModLog)\b");
        static readonly Regex LineComment = new Regex(@"//.*$", RegexOptions.Multiline);
        static readonly Regex StringLiteral = new Regex(@"""([^""\\]|\\.)*""");

        static string Src { get { return Path.Combine(TestPaths.ModDir, "src"); } }

        static IEnumerable<string> PureFiles()
        {
            var core = Path.Combine(Src, "Core");
            var files = Directory.Exists(core) ? Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories).ToList() : new List<string>();
            foreach (var dir in Directory.GetDirectories(Src, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(dir);
                if (name == "Domain" || name == "Application" || name == "Execution") files.AddRange(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories));
            }
            return files.Distinct();
        }

        [Test] public void PureLayersExist() { Assert.That(PureFiles().Count(), Is.GreaterThanOrEqualTo(15), "src/Core + Trolls/Recovery/{Domain,Application,Execution}"); }

        [Test] public void PureLayers_ReferenceNoGameOrEngine()
        {
            var bad = new List<string>();
            foreach (var f in PureFiles())
            {
                string text = File.ReadAllText(f);
                string code = StringLiteral.Replace(LineComment.Replace(text, ""), "\"\"");
                var u = ForbiddenUsing.Match(code);
                if (u.Success) bad.Add(Rel(f) + ": " + u.Value.Trim());
                var t = ForbiddenType.Match(code);
                if (t.Success) bad.Add(Rel(f) + ": names " + t.Value);
            }
            Assert.That(bad, Is.Empty, "D-169: the domain decides without the game - move game reads into the Infrastructure adapter");
        }

        [Test] public void KydlCore_StaysAHardDependency()
        {
            string proj = File.ReadAllText(Path.Combine(TestPaths.ModDir, "SteveTheTroll.csproj"));
            string plugin = File.ReadAllText(Path.Combine(Src, "Plugin.cs"));
            Assert.That(proj, Does.Contain("<KydlUseCore>true</KydlUseCore>"));
            Assert.That(plugin, Does.Contain("[BepInDependency(KydlCore.Plugin.ModGuid"));
            Assert.That(File.ReadAllText(Path.Combine(Src, "Ui", "HubTabs.cs")), Does.Contain("KyHub"));
        }

        static string Rel(string f) { return f.Substring(Src.Length + 1); }
    }
}
