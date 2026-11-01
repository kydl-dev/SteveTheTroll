using System.Collections.Generic;
using System.Linq;
using System.Text;
using SteveTheTroll.Fit;
using UnityEditor;
using UnityEngine;

namespace SteveTheTroll.EditorTools
{
    /// <summary>
    /// D-166 (user 2026-11-01): the slippers WITH the troll on them, made once here instead of at runtime. Inputs: the new Blender slipper (Assets/SteveTheTroll_Slipper/Slipper3.fbx,
    /// scripts/slipper_model3.py: one slipper in its own frame, toe +Z, symmetric), the player in its rest pose (RipReference Player.prefab), the game's own Troll and its log
    /// (RipReference, shipped inside our bundle now: the user overrode D-013). The mini troll is posed with the saved fit (TrollFitData, the same TrollFitMath the game used at
    /// runtime), sized, placed on each foot, BAKED (BakeMesh) and written, together with the slippers, into ONE skinned mesh with the player's bone list: prefab SlipperSkinParts,
    /// child "attach_skin" - exactly how the costume pieces and every vanilla armour piece are made (VisEquipment swaps in the wearer's bones). Slipper vertices take their
    /// weights from the player's own body (foot, toe and shin of that side), the troll and its log are rigid on the foot bone. No runtime figure, no glue, no timing.
    /// Menu: SteveTheTroll > Slipper > Build slipper skin parts.
    /// </summary>
    public static class SlipperBuild
    {
        const string Root = "Assets/SteveTheTroll_Slipper";
        const string Gen = Root + "/Generated";
        const string TexDir = Root + "/Textures";
        const string PlayerPrefab = "Assets/RipReference/GameObject/Player.prefab";
        const string TrollPrefab = "Assets/RipReference/GameObject/Troll.prefab";
        const string LogItemPrefab = "Assets/RipReference/GameObject/troll_log_swing_v.prefab";
        const string FurShader = "Assets/SteveTheTroll_Fur/ShellFur.shader";
        const string Bundle = "stevethetroll";
        const int K = 6;
        // the plush look the user settled on (ModSettings Plush*): colour 204 / 245 / 255 x brightness 1.25, smoothness 0.0727
        static readonly Color PlushTint = new Color(204f / 255f * 1.25f, 245f / 255f * 1.25f, 255f / 255f * 1.25f, 1f);
        const float PlushSmoothness = 0.0727f;
        static readonly Matrix4x4 Fix = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));

        static StringBuilder _log;
        static void Log(string s) { _log.AppendLine(s); }

        sealed class Geo
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<BoneWeight> W = new List<BoneWeight>();
            public readonly Dictionary<string, List<int>> Tris = new Dictionary<string, List<int>>();
            public readonly List<string> Order = new List<string>();

            /// The vertices of one source mesh, written ONCE (its submeshes share them); returns their offset.
            public int Verts(Vector3[] v, Vector3[] n, Vector2[] uv, BoneWeight[] w)
            {
                int o = V.Count;
                V.AddRange(v); N.AddRange(n); W.AddRange(w);
                UV.AddRange(uv != null && uv.Length == v.Length ? uv : new Vector2[v.Length]);
                return o;
            }

            public void TrisOf(string mat, int offset, IEnumerable<int[]> tris)
            {
                if (!Tris.TryGetValue(mat, out var list)) { list = new List<int>(); Tris[mat] = list; Order.Add(mat); }
                foreach (var t in tris) foreach (var i in t) list.Add(i + offset);
            }

            public void Add(string mat, Vector3[] v, Vector3[] n, Vector2[] uv, IEnumerable<int[]> tris, BoneWeight[] w) { TrisOf(mat, Verts(v, n, uv, w), tris); }
        }

        [MenuItem("SteveTheTroll/Slipper/Build slipper skin parts")]
        public static string Build()
        {
            _log = new StringBuilder("SlipperBuild\n");
            TrollFitMath.Log = s => { };
            TrollFitMath.Warn = s => Log("  fit warning: " + s);
            var playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var trollAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TrollPrefab);
            var logItem = AssetDatabase.LoadAssetAtPath<GameObject>(LogItemPrefab);
            var slipperFbx = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Slipper3.fbx");
            if (playerAsset == null || trollAsset == null || logItem == null) return Done("FAIL: rip reference missing (Player / Troll / troll_log_swing_v under Assets/RipReference)");
            if (slipperFbx == null) return Done("FAIL: " + Root + "/Slipper3.fbx not imported (run SteveTheTroll-Blender/scripts/slipper_model3.py, copy exports/Slipper3.fbx)");
            foreach (var f in new[] { Gen, TexDir }) if (!AssetDatabase.IsValidFolder(f)) AssetDatabase.CreateFolder(Root, f.Substring(Root.Length + 1));
            Readable(Root + "/Slipper3.fbx");

            var player = (GameObject)Object.Instantiate(playerAsset);
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var temp = new List<Object>();
            try
            {
                var body = player.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.sharedMesh != null && r.sharedMesh.name == "body");
                var visual = body.transform.parent;
                var armature = body.rootBone; while (armature.parent != visual) armature = armature.parent;
                var bm = body.sharedMesh; var bv = bm.vertices; var bw = bm.boneWeights; var bones = body.bones;
                var src = new Vector3[bv.Length]; var strong = new string[bv.Length];
                for (int i = 0; i < bv.Length; i++) { src[i] = body.transform.TransformPoint(bv[i]); strong[i] = bones[bw[i].boneIndex0].name; }
                var toBody = body.transform.worldToLocalMatrix;
                Log("player body " + bm.vertexCount + " verts, " + bones.Length + " bones");

                var albedo = CopyTexture("Assets/RipReference/Texture2D/troll_diffuse.png", "troll_diffuse.png");
                var logTex = CopyTexture("Assets/RipReference/Texture2D/Pine_tree_log_texture_0.png", "Pine_tree_log_texture_0.png");
                var mats = new Dictionary<string, Material>
                {
                    // the user's tint choices (Slipper Brightness 0.84, RimBrightness 0.88; TEST-005) baked into "Worn" copies of the slipper materials
                    { "SlipperPlush", Worn("SlipperPlush", 0.84f) }, { "SlipperLining", Worn("SlipperLining", 0.84f) }, { "SlipperRim", Worn("SlipperRim", 0.88f) }, { "SlipperSole", Existing("SlipperSole") },
                    { "TrollPlush", TrollMat("TrollPlush", albedo, PlushTint, PlushSmoothness) },
                    { "TrollHair", TrollMat("TrollHair", albedo, Color.white, 0.03f, 0.5f) },
                    { "TrollLog", TrollMat("TrollLog", logTex, Color.white, 0.03f) },
                };
                var geo = new Geo();
                var filterCache = new Dictionary<string, int[]>();

                foreach (bool left in new[] { true, false })
                {
                    string side = left ? "Left" : "Right";
                    var foot = Find(player.transform, side + "Foot"); var toe = Find(player.transform, side + "ToeBase");
                    if (foot == null || toe == null) return Done("FAIL: " + side + " foot / toe bone missing");
                    var fwd = TrollFitMath.FootForward(foot, toe, Vector3.forward);
                    int footIdx = System.Array.FindIndex(bones, b => b.name == side + "Foot");
                    Log(side + ": foot " + foot.position.ToString("F3") + ", forward " + fwd.ToString("F3") + ", foot bone " + footIdx);

                    // --- the slipper: placed with the fit numbers (slipper model = slipper frame, toe +Z), skinned from the player's body
                    var spos = FitGeometry.FootFramePosition(foot.position, fwd, left, TrollFitData.SlipperForward, TrollFitData.SlipperSide, TrollFitData.SlipperUp, 0f);
                    var place = Matrix4x4.TRS(spos, Quaternion.Euler(0f, FitGeometry.FigureYawDeg(fwd, TrollFitData.SlipperYaw), 0f), Vector3.one);
                    string key = side;
                    if (!filterCache.TryGetValue(key, out var cand))
                    {
                        var allowed = new HashSet<string> { side + "Leg", side + "Foot", side + "ToeBase" };
                        cand = Enumerable.Range(0, src.Length).Where(j => allowed.Contains(strong[j])).ToArray();
                        filterCache[key] = cand;
                        Log("  " + side + ": " + cand.Length + " body source vertices (shin, foot, toe)");
                    }
                    int slipperVerts = 0;
                    foreach (var mf in slipperFbx.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var mr = mf.GetComponent<MeshRenderer>();
                        var m = mf.sharedMesh;
                        var toWorld = place * Fix * mf.transform.localToWorldMatrix;
                        var v = m.vertices; var n = m.normals;
                        var wv = new Vector3[v.Length]; var wn = new Vector3[v.Length]; var ww = new BoneWeight[v.Length];
                        for (int i = 0; i < v.Length; i++)
                        {
                            var p = toWorld.MultiplyPoint3x4(v[i]);
                            wv[i] = toBody.MultiplyPoint3x4(p);
                            wn[i] = toBody.MultiplyVector(toWorld.MultiplyVector(n[i])).normalized;
                            ww[i] = Blend(p, src, bw, cand);
                        }
                        int off = geo.Verts(wv, wn, m.uv, ww);
                        for (int s = 0; s < m.subMeshCount; s++)
                        {
                            string matKey = BaseName(mr.sharedMaterials[Mathf.Min(s, mr.sharedMaterials.Length - 1)].name);
                            if (!mats.ContainsKey(matKey)) { Log("  WARNING: slipper material '" + matKey + "' unknown (SlipperPlush used)"); matKey = "SlipperPlush"; }
                            geo.TrisOf(matKey, off, new[] { m.GetTriangles(s) });
                        }
                        slipperVerts += v.Length;
                    }
                    Log("  " + side + " slipper: " + slipperVerts + " vertices written");

                    // --- the troll: posed with the fit, sized, placed on the foot (what TrollFigure did every frame), baked, rigid on the foot bone
                    var holder = new GameObject("TrollHolder_" + side); temp.Add(holder);
                    var visualSrc = trollAsset.transform.Find("Visual");
                    var copy = (GameObject)Object.Instantiate(visualSrc.gameObject, holder.transform, false);
                    copy.name = "TrollVisual"; copy.transform.localPosition = Vector3.zero; copy.transform.localRotation = Quaternion.identity;
                    Strip(copy);
                    var logAttach = logItem.transform.Find("attach");
                    var logGo = (GameObject)Object.Instantiate(logAttach.gameObject, holder.transform, false);
                    logGo.name = "TrollLog"; logGo.transform.localPosition = Vector3.zero; logGo.transform.localRotation = Quaternion.identity; Strip(logGo);
                    var tbody = TrollFitMath.FindBody(copy.transform);
                    int n2 = TrollFitMath.PosedBones.Length;
                    var tb = new Transform[n2]; var rest = new Quaternion[n2]; var delta = new Quaternion[n2];
                    var d = TrollFitData.Delta; int found = 0;
                    for (int i = 0; i < n2; i++)
                    {
                        delta[i] = d.Length >= (i + 1) * 4 ? new Quaternion(d[i * 4], d[i * 4 + 1], d[i * 4 + 2], d[i * 4 + 3]) : Quaternion.identity;
                        tb[i] = TrollFitMath.FindDeep(copy.transform, TrollFitMath.PosedBones[i]);
                        if (tb[i] == null) { Log("  WARNING: troll bone " + TrollFitMath.PosedBones[i] + " not found"); continue; }
                        rest[i] = tb[i].localRotation; found++;
                    }
                    TrollFitMath.ApplyPose(tb, rest, delta, null);
                    var hand = TrollFitMath.FindDeep(copy.transform, TrollFitMath.PosedBones.First(x => x == "RightHand"));
                    var scratch = new Mesh(); temp.Add(scratch);
                    float side0 = TrollFitData.Side;
                    Vector3 tpos; Quaternion trot;
                    TrollFitMath.Place(foot.position, fwd, left, TrollFitData.Forward, side0, TrollFitData.Up, TrollFitData.Yaw, 0f, out tpos, out trot);
                    holder.transform.SetPositionAndRotation(tpos, trot);
                    float min, max;
                    if (!TrollFitMath.MeasureY(tbody, scratch, out min, out max)) return Done("FAIL: the troll body baked no vertices");
                    float before = max - min;
                    holder.transform.localScale *= TrollFitData.Height / before;
                    TrollFitMath.MeasureY(tbody, scratch, out min, out max);
                    float bottomBelowPivot = holder.transform.position.y - min;
                    TrollFitMath.Place(foot.position, fwd, left, TrollFitData.Forward, side0, TrollFitData.Up, TrollFitData.Yaw, bottomBelowPivot, out tpos, out trot);
                    holder.transform.SetPositionAndRotation(tpos, trot);
                    var logMesh = logGo.GetComponentInChildren<MeshFilter>(true);
                    float logLen = TrollFitMath.PlaceLog(logGo.transform, logMesh, holder.transform, hand, TrollFitData.Height, TrollFitData.LogLength,
                        new Vector3(TrollFitData.LogOffset[0], TrollFitData.LogOffset[1], TrollFitData.LogOffset[2]),
                        new Vector3(TrollFitData.LogEuler[0], TrollFitData.LogEuler[1], TrollFitData.LogEuler[2]), Vector3.zero, Quaternion.identity);
                    TrollFitMath.MeasureY(tbody, scratch, out min, out max);
                    Log("  " + side + " troll: " + found + "/" + n2 + " bones posed, was " + before.ToString("F3") + " m tall -> " + (max - min).ToString("F3") + " m (want " + TrollFitData.Height.ToString("F3") + "), lowest vertex " + (min - foot.position.y).ToString("F3")
                        + " m from the foot bone (fit " + TrollFitData.Up.ToString("F3") + "), log " + logLen.ToString("F3") + " m");

                    var rigid = new BoneWeight { boneIndex0 = footIdx, weight0 = 1f };
                    int trollVerts = 0;
                    foreach (var smr in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (smr.sharedMesh == null) continue;
                        var baked = new Mesh(); temp.Add(baked);
                        smr.BakeMesh(baked);                                                     // world size, un-rotated / un-translated into the renderer's frame (TrollFitMath.MeasureY)
                        var tm = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                        var v = baked.vertices; var n = baked.normals;
                        var wv = new Vector3[v.Length]; var wn = new Vector3[v.Length]; var ww = new BoneWeight[v.Length];
                        for (int i = 0; i < v.Length; i++)
                        {
                            wv[i] = toBody.MultiplyPoint3x4(tm.MultiplyPoint3x4(v[i]));
                            wn[i] = toBody.MultiplyVector(tm.MultiplyVector(n[i])).normalized;
                            ww[i] = rigid;
                        }
                        string matKey = smr == tbody ? "TrollPlush" : "TrollHair";
                        var tris = Enumerable.Range(0, baked.subMeshCount).Select(s => baked.GetTriangles(s)).ToArray();
                        geo.Add(matKey, wv, wn, baked.uv, tris, ww);
                        trollVerts += v.Length;
                    }
                    if (logMesh != null && logMesh.sharedMesh != null)
                    {
                        var lm = logMesh.sharedMesh; var lw = logMesh.transform.localToWorldMatrix;
                        var v = lm.vertices; var n = lm.normals;
                        var wv = new Vector3[v.Length]; var wn = new Vector3[v.Length]; var ww = new BoneWeight[v.Length];
                        for (int i = 0; i < v.Length; i++)
                        {
                            wv[i] = toBody.MultiplyPoint3x4(lw.MultiplyPoint3x4(v[i]));
                            wn[i] = toBody.MultiplyVector(lw.MultiplyVector(n[i])).normalized;
                            ww[i] = rigid;
                        }
                        geo.Add("TrollLog", wv, wn, lm.uv, Enumerable.Range(0, lm.subMeshCount).Select(s => lm.GetTriangles(s)).ToArray(), ww);
                        trollVerts += v.Length;
                    }
                    Log("  " + side + " troll + log: " + trollVerts + " vertices written (rigid on " + side + "Foot)");
                    Object.DestroyImmediate(holder);
                }

                // --- one skinned mesh, one submesh per material
                var mesh = new Mesh { name = "SlipperSkin", indexFormat = geo.V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                mesh.vertices = geo.V.ToArray(); mesh.normals = geo.N.ToArray(); mesh.uv = geo.UV.ToArray();
                mesh.subMeshCount = geo.Order.Count;
                for (int s = 0; s < geo.Order.Count; s++) mesh.SetTriangles(geo.Tris[geo.Order[s]], s);
                mesh.boneWeights = geo.W.ToArray(); mesh.bindposes = bm.bindposes;
                mesh.RecalculateBounds();
                Log("SlipperSkin: " + geo.V.Count + " verts, submeshes " + string.Join(", ", geo.Order.Select((k, i) => k + "=" + geo.Tris[k].Count / 3 + " tris")));
                mesh = Save(mesh, "SlipperSkin");
                SkinPrefab("SlipperSkinParts", visual, armature, body, mesh, geo.Order.Select(k => mats[k]).ToArray());
            }
            finally
            {
                foreach (var o in temp) if (o != null) Object.DestroyImmediate(o);
                Object.DestroyImmediate(player);
            }
            AssetDatabase.SaveAssets();
            return Done("OK");
        }

        static string BaseName(string n) { int i = n.IndexOf(' '); if (i > 0) n = n.Substring(0, i); i = n.IndexOf('.'); return i > 0 ? n.Substring(0, i) : n; }

        static void Readable(string path)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            bool dirty = !imp.isReadable || imp.importAnimation || imp.animationType != ModelImporterAnimationType.None || imp.meshCompression != ModelImporterMeshCompression.Off;
            imp.isReadable = true; imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None; imp.meshCompression = ModelImporterMeshCompression.Off;
            if (dirty) imp.SaveAndReimport();
        }

        /// The rip's texture, copied into our project (shipped in our bundle, D-166), tagged for the bundle.
        static Texture2D CopyTexture(string from, string name)
        {
            string to = TexDir + "/" + name;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(to) == null)
            {
                if (!AssetDatabase.CopyAsset(from, to)) { Log("WARNING: could not copy " + from + " -> " + to); return null; }
                AssetDatabase.ImportAsset(to);
            }
            var imp = AssetImporter.GetAtPath(to);
            if (imp != null && imp.assetBundleName != Bundle) imp.assetBundleName = Bundle;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(to);
        }

        static Material Existing(string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Root + "/" + name + ".mat");
            if (m == null) throw new System.Exception("material " + name + " missing under " + Root);
            Tag(m);
            return m;
        }

        /// A copy of a slipper material with its colour scaled (the worn look; the ground look keeps tinting at runtime).
        static Material Worn(string name, float k)
        {
            var src = Existing(name);
            string path = Root + "/" + name + "Worn.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(src);
            var c = src.GetColor("_Color");
            m.SetColor("_Color", new Color(c.r * k, c.g * k, c.b * k, 1f));
            m.name = name + "Worn";
            EditorUtility.SetDirty(m);
            Tag(m);
            return m;
        }

        static Material TrollMat(string name, Texture tex, Color tint, float gloss, float cutoff = 0f)
        {
            string path = Root + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(AssetDatabase.LoadAssetAtPath<Shader>(FurShader)); AssetDatabase.CreateAsset(m, path); }
            m.shader = AssetDatabase.LoadAssetAtPath<Shader>(FurShader);
            m.SetTexture("_MainTex", tex); m.SetColor("_Color", tint); m.SetColor("_TipColor", Color.white);
            m.SetFloat("_Shell", 0f); m.SetFloat("_Occlusion", 0f); m.SetFloat("_FurLength", 0f); m.SetFloat("_Glossiness", gloss); m.SetFloat("_Cutoff", cutoff);
            EditorUtility.SetDirty(m);
            Tag(m);
            return m;
        }

        static void Tag(Object o) { var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(o)); if (imp != null && imp.assetBundleName != Bundle) imp.assetBundleName = Bundle; }

        static Mesh Save(Mesh m, string name)
        {
            string path = Gen + "/" + name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); m = old; }
            else AssetDatabase.CreateAsset(m, path);
            Tag(m);
            return m;
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = Find(c, name); if (r != null) return r; }
            return null;
        }

        /// Removes everything but transforms and renderers from a copy (no animator re-posing the bones, no LOD switching).
        static void Strip(GameObject root)
        {
            foreach (var type in new[] { typeof(MonoBehaviour), typeof(Animator), typeof(LODGroup), typeof(Collider), typeof(Rigidbody), typeof(ParticleSystem), typeof(AudioSource), typeof(Light) })
                foreach (var c in root.GetComponentsInChildren(type, true)) Object.DestroyImmediate(c);
        }

        static BoneWeight Blend(Vector3 p, Vector3[] src, BoneWeight[] bw, int[] cand)
        {
            var bestI = new int[K]; var bestD = new float[K];
            for (int k = 0; k < K; k++) { bestI[k] = -1; bestD[k] = float.MaxValue; }
            foreach (var j in cand)
            {
                float d = (src[j] - p).sqrMagnitude;
                if (d >= bestD[K - 1]) continue;
                int k2 = K - 1;
                while (k2 > 0 && bestD[k2 - 1] > d) { bestD[k2] = bestD[k2 - 1]; bestI[k2] = bestI[k2 - 1]; k2--; }
                bestD[k2] = d; bestI[k2] = j;
            }
            var acc = new Dictionary<int, float>();
            for (int k = 0; k < K; k++)
            {
                if (bestI[k] < 0) continue;
                float w = 1f / (Mathf.Sqrt(bestD[k]) + 0.02f);
                var b = bw[bestI[k]];
                Add(acc, b.boneIndex0, b.weight0 * w); Add(acc, b.boneIndex1, b.weight1 * w); Add(acc, b.boneIndex2, b.weight2 * w); Add(acc, b.boneIndex3, b.weight3 * w);
            }
            var top = acc.OrderByDescending(x => x.Value).Take(4).ToArray();
            float sum = top.Sum(x => x.Value);
            var r = new BoneWeight();
            if (top.Length > 0) { r.boneIndex0 = top[0].Key; r.weight0 = top[0].Value / sum; }
            if (top.Length > 1) { r.boneIndex1 = top[1].Key; r.weight1 = top[1].Value / sum; }
            if (top.Length > 2) { r.boneIndex2 = top[2].Key; r.weight2 = top[2].Value / sum; }
            if (top.Length > 3) { r.boneIndex3 = top[3].Key; r.weight3 = top[3].Value / sum; }
            return r;
        }
        static void Add(Dictionary<int, float> d, int i, float w) { if (w <= 0f) return; d.TryGetValue(i, out var o); d[i] = o + w; }

        static Transform CopyTree(Transform t, Transform parent)
        {
            var c = new GameObject(t.name).transform;
            c.SetParent(parent, false);
            c.localPosition = t.localPosition; c.localRotation = t.localRotation; c.localScale = t.localScale;
            foreach (Transform ch in t) CopyTree(ch, c);
            return c;
        }

        static void SkinPrefab(string prefabName, Transform visual, Transform armature, SkinnedMeshRenderer body, Mesh mesh, Material[] mats)
        {
            var root = new GameObject(prefabName);
            var skin = new GameObject("attach_skin").transform; skin.SetParent(root.transform, false);
            var arm = CopyTree(armature, skin);
            var map = arm.GetComponentsInChildren<Transform>(true).GroupBy(x => x.name).ToDictionary(g => g.Key, g => g.First());
            var bones = body.bones.Select(b => map.TryGetValue(b.name, out var c) ? c : null).ToArray();
            int missing = bones.Count(b => b == null);
            var go = new GameObject("SlipperSkin"); go.transform.SetParent(skin, false);
            go.transform.localPosition = body.transform.localPosition; go.transform.localRotation = body.transform.localRotation; go.transform.localScale = body.transform.localScale;
            var r = go.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = mesh; r.sharedMaterials = mats; r.bones = bones; r.rootBone = map[body.rootBone.name];
            r.updateWhenOffscreen = false; r.localBounds = mesh.bounds;
            string path = Root + "/" + prefabName + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Tag(saved);
            Log(prefabName + ": attach_skin with armature '" + armature.name + "' (" + map.Count + " transforms, " + missing + " body bone(s) missing) + SlipperSkin with " + mats.Length + " material(s) -> " + path);
        }

        static string Done(string s) { _log.AppendLine(s); var t = _log.ToString(); Debug.Log(t); try { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/slipper_build.txt"), t); } catch (System.Exception) { } return t; }
    }
}
