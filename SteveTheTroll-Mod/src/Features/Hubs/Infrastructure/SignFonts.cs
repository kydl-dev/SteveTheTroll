using System;
using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Ui;
using TMPro;
using UnityEngine;

namespace SteveTheTroll.Features.Hubs.Infrastructure;

/// <summary>
///     D-135: the TextMeshPro fonts loaded in the game and putting a style on a sign's text (copied from the pool sign;
///     the size is NOT part of the
///     style here: ModSettings.HubSignFontSize, fixed, no auto size, no wrapping - all letters on one horizontal line).
/// </summary>
internal static class SignFonts
{
    private static readonly Dictionary<string, TMP_FontAsset> ByName = new();
    private static readonly List<string> Names = new();

    private static string _emoji;
    private static bool _emojiChecked;

    /// The game's emoji font, registered with TextMeshPro so a font tag around each emoji finds it (null = none loaded).
    internal static string EmojiFont
    {
        get
        {
            if (_emojiChecked)
            {
                return _emoji;
            }

            _emojiChecked = true;
            if (Names.Count == 0)
            {
                Refresh();
            }

            foreach (string n in new[] { "NotoEmoji-Regular SDF", "NotoEmoji-Light SDF" })
            {
                TMP_FontAsset f;
                if (!ByName.TryGetValue(n, out f) || f == null)
                {
                    continue;
                }

                MaterialReferenceManager.AddFontAsset(f);
                _emoji = n;
                break;
            }

            return _emoji;
        }
    }

    internal static IList<string> All()
    {
        if (Names.Count == 0)
        {
            Refresh();
        }

        return Names;
    }

    internal static int Refresh()
    {
        ByName.Clear();
        Names.Clear();
        foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (f == null || string.IsNullOrEmpty(f.name) || ByName.ContainsKey(f.name))
            {
                continue;
            }

            ByName[f.name] = f;
            Names.Add(f.name);
        }

        Names.Sort(StringComparer.OrdinalIgnoreCase);
        ModLog.Verbose("SignFonts: " + Names.Count + " TextMeshPro font(s) loaded");
        return Names.Count;
    }

    internal static string Apply(TMP_Text text, SignStyle style, TMP_FontAsset defFont, float size)
    {
        if (text == null)
        {
            return "no text object";
        }

        TMP_FontAsset f = null;
        if (!string.IsNullOrEmpty(style.Font) && (!ByName.TryGetValue(style.Font, out f) || f == null))
        {
            Refresh();
            ByName.TryGetValue(style.Font, out f);
        }

        text.font = f != null ? f : defFont;
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.textWrappingMode = TextWrappingModes.NoWrap; // all letters on one horizontal line
        text.overflowMode = TextOverflowModes.Overflow;
        // the game's sign text material has a dark face colour that darkens every picked colour: a white face per sign
        Material m = text.fontMaterial;
        if (m != null && m.HasProperty(ShaderUtilities.ID_FaceColor))
        {
            m.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        }

        text.color = Color.white;
        return "font " + (f != null ? f.name : "default") + ", size " + size.ToString("0") + ", one line";
    }
}
