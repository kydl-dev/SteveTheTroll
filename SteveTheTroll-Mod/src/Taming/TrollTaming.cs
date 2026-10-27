using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// T-008a (D-037 / D-038): the game's Troll gets the game's own Tameable (copied from the Wolf: effects, commandable = E follow / stay),
    /// with the taming time from ModSettings. Food taming stays off for trolls: TrollTamingPatch replaces Tameable.TamingUpdate for them.
    /// Owner = the player who tamed the troll, saved in the troll's ZDO (player id + name).
    /// </summary>
    internal static class TrollTaming
    {
        public const string TrollPrefab = "Troll";
        const string DonorPrefab = "Wolf";
        public const string OwnerKey = "stt_owner";
        public const string OwnerNameKey = "stt_owner_name";

        static readonly AccessTools.FieldRef<VisEquipment, int> UtilityHash = AccessTools.FieldRefAccess<VisEquipment, int>("m_currentUtilityItemHash");
        static readonly AccessTools.FieldRef<Character, ZNetView> CharView = AccessTools.FieldRefAccess<Character, ZNetView>("m_nview");
        static readonly List<Character> Trolls = new List<Character>();

        /// Expected outcome: subscribed to the vanilla prefab event (returns that the handler is queued).
        public static bool Install()
        {
            PrefabManager.OnVanillaPrefabsAvailable += AddTameable;
            ModLog.Verbose("TrollTaming: waiting for vanilla prefabs to make '" + TrollPrefab + "' tameable");
            return true;
        }

        static void AddTameable()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddTameable;
            var troll = PrefabManager.Instance.GetPrefab(TrollPrefab);
            var donorGo = PrefabManager.Instance.GetPrefab(DonorPrefab);
            var donor = donorGo != null ? donorGo.GetComponent<Tameable>() : null;
            ModLog.Verbose("TrollTaming: prefabs troll " + (troll != null) + ", donor " + DonorPrefab + " " + (donorGo != null) + " (Tameable " + (donor != null) + ")");
            if (troll == null) { ModLog.Error("TrollTaming: prefab '" + TrollPrefab + "' not found - no troll taming"); return; }
            var t = troll.GetComponent<Tameable>();
            bool existed = t != null;
            if (t == null) t = troll.AddComponent<Tameable>();
            if (donor != null)
            {
                // D-043: troll sounds only - the wolf's visual effects stay, its sounds (sfx_*) go, the troll's own idle growl is added
                var growl = troll.GetComponent<BaseAI>() != null ? troll.GetComponent<BaseAI>().m_idleSound : null;
                t.m_tamedEffect = TrollSounds(donor.m_tamedEffect, growl, "tamed");
                t.m_sootheEffect = TrollSounds(donor.m_sootheEffect, growl, "soothe");
                t.m_petEffect = TrollSounds(donor.m_petEffect, growl, "pet / command");
                t.m_unSummonEffect = TrollSounds(donor.m_unSummonEffect, null, "unsummon");
            }
            else
            {
                ModLog.Warn("TrollTaming: no " + DonorPrefab + " Tameable to copy effects from - empty effects");
                t.m_tamedEffect = new EffectList();
                t.m_sootheEffect = new EffectList();
                t.m_petEffect = new EffectList();
                t.m_unSummonEffect = new EffectList();
            }
            t.m_tamingTime = ModSettings.TameSeconds.Value;
            t.m_commandable = true;
            t.m_startsTamed = false;
            t.m_fedDuration = 0f;
            ModLog.Info("TrollTaming: " + TrollPrefab + " tameable (component " + (existed ? "existed" : "added") + ", taming " + t.m_tamingTime + " s, commandable " + t.m_commandable + ", effects from " + (donor != null ? DonorPrefab : "none") + ")");
            ModLog.Check("TrollTaming: troll taming time", t.m_tamingTime, ModSettings.TameSeconds.Value, 0.001, "s");
        }

        /// The donor's effects without its sounds (prefab names starting with "sfx"), plus the troll's growl. Expected outcome: a new list.
        static EffectList TrollSounds(EffectList donor, EffectList growl, string what)
        {
            var list = new List<EffectList.EffectData>();
            int dropped = 0, added = 0;
            if (donor != null && donor.m_effectPrefabs != null)
                foreach (var e in donor.m_effectPrefabs)
                {
                    if (e == null || e.m_prefab == null) continue;
                    if (e.m_prefab.name.StartsWith("sfx", System.StringComparison.OrdinalIgnoreCase)) { dropped++; continue; }
                    list.Add(e);
                }
            if (growl != null && growl.m_effectPrefabs != null)
                foreach (var e in growl.m_effectPrefabs) if (e != null && e.m_prefab != null) { list.Add(e); added++; }
            ModLog.Verbose("TrollTaming: " + what + " effect: " + list.Count + " part(s), wolf sounds removed " + dropped + ", troll growl parts " + added);
            if (growl != null) ModLog.CheckRange("TrollTaming: troll growl parts in the " + what + " effect", added, 1, 100, "");
            return new EffectList { m_effectPrefabs = list.ToArray() };
        }

        /// The troll's prefab is Troll (not Troll_Summoned or other mods' trolls).
        public static bool IsTroll(Component c)
        {
            return c != null && Utils.GetPrefabName(c.gameObject) == TrollPrefab;
        }

        /// The character's ZDO (null when not networked yet).
        public static ZDO Zdo(Character c)
        {
            var nview = c != null ? CharView(c) : null;
            return nview != null && nview.IsValid() ? nview.GetZDO() : null;
        }

        /// Player id of the troll's owner (0 = none).
        public static long Owner(Character troll)
        {
            var zdo = Zdo(troll);
            return zdo != null ? zdo.GetLong(OwnerKey, 0L) : 0L;
        }

        /// Expected outcome: the troll's ZDO names the player as owner (returns that check).
        public static bool SetOwner(Character troll, Player player)
        {
            var zdo = Zdo(troll);
            if (zdo == null || player == null) { ModLog.Warn("TrollTaming: cannot set the owner (zdo " + (zdo != null) + ", player " + (player != null) + ")"); return zdo != null && player != null; }
            zdo.Set(OwnerKey, player.GetPlayerID());
            zdo.Set(OwnerNameKey, player.GetPlayerName());
            ModLog.Verbose("TrollTaming: " + troll.name + " owner = " + player.GetPlayerName() + " (" + player.GetPlayerID() + ")");
            return zdo.GetLong(OwnerKey, 0L) == player.GetPlayerID();
        }

        /// The player wears the slippers (any player: VisEquipment carries the synced utility hash).
        public static bool Wears(Player p)
        {
            bool ready = p != null && Item.SlipperItem.Hash != 0 && ModSettings.On;
            if (!ready) return ready;
            var vis = p.GetComponent<VisEquipment>();
            return vis != null && UtilityHash(vis) == Item.SlipperItem.Hash;
        }

        /// Nearest slipper wearer to pos (any distance; null if none) and its distance in metres (-1 if none).
        public static Player NearestWearer(Vector3 pos, out float distance)
        {
            Player best = null;
            distance = -1f;
            foreach (var p in Player.GetAllPlayers())
            {
                if (p == null || p.IsDead() || !Wears(p)) continue;
                float d = Vector3.Distance(pos, p.transform.position);
                if (best == null || d < distance) { best = p; distance = d; }
            }
            return best;
        }

        /// The player's own tamed troll among the loaded characters, nearest to the player (null if none loaded); distance in metres (-1).
        public static Character OwnTroll(Player p, out float distance)
        {
            Character best = null;
            distance = -1f;
            if (p == null) return null;
            long id = p.GetPlayerID();
            Trolls.Clear();
            foreach (var c in Character.GetAllCharacters()) if (c != null && !c.IsDead() && c.IsTamed() && IsTroll(c) && Owner(c) == id) Trolls.Add(c);
            foreach (var c in Trolls)
            {
                float d = Vector3.Distance(p.transform.position, c.transform.position);
                if (best == null || d < distance) { best = c; distance = d; }
            }
            return best;
        }
    }
}
