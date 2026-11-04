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

/// <summary>
///     D-171: TestLab hooks for combat, orders and unarmed trolls (suite tests defend-*, counter-*, guard-*, nohit-*,
///     walls-*, attack-key-*, unarmed-*). Hits go through the
///     game's own Character.Damage, so the mod's real patches (TrollDefend, TamedTrollStats -> TrollCounter) react exactly
///     as in play.
/// </summary>
internal static class CombatTestHooks
{
    private const float ArenaClearMetres = 150f;

    private static readonly AccessTools.FieldRef<MonsterAI, Character> TargetOf =
        AccessTools.FieldRefAccess<MonsterAI, Character>("m_targetCreature");

    private static readonly Action<MonsterAI, Character> SetTarget =
        AccessTools.MethodDelegate<Action<MonsterAI, Character>>(AccessTools.Method(typeof(MonsterAI), "SetTarget"));

    private static HitData Hit(Character attacker, float damage)
    {
        var h = new HitData();
        h.m_damage.m_blunt = damage;
        h.m_point = attacker != null ? attacker.transform.position : Vector3.zero;
        h.SetAttacker(attacker);
        return h;
    }

    /// A wild creature `dx`,`dz` metres from the player AS IT STANDS NOW (the lab's own spawn step is relative to the test's start; after stt.arenaFurther that was 100 m back).
    [TestHook("stt.spawnMob")]
    private static GameObject SpawnMob(string prefab, float dx, float dz)
    {
        GameObject pf = ZNetScene.instance.GetPrefab(prefab);
        if (pf == null)
        {
            throw new Exception(prefab + " prefab missing");
        }

        Vector3 at = Player.m_localPlayer.transform.position + new Vector3(dx, 0f, dz);
        at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.3f;
        ModLog.Info("CombatTestHooks: " + prefab + " spawned at " + at.ToString("F0"));
        // D-175 (run 192749, guard-mob: the Greydwarf stood with its back to the player - Quaternion.identity - could not see it, and its own AI dropped the target the hook had
        // just set, so no guard scan ever saw a hunter): a wild creature the test sets on the player looks at the player, as an aggressive one would
        Vector3 toPlayer = Player.m_localPlayer.transform.position - at;
        toPlayer.y = 0f;
        Quaternion face = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
        GameObject spawned = Object.Instantiate(pf, at, face);
        _lastMob = spawned;
        return spawned;
    }

    /// D-176 (review of attack-key-creature: a hostile Greydwarf went for the player and TrollGuard gave the troll the target BEFORE the key was pressed, so the test passed without the key doing anything):
    /// the creature keeps its body and colliders (the key ray and the troll can still hit it) but its AI is off and it has no target, so nothing but the key can give the troll one. Expected outcome: true when frozen.
    [TestHook("stt.freezeMob")]
    private static bool FreezeMob(GameObject mob)
    {
        MonsterAI ai = mob != null ? mob.GetComponent<MonsterAI>() : null;
        if (ai == null)
        {
            return false;
        }

        TargetOf(ai) = null;
        ai.enabled = false;
        return !ai.enabled;
    }

    /// The creature `attacker` hits the local player for `damage` (blunt). Expected outcome: the player's health afterwards.
    [TestHook("stt.hitPlayer")]
    private static float HitPlayer(GameObject attacker, float damage)
    {
        Player p = Player.m_localPlayer;
        Character a = attacker != null ? attacker.GetComponent<Character>() : null;
        p.Damage(Hit(a, damage));
        ModLog.Info("CombatTestHooks: " + (a != null ? a.name : "?") + " hit the player for " + damage + " - health " +
                    p.GetHealth().ToString("F0"));
        return p.GetHealth();
    }

    /// The creature `attacker` hits the troll for `damage` (blunt). Expected outcome: the troll's health afterwards.
    [TestHook("stt.hitTroll")]
    private static float HitTroll(GameObject troll, GameObject attacker, float damage)
    {
        Character t = troll.GetComponent<Character>();
        Character a = attacker != null ? attacker.GetComponent<Character>() : null;
        t.Damage(Hit(a, damage));
        ModLog.Info("CombatTestHooks: " + (a != null ? a.name : "?") + " hit " + TrollTaming.Tag(t) + " for " + damage +
                    " - health " + t.GetHealth().ToString("F0"));
        return t.GetHealth();
    }

    /// The creature's AI goes for the local player (as an aggressive mob would). Expected outcome: whether its target is the player now.
    [TestHook("stt.mobTargetsPlayer")]
    private static bool MobTargetsPlayer(GameObject mob)
    {
        MonsterAI ai = mob.GetComponent<MonsterAI>();
        if (ai == null)
        {
            return ai != null;
        }

        SetTarget(ai, Player.m_localPlayer);
        if (TargetOf(ai) != Player.m_localPlayer)
        {
            TargetOf(ai) =
                Player.m_localPlayer; // the game's SetTarget may refuse (alerted state / sight); the field is what TrollGuard reads
        }

        ai.Alert();
        return TargetOf(ai) == Player.m_localPlayer;
    }

    /// The attack key's ray goes from the camera towards this object's collider centre instead of along the camera's forward axis (TrollOrders.AimOverride, one press).
    /// The lab's `face` turns the player but not the 3rd-person camera's pitch, so the ray missed small targets. Expected outcome: whether an aim was set.
    [TestHook("stt.aimAt")]
    private static bool AimAt(GameObject target)
    {
        TrollOrders.AimOverride = target;
        return TrollOrders.AimOverride != null;
    }

    /// The name of the creature TrollGuard holds as the troll's forced target ("" = none). Only a hunter of the owner or an order sets one, so it shows the attack key's own effect. Expected outcome: the name.
    [TestHook("stt.forcedTargetOf")]
    private static string ForcedTargetOf(GameObject troll)
    {
        return TrollGuard.ForcedName(troll != null ? troll.GetComponent<Character>() : null);
    }

    /// The prefab name of the creature the troll's AI targets ("" = none).
    [TestHook("stt.targetOf")]
    private static string Target(GameObject troll)
    {
        MonsterAI ai = troll.GetComponent<MonsterAI>();
        Character t = ai != null ? TargetOf(ai) : null;
        return t != null ? Utils.GetPrefabName(t.gameObject) : "";
    }

    /// D-175 (guard-mob): the mob goes for the player AT EVERY POLL (the game's own AI may drop a target it cannot sense; the real TrollGuard scan runs 4 times a second and
    /// only needs to see it once) and the answer is what the troll targets. The guard's own scan still decides - nothing here calls it. Expected outcome: the prefab name the troll targets.
    [TestHook("stt.guardTargetOf")]
    private static string GuardTargetOf(GameObject mob, GameObject troll)
    {
        MobTargetsPlayer(mob);
        return Target(troll);
    }

    private static float _nextKillLog;
    private static GameObject _lastMob;

    /// stt.guardKill for the creature stt.spawnMob made last: a named parameter cannot be resolved once the creature is destroyed (the lab says 'not found'), and a kill destroys it.
    /// Expected outcome: "dead" once it is dead / gone, else "alive".
    [TestHook("stt.guardKillLast")]
    private static string GuardKillLast(GameObject troll)
    {
        return GuardKill(_lastMob, troll);
    }

    /// The creature stt.spawnMob made last has lost health, or is dead / gone (a kill destroys it, so it cannot be named as a hook parameter). Expected outcome: true once it was hurt.
    [TestHook("stt.lastMobHurt")]
    private static bool LastMobHurt()
    {
        Character c = _lastMob != null ? _lastMob.GetComponent<Character>() : null;
        if (c == null || c.IsDead())
        {
            return true;
        }

        MobTargetsPlayer(_lastMob); // it keeps going for the player at every poll, as in guardKill
        return c.GetHealth() < c.GetMaxHealth() - 0.01f;
    }

    private static float _nextNearLog;

    /// D-176 (guard-mob-kills: "never hurt" did not say where the chase stopped): true once the troll is within `metres` (flat) of the creature stt.spawnMob made last, or the creature is dead /
    /// gone. Logs the distance once a second, so the log shows the approach (target assigned -> distance falls -> within reach -> swing -> damage -> dead). Expected outcome: true when near.
    [TestHook("stt.trollNearLastMob")]
    private static bool TrollNearLastMob(GameObject troll, float metres)
    {
        Character c = _lastMob != null ? _lastMob.GetComponent<Character>() : null;
        if (c == null || c.IsDead())
        {
            return true;
        }

        Vector3 d = c.transform.position - troll.transform.position;
        d.y = 0f;
        if (Time.time >= _nextNearLog)
        {
            _nextNearLog = Time.time + 1f;
            ModLog.Info("CombatTestHooks: the troll is " + d.magnitude.ToString("F1") + " m from the creature (wants " + metres.ToString("F1") + " m), real speed " +
                        troll.GetComponent<Character>().GetVelocity().magnitude.ToString("F1") + " m/s");
        }

        MobTargetsPlayer(_lastMob);
        return d.magnitude <= metres;
    }

    /// D-175 (run 194722, spot-return: a wild Greyling went for the player, the guard gave the troll that target, and the troll then stood at 0.0 m/s for 75 s - it never reached
    /// the creature, never swung, never went back to work): the mob keeps going for the player (as an aggressive one does) and the answer says whether the TROLL killed it - "dead"
    /// or "alive". Logs where the troll is relative to the mob every 2 s, so a stalled troll shows. Expected outcome: "dead" once the mob is dead / gone.
    [TestHook("stt.guardKill")]
    private static string GuardKill(GameObject mob, GameObject troll)
    {
        Character c = mob != null ? mob.GetComponent<Character>() : null;
        if (c == null || c.IsDead())
        {
            return "dead";
        }

        MobTargetsPlayer(mob);
        if (Time.time >= _nextKillLog && troll != null)
        {
            _nextKillLog = Time.time + 2f;
            Vector3 d = mob.transform.position - troll.transform.position;
            d.y = 0f;
            MonsterAI ai = troll.GetComponent<MonsterAI>();
            Character t = ai != null ? TargetOf(ai) : null;
            TrollChase chase = troll.GetComponent<TrollChase>();
            ModLog.Info("CombatTestHooks: guard-kill - the " + Utils.GetPrefabName(mob) + " is " + d.magnitude.ToString("F1") +
                        " m from the troll, no-hit zone " + TrollNoHit.InZone(troll.transform.position) + ", swings " + (chase != null ? chase.Swings.ToString() : "-") + ", last blocker '" +
                        (chase != null ? chase.LastBlocker : "") + "', its target '" + (t != null ? Utils.GetPrefabName(t.gameObject) : "") + "', forced " +
                        TrollGuard.HasForced(troll.GetComponent<Character>()) + ", farmer " +
                        (troll.GetComponent<TrollFarmer>() != null) + ", combat " + CombatState(troll) + ", mob health " +
                        c.GetHealth().ToString("F0"));
        }

        return "alive";
    }

    /// What combat drives the troll now: rage / slam / chase / none.
    [TestHook("stt.combatState")]
    private static string CombatState(GameObject troll)
    {
        if (troll.GetComponent<TrollRage>() != null)
        {
            return "rage";
        }

        if (troll.GetComponent<TrollSlam>() != null)
        {
            return "slam";
        }

        if (troll.GetComponent<TrollChase>() != null)
        {
            return "chase";
        }

        return "none";
    }

    /// One swing attempt of the troll right now (the game's StartAttack through the mod's no-hit patch). Expected outcome: whether the game started an attack.
    [TestHook("stt.trySwing")]
    private static bool TrySwing(GameObject troll)
    {
        Humanoid h = troll.GetComponent<Humanoid>();
        ItemDrop.ItemData w = TrollWeapons.Pick(h, Attack.AttackType.Horizontal, false) ??
                              TrollWeapons.Pick(h, Attack.AttackType.Vertical, false);
        if (w != null && !h.IsItemEquiped(w))
        {
            h.EquipItem(w, false);
        }

        return h.StartAttack(null, false);
    }

    /// The troll's star level (1 = no star, 3 = 2 stars). Expected outcome: the level set.
    [TestHook("stt.setLevel")]
    private static int SetLevel(GameObject troll, int level)
    {
        Character c = troll.GetComponent<Character>();
        c.SetLevel(level);
        return c.GetLevel();
    }

    /// The troll may break fortress walls (D-124).
    [TestHook("stt.wallBreaker")]
    private static bool WallBreaker(GameObject troll)
    {
        return TrollWalls.Breaks(troll.GetComponent<Character>());
    }

    /// Queues `count` presses of the attack key (read by TrollOrders like a real press). Expected outcome: the presses queued.
    [TestHook("stt.attackKeyPress")]
    private static int AttackKeyPress(int count)
    {
        TrollOrders.FakePresses = Mathf.Clamp(count, 0, 5);
        return TrollOrders.FakePresses;
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
