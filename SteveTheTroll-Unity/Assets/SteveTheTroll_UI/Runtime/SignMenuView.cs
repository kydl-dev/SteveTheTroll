using System;
using System.Collections.Generic;
using KydlCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteveTheTroll.Ui;

/// <summary>What the sign menu needs from the outside: the game (HubSignMenu in the mod) or the Unity preview.</summary>
internal interface ISignMenuHost
{
    IList<string> Fonts { get; } // every font that can be chosen ("" = default is added by the view)
    int MaxLetters { get; }
    string T(string key, params string[] args); // localized text
    void Preview(string name, SignStyle style); // show the not-yet-saved look on the sign
    void Save(string name, SignStyle style);
    void Close(string why);
    int SavePreferred(SignStyle style); // characters written, 0 = failed
    SignStyle LoadPreferred(); // null = none saved
}

/// <summary>
///     T-023 / T-56R: the Troll Hub sign menu (copied from the pool sign, D-135: no size control) (UI Toolkit). Name,
///     emoji, font (every font the host lists), size, bold / italic / underline / strike /
///     upper case, alignment, letter spacing, colour, a colour per letter (select: paint / rainbow / gradient / clear),
///     preferred style
///     (save / use), reset to the default style, Save / Cancel. Shared by the game and the Unity preview scene
///     (Assets/DTP_UI/Preview).
/// </summary>
internal sealed class SignMenuView
{
    private static readonly string[] Emoji =
    {
        "\U0001F41F", "\U0001F4A7", "\U0001F30A", "\U0001F525", "⚓", "⭐", "❤", "☠", "\U0001F37A", "\U0001F33F", "❄", "⚔"
    };

    private readonly VisualElement _content;

    private readonly ISignMenuHost _host;
    private readonly HashSet<int> _selected = new();
    internal readonly VisualElement Window;
    private VisualElement _letters;
    private string _letterColour = "#FF0000";
    private Label _status;

    internal SignMenuView(ISignMenuHost host, string name, SignStyle style)
    {
        _host = host;
        Name = name ?? "";
        Style = style != null ? style.Clone() : new SignStyle();
        UiScroll body;
        Window = UiKit.Window(_host.T("stevethetroll_sign_title"), _host.T("stevethetroll_sign_esc_close"),
            () => _host.Close("X"), out body);
        _content = body.Content;
        Build();
        UiLog.Verbose("SignMenuView: built for '" + Name + "' (" + _host.Fonts.Count + " font(s), style " + Style +
                      ")");
    }

    internal string Name { get; private set; }

    internal SignStyle Style { get; private set; }

    private void Changed(string what)
    {
        Style.Clamp();
        _host.Preview(Name, Style);
        UiLog.Verbose("SignMenuView: " + what + " -> '" + Name + "' " + Style);
    }

    private void Status(string text)
    {
        if (_status != null)
        {
            _status.text = text;
        }

        UiLog.Verbose("SignMenuView: " + text);
    }

    // --- layout --------------------------------------------------------------------------------------------------------
    private void Build()
    {
        _content.Clear();

        _content.Add(UiControls.Heading(_host.T("stevethetroll_sign_name")));
        _content.Add(UiControls.TextField(Name, _host.MaxLetters * 2, v =>
        {
            Name = v;
            RebuildLetters();
            Changed("name");
        }));
        var emo = new VisualElement();
        emo.style.flexDirection = FlexDirection.Row;
        emo.style.flexWrap = Wrap.Wrap;
        emo.style.flexShrink = 0;
        foreach (string e in Emoji)
        {
            string em = e;
            emo.Add(UiControls.Button(em, () => AddEmoji(em)));
        }

        _content.Add(UiControls.Row(_host.T("stevethetroll_sign_emoji"), emo));
        _content.Add(UiControls.Note(_host.T("stevethetroll_sign_emoji_note")));

        _content.Add(UiControls.Heading(_host.T("stevethetroll_sign_text")));
        var fonts = new List<string> { "" };
        fonts.AddRange(_host.Fonts);
        _content.Add(UiControls.Row(_host.T("stevethetroll_sign_font"), UiControls.Dropdown(fonts, Style.Font,
            f => f.Length == 0 ? _host.T("stevethetroll_sign_font_default") : f, f =>
            {
                Style.Font = f;
                Changed("font");
            })));
        _content.Add(UiControls.Row(_host.T("stevethetroll_sign_style"), UiControls.Line(
            UiControls.Toggle(_host.T("stevethetroll_sign_bold"), Style.Bold, on =>
            {
                Style.Bold = on;
                Changed("bold");
            }),
            UiControls.Toggle(_host.T("stevethetroll_sign_italic"), Style.Italic, on =>
            {
                Style.Italic = on;
                Changed("italic");
            }),
            UiControls.Toggle(_host.T("stevethetroll_sign_underline"), Style.Underline, on =>
            {
                Style.Underline = on;
                Changed("underline");
            }),
            UiControls.Toggle(_host.T("stevethetroll_sign_strike"), Style.Strike, on =>
            {
                Style.Strike = on;
                Changed("strike");
            }),
            UiControls.Toggle(_host.T("stevethetroll_sign_upper"), Style.Upper, on =>
            {
                Style.Upper = on;
                Changed("upper case");
            }))));
        var aligns = new List<string> { "l", "c", "r" };
        _content.Add(UiControls.Row(_host.T("stevethetroll_sign_align"), UiControls.Dropdown(aligns,
            Style.Align.ToString(), AlignName, a =>
            {
                Style.Align = a[0];
                Changed("align");
            })));
        _content.Add(UiControls.Row(_host.T("stevethetroll_sign_spacing"), UiControls.Slider(-20f, 50f, Style.Spacing,
            "0", v =>
            {
                Style.Spacing = v;
                Changed("spacing");
            })));

        _content.Add(UiControls.Heading(_host.T("stevethetroll_sign_colour")));
        Action<string> setBase;
        _content.Add(UiControls.ColorPicker(Style.Color, BaseColour, out setBase));

        _content.Add(UiControls.Heading(_host.T("stevethetroll_sign_letters")));
        _content.Add(UiControls.Note(_host.T("stevethetroll_sign_letters_note")));
        _letters = new VisualElement();
        _letters.style.flexDirection = FlexDirection.Row;
        _letters.style.flexWrap = Wrap.Wrap;
        _letters.style.flexShrink = 0;
        _content.Add(_letters);
        RebuildLetters();
        _content.Add(UiControls.Line(
            UiControls.Button(_host.T("stevethetroll_sign_all"), SelectAll),
            UiControls.Button(_host.T("stevethetroll_sign_none"), () =>
            {
                _selected.Clear();
                RebuildLetters();
            }),
            UiControls.Button(_host.T("stevethetroll_sign_paint"), Paint),
            UiControls.Button(_host.T("stevethetroll_sign_rainbow"), Rainbow),
            UiControls.Button(_host.T("stevethetroll_sign_gradient"), Gradient),
            UiControls.Button(_host.T("stevethetroll_sign_clear"), ClearLetters)));
        Action<string> setLetter;
        _content.Add(UiControls.ColorPicker(_letterColour, LetterColour, out setLetter));

        _content.Add(UiControls.Heading(_host.T("stevethetroll_sign_pref")));
        _content.Add(UiControls.Line(
            UiControls.Button(_host.T("stevethetroll_sign_pref_save"), () =>
            {
                int n = _host.SavePreferred(Style);
                Status(_host.T(n > 0 ? "stevethetroll_sign_pref_saved" : "stevethetroll_sign_pref_failed"));
            }),
            UiControls.Button(_host.T("stevethetroll_sign_pref_use"), UsePreferred),
            UiControls.Button(_host.T("stevethetroll_sign_reset"), ResetStyle)));

        _status = UiControls.Note("");
        _content.Add(_status);
        VisualElement bottom = UiControls.Line(
            UiControls.Button(_host.T("stevethetroll_sign_save"), () => _host.Save(Name, Style), true),
            UiControls.Button(_host.T("stevethetroll_sign_cancel"), () => _host.Close("Cancel")));
        bottom.style.justifyContent = Justify.FlexEnd;
        bottom.style.marginTop = 12;
        _content.Add(bottom);
    }

    private static VisualElement Grow(VisualElement e)
    {
        e.style.flexGrow = 1;
        e.style.minWidth = 200;
        return e;
    }

    private string AlignName(string a)
    {
        return _host.T(a == "l" ? "stevethetroll_sign_left" :
            a == "r" ? "stevethetroll_sign_right" : "stevethetroll_sign_center");
    }

    // --- letters ---------------------------------------------------------------------------------------------------------
    private void RebuildLetters()
    {
        if (_letters == null)
        {
            return;
        }

        _letters.Clear();
        List<string> letters = SignStyle.SplitLetters(Name);
        _selected.RemoveWhere(i => i >= letters.Count);
        for (int i = 0; i < letters.Count; i++)
        {
            int idx = i;
            string hex;
            if (!Style.Letters.TryGetValue(i, out hex))
            {
                hex = Style.Color;
            }

            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            var chip = new Label(letters[i]);
            chip.style.minWidth = 36;
            chip.style.height = 40;
            chip.style.unityTextAlign = TextAnchor.MiddleCenter;
            chip.style.fontSize = 24;
            chip.style.color = c;
            chip.style.marginRight = 4;
            chip.style.marginTop = 4;
            chip.style.flexShrink = 0;
            bool sel = _selected.Contains(i);
            chip.style.backgroundColor = UiKit.Track;
            UiKit.Radius(chip, 4);
            UiKit.Borders(chip, sel ? 2 : 1, sel ? UiKit.Gold : UiKit.Border);
            chip.RegisterCallback<PointerDownEvent>(e =>
            {
                if (!_selected.Remove(idx))
                {
                    _selected.Add(idx);
                }

                UiLog.Verbose("SignMenuView: letter " + idx + " " +
                              (_selected.Contains(idx) ? "selected" : "unselected"));
                RebuildLetters();
            });
            _letters.Add(chip);
        }
    }

    private void SelectAll()
    {
        _selected.Clear();
        int n = SignStyle.SplitLetters(Name).Count;
        for (int i = 0; i < n; i++)
        {
            _selected.Add(i);
        }

        RebuildLetters();
    }

    private List<int> Sel()
    {
        var l = new List<int>(_selected);
        l.Sort();
        return l;
    }

    // T-67 (user: "clicking on the colors under the general Colors or the Letter-Colors does not actually change the color"; log 10:31:
    // col changed every time, but every letter had its own colour from an old gradient, which covers the text colour; and the letter
    // colour was only used by Paint). Text colour: when EVERY letter has its own colour they are cleared, so the new colour shows.
    // Letter colour: selected letters take it at once (preset, sliders, hex), Paint still works.
    private void BaseColour(string h)
    {
        Style.Color = h;
        int n = SignStyle.SplitLetters(Name).Count, own = 0;
        for (int i = 0; i < n; i++)
        {
            if (Style.Letters.ContainsKey(i))
            {
                own++;
            }
        }

        if (n > 0 && own == n)
        {
            Style.Letters.Clear();
            Status(_host.T("stevethetroll_sign_letters_reset"));
            UiLog.Verbose("SignMenuView: all " + n + " letters had their own colour, cleared for the text colour " + h);
        }

        RebuildLetters();
        Changed("colour " + h);
    }

    private void LetterColour(string h)
    {
        _letterColour = h;
        List<int> sel = Sel();
        if (sel.Count == 0)
        {
            UiLog.Verbose("SignMenuView: letter colour " + h + " picked, no letter selected (Paint / Gradient use it)");
            return;
        }

        foreach (int i in sel)
        {
            Style.Letters[i] = h;
        }

        RebuildLetters();
        Changed("letter colour " + h + " on " + sel.Count + " selected letter(s)");
    }

    private void Paint()
    {
        List<int> sel = Sel();
        if (sel.Count == 0)
        {
            Status(_host.T("stevethetroll_sign_select_first"));
            return;
        }

        foreach (int i in sel)
        {
            Style.Letters[i] = _letterColour;
        }

        RebuildLetters();
        Changed("painted " + sel.Count + " letter(s) " + _letterColour);
    }

    private void Rainbow()
    {
        List<int> sel = Sel();
        if (sel.Count == 0)
        {
            Status(_host.T("stevethetroll_sign_select_first"));
            return;
        }

        for (int k = 0; k < sel.Count; k++)
        {
            Style.Letters[sel[k]] =
                "#" + ColorUtility.ToHtmlStringRGB(
                    Color.HSVToRGB(sel.Count == 1 ? 0f : k / (float)sel.Count, 0.85f, 1f));
        }

        RebuildLetters();
        Changed("rainbow over " + sel.Count + " letter(s)");
    }

    /// From the text colour to the letter colour over the selected letters (first to last selected).
    private void Gradient()
    {
        List<int> sel = Sel();
        if (sel.Count == 0)
        {
            Status(_host.T("stevethetroll_sign_select_first"));
            return;
        }

        int n = Style.Gradient(sel[0], sel[sel.Count - 1], Style.Color, _letterColour);
        RebuildLetters();
        Changed("gradient over " + n + " letter(s) " + Style.Color + " -> " + _letterColour);
    }

    private void ClearLetters()
    {
        List<int> sel = Sel();
        if (sel.Count == 0)
        {
            int all = Style.Letters.Count;
            Style.Letters.Clear();
            RebuildLetters();
            Changed("cleared all " + all + " letter colour(s)");
            return;
        }

        foreach (int i in sel)
        {
            Style.Letters.Remove(i);
        }

        RebuildLetters();
        Changed("cleared " + sel.Count + " letter colour(s)");
    }

    private void AddEmoji(string em)
    {
        if (SignStyle.SplitLetters(Name).Count >= _host.MaxLetters)
        {
            Status(_host.T("stevethetroll_sign_name_full"));
            return;
        }

        Name += em;
        Build(); // the name field shows the new text
        Changed("emoji added");
    }

    // --- preferred / default style -------------------------------------------------------------------------------------------
    private void UsePreferred()
    {
        SignStyle p = _host.LoadPreferred();
        if (p == null)
        {
            Status(_host.T("stevethetroll_sign_pref_none"));
            return;
        }

        Style = p;
        Build();
        Changed("preferred style used");
        Status(_host.T("stevethetroll_sign_pref_used"));
    }

    /// T-56R (user: "a 3rd button: Reset to default style"): every style value back to the default; the name stays.
    private void ResetStyle()
    {
        Style = new SignStyle();
        Build();
        Changed("reset to the default style");
        Status(_host.T("stevethetroll_sign_reset_done"));
    }
}
