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
        public static readonly Tunable<float> ExampleSpeed = new Tunable<float>("Example", "ExampleSpeed", 1f, 0f, 10f,
            "Example dev-only value: replace or remove. Read it with ModSettings.ExampleSpeed.Value.");

        // --- runtime troll figure on each slipper (D-013 trial). Defaults = the Unity fit (TrollFitData, D-014/D-015); these Tunables are the
        //     user's in-game fine tune on top of it (KydlTools). Sizes in metres, angles in degrees. Settled values go back into the fit / here.
        public static readonly Tunable<float> FigureHeight = new Tunable<float>("TrollPlace", "Height", Fit.TrollFitData.Height, 0.05f, 0.8f,
            "World height of the seated mini troll on each slipper, in metres (picture: about mid-shin).");
        public static readonly Tunable<float> FigureForward = new Tunable<float>("TrollPlace", "OffsetForward", Fit.TrollFitData.Forward, -0.3f, 0.3f,
            "Moves the troll along the player's facing direction from the foot bone, in metres (+ = toward the toes).");
        public static readonly Tunable<float> FigureUp = new Tunable<float>("TrollPlace", "OffsetUp", Fit.TrollFitData.Up, -0.2f, 0.3f,
            "Height of the troll's lowest point above the foot bone, in metres.");
        public static readonly Tunable<float> FigureSide = new Tunable<float>("TrollPlace", "OffsetSide", Fit.TrollFitData.Side, -0.2f, 0.2f,
            "Moves both trolls sideways in their foot's frame (+ = outward from the body, mirrored per foot), in metres.");
        public static readonly Tunable<float> FigureSideLeft = new Tunable<float>("TrollPlace", "OffsetSideLeftFoot", 0f, -0.15f, 0.15f,
            "Extra sideways move of the LEFT-foot troll only (+ = outward, to the player's left), in metres.");
        public static readonly Tunable<float> FigureSideRight = new Tunable<float>("TrollPlace", "OffsetSideRightFoot", 0f, -0.15f, 0.15f,
            "Extra sideways move of the RIGHT-foot troll only (+ = outward, to the player's right), in metres.");
        public static readonly Tunable<float> FigureYaw = new Tunable<float>("TrollPlace", "Yaw", Fit.TrollFitData.Yaw, -180f, 180f,
            "Turns the troll around the up axis, in degrees (0 = looks where its foot points).");
        public static readonly Tunable<float> PoseSpineX = new Tunable<float>("TrollTune", "SpineX", 0f, -90f, 90f, "Extra Spine0 bend on top of the Unity fit, local X, degrees.");
        public static readonly Tunable<float> PoseUpLegX = new Tunable<float>("TrollTune", "UpLegX", 0f, -90f, 90f, "Extra thigh swing on top of the fit, local X, degrees.");
        public static readonly Tunable<float> PoseUpLegZ = new Tunable<float>("TrollTune", "UpLegZ", 0f, -90f, 90f, "Extra thigh spread on top of the fit, local Z, degrees (mirrored on the right leg).");
        public static readonly Tunable<float> PoseLegX = new Tunable<float>("TrollTune", "LegX", 0f, -90f, 90f, "Extra knee bend on top of the fit, local X, degrees.");
        public static readonly Tunable<float> PoseRightArmX = new Tunable<float>("TrollTune", "RightArmX", 0f, -90f, 90f, "Extra right upper arm (log arm) on top of the fit, local X, degrees.");
        public static readonly Tunable<float> PoseRightArmZ = new Tunable<float>("TrollTune", "RightArmZ", 0f, -90f, 90f, "Extra right upper arm on top of the fit, local Z, degrees.");
        public static readonly Tunable<float> PoseRightForeArmX = new Tunable<float>("TrollTune", "RightForeArmX", 0f, -90f, 90f, "Extra right forearm on top of the fit, local X, degrees.");
        public static readonly Tunable<float> PoseLeftArmX = new Tunable<float>("TrollTune", "LeftArmX", 0f, -90f, 90f, "Extra left upper arm on top of the fit, local X, degrees.");
        public static readonly Tunable<float> PoseLeftArmZ = new Tunable<float>("TrollTune", "LeftArmZ", 0f, -90f, 90f, "Extra left upper arm on top of the fit, local Z, degrees.");
        public static readonly Tunable<float> PoseLeftForeArmX = new Tunable<float>("TrollTune", "LeftForeArmX", 0f, -90f, 90f, "Extra left forearm on top of the fit, local X, degrees.");

        // --- D-019 in-game fine tune of the log and the right hand, on top of the Unity fit (all 0 / 1 = exactly the fit)
        public static readonly Tunable<float> LogForward = new Tunable<float>("TrollLog", "OffsetForward", 0f, -0.1f, 0.1f, "Moves the log along the troll's facing direction, in metres.");
        public static readonly Tunable<float> LogUp = new Tunable<float>("TrollLog", "OffsetUp", 0f, -0.1f, 0.1f, "Moves the log up / down, in metres.");
        public static readonly Tunable<float> LogSide = new Tunable<float>("TrollLog", "OffsetSide", 0f, -0.1f, 0.1f, "Moves the log to the troll's right (+) / left (-), in metres.");
        public static readonly Tunable<float> LogYaw = new Tunable<float>("TrollLog", "Yaw", 0f, -90f, 90f, "Turns the log left / right around the up axis, in degrees.");
        public static readonly Tunable<float> LogPitch = new Tunable<float>("TrollLog", "Pitch", 0f, -90f, 90f, "Tilts the log up / down (around the troll's right axis), in degrees.");
        public static readonly Tunable<float> LogRoll = new Tunable<float>("TrollLog", "Roll", 0f, -180f, 180f, "Rolls the log around its own length, in degrees.");
        public static readonly Tunable<float> LogLengthScale = new Tunable<float>("TrollLog", "LengthScale", 1f, 0.3f, 2f, "Log length as a factor of the fitted length (1 = as fitted).");
        public static readonly Tunable<float> HandWristX = new Tunable<float>("TrollHand", "WristX", 0f, -90f, 90f, "Extra right-wrist turn, local X, degrees.");
        public static readonly Tunable<float> HandWristY = new Tunable<float>("TrollHand", "WristY", 0f, -90f, 90f, "Extra right-wrist turn, local Y, degrees.");
        public static readonly Tunable<float> HandWristZ = new Tunable<float>("TrollHand", "WristZ", 0f, -90f, 90f, "Extra right-wrist turn, local Z, degrees.");
        public static readonly Tunable<float> HandFingerCurl = new Tunable<float>("TrollHand", "FingerCurl", 0f, -60f, 60f, "Extra curl of index, middle and ring finger (each joint, around the knuckle line), degrees.");
        public static readonly Tunable<float> HandThumbCurl = new Tunable<float>("TrollHand", "ThumbCurl", 0f, -60f, 60f, "Extra curl of the thumb (each joint, around the knuckle line), degrees.");

        // --- D-020 fluffy fur on the mini trolls (shell fur, bundle material "TrollFur"); first values from the Unity preview fur_v2 (the 5% darker was meant for the slipper: D-024)
        public static readonly Tunable<bool> FurEnable = new Tunable<bool>("TrollFur", "Enable", true, "Fluffy fur on the mini trolls (off = the game's troll skin).");
        public static readonly Tunable<int> FurShells = new Tunable<int>("TrollFur", "Shells", 16, 4, 32, "Number of fur layers (more = smoother fur, more cost).");
        public static readonly Tunable<float> FurLength = new Tunable<float>("TrollFur", "Length", 0.01f, 0.001f, 0.04f, "Fur length in metres.");
        public static readonly Tunable<float> FurDensity = new Tunable<float>("TrollFur", "Density", 300f, 50f, 1500f, "Strands per texture unit (higher = finer fur).");
        public static readonly Tunable<float> FurThickness = new Tunable<float>("TrollFur", "Thickness", 1.0f, 0.2f, 1.5f, "Strand thickness at the root (share of a strand cell).");
        public static readonly Tunable<float> FurDroop = new Tunable<float>("TrollFur", "Droop", 0.003f, 0f, 0.02f, "How far the tips hang down, in metres.");
        public static readonly Tunable<float> FurTintR = new Tunable<float>("TrollFur", "TintR", 0.62f, 0f, 1.5f, "Fur colour multiplier, red.");
        public static readonly Tunable<float> FurTintG = new Tunable<float>("TrollFur", "TintG", 0.85f, 0f, 1.5f, "Fur colour multiplier, green.");
        public static readonly Tunable<float> FurTintB = new Tunable<float>("TrollFur", "TintB", 0.95f, 0f, 1.5f, "Fur colour multiplier, blue.");
        public static readonly Tunable<float> FurTipLight = new Tunable<float>("TrollFur", "TipLight", 0.95f, 0.3f, 1.5f, "Brightness of the fur tips (1 = like the roots before darkening).");

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
