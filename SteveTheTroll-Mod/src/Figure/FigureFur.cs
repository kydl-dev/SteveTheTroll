using System.Collections.Generic;
using Kydl.Common;
using SteveTheTroll.Fit;
using UnityEngine;

namespace SteveTheTroll.Figure
{
    /// <summary>
    /// D-020: the fluffy fur of one mini troll. Loads the bundle material "TrollFur" (shader SteveTheTroll/ShellFur) once, puts the
    /// shells on through the shared TrollFur (same code as the Unity preview) and pushes the TrollFur Tunables into them. Rebuilds the
    /// shells when the shell count or Enable changes.
    /// </summary>
    internal sealed class FigureFur
    {
        const string MaterialName = "TrollFur";
        static Material _template;
        static bool _templateLooked;

        readonly SkinnedMeshRenderer _body;
        readonly string _owner;
        List<SkinnedMeshRenderer> _shells = new List<SkinnedMeshRenderer>();
        int _builtCount = -1;
        Material _original, _plush;                       // D-027: the troll's own body material / our plush copy

        public FigureFur(SkinnedMeshRenderer body, string owner) { _body = body; _owner = owner; }

        static Material Template()
        {
            if (_templateLooked) return _template;
            _templateLooked = true;
            _template = Plugin.Bundle != null ? Plugin.Bundle.LoadAsset<Material>(MaterialName) : null;
            if (_template == null) ModLog.Warn("FigureFur: material '" + MaterialName + "' not in the bundle (" + (Plugin.Bundle != null ? "bundle loaded" : "no bundle") + ") - trolls stay without fur");
            else ModLog.Verbose("FigureFur: material '" + MaterialName + "' loaded, shader " + _template.shader.name + ", supported " + _template.shader.isSupported);
            return _template;
        }

        /// Called when a Tunable changed (and once at start). Expected outcome: shell count = FurShells when enabled, 0 when not.
        public int Refresh()
        {
            RefreshPlush();
            int want = ModSettings.FurEnable.Value ? ModSettings.FurShells.Value : 0;
            if (want != _builtCount) Rebuild(want);
            var tint = ModSettings.Rgb(ModSettings.FurColorR, ModSettings.FurColorG, ModSettings.FurColorB, ModSettings.FurBrightness);
            float t = ModSettings.FurTipLight.Value;
            TrollFur.Apply(_shells, ModSettings.FurLength.Value, ModSettings.FurDensity.Value, ModSettings.FurThickness.Value, ModSettings.FurDroop.Value, tint, new Color(t, t, t, 1f));
            ModLog.Check("FigureFur: " + _owner + " shells", _shells.Count, want, 0, "");
            return _shells.Count;
        }

        // D-027 smooth plush: swap the body material for our plush copy (and back when switched off), colour live.
        void RefreshPlush()
        {
            if (_body == null) return;
            if (_original == null) _original = _body.sharedMaterial;
            var tint = ModSettings.Rgb(ModSettings.PlushColorR, ModSettings.PlushColorG, ModSettings.PlushColorB, ModSettings.PlushBrightness);
            if (ModSettings.PlushEnable.Value)
            {
                if (_plush == null && Template() != null)
                {
                    _body.sharedMaterial = _original;                 // the plush takes the albedo from the troll's own material
                    _plush = TrollFur.MakePlush(_body, Template(), tint, ModSettings.PlushSmoothness.Value);
                }
                if (_plush != null) { _body.sharedMaterial = _plush; TrollFur.SetPlush(_plush, tint, ModSettings.PlushSmoothness.Value); }
            }
            else if (_body.sharedMaterial != _original) _body.sharedMaterial = _original;
            ModLog.Verbose("FigureFur: " + _owner + " plush " + (ModSettings.PlushEnable.Value ? "on, tint " + tint.ToString("F2") + ", smoothness " + ModSettings.PlushSmoothness.Value.ToString("F2") : "off") + ", body material " + (_body.sharedMaterial != null ? _body.sharedMaterial.name : "none"));
            ModLog.Check("FigureFur: " + _owner + " plush material in use", _body.sharedMaterial == _plush && _plush != null ? 1 : 0, ModSettings.PlushEnable.Value && Template() != null ? 1 : 0, 0, "");
        }

        void Rebuild(int count)
        {
            foreach (var s in _shells) if (s != null) { Object.Destroy(s.sharedMaterial); Object.Destroy(s.gameObject); }
            _shells = new List<SkinnedMeshRenderer>();
            _builtCount = count;
            var tpl = count > 0 ? Template() : null;
            if (tpl != null) _shells = TrollFur.AddShells(_body, tpl, count);
            ModLog.Verbose("FigureFur: " + _owner + " rebuilt with " + _shells.Count + " shell(s) (wanted " + count + "), body material " + (_body != null && _body.sharedMaterial != null ? _body.sharedMaterial.name + " / " + _body.sharedMaterial.shader.name + ", _MainTex " + _body.sharedMaterial.HasProperty("_MainTex") : "none"));
        }

        /// The figure is destroyed: the shells' own materials go with it. Expected outcome: no shell left.
        public int Dispose()
        {
            int n = _shells.Count;
            Rebuild(0);
            if (_plush != null) { if (_body != null) _body.sharedMaterial = _original; Object.Destroy(_plush); _plush = null; }
            return n - _shells.Count;
        }
    }
}
