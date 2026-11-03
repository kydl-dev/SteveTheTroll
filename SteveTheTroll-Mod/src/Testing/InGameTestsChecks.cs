using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Bootstrap;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Integrations.Unity;
using UnityEngine;

namespace SteveTheTroll.Testing
{
    /// <summary>
    /// D-100 (user: "we have to make sure it all works as it should"): in-game checks of the content and the bed rules. GameProbe runs every
    /// static string Startup_* / World_* of InGameTests (this file is the same class) and reads "PASS: / FAIL: / SKIP: detail".
    /// </summary>
    internal static partial class InGameTests
    {
        /// Every asset the mod loads (BundleAssets.All) loads from the real bundle with its type.
        static string World_BundleAssets()
        {
            if (Plugin.Bundle == null) return "FAIL: no AssetBundle loaded";
            var missing = new List<string>();
            foreach (var a in BundleAssets.All) if (Plugin.Bundle.LoadAsset(a.Key, a.Value) == null) missing.Add(a.Value.Name + " " + a.Key);
            string detail = BundleAssets.All.Length + " asset(s) checked, " + Plugin.Bundle.GetAllAssetNames().Length + " in the bundle" + (missing.Count > 0 ? "; MISSING: " + string.Join(", ", missing.ToArray()) : "");
            ModLog.Verbose("InGameTests: World_BundleAssets " + detail);
            return (missing.Count == 0 ? "PASS: " : "FAIL: ") + detail;
        }

        /// Every English text of the mod is known to the game's localization (the player never sees a raw key).
        static string World_Texts()
        {
            var l = Localization.instance;
            if (l == null) return "SKIP: no Localization yet";
            var raw = new List<string>();
            foreach (var key in Ui.UiText.En.Keys)
            {
                string shown = l.Localize("$" + key);
                if (string.IsNullOrEmpty(shown) || shown.Contains(key)) raw.Add(key);
            }
            string detail = Ui.UiText.En.Count + " text(s), " + raw.Count + " not localized" + (raw.Count > 0 ? ": " + string.Join(", ", raw.ToArray()) : "");
            ModLog.Verbose("InGameTests: World_Texts " + detail);
            return (raw.Count == 0 ? "PASS: " : "FAIL: ") + detail;
        }

        /// The game methods the bed / spawn / tooltip / costume rules hook into carry this mod's Harmony patch (a renamed game method would
        /// make a rule silently stop working).
        static string World_CriticalPatches()
        {
            var want = new List<KeyValuePair<string, MethodBase>>
            {
                Hook("SpawnSystem.UpdateSpawnList (spawn boost)", AccessTools.Method(typeof(SpawnSystem), "UpdateSpawnList")),
                Hook("SpawnSystem.IsSpawnPointGood (200 - 500 m ring)", AccessTools.Method(typeof(SpawnSystem), "IsSpawnPointGood")),
                Hook("SpawnSystem.Spawn (log + stars)", AccessTools.Method(typeof(SpawnSystem), "Spawn")),
                Hook("Humanoid.StartAttack (no-hit zone)", AccessTools.Method(typeof(Humanoid), nameof(Humanoid.StartAttack))),
                Hook("Game.Logout (trolls to the home ring)", AccessTools.Method(typeof(Game), nameof(Game.Logout))),
                Hook("SE_Stats.GetTooltipString (one stamina line)", AccessTools.Method(typeof(SE_Stats), nameof(SE_Stats.GetTooltipString))),
                Hook("Player.HaveRequirementItems (recipes unlock)", AccessTools.Method(typeof(Player), "HaveRequirementItems")),
                Hook("VisEquipment.UpdateEquipmentVisuals (costume body)", AccessTools.Method(typeof(VisEquipment), "UpdateEquipmentVisuals")),
            };
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(ItemDrop.ItemData)))
                if (m.Name == "GetTooltip" && m.IsStatic && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(ItemDrop.ItemData))
                    want.Add(Hook("ItemData.GetTooltip (set kept secret)", m));
            var bad = new List<string>();
            foreach (var w in want)
            {
                var info = w.Value != null ? Harmony.GetPatchInfo(w.Value) : null;
                if (info == null || !info.Owners.Contains(Plugin.ModGuid)) bad.Add(w.Key + (w.Value == null ? " (method not found)" : " (not patched)"));
            }
            string detail = want.Count + " hook(s) checked" + (bad.Count > 0 ? "; WRONG: " + string.Join(", ", bad.ToArray()) : "");
            ModLog.Verbose("InGameTests: World_CriticalPatches " + detail);
            return (bad.Count == 0 ? "PASS: " : "FAIL: ") + detail;
        }

        static KeyValuePair<string, MethodBase> Hook(string what, MethodBase m) { return new KeyValuePair<string, MethodBase>(what, m); }

        /// D-097: where trolls wait / are put on logout: at least HomeRadius from the bed, apart from each other, not under a roof.
        static string World_HomeSpots()
        {
            Vector3 bed;
            if (!TrollNoHit.Bed(out bed)) return "SKIP: no bed set";
            float r = ModSettings.HomeRadius.Value;
            var spots = new List<Vector3>();
            var sb = new System.Text.StringBuilder("home radius " + r.ToString("0") + " m; ");
            bool ok = true;
            int roofMask = LayerMask.GetMask("piece");
            bool loaded = ZoneSystem.instance != null && ZoneSystem.instance.IsZoneLoaded(bed);
            for (int i = 0; i < 6; i++)
            {
                var s = TrollHomeSpot.Spot(bed, i);
                float d = new Vector2(s.x - bed.x, s.z - bed.z).magnitude;
                bool apart = spots.TrueForAll(o => Vector3.Distance(o, s) >= 2f);
                bool roof = loaded && Physics.Raycast(s + Vector3.up, Vector3.up, 40f, roofMask);
                bool good = d >= r - 0.01f && apart && !roof;
                ok &= good;
                sb.Append("#" + (i + 1) + " " + d.ToString("0.0") + " m" + (roof ? " UNDER A ROOF" : "") + (apart ? "" : " TOO CLOSE TO ANOTHER") + "; ");
                spots.Add(s);
            }
            if (!loaded) sb.Append("(bed area not loaded: roofs not checked)");
            ModLog.Verbose("InGameTests: World_HomeSpots " + sb);
            return (ok ? "PASS: " : "FAIL: ") + sb;
        }

        /// D-097: the no-hit circle around the bed (inside at 5 m, outside past the radius) and the swing patch being in place.
        static string World_NoHitZone()
        {
            Vector3 bed;
            if (!TrollNoHit.Bed(out bed)) return "SKIP: no bed set";
            float r = ModSettings.NoHitRadius.Value;
            if (r <= 0f) return "SKIP: NoHitRadius is 0 (off)";
            bool inside = TrollNoHit.InZone(bed + new Vector3(Mathf.Min(5f, r * 0.5f), 0f, 0f));
            bool outside = !TrollNoHit.InZone(bed + new Vector3(0f, 0f, r + 5f));
            var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(Humanoid), nameof(Humanoid.StartAttack)));
            bool patched = info != null && info.Owners.Contains(Plugin.ModGuid);
            int own = 0, ownInside = 0;
            var p = Player.m_localPlayer;
            foreach (var c in Character.GetAllCharacters())
                if (c != null && c.IsTamed() && TrollTaming.IsTroll(c) && p != null && TrollTaming.Owner(c) == p.GetPlayerID())
                { own++; if (TrollNoHit.InZone(c.transform.position)) ownInside++; }
            string detail = "radius " + r.ToString("0") + " m: inside " + inside + ", outside " + outside + ", swing patch " + patched + "; own trolls loaded " + own + ", in the zone now " + ownInside;
            ModLog.Verbose("InGameTests: World_NoHitZone " + detail);
            return (inside && outside && patched ? "PASS: " : "FAIL: ") + detail;
        }

        // D-118 (D-160: EVERY loaded tamed troll, not the first): no elemental damage (fire, frost, lightning, poison, spirit Immune), pierce / slash at least Resistant.
        static string World_TrollElementalImmune()
        {
            int n = 0, bad = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var c in Character.GetAllCharacters())
            {
                if (!TamedTrollStats.IsTamedTroll(c)) continue;
                var m = c.GetDamageModifiers();
                int immune = TamedTrollStats.Immune(m);
                bool tough = m.m_pierce != HitData.DamageModifier.Normal && m.m_slash != HitData.DamageModifier.Normal;
                n++;
                if (immune != 5 || !tough) { bad++; sb.Append(c.name + ": immune " + immune + "/5, pierce " + m.m_pierce + ", slash " + m.m_slash + "; "); }
            }
            if (n == 0) return "SKIP: no tamed troll loaded";
            string detail = n + " tamed troll(s) checked, " + bad + " wrong " + sb;
            ModLog.Verbose("InGameTests: World_TrollElementalImmune " + detail);
            return (bad == 0 ? "PASS: " : "FAIL: ") + detail;
        }
    }
}
