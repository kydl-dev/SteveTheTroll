using Jotunn.Entities;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Orders.Commands;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing.Commands;

/// <summary>
///     Test help (D-064): console command stt_fillbackpack [item] fills the backpack of your nearest tamed troll (within
///     30 m) with that
///     item (default Wood) until it is full, then sends it farming - it notices the full backpack and walks to your bed /
///     the troll depot.
/// </summary>
internal sealed class FillBackpackCommand : ConsoleCommand
{
    public override string Name => "stt_fillbackpack";

    public override string Help =>
        "SteveTheTroll test: fill your nearest tamed troll's backpack (default Wood) so it goes to the troll depot";

    public override void Run(string[] args)
    {
        Player p = Player.m_localPlayer;
        string item = args != null && args.Length > 0 && args[0].Length > 0 ? args[0] : "Wood";
        GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(item) : null;
        ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        if (p == null || drop == null)
        {
            Console.instance.Print("stt_fillbackpack: no player / no item '" + item + "'");
            return;
        }

        Character troll = null;
        float best = 30f;
        foreach (Character c in Character.GetAllCharacters())
        {
            if (c == null || !c.IsTamed() || !TrollTaming.IsTroll(c))
            {
                continue;
            }

            float d = Vector3.Distance(c.transform.position, p.transform.position);
            if (d < best)
            {
                best = d;
                troll = c;
            }
        }

        ZNetView view = troll != null ? troll.GetComponent<ZNetView>() : null;
        if (view == null || !view.IsValid() || !view.IsOwner())
        {
            Console.instance.Print("stt_fillbackpack: no tamed troll of yours within 30 m");
            return;
        }

        int stacks = 0;
        ZDO zdo = view.GetZDO();
        for (int i = 0; i < BackpackStore.ChestCount; i++)
        {
            Inventory inv = BackpackStore.Read(zdo, i);
            while (inv.HaveEmptySlot())
            {
                ItemDrop.ItemData it = drop.m_itemData.Clone();
                it.m_dropPrefab = prefab;
                it.m_stack = it.m_shared.m_maxStackSize;
                if (!inv.AddItem(it))
                {
                    break;
                }

                stacks++;
            }

            BackpackStore.Write(zdo, i, inv);
        }

        bool sent = troll.GetComponent<TrollFarmer>() != null ||
                    TrollOrderService.Send(new FarmFocusCommand(troll, "", true));
        ModLog.Info("FillBackpackCommand: " + TrollTaming.Tag(troll) + " got " + stacks + " stack(s) of " + item +
                    ", farming " + sent);
        Console.instance.Print("stt_fillbackpack: " + stacks + " stack(s) of " + item + " added; the troll heads home");
    }
}
