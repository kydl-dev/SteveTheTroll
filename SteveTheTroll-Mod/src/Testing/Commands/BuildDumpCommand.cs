using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Jotunn.Entities;
using KydlCore.Common;
using SteveTheTroll.Bootstrap;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Testing.Commands;

/// <summary>
///     T-008b step 1 (D-037, reworked D-137 after the user's 22:55 review: two dumps of the same building gave different
///     piece counts):
///     console command stt_dumpbuild.
///     stt_dumpbuild anchor        remembers where you stand as the fixed capture centre (world position, kept until the
///     game ends)
///     stt_dumpbuild clear         forgets the anchor again
///     stt_dumpbuild [radius m]    writes every building piece AND every dropped item (lanterns) within that HORIZONTAL
///     radius of the anchor
///     (or of you when no anchor is set) and within VerticalWindow m above / below it. Default 12, max 40.
///     The capture centre is never the player's position unless no anchor was set, and it is written to the file, so two
///     runs are comparable:
///     every offset is relative to the capture centre in WORLD axes (not rotated into a construction frame: rotations are
///     world euler angles).
///     Lines are sorted (prefab, x, y, z). Every capture goes to its own file build_dump_yyyyMMdd_HHmmss.txt in
///     BepInEx/cache/SteveTheTroll
///     (ModCache) plus the same text as build_dump.txt (the latest). The log names the player's coordinates, the centre,
///     the radius, the count and every piece.
/// </summary>
internal sealed class BuildDumpCommand : ConsoleCommand
{
    private const float DefaultRadius = 12f, MaxRadius = 40f, VerticalWindow = 30f;
    private static Vector3? _anchor;

    public override string Name => "stt_dumpbuild";

    public override string Help =>
        "SteveTheTroll: 'anchor' = remember this spot as capture centre, 'clear' = forget it, [radius m] = write the pieces and dropped items around the anchor (or you) to a new file";

    public override void Run(string[] args)
    {
        Player p = Player.m_localPlayer;
        if (p == null)
        {
            Console.instance.Print("stt_dumpbuild: no player");
            return;
        }

        string first = args != null && args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "";
        if (first == "anchor")
        {
            _anchor = p.transform.position;
            string text = "stt_dumpbuild: anchor set to " + _anchor.Value.ToString("F2");
            ModLog.Info("BuildDump: " + text);
            Console.instance.Print(text);
            return;
        }

        if (first == "clear")
        {
            _anchor = null;
            Console.instance.Print("stt_dumpbuild: anchor cleared (the next capture centres on you)");
            return;
        }

        float radius = DefaultRadius;
        if (first.Length > 0 && !float.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out radius))
        {
            radius = DefaultRadius;
        }

        radius = Mathf.Clamp(radius, 0.5f, MaxRadius);
        Console.instance.Print("stt_dumpbuild: " +
                               Dump(_anchor ?? p.transform.position, _anchor.HasValue, p.transform.position, radius));
    }

    /// The sort order of the file: prefab name, then x, y, z of the offset (strings of the written numbers, so equal text sorts equal).
    internal static int Order(string a, string b)
    {
        return string.CompareOrdinal(a, b);
    }

    /// Expected outcome: a text naming the file and the piece count (or why nothing was written).
    internal static string Dump(Vector3 centre, bool anchored, Vector3 player, float radius)
    {
        var found = new List<KeyValuePair<string, Transform>>();
        foreach (Piece piece in Object.FindObjectsByType<Piece>(FindObjectsSortMode.None))
        {
            if (piece != null && piece.GetComponent<ZNetView>() != null &&
                Inside(piece.transform.position, centre, radius))
            {
                found.Add(new KeyValuePair<string, Transform>(Utils.GetPrefabName(piece.gameObject), piece.transform));
            }
        }

        int pieces = found.Count;
        foreach (ItemDrop item in Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
        {
            if (item != null && Inside(item.transform.position, centre, radius))
            {
                found.Add(new KeyValuePair<string, Transform>(Utils.GetPrefabName(item.gameObject), item.transform));
            }
        }

        ModLog.Info("BuildDump: player at " + player.ToString("F2") + ", centre " + centre.ToString("F2") +
                    (anchored ? " (anchor)" : " (the player, no anchor set)")
                    + ", radius " + radius.ToString("F1", CultureInfo.InvariantCulture) + " m (+-" + VerticalWindow +
                    " m high): " + pieces + " piece(s), " + (found.Count - pieces) + " dropped item(s)");
        if (found.Count == 0)
        {
            return "nothing within " + radius + " m of " + centre.ToString("F1");
        }

        CultureInfo c = CultureInfo.InvariantCulture;
        var lines = new List<string>();
        float minY = float.MaxValue;
        Vector3 sum = Vector3.zero;
        foreach (KeyValuePair<string, Transform> f in found)
        {
            Transform t = f.Value;
            Vector3 lp = t.position - centre;
            Vector3 e = t.rotation.eulerAngles;
            Vector3 s = t.lossyScale;
            sum += t.position;
            minY = Mathf.Min(minY, t.position.y);
            lines.Add(f.Key + " | " + lp.x.ToString("0.###", c) + " " + lp.y.ToString("0.###", c) + " " +
                      lp.z.ToString("0.###", c)
                      + " | " + e.x.ToString("0.#", c) + " " + e.y.ToString("0.#", c) + " " + e.z.ToString("0.#", c) +
                      " | " + s.x.ToString("0.###", c) + " " + s.y.ToString("0.###", c) + " " + s.z.ToString("0.###", c)
                      + " | world " + t.position.x.ToString("0.###", c) + " " + t.position.y.ToString("0.###", c) +
                      " " + t.position.z.ToString("0.###", c));
        }

        lines.Sort(Order);
        Vector3 centreOfAll = sum / found.Count;
        centreOfAll.y = minY;
        var sb = new StringBuilder();
        sb.AppendLine("# stt_dumpbuild " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", c) + ", " + found.Count +
                      " object(s): " + pieces + " piece(s), " + (found.Count - pieces) + " dropped item(s)");
        sb.AppendLine("# player at " + player.ToString("F3") + "; capture centre " + centre.ToString("F3") +
                      (anchored ? " (anchor)" : " (the player: no anchor set)") + "; radius " +
                      radius.ToString("F1", c) + " m horizontal, +-" + VerticalWindow + " m vertical");
        sb.AppendLine("# centre of the objects, lowest y (for a construction base): " + centreOfAll.ToString("F3") +
                      " = offset " + (centreOfAll - centre).ToString("F3") + " from the capture centre");
        sb.AppendLine(
            "# prefab | offset x y z from the capture centre, WORLD axes | rotation euler x y z, WORLD | lossy scale x y z | world position x y z");
        foreach (string l in lines)
        {
            sb.AppendLine(l);
        }

        string text = sb.ToString();
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", c);
        string path = ModCache.File(Plugin.ModName, "build_dump_" + stamp + ".txt");
        File.WriteAllText(path, text);
        File.WriteAllText(ModCache.File(Plugin.ModName, "build_dump.txt"), text);
        foreach (string l in lines)
        {
            ModLog.Verbose("BuildDump: " + l);
        }

        ModLog.Info("BuildDump: " + found.Count + " object(s) written to " + path);
        return found.Count + " object(s) (" + pieces + " pieces, " + (found.Count - pieces) + " dropped items) -> " +
               path;
    }

    private static bool Inside(Vector3 at, Vector3 centre, float radius)
    {
        float dx = at.x - centre.x, dz = at.z - centre.z;
        return dx * dx + dz * dz <= radius * radius && Mathf.Abs(at.y - centre.y) <= VerticalWindow;
    }
}
