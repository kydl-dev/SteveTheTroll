using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Combat.Infrastructure;
using SteveTheTroll.Trolls.Farming.Infrastructure;
using SteveTheTroll.Trolls.Orders.Infrastructure;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing
{
    /// <summary>
    /// D-171: TestLab hooks for combat, orders and unarmed trolls (suite tests defend-*, counter-*, guard-*, nohit-*, walls-*, attack-key-*, unarmed-*). Hits go through the
    /// game's own Character.Damage, so the mod's real patches (TrollDefend, TamedTrollStats -> TrollCounter) react exactly as in play.
    /// </summary>
    internal static class CombatTestHooks
    {
        static readonly AccessTools.FieldRef<MonsterAI, Character> TargetOf = AccessTools.FieldRefAccess<MonsterAI, Character>("m_targetCreature");
        static readonly System.Action<MonsterAI, Character> SetTarget = AccessTools.MethodDelegate<System.Action<MonsterAI, Character>>(AccessTools.Method(typeof(MonsterAI), "SetTarget"));

        static HitData Hit(Character attacker, float damage)
        {
            var h = new HitData();
            h.m_damage.m_blunt = damage;
            h.m_point = attacker != null ? attacker.transform.position : Vector3.zero;
            h.SetAttacker(attacker);
            return h;
        }

        /// The creature `attacker` hits the local player for `damage` (blunt). Expected outcome: the player's health afterwards.
        [TestHook("stt.hitPlayer")]
        static float HitPlayer(GameObject attacker, float damage)
        {
            var p = Player.m_localPlayer;
            var a = attacker != null ? attacker.GetComponent<Character>() : null;
            p.Damage(Hit(a, damage));
            ModLog.Info("CombatTestHooks: " + (a != null ? a.name : "?") + " hit the player for " + damage + " - health " + p.GetHealth().ToString("F0"));
            return p.GetHealth();
        }

        /// The creature `attacker` hits the troll for `damage` (blunt). Expected outcome: the troll's health afterwards.
        [TestHook("stt.hitTroll")]
        static float HitTroll(GameObject troll, GameObject attacker, float damage)
        {
            var t = troll.GetComponent<Character>();
            var a = attacker != null ? attacker.GetComponent<Character>() : null;
            t.Damage(Hit(a, damage));
            ModLog.Info("CombatTestHooks: " + (a != null ? a.name : "?") + " hit " + TrollTaming.Tag(t) + " for " + damage + " - health " + t.GetHealth().ToString("F0"));
            return t.GetHealth();
        }

        /// The creature's AI goes for the local player (as an aggressive mob would). Expected outcome: whether its target is the player now.
        [TestHook("stt.mobTargetsPlayer")]
        static bool MobTargetsPlayer(GameObject mob)
        {
            var ai = mob.GetComponent<MonsterAI>();
            if (ai == null) return ai != null;
            SetTarget(ai, Player.m_localPlayer);
            if (TargetOf(ai) != Player.m_localPlayer) TargetOf(ai) = Player.m_localPlayer;      // the game's SetTarget may refuse (alerted state / sight); the field is what TrollGuard reads
            ai.Alert();
            return TargetOf(ai) == Player.m_localPlayer;
        }

        /// The attack key's ray goes from the camera towards this object's collider centre instead of along the camera's forward axis (TrollOrders.AimOverride, one press).
        /// The lab's `face` turns the player but not the 3rd-person camera's pitch, so the ray missed small targets. Expected outcome: whether an aim was set.
        [TestHook("stt.aimAt")]
        static bool AimAt(GameObject target)
        {
            TrollOrders.AimOverride = target;
            return TrollOrders.AimOverride != null;
        }

        /// The prefab name of the creature the troll's AI targets ("" = none).
        [TestHook("stt.targetOf")]
        static string Target(GameObject troll)
        {
            var ai = troll.GetComponent<MonsterAI>();
            var t = ai != null ? TargetOf(ai) : null;
            return t != null ? Utils.GetPrefabName(t.gameObject) : "";
        }

        /// What combat drives the troll now: rage / slam / chase / none.
        [TestHook("stt.combatState")]
        static string CombatState(GameObject troll)
        {
            if (troll.GetComponent<TrollRage>() != null) return "rage";
            if (troll.GetComponent<TrollSlam>() != null) return "slam";
            if (troll.GetComponent<TrollChase>() != null) return "chase";
            return "none";
        }

        /// One swing attempt of the troll right now (the game's StartAttack through the mod's no-hit patch). Expected outcome: whether the game started an attack.
        [TestHook("stt.trySwing")]
        static bool TrySwing(GameObject troll)
        {
            var h = troll.GetComponent<Humanoid>();
            var w = TrollWeapons.Pick(h, Attack.AttackType.Horizontal, false) ?? TrollWeapons.Pick(h, Attack.AttackType.Vertical, false);
            if (w != null && !h.IsItemEquiped(w)) h.EquipItem(w, false);
            return h.StartAttack(null, false);
        }

        /// The troll's star level (1 = no star, 3 = 2 stars). Expected outcome: the level set.
        [TestHook("stt.setLevel")]
        static int SetLevel(GameObject troll, int level)
        {
            var c = troll.GetComponent<Character>();
            c.SetLevel(level);
            return c.GetLevel();
        }

        /// The troll may break fortress walls (D-124).
        [TestHook("stt.wallBreaker")]
        static bool WallBreaker(GameObject troll) { return TrollWalls.Breaks(troll.GetComponent<Character>()); }

        /// Queues `count` presses of the attack key (read by TrollOrders like a real press). Expected outcome: the presses queued.
        [TestHook("stt.attackKeyPress")]
        static int AttackKeyPress(int count)
        {
            TrollOrders.FakePresses = Mathf.Clamp(count, 0, 5);
            return TrollOrders.FakePresses;
        }

        /// The troll loses its log (D-121 "Unarmed" spawns): only its own body attacks are left. Expected outcome: how many items were removed.
        [TestHook("stt.disarm")]
        static int Disarm(GameObject troll)
        {
            var h = troll.GetComponent<Humanoid>();
            var inv = h.GetInventory();
            int n = 0;
            foreach (var it in new System.Collections.Generic.List<ItemDrop.ItemData>(inv.GetAllItems()))
            {
                string name = it.m_dropPrefab != null ? it.m_dropPrefab.name : it.m_shared.m_name;
                if (name.ToLowerInvariant().Contains("log")) { h.UnequipItem(it, false); inv.RemoveItem(it); n++; }
            }
            ModLog.Info("CombatTestHooks: " + TrollTaming.Tag(h) + " disarmed (" + n + " log item(s) removed, " + inv.GetAllItems().Count + " left)");
            return n;
        }

        /// The troll loses EVERY item (no attack at all). Expected outcome: how many items were removed.
        [TestHook("stt.stripWeapons")]
        static int StripWeapons(GameObject troll)
        {
            var h = troll.GetComponent<Humanoid>();
            var inv = h.GetInventory();
            int n = inv.GetAllItems().Count;
            h.UnequipAllItems();
            inv.RemoveAll();
            ModLog.Info("CombatTestHooks: " + TrollTaming.Tag(h) + " stripped of all " + n + " item(s)");
            return n;
        }

        /// The tool the farmer would swing ("" = none).
        [TestHook("stt.weaponOf")]
        static string WeaponOf(GameObject troll)
        {
            var h = troll.GetComponent<Humanoid>();
            var w = TrollWeapons.Pick(h, Attack.AttackType.Horizontal, true) ?? TrollWeapons.Pick(h, Attack.AttackType.Vertical, true);
            return w != null ? w.m_shared.m_name : "";
        }

        /// Removes every tamed troll (tests leave farming trolls behind; a stray one can chop the next test's tree). Expected outcome: how many were removed.
        [TestHook("stt.clearTrolls")]
        static int ClearTrolls()
        {
            int n = 0;
            foreach (var c in new System.Collections.Generic.List<Character>(Character.GetAllCharacters()))
            {
                if (c == null || !c.IsTamed() || !TrollTaming.IsTroll(c)) continue;
                var v = c.GetComponent<ZNetView>();
                if (v != null && v.IsValid()) { v.ClaimOwnership(); ZNetScene.instance.Destroy(c.gameObject); n++; }
            }
            ModLog.Info("CombatTestHooks: " + n + " tamed troll(s) removed before the test");
            return n;
        }

        /// Whether the troll has a farmer component (it farms / goes home / unloads).
        [TestHook("stt.hasFarmer")]
        static bool HasFarmer(GameObject troll) { return troll.GetComponent<TrollFarmer>() != null; }
    }
}
