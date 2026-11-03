using Jotunn.Entities;
using Kydl.Common;
using SteveTheTroll.Presentation.Notifications;

namespace SteveTheTroll.Testing.Commands
{
    /// <summary>
    /// Console command stt_coming [name] (user 2026-10-27: "can I trigger this to see how it looks?"): shows the big first-tame message
    /// (D-058, Notify.Big) without taming anything and without touching the first-tame flag. Default name: Spencer.
    /// </summary>
    internal sealed class ComingPreviewCommand : ConsoleCommand
    {
        public override string Name { get { return "stt_coming"; } }
        public override string Help { get { return "SteveTheTroll: show the big 'X is coming!' message of the first tamed troll (preview only)"; } }

        public override void Run(string[] args)
        {
            string who = args != null && args.Length > 0 && args[0].Length > 0 ? string.Join(" ", args) : "Spencer";
            bool shown = Notify.Big(Loc.T("stevethetroll_troll_coming", who), Notify.Kind.Good);
            ModLog.Info("ComingPreviewCommand: '" + who + " is coming!' preview shown " + shown);
            Console.instance.Print("stt_coming: " + (shown ? "shown" : "no player in the world"));
        }
    }
}
