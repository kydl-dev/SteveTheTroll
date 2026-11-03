using HarmonyLib;
using Kydl.Common;

namespace SteveTheTroll.Features.Trolls.Taming.Infrastructure
{
    /// <summary>
    /// D-060 (TEST-025 screenshot: "Troll ( Tame, Happy ) [E] Pet"): the hover text of a TAMED troll shows its own name (Steve N / Spencer N,
    /// like the name over its head) and what E does now - "Follow" while it stays, "Stay" while it follows (D-038: E = follow / stay).
    /// Postfix on Tameable.GetHoverText; other tameable creatures and wild trolls keep the game's text. The rename line stays.
    /// </summary>
    // Harmony hook: src/Integrations/Harmony/TrollsTamingPatches.cs, TrollHoverHook (MIGRATION_DECISIONS A5)
    internal static class TrollHover
    {
        static readonly AccessTools.FieldRef<Tameable, MonsterAI> TAI = AccessTools.FieldRefAccess<Tameable, MonsterAI>("m_monsterAI");
        static float _nextLog;

        internal static void Postfix(Tameable __instance, ref string __result)
        {
            if (__instance == null || string.IsNullOrEmpty(__result) || !TrollTaming.IsTroll(__instance) || !__instance.IsTamed()) return;
            var lines = __result.Split('\n');
            string name = __instance.GetHoverName();
            string game = __instance.GetName();
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(game) && lines[0].StartsWith(game)) lines[0] = name + lines[0].Substring(game.Length);
            var ai = TAI(__instance);
            bool following = ai != null && ai.GetFollowTarget() != null;
            if (lines.Length > 1)
                lines[1] = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>] ") + Loc.T(following ? "stevethetroll_hover_stay" : "stevethetroll_hover_follow");
            __result = string.Join("\n", lines);
            if (UnityEngine.Time.time >= _nextLog)
            {
                _nextLog = UnityEngine.Time.time + 10f;
                ModLog.Verbose("TrollHover: " + name + " hover text: '" + lines[0] + "', E = " + (following ? "stay" : "follow"));
            }
        }
    }
}
