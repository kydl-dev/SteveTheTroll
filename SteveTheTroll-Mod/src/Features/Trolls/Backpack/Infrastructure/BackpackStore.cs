using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Backpack.Infrastructure
{
    /// <summary>
    /// T-008c (D-062): the 4 chest inventories of a tamed troll, one per reinforced chest of the backpack, normal stacks (D-058). Saved in the
    /// troll's ZDO as byte arrays under stt_bp0 .. stt_bp3; size = the game's reinforced chest (piece_chest Container, 6 x 4 in 1.0.20).
    /// The game allows ONE Container per networked object (it registers its RPCs there), so each troll gets one Container
    /// ("STT_BackpackStore", child of the troll, ZNetView = the troll's) that shows the chest the player opened: its Load / Save are
    /// redirected to that chest's key. Opening, "take all", in-use and ownership hand-over stay the game's own code.
    /// </summary>
    internal static class BackpackStore
    {
        internal const int ChestCount = 4;
        const string ChestPrefab = "piece_chest", StoreName = "STT_BackpackStore";
        static readonly int[] Keys = { "stt_bp0".GetStableHashCode(), "stt_bp1".GetStableHashCode(), "stt_bp2".GetStableHashCode(), "stt_bp3".GetStableHashCode() };
        static readonly AccessTools.FieldRef<Container, uint> LastRevision = AccessTools.FieldRefAccess<Container, uint>("m_lastRevision");
        static readonly AccessTools.FieldRef<Container, bool> Loading = AccessTools.FieldRefAccess<Container, bool>("m_loading");
        static readonly AccessTools.FieldRef<Container, bool> InUse = AccessTools.FieldRefAccess<Container, bool>("m_inUse");
        static readonly AccessTools.FieldRef<Inventory, string> InvName = AccessTools.FieldRefAccess<Inventory, string>("m_name");
        static readonly AccessTools.FieldRef<Container, Inventory> Inv = AccessTools.FieldRefAccess<Container, Inventory>("m_inventory");
        static readonly System.Func<Container, bool> LoadNow = AccessTools.MethodDelegate<System.Func<Container, bool>>(AccessTools.Method(typeof(Container), "Load"));
        static readonly Dictionary<Container, int> Active = new Dictionary<Container, int>();
        static int _w = 6, _h = 4;
        static Sprite _bkg;
        static Container _source;

        /// In-game test Startup_TamingReflection. Expected outcome: whether every private member was found.
        internal static bool SelfCheck() { return LoadNow != null; }

        static void Size()
        {
            if (_source != null) return;
            var p = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(ChestPrefab) : null;
            _source = p != null ? p.GetComponent<Container>() : null;
            if (_source == null) { ModLog.Warn("BackpackStore: '" + ChestPrefab + "' has no Container - chests are " + _w + " x " + _h); return; }
            _w = _source.m_width; _h = _source.m_height; _bkg = _source.m_bkg;
            ModLog.Info("BackpackStore: chest size from " + ChestPrefab + ": " + _w + " x " + _h);
        }

        internal static string Name(int i) { return Loc.T("stevethetroll_backpack_chest", (i + 1).ToString()); }

        /// Expected outcome: chest i of the troll as a new Inventory (empty when nothing is saved).
        internal static Inventory Read(ZDO zdo, int i)
        {
            Size();
            var inv = new Inventory(Name(i), _bkg, _w, _h);
            var data = zdo != null ? zdo.GetByteArray(Keys[i], null) : null;
            if (data != null && data.Length > 0) inv.Load(new ZPackage(data));
            return inv;
        }

        /// Owner of the troll's ZDO only.
        internal static void Write(ZDO zdo, int i, Inventory inv)
        {
            var pkg = new ZPackage();
            inv.Save(pkg);
            zdo.Set(Keys[i], pkg.GetArray());
            ForgetFull(zdo);
        }

        /// D-096 (user: "show how full is their backpack x stacks / y total stacks"): used slots (stacks) and all slots of the 4 chests.
        /// Expected outcome: used 0 .. slots; slots = 4 x the reinforced chest's size (0 / 0 without a ZDO).
        internal static void Fill(ZDO zdo, out int used, out int slots)
        {
            used = 0; slots = 0;
            if (zdo == null) return;
            for (int i = 0; i < ChestCount; i++)
            {
                var inv = Read(zdo, i);
                used += inv.NrOfItems();
                slots += inv.GetWidth() * inv.GetHeight();
            }
        }

        /// No chest has an empty slot. Expected outcome: true = full.
        static readonly System.Collections.Generic.Dictionary<ZDOID, KeyValuePair<float, bool>> FullCache = new System.Collections.Generic.Dictionary<ZDOID, KeyValuePair<float, bool>>();

        /// D-144: IsFull reads and deserializes all chests; idle check and pickup ask every second, so the answer is reused for CacheSeconds.
        /// A pickup that fills the last slot (TryAdd) and a depot unload drop the entry, so the answer is never stale after a change of ours.
        internal static bool IsFullCached(ZDO zdo, float cacheSeconds = 5f)
        {
            if (zdo == null) return false;
            KeyValuePair<float, bool> e;
            if (FullCache.TryGetValue(zdo.m_uid, out e) && Time.time - e.Key < cacheSeconds) return e.Value;
            bool full = IsFull(zdo);
            if (FullCache.Count > 200) FullCache.Clear();
            FullCache[zdo.m_uid] = new KeyValuePair<float, bool>(Time.time, full);
            return full;
        }

        internal static void ForgetFull(ZDO zdo) { if (zdo != null) FullCache.Remove(zdo.m_uid); }

        internal static bool IsFull(ZDO zdo)
        {
            if (zdo == null) return zdo != null;
            return BackpackRules.Full(ChestCount, i => Read(zdo, i).HaveEmptySlot());      // D-171: the rule is game-free
        }

        /// Puts the item (whole stack) into the first chest with room (owner of the troll's ZDO). Expected outcome: the chest index or -1.
        internal static int TryAdd(ZDO zdo, ItemDrop.ItemData item, Container store)
        {
            for (int i = 0; i < ChestCount; i++)
            {
                var inv = Read(zdo, i);
                if (!inv.CanAddItem(item)) continue;
                if (!inv.AddItem(item.Clone())) continue;
                Write(zdo, i, inv);
                if (store != null) LastRevision(store) = uint.MaxValue;     // the shown chest re-reads
                return i;
            }
            return -1;
        }

        /// The troll's store Container, created once per troll object on every client (the RPCs must exist where the owner is).
        /// Expected outcome: the Container, or null without a valid ZNetView.
        internal static Container Store(Character troll)
        {
            if (troll == null) return null;
            var t = troll.transform.Find(StoreName);
            if (t != null) return t.GetComponent<Container>();
            var view = troll.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return null;
            Size();
            var go = new GameObject(StoreName);
            go.SetActive(false);                       // Container.Awake runs on activation, after the fields are set
            go.transform.SetParent(troll.transform, false);
            var c = go.AddComponent<Container>();
            c.m_name = Name(0); c.m_bkg = _bkg; c.m_width = _w; c.m_height = _h;
            c.m_rootObjectOverride = view;
            c.m_privacy = Container.PrivacySetting.Public;
            c.m_checkGuardStone = false;
            c.m_autoDestroyEmpty = false;
            if (_source != null) { c.m_openEffects = _source.m_openEffects; c.m_closeEffects = _source.m_closeEffects; }
            Active[c] = 0;
            go.SetActive(true);
            ModLog.Info("BackpackStore: " + TrollTaming.Tag(troll) + " store ready (" + ChestCount + " chests of " + _w + " x " + _h + ")");
            return c;
        }

        /// E on chest i of the troll's backpack. Expected outcome: whether the game's open request was sent.
        internal static bool Open(Character troll, int i, Humanoid user)
        {
            var c = Store(troll);
            if (c == null) { ModLog.Warn("BackpackStore: no store on " + (troll != null ? TrollTaming.Tag(troll) : "?")); return false; }
            if (InUse(c)) { if (user != null) user.Message(MessageHud.MessageType.Center, "$msg_inuse"); return false; }
            Active[c] = i;
            // D-063 (TEST-027: the window said "Backpack chest 1" for every chest - the game shows the INVENTORY's name, set once in Awake)
            var tame = troll.GetComponent<Tameable>();
            string title = Loc.T("stevethetroll_backpack_hover", tame != null ? tame.GetHoverName() : troll.name, (i + 1).ToString());
            c.m_name = title;
            InvName(Inv(c)) = title;
            LastRevision(c) = uint.MaxValue;
            LoadNow(c);
            ModLog.Info("BackpackStore: " + TrollTaming.Tag(troll) + " chest " + (i + 1) + " opened by " + (user != null ? user.GetHoverName() : "?") + " (" + Inv(c).NrOfItems() + " stack(s))");
            return c.Interact(user, false, false);
        }

        /// Someone has one of the troll's chests open (no pick-up into it meanwhile).
        internal static bool Busy(Container store) { return store != null && InUse(store); }

        // Harmony hook: src/Integrations/Harmony/TrollsBackpackPatches.cs, BackpackStore_LoadPatch (MIGRATION_DECISIONS A5)
        internal static class LoadPatch
        {
            internal static bool Prefix(Container __instance, ref bool __result)
            {
                int i;
                if (!Active.TryGetValue(__instance, out i)) return true;
                var zdo = __instance.m_rootObjectOverride != null ? __instance.m_rootObjectOverride.GetZDO() : null;
                __result = false;
                if (zdo == null || zdo.DataRevision == LastRevision(__instance) || InUse(__instance)) return false;
                LastRevision(__instance) = zdo.DataRevision;
                var data = zdo.GetByteArray(Keys[i], null);
                var inv = Inv(__instance);
                Loading(__instance) = true;
                try { inv.RemoveAll(); if (data != null && data.Length > 0) inv.Load(new ZPackage(data)); }
                finally { Loading(__instance) = false; }
                __result = true;
                return false;
            }
        }

        // Harmony hook: src/Integrations/Harmony/TrollsBackpackPatches.cs, BackpackStore_SavePatch (MIGRATION_DECISIONS A5)
        internal static class SavePatch
        {
            internal static bool Prefix(Container __instance)
            {
                int i;
                if (!Active.TryGetValue(__instance, out i)) return true;
                var zdo = __instance.m_rootObjectOverride != null ? __instance.m_rootObjectOverride.GetZDO() : null;
                if (zdo == null) return false;
                Write(zdo, i, Inv(__instance));
                LastRevision(__instance) = zdo.DataRevision;
                ModLog.Verbose("BackpackStore: chest " + (i + 1) + " saved (" + Inv(__instance).NrOfItems() + " stack(s))");
                return false;
            }
        }
    }
}
