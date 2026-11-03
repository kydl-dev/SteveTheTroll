using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Jotunn.Utils;
using Kydl.Common;
using SteveTheTroll.Backpack.Infrastructure;
using SteveTheTroll.Hubs.Infrastructure;
using SteveTheTroll.Items.Costume;
using SteveTheTroll.Items.Slippers;
using SteveTheTroll.Testing;
using SteveTheTroll.Trolls.Combat.Infrastructure;
using SteveTheTroll.Trolls.Home.Infrastructure;
using SteveTheTroll.Trolls.Orders.Infrastructure;
using SteveTheTroll.Trolls.Registry.Infrastructure;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using SteveTheTroll.Ui;
using SteveTheTroll.World.Zones;
using UnityEngine;

namespace SteveTheTroll
{
    /// <summary>
    /// SteveTheTroll: Steve the Troll plush slippers (utility item, troll powers) and later a fluffy troll costume (_agent/PLAN.md).
    /// Entry point. Order in Awake matters: log first, then the config (player settings), then text, then everything that uses them.
    /// </summary>
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(KydlCore.Plugin.ModGuid, "0.1.0")]
    // shared world state (tamed trolls, ZDOs, items, pieces, RPCs): everyone needs the mod, the exact same version (MIGRATION_DECISIONS network rule)
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    public class Plugin : BaseUnityPlugin
    {
        // ModGuid never changes after the first release. ModVersion = manifest.json version_number = newest CHANGELOG heading (tests check it).
        public const string ModGuid = "com.kydldev.SteveTheTroll";
        public const string ModName = "SteveTheTroll";
        public const string ModVersion = "2.1.0";
        public const string BundleName = "stevethetroll";

        internal static Plugin Instance;
        /// This mod's AssetBundle (null while the mod has none yet: Unity has not built one).
        internal static AssetBundle Bundle;
        ConfigReloader _cfgWatcher;
        Bootstrap.ModInstaller _installer;

        void Awake()
        {
            Instance = this;
            ModLog.Init(Logger, ModName, () => ModSettings.DebugLog != null && ModSettings.DebugLog.Value);
            ModLog.Info(ModName + " " + ModVersion + " loaded (architecture V" + Core.Architecture.Version + " " + Core.Architecture.Marker + ", dll built " + File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location).ToString("yyyy-MM-dd HH:mm:ss") + ")");

            // the UI kit, the input lock and the hub come from KydlCore (it logs through its own log)

            ModSettings.Bind(Config);
            _cfgWatcher = new ConfigReloader(Config);
            Loc.Register(UiText.En);
            Loc.Register(HubText.En);               // D-135: Troll Hub + its sign menu

            // PROJECT_RULES 11: the prefabs of a part that is off at start are not added at all (switching it ON later needs a restart); the features
            // (Bootstrap.ModInstaller) switch their tabs, KY-Items entries and ticks live
            Bundle = LoadBundle();
            bool slippers = ModSettings.SlippersOn && SlipperItem.Install();      // T-007: needs the bundle (icon) and the vanilla prefabs (added later by Jotunn)
            bool costume = ModSettings.CostumeOn && CostumeItems.Install();       // T-009d: costume helmet / chest / legs + Troll Club (bundle parts, vanilla prefabs)
            bool trolls = false, hubs = false;
            if (ModSettings.TrollsOn)
            {
                DepotPrefab.Install();     // D-064: troll depot frame prefab (vanilla prefabs)
                DepotGround.Install();     // D-103: own level-ground terrain op for the depot spots
                TrollTaming.Install();       // T-008a: the game's Troll becomes tameable (vanilla prefabs, added later by Jotunn)
                trolls = true;
                if (ModSettings.HubsOn) { HubPrefab.Install(); hubs = true; }   // D-135: the Troll Hub piece (needs the level op above)
            }
            ModLog.Verbose("Plugin: set up slippers " + slippers + ", costume " + costume + ", trolls " + trolls + ", hubs " + hubs);
            new Harmony(ModGuid).PatchAll(Assembly.GetExecutingAssembly());      // the patches are declared in src/Integrations/Harmony (MIGRATION_DECISIONS A5)
            _installer = new Bootstrap.ModInstaller(Config);
            bool installed = _installer.Install(slippers, costume, trolls, hubs);  // the KY hub page (F6) + the four features
            Config.SettingChanged += (s, e) => { if (_installer != null) _installer.Refresh(); };
            ModLog.Verbose("Plugin: features " + (installed ? "started" : "had failures") + " (architecture " + Core.Architecture.Tag + ")");
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new BuildDumpCommand());   // T-008b: stt_dumpbuild (backpack model)
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Testing.ClearCommand());        // D-093: stt_clear (clean test area)
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Testing.ClearDepotsCommand());  // D-104: stt_cleardepots (start clean)
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Testing.LevelGroundCommand());   // D-105: stt_levelground (repair)
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Ui.ComingPreviewCommand());     // D-058 preview: stt_coming [name]
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new FillBackpackCommand());  // D-064 test: stt_fillbackpack [item]
            ModLog.Verbose("Plugin: console commands stt_dumpbuild, stt_clear, stt_cleardepots, stt_levelground, stt_coming, stt_fillbackpack registered");
            ModLog.Verbose("Plugin: Awake finished (Harmony patches applied)");
        }

        // 1) embedded in the DLL (Kydl.Mod.targets embeds Assets\<bundle>; read fully into memory: LoadFromStream breaks once the stream
        //    is disposed), 2) the file next to the DLL (the Gale test profile gets both). No bundle at all is normal for a new mod.
        static AssetBundle LoadBundle()
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var res in asm.GetManifestResourceNames())
            {
                if (res != BundleName && !res.EndsWith("." + BundleName)) continue;
                using (var stream = asm.GetManifestResourceStream(res))
                using (var ms = new MemoryStream())
                {
                    if (stream != null) stream.CopyTo(ms);
                    var b = AssetBundle.LoadFromMemory(ms.ToArray());
                    if (b != null) { ModLog.Verbose("Plugin: bundle '" + BundleName + "' loaded from the DLL (" + b.GetAllAssetNames().Length + " asset(s))"); return b; }
                    ModLog.Warn("Plugin: the embedded bundle '" + res + "' could not be loaded");
                }
            }
            var path = Path.Combine(Path.GetDirectoryName(asm.Location) ?? "", BundleName);
            if (!File.Exists(path)) { ModLog.Verbose("Plugin: no bundle '" + BundleName + "' (not embedded, not at " + path + ") - fine until the mod has assets"); return null; }
            var fromFile = AssetBundle.LoadFromFile(path);
            if (fromFile == null) ModLog.Warn("Plugin: bundle file " + path + " could not be loaded");
            else ModLog.Verbose("Plugin: bundle loaded from " + path);
            return fromFile;
        }

        // D-085 (TEST-037: one exception in TrollZones.Tick, every second, also stopped every tick after it): each tick runs on its own; a
        // failing tick is logged (once per name, then every 60 s) and the others still run.
        static readonly System.Collections.Generic.Dictionary<string, float> TickErrorAt = new System.Collections.Generic.Dictionary<string, float>();
        // D-139 (user 23:39: "we need to fix the performance hits"): what each per-frame tick of this mod costs, so a hitch can be pinned on it or cleared of it.
        // Every 5 s (debug log): the total ms the ticks took, the most expensive tick and its worst single call; a single call over SlowTickMs is logged at once.
        const double SlowTickMs = 15.0;
        static readonly System.Collections.Generic.Dictionary<string, double> TickTotal = new System.Collections.Generic.Dictionary<string, double>();
        static readonly System.Collections.Generic.Dictionary<string, double> TickWorst = new System.Collections.Generic.Dictionary<string, double>();
        static readonly System.Diagnostics.Stopwatch TickClock = new System.Diagnostics.Stopwatch();
        static float _nextTickStats;

        static void Meter(string name, double ms)
        {
            double t, w;
            TickTotal.TryGetValue(name, out t); TickWorst.TryGetValue(name, out w);
            TickTotal[name] = t + ms; if (ms > w) TickWorst[name] = ms;
            if (ms > SlowTickMs) ModLog.Warn("Plugin: tick " + name + " took " + ms.ToString("F1") + " ms in one frame");
            if (Time.time < _nextTickStats || !ModLog.DebugOn) return;
            _nextTickStats = Time.time + 5f;
            var sb = new System.Text.StringBuilder();
            double all = 0;
            foreach (var kv in TickTotal) all += kv.Value;
            foreach (var kv in TickTotal) if (kv.Value >= all * 0.1 || TickWorst[kv.Key] > 5.0) sb.Append(' ').Append(kv.Key).Append(' ').Append(kv.Value.ToString("F1")).Append(" ms (worst ").Append(TickWorst[kv.Key].ToString("F1")).Append(')');
            ModLog.Verbose("Plugin: ticks of the last 5 s took " + all.ToString("F1") + " ms in total;" + sb);
            TickTotal.Clear(); TickWorst.Clear();
        }

        static void Run(string name, System.Action tick)
        {
            TickClock.Reset(); TickClock.Start();
            try { tick(); TickClock.Stop(); Meter(name, TickClock.Elapsed.TotalMilliseconds); }
            catch (System.Exception e)
            {
                TickClock.Stop();
                float last;
                if (TickErrorAt.TryGetValue(name, out last) && Time.time - last < 60f) return;
                TickErrorAt[name] = Time.time;
                ModLog.Error("Plugin: " + name + " failed: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
        }

        void Update()
        {
            if (_cfgWatcher != null) _cfgWatcher.Tick();
            if (!ModSettings.On) return;
            if (!Trolls.TrollsFeature.Running) return;       // every tick below belongs to the trolls feature (taming, farming, backpack, orders, map pins, hubs)
            Run("TrollRegistry", () => TrollRegistry.Tick());
            Run("TrollZones", () => TrollZones.Tick());               // D-084: zones around far own trolls stay loaded (they keep farming)
            Run("TrollCensus", () => TrollCensus.Tick());             // D-085: trolls within 500 m of the bed (spawn cap)
            Run("TrollMapPins", () => Ui.TrollMapPins.Tick());               // D-081: a map pin (costume head icon) per own tamed troll
            Run("TrollBedHeal", () => TrollBedHeal.Tick());           // D-085: +50 HP/s within 50 m of the bed
            Run("TrollDepot", () => TrollDepot.Tick());              // D-159: retry removing empty depots
            Run("RuntimeHealth", () => Testing.RuntimeHealth.Tick());          // D-160: read-only health report (full backpacks, hub leash, stale hubs, stuck depots)
            Run("BackpackVisual", () => BackpackVisual.Tick());
            Run("BackpackPickup", () => BackpackPickup.Tick());     // T-008c: wood / ores / scrap within 50 m into the backpack
            Run("TrollOrders", () => TrollOrders.Tick());             // D-065: attack key (Mouse 4)
            Run("TrollCommands", () => Ui.TrollCommands.Tick());             // D-119: far trolls sent to die from the My trolls tab
            Run("TrollGuard", () => TrollGuard.Tick());               // D-065 / D-084: creatures going for the player near a troll
            Run("TrollHome", () => TrollHome.Tick());                 // T-008e: slippers off -> own trolls wait at the bed
            Run("HubPlacement", () => HubPlacement.Tick());               // D-135: set bonus active once -> Troll Hub unlocked
            Run("HubSpeed", () => HubSpeed.Tick());                       // D-135: half-speed notice
            Run("HubSignMenu", () => HubSignMenu.Tick());                 // D-135: Escape closes the sign editor
            Run("TrollIdle", () => TrollIdle.Tick());                 // D-062: no job for 2 min -> farms on its own
        }

        void OnDestroy()
        {
            if (_cfgWatcher != null) { _cfgWatcher.Dispose(); _cfgWatcher = null; }
            if (_installer != null) { _installer.Uninstall(); _installer = null; }
            ModLog.Verbose("Plugin: destroyed");
        }
    }
}
