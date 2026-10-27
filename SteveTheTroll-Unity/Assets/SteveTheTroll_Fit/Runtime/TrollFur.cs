using System.Collections.Generic;
using UnityEngine;

namespace SteveTheTroll.Fit
{
    /// <summary>
    /// D-020 shell fur, shared by the Unity fit scene and the mod (compiled into both). Adds `shells` copies of the troll's body renderer
    /// (same mesh, same bones) with the SteveTheTroll/ShellFur material, shell i at _Shell = i / shells. The base body keeps the game's
    /// own material. Shells cast no shadows (cost). Values are pushed live with Apply (Tunables in game, rig fields in Unity).
    /// </summary>
    public static class TrollFur
    {
        public const string ShellPrefix = "FurShell_";

        /// Expected outcome: `shells` shell renderers exist next to the body (returns how many were made).
        public static List<SkinnedMeshRenderer> AddShells(SkinnedMeshRenderer body, Material template, int shells)
        {
            var list = new List<SkinnedMeshRenderer>();
            if (body == null || template == null || shells <= 0) { TrollFitMath.Warn("TrollFur: AddShells without " + (body == null ? "body" : template == null ? "fur material" : "shells")); return list; }
            var albedo = body.sharedMaterial != null && body.sharedMaterial.HasProperty("_MainTex") ? body.sharedMaterial.GetTexture("_MainTex") : null;
            for (int i = 1; i <= shells; i++)
            {
                var go = new GameObject(ShellPrefix + i);
                go.layer = body.gameObject.layer;
                go.transform.SetParent(body.transform.parent, false);
                go.transform.localPosition = body.transform.localPosition;
                go.transform.localRotation = body.transform.localRotation;
                go.transform.localScale = body.transform.localScale;
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = body.sharedMesh;
                smr.bones = body.bones;
                smr.rootBone = body.rootBone;
                smr.localBounds = body.localBounds;
                smr.updateWhenOffscreen = body.updateWhenOffscreen;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var m = new Material(template) { name = "TrollFur_" + i };
                if (albedo != null) m.SetTexture("_MainTex", albedo);
                m.SetFloat("_Shell", i / (float)shells);
                smr.sharedMaterial = m;
                list.Add(smr);
            }
            TrollFitMath.Log("TrollFur: " + list.Count + " shell(s) on " + body.name + " (" + body.sharedMesh.vertexCount + " verts each), albedo " + (albedo != null ? albedo.name : "none"));
            return list;
        }

        /// Pushes the look into every shell. Expected outcome: returns how many shell materials were updated.
        public static int Apply(List<SkinnedMeshRenderer> shells, float lengthM, float density, float thickness, float gravityM, Color tint, Color tip)
        {
            int n = 0;
            foreach (var s in shells)
            {
                if (s == null || s.sharedMaterial == null) continue;
                var m = s.sharedMaterial;
                m.SetFloat("_FurLength", lengthM);
                m.SetFloat("_Density", density);
                m.SetFloat("_Thickness", thickness);
                m.SetFloat("_Gravity", gravityM);
                m.SetColor("_Color", tint);
                m.SetColor("_TipColor", tip);
                n++;
            }
            TrollFitMath.Log("TrollFur: Apply length " + lengthM.ToString("F4") + " m, density " + density.ToString("F0") + ", thickness " + thickness.ToString("F2") + ", droop " + gravityM.ToString("F4") + " m on " + n + " shell(s)");
            return n;
        }
    }
}
