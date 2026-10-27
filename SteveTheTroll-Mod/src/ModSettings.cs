using BepInEx.Configuration;
using Kydl.Common;
using UnityEngine;

namespace SteveTheTroll
{
    /// <summary>
    /// All settings in one place (user rule, every mod):
    ///  * PLAYER CONFIG (BepInEx entries below, bound once from Plugin.Awake): only what a player should change. Clear descriptions, ranges,
    ///    drop-downs (enums). Descriptions never mention older versions, old names or old values.
    ///  * DEV-ONLY values (Tunable fields below): fixed values chosen by the user, NOT in the player's file. The user's KydlTools mod (F7) finds
    ///    every static Tunable here by itself and lets the user change it in game - nothing to register.
    /// When the user settles a value in game, it becomes the new fixed value / default here.
    /// </summary>
    internal static class ModSettings
    {
        // [General]
        public static ConfigEntry<bool> EnableAll;
        public static ConfigEntry<KeyCode> MenuKey;
        // [DebugLog]
        public static ConfigEntry<bool> DebugLog;

        /// Is the mod switched on ([General] EnableAll)? Off = inert: no effects, no menus; nothing built is removed.
        public static bool On { get { return EnableAll == null || EnableAll.Value; } }

        // --- dev-only fixed values (Tunables): not in the config file, KydlTools changes them for testing -----------------------------------
        public static readonly Tunable<float> ExampleSpeed = new Tunable<float>("Example", "ExampleSpeed", 1f,
            "Example dev-only value: replace or remove. Read it with ModSettings.ExampleSpeed.Value.");

        // --- runtime troll figure on each slipper (D-013 trial). Defaults = the Unity fit (TrollFitData, D-014/D-015); these Tunables are the
        //     user's in-game fine tune on top of it (KydlTools). Sizes in metres, angles in degrees. Settled values go back into the fit / here.
        public static readonly Tunable<float> FigureHeight = new Tunable<float>("TrollFigure", "Height", Fit.TrollFitData.Height, 0.05f, 0.8f,
            "World height of the seated mini troll on each slipper, in metres (picture: about mid-shin).");
        public static readonly Tunable<float> FigureForward = new Tunable<float>("TrollFigure", "OffsetForward", Fit.TrollFitData.Forward, -0.3f, 0.3f,
            "Moves the troll along the player's facing direction from the foot bone, in metres (+ = toward the toes).");
        public static readonly Tunable<float> FigureUp = new Tunable<float>("TrollFigure", "OffsetUp", Fit.TrollFitData.Up, -0.2f, 0.3f,
            "Height of the troll's lowest point above the foot bone, in metres.");
        public static readonly Tunable<float> FigureSide = new Tunable<float>("TrollFigure", "OffsetSide", Fit.TrollFitData.Side, -0.2f, 0.2f,
            "Moves the troll sideways (+ = outward from the body, mirrored per foot), in metres.");
        public static readonly Tunable<float> FigureYaw = new Tunable<float>("TrollFigure", "Yaw", Fit.TrollFitData.Yaw, -180f, 180f,
            "Turns the troll around the up axis, in degrees (0 = looks where the player looks).");
        public static readonly Tunable<float> PoseSpineX = new Tunable<float>("TrollPose", "SpineX", 0f, -90f, 90f, "Extra Spine0 bend on top of the Unity fit, local X, degrees.");
        public static readonly Tunable<float> PoseUpLegX = new Tunable<float>("TrollPose", "UpLegX", 0f, -90f, 90f, "Extra thigh swing on top of the fit, local X, degrees.");
        public static readonly Tunable<float> PoseUpLegZ = new Tunable<float>("TrollPose", "UpLegZ", 0f, -90f, 90f, "Extra thigh spread on top of the fit, local Z, degrees (mirrored on the right leg).");
        public static readonly Tunable<float> PoseLegX = new Tunable<float>("TrollPose", "LegX", 0f, -90f, 90f, "Extra knee bend on top of the fit, local X, degrees.");
        public static readonly Tunable<float> PoseRightArmX = new Tunable<float>("TrollPose", "RightArmX", 0f, -90f, 90f, "Extra right upper arm (log arm) on top of the fit, local X, degrees.");
        public static readonly Tunable<float> PoseRightArmZ = new Tunable<float>("TrollPose", "RightArmZ", 0f, -90f, 90f, "Extra right upper arm on top of the fit, local Z, degrees.");
        public static readonly Tunable<float> PoseRightForeArmX = new Tunable<float>("TrollPose", "RightForeArmX", 0f, -90f, 90f, "Extra right forearm on top of the fit, local X, degrees.");
        public static readonly Tunable<float> PoseLeftArmX = new Tunable<float>("TrollPose", "LeftArmX", 0f, -90f, 90f, "Extra left upper arm on top of the fit, local X, degrees.");
        public static readonly Tunable<float> PoseLeftArmZ = new Tunable<float>("TrollPose", "LeftArmZ", 0f, -90f, 90f, "Extra left upper arm on top of the fit, local Z, degrees.");
        public static readonly Tunable<float> PoseLeftForeArmX = new Tunable<float>("TrollPose", "LeftForeArmX", 0f, -90f, 90f, "Extra left forearm on top of the fit, local X, degrees.");

        /// Expected outcome: every player entry is bound (returns that check).
        public static bool Bind(ConfigFile c)
        {
            var savedLines = ConfigTools.Capture(c);     // before the first Bind: every Bind saves the file with what BepInEx parsed

            EnableAll = c.Bind("General", "EnableAll", true,
                "Switches the whole mod on / off while playing. Off: no effects and no menus from this mod; things already built stay in the world.");
            MenuKey = c.Bind("General", "MenuKey", KeyCode.F6,
                "Opens / closes the SteveTheTroll menu. None = no menu.");
            DebugLog = c.Bind("DebugLog", "Enable", false,
                "Writes detailed lines about everything the mod does to the BepInEx log (for bug reports). Leave off while playing normally.");

            ConfigTools.ApplyCommaFix(c, savedLines);    // after every Bind: "3,5" typed by hand means 3.5, not 35
            ConfigTools.DropRemoved(c);

            EnableAll.SettingChanged += (s, e) => ModLog.Info("Config: EnableAll = " + EnableAll.Value + (EnableAll.Value ? ", the mod is on" : ", the mod is inert (no effects or menus)"));
            ModLog.Verbose("Config: bound (EnableAll " + EnableAll.Value + ", menu key " + MenuKey.Value + ", debug log " + DebugLog.Value + ")");
            return EnableAll != null && MenuKey != null && DebugLog != null;
        }
    }
}
