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
        const string LeftName = "SteveTheTroll_FigureLeft";
        const string RightName = "SteveTheTroll_FigureRight";
        static GameObject _left, _right;

        /// Expected outcome: both figures exist after "on", none after "off"; returns the text shown in the console.
        public static string Toggle()
        {
            if (_left != null || _right != null)
            {
                if (_left != null) Object.Destroy(_left);
                if (_right != null) Object.Destroy(_right);
                _left = _right = null;
                return "troll preview off";
            }
            var player = Player.m_localPlayer;
            if (player == null) return "no local player (load a world first)";
            if (!ModSettings.On) return "the mod is switched off ([General] EnableAll)";
            // both feet are looked up BEFORE any figure is parented to the player: the troll copy has bones with the same names
            var leftFoot = Utils.FindChild(player.transform, "LeftFoot");
            var rightFoot = Utils.FindChild(player.transform, "RightFoot");
            _left = Spawn(player, leftFoot, "LeftFoot", LeftName, true);
            _right = Spawn(player, rightFoot, "RightFoot", RightName, false);
            bool ok = _left != null && _right != null;
            ModLog.Verbose("FigurePreview: on, left " + (_left != null) + ", right " + (_right != null));
            return ok ? "troll preview on (tune it in KydlTools F7: TrollPlace / TrollTune)" : "troll preview failed, see the log (debug on for details)";
        }

        static GameObject Spawn(Player player, Transform foot, string footBone, string name, bool left)
        {
            ModLog.Verbose("FigurePreview: Spawn " + name + ", foot '" + footBone + "' " + (foot != null ? "at " + foot.position.ToString("F2") + ", lossyScale " + foot.lossyScale.ToString("F3") : "NOT FOUND"));
            if (foot == null) { ModLog.Warn("FigurePreview: player bone '" + footBone + "' not found"); return null; }
            var holder = TrollFigureFactory.Build(name);
            if (holder == null) return null;
            holder.transform.SetParent(player.transform, false);
            var figure = holder.AddComponent<TrollFigure>();
            bool ready = figure.Init(foot, player.transform, left);
            holder.SetActive(true);
            ModLog.Verbose("FigurePreview: " + name + " active, init " + (ready ? "complete" : "INCOMPLETE (see warnings above)"));
            return holder;
        }
    }
}
