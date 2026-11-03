using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Trolls.Combat.Domain;
using SteveTheTroll.Trolls.Home.Infrastructure;
using SteveTheTroll.Trolls.Registry.Infrastructure;
using SteveTheTroll.Trolls.Taming.Domain;
using SteveTheTroll.World.Spawning;
using UnityEngine;

namespace SteveTheTroll.Trolls.Taming.Infrastructure
{
    /// <summary>
    /// T-008a: Tameable.TamingUpdate (InvokeRepeating, owner of the ZDO only) is replaced for the game's Troll: no food, no "not alerted"
    /// condition (D-038: the troll fights normally while it is tamed); the time counts down while a slipper wearer is within TameRange
    /// (TamingRules). A wearer whose own tamed troll is within OwnTrollRange calls that troll instead (it follows the wearer). Every other
    /// tameable creature keeps the vanilla code. GetStatusString: a wild troll shows "taming" instead of "hungry".
    /// </summary>
    internal static class TrollTamingPatch
    {
        static readonly AccessTools.FieldRef<Tameable, Character> TCharacter = AccessTools.FieldRefAccess<Tameable, Character>("m_character");
        static readonly AccessTools.FieldRef<Tameable, MonsterAI> TAI = AccessTools.FieldRefAccess<Tameable, MonsterAI>("m_monsterAI");
        static readonly AccessTools.FieldRef<Tameable, ZNetView> TView = AccessTools.FieldRefAccess<Tameable, ZNetView>("m_nview");
        // m_tameDeltaTime is a CONST (3 s in 1.0.20, Cecil: literal) - no field ref possible (TEST-012 / TEST-014 crashed on FieldRefAccess and
        // StaticFieldRefAccess). Read its constant value once; 3 s if the game ever changes it to something unreadable.
        const float FallbackTick = 3f;
        /// D-058: set in the player's custom data after the first troll this character tamed (big "is coming!" only once).
        internal const string FirstTameKey = "stevethetroll_first_tame";
        static readonly float TickSeconds = ReadTick();

        static float ReadTick()
        {
            var f = AccessTools.Field(typeof(Tameable), "m_tameDeltaTime");
            try
            {
                object v = f != null && f.IsLiteral ? f.GetRawConstantValue() : f != null && f.IsStatic ? f.GetValue(null) : null;
                float tick = v is float x && x > 0f ? x : FallbackTick;
                ModLog.Verbose("TrollTamingPatch: m_tameDeltaTime " + (f == null ? "not found" : (f.IsLiteral ? "const" : f.IsStatic ? "static" : "instance") + " = " + v) + " -> tick " + tick + " s");
                return tick;
            }
            catch (System.InvalidOperationException e)
            {
                ModLog.Warn("TrollTamingPatch: m_tameDeltaTime unreadable (" + e.Message + ") - tick " + FallbackTick + " s");
                return FallbackTick;
            }
        }
        static readonly System.Func<Tameable, float> GetRemaining = AccessTools.MethodDelegate<System.Func<Tameable, float>>(AccessTools.Method(typeof(Tameable), "GetRemainingTime"));
        static readonly System.Action<Tameable> DoTame = AccessTools.MethodDelegate<System.Action<Tameable>>(AccessTools.Method(typeof(Tameable), "Tame"));
        const float DiagEverySeconds = 5f;
        static float _nextDiag;

        /// For the in-game test Startup_TamingReflection: touching this runs the type initializer (all reflection lookups).
        /// Expected outcome: "tick N s" with the game's tick interval (> 0).
        internal static string SelfCheck()
        {
            float dt = TickSeconds;
            bool ok = dt > 0f && GetRemaining != null && DoTame != null;
            ModLog.Verbose("TrollTamingPatch: self check tick " + dt + " s, delegates " + (GetRemaining != null) + "/" + (DoTame != null));
            return (ok ? "PASS: " : "FAIL: ") + "tick " + dt + " s, GetRemainingTime " + (GetRemaining != null) + ", Tame " + (DoTame != null);
        }

        [HarmonyPatch(typeof(Tameable), "TamingUpdate")]
        static class TamingUpdate
        {
            static bool Prefix(Tameable __instance)
            {
                if (!TrollTaming.IsTroll(__instance)) return true;     // vanilla for every other creature
                if (ModSettings.On) Tick(__instance);
                return false;
            }
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetStatusString))]
        static class StatusString
        {
            static void Postfix(Tameable __instance, ref string __result)
            {
                // our trolls never eat: no "hungry"; tamed = happy (TEST-015 showed "Tame, Acclimatizing"), wild = taming in progress
                if (__result == "$hud_tamehungry" && TrollTaming.IsTroll(__instance)) __result = __instance.IsTamed() ? "$hud_tamehappy" : "$hud_tameinprogress";
            }
        }

        // One taming tick of one troll (this client owns its ZDO).
        static void Tick(Tameable t)
        {
            var nview = TView(t);
            var troll = TCharacter(t);
            if (nview == null || !nview.IsValid() || !nview.IsOwner() || troll == null || troll.IsDead()) return;
            bool diag = Time.time >= _nextDiag;
            if (troll.IsTamed()) return;      // D-145: a tamed troll needs no diagnostic (it was 179 lines per test run)
            if (t.m_tamingTime != ModSettings.TameSeconds.Value)
            {
                ModLog.Verbose("TrollTamingPatch: " + TrollTaming.Tag(troll) + " taming time " + t.m_tamingTime + " -> " + ModSettings.TameSeconds.Value + " s (Tunable changed)");
                t.m_tamingTime = ModSettings.TameSeconds.Value;
            }

            float wearerDist;
            var wearer = TrollTaming.NearestWearer(troll.transform.position, out wearerDist);
            float ownDist;
            var own = TrollTaming.OwnTroll(wearer, out ownDist);
            int ownNear = TrollTaming.OwnTrollCount(wearer, ModSettings.OwnTrollRange.Value);
            int limit = TrollTaming.TrollLimit(wearer);
            var step = TamingRules.DecideWildLimit(wearerDist, ModSettings.TameRange.Value, ownNear, limit);
            // D-123: at most GlobalTameCap (15) living tamed trolls per player in total (known for the local player: TrollRegistry)
            if (step == TamingRules.Step.CountDown && wearer == Player.m_localPlayer && !BreakRules.MayTameMore(TrollRegistry.LivingCount(), ModSettings.GlobalTameCap.Value))
            {
                if (diag) Diag(troll, "wild - " + wearer.GetPlayerName() + " already has " + TrollRegistry.LivingCount() + " living tamed trolls (cap " + ModSettings.GlobalTameCap.Value + ")");
                return;
            }
            float before = GetRemaining(t);
            if (diag)
            {
                Diag(troll, "wild, nearest wearer " + (wearer != null ? wearer.GetPlayerName() : "none") + " at " + wearerDist.ToString("F1") + " m (range "
                    + ModSettings.TameRange.Value + "), own tamed troll " + (own != null ? own.name : "none") + " at " + ownDist.ToString("F1") + " m (limit "
                    + ModSettings.OwnTrollRange.Value + " m: " + ownNear + " of " + limit + " allowed), left " + before.ToString("F1") + " s (" + TamingRules.Percent(before, t.m_tamingTime) + " %), step " + step);
            }
            if (step == TamingRules.Step.CallOwnTroll) { CallTroll(own, wearer); return; }
            if (step != TamingRules.Step.CountDown) return;

            float left = TamingRules.CountDown(before, TickSeconds);
            nview.GetZDO().Set(ZDOVars.s_tameTimeLeft, left);
            float stored = GetRemaining(t);
            ModLog.Check("TrollTamingPatch: " + TrollTaming.Tag(troll) + " stored taming time", stored, left, 0.01, "s");
            ModLog.Verbose("TrollTamingPatch: " + TrollTaming.Tag(troll) + " taming by " + wearer.GetPlayerName() + " at " + wearerDist.ToString("F1") + " m: " + before.ToString("F1") + " -> " + left.ToString("F1") + " s left");
            if (!TamingRules.Done(left)) return;

            bool owned = TrollTaming.SetOwner(troll, wearer);
            string id = TrollRegistry.IdOf(troll);    // D-043: our id for the F6 lists
            string name = TrollRegistry.NameFor(troll);
            t.SetText(name);                          // D-044: Steve N (club) / Spencer N - before Tame, so every message uses it
            string baseName = name.Contains(" ") ? name.Substring(0, name.LastIndexOf(' ')) : name;
            DoTame(t);
            // D-047 / D-048: "Steve is coming!" (green, left) for the tamer when this is the tamer's client
            // D-058: the very first troll a player tames: the message big in the middle of the screen (once per character)
            if (wearer == Player.m_localPlayer)
            {
                bool first = !wearer.m_customData.ContainsKey(FirstTameKey);
                if (first) { wearer.m_customData[FirstTameKey] = "1"; Ui.Notify.Big(Loc.T("stevethetroll_troll_coming", baseName), Ui.Notify.Kind.Good); }
                else Ui.Notify.Show(Loc.T("stevethetroll_troll_coming", baseName), Ui.Notify.Kind.Good);
            }
            else ModLog.Verbose("TrollTamingPatch: tamer " + wearer.GetPlayerName() + " is on another client - no 'is coming' message here");
            ModLog.Info("TrollTamingPatch: " + TrollTaming.Tag(troll) + " tamed by " + wearer.GetPlayerName() + " as '" + name + "' (owner set " + owned + ", tamed " + troll.IsTamed() + ")");
            ModLog.Check("TrollTamingPatch: " + TrollTaming.Tag(troll) + " tamed after the countdown", troll.IsTamed() ? 1 : 0, 1, 0, "");
            TamedTrollStats.Refresh(troll);           // D-042: x3 health from now on
            ModLog.Verbose("TrollTamingPatch: " + TrollTaming.Tag(troll) + " item set kept: '" + TrollSpawnBoost.Remember(troll) + "' (D-063)");
            TrollArrival.Begin(troll, wearer);       // D-043: walk to the tamer, then wait for orders
            ModLog.Verbose("TrollTamingPatch: " + TrollTaming.Tag(troll) + " id " + id + ", walking to " + wearer.GetPlayerName());
        }

        // D-037: the wearer's own tamed troll walks to the wearer (follows) instead of a second taming. Only the troll's ZDO owner may steer it.
        static void CallTroll(Character own, Player wearer)
        {
            var ai = own != null ? own.GetComponent<MonsterAI>() : null;
            var view = own != null ? own.GetComponent<ZNetView>() : null;
            if (ai == null || wearer == null) { ModLog.VerboseWarn("TrollTamingPatch: cannot call the own troll (ai " + (ai != null) + ", wearer " + (wearer != null) + ")"); return; }
            if (ai.GetFollowTarget() == wearer.gameObject) return;
            if (view == null || !view.IsValid() || !view.IsOwner()) { ModLog.Verbose("TrollTamingPatch: own troll " + TrollTaming.Tag(own) + " is owned by another client - it is called on that client"); return; }
            ai.SetFollowTarget(wearer.gameObject);
            ModLog.Info("TrollTamingPatch: " + wearer.GetPlayerName() + " already has a tamed troll (" + TrollTaming.Tag(own) + ") - no second taming, it now follows the wearer");
            ModLog.Check("TrollTamingPatch: called troll follows the wearer", ai.GetFollowTarget() == wearer.gameObject ? 1 : 0, 1, 0, "");
        }

        static void Diag(Character troll, string what)
        {
            _nextDiag = Time.time + DiagEverySeconds;
            ModLog.Verbose("TrollTamingPatch: " + TrollTaming.Tag(troll) + " at " + troll.transform.position.ToString("F0") + " " + what);
        }
    }
}
