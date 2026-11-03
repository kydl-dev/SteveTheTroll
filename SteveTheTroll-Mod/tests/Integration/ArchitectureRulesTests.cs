using Kydl.Core.Commands;
using Kydl.Core.Features;
using Kydl.Core.Identity;
using Kydl.Core.Pipeline;
using Kydl.Core.Results;
using Kydl.Core.Time;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SteveTheTroll.Tests.Integration
{
    /// <summary>
    /// The KydlDev spec / Architecture V2 rules, checked on the sources (MIGRATION_DECISIONS A3-A9, B10): pure layers reference no game or
    /// engine, Harmony patches live only in src/Integrations/Harmony, KydlCore stays a hard dependency, exact network versions, the
    /// architecture marker the lab wrapper looks for; plus SteveTheTroll's Architecture V2 rules (D-169..D-175). Keep these green; a deliberate deviation is written in docs/Architecture.md AND here.
    /// </summary>
    [TestFixture]
    public class ArchitectureRulesTests
    {
        static readonly Regex ForbiddenUsing = new Regex(@"^\s*using\s+(UnityEngine|HarmonyLib|BepInEx|Jotunn|Kydl\.Common|KydlCore)\b", RegexOptions.Multiline);
        static readonly Regex ForbiddenType = new Regex(@"\b(GameObject|Component|MonoBehaviour|Transform|Vector3|Quaternion|Character|Humanoid|MonsterAI|BaseAI|ZNetView|ZDO|ZNet|ZNetScene|Player|Time\.time|Physics|ModLog|KyHub)\b");
        static readonly Regex LineComment = new Regex(@"//.*$", RegexOptions.Multiline);
        static readonly Regex StringLiteral = new Regex(@"""([^""\\]|\\.)*""");

        static string Src { get { return TestPaths.Src; } }
        static string SharedCore { get { return Path.GetFullPath(Path.Combine(TestPaths.ProjectRoot, "..", "Claude", "shared", "Kydl.Core")); } }

        static IEnumerable<string> PureFiles()
        {
            var files = new List<string>();
            var core = Path.Combine(Src, "Core");
            if (Directory.Exists(core)) files.AddRange(Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories));
            foreach (var dir in Directory.GetDirectories(Path.Combine(Src, "Features"), "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(dir);
                if (name == "Domain" || name == "Application" || name == "Execution") files.AddRange(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories));
            }
            return files.Distinct();
        }

        static List<string> Violations(IEnumerable<string> files)
        {
            var bad = new List<string>();
            foreach (var f in files)
            {
                string code = StringLiteral.Replace(LineComment.Replace(File.ReadAllText(f), ""), "\"\"");
                var u = ForbiddenUsing.Match(code);
                if (u.Success) bad.Add(Path.GetFileName(f) + ": " + u.Value.Trim());
                var t = ForbiddenType.Match(code);
                if (t.Success) bad.Add(Path.GetFileName(f) + ": names " + t.Value);
            }
            return bad;
        }

        [Test] public void PureLayersExist() { Assert.That(PureFiles().Count(), Is.GreaterThanOrEqualTo(25), "src/Core + Features/**/{Domain,Application,Execution}"); }

        [Test] public void PureLayers_ReferenceNoGameOrEngine()
        {
            Assert.That(PureFiles(), Is.Not.Empty, "src/Features/<F>/{Domain,Application,Execution}");
            Assert.That(Violations(PureFiles()), Is.Empty, "the domain decides without the game - move game reads into the feature's Infrastructure adapter");
        }

        [Test] public void KydlCoreContracts_ArePure()
        {
            Assert.That(Directory.Exists(SharedCore), Is.True, SharedCore);
            Assert.That(Violations(Directory.GetFiles(SharedCore, "*.cs", SearchOption.AllDirectories)), Is.Empty, "Claude\\shared\\Kydl.Core must stay pure");
        }

        [Test] public void HarmonyPatches_LiveOnlyIn_IntegrationsHarmony()
        {
            var harmonyDir = Path.Combine(Src, "Integrations", "Harmony") + Path.DirectorySeparatorChar;
            var outside = Directory.GetFiles(Src, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.StartsWith(harmonyDir) && Regex.IsMatch(LineComment.Replace(File.ReadAllText(f), ""), @"\[HarmonyPatch\b|\.Patch\("))
                .Select(Path.GetFileName).ToList();
            Assert.That(outside, Is.Empty, "MIGRATION_DECISIONS A5: Harmony patches go into src/Integrations/Harmony");
        }

        [Test] public void Plugin_HardKydlCore_ExactNetworkVersion()
        {
            string plugin = File.ReadAllText(Path.Combine(Src, "Bootstrap", "Plugin.cs"));
            string proj = File.ReadAllText(Directory.GetFiles(TestPaths.ModDir, "*.csproj").Single());
            Assert.That(proj, Does.Contain("<KydlUseCore>true</KydlUseCore>"));
            Assert.That(plugin, Does.Contain("[BepInDependency(KydlCore.Plugin.ModGuid"));
            Assert.That(Regex.IsMatch(plugin, @"\[NetworkCompatibility\(CompatibilityLevel\.(EveryoneMustHaveMod|VersionCheckOnly), VersionStrictness\.Patch\)\]"), Is.True,
                "MIGRATION_DECISIONS: exact versions (Patch); EveryoneMustHaveMod for shared-world mods, VersionCheckOnly for client-only mods");
        }

        [Test] public void EveryFeature_HasAnAdapterImplementingIFeature()
        {
            foreach (var feature in Directory.GetDirectories(Path.Combine(Src, "Features")))
            {
                var infra = Path.Combine(feature, "Infrastructure");
                bool has = Directory.Exists(infra) && Directory.GetFiles(infra, "*.cs", SearchOption.AllDirectories).Any(f => Regex.IsMatch(File.ReadAllText(f), @":\s*IFeature\b"));
                Assert.That(has, Is.True, Path.GetFileName(feature) + ": Infrastructure/<X>Feature.cs implementing IFeature (MIGRATION_DECISIONS A4)");
            }
        }

        [Test] public void ArchitectureMarker_IsPresent()
        {
            Assert.That(File.ReadAllText(Path.Combine(Src, "Core", "Architecture.cs")), Does.Contain("Tag = \"SteveTheTroll-KYDL-SPEC-V1\""));
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
