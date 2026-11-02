using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;
using Kydl.Common;
using SteveTheTroll.Items.Costume;
using SteveTheTroll.Items.Slippers;
using SteveTheTroll.Trolls.Registry.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Trolls.Taming.Infrastructure
{
    /// <summary>
    /// T-008a (D-037 / D-038): the game's Troll gets the game's own Tameable (copied from the Wolf: effects, commandable = E follow / stay),
    /// with the taming time from ModSettings. Food taming stays off for trolls: TrollTamingPatch replaces Tameable.TamingUpdate for them.
    /// Owner = the player who tamed the troll, saved in the troll's ZDO (player id + name).
    /// </summary>
    internal static class TrollTaming
    {
        static readonly Dictionary<int, KeyValuePair<float, string>> Tags = new Dictionary<int, KeyValuePair<float, string>>();

        /// The ONE place for "what is this troll called": its Tameable text (Steve 1), else the object name. Used by messages, orders and Tag.
        internal static string ShownName(Component any)
        {
            var t = any != null ? any.GetComponent<Tameable>() : null;
            string n = "";
            // D-148: Tameable.GetText throws a NullReferenceException while the troll is still in Character.Awake (the log lines of the max-health patch run there); that
            // exception aborted the troll's Awake - Steve 1 loaded without his club and stood still (test run 011447). Never throw out of a name lookup.
            try { n = t != null ? t.GetText() : ""; } catch (System.NullReferenceException) { n = ""; }
            return string.IsNullOrEmpty(n) ? (any != null ? any.gameObject.name : "?") : n;
        }

        /// D-145: how a troll is named in every log line: "Steve 1 [00c789c2d10d]" - its shown name and its permanent id (given at taming, TrollRegistry.IdOf), so two trolls
        /// with the same name stay apart and one id can be followed through the whole log. Read-only (never writes the id); cached for 5 s.
        internal static string Tag(Component any)
        {
            if (any == null) return "?";
            int key = any.gameObject.GetInstanceID();
            KeyValuePair<float, string> e;
            if (Tags.TryGetValue(key, out e) && Time.time - e.Key < 5f) return e.Value;
            string tag = any.gameObject.name;
            ZDO z = null;
            try { z = Zdo(any.GetComponent<Character>()); } catch (System.NullReferenceException) { }      // D-148: not ready yet (Awake)
            string shown = ShownName(any);
            string id = z != null ? z.GetString(TrollRegistry.IdKey, "") : "";
            if (shown.Length > 0 || id.Length > 0) tag = (shown.Length > 0 ? shown : tag) + (id.Length > 0 ? " [" + id + "]" : "");
            if (Tags.Count > 300) Tags.Clear();
            if (id.Length > 0) Tags[key] = new KeyValuePair<float, string>(Time.time, tag);      // D-148: an unready troll (no id yet) is not cached
            return tag;
        }

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
                // D-044: nothing of the wolf at all (its "fx_" prefabs carried the sounds too): the tamed moment = the troll's idle growl only;
                // follow / stay / pet / taming progress = no effect, the game's text message is enough (user)
                var growl = troll.GetComponent<BaseAI>() != null ? troll.GetComponent<BaseAI>().m_idleSound : null;
                t.m_tamedEffect = TrollSounds(null, growl, "tamed");
                t.m_sootheEffect = new EffectList();
                t.m_petEffect = new EffectList();
                t.m_unSummonEffect = new EffectList();
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
            ModLog.Info("TrollTaming: " + TrollPrefab + " tameable (component " + (existed ? "existed" : "added") + ", taming " + t.m_tamingTime + " s, commandable " + t.m_commandable + ", effects: troll growl when tamed, nothing on follow / stay / pet)");
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

        /// D-066 (TEST-029: Mouse 4 / Go farm right after E-follow - "got a follow order" again at once): the game keeps the follow order
        /// in the ZDO too (s_follow, the player's name) and puts it back; clearing it means clearing both. Owner of the ZDO only.
        public static void StopFollow(MonsterAI ai)
        {
            if (ai == null) return;
            ai.SetFollowTarget(null);
            var view = ai.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.IsOwner()) view.GetZDO().Set(ZDOVars.s_follow, "");
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
            ModLog.Verbose("TrollTaming: " + TrollTaming.Tag(troll) + " owner = " + player.GetPlayerName() + " (" + player.GetPlayerID() + ")");
            return zdo.GetLong(OwnerKey, 0L) == player.GetPlayerID();
        }

        /// The player wears the slippers (any player: VisEquipment carries the synced utility hash).
        public static bool Wears(Player p)
        {
            bool ready = p != null && SlipperItem.Hash != 0 && ModSettings.On;
            if (!ready) return ready;
            var vis = p.GetComponent<VisEquipment>();
            return vis != null && UtilityHash(vis) == SlipperItem.Hash;
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

        /// D-076: how many of the player's own tamed trolls are loaded within range metres of the player.
        public static int OwnTrollCount(Player p, float range)
        {
            if (p == null) return 0;
            long id = p.GetPlayerID();
            int n = 0;
            foreach (var c in Character.GetAllCharacters())
                if (c != null && !c.IsDead() && c.IsTamed() && IsTroll(c) && Owner(c) == id && Vector3.Distance(p.transform.position, c.transform.position) <= range) n++;
            return n;
        }

        /// D-076: own tamed trolls a wearer may have: 1, or CostumeItems.SetTrollLimit (5) while the set effect "Steve The Troll" is on.
        public static int TrollLimit(Player p)
        {
            int h = CostumeItems.SetEffectHash;
            return p != null && h != 0 && p.GetSEMan() != null && p.GetSEMan().HaveStatusEffect(h) ? CostumeItems.SetTrollLimit : 1;
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
