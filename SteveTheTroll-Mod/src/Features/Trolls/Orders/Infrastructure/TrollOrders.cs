using System;
using System.Collections.Generic;
using KydlCore.Common;
using KydlCore.Core;
using KydlCore.Features.Hub.Contracts;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Orders.Application;
using SteveTheTroll.Features.Trolls.Orders.Commands;
using SteveTheTroll.Features.Trolls.Orders.Domain;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Orders.Infrastructure;

/// <summary>
///     T-008d / D-065: the attack key ([Controls] AttackKey, Mouse 4 = the mouse's back button): the player looks at
///     something and presses
///     it - his nearest own tamed troll (within OrderRange) attacks that creature, or breaks that tree / log / rock / vein
///     (then goes on
///     farming), or, looking at nothing of that kind, farms its focus / the default priority. Not while a menu, chat or
///     the console is open.
///     D-170 (Architecture V2, orders slice): the Infrastructure adapter - key, raycast and ownership take-over here;
///     OrderPipeline / OrderRules decide; the order is a command
///     handled by TrollOrderService.
/// </summary>
internal static class TrollOrders
{
    private const float RayMetres = 80f, OrderRange = 100f;
    private static int _mask;
    private static readonly RaycastHit[] Hits = new RaycastHit[32];

    private static readonly OrderPipeline Pipeline = new();

    /// D-171: TestLab only (hook stt.aimAt) - the next attack-key ray aims from the camera at this object's centre; used up by one press. null in normal play.
    internal static GameObject AimOverride;

    /// D-171: TestLab only (hook stt.attackKeyPress) - presses of the attack key queued by a test, used up one per frame. 0 in normal play.
    internal static int FakePresses;

    /// stt.reset: a failed test leaves no queued key press and no aim override behind. Expected outcome: none.
    internal static void ResetTestInput()
    {
        FakePresses = 0;
        AimOverride = null;
        _probing = false;
    }

    // What the player looks at: an enemy creature, or a farm source (tree, log, rock, vein, destructible). Expected outcome: the creature or null (+ source).
    // D-172 (review: "add a pre-press check that the actual GameCamera ray intersects the target tree"): the test asks the SAME lookup the attack key uses, without an order
    private static bool _probing;

    private static string KeyLog =>
        _probing ? "TrollOrders: test probe - the crosshair is on " : "TrollOrders: attack key on ";

    /// D-176 (user: "diagnostic lines do not change behaviour"): everything that happens between a key press and the troll's target, in ONE record per press - was the press consumed
    /// (test or real), player alive, UI blocking, which troll, is it ours to steer, is there a camera, what the ray hit, which creature, what the pipeline decided, whether the command was
    /// accepted, and what the troll holds right afterwards (AI target / forced target / chase).
    private sealed class KeyRecord
    {
        internal bool Pressed, Fake, Alive, Blocked, Camera, Steer, Sent, SentKnown;
        internal string Troll = "none", Owner = "-", Candidate = "none", Decision = "-", Now = "-";
    }

    private static bool _fakeThisFrame;
    private static string _lastRay = "";
    private static Character _watch;
    private static float _watchUntil, _nextWatch;
    private static string _watchLast = "";

    /// Plugin.Update. Expected outcome: whether an order was given this frame.
    internal static bool Tick()
    {
        Watch();
        KeyRecord r = new();
        bool result = TickCore(r);
        if (r.Pressed)
        {
            ModLog.Info("TrollOrders: key record - press " + (r.Fake ? "by a test" : "real") + ", player alive " + r.Alive + ", UI blocked " + r.Blocked + ", nearest troll " + r.Troll +
                        ", owner here " + r.Owner + ", steerable " + r.Steer + ", camera " + r.Camera + ", ray: " + (_lastRay.Length > 0 ? _lastRay : "not cast") + ", creature " + r.Candidate +
                        ", decision " + r.Decision + ", command accepted " + (r.SentKnown ? r.Sent.ToString() : "n/a") + ", troll now: " + r.Now + ", gave an order " + result);
        }

        return result;
    }

    /// For 2 s after an attack order: a change of what the troll holds (AI target / forced target / chase) is logged - the moment a target is lost shows with its time.
    private static void Watch()
    {
        if (_watch == null || Time.time > _watchUntil || Time.time < _nextWatch)
        {
            if (_watch != null && Time.time > _watchUntil)
            {
                _watch = null;
            }

            return;
        }

        _nextWatch = Time.time + 0.05f;
        string now = TrollGuard.Describe(_watch);
        if (now != _watchLast)
        {
            ModLog.Info("TrollOrders: watch (" + (_watchUntil - Time.time).ToString("F1") + " s left) - " + TrollTaming.Tag(_watch) + " holds: " + now);
            _watchLast = now;
        }
    }

    private static bool TickCore(KeyRecord rec)
    {
        KeyCode key = ModSettings.AttackKey != null ? ModSettings.AttackKey.Value : KeyCode.None;
        _fakeThisFrame = false;
        _lastRay = "";
        bool pressed = key != KeyCode.None && Pressed(key);
        rec.Pressed = pressed;
        rec.Fake = _fakeThisFrame;
        Player p = Player.m_localPlayer;
        bool alive = pressed && p != null && !p.IsDead();
        rec.Alive = alive;
        bool blocked = alive && (KyHub.IsOpen || InventoryGui.IsVisible() || Menu.IsVisible() || Console.IsVisible() ||
                                 (Chat.instance != null && Chat.instance.HasFocus()) || TextInput.IsVisible());
        rec.Blocked = blocked;
        Character troll = null, creature = null;
        GameObject source = null;
        ZNetView view = null;
        bool steer = false;
        if (alive && !blocked)
        {
            troll = Nearest(p);
            view = troll != null ? troll.GetComponent<ZNetView>() : null;
            rec.Troll = troll != null ? TrollTaming.Tag(troll) : "none";
            rec.Owner = view != null && view.IsValid() ? view.IsOwner().ToString() : "no valid view";
            if (view != null && view.IsValid() && !view.IsOwner())
            {
                // D-066 (TEST-029: "steered by another client" twice): the owner of the troll gives an order -> takes over its steering
                ModLog.Info("TrollOrders: " + TrollTaming.Tag(troll) + " was steered by client " +
                            view.GetZDO().GetOwner() + " - " + p.GetPlayerName() + " takes it over");
                view.ClaimOwnership();
            }

            steer = view != null && view.IsValid() && view.IsOwner();
            rec.Steer = steer;
            rec.Camera = GameCamera.instance != null;
            if (steer)
            {
                creature = Look(p, out source);
                rec.Candidate = creature != null ? creature.name : source != null ? source.name + " (a source)" : "none";
            }
        }

        OperationResponse<OrderKind> decision = Pipeline.Evaluate(new OrderInput
        {
            KeyPressed = pressed, PlayerAlive = alive, UiBlocked = blocked, HasTroll = troll != null, CanSteer = steer,
            HasCreature = creature != null, HasSource = source != null, SourceIsWall = source != null && TrollWalls.IsWall(source)
        });
        rec.Decision = decision.IsSuccess ? decision.Value.ToString() : "refused: " + decision.Reason;
        if (!decision.IsSuccess)
        {
            if (decision.Reason == OrderPipeline.NoTroll)
            {
                ModLog.Info("TrollOrders: attack key - no own tamed troll within " + OrderRange + " m");
            }
            else if (decision.Reason == OrderPipeline.NoSteer)
            {
                ModLog.Warn("TrollOrders: " + TrollTaming.Tag(troll) + " cannot be steered here (valid " +
                            (view != null && view.IsValid()) + ")");
            }

            return decision.IsSuccess;
        }

        string shown = Shown(troll);
        string focus = TrollFocus.Normalize(view.GetZDO().GetString(TrollRegistry.FocusKey));
        ModLog.Verbose("TrollOrders: " + TrollTaming.Tag(troll) + " order " + decision.Value);
        switch (decision.Value)
        {
            case OrderKind.Attack:
                // D-176 (review of attack-key-creature): the answer of the command counts - a refused order is not announced as an attack
                rec.Sent = TrollOrderService.Send(new AttackTargetCommand(troll, creature));
                rec.SentKnown = true;
                rec.Now = TrollGuard.Describe(troll);
                _watch = troll;
                _watchUntil = Time.time + 2f;
                _nextWatch = 0f;
                _watchLast = rec.Now;
                if (!rec.Sent)
                {
                    ModLog.Warn("TrollOrders: the attack order was not accepted by " + TrollTaming.Tag(troll) + " -> " + creature.name);
                    return false;
                }

                Notify.Show(Loc.T("stevethetroll_order_attack", shown, creature.GetHoverName()), Notify.Kind.Attack);
                return true;
            case OrderKind.BreakWall:
            case OrderKind.BreakSource:
                // D-124: walls only for 2-star trolls (TrollWalls decides)
                bool ok = decision.Value == OrderKind.BreakWall
                    ? TrollOrderService.Send(new BreakWallCommand(troll, source, focus))
                    : TrollOrderService.Send(new BreakSourceCommand(troll, source, focus));
                rec.Sent = ok;
                rec.SentKnown = true;
                if (ok)
                {
                    Notify.Show(Loc.T("stevethetroll_order_break", shown, Localization.instance.Localize(Name(source))),
                        Notify.Kind.Farm);
                }

                return ok;
            default:
                bool started = TrollOrderService.Send(new FarmFocusCommand(troll, focus));
                rec.Sent = started;
                rec.SentKnown = true;
                if (started)
                {
                    Notify.Show(Loc.T("stevethetroll_gofarm_started", shown, Loc.T(TrollFocus.KeyOf(focus))),
                        Notify.Kind.Farm);
                }

                ModLog.Info("TrollOrders: attack key on nothing - " + TrollTaming.Tag(troll) + " farms (focus '" +
                            focus + "') " + started);
                return started;
        }
    }

    // ZInput first (the game's input), the plain Unity input as a fallback for the extra mouse buttons.
    private static bool Pressed(KeyCode key)
    {
        if (FakePresses > 0)
        {
            FakePresses--;
            _fakeThisFrame = true;
            ModLog.Info("TrollOrders: attack key " + key + " pressed by a test (" + FakePresses + " left)");
            return FakePresses >= 0;
        }

        try
        {
            if (ZInput.GetKeyDown(key, false))
            {
                return true;
            }
        }
        catch (Exception)
        {
        }

        try
        {
            return Input.GetKeyDown(key);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static Character Nearest(Player p)
    {
        Character best = null;
        float bestD = OrderRange;
        foreach (Character c in Character.GetAllCharacters())
        {
            if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c) ||
                TrollTaming.Owner(c) != p.GetPlayerID())
            {
                continue;
            }

            float d = Vector3.Distance(c.transform.position, p.transform.position);
            if (d < bestD)
            {
                bestD = d;
                best = c;
            }
        }

        return best;
    }

    /// Test seam: what the attack key would act on right now (real camera position and forward, same mask and rules). Expected outcome: the name of the creature or
    /// source under the crosshair, "" for nothing.
    internal static string Aimed(Player p)
    {
        _probing = true;
        try
        {
            GameObject source;
            Character ch = Look(p, out source);
            return ch != null ? ch.name : source != null ? source.name : "";
        }
        finally
        {
            _probing = false;
        }
    }

    private static Character Look(Player p, out GameObject source)
    {
        source = null;
        if (_mask == 0)
        {
            _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain",
                "character", "character_net", "character_noenv", "hitbox", "vehicle");
        }

        Transform cam = GameCamera.instance != null ? GameCamera.instance.transform : null;
        if (cam == null)
        {
            _lastRay = "no camera";
            return null;
        }

        Vector3 dir = cam.forward;
        if (AimOverride != null)
        {
            // D-172: the trunk / body, not the bounds centre of the first collider (a tree's is up in the canopy: the ray passed the planted tree and ordered one 67 m away)
            Vector3 chest = AimOverride.transform.position + Vector3.up * 1.5f;
            Vector3 at = chest;
            float best = float.MaxValue;
            foreach (Collider col in AimOverride.GetComponentsInChildren<Collider>())
            {
                if (col == null || col.isTrigger || !col.enabled)
                {
                    continue;
                }

                Vector3 q = col.ClosestPoint(chest);
                float d = (q - chest).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    at = q;
                }
            }

            dir = (at - cam.position).normalized;
            _lastRay = "aimed by a test at " + AimOverride.name + "; ";
            ModLog.Info("TrollOrders: attack-key ray aimed at " + AimOverride.name + " by a test (" +
                        Vector3.Distance(cam.position, at).ToString("F1") + " m)");
            AimOverride = null;
        }

        int n = Physics.RaycastNonAlloc(cam.position, dir, Hits, RayMetres, _mask);
        Array.Sort(Hits, 0, n, Comparer.Instance);
        for (int i = 0; i < n; i++)
        {
            Collider col = Hits[i].collider;
            if (col == null)
            {
                continue;
            }

            Character ch = col.GetComponentInParent<Character>();
            if (ch == p)
            {
                continue;
            }

            if (ch != null)
            {
                if (ch.IsDead() || ch.IsPlayer() || ch.IsTamed())
                {
                    _lastRay += "first hit " + ch.name + " at " + Hits[i].distance.ToString("F1") + " m ignored (" + (ch.IsDead() ? "dead" : ch.IsPlayer() ? "a player" : "tamed") + ")";
                    return null;
                }

                _lastRay += "hit creature " + ch.name + " (" + col.name + ") at " + Hits[i].distance.ToString("F1") + " m";

                ModLog.Info(KeyLog + ch.name + " at " + Hits[i].distance.ToString("F1") + " m");
                return ch;
            }

            ZNetView view = col.GetComponentInParent<ZNetView>();
            GameObject g = view != null ? view.gameObject : null;
            if (g != null && (g.GetComponent<TreeBase>() != null || g.GetComponent<TreeLog>() != null ||
                              g.GetComponent<MineRock5>() != null || g.GetComponent<MineRock>() != null ||
                              g.GetComponent<Destructible>() != null ||
                              TrollWalls.IsWall(g))) // D-124: fortress walls too
            {
                source = g;
                _lastRay += "hit source " + g.name + " at " + Hits[i].distance.ToString("F1") + " m";
                ModLog.Info(KeyLog + g.name + " (" + TrollFarming.KindOf(g) + ") at " +
                            Hits[i].distance.ToString("F1") + " m");
                return null;
            }

            _lastRay += "hit " + col.name + " of " + col.transform.root.name + " at " + Hits[i].distance.ToString("F1") + " m (nothing to attack)";
            ModLog.Info(KeyLog + col.name + " (nothing to attack) at " + Hits[i].distance.ToString("F1") + " m");
            return null;
        }

        _lastRay += "nothing hit within " + RayMetres.ToString("F0") + " m";
        return null;
    }

    private static string Name(GameObject g)
    {
        HoverText h = g.GetComponent<HoverText>();
        if (h != null && !string.IsNullOrEmpty(h.m_text))
        {
            return h.m_text;
        }

        return Utils.GetPrefabName(g);
    }

    internal static string Shown(Character troll)
    {
        return TrollTaming.ShownName(troll);
    }

    private sealed class Comparer : IComparer<RaycastHit>
    {
        internal static readonly Comparer Instance = new();

        public int Compare(RaycastHit a, RaycastHit b)
        {
            return a.distance.CompareTo(b.distance);
        }
    }
}
