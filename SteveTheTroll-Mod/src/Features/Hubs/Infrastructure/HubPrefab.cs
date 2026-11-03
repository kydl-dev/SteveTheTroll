using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Kydl.Common;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Integrations.Unity;
using UnityEngine;

namespace SteveTheTroll.Features.Hubs.Infrastructure
{
    /// <summary>
    /// D-135: the Troll Hub as our own networked prefab "STT_TrollHub" and as a hammer piece (category KY-Items, recipe HubData.Recipe, every item
    /// recoverable: removing it gives back exactly the recipe). Built from the game's own pieces once their prefabs exist (meshes + solid colliders,
    /// nothing that wears: no WearNTear, so nothing in the world can damage it) plus the sign (the game's sign board with HubSign instead of Sign).
    /// </summary>
    internal static class HubPrefab
    {
        internal static void Install() { PrefabManager.OnVanillaPrefabsAvailable += Build; }

        static void Build()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Build;
            try { Make(); }
            catch (System.Exception e) { ModLog.Error("HubPrefab: building the Troll Hub failed: " + e); }
        }

        internal const string IconResource = "stevethetroll_hub_icon.png";

        static void Make()
        {
            var go = PrefabManager.Instance.CreateEmptyPrefab(HubData.PrefabName, false);
            foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
            foreach (var r in go.GetComponents<MeshRenderer>()) Object.DestroyImmediate(r);
            foreach (var f in go.GetComponents<MeshFilter>()) Object.DestroyImmediate(f);
            var view = go.GetComponent<ZNetView>();
            if (view == null) view = go.AddComponent<ZNetView>();
            view.m_persistent = true;
            int parts = 0, cols = 0, missing = 0;
            foreach (var p in HubData.Parts)
            {
                var prefab = PrefabManager.Instance.GetPrefab(p.Prefab);
                if (prefab == null) { missing++; ModLog.Warn("HubPrefab: piece '" + p.Prefab + "' not found"); continue; }
                int r;
                var copy = BackpackVisual.VisualCopy(prefab, out r);
                copy.transform.SetParent(go.transform, false);
                copy.transform.localPosition = new Vector3(p.X, p.Y, p.Z);
                copy.transform.localRotation = Quaternion.Euler(0f, p.Yaw, 0f);
                copy.transform.localScale = Vector3.one * p.Scale;
                cols += PieceCopy.Colliders(prefab, copy.transform);
                parts++;
            }
            int lanterns = AddLanterns(go);
            bool sign = AddSign(go);
            go.AddComponent<TrollHubPiece>();
            var piece = go.AddComponent<Piece>();
            piece.m_name = "Troll Hub";
            piece.m_canBeRemoved = true;
            piece.m_groundOnly = true;
            piece.m_canRotate = true;
            // T-200 (user 2026-11-03: "a better hub icon ... or simply the word HUB"): the word HUB on a wooden plaque, embedded in the DLL; the stone floor icon only as a fallback
            var icon = EmbeddedImages.Sprite(IconResource);
            piece.m_icon = icon != null ? icon : IconOf("stone_floor_2x2");
            var cfg = new PieceConfig
            {
                Name = Loc.Token("stevethetroll_hub_piece"),
                Description = Loc.Token("stevethetroll_hub_piece_desc"),
                PieceTable = PieceTables.Hammer,
                Category = KyCategory.For(HubData.PrefabName),
                Icon = piece.m_icon,
            };
            var reqs = new RequirementConfig[HubData.Recipe.Length];
            for (int i = 0; i < reqs.Length; i++) reqs[i] = new RequirementConfig(HubData.Recipe[i].Item, HubData.Recipe[i].Amount, 0, true);
            cfg.Requirements = reqs;
            PieceManager.Instance.AddPiece(new CustomPiece(go, false, cfg));
            ModLog.Info("HubPrefab: " + HubData.PrefabName + " built: " + parts + " of " + HubData.Parts.Length + " part(s), " + cols + " collider(s), " + missing + " missing, " + lanterns + " lantern(s), sign " + sign);
            ModLog.Check("HubPrefab: parts", parts, HubData.Parts.Length, 0, "");
        }

        /// The user's model has four lanterns (prefab Lantern_DN, a dropped item - the build dump only lists pieces) hanging in the air.
        const string LanternPrefab = "Lantern_DN";

        static int AddLanterns(GameObject go)
        {
            var lantern = PrefabManager.Instance.GetPrefab(LanternPrefab);
            if (lantern == null) { ModLog.Warn("HubPrefab: lantern '" + LanternPrefab + "' not found"); return 0; }
            int n = 0;
            foreach (var p in HubData.Lanterns)
            {
                int r;
                var copy = BackpackVisual.VisualCopy(lantern, out r);
                copy.transform.SetParent(go.transform, false);
                copy.transform.localPosition = new Vector3(p.X, p.Y, p.Z);
                copy.transform.localRotation = Quaternion.Euler(0f, p.Yaw, 0f);
                n++;
            }
            return n;
        }

        static Sprite IconOf(string pieceName)
        {
            var p = PrefabManager.Instance.GetPrefab(pieceName);
            var piece = p != null ? p.GetComponent<Piece>() : null;
            return piece != null ? piece.m_icon : null;
        }

        // The game's sign, copied under the hub: networking / piece / wear parts out, HubSign in. Its post is not needed: it hangs on the roof beam.
        static bool AddSign(GameObject hub)
        {
            var src = PrefabManager.Instance.GetPrefab("sign");
            if (src == null) { ModLog.Warn("HubPrefab: the game's 'sign' prefab was not found - no sign on the hub"); return false; }
            var copy = Object.Instantiate(src, hub.transform);
            copy.name = "hub_sign";
            foreach (var c in copy.GetComponents<Component>())
                if (c is Sign || c is ZNetView || c is Piece || c is WearNTear) Object.DestroyImmediate(c);
            copy.transform.localPosition = new Vector3(HubData.SignX, HubData.SignY, HubData.SignZ);
            copy.transform.localRotation = Quaternion.Euler(0f, HubData.SignYaw, 0f);
            copy.AddComponent<HubSign>();
            return copy.GetComponentInChildren<TMPro.TMP_Text>(true) != null;
        }
    }
}
