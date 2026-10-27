using Jotunn.Entities;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Figure
{
    /// <summary>
    /// Dev preview (T-004b): console command `stt_preview` puts / removes a mini troll on each foot of the local player, so the
    /// user can judge the runtime-troll look (D-013) before the slipper item exists. Local only, nothing is saved or synced.
    /// </summary>
    internal sealed class FigurePreviewCommand : ConsoleCommand
    {
        public override string Name { get { return "stt_preview"; } }
        public override string Help { get { return "SteveTheTroll: toggle the mini troll on each of your feet (look test)"; } }

        public override void Run(string[] args)
        {
            ModLog.Verbose("FigurePreview: command " + Name + " (" + (args != null ? args.Length : 0) + " arg(s))");
            string result = FigurePreview.Toggle();
            ModLog.Info("FigurePreview: " + result);
            if (Console.instance != null) Console.instance.Print(result);
        }
    }

    internal static class FigurePreview
    {
        static SlipperSet _set;

        /// Expected outcome: the full set exists after "on", nothing after "off"; returns the text shown in the console.
        public static string Toggle()
        {
            if (_set != null)
            {
                _set.Despawn();
                _set = null;
                return "troll preview off";
            }
            var player = Player.m_localPlayer;
            if (player == null) return "no local player (load a world first)";
            if (!ModSettings.On) return "the mod is switched off ([General] EnableAll)";
            _set = SlipperSet.Spawn(player.transform, "preview " + player.GetPlayerName());
            return _set.Complete ? "troll preview on (tune it in KydlTools F7)" : "troll preview incomplete, see the log (debug on for details)";
        }
    }
}
