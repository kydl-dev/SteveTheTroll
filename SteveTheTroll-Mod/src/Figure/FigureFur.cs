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
            int want = ModSettings.FurEnable.Value ? ModSettings.FurShells.Value : 0;
            if (want != _builtCount) Rebuild(want);
            var tint = new Color(ModSettings.FurTintR.Value, ModSettings.FurTintG.Value, ModSettings.FurTintB.Value, 1f);
            float t = ModSettings.FurTipLight.Value;
            TrollFur.Apply(_shells, ModSettings.FurLength.Value, ModSettings.FurDensity.Value, ModSettings.FurThickness.Value, ModSettings.FurDroop.Value, tint, new Color(t, t, t, 1f));
            ModLog.Check("FigureFur: " + _owner + " shells", _shells.Count, want, 0, "");
            return _shells.Count;
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
            return n - _shells.Count;
        }
    }
}
