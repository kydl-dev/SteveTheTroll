using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SteveTheTroll.Tests.Integration;

/// <summary>
/// D-100 (user: "we have to make sure it all works as it should"): SteveTheTroll's own content rules, checked on the source and the built
/// DLL without the game. Texts: every text key the code uses exists in UiText.En and every text with {n} formats. Bundle: the code
/// loads assets only through BundleAssets, every name there is in the bundle's manifest, and the bundle shipped in the mod is the one
/// that manifest describes. (World_BundleAssets / World_Texts check the same in the running game.)
/// </summary>
[TestFixture]
public class ModContentTests
{
    static readonly string Src = Path.Combine(TestPaths.ModDir, "src");
    static readonly string UiRuntime = Path.Combine(TestPaths.ProjectRoot, "SteveTheTroll-Unity", "Assets", "SteveTheTroll_UI", "Runtime");
    static readonly string BundleDir = Path.Combine(TestPaths.ProjectRoot, "SteveTheTroll-Unity", "AssetBundles");
    const string Bundle = "stevethetroll";

    static IEnumerable<string> CodeFiles()
    {
        foreach (var d in new[] { Src, UiRuntime })
            foreach (var f in Directory.GetFiles(d, "*.cs", SearchOption.AllDirectories)) yield return f;
    }

    static Dictionary<string, string> Texts()
    {
        var r = new Dictionary<string, string>();
        // UiText = menus and items; HubText = the Troll Hub and its sign menu (D-135)
        foreach (var type in new[] { "SteveTheTroll.Ui.UiText", "SteveTheTroll.Ui.HubText" })
            foreach (DictionaryEntry e in (IDictionary)ModAssembly.GetStatic(type, "En")) r[(string)e.Key] = (string)e.Value;
        return r;
    }

    // "stevethetroll_xyz" or "$stevethetroll_xyz" as a whole string literal; literals ending in "_" are prefixes that code completes.
    static readonly Regex KeyLiteral = new Regex("\"\\$?(stevethetroll_[a-z0-9_]*[a-z0-9])\"");

    // ids that look like text keys but are not texts: the set id (CostumeItems.SetName), saved-data keys (TrollRegistry.DataKey,
    // TrollTamingPatch.FirstTameKey), the Troll Hub icon's sprite name (T-200, EmbeddedImages: the resource name without .png). A new one goes here only
    // with a comment saying what it is.
    static readonly HashSet<string> NotTexts = new HashSet<string> { "stevethetroll_set", "stevethetroll_trolls", "stevethetroll_first_tame", "stevethetroll_hub_icon" };

    static SortedSet<string> UsedKeys(out Dictionary<string, string> where)
    {
        var used = new SortedSet<string>();
        where = new Dictionary<string, string>();
        foreach (var f in CodeFiles())
        {
            if (Path.GetFileName(f) == "UiText.cs" || Path.GetFileName(f) == "HubText.cs") continue;
            foreach (Match m in KeyLiteral.Matches(File.ReadAllText(f)))
            {
                if (NotTexts.Contains(m.Groups[1].Value)) continue;
                used.Add(m.Groups[1].Value);
                if (!where.ContainsKey(m.Groups[1].Value)) where[m.Groups[1].Value] = Path.GetFileName(f);
            }
        }
        return used;
    }

    [Test]
    public void Texts_EveryKeyTheCodeUses_HasAnEnglishText()
    {
        var texts = Texts();
        Dictionary<string, string> where;
        var used = UsedKeys(out where);
        Assert.That(used, Has.Count.GreaterThan(50), "the scan found the code's text keys");
        var missing = used.Where(k => !texts.ContainsKey(k)).Select(k => k + " (" + where[k] + ")").ToList();
        Assert.That(missing, Is.Empty, "text keys used in code but missing in UiText.En - the player would see the raw key");
    }

    [Test]
    public void Texts_AreNotEmpty_AndFormatWithArguments()
    {
        foreach (var kv in Texts())
        {
            Assert.That(kv.Value, Is.Not.Null.And.Not.Empty, kv.Key);
            Assert.DoesNotThrow(() => string.Format(kv.Value, "a", "b", "c", "d", "e"), kv.Key + ": '" + kv.Value + "' is not a valid format text");
        }
    }

    [Test]
    public void Texts_Unused_AreReported()
    {
        Dictionary<string, string> where;
        var used = UsedKeys(out where);
        var all = new HashSet<string>();
        foreach (var f in CodeFiles()) if (Path.GetFileName(f) != "UiText.cs" && Path.GetFileName(f) != "HubText.cs") all.UnionWith(Regex.Matches(File.ReadAllText(f), "stevethetroll_[a-z0-9_]+").Cast<Match>().Select(m => m.Value));
        // item descriptions are built as token + "_desc" (Jotunn ItemConfig)
        var unused = Texts().Keys.Where(k => !used.Contains(k) && !all.Any(p => p.EndsWith("_") && k.StartsWith(p))
            && !(k.EndsWith("_desc") && used.Contains(k.Substring(0, k.Length - 5)))).ToList();
        if (unused.Count > 0) Assert.Warn("texts no code uses (dead or built from a prefix): " + string.Join(", ", unused));
    }

    static List<string> BundleNames()
    {
        var t = ModAssembly.Type("SteveTheTroll.Integrations.Unity.BundleAssets");
        return t.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()).ToList();
    }

    static List<string> ManifestAssets()
    {
        var lines = File.ReadAllLines(Path.Combine(BundleDir, Bundle + ".manifest"));
        int at = Array.IndexOf(lines, "Assets:");
        Assert.That(at, Is.GreaterThanOrEqualTo(0), "manifest has an Assets list");
        var list = new List<string>();
        for (int i = at + 1; i < lines.Length && lines[i].StartsWith("- "); i++) list.Add(lines[i].Substring(2).Trim());
        return list;
    }

    [Test]
    public void Bundle_CodeLoadsOnlyThroughBundleAssets()
    {
        var direct = CodeFiles().Where(f => Path.GetFileName(f) != "BundleAssets.cs" && f.StartsWith(Src) && File.ReadAllText(f).Contains("LoadAsset<")).Select(Path.GetFileName).ToList();
        Assert.That(direct, Is.Empty, "LoadAsset outside BundleAssets: its name would not be checked against the bundle");
    }

    [Test]
    public void Bundle_EveryNameTheCodeLoads_IsInTheBundle()
    {
        var names = BundleNames();
        Assert.That(names, Has.Count.GreaterThanOrEqualTo(15));
        var files = ManifestAssets().Select(p => Path.GetFileNameWithoutExtension(p)).ToList();
        var missing = names.Where(n => !files.Contains(n)).ToList();
        Assert.That(missing, Is.Empty, "asset names the mod loads but the bundle does not have (rebuild the bundle: Valheim > Build AssetBundles)");
        var twice = names.Where(n => files.Count(f => string.Equals(f, n, StringComparison.OrdinalIgnoreCase)) > 1).ToList();
        Assert.That(twice, Is.Empty, "names that match two bundle assets (LoadAsset by name could pick the wrong one)");
    }

    [Test]
    public void Bundle_ShippedInTheMod_IsTheOneTheManifestDescribes()
    {
        var built = Path.Combine(BundleDir, Bundle);
        var shipped = Path.Combine(TestPaths.ModDir, "Assets", Bundle);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(built), Is.True, built);
            Assert.That(File.Exists(shipped), Is.True, shipped + " (copied by Valheim > Build AssetBundles (and copy to Mod))");
            Assert.That(File.ReadAllBytes(shipped), Is.EqualTo(File.ReadAllBytes(built)), "the mod ships an older / other bundle than the Unity build");
        }
    }

    [Test]
    public void Bundle_Prefabs_ReferencedByTheCode_AreUsed()
    {
        // every BundleAssets name is used somewhere in the code (a name nobody loads is a leftover)
        var code = string.Join("\n", CodeFiles().Where(f => Path.GetFileName(f) != "BundleAssets.cs").Select(File.ReadAllText));
        var fields = ModAssembly.Type("SteveTheTroll.Integrations.Unity.BundleAssets").GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Where(f => f.IsLiteral).Select(f => f.Name);
        var unused = fields.Where(n => !code.Contains("BundleAssets." + n)).ToList();
        Assert.That(unused, Is.Empty, "BundleAssets names no code loads");
    }
}
