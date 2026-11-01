using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Testing
{
    /// <summary>D-160: more automatic checks (same InGameTests class, found by GameProbe by name): critical patches, depot / hub prefabs, every own troll, slipper sockets, the runtime health counts.</summary>
    internal static partial class InGameTests
    {
        /// The Harmony patches the gameplay depends on are installed by this mod.
        static string Startup_CriticalPatches()
        {
            var wanted = new object[][]
            {
                new object[] { typeof(Container), "Save" }, new object[] { typeof(Container), "SetInUse" }, new object[] { typeof(Humanoid), nameof(Humanoid.StartAttack) },
            };
            var sb = new System.Text.StringBuilder();
            int missing = 0;
            foreach (var w in wanted)
            {
                var m = AccessTools.Method((System.Type)w[0], (string)w[1]);
                var info = m != null ? Harmony.GetPatchInfo(m) : null;
                bool ok = info != null && (info.Postfixes.Count + info.Prefixes.Count + info.Finalizers.Count + info.Transpilers.Count) > 0 && info.Owners.Contains(Plugin.ModGuid);
                if (!ok) { missing++; sb.Append(((System.Type)w[0]).Name + "." + w[1] + (m == null ? " (method not found)" : " (not patched by us)") + "; "); }
            }
            string detail = wanted.Length + " critical patch point(s), " + missing + " missing " + sb;
            ModLog.Verbose("InGameTests: Startup_CriticalPatches " + detail);
            return (missing == 0 ? "PASS: " : "FAIL: ") + detail;
        }

        /// The depot frame, its chest prefab and the hub piece exist with a ZNetView (a piece without one is never saved).
        static string World_DepotAndHubPrefabs()
        {
            var zs = ZNetScene.instance;
            if (zs == null) return "SKIP: no ZNetScene";
            var sb = new System.Text.StringBuilder();
            bool ok = true;
            foreach (var n in new[] { Backpack.DepotPrefab.Name, Backpack.DepotData.ChestPrefab, Hub.HubData.PrefabName })
            {
                var p = zs.GetPrefab(n);
                bool good = p != null && p.GetComponent<ZNetView>() != null;
                ok &= good;
                sb.Append(n + (good ? " ok; " : p == null ? " MISSING; " : " has no ZNetView; "));
            }
            ModLog.Verbose("InGameTests: World_DepotAndHubPrefabs " + sb);
            return (ok ? "PASS: " : "FAIL: ") + sb;
        }

        /// EVERY loaded own tamed troll has its AI parts and readable data, and its hub assignment points at a hub that exists.
        static string World_OwnTrolls()
        {
            var p = Player.m_localPlayer;
            if (p == null) return "SKIP: no local player";
            long me = p.GetPlayerID();
            int n = 0, bad = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || !c.IsTamed() || !Taming.TrollTaming.IsTroll(c) || Taming.TrollTaming.Owner(c) != me) continue;
                n++;
                var view = c.GetComponent<ZNetView>();
                string why = null;
                if (c.GetComponent<MonsterAI>() == null) why = "no MonsterAI";
                else if (c.GetComponent<Humanoid>() == null) why = "no Humanoid";
                else if (view == null || !view.IsValid()) why = "no valid ZNetView";
                else
                {
                    int assigned = view.GetZDO().GetInt(Hub.HubRegistry.AssignKey, 0);
                    if (assigned > 0 && Hub.HubRegistry.Find(me, assigned) == null) why = "assigned to hub " + assigned + " which does not exist";
                }
                if (why != null) { bad++; sb.Append(Taming.TrollTaming.Tag(c) + ": " + why + "; "); }
            }
            if (n == 0) return "SKIP: no own tamed troll loaded";
            string detail = n + " own troll(s) checked, " + bad + " wrong " + sb;
            ModLog.Verbose("InGameTests: World_OwnTrolls " + detail);
            return (bad == 0 ? "PASS: " : "FAIL: ") + detail;
        }

        /// The read-only runtime monitor (RuntimeHealth) has found nothing wrong with the player's trolls in its last pass.
        static string World_RuntimeHealth()
        {
            if (RuntimeHealth.Checked == 0) return "SKIP: the monitor has not seen an own troll yet";
            string detail = RuntimeHealth.Checked + " troll(s) in the last pass, " + RuntimeHealth.LastProblems + " problem(s) now, " + RuntimeHealth.Problems + " reported since start" + (RuntimeHealth.LastText.Length > 0 ? ": " + RuntimeHealth.LastText : "");
            return (RuntimeHealth.LastProblems == 0 ? "PASS: " : "FAIL: ") + detail;
        }
    }
}
