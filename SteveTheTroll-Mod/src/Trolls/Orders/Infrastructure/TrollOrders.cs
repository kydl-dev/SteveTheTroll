using Kydl.Common;
using SteveTheTroll.Trolls.Combat.Infrastructure;
using SteveTheTroll.Trolls.Farming.Infrastructure;
using SteveTheTroll.Trolls.Orders.Application;
using SteveTheTroll.Trolls.Orders.Commands;
using SteveTheTroll.Trolls.Orders.Domain;
using SteveTheTroll.Trolls.Registry.Infrastructure;
using SteveTheTroll.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Trolls.Orders.Infrastructure
{
    /// <summary>
    /// T-008d / D-065: the attack key ([Controls] AttackKey, Mouse 4 = the mouse's back button): the player looks at something and presses
    /// it - his nearest own tamed troll (within OrderRange) attacks that creature, or breaks that tree / log / rock / vein (then goes on
    /// farming), or, looking at nothing of that kind, farms its focus / the default priority. Not while a menu, chat or the console is open.
    /// D-170 (Architecture V2, orders slice): the Infrastructure adapter - key, raycast and ownership take-over here; OrderPipeline / OrderRules decide; the order is a command
    /// handled by TrollOrderService.
    /// </summary>
    internal static class TrollOrders
    {
        const float RayMetres = 80f, OrderRange = 100f;
        static int _mask;
        static readonly RaycastHit[] Hits = new RaycastHit[32];

        static readonly OrderPipeline Pipeline = new OrderPipeline();

        /// Plugin.Update. Expected outcome: whether an order was given this frame.
        /// D-170: reads the facts into an OrderInput (the raycast only once a steerable troll exists, as before), OrderPipeline decides, the order goes out as a command.
        internal static bool Tick()
        {
            var key = ModSettings.AttackKey != null ? ModSettings.AttackKey.Value : KeyCode.None;
            bool pressed = key != KeyCode.None && Pressed(key);
            var p = Player.m_localPlayer;
            bool alive = pressed && p != null && !p.IsDead();
            bool blocked = alive && (KydlCore.KyHub.IsOpen || InventoryGui.IsVisible() || Menu.IsVisible() || global::Console.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus()) || TextInput.IsVisible());
            Character troll = null, creature = null;
            GameObject source = null;
            ZNetView view = null;
            bool steer = false;
            if (alive && !blocked)
            {
                troll = Nearest(p);
                view = troll != null ? troll.GetComponent<ZNetView>() : null;
                if (view != null && view.IsValid() && !view.IsOwner())
                {
                    // D-066 (TEST-029: "steered by another client" twice): the owner of the troll gives an order -> takes over its steering
                    ModLog.Info("TrollOrders: " + TrollTaming.Tag(troll) + " was steered by client " + view.GetZDO().GetOwner() + " - " + p.GetPlayerName() + " takes it over");
                    view.ClaimOwnership();
                }
                steer = view != null && view.IsValid() && view.IsOwner();
                if (steer) creature = Look(p, out source);
            }
            var decision = Pipeline.Evaluate(new OrderInput(pressed, alive, blocked, troll != null, steer, creature != null, source != null, source != null && TrollWalls.IsWall(source)));
            if (!decision.IsSuccess)
            {
                if (decision.Reason == OrderPipeline.NoTroll) ModLog.Info("TrollOrders: attack key - no own tamed troll within " + OrderRange + " m");
                else if (decision.Reason == OrderPipeline.NoSteer) ModLog.Warn("TrollOrders: " + TrollTaming.Tag(troll) + " cannot be steered here (valid " + (view != null && view.IsValid()) + ")");
                return decision.IsSuccess;
            }
            string shown = Shown(troll);
            string focus = Ui.TrollFocus.Normalize(view.GetZDO().GetString(TrollRegistry.FocusKey, ""));
            ModLog.Verbose("TrollOrders: " + TrollTaming.Tag(troll) + " order " + decision.Value);
            switch (decision.Value)
            {
                case OrderKind.Attack:
                    TrollOrderService.Send(new AttackTargetCommand(troll, creature));
                    Ui.Notify.Show(Loc.T("stevethetroll_order_attack", shown, creature.GetHoverName()), Ui.Notify.Kind.Attack);
                    return decision.Value == OrderKind.Attack;
                case OrderKind.BreakWall:
                case OrderKind.BreakSource:
                    // D-124: walls only for 2-star trolls (TrollWalls decides)
                    bool ok = decision.Value == OrderKind.BreakWall ? TrollOrderService.Send(new BreakWallCommand(troll, source, focus)) : TrollOrderService.Send(new BreakSourceCommand(troll, source, focus));
                    if (ok) Ui.Notify.Show(Loc.T("stevethetroll_order_break", shown, Localization.instance.Localize(Name(source))), Ui.Notify.Kind.Farm);
                    return ok;
                default:
                    bool started = TrollOrderService.Send(new FarmFocusCommand(troll, focus));
                    if (started) Ui.Notify.Show(Loc.T("stevethetroll_gofarm_started", shown, Loc.T(Ui.TrollFocus.KeyOf(focus))), Ui.Notify.Kind.Farm);
                    ModLog.Info("TrollOrders: attack key on nothing - " + TrollTaming.Tag(troll) + " farms (focus '" + focus + "') " + started);
                    return started;
            }
        }

        /// D-171: TestLab only (hook stt.aimAt) - the next attack-key ray aims from the camera at this object's centre; used up by one press. null in normal play.
        internal static GameObject AimOverride;

        /// D-171: TestLab only (hook stt.attackKeyPress) - presses of the attack key queued by a test, used up one per frame. 0 in normal play.
        internal static int FakePresses;

        // ZInput first (the game's input), the plain Unity input as a fallback for the extra mouse buttons.
        static bool Pressed(KeyCode key)
        {
            if (FakePresses > 0) { FakePresses--; ModLog.Info("TrollOrders: attack key " + key + " pressed by a test (" + FakePresses + " left)"); return FakePresses >= 0; }
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
            var dir = cam.forward;
            if (AimOverride != null)
            {
                var col = AimOverride.GetComponentInChildren<Collider>();
                var at = col != null ? col.bounds.center : AimOverride.transform.position;
                dir = (at - cam.position).normalized;
                ModLog.Info("TrollOrders: attack-key ray aimed at " + AimOverride.name + " by a test (" + Vector3.Distance(cam.position, at).ToString("F1") + " m)");
                AimOverride = null;
            }
            int n = Physics.RaycastNonAlloc(cam.position, dir, Hits, RayMetres, _mask);
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
