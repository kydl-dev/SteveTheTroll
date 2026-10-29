using System.Collections.Generic;
using HarmonyLib;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll.Taming
{
    /// <summary>
    /// D-111 (user 2026-10-28: "tamed trolls have extra 25% speed (walking and hitting animations)"): an own or anyone's tamed Troll moves
    /// TamedSpeedFactor x faster (the instance's m_speed / m_walkSpeed / m_runSpeed; the prefab stays vanilla) and its animations play
    /// TamedSpeedFactor x faster, so the legs match the speed and every swing / slam hits sooner. Hooks (1.0.21):
    ///  - CharacterAnimEvent.CustomFixedUpdate: the game puts the animator speed back to 1 there whenever the character is free (no attack,
    ///    no minor action, no emote); the postfix makes that 1 the factor and keeps the movement speeds scaled;
    ///  - CharacterAnimEvent.Speed: an attack clip's own speed event (scale) becomes scale x factor.
    /// D-130 (user 2026-10-29): swimming has its own update (Character.UpdateSwimming prefix, every physics tick the game swims the troll),
    /// separate from the animation callback, and the troll keeps the fast factor while swimming. A freeze frame (animator speed ~0, the hit pause) is never touched. The owner's animator speed reaches other clients through
    /// ZSyncAnimation (s_animationSpeed). Wild trolls and every other creature stay vanilla.
    /// </summary>
    internal static class TamedTrollSpeed
    {
        static readonly AccessTools.FieldRef<CharacterAnimEvent, Character> EventCharacter = AccessTools.FieldRefAccess<CharacterAnimEvent, Character>("m_character");
        static readonly AccessTools.FieldRef<CharacterAnimEvent, Animator> EventAnimator = AccessTools.FieldRefAccess<CharacterAnimEvent, Animator>("m_animator");

        sealed class Base { public float Speed, Walk, Run, Factor, Swim, SwimDepth, SwimAccel, SwimTurn; public bool Wading; }
        static readonly Dictionary<int, Base> Bases = new Dictionary<int, Base>();     // by GameObject instance id: the instance's own vanilla speeds (a few per session)
        static float _nextLog;

        /// In-game test Startup_TamingReflection: the two private fields exist. Expected outcome: whether both were found.
        internal static bool SelfCheck() { return EventCharacter != null && EventAnimator != null; }

        static float Factor { get { return ModSettings.TamedSpeedFactor.Value; } }

        /// The instance's own vanilla speeds, taken the first time the troll is seen (by the animation callback or the swim update, whichever
        /// comes first - both run before this mod changes anything on it). Expected outcome: the Base of this troll.
        static Base BaseOf(Character c)
        {
            int id = c.gameObject.GetInstanceID();
            Base b;
            if (Bases.TryGetValue(id, out b)) return b;
            b = new Base { Speed = c.m_speed, Walk = c.m_walkSpeed, Run = c.m_runSpeed, Factor = 1f, Swim = c.m_swimSpeed, SwimDepth = c.m_swimDepth, SwimAccel = c.m_swimAcceleration, SwimTurn = c.m_swimTurnSpeed };
            Bases[id] = b;
            ModLog.Verbose("TamedTrollSpeed: " + c.name + " vanilla speeds jog " + b.Speed + ", walk " + b.Walk + ", run " + b.Run + " m/s, swim " + b.Swim + " m/s, swim acceleration "
                + b.SwimAccel + ", swim turn " + b.SwimTurn);
            return b;
        }

        /// Keeps the instance's movement speeds at vanilla x factor (factor 1 = vanilla; also puts them back when the troll is no longer tamed).
        /// Expected outcome: the run speed now in use equals the vanilla run speed x the factor.
        internal static bool Movement(Character c, float factor)
        {
            if (c == null) return false;
            Base b = BaseOf(c);
            if (!Mathf.Approximately(b.Factor, factor))
            {
                c.m_speed = b.Speed * factor; c.m_walkSpeed = b.Walk * factor; c.m_runSpeed = b.Run * factor;
                ModLog.Info("TamedTrollSpeed: " + c.name + " speed x" + factor.ToString("0.00") + " (was x" + b.Factor.ToString("0.00") + "): jog " + c.m_speed.ToString("F1")
                    + ", walk " + c.m_walkSpeed.ToString("F1") + ", run " + c.m_runSpeed.ToString("F1") + " m/s");
                b.Factor = factor;
                ModLog.Check("TamedTrollSpeed: " + c.name + " run speed", c.m_runSpeed, b.Run * factor, 0.001, "m/s");
            }
            return Mathf.Approximately(c.m_runSpeed, b.Run * factor);
        }

        static float _nextSwimLog;

        /// D-129: wading state of the troll from the water depth at its feet; logs every change. Expected outcome: the movement factor to use.
        internal static float Wade(Character c, float tamedFactor)
        {
            Base b;
            if (c == null || !Bases.TryGetValue(c.gameObject.GetInstanceID(), out b)) return tamedFactor;
            float depth = c.GetLiquidLevel() - c.transform.position.y;
            bool now = BreakRules.Wading(b.Wading, depth, ModSettings.WadeDepth.Value, c.IsSwimming());
            if (now != b.Wading)
            {
                b.Wading = now;
                ModLog.Info("TamedTrollSpeed: " + c.name + (now ? " wades" : " out of deep water") + " (water " + depth.ToString("F1") + " m, wade from " + ModSettings.WadeDepth.Value.ToString("F1")
                    + " m, swimming " + c.IsSwimming() + ") -> speed x" + BreakRules.MoveFactor(now || c.IsSwimming(), tamedFactor, ModSettings.WadeFactor.Value).ToString("0.00"));
            }
            // D-130: keep the wade factor while the game has the troll swimming too (no drop to the tamed factor at the swim edge: the legs and
            // the ground speed stay fast, the troll leaves the water already at speed).
            return BreakRules.MoveFactor(now || c.IsSwimming(), tamedFactor, ModSettings.WadeFactor.Value);
        }

        /// D-122 (user: "swim animation for trolls; up to 300% of base run speed while swimming; when following a slower player, keep up with
        /// the player's speed") + D-130 (user 2026-10-29: faster ramp-up, a minimum speed advantage over the followed player): a tamed troll
        /// swims where the game lets it (its own swim depth, 5 m - D-129: changing it did not work) at BreakRules.SwimSpeed: vanilla run x
        /// SwimRunFactor when nobody is followed; following a player: the player's speed + SwimFollowAdvantage, rising to the full speed when
        /// the troll is 3 - 8 m behind. Swim acceleration and turn speed are raised so it reaches that speed quickly. `tamed` false puts the
        /// vanilla values back. Called every physics tick by the UpdateSwimming prefix (not by the animation callback). The troll has no swim
        /// animation of its own (user 2026-10-29). Expected outcome: the swim speed in use.
        internal static float Swim(Character c, bool tamed)
        {
            if (c == null) return 0f;
            Base b;
            if (!tamed)
            {
                if (!Bases.TryGetValue(c.gameObject.GetInstanceID(), out b)) return c.m_swimSpeed;
                c.m_swimSpeed = b.Swim; c.m_swimDepth = b.SwimDepth; c.m_swimAcceleration = b.SwimAccel; c.m_swimTurnSpeed = b.SwimTurn;
                return c.m_swimSpeed;
            }
            b = BaseOf(c);
            c.m_swimDepth = b.SwimDepth;                      // D-129: the game's own swim depth (changing it only sank / lifted the body)
            var ai = c.GetComponent<MonsterAI>();
            var target = ai != null ? ai.GetFollowTarget() : null;
            var player = target != null ? target.GetComponent<Player>() : null;
            float playerSpeed = player != null ? player.GetVelocity().magnitude : 0f;
            float distance = player != null ? Vector3.Distance(c.transform.position, player.transform.position) : 0f;
            if (player != null && TrollWaterKeep.Keeps(c)) distance = BreakRules.BehindRing(distance, ModSettings.WaterKeepDistance.Value);   // D-131: catch up to the ring, not to the player
            float want = BreakRules.SwimSpeed(b.Swim, b.Run, ModSettings.SwimRunFactor.Value, player != null, playerSpeed, distance, ModSettings.SwimFollowAdvantage.Value);
            if (!Mathf.Approximately(c.m_swimSpeed, want)) c.m_swimSpeed = want;
            float accel = Mathf.Max(b.SwimAccel, ModSettings.SwimAcceleration.Value);
            if (!Mathf.Approximately(c.m_swimAcceleration, accel)) c.m_swimAcceleration = accel;
            float turn = b.SwimTurn * ModSettings.SwimTurnFactor.Value;
            if (!Mathf.Approximately(c.m_swimTurnSpeed, turn)) c.m_swimTurnSpeed = turn;
            if (Time.time >= _nextSwimLog)
            {
                _nextSwimLog = Time.time + 5f;
                ModLog.Verbose("TamedTrollSpeed: " + c.name + " swims at " + want.ToString("F1") + " m/s, real " + c.GetVelocity().magnitude.ToString("F1") + " m/s (vanilla swim " + b.Swim.ToString("F1")
                    + ", run " + b.Run.ToString("F1") + ", following " + (player != null ? player.GetPlayerName() + " at " + playerSpeed.ToString("F1") + " m/s, " + distance.ToString("F1") + " m away" : "nobody")
                    + "), acceleration " + c.m_swimAcceleration.ToString("F2") + ", turn " + c.m_swimTurnSpeed.ToString("F0") + ", swim depth " + c.m_swimDepth.ToString("F1") + " m");
            }
            return c.m_swimSpeed;
        }

        /// D-130: the dedicated swimming update. The game calls UpdateSwimming every physics tick while a character swims; the prefix sets the
        /// tamed troll's swim speed, acceleration and turn speed just before the game uses them (the animation callback no longer does this).
        [HarmonyPatch(typeof(Character), "UpdateSwimming")]
        static class SwimUpdate
        {
            static void Prefix(Character __instance)
            {
                if (TamedTrollStats.IsTamedTroll(__instance)) Swim(__instance, true);
            }
        }

        [HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.CustomFixedUpdate))]
        static class Free
        {
            static void Postfix(CharacterAnimEvent __instance)
            {
                var c = EventCharacter(__instance);
                if (c == null) return;
                bool tamed = TamedTrollStats.IsTamedTroll(c);
                if (!tamed)
                {
                    if (Bases.ContainsKey(c.gameObject.GetInstanceID())) { Movement(c, 1f); Swim(c, false); }   // no longer tamed / mod off: vanilla again
                    return;
                }
                float f = Wade(c, Factor);                       // D-129: WadeFactor x in water from WadeDepth, else the tamed factor
                Movement(c, f);                                  // D-130: swimming is handled by SwimUpdate, not here
                var anim = EventAnimator(__instance);
                if (anim == null) return;
                float was = anim.speed;
                float now = TamingRules.FreeAnimSpeed(was, f);
                if (now != was) anim.speed = now;
                if (Time.time >= _nextLog)
                {
                    _nextLog = Time.time + 5f;
                    ModLog.Verbose("TamedTrollSpeed: " + c.name + " animator speed " + was.ToString("F3") + " -> " + now.ToString("F3") + " (factor " + f.ToString("F2") + ", real speed " + c.GetVelocity().magnitude.ToString("F1") + " m/s, attack " + c.InAttack() + ")");
                }
            }
        }

        [HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.Speed))]
        static class AttackClip
        {
            static void Postfix(CharacterAnimEvent __instance, float speedScale)
            {
                var c = EventCharacter(__instance);
                if (!TamedTrollStats.IsTamedTroll(c)) return;
                var anim = EventAnimator(__instance);
                if (anim == null) return;
                anim.speed = TamingRules.AttackAnimSpeed(speedScale, Factor);
                ModLog.Verbose("TamedTrollSpeed: " + c.name + " attack clip speed " + speedScale.ToString("F2") + " -> " + anim.speed.ToString("F2"));
            }
        }
    }
}
