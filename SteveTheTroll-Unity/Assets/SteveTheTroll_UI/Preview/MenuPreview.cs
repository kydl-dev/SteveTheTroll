using Kydl.Ui;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SteveTheTroll.Ui
{
    /// <summary>
    /// Unity-only preview of the mod's menu (NOT compiled into the mod: only Runtime/ is). Add this component to any GameObject of an empty
    /// scene and press Play: the menu opens in the Game view in the same look as in game (game fonts from Assets/Kydl.Ui/Preview/GameAssets).
    /// Set the Game view to 1920x1080 (the panel scales from that like in game).
    /// </summary>
    public sealed class MenuPreview : MonoBehaviour
    {
        const string FontFolder = "Assets/Kydl.Ui/Preview/GameAssets/";
        [SerializeField] int startTab;                      // 0 Slippers, 1 Troll work, 2 Trolls: check a tab without clicking

        void OnEnable()
        {
            LoadGameFonts();
            UiKit.Reset();
            var root = UiKit.Root;
            if (root == null) { Debug.LogError("[MenuPreview] UI Toolkit panel could not be made"); return; }
            var trolls = new System.Collections.Generic.List<TrollInfo>
            {
                new TrollInfo { Id = "1:1", Number = 1, Loaded = true, Status = "stevethetroll_status_following", Focus = "CopperOre", Health = 1800, MaxHealth = 1800, DamageFactor = 3f, X = 412, Y = 77, Z = 268, BedDistance = 35 },
                new TrollInfo { Id = "1:2", Number = 2, Name = "Steve", Stars = 1, Status = "stevethetroll_status_staying", Health = 2400, MaxHealth = 3600, DamageFactor = 3f, X = 800, Y = 60, Z = -120, BedDistance = 420,
                    LastSeenTicks = System.DateTime.UtcNow.Ticks - System.TimeSpan.TicksPerMinute * 12 },
            };
            root.Add(MainMenuView.Build(UiText.English, () => Debug.Log("[MenuPreview] close pressed"), SlipperData.Sample(), null, null, trolls,
                (id, v) => Debug.Log("[MenuPreview] troll " + id + " focus '" + v + "'"), id => Debug.Log("[MenuPreview] go farm " + id), startTab, System.DateTime.UtcNow.Ticks, CostumeData.Sample()));
            Debug.Log("[MenuPreview] SteveTheTroll menu shown");
        }

        void OnDisable() { UiKit.Reset(); }

        // UiKit finds the game fonts by name among the loaded fonts (Norse, Norsebold, NotoEmoji-Regular): load them first.
        static void LoadGameFonts()
        {
#if UNITY_EDITOR
            foreach (var f in new[] { "Norse.otf", "Norsebold.otf", "NotoEmoji-Regular.ttf" })
                if (AssetDatabase.LoadAssetAtPath<Font>(FontFolder + f) == null) Debug.LogWarning("[MenuPreview] font missing: " + FontFolder + f + " (Kydl > Sync shared UI kit)");
#endif
        }
    }
}
