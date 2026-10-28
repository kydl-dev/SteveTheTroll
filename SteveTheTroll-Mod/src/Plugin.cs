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
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        // ModGuid never changes after the first release. ModVersion = manifest.json version_number = newest CHANGELOG heading (tests check it).
        public const string ModGuid = "com.kydldev.SteveTheTroll";
        public const string ModName = "SteveTheTroll";
        public const string ModVersion = "1.0.0";
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

            // the shared UI kit (Kydl.Ui) logs through the mod's log: debug lines only with the debug switch on
            Kydl.Ui.UiLog.VerboseSink = s => ModLog.Verbose(s);
            Kydl.Ui.UiLog.WarnSink = s => ModLog.Warn(s);
            Kydl.Ui.UiLog.ErrorSink = s => ModLog.Error(s);
            Kydl.Ui.UiKit.Owner = ModName;
            Fit.TrollFitMath.Log = s => ModLog.Verbose(s);     // shared with the Unity fit scene: it logs through the mod's log here
            Fit.TrollFitMath.Warn = s => ModLog.Warn(s);

            ModSettings.Bind(Config);
            _cfgWatcher = new ConfigReloader(Config);
            Loc.Register(UiText.En);

            Bundle = LoadBundle();
            Item.SlipperItem.Install();             // T-007: needs the bundle (icon) and the vanilla prefabs (added later by Jotunn)
            Item.CostumeItems.Install();            // T-009d: costume helmet / chest / legs + Troll Club (bundle parts, vanilla prefabs)
            Backpack.DepotPrefab.Install();         // D-064: troll depot frame prefab (vanilla prefabs)
            Backpack.DepotGround.Install();         // D-103: own level-ground terrain op for the depot spots
            Taming.TrollTaming.Install();           // T-008a: the game's Troll becomes tameable (vanilla prefabs, added later by Jotunn)
            UiInputLock.Install(ModGuid);           // the character does not move while one of the mod's menus is open
            new Harmony(ModGuid).PatchAll(Assembly.GetExecutingAssembly());
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Figure.FigurePreviewCommand());   // T-004b look test: stt_preview
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Backpack.BuildDumpCommand());   // T-008b: stt_dumpbuild (backpack model)
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Testing.ClearCommand());        // D-093: stt_clear (clean test area)
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Ui.ComingPreviewCommand());     // D-058 preview: stt_coming [name]
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Backpack.FillBackpackCommand());  // D-064 test: stt_fillbackpack [item]
            ModLog.Verbose("Plugin: console commands stt_preview, stt_dumpbuild, stt_clear, stt_coming, stt_fillbackpack registered");
            ModLog.Verbose("Plugin: Awake finished (Harmony patches applied, menu key " + ModSettings.MenuKey.Value + ")");
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
        static void Run(string name, System.Action tick)
        {
            try { tick(); }
            catch (System.Exception e)
            {
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
            Run("MainMenu", () => MainMenu.Tick());
            Run("TrollRegistry", () => Taming.TrollRegistry.Tick());
            Run("TrollZones", () => Taming.TrollZones.Tick());               // D-084: zones around far own trolls stay loaded (they keep farming)
            Run("TrollCensus", () => Taming.TrollCensus.Tick());             // D-085: trolls within 500 m of the bed (spawn cap)
            Run("TrollMapPins", () => Ui.TrollMapPins.Tick());               // D-081: a map pin (costume head icon) per own tamed troll
            Run("TrollBedHeal", () => Taming.TrollBedHeal.Tick());           // D-085: +50 HP/s within 50 m of the bed
            Run("BackpackVisual", () => Backpack.BackpackVisual.Tick());
            Run("BackpackPickup", () => Backpack.BackpackPickup.Tick());     // T-008c: wood / ores / scrap within 50 m into the backpack
            Run("TrollOrders", () => Taming.TrollOrders.Tick());             // D-065: attack key (Mouse 4)
            Run("TrollGuard", () => Taming.TrollGuard.Tick());               // D-065 / D-084: creatures going for the player near a troll
            Run("TrollHome", () => Taming.TrollHome.Tick());                 // T-008e: slippers off -> own trolls wait at the bed
            Run("TrollIdle", () => Taming.TrollIdle.Tick());                 // D-062: no job for 2 min -> farms on its own
        }

        void OnDestroy()
        {
            if (_cfgWatcher != null) { _cfgWatcher.Dispose(); _cfgWatcher = null; }
            ModLog.Verbose("Plugin: destroyed");
        }
    }
}
