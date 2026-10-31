using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// D-160 (phase 5): checks the SKINNED costume meshes of the built prefabs without starting Valheim: not empty, valid bone indices, normalised weights, and for the trousers
/// no vertex that follows both legs and no triangle that joins a left-leg vertex to a right-leg vertex (the "fabric between the legs" of the 2026-10-31 test).
/// Menu: SteveTheTroll > Costume > Validate costume meshes. Errors go to the console (LogError), the summary to Log.
/// </summary>
public static class CostumeValidate
{
    const float MeshUnitsToMetres = 100f, MiddleStrip = 0.025f;      // the skinned meshes are in 1 cm units (FBX); 2.5 cm each side of the middle

    [MenuItem("SteveTheTroll/Costume/Validate costume meshes")]
    public static void Run()
    {
        var report = new StringBuilder("CostumeValidate\n");
        int errors = 0;
        foreach (var prefabName in new[] { "CostumeLegsParts", "CostumeChestParts", "CostumeHelmetParts" })
        {
            var guids = AssetDatabase.FindAssets(prefabName + " t:Prefab");
            if (guids.Length == 0) { report.AppendLine("  " + prefabName + ": prefab not found (skipped)"); continue; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                errors += Check(prefabName, smr, prefabName == "CostumeLegsParts", report);
        }
        report.AppendLine(errors == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + errors + " problem(s))");
        try { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/costume_validate.txt"), report.ToString()); } catch (System.Exception) { }
        if (errors == 0) Debug.Log(report.ToString()); else Debug.LogError(report.ToString());
    }

    static int Side(string bone)
    {
        bool leg = bone.Contains("UpLeg") || bone.Contains("Leg") || bone.Contains("Foot") || bone.Contains("Toe");
        if (!leg) return 0;
        if (bone.StartsWith("Left") || bone.Contains("_L") || bone.EndsWith("Left")) return -1;
        if (bone.StartsWith("Right") || bone.Contains("_R") || bone.EndsWith("Right")) return 1;
        return 0;
    }

    static int Check(string prefab, SkinnedMeshRenderer smr, bool legs, StringBuilder rep)
    {
        int errors = 0;
        var m = smr.sharedMesh;
        string id = prefab + "/" + smr.name;
        if (m == null || m.vertexCount == 0) { rep.AppendLine("  " + id + ": EMPTY MESH"); return 1; }
        var w = m.boneWeights; var bones = smr.bones; int bind = m.bindposes.Length;
        if (w.Length != m.vertexCount) { rep.AppendLine("  " + id + ": " + w.Length + " weights for " + m.vertexCount + " vertices"); return 1; }
        // the belt / loincloth (straw material) hang from the Hips on purpose; they are not part of the leg check
        var straw = new bool[m.vertexCount];
        var mats = smr.sharedMaterials;
        for (int sm = 0; sm < m.subMeshCount && sm < mats.Length; sm++)
            if (mats[sm] != null && mats[sm].name.ToLower().Contains("straw"))
                foreach (var vi in m.GetIndices(sm)) straw[vi] = true;
        int badIndex = 0, notNorm = 0, both = 0; var bothBounds = new Bounds(); bool anyBoth = false;
        var side = new int[m.vertexCount];
        for (int i = 0; i < w.Length; i++)
        {
            float sum = w[i].weight0 + w[i].weight1 + w[i].weight2 + w[i].weight3;
            if (Mathf.Abs(sum - 1f) > 0.02f) notNorm++;
            int[] idx = { w[i].boneIndex0, w[i].boneIndex1, w[i].boneIndex2, w[i].boneIndex3 };
            float[] ww = { w[i].weight0, w[i].weight1, w[i].weight2, w[i].weight3 };
            float left = 0f, right = 0f;
            for (int k = 0; k < 4; k++)
            {
                if (ww[k] <= 0f) continue;
                if (idx[k] < 0 || idx[k] >= bind || idx[k] >= bones.Length || bones[idx[k]] == null) { badIndex++; continue; }
                int s = Side(bones[idx[k]].name);
                if (s < 0) left += ww[k]; else if (s > 0) right += ww[k];
            }
            if (straw[i]) { side[i] = 0; continue; }
            if (left > 0.05f && right > 0.05f)
            {
                both++; if (!anyBoth) { bothBounds = new Bounds(m.vertices[i], Vector3.zero); anyBoth = true; } else bothBounds.Encapsulate(m.vertices[i]);
                if (both <= 4 && legs)
                {
                    var d = new StringBuilder();
                    for (int k = 0; k < 4; k++) if (ww[k] > 0f) d.Append(bones[idx[k]].name).Append(' ').Append(ww[k].ToString("F2")).Append(", ");
                    rep.AppendLine("    both-legs sample v" + i + " x " + m.vertices[i].x.ToString("F3") + " y " + m.vertices[i].y.ToString("F3") + " z " + m.vertices[i].z.ToString("F3") + ": " + d);
                }
            }
            side[i] = left > right && left > 0.5f ? -1 : right > left && right > 0.5f ? 1 : 0;
        }
        int bridges = 0; var bridgeBox = new Bounds(); float bridgeEdge = 0f;
        var tri = m.triangles;
        for (int t = 0; t < tri.Length; t += 3)
        {
            int a = side[tri[t]], b = side[tri[t + 1]], c = side[tri[t + 2]];
            // a triangle only "joins the legs" when it really spans the middle: the two cut caps sit 2 cm apart at x = 0 and the left / right call of a vertex INSIDE that strip is not reliable
            float x0 = m.vertices[tri[t]].x * MeshUnitsToMetres, x1 = m.vertices[tri[t + 1]].x * MeshUnitsToMetres, x2 = m.vertices[tri[t + 2]].x * MeshUnitsToMetres;
            bool spans = Mathf.Min(x0, Mathf.Min(x1, x2)) < -MiddleStrip && Mathf.Max(x0, Mathf.Max(x1, x2)) > MiddleStrip;
            if (spans && (a < 0 || b < 0 || c < 0) && (a > 0 || b > 0 || c > 0))
            {
                bridges++;
                if (bridges <= 3)
                {
                    var tb = new StringBuilder();
                    for (int q = 0; q < 3; q++) { var ww2 = w[tri[t + q]]; tb.Append("v").Append(tri[t + q]).Append(" ").Append(bones[ww2.boneIndex0].name).Append(" ").Append(ww2.weight0.ToString("F2")).Append("/").Append(bones[ww2.boneIndex1].name).Append(" ").Append(ww2.weight1.ToString("F2")).Append(" x ").Append((m.vertices[tri[t + q]].x * 1000f).ToString("F3")).Append("; "); }
                    rep.AppendLine("    bridge sample: " + tb);
                }
                var vs = m.vertices;
                var cen = (vs[tri[t]] + vs[tri[t + 1]] + vs[tri[t + 2]]) / 3f;
                if (bridges == 1) bridgeBox = new Bounds(cen, Vector3.zero); else bridgeBox.Encapsulate(cen);
                float e = Mathf.Max((vs[tri[t]] - vs[tri[t + 1]]).magnitude, (vs[tri[t + 1]] - vs[tri[t + 2]]).magnitude);
                if (e > bridgeEdge) bridgeEdge = e;
            }
        }
        rep.AppendLine("  " + id + ": " + m.vertexCount + " verts, " + m.triangles.Length / 3 + " triangles, " + bones.Length + " bones; bad bone index " + badIndex + ", weights not normalised " + notNorm
            + (legs ? ", mesh bounds " + m.bounds.min.ToString("F2") + ".." + m.bounds.max.ToString("F2") + ", both-legs vertices span " + bothBounds.min.ToString("F2") + ".." + bothBounds.max.ToString("F2") + ", vertices following both legs " + both + ", triangles joining the legs " + bridges + (bridges > 0 ? " (centroids " + bridgeBox.min.ToString("F4") + ".." + bridgeBox.max.ToString("F4") + ", longest edge " + bridgeEdge.ToString("F4") + ")" : "") : ""));
        if (badIndex > 0 || notNorm > 0) errors++;
        if (legs && (both > 0 || bridges > 0)) errors++;
        return errors;
    }
}
