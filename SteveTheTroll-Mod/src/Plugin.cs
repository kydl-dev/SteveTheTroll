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
        public const string ModVersion = "0.1.0";
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
            Backpack.DepotPrefab.Install();         // D-064: troll depot frame prefab (vanilla prefabs)
            Taming.TrollTaming.Install();           // T-008a: the game's Troll becomes tameable (vanilla prefabs, added later by Jotunn)
            UiInputLock.Install(ModGuid);           // the character does not move while one of the mod's menus is open
            new Harmony(ModGuid).PatchAll(Assembly.GetExecutingAssembly());
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Figure.FigurePreviewCommand());   // T-004b look test: stt_preview
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Backpack.BuildDumpCommand());   // T-008b: stt_dumpbuild (backpack model)
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Ui.ComingPreviewCommand());     // D-058 preview: stt_coming [name]
            Jotunn.Managers.CommandManager.Instance.AddConsoleCommand(new Backpack.FillBackpackCommand());  // D-064 test: stt_fillbackpack [item]
            ModLog.Verbose("Plugin: console commands stt_preview, stt_dumpbuild, stt_coming, stt_fillbackpack registered");
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

        void Update()
        {
            if (_cfgWatcher != null) _cfgWatcher.Tick();
            if (!ModSettings.On) return;
            MainMenu.Tick();
            Taming.TrollRegistry.Tick();
            Backpack.BackpackVisual.Tick();
            Backpack.BackpackPickup.Tick();         // T-008c: wood / ores / scrap within 50 m into the backpack
            Taming.TrollOrders.Tick();              // D-065: attack key (Mouse 4)
            Taming.TrollGuard.Tick();               // D-065: attackers of the player / troll are targeted at once, crowds get ground slams
            Taming.TrollHome.Tick();                // T-008e: slippers off -> own trolls wait at the bed
            Taming.TrollIdle.Tick();                // D-062: no job for 2 min -> farms on its own         // T-008b: backpack on every tamed troll       // D-043: live values of the player's tamed trolls for the F6 lists
        }

        void OnDestroy()
        {
            if (_cfgWatcher != null) { _cfgWatcher.Dispose(); _cfgWatcher = null; }
            ModLog.Verbose("Plugin: destroyed");
        }
    }
}
