using Kydl.Common;
using SteveTheTroll.Bootstrap;
using UnityEngine;

namespace SteveTheTroll.Integrations.Unity
{
    /// <summary>
    /// D-100 (user: "we have to make sure it all works as it should"): every asset the mod loads from its AssetBundle "stevethetroll", by
    /// name, in ONE place. Code loads only through Load (integration test: no LoadAsset anywhere else); the integration tests check every
    /// name against the bundle's manifest, the in-game test World_BundleAssets loads each one with its type from the real bundle.
    /// </summary>
    internal static class BundleAssets
    {
        // prefabs
        internal const string SlipperParts = "SlipperParts", SlipperSkinParts = "SlipperSkinParts", CostumeHelmetParts = "CostumeHelmetParts", CostumeChestParts = "CostumeChestParts",
            CostumeLegsParts = "CostumeLegsParts", ClubParts = "ClubParts", CostumeChestGround = "CostumeChestGround", CostumeLegsGround = "CostumeLegsGround",
            CostumeViewer = "CostumeViewer";
        // sprites (item / status effect / map icons)
        internal const string SlipperIcon = "SlipperIcon", CostumeHelmetIcon = "CostumeHelmetIcon", CostumeChestIcon = "CostumeChestIcon",
            CostumeLegsIcon = "CostumeLegsIcon", TrollClubIcon = "TrollClubIcon", SetIcon = "SteveTheTrollSetIcon";

        /// Name and type of every asset above (World_BundleAssets loads each; the integration tests compare it with the constants).
        internal static readonly System.Collections.Generic.KeyValuePair<string, System.Type>[] All =
        {
            Of<GameObject>(SlipperParts), Of<GameObject>(SlipperSkinParts), Of<GameObject>(CostumeHelmetParts), Of<GameObject>(CostumeChestParts), Of<GameObject>(CostumeLegsParts),
            Of<GameObject>(ClubParts), Of<GameObject>(CostumeChestGround), Of<GameObject>(CostumeLegsGround), Of<GameObject>(CostumeViewer),
            Of<Sprite>(SlipperIcon), Of<Sprite>(CostumeHelmetIcon), Of<Sprite>(CostumeChestIcon), Of<Sprite>(CostumeLegsIcon), Of<Sprite>(TrollClubIcon),
            Of<Sprite>(SetIcon),
        };

        static System.Collections.Generic.KeyValuePair<string, System.Type> Of<T>(string name) { return new System.Collections.Generic.KeyValuePair<string, System.Type>(name, typeof(T)); }

        /// The asset, or null (warned) when the bundle is not loaded or does not have it.
        internal static T Load<T>(string name) where T : Object
        {
            var bundle = Plugin.Bundle;
            var a = bundle != null ? bundle.LoadAsset<T>(name) : null;
            if (a == null) ModLog.Warn("BundleAssets: " + typeof(T).Name + " '" + name + "' " + (bundle == null ? "- no bundle loaded" : "not in the bundle"));
            return a;
        }
    }
}
