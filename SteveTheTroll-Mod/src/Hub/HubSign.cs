using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Ui;
using TMPro;
using UnityEngine;

namespace SteveTheTroll.Hub
{
    /// <summary>
    /// D-135 (user: "a new sign like the one in WaterAndPools, without the font size change"): the sign on a Troll Hub. Name and look (SignStyle) are
    /// stored in the HUB's ZDO (HubRegistry.NameKey / StyleKey), so every player sees the same. Font size is fixed (ModSettings.HubSignFontSize, 50),
    /// the name stays on one horizontal line (no wrapping) and the whole sign is scaled up so the line fits the board. Only the hub's owner edits it.
    /// </summary>
    internal class HubSign : MonoBehaviour, Hoverable, Interactable
    {
        internal const int MaxName = 20;
        internal static readonly List<HubSign> Loaded = new List<HubSign>();

        ZNetView _nview;
        TMP_Text _text;
        TMP_FontAsset _defFont;
        Vector3 _baseScale = Vector3.one;
        uint _lastRev = uint.MaxValue;
        float _drawnSize;
        string _previewName;
        SignStyle _previewStyle;

        internal string SignName { get; private set; } = "";
        internal SignStyle Style { get; private set; } = new SignStyle();

        void Awake()
        {
            _nview = GetComponentInParent<ZNetView>();
            _text = GetComponentInChildren<TMP_Text>(true);
            _baseScale = transform.localScale;
            if (_text != null) { _defFont = _text.font; _text.richText = true; }
            if (_nview == null || _nview.GetZDO() == null) return;           // the build ghost: no ZDO
            Loaded.Add(this);
            Refresh();
            InvokeRepeating(nameof(Refresh), 1f, 1f);
        }

        void OnDestroy() { Loaded.Remove(this); }

        long OwnerId { get { return _nview != null && _nview.GetZDO() != null ? _nview.GetZDO().GetLong(HubRegistry.OwnerKey, 0L) : 0L; } }
        internal int Number { get { return _nview != null && _nview.GetZDO() != null ? _nview.GetZDO().GetInt(HubRegistry.NumberKey, 0) : 0; } }
        internal bool Mine { get { var p = Player.m_localPlayer; return p != null && OwnerId == p.GetPlayerID(); } }

        void Refresh()
        {
            if (_nview == null || !_nview.IsValid()) return;
            var zdo = _nview.GetZDO();
            // F7 (Tunables): a new font size redraws the sign
            if (!Mathf.Approximately(_drawnSize, ModSettings.HubSignFontSize.Value)) { _drawnSize = ModSettings.HubSignFontSize.Value; _lastRev = uint.MaxValue; }
            if (zdo.DataRevision == _lastRev) return;
            _lastRev = zdo.DataRevision;
            SignName = HubRegistry.NameOf(zdo, Number);
            var problems = new List<string>();
            Style = SignStyle.Parse(zdo.GetString(HubRegistry.StyleKey, ""), problems);
            if (problems.Count > 0) ModLog.VerboseWarn("HubSign '" + SignName + "': style read with problems: " + string.Join("; ", problems));
            if (_previewStyle == null) Draw(SignName, Style);
        }

        void Draw(string name, SignStyle style)
        {
            if (_text == null) return;
            string how = SignFonts.Apply(_text, style, _defFont, ModSettings.HubSignFontSize.Value);
            _text.text = style.Render(name, SignFonts.EmojiFont);
            _text.ForceMeshUpdate();
            // the board grows with the name: the text keeps its size, one line
            float need = _text.preferredWidth, have = _text.rectTransform.rect.width;
            float k = have > 0.001f ? Mathf.Clamp(need / (have * 0.95f), 1f, 40f) : 1f;
            transform.localScale = _baseScale * k;
            ModLog.Verbose("HubSign: drawn '" + name + "' (" + how + "), line " + need.ToString("0.0") + " of " + have.ToString("0.0") + " -> sign x" + k.ToString("0.00"));
        }

        internal void Preview(string name, SignStyle style) { _previewName = name; _previewStyle = style.Clone(); Draw(_previewName, _previewStyle); }

        internal void EndPreview()
        {
            if (_previewStyle == null) return;
            _previewName = null; _previewStyle = null;
            Draw(SignName, Style);
        }

        /// Saves name + style into the hub (everyone sees it). Expected outcome: the stored name.
        internal string Save(string name, SignStyle style)
        {
            name = Clean(name);
            if (_nview == null || !_nview.IsValid()) { ModLog.Warn("HubSign: save refused, the hub has no network object"); return SignName; }
            _nview.ClaimOwnership();
            var zdo = _nview.GetZDO();
            zdo.Set(HubRegistry.NameKey, name);
            zdo.Set(HubRegistry.StyleKey, style.Serialize());
            _previewName = null; _previewStyle = null;
            _lastRev = uint.MaxValue;
            Refresh();
            ModLog.Info("HubSign: saved '" + name + "' for hub " + Number + " at " + transform.position.ToString("0.0"));
            return SignName;
        }

        internal static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char ch in name) if (!char.IsControl(ch)) sb.Append(ch);
            var letters = SignStyle.SplitLetters(sb.ToString().Trim());
            if (letters.Count > MaxName) letters.RemoveRange(MaxName, letters.Count - MaxName);
            return string.Concat(letters);
        }

        /// The loaded sign of hub (owner, number), or null (the hub is not near).
        internal static HubSign Of(long owner, int number)
        {
            foreach (var s in Loaded) if (s != null && s.OwnerId == owner && s.Number == number) return s;
            return null;
        }

        public string GetHoverName() { return Loc.T("stevethetroll_hub_piece"); }
        public float GetHoverOffset() { return 0f; }

        public string GetHoverText()
        {
            string text = "\"" + SignName + "\"";
            text += Mine ? "\n[<color=yellow><b>$KEY_Use</b></color>] " + Loc.T("stevethetroll_hub_hover_edit") : "";
            return Localization.instance != null ? Localization.instance.Localize(text) : text;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !ModSettings.On || !Mine) return false;
            return HubSignMenu.Open(this) == 1;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }
}
