using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Jotunn.Utils;
using Kydl.Common;
using SteveTheTroll.Ui;
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
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        // ModGuid never changes after the first release. ModVersion = manifest.json version_number = newest CHANGELOG heading (tests check it).
        public const string ModGuid = "com.kydldev.SteveTheTroll";
        public const string ModName = "SteveTheTroll";
        public const string ModVersion = "2.0.0";
        public const string BundleName = "stevethetroll";

        internal static Plugin Instance;
        /// This mod's AssetBundle (null while the mod has none yet: Unity has not built one).
        internal static AssetBundle Bundle;
        ConfigReloader _cfgWatcher;

        void Awake()
        {
            Instance = this;
            ModLog.Init(Logger, ModName, () => ModSettings.DebugLog != null && ModSettings.DebugLog.Value);
            ModLog.Info(ModName + " " + ModVersion + " loaded (dll built " + File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location).ToString("yyyy-MM-dd HH:mm:ss") + ")");

            // the UI kit, the input lock and the hub come from KydlCore (it logs through its own log); only the fit math logs through this mod
            Fit.TrollFitMath.Log = s => ModLog.Verbose(s);     // shared with the Unity fit scene: it logs through the mod's log here
            Fit.TrollFitMath.Warn = s => ModLog.Warn(s);

            ModSettings.Bind(Config);
            _cfgWatcher = new ConfigReloader(Config);
            Loc.Register(UiText.En);
            Loc.Register(HubText.En);               // D-135: Troll Hub + its sign menu

            HubTabs.Register(Config);               // the menu = tabs in the KY hub (F6) of KydlCore

            // PROJECT_RULES 11: a switched-off part is not set up at all (restart the game after changing a switch)
            Bundle = LoadBundle();
            if (ModSettings.SlippersOn) Item.SlipperItem.Install();             // T-007: needs the bundle (icon) and the vanilla prefabs (added later by Jotunn)
            if (ModSettings.CostumeOn) Item.CostumeItems.Install();            // T-009d: costume helmet / chest / legs + Troll Club (bundle parts, vanilla prefabs)
            if (ModSettings.TrollsOn)
            {
                Backpack.DepotPrefab.Install();     // D-064: troll depot frame prefab (vanilla prefabs)
                Backpack.DepotGround.Install();     // D-103: own level-ground terrain op for the depot spots
                Taming.TrollTaming.Install();       // T-008a: the game's Troll becomes tameable (vanilla prefabs, added later by Jotunn)
                if (ModSettings.HubsOn) Hub.HubPrefab.Install();   // D-135: the Troll Hub piece (needs the level op above)
            }
            ModLog.Verbose("Plugin: set up slippers " + ModSettings.SlippersOn + ", costume " + ModSettings.CostumeOn + ", trolls " + ModSettings.TrollsOn);
            new Harmony(ModGuid).PatchAll(Assembly.GetExecutingAssembly());
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Figure.FigurePreviewCommand());   // T-004b look test: stt_preview
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Backpack.BuildDumpCommand());   // T-008b: stt_dumpbuild (backpack model)
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Testing.ClearCommand());        // D-093: stt_clear (clean test area)
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Testing.ClearDepotsCommand());  // D-104: stt_cleardepots (start clean)
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Testing.LevelGroundCommand());   // D-105: stt_levelground (repair)
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Ui.ComingPreviewCommand());     // D-058 preview: stt_coming [name]
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Backpack.FillBackpackCommand());  // D-064 test: stt_fillbackpack [item]
            ModLog.Verbose("Plugin: console commands stt_preview, stt_dumpbuild, stt_clear, stt_cleardepots, stt_levelground, stt_coming, stt_fillbackpack registered");
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
            if (!ModSettings.TrollsOn) return;       // every tick below belongs to the trolls (taming, farming, backpack, orders, map pins)
            Run("TrollRegistry", () => Taming.TrollRegistry.Tick());
            Run("TrollZones", () => Taming.TrollZones.Tick());               // D-084: zones around far own trolls stay loaded (they keep farming)
            Run("TrollCensus", () => Taming.TrollCensus.Tick());             // D-085: trolls within 500 m of the bed (spawn cap)
            Run("TrollMapPins", () => Ui.TrollMapPins.Tick());               // D-081: a map pin (costume head icon) per own tamed troll
            Run("TrollBedHeal", () => Taming.TrollBedHeal.Tick());           // D-085: +50 HP/s within 50 m of the bed
            Run("TrollDepot", () => Backpack.TrollDepot.Tick());              // D-159: retry removing empty depots
            Run("RuntimeHealth", () => Testing.RuntimeHealth.Tick());          // D-160: read-only health report (full backpacks, hub leash, stale hubs, stuck depots)
            Run("BackpackVisual", () => Backpack.BackpackVisual.Tick());
            Run("BackpackPickup", () => Backpack.BackpackPickup.Tick());     // T-008c: wood / ores / scrap within 50 m into the backpack
            Run("TrollOrders", () => Taming.TrollOrders.Tick());             // D-065: attack key (Mouse 4)
            Run("TrollCommands", () => Ui.TrollCommands.Tick());             // D-119: far trolls sent to die from the My trolls tab
            Run("TrollGuard", () => Taming.TrollGuard.Tick());               // D-065 / D-084: creatures going for the player near a troll
            Run("TrollHome", () => Taming.TrollHome.Tick());                 // T-008e: slippers off -> own trolls wait at the bed
            Run("HubPlacement", () => Hub.HubPlacement.Tick());               // D-135: set bonus active once -> Troll Hub unlocked
            Run("HubSpeed", () => Hub.HubSpeed.Tick());                       // D-135: half-speed notice
            Run("HubSignMenu", () => Hub.HubSignMenu.Tick());                 // D-135: Escape closes the sign editor
            Run("TrollIdle", () => Taming.TrollIdle.Tick());                 // D-062: no job for 2 min -> farms on its own
        }

        void OnDestroy()
        {
            if (_cfgWatcher != null) { _cfgWatcher.Dispose(); _cfgWatcher = null; }
            ModLog.Verbose("Plugin: destroyed");
        }
    }
}
