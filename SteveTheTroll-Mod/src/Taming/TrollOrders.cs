using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// T-008d / D-065: the attack key ([Controls] AttackKey, Mouse 4 = the mouse's back button): the player looks at something and presses
    /// it - his nearest own tamed troll (within OrderRange) attacks that creature, or breaks that tree / log / rock / vein (then goes on
    /// farming), or, looking at nothing of that kind, farms its focus / the default priority. Not while a menu, chat or the console is open.
    /// </summary>
    internal static class TrollOrders
    {
        const float RayMetres = 80f, OrderRange = 100f;
        static int _mask;
        static readonly RaycastHit[] Hits = new RaycastHit[32];

        /// Plugin.Update. Expected outcome: whether an order was given this frame.
        internal static bool Tick()
        {
            var key = ModSettings.AttackKey != null ? ModSettings.AttackKey.Value : KeyCode.None;
            if (key == KeyCode.None || !Pressed(key)) return false;
            var p = Player.m_localPlayer;
            if (p == null || p.IsDead() || KydlCore.KyHub.IsOpen || InventoryGui.IsVisible() || Menu.IsVisible() || global::Console.IsVisible()
                || (Chat.instance != null && Chat.instance.HasFocus()) || TextInput.IsVisible()) return false;
            var troll = Nearest(p);
            if (troll == null) { ModLog.Info("TrollOrders: attack key - no own tamed troll within " + OrderRange + " m"); return false; }
            var view = troll.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && !view.IsOwner())
            {
                // D-066 (TEST-029: "steered by another client" twice): the owner of the troll gives an order -> takes over its steering
                ModLog.Info("TrollOrders: " + TrollTaming.Tag(troll) + " was steered by client " + view.GetZDO().GetOwner() + " - " + p.GetPlayerName() + " takes it over");
                view.ClaimOwnership();
            }
            if (view == null || !view.IsValid() || !view.IsOwner()) { ModLog.Warn("TrollOrders: " + TrollTaming.Tag(troll) + " cannot be steered here (valid " + (view != null && view.IsValid()) + ")"); return false; }
            GameObject source;
            var creature = Look(p, out source);
            string shown = Shown(troll);
            if (creature != null)
            {
                TrollGuard.Order(troll, creature);
                Ui.Notify.Show(Loc.T("stevethetroll_order_attack", shown, creature.GetHoverName()), Ui.Notify.Kind.Attack);
                return true;
            }
            string focus = Ui.TrollFocus.Normalize(view.GetZDO().GetString(TrollRegistry.FocusKey, ""));
            if (source != null)
            {
                bool ok = TrollWalls.IsWall(source) ? TrollWalls.Order(troll, source, focus) : TrollFarming.BeginOn(troll, source, focus);   // D-124: walls only for 2-star trolls
                if (ok) Ui.Notify.Show(Loc.T("stevethetroll_order_break", shown, Localization.instance.Localize(Name(source))), Ui.Notify.Kind.Farm);
                return ok;
            }
            bool started = TrollFarming.Begin(troll, focus);
            if (started) Ui.Notify.Show(Loc.T("stevethetroll_gofarm_started", shown, Loc.T(Ui.TrollFocus.KeyOf(focus))), Ui.Notify.Kind.Farm);
            ModLog.Info("TrollOrders: attack key on nothing - " + TrollTaming.Tag(troll) + " farms (focus '" + focus + "') " + started);
            return started;
        }

        // ZInput first (the game's input), the plain Unity input as a fallback for the extra mouse buttons.
        static bool Pressed(KeyCode key)
        {
            try { if (ZInput.GetKeyDown(key, false)) return true; } catch (System.Exception) { }
            try { return Input.GetKeyDown(key); } catch (System.Exception) { return false; }
        }

        static Character Nearest(Player p)
        {
            Character best = null;
            float bestD = OrderRange;
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c) || TrollTaming.Owner(c) != p.GetPlayerID()) continue;
                float d = Vector3.Distance(c.transform.position, p.transform.position);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        // What the player looks at: an enemy creature, or a farm source (tree, log, rock, vein, destructible). Expected outcome: the creature or null (+ source).
        static Character Look(Player p, out GameObject source)
        {
            source = null;
            if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "character", "character_net", "character_noenv", "hitbox", "vehicle");
            var cam = GameCamera.instance != null ? GameCamera.instance.transform : null;
            if (cam == null) return null;
            int n = Physics.RaycastNonAlloc(cam.position, cam.forward, Hits, RayMetres, _mask);
            System.Array.Sort(Hits, 0, n, Comparer.Instance);
            for (int i = 0; i < n; i++)
            {
                var col = Hits[i].collider;
                if (col == null) continue;
                var ch = col.GetComponentInParent<Character>();
                if (ch == p) continue;
                if (ch != null)
                {
                    if (ch.IsDead() || ch.IsPlayer() || ch.IsTamed()) return null;
                    ModLog.Info("TrollOrders: attack key on " + ch.name + " at " + Hits[i].distance.ToString("F1") + " m");
                    return ch;
                }
                var view = col.GetComponentInParent<ZNetView>();
                var g = view != null ? view.gameObject : null;
                if (g != null && (g.GetComponent<TreeBase>() != null || g.GetComponent<TreeLog>() != null || g.GetComponent<MineRock5>() != null || g.GetComponent<MineRock>() != null || g.GetComponent<Destructible>() != null || TrollWalls.IsWall(g)))   // D-124: fortress walls too
                {
                    source = g;
                    ModLog.Info("TrollOrders: attack key on " + g.name + " (" + TrollFarming.KindOf(g) + ") at " + Hits[i].distance.ToString("F1") + " m");
                    return null;
                }
                ModLog.Info("TrollOrders: attack key on " + col.name + " (nothing to attack) at " + Hits[i].distance.ToString("F1") + " m");
                return null;
            }
            return null;
        }

        static string Name(GameObject g)
        {
            var h = g.GetComponent<HoverText>();
            if (h != null && !string.IsNullOrEmpty(h.m_text)) return h.m_text;
            return Utils.GetPrefabName(g);
        }

        internal static string Shown(Character troll) { return TrollTaming.ShownName(troll); }

        sealed class Comparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            internal static readonly Comparer Instance = new Comparer();
            public int Compare(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); }
        }
    }
}
