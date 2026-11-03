using HarmonyLib;
using Kydl.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Costume.Domain;
using SteveTheTroll.Features.Slippers.Infrastructure;
using SteveTheTroll.Presentation.Notifications;

namespace SteveTheTroll.Features.Costume.Infrastructure
{
    /// <summary>
    /// T-201: the game side of the costume unlock. The first time the local player equips the Fluffy Slippers the character is marked
    /// (<see cref="CostumeUnlockRules.WornKey"/> in Player.m_customData, saved with the character, so it survives a relog) and the known recipes are refreshed,
    /// so the costume recipes show at once. RecipeUnlockPatch hides the costume recipes until then. Characters that already knew a costume recipe keep it.
    /// </summary>
    internal static class CostumeUnlock
    {
        static System.Reflection.MethodInfo _updateRecipes;

        /// Has this character worn the slippers at least once? Expected outcome: true once marked.
        internal static bool WornOnce(Player p) { return p != null && p.m_customData != null && p.m_customData.ContainsKey(CostumeUnlockRules.WornKey); }

        /// Marks the local player (equip hook, test hooks). Expected outcome: true when the character is marked afterwards.
        internal static bool Mark(Player p, string why)
        {
            if (p == null || p.m_customData == null) return false;
            p.m_customData[CostumeUnlockRules.WornKey] = "1";
            bool refreshed = RefreshRecipes(p);
            ModLog.Info("CostumeUnlock: " + p.GetPlayerName() + " " + why + " - costume recipes unlocked (known recipes refreshed " + refreshed + ")");
            if (ModSettings.CostumeOn) Notify.Show(Loc.T("stevethetroll_msg_costume_unlocked"), Notify.Kind.Good);
            return WornOnce(p);
        }

        /// Test hooks only: forgets the mark (the costume recipes the character knows are left alone). Expected outcome: true when no longer marked.
        internal static bool Forget(Player p)
        {
            if (p == null || p.m_customData == null) return false;
            p.m_customData.Remove(CostumeUnlockRules.WornKey);
            ModLog.Verbose("CostumeUnlock: mark removed for " + p.GetPlayerName());
            return !WornOnce(p);
        }

        /// Asks the game to update the player's known recipes now (private Player.UpdateKnownRecipesList). Expected outcome: true when the call ran.
        internal static bool RefreshRecipes(Player p)
        {
            if (_updateRecipes == null) _updateRecipes = AccessTools.Method(typeof(Player), "UpdateKnownRecipesList");
            if (_updateRecipes == null) { ModLog.Warn("CostumeUnlock: Player.UpdateKnownRecipesList not found - the recipes show on the game's next update"); return false; }
            _updateRecipes.Invoke(p, null);
            return true;
        }

        // Humanoid.EquipItem postfix (declared in src/Integrations/Harmony/CostumePatches.cs).
        internal static class Equip
        {
            internal static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
            {
                if (!ModSettings.On) return;
                var p = __instance as Player;
                bool local = p != null && p == Player.m_localPlayer;
                if (!CostumeUnlockRules.ShouldMark(local, SlipperStatsPatch.IsSlippers(item), __result, local && WornOnce(p))) return;
                Mark(p, "equipped the Fluffy Slippers for the first time");
            }
        }
    }
}
