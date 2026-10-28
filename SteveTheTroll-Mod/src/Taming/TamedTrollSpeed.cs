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
    /// A freeze frame (animator speed ~0, the hit pause) is never touched. The owner's animator speed reaches other clients through
    /// ZSyncAnimation (s_animationSpeed). Wild trolls and every other creature stay vanilla.
    /// </summary>
    internal static class TamedTrollSpeed
    {
        static readonly AccessTools.FieldRef<CharacterAnimEvent, Character> EventCharacter = AccessTools.FieldRefAccess<CharacterAnimEvent, Character>("m_character");
        static readonly AccessTools.FieldRef<CharacterAnimEvent, Animator> EventAnimator = AccessTools.FieldRefAccess<CharacterAnimEvent, Animator>("m_animator");

        sealed class Base { public float Speed, Walk, Run, Factor; }
        static readonly Dictionary<int, Base> Bases = new Dictionary<int, Base>();     // by GameObject instance id: the instance's own vanilla speeds (a few per session)
        static float _nextLog;

        /// In-game test Startup_TamingReflection: the two private fields exist. Expected outcome: whether both were found.
        internal static bool SelfCheck() { return EventCharacter != null && EventAnimator != null; }

        static float Factor { get { return ModSettings.TamedSpeedFactor.Value; } }

        /// Keeps the instance's movement speeds at vanilla x factor (factor 1 = vanilla; also puts them back when the troll is no longer tamed).
        /// Expected outcome: the run speed now in use equals the vanilla run speed x the factor.
        internal static bool Movement(Character c, float factor)
        {
            if (c == null) return false;
            int id = c.gameObject.GetInstanceID();
            Base b;
            if (!Bases.TryGetValue(id, out b))
            {
                b = new Base { Speed = c.m_speed, Walk = c.m_walkSpeed, Run = c.m_runSpeed, Factor = 1f };
                Bases[id] = b;
                ModLog.Verbose("TamedTrollSpeed: " + c.name + " vanilla speeds jog " + b.Speed + ", walk " + b.Walk + ", run " + b.Run + " m/s");
            }
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
                    if (Bases.ContainsKey(c.gameObject.GetInstanceID())) Movement(c, 1f);   // no longer tamed / mod off: vanilla again
                    return;
                }
                float f = Factor;
                Movement(c, f);
                var anim = EventAnimator(__instance);
                if (anim == null) return;
                float was = anim.speed;
                float now = TamingRules.FreeAnimSpeed(was, f);
                if (now != was) anim.speed = now;
                if (Time.time >= _nextLog)
                {
                    _nextLog = Time.time + 5f;
                    ModLog.Verbose("TamedTrollSpeed: " + c.name + " animator speed " + was.ToString("F3") + " -> " + now.ToString("F3") + " (factor " + f.ToString("F2") + ", attack " + c.InAttack() + ")");
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
