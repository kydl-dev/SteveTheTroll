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
        public static ConfigEntry<KeyCode> AttackKey;
        // [DebugLog]
        public static ConfigEntry<bool> DebugLog;

        /// Is the mod switched on ([General] EnableAll)? Off = inert: no effects, no menus; nothing built is removed.
        public static bool On { get { return EnableAll == null || EnableAll.Value; } }

        // --- dev-only fixed values (Tunables): not in the config file, KydlTools changes them for testing -----------------------------------
        // --- T-008a troll taming (D-037 / D-038): the user's numbers. ---
        public static readonly Tunable<float> TameSeconds = new Tunable<float>("TrollTaming", "TameSeconds", 60f, 5f, 600f, "Seconds a wild troll needs a slipper wearer near it to become tamed (user: 60 s).");
        public static readonly Tunable<float> TameRange = new Tunable<float>("TrollTaming", "TameRange", 250f, 10f, 300f, "Metres from the wearer within which a wild troll is being tamed (user: 150 m; F7 value 250 m baked 2026-10-28).");
        public static readonly Tunable<float> OwnTrollRange = new Tunable<float>("TrollTaming", "OwnTrollRange", 300f, 50f, 1000f, "Metres: a wearer whose own tamed troll is this close tames no second one; that troll walks to the wearer instead (user: 300 m).");

        // --- D-042 tamed trolls (always while tamed) and the troll spawn boost: the user's numbers. ---
        public static readonly Tunable<float> TamedHealthFactor = new Tunable<float>("TrollTamed", "HealthFactor", 3f, 1f, 10f, "Tamed troll max health x this (user: 3).");
        public static readonly Tunable<float> TamedDamageFactor = new Tunable<float>("TrollTamed", "DamageFactor", 3f, 1f, 10f, "Tamed troll attack damage x this (user: 3).");
        public static readonly Tunable<float> DefendRange = new Tunable<float>("TrollTamed", "DefendRange", 50f, 5f, 100f, "Metres: own tamed trolls this close defend the player when the player is hit (user: 50 m, TEST-021).");
        public static readonly Tunable<float> FarmRange = new Tunable<float>("TrollTamed", "FarmRange", 50f, 5f, 100f, "Metres around where a troll was sent (then around the owner's bed) in which it looks for its focus item (user: 50 m, TEST-021).");
        public static readonly Tunable<float> FarmQuickRange = new Tunable<float>("TrollTamed", "FarmQuickRange", 20f, 5f, 60f, "Metres: first, fast search around the troll (focus item, then the other allowed items) (user: 20 m).");
        public static readonly Tunable<float> FarmMaxRange = new Tunable<float>("TrollTamed", "FarmMaxRange", 150f, 50f, 200f, "Metres: last search around the troll; nothing here -> it walks to the owner's bed (user: 150 m).");
        public static readonly Tunable<float> FarmReach = new Tunable<float>("TrollTamed", "FarmReach", 2f, 1f, 6f, "Metres (flat) from a tree trunk / log / rock at which a farming troll stops walking and swings (user: 2 m, TEST-021).");
        public static readonly Tunable<float> AutoFarmSeconds = new Tunable<float>("TrollTamed", "AutoFarmSeconds", 120f, 10f, 600f, "Seconds a newly tamed troll waits at the player for an order; no order -> it farms the default priority on its own (user: 2 minutes).");
        public static readonly Tunable<float> HoldNearPlayerRange = new Tunable<float>("TrollTamed", "HoldNearPlayerRange", 4f, 0f, 15f, "Metres: a tamed troll that is not following stands still while a player is this close (D-063, to open the backpack; 0 = off).");
        public static readonly Tunable<bool> TrollMapPins = new Tunable<bool>("TrollTamed", "MapPins", true, "Shows each own tamed troll on the map (costume head icon) at its live / last known place (D-081).");
        public static readonly Tunable<float> RageRange = new Tunable<float>("TrollTamed", "RageRange", 10f, 0f, 30f, "Metres: own trolls this close to the player go into rage when the player is hit - swinging non-stop at everything hostile around (D-082; 0 = off).");
        public static readonly Tunable<float> RageCalmSeconds = new Tunable<float>("TrollTamed", "RageCalmSeconds", 3f, 1f, 15f, "Seconds without a hit on the player before an enraged troll calms down (D-082).");
        public static readonly Tunable<float> CounterIgnoreSeconds = new Tunable<float>("TrollTamed", "CounterIgnoreSeconds", 15f, 0f, 120f, "Seconds a troll ignores hits on itself and keeps farming after it answered a hit (D-084: 3 slams / going for the enemy).");
        public static readonly Tunable<float> CounterChaseSeconds = new Tunable<float>("TrollTamed", "CounterChaseSeconds", 30f, 5f, 120f, "Longest a troll goes after a creature that hit it again after the quiet time (D-084), in seconds.");
        public static readonly Tunable<float> TrollZoneRadius = new Tunable<float>("TrollTamed", "ZoneRadius", 50f, 0f, 120f, "Metres around each own troll outside your area that stay loaded so it keeps farming (D-084; 0 = off, the game's areas only).");
        public static readonly Tunable<float> BedHealPerSecond = new Tunable<float>("TrollTamed", "BedHealPerSecond", 50f, 0f, 500f, "HP per second own tamed trolls regain near the bed (D-085; 0 = off).");
        public static readonly Tunable<float> BedHealRange = new Tunable<float>("TrollTamed", "BedHealRange", 50f, 5f, 200f, "Metres around the bed where own tamed trolls regain BedHealPerSecond (D-085).");
        public static readonly Tunable<float> GuardOwnerRange = new Tunable<float>("TrollTamed", "GuardOwnerRange", 20f, 5f, 60f, "Metres: a troll only runs at a creature that goes for the PLAYER when that creature is this close to the troll (D-081; creatures that attack the troll itself or hit the player: DefendRange).");
        public static readonly Tunable<int> CrowdCount = new Tunable<int>("TrollTamed", "CrowdCount", 3, 2, 10, "This many creatures attacking the player / troll at once -> the troll does 4 ground slams first (D-065).");
        public static readonly Tunable<float> DeadKeepSeconds = new Tunable<float>("TrollTamed", "DeadKeepSeconds", 60f, 5f, 600f, "Seconds a dead troll stays in the F6 lists before it is removed and its name is free again (user: 60 s).");
        public static readonly Tunable<float> SpawnBoostSeconds = new Tunable<float>("TrollTamed", "SpawnBoostSeconds", 120f, 0f, 1800f, "Seconds between troll spawn tries (100 % chance, day and night) near a slipper wearer without a tamed troll; the game's own entry tries every 4000 s with 5 % (D-059; 0 = off).");
        public static readonly Tunable<float> SpawnBedRadius = new Tunable<float>("TrollTamed", "SpawnBedRadius", 500f, 50f, 2000f, "Metres around the bed where troll spawns are boosted (D-084: cap 2 with the slippers and 0-1 tamed trolls, 6 with the whole set and up to 5; outside: the game's own rates).");
        public static readonly Tunable<float> SpawnBedMinRadius = new Tunable<float>("TrollTamed", "SpawnBedMinRadius", 200f, 0f, 1000f, "D-094: boosted trolls spawn in any biome, but never closer than this to the bed (they must not wreck the house before they are tamed). Black Forest trolls closer than this do not spawn either while the boost runs.");

        // --- T-008b backpack on tamed trolls (D-053): placement behind Spine2 in the troll's frame; the user tunes it in game (F7). ---
        public static readonly Tunable<float> PickupRange = new Tunable<float>("TrollBackpack", "PickupRange", 50f, 5f, 100f, "Metres around a tamed troll from which wood, ores and scrap go into its backpack (user: 50 m).");
        public static readonly Tunable<float> BackpackScale = new Tunable<float>("TrollBackpack", "Scale", 0.671f, 0.1f, 1.5f, "World size of the backpack (1 = the construction's 2 x 2 x 2 m).");
        public static readonly Tunable<float> BackpackBack = new Tunable<float>("TrollBackpack", "Back", -0.733f, -1f, 3f, "Metres behind the troll's upper spine (Spine2) where the struts sit.");
        public static readonly Tunable<float> BackpackUp = new Tunable<float>("TrollBackpack", "Up", 0.127f, -3f, 3f, "Metres up (+) / down (-) from the upper spine.");
        public static readonly Tunable<float> BackpackSide = new Tunable<float>("TrollBackpack", "Side", 0f, -2f, 2f, "Metres to the troll's right (+) / left (-).");
        public static readonly Tunable<float> BackpackPitch = new Tunable<float>("TrollBackpack", "Pitch", 0f, -90f, 90f, "Tilt forward / back, degrees.");
        public static readonly Tunable<float> BackpackYaw = new Tunable<float>("TrollBackpack", "Yaw", 0f, -180f, 180f, "Turn around the up axis, degrees.");
        public static readonly Tunable<float> BackpackRoll = new Tunable<float>("TrollBackpack", "Roll", 0f, -90f, 90f, "Lean left / right, degrees.");

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

        // --- D-020 fluffy fur on the mini trolls (shell fur, bundle material "TrollFur"); values of the Unity preview fur_v4 (user: v2 "kind of bad" -> denser, shorter, softer, lighter; D-026)
        public static readonly Tunable<bool> FurEnable = new Tunable<bool>("TrollFur", "Enable", false, "Fur strands (shells) on the mini trolls. Off by default since D-027 (the user chose the smooth plush look).");
        public static readonly Tunable<int> FurShells = new Tunable<int>("TrollFur", "Shells", 4, 4, 32, "Number of fur layers (more = smoother fur, more cost).");
        public static readonly Tunable<float> FurLength = new Tunable<float>("TrollFur", "Length", 0.00218f, 0.001f, 0.04f, "Fur length in metres.");
        public static readonly Tunable<float> FurDensity = new Tunable<float>("TrollFur", "Density", 545f, 50f, 1500f, "Strands per texture unit (higher = finer fur).");
        public static readonly Tunable<float> FurThickness = new Tunable<float>("TrollFur", "Thickness", 0.864f, 0.2f, 1.5f, "Strand thickness at the root (share of a strand cell).");
        public static readonly Tunable<float> FurDroop = new Tunable<float>("TrollFur", "Droop", 0.02f, 0f, 0.02f, "How far the tips hang down, in metres.");
        // D-083 (user: "change them to 0-255 ... with 0-255 I know exactly"): colour as R / G / B 0..255 (as any colour picker) + a Brightness factor;
        // the old multipliers 0.73 / 0.4545 / 0.6 = colour (186, 116, 153) x 1.0
        public static readonly Tunable<int> FurColorR = new Tunable<int>("TrollFur", "ColorR", 186, 0, 255, "Fur colour, red (0-255).");
        public static readonly Tunable<int> FurColorG = new Tunable<int>("TrollFur", "ColorG", 116, 0, 255, "Fur colour, green (0-255).");
        public static readonly Tunable<int> FurColorB = new Tunable<int>("TrollFur", "ColorB", 153, 0, 255, "Fur colour, blue (0-255).");
        public static readonly Tunable<float> FurBrightness = new Tunable<float>("TrollFur", "Brightness", 1f, 0f, 1.5f, "Brightness factor on the fur colour (1 = the colour as set).");
        public static readonly Tunable<float> FurTipLight = new Tunable<float>("TrollFur", "TipLight", 1.01f, 0.3f, 1.5f, "Brightness of the fur tips (1 = like the roots before darkening).");

        // --- D-027 smooth plush look of the mini trolls (user picked style D): the troll skin re-coloured, no strands
        public static readonly Tunable<bool> PlushEnable = new Tunable<bool>("TrollPlush", "Enable", true, "Smooth plush colour on the mini trolls (off = the game's troll skin).");
        // D-083: the old multipliers 1.0 / 1.2 / 1.25 = colour (204, 245, 255) x brightness 1.25 (the troll skin texture is multiplied by it)
        public static readonly Tunable<int> PlushColorR = new Tunable<int>("TrollPlush", "ColorR", 204, 0, 255, "Plush colour on the troll skin, red (0-255).");
        public static readonly Tunable<int> PlushColorG = new Tunable<int>("TrollPlush", "ColorG", 245, 0, 255, "Plush colour on the troll skin, green (0-255).");
        public static readonly Tunable<int> PlushColorB = new Tunable<int>("TrollPlush", "ColorB", 255, 0, 255, "Plush colour on the troll skin, blue (0-255).");
        public static readonly Tunable<float> PlushBrightness = new Tunable<float>("TrollPlush", "Brightness", 1.25f, 0f, 2f, "Brightness factor on the plush colour (1 = the colour as set; above 1 lightens the dark troll skin).");

        /// D-083: a colour from 0..255 channels and a brightness factor (alpha 1).
        public static UnityEngine.Color Rgb(Tunable<int> r, Tunable<int> g, Tunable<int> b, Tunable<float> brightness)
        {
            float k = brightness.Value / 255f;
            return new UnityEngine.Color(r.Value * k, g.Value * k, b.Value * k, 1f);
        }
        public static readonly Tunable<float> PlushSmoothness = new Tunable<float>("TrollPlush", "Smoothness", 0.0727f, 0f, 0.6f, "Shine of the plush (0 = matt fabric; 0.07 = the user's TEST-008 choice).");

        // --- D-025 slipper on each foot (bundle prefab "SlipperParts", skinned to the player's foot + toe bones). Placement defaults = Unity fit.
        public static readonly Tunable<float> SlipperBrightness = new Tunable<float>("Slipper", "Brightness", 0.84f, 0.3f, 1.7f, "Brightness of the blue plush slipper (1 = as modelled; 0.84 = the user's choice, TEST-005).");
        public static readonly Tunable<float> SlipperRimBrightness = new Tunable<float>("Slipper", "RimBrightness", 0.88f, 0.3f, 1.7f, "Brightness of the fluffy rim around the opening (1 = as modelled; 0.88 = the user's choice, TEST-005).");
        public static readonly Tunable<float> SlipperForward = new Tunable<float>("Slipper", "OffsetForward", Fit.TrollFitData.SlipperForward, -0.15f, 0.25f, "Slipper middle along the foot from the foot bone, in metres.");
        public static readonly Tunable<float> SlipperUp = new Tunable<float>("Slipper", "OffsetUp", Fit.TrollFitData.SlipperUp, -0.25f, 0f, "Slipper ground plane below the foot bone, in metres.");
        public static readonly Tunable<float> SlipperSide = new Tunable<float>("Slipper", "OffsetSide", Fit.TrollFitData.SlipperSide, -0.1f, 0.1f, "Moves the slippers outward (+) / inward (-), in metres.");
        public static readonly Tunable<float> SlipperYaw = new Tunable<float>("Slipper", "Yaw", Fit.TrollFitData.SlipperYaw, -45f, 45f, "Turns the slippers around the up axis, in degrees.");
        public static readonly Tunable<float> SlipperScale = new Tunable<float>("Slipper", "Scale", Fit.TrollFitData.SlipperScale, 0.6f, 1.6f, "Size of the slippers (1 = as modelled, 0.40 m long).");
        public static readonly Tunable<float> SlipperWidth = new Tunable<float>("Slipper", "Width", Fit.TrollFitData.SlipperWidth, 0.6f, 1.3f, "Width of the slippers across the foot (1 = as modelled, 16 cm at the widest; the user's TEST-008 choice 1.05 with Scale 1.1).");

        // --- D-077 Troll Club in the hand (TEST-032: "the club goes into the head mesh"): turns / slides the club around the grip, live (F7).
        // Axes in the hand: Outward = the tip towards the player's right in the rest pose, Up = the tip upwards, Twist = round the club.
        public static readonly Tunable<float> ClubOutward = new Tunable<float>("Club", "TiltOutward", 20f, -60f, 60f, "Tilts the Troll Club's tip away from the head (degrees, + = outward).");
        public static readonly Tunable<float> ClubUp = new Tunable<float>("Club", "TiltUp", 0f, -60f, 60f, "Tilts the Troll Club's tip up (+) / down (-), in degrees.");
        public static readonly Tunable<float> ClubTwist = new Tunable<float>("Club", "Twist", 0f, -180f, 180f, "Turns the Troll Club round its own length, in degrees.");
        public static readonly Tunable<float> ClubSlide = new Tunable<float>("Club", "Slide", 0f, -0.4f, 0.4f, "Slides the Troll Club through the hand (metres, + = the hand holds it further down).");

        /// Expected outcome: every player entry is bound (returns that check).
        public static bool Bind(ConfigFile c)
        {
            var savedLines = ConfigTools.Capture(c);     // before the first Bind: every Bind saves the file with what BepInEx parsed

            EnableAll = c.Bind("General", "EnableAll", true,
                "Switches the whole mod on / off while playing. Off: no effects and no menus from this mod; things already built stay in the world.");
            MenuKey = c.Bind("General", "MenuKey", KeyCode.F6,
                "Opens / closes the SteveTheTroll menu. None = no menu.");
            AttackKey = c.Bind("Controls", "AttackKey", KeyCode.Mouse3,
                "Look at a creature, tree, log or rock and press this: your nearest tamed troll attacks / breaks it (Mouse3 = the mouse's back button, Mouse 4). Looking at nothing: it goes farming. None = off.");
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
