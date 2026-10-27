using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using Jotunn.Entities;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Backpack
{
    /// <summary>
    /// T-008b step 1 (D-037: the troll backpack looks like the user's in-game construction): console command stt_dumpbuild [radius m, default 4].
    /// Writes every building piece within the radius of the player - prefab name, position and rotation relative to the construction's base
    /// (centre of all pieces, lowest point) - to BepInEx/config/SteveTheTroll_build_dump.txt in the test profile. The agent turns the lines into
    /// BackpackData and the backpack is then built from the game's own piece meshes on the troll's back (nothing of the game is shipped).
    /// </summary>
    internal sealed class BuildDumpCommand : ConsoleCommand
    {
        const float DefaultRadius = 4f, MaxRadius = 12f;
        public override string Name { get { return "stt_dumpbuild"; } }
        public override string Help { get { return "SteveTheTroll: write the building pieces within [radius] m of you to a file (backpack model)"; } }

        public override void Run(string[] args)
        {
            float radius = DefaultRadius;
            if (args != null && args.Length > 0 && !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out radius)) radius = DefaultRadius;
            radius = Mathf.Clamp(radius, 0.5f, MaxRadius);
            var p = Player.m_localPlayer;
            if (p == null) { Console.instance.Print("stt_dumpbuild: no player"); return; }
            string file = Dump(p.transform.position, radius);
            Console.instance.Print("stt_dumpbuild: " + file);
        }

        /// Expected outcome: a text naming the file and the piece count (or why nothing was written).
        internal static string Dump(Vector3 at, float radius)
        {
            var pieces = new List<Piece>();
            foreach (var piece in Object.FindObjectsByType<Piece>(FindObjectsSortMode.None))
                if (piece != null && piece.GetComponent<ZNetView>() != null && Vector3.Distance(piece.transform.position, at) <= radius) pieces.Add(piece);
            if (pieces.Count == 0) { ModLog.Info("BuildDump: no pieces within " + radius + " m of " + at.ToString("F1")); return "no pieces within " + radius + " m"; }
            Vector3 sum = Vector3.zero; float minY = float.MaxValue;
            foreach (var piece in pieces) { sum += piece.transform.position; minY = Mathf.Min(minY, piece.transform.position.y); }
            var origin = sum / pieces.Count; origin.y = minY;
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("# stt_dumpbuild " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", c) + " at " + at.ToString("F2") + ", radius " + radius + " m, " + pieces.Count + " piece(s)");
            sb.AppendLine("# origin (centre of the pieces, lowest y) " + origin.ToString("F3") + "; player forward " + Player.m_localPlayer.transform.forward.ToString("F3"));
            sb.AppendLine("# prefab | local position x y z | local rotation euler x y z | lossy scale x y z");
            foreach (var piece in pieces)
            {
                var t = piece.transform;
                var lp = t.position - origin;
                var e = t.rotation.eulerAngles;
                var s = t.lossyScale;
                sb.AppendLine(Utils.GetPrefabName(piece.gameObject) + " | " + lp.x.ToString("0.###", c) + " " + lp.y.ToString("0.###", c) + " " + lp.z.ToString("0.###", c)
                    + " | " + e.x.ToString("0.#", c) + " " + e.y.ToString("0.#", c) + " " + e.z.ToString("0.#", c) + " | " + s.x.ToString("0.###", c) + " " + s.y.ToString("0.###", c) + " " + s.z.ToString("0.###", c));
            }
            string path = Path.Combine(Paths.ConfigPath, "SteveTheTroll_build_dump.txt");
            File.WriteAllText(path, sb.ToString());
            ModLog.Info("BuildDump: " + pieces.Count + " piece(s) within " + radius + " m written to " + path);
            return pieces.Count + " piece(s) -> " + path;
        }
    }
}
