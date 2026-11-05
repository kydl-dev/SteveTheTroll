using System;
using System.Collections.Generic;
using HarmonyLib;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Orders.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;
using Object = UnityEngine.Object;
namespace SteveTheTroll.Testing.TestHooks;

/// <summary>Class size: CombatTestHooks continued - weapons, removing test trolls, the aim, the slippers, the farmer check.</summary>
internal static partial class CombatTestHooks
{
    /// Creatures stt.freezeMob froze (cleared by stt.reset).
    internal static readonly HashSet<Character> Frozen = new HashSet<Character>();

    /// 2026-11-05 (gate run 1, farm-friend-detour: the frozen "friend" troll was sent back to work 1.5 s after its spawn and walked 6 m - the test read it as pushed):
    /// MonsterAI.UpdateAI postfix, last of all - a frozen creature's move for this frame is zeroed, whatever steered it (game AI, farming, chase). Pushes still move it.
    internal static void HoldStill(MonsterAI ai)
    {
        if (Frozen.Count == 0 || ai == null)
        {
            return;
        }

        Character ch = ai.GetComponent<Character>();
        if (ch != null && Frozen.Contains(ch))
        {
            ch.SetMoveDir(Vector3.zero);
            ch.SetRun(false);
        }
    }

    /// stt.reset: no creature stays frozen into the next test. Expected outcome: how many were released.
    internal static int Unfreeze()
    {
        int n = Frozen.Count;
        Frozen.Clear();
        return n;
    }
    /// The troll loses its log (D-121 "Unarmed" spawns): only its own body attacks are left. Expected outcome: how many items were removed.
    [TestHook("stt.disarm")]
    private static int Disarm(GameObject troll)
    {
        Humanoid h = troll.GetComponent<Humanoid>();
        Inventory inv = h.GetInventory();
        int n = 0;
        foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(inv.GetAllItems()))
        {
            string name = it.m_dropPrefab != null ? it.m_dropPrefab.name : it.m_shared.m_name;
            if (name.ToLowerInvariant().Contains("log"))
            {
                h.UnequipItem(it, false);
                inv.RemoveItem(it);
                n++;
            }
        }

        // D-172 (run 141510: a club troll had ONLY its two log swings - after the disarm it had nothing, not the slap a club-less troll farms with): give it the slap of
        // its own default / random item sets, so "unarmed" means what a Spencer is - slap only.
        bool slap = false;
        foreach (ItemDrop.ItemData it in inv.GetAllItems())
        {
            if (it.m_shared.m_name.ToLowerInvariant().Contains("slap"))
            {
                slap = true;
            }
        }

        if (!slap)
        {
            var all = new List<GameObject>();
            if (h.m_defaultItems != null)
            {
                all.AddRange(h.m_defaultItems);
            }

            if (h.m_randomWeapon != null)
            {
                all.AddRange(h.m_randomWeapon);
            }

            if (h.m_randomSets != null)
            {
                foreach (Humanoid.ItemSet set in h.m_randomSets)
                {
                    if (set != null && set.m_items != null)
                    {
                        all.AddRange(set.m_items);
                    }
                }
            }

            foreach (GameObject g in all)
            {
                ItemDrop drop = g != null ? g.GetComponent<ItemDrop>() : null;
                if (drop == null || !drop.m_itemData.m_shared.m_name.ToLowerInvariant().Contains("slap"))
                {
                    continue;
                }

                inv.AddItem(g, 1);
                slap = true;
                break;
            }
        }

        ModLog.Info("CombatTestHooks: " + TrollTaming.Tag(h) + " disarmed (" + n + " log item(s) removed, " +
                    inv.GetAllItems().Count + " left, slap " + slap + ")");
        return n;
    }

    /// The troll loses EVERY item (no attack at all). Expected outcome: how many items were removed.
    [TestHook("stt.stripWeapons")]
    private static int StripWeapons(GameObject troll)
    {
        Humanoid h = troll.GetComponent<Humanoid>();
        Inventory inv = h.GetInventory();
        int n = inv.GetAllItems().Count;
        h.UnequipAllItems();
        inv.RemoveAll();
        ModLog.Info("CombatTestHooks: " + TrollTaming.Tag(h) + " stripped of all " + n + " item(s)");
        return n;
    }

    /// The tool the farmer would swing ("" = none).
    [TestHook("stt.weaponOf")]
    private static string WeaponOf(GameObject troll)
    {
        Humanoid h = troll.GetComponent<Humanoid>();
        ItemDrop.ItemData w = TrollWeapons.Pick(h, Attack.AttackType.Horizontal, true) ??
                              TrollWeapons.Pick(h, Attack.AttackType.Vertical, true);
        return w != null ? w.m_shared.m_name : "";
    }

    /// Removes every tamed troll (tests leave farming trolls behind; a stray one can chop the next test's tree). Expected outcome: how many were removed.
    [TestHook("stt.clearTrolls")]
    internal static int ClearTrolls()
    {
        // D-172 (review: "restrict it to the test arena or track test-created trolls"): only trolls a test hook made, or tamed trolls within ArenaClearMetres of the
        // test arena (stt.arenaFurther / stt.teleport places included) - never a player's trolls elsewhere in a normal save
        int n = 0, kept = 0;
        foreach (Character c in new List<Character>(Character.GetAllCharacters()))
        {
            if (c == null || !c.IsTamed() || !TrollTaming.IsTroll(c))
            {
                continue;
            }

            ZNetView v = c.GetComponent<ZNetView>();
            if (v == null || !v.IsValid())
            {
                continue;
            }

            if (!TestHooks.MadeByTest(v.GetZDO().m_uid) &&
                !ArenaTestHooks.InArena(c.transform.position, ArenaClearMetres))
            {
                kept++;
                continue;
            }

            v.ClaimOwnership();
            ZNetScene.instance.Destroy(c.gameObject);
            n++;
        }

        ModLog.Info("CombatTestHooks: " + n + " tamed troll(s) removed before the test (test-made or within " +
                    ArenaClearMetres + " m of the arena), " + kept + " kept");
        return n;
    }

    /// D-172: what the REAL attack key would act on now (TrollOrders.Aimed: camera position + forward, the key's own rules), checked before the lab presses the key.
    /// Expected outcome: the name under the crosshair, "" for nothing.
    [TestHook("stt.aimedAt")]
    private static string AimedAt()
    {
        return TrollOrders.Aimed(Player.m_localPlayer);
    }

    /// D-172: the player puts the Fluffy Slippers on / takes them off (the plugin's own rule: no slippers -> a full troll is not sent to a depot, D-149).
    /// Expected outcome: whether the slippers are worn afterwards.
    [TestHook("stt.slippers")]
    private static bool Slippers(bool on)
    {
        Player p = Player.m_localPlayer;
        ItemDrop.ItemData
            any = null, worn = null; // earlier tests' 'give' steps leave several pairs: act on the worn one
        foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
        {
            if (it.m_dropPrefab == null || it.m_dropPrefab.name != "STT_TrollSlippers")
            {
                continue;
            }

            if (any == null)
            {
                any = it;
            }

            if (p.IsItemEquiped(it))
            {
                worn = it;
            }
        }

        if (any == null)
        {
            throw new Exception("the player has no STT_TrollSlippers");
        }

        if (on && worn == null)
        {
            p.EquipItem(any);
            worn = p.IsItemEquiped(any) ? any : null;
        }

        if (!on && worn != null)
        {
            p.UnequipItem(worn);
            worn = p.IsItemEquiped(worn) ? worn : null;
        }

        ModLog.Info("CombatTestHooks: slippers " + (worn != null ? "on" : "off"));
        return worn != null;
    }

    /// Whether the troll has a farmer component (it farms / goes home / unloads).
    [TestHook("stt.hasFarmer")]
    private static bool HasFarmer(GameObject troll)
    {
        return troll.GetComponent<TrollFarmer>() != null;
    }
}
