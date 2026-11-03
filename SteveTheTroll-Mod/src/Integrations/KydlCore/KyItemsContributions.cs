using System.Collections.Generic;
using Jotunn.Managers;
using Kydl.Common;
using KydlCore;
using UnityEngine;

namespace SteveTheTroll.Integrations.KydlCoreItems
{
    /// <summary>
    /// SteveTheTroll's items and pieces in KydlCore's KY-Items registry (MIGRATION_DECISIONS B13, PLAN T-210 step 4): the slippers, the costume parts, the Troll
    /// Club and the Troll Hub, each as "stevethetroll:&lt;item&gt;". The mod still adds the prefabs to the game itself (Jotunn, RegisterInGame = false); this only
    /// tells the registry about them. A feature asks with <see cref="Want"/> in Start and <see cref="Drop"/> in Stop; the contribution is made as soon as the
    /// prefab exists (at once, or when Jotunn has registered the prefabs) and only once (KyItems rejects duplicates with a warning).
    /// </summary>
    internal static class KyItemsContributions
    {
        sealed class Wish { internal string Id, Prefab; internal KyKind Kind; }
        static readonly Dictionary<string, Wish> Wanted = new Dictionary<string, Wish>();
        static bool _hooked;

        internal static string IdOf(string item) { return "stevethetroll:" + item; }

        /// Wants the prefab listed under "stevethetroll:&lt;item&gt;". Expected outcome: true when it is listed now or will be once the prefab exists.
        internal static bool Want(string item, string prefabName, KyKind kind)
        {
            string id = IdOf(item);
            Wanted[id] = new Wish { Id = id, Prefab = prefabName, Kind = kind };
            if (!_hooked) { _hooked = true; PrefabManager.OnPrefabsRegistered += ContributePending; }
            bool now = TryContribute(Wanted[id]);
            ModLog.Verbose("KyItemsContributions: want " + id + " (" + kind + " '" + prefabName + "') - " + (now ? "listed" : "waiting for the prefab"));
            return Wanted.ContainsKey(id);
        }

        /// No longer lists "stevethetroll:&lt;item&gt;" (its feature stopped). Expected outcome: the id is not listed any more.
        internal static bool Drop(string item)
        {
            string id = IdOf(item);
            Wanted.Remove(id);
            bool gone = KyItems.FindById(id) == null || KyItems.Withdraw(id);
            ModLog.Verbose("KyItemsContributions: drop " + id + " - " + (gone ? "not listed" : "STILL listed"));
            return gone && !Wanted.ContainsKey(id);
        }

        /// Is "stevethetroll:&lt;item&gt;" listed in KY-Items now? (test hooks)
        internal static bool Listed(string item) { return KyItems.FindById(IdOf(item)) != null; }

        static void ContributePending()
        {
            int listed = 0;
            foreach (var w in new List<Wish>(Wanted.Values)) if (TryContribute(w)) listed++;
            ModLog.Verbose("KyItemsContributions: prefabs registered - " + listed + " of " + Wanted.Count + " wanted id(s) listed");
        }

        static bool TryContribute(Wish w)
        {
            if (KyItems.FindById(w.Id) != null) return true;
            GameObject prefab = PrefabManager.Instance != null ? PrefabManager.Instance.GetPrefab(w.Prefab) : null;
            if (prefab == null) return false;
            var r = KyItems.Contribute(new KyContribution { Id = w.Id, ModGuid = Plugin.ModGuid, Prefab = prefab, Kind = w.Kind, RegisterInGame = false, Tags = new[] { "SteveTheTroll" } });
            if (!r.Accepted) ModLog.Warn("KyItemsContributions: KY-Items refused " + w.Id + ": " + r.Reason);
            return r.Accepted && KyItems.FindById(w.Id) != null;
        }
    }
}
