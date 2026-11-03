using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using Jotunn.Entities;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing.Commands;

/// <summary>
///     D-104 (user: "a devcommand that removes all prefabs placed by the mod, so we can start clean"): console command
///     stt_cleardepots [radius m, default: the whole world]. Removes every troll depot the mod built - the frames
///     (STT_TrollDepot) and the
///     depot chests (piece_chest marked stt_depot) WITH their contents - loaded ones through the game, unloaded ones by
///     deleting their saved
///     world objects (ZDOs). The player's own chests are never touched (no stt_depot mark). Levelled ground stays as it
///     is. Needs devcommands.
/// </summary>
internal sealed class ClearDepotsCommand : ConsoleCommand
{
    private static readonly AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> Objects =
        AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");

    public override string Name => "stt_cleardepots";

    public override string Help =>
        "SteveTheTroll test: remove every troll depot (frames + depot chests WITH contents) [within radius m, default whole world]";

    public override void Run(string[] args)
    {
        float radius = float.MaxValue;
        float r;
        if (args != null && args.Length > 0 &&
            float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out r) && r > 0f)
        {
            radius = r;
        }

        Player p = Player.m_localPlayer;
        if (p == null || ZNetScene.instance == null || ZDOMan.instance == null)
        {
            Console.instance.Print("stt_cleardepots: no player / world");
            return;
        }

        if (!Terminal.m_cheat)
        {
            Console.instance.Print("stt_cleardepots: needs devcommands");
            return;
        }

        int frameHash = DepotPrefab.Name.GetStableHashCode(), chestHash = DepotData.ChestPrefab.GetStableHashCode();
        Vector3 at = p.transform.position;
        var gone = new List<ZDO>();
        foreach (ZDO z in Objects(ZDOMan.instance).Values)
        {
            if (z == null)
            {
                continue;
            }

            int h = z.GetPrefab();
            bool ours = h == frameHash || (h == chestHash && z.GetBool(TrollDepot.FlagKey));
            if (ours && ClearCommand.Within(at, z.GetPosition(), radius))
            {
                gone.Add(z);
            }
        }

        int frames = 0, chests = 0, loaded = 0;
        long me = ZDOMan.GetSessionID();
        foreach (ZDO z in gone)
        {
            if (z.GetPrefab() == frameHash)
            {
                frames++;
            }
            else
            {
                chests++;
            }

            ZNetView view = ZNetScene.instance.FindInstance(z);
            if (view != null && view.IsValid())
            {
                view.ClaimOwnership();
                ZNetScene.instance.Destroy(view.gameObject);
                loaded++;
            }
            else
            {
                z.SetOwner(me);
                ZDOMan.instance.DestroyZDO(z);
            }
        }

        string msg = "stt_cleardepots: removed " + frames + " depot frame(s) and " + chests + " depot chest(s) (" +
                     loaded + " loaded, " + (gone.Count - loaded) + " far away)"
                     + (radius < float.MaxValue
                         ? " within " + radius.ToString("0", CultureInfo.InvariantCulture) + " m"
                         : " in the whole world");
        Console.instance.Print(msg);
        ModLog.Info("ClearDepotsCommand: " + msg);
    }
}
