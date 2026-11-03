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

        [Test] public void PureLayersExist() { Assert.That(PureFiles().Count(), Is.GreaterThanOrEqualTo(25), "src/Core + Trolls/{Recovery,Farming,Orders}/{Domain,Application,Execution}"); }

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

        // the V1 guard (D-169): the marker the lab wrapper searches for, and the lab suite proves V2 before anything else runs
        [Test] public void V2Marker_AndTheLabTestThatRunsFirst()
        {
            Assert.That(File.ReadAllText(Path.Combine(Src, "Core", "Architecture.cs")), Does.Contain("Marker = \"STT-ARCH-V2\"").And.Contain("Version = 2"));
            string suite = File.ReadAllText(Path.Combine(TestPaths.SuiteDir, "suite.json"));
            var first = Regex.Match(suite, @"""tests""\s*:\s*\[\s*""([^""]+)""");
            Assert.That(first.Groups[1].Value, Is.EqualTo("architecture-v2"));
            Assert.That(File.Exists(Path.Combine(TestPaths.SuiteDir, "architecture-v2.json")), Is.True);
        }

        // D-170: every order command has exactly one handler (the dispatcher would answer Failed for a command without one)
        [Test] public void EveryOrderCommand_HasAHandler()
        {
            System.Type[] types;
            try { types = ModAssembly.Load().GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            var commands = types.Where(t => t.Namespace == "SteveTheTroll.Trolls.Orders.Commands" && t.Name.EndsWith("Command") && !t.IsInterface).ToList();
            Assert.That(commands.Count, Is.EqualTo(6));
            foreach (var c in commands)
            {
                int handlers = types.Count(h => h.GetInterfaces().Any(i => i.IsGenericType && i.Name.StartsWith("ICommandHandler") && i.GetGenericArguments()[0] == c));
                Assert.That(handlers, Is.EqualTo(1), c.Name);
            }
        }

        // D-171 (P9 audit): one definition of the game's private BaseAI.MoveTo delegate, and no order facade left (every caller sends a command)
        [Test] public void P9_OneMoveToDelegate_NoOrderFacade()
        {
            var all = Directory.GetFiles(Src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).ToList();
            Assert.That(all.Sum(t => Regex.Matches(t, @"AccessTools\.Method\(typeof\(BaseAI\), ""MoveTo""\)").Count), Is.EqualTo(1));
            Assert.That(all.Count(t => Regex.IsMatch(t, @"TrollFarming\.(Begin|BeginOn|SendHome|BeginUnload)\(")), Is.EqualTo(0));
        }

        // D-175: behaviour components walk / turn through the movement capability (TrollContext); no Step(...) takes the game's MoveTo / LookAt delegates as parameters any more
        [Test] public void D175_NoStepTakesMoveDelegates()
        {
            foreach (var f in Directory.GetFiles(Src, "*.cs", SearchOption.AllDirectories))
                Assert.That(Regex.IsMatch(File.ReadAllText(f), @"\bStep\([^)]*System\.(Func<BaseAI|Action<BaseAI)"), Is.False, Rel(f) + " still passes BaseAI move delegates into a Step");
        }

        static string Rel(string f) { return f.Substring(Src.Length + 1); }
    }
}
