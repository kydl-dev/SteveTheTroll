using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SteveTheTroll.EditorTools
{
    /// <summary>
    /// T-009c (Claude-Agent-2, 2026-10-28): turns the Blender export (Assets/SteveTheTroll_Costume/Costume.fbx + Club.fbx, made by
    /// SteveTheTroll-Blender/scripts/costume_export.py) into game-ready parts the mod grafts onto its items (T-009d), the vanilla way
    /// (STATE "Research": VisEquipment.AttachArmor / AttachItem):
    ///  - CostumeChestParts / CostumeLegsParts: child "attach_skin" = a copy of the player's armature (bone transforms only) + skinned
    ///    meshes whose bone list / bindposes are the player body's, weights taken from the reference body (nearest body vertices, per
    ///    piece only the bones that piece may follow). The game swaps the bones for the wearer's at equip time.
    ///    CostumeLegsParts has a second skinned mesh "CostumeFeet" (plush feet) that the mod hides while the slippers are worn.
    ///  - CostumeHelmetParts: child "attach" (rigid), placed in the player's Helmet_attach space.
    ///  - ClubParts: child "attach" -> "model": the club, grip at the origin, trunk along +Z (as the vanilla sledges' attach space).
    /// Materials: one per Blender slot name, on our ShellFur shader (shell 0 = plain plush colour), Blender's linear colours.
    /// The reference body (Assets/RipReference, git-ignored) is only READ here; no rip mesh / texture goes into the outputs (D-014).
    /// </summary>
    public static class CostumeBuild
    {
        const string Root = "Assets/SteveTheTroll_Costume";
        const string Gen = Root + "/Generated";
        const string MatDir = Root + "/Materials";
        const string PlayerPrefab = "Assets/RipReference/GameObject/Player.prefab";
        const string FurShader = "Assets/SteveTheTroll_Fur/ShellFur.shader";
        const string Bundle = "stevethetroll";
        const int K = 6;

        // Blender material -> (linear colour, smoothness)
        static readonly Dictionary<string, (Color c, float gloss)> Colours = new Dictionary<string, (Color, float)>
        {
            { "plush_blue", (new Color(0.30f, 0.46f, 0.56f), 0.05f) },
            { "plush_blue_dark", (new Color(0.17f, 0.27f, 0.36f), 0.05f) },
            { "plush_blue_light", (new Color(0.48f, 0.62f, 0.68f), 0.05f) },
            { "straw_yellow", (new Color(0.80f, 0.66f, 0.28f), 0.05f) },
            { "eye_orange", (new Color(1.00f, 0.35f, 0.02f), 0.6f) },
            { "pupil_black", (new Color(0.01f, 0.01f, 0.01f), 0.6f) },
            { "fang_white", (new Color(0.92f, 0.90f, 0.82f), 0.4f) },
            { "club_bark", (new Color(0.08f, 0.06f, 0.05f), 0.05f) },
            { "club_bark_light", (new Color(0.14f, 0.10f, 0.07f), 0.05f) },
            { "club_cut", (new Color(0.70f, 0.40f, 0.15f), 0.1f) },
            { "club_cut_ring", (new Color(0.45f, 0.24f, 0.08f), 0.1f) },
        };

        static readonly string[] Legs = { "Hips", "Spine", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot" };
        static readonly string[] Feet = { "LeftLeg", "LeftFoot", "LeftToeBase", "RightLeg", "RightFoot", "RightToeBase" };
        static readonly string[] Belt = { "Hips", "Spine" };
        static readonly string[] Collar = { "Spine1", "Spine2", "Neck" };
        static bool ChestBone(string n) => !n.Contains("UpLeg") && !n.EndsWith("Leg") && !n.Contains("Foot") && !n.Contains("Toe") && !n.StartsWith("Head") && n != "Jaw";

        // the FBX importer brings Blender's +Y forward / +X right in as Unity -Z / -X (a half turn about Y, measured on the club's stubs and
        // the feet's toes): turn it back so +Z is the player's forward and +X their right, as the reference player
        static readonly Matrix4x4 Fix = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));

        static StringBuilder _log;
        static void Log(string s) { _log.AppendLine(s); }

        [MenuItem("SteveTheTroll/Costume/Build costume parts")]
        public static string Build()
        {
            _log = new StringBuilder("CostumeBuild\n");
            var playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (playerAsset == null) return Done("FAIL: " + PlayerPrefab + " missing (run _agent/tools/copy-rip-reference.ps1)");
            foreach (var f in new[] { Gen, MatDir }) if (!AssetDatabase.IsValidFolder(f)) AssetDatabase.CreateFolder(Root, f.Substring(Root.Length + 1));
            Readable(Root + "/Costume.fbx"); Readable(Root + "/Club.fbx");

            var player = (GameObject)Object.Instantiate(playerAsset);
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                var body = player.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.sharedMesh != null && r.sharedMesh.name == "body");
                var visual = body.transform.parent;
                var armature = body.rootBone; while (armature.parent != visual) armature = armature.parent;
                Log("body " + body.sharedMesh.name + " " + body.sharedMesh.vertexCount + " verts, " + body.bones.Length + " bones, armature '" + armature.name + "' under '" + visual.name + "'");
                CheckRest(body);

                var costume = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Costume.fbx");
                var club = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Club.fbx");
                if (costume == null || club == null) return Done("FAIL: Costume.fbx / Club.fbx not imported under " + Root);

                // --- body source points (player root space = the Blender export's space) with their weights / strongest bone
                var bm = body.sharedMesh; var bv = bm.vertices; var bw = bm.boneWeights; var bones = body.bones;
                var src = new Vector3[bv.Length]; var strong = new string[bv.Length];
                for (int i = 0; i < bv.Length; i++) { src[i] = body.transform.TransformPoint(bv[i]); strong[i] = bones[bw[i].boneIndex0].name; }

                var chest = Skinned(costume, "CostumeChest", body, src, bw, strong, mat => mat == "straw_yellow" ? (System.Func<string, bool>)(n => Collar.Contains(n)) : ChestBone);
                var legs = Skinned(costume, "CostumeLegs", body, src, bw, strong, mat => mat == "straw_yellow" ? (System.Func<string, bool>)(n => Belt.Contains(n)) : n => Legs.Contains(n));
                var feet = Skinned(costume, "CostumeFeet", body, src, bw, strong, mat => n => Feet.Contains(n));
                SkinPrefab("CostumeChestParts", visual, armature, body, new[] { chest });
                SkinPrefab("CostumeLegsParts", visual, armature, body, new[] { legs, feet });

                var helmetJoint = Find(player.transform, "Helmet_attach");
                RigidPrefab("CostumeHelmetParts", costume, "CostumeHelmet", helmetJoint.worldToLocalMatrix, Quaternion.identity);
                // club: Blender +Z (trunk) arrives as Unity +Y; the vanilla sledges' attach space has the shaft along +Z -> model rotated +90 deg about X
                RigidPrefab("ClubParts", club, "Club", Matrix4x4.identity, Quaternion.Euler(90f, 0f, 0f));
                // D-080 ground looks (the item lying on the ground): suit / trousers + feet laid on their back, half size
                GroundPrefab("CostumeChestGround", costume, new[] { "CostumeChest" }, 0.5f, true);
                GroundPrefab("CostumeLegsGround", costume, new[] { "CostumeLegs", "CostumeFeet" }, 0.5f, true);
                // D-080 the F6 Costume tab's 3D model: the whole mascot standing (static)
                GroundPrefab("CostumeViewer", costume, new[] { "CostumeHelmet", "CostumeChest", "CostumeLegs", "CostumeFeet" }, 1f, false);
            }
            finally { Object.DestroyImmediate(player); }
            AssetDatabase.SaveAssets();
            return Done("OK");
        }

        static void Readable(string path)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            bool dirty = !imp.isReadable || imp.importAnimation || imp.animationType != ModelImporterAnimationType.None || imp.meshCompression != ModelImporterMeshCompression.Off;
            imp.isReadable = true; imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None; imp.meshCompression = ModelImporterMeshCompression.Off;
            if (dirty) imp.SaveAndReimport();
        }

        // rest pose == bind pose? (the Blender model was built around the rest pose; the skin uses the bind pose)
        static void CheckRest(SkinnedMeshRenderer body)
        {
            float worst = 0f; string at = "-";
            var bp = body.sharedMesh.bindposes;
            for (int i = 0; i < body.bones.Length; i++)
            {
                var m = body.bones[i].localToWorldMatrix * bp[i];
                var p = m.MultiplyPoint3x4(Vector3.one * 0.005f) - body.transform.localToWorldMatrix.MultiplyPoint3x4(Vector3.one * 0.005f);
                if (p.magnitude > worst) { worst = p.magnitude; at = body.bones[i].name; }
            }
            Log("rest vs bind pose: worst " + (worst * 100f).ToString("F2") + " cm at " + at + (worst < 0.01f ? " (OK)" : " (WARNING: the rest pose is not the bind pose)"));
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = Find(c, name); if (r != null) return r; }
            return null;
        }

        static (MeshFilter mf, Material[] mats) Part(GameObject fbx, string name)
        {
            var t = Find(fbx.transform, name);
            var mf = t != null ? t.GetComponent<MeshFilter>() : null;
            if (mf == null) throw new System.Exception("part '" + name + "' not in " + fbx.name);
            return (mf, t.GetComponent<MeshRenderer>().sharedMaterials);
        }

        static Material Mat(string name)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(AssetDatabase.LoadAssetAtPath<Shader>(FurShader)); AssetDatabase.CreateAsset(m, path); }
            (Color c, float g) v;
            if (!Colours.TryGetValue(name, out v)) { v = (new Color(1f, 0f, 1f), 0f); Log("WARNING: no colour for material '" + name + "' (magenta)"); }
            m.SetColor("_Color", v.c.gamma); m.SetColor("_TipColor", Color.white);
            m.SetFloat("_Shell", 0f); m.SetFloat("_Occlusion", 0f); m.SetFloat("_FurLength", 0f); m.SetFloat("_Glossiness", v.g);
            EditorUtility.SetDirty(m);
            Tag(m);
            return m;
        }

        static void Tag(Object o) { var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(o)); if (imp != null && imp.assetBundleName != Bundle) { imp.assetBundleName = Bundle; } }

        static Mesh Save(Mesh m, string name)
        {
            string path = Gen + "/" + name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); m = old; }
            else AssetDatabase.CreateAsset(m, path);
            Tag(m);
            return m;
        }

        sealed class SkinnedPart { public string Name; public Mesh Mesh; public Material[] Mats; }

        /// The FBX part in the body's mesh space, with weights from the nearest body vertices whose strongest bone the part may follow
        /// (allowed(material) -> bone-name filter, so the belt / collar stay on the hips / neck and a leg never follows the other leg or a hand).
        static SkinnedPart Skinned(GameObject fbx, string name, SkinnedMeshRenderer body, Vector3[] src, BoneWeight[] bw, string[] strong,
                                   System.Func<string, System.Func<string, bool>> allowed)
        {
            var (mf, mats) = Part(fbx, name);
            var m = mf.sharedMesh; var toRoot = Fix * mf.transform.localToWorldMatrix;            // FBX asset root sits at the origin = player root
            var toBody = body.transform.worldToLocalMatrix;                                   // player root == world here
            var v = m.vertices; var n = m.normals;
            var outV = new Vector3[v.Length]; var outN = new Vector3[v.Length]; var outW = new BoneWeight[v.Length];
            var vmat = new string[v.Length];
            for (int s = 0; s < m.subMeshCount; s++) foreach (var i in m.GetTriangles(s)) vmat[i] = mats[s].name;
            var filterCache = new Dictionary<string, int[]>();
            Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
            float far = 0f; var used = new HashSet<string>();
            for (int i = 0; i < v.Length; i++)
            {
                var p = toRoot.MultiplyPoint3x4(v[i]);
                lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p);
                outV[i] = toBody.MultiplyPoint3x4(p);
                outN[i] = toBody.MultiplyVector(toRoot.MultiplyVector(n[i])).normalized;
                string key = vmat[i] ?? "";
                if (!filterCache.TryGetValue(key, out var cand))
                {
                    var f = allowed(key);
                    cand = Enumerable.Range(0, src.Length).Where(j => f(strong[j])).ToArray();
                    filterCache[key] = cand;
                    Log("  " + name + " / " + key + ": " + cand.Length + " body source vertices");
                }
                outW[i] = Blend(p, src, bw, cand, out float d0);
                far = Mathf.Max(far, d0);
                used.Add(body.bones[outW[i].boneIndex0].name);
            }
            var mesh = new Mesh { name = name, indexFormat = v.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.vertices = outV; mesh.normals = outN; mesh.subMeshCount = m.subMeshCount;
            for (int s = 0; s < m.subMeshCount; s++) mesh.SetTriangles(m.GetTriangles(s), s);
            mesh.boneWeights = outW; mesh.bindposes = body.sharedMesh.bindposes;
            mesh.RecalculateBounds();
            Log(name + ": " + v.Length + " verts, " + m.subMeshCount + " materials [" + string.Join(", ", mats.Select(x => x.name)) + "], root-space bounds "
                + lo.ToString("F2") + " .. " + hi.ToString("F2") + ", farthest source " + (far * 100f).ToString("F0") + " cm, main bones: " + string.Join(", ", used.OrderBy(x => x)));
            return new SkinnedPart { Name = name, Mesh = Save(mesh, name + "_Skinned"), Mats = mats.Select(x => Mat(x.name)).ToArray() };
        }

        static BoneWeight Blend(Vector3 p, Vector3[] src, BoneWeight[] bw, int[] cand, out float nearest)
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
            nearest = bestI[0] >= 0 ? Mathf.Sqrt(bestD[0]) : 0f;
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

        static void SkinPrefab(string prefabName, Transform visual, Transform armature, SkinnedMeshRenderer body, SkinnedPart[] parts)
        {
            var root = new GameObject(prefabName);
            var skin = new GameObject("attach_skin").transform; skin.SetParent(root.transform, false);
            var arm = CopyTree(armature, skin);
            var map = arm.GetComponentsInChildren<Transform>(true).GroupBy(x => x.name).ToDictionary(g => g.Key, g => g.First());
            var bones = body.bones.Select(b => map.TryGetValue(b.name, out var c) ? c : null).ToArray();
            int missing = bones.Count(b => b == null);
            foreach (var p in parts)
            {
                var go = new GameObject(p.Name); go.transform.SetParent(skin, false);
                go.transform.localPosition = body.transform.localPosition; go.transform.localRotation = body.transform.localRotation; go.transform.localScale = body.transform.localScale;
                var r = go.AddComponent<SkinnedMeshRenderer>();
                r.sharedMesh = p.Mesh; r.sharedMaterials = p.Mats; r.bones = bones; r.rootBone = map[body.rootBone.name];
                r.updateWhenOffscreen = false; r.localBounds = p.Mesh.bounds;
            }
            string path = Root + "/" + prefabName + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Tag(saved);
            Log(prefabName + ": attach_skin with armature '" + armature.name + "' (" + map.Count + " transforms, " + missing + " body bone(s) missing) + " + string.Join(", ", parts.Select(x => x.Name)) + " -> " + path);
        }

        /// D-080: a static model of FBX parts (ground look or the F6 viewer's mascot): centred on the origin, laid on its back (front up) or standing, scaled. Child "model_<part>" per part.
        static void GroundPrefab(string prefabName, GameObject fbx, string[] parts, float scale, bool layDown)
        {
            var srcs = parts.Select(n => Part(fbx, n)).ToArray();
            var b = new Bounds(); bool first = true;
            foreach (var (mf, _) in srcs)
            {
                var toRoot = Fix * mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) { var p = toRoot.MultiplyPoint3x4(v); if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p); }
            }
            var lay = Matrix4x4.Scale(Vector3.one * scale) * Matrix4x4.Rotate(Quaternion.Euler(layDown ? -90f : 0f, 0f, 0f)) * Matrix4x4.Translate(-b.center);
            var root = new GameObject(prefabName);
            int verts = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                var (mf, mats) = srcs[i];
                var m = Matrix4x4.Rotate(Quaternion.identity) * lay * Fix * mf.transform.localToWorldMatrix;
                var src = mf.sharedMesh; var v = src.vertices; var n = src.normals;
                var outV = new Vector3[v.Length]; var outN = new Vector3[v.Length];
                for (int k = 0; k < v.Length; k++) { outV[k] = m.MultiplyPoint3x4(v[k]); outN[k] = m.MultiplyVector(n[k]).normalized; }
                var mesh = new Mesh { name = parts[i] + (layDown ? "_Ground" : "_Viewer"), indexFormat = v.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                mesh.vertices = outV; mesh.normals = outN; mesh.subMeshCount = src.subMeshCount;
                for (int sm = 0; sm < src.subMeshCount; sm++) mesh.SetTriangles(src.GetTriangles(sm), sm);
                mesh.RecalculateBounds();
                mesh = Save(mesh, parts[i] + (layDown ? "_Ground" : "_Viewer"));
                var go = new GameObject("model_" + parts[i]); go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = mats.Select(x => Mat(x.name)).ToArray();
                verts += v.Length;
            }
            string path = Root + "/" + prefabName + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Tag(saved);
            Log(prefabName + ": " + parts.Length + " part(s), " + verts + " verts, " + (layDown ? "laid down" : "standing") + ", scale " + scale + ", size " + (b.size * scale).ToString("F2") + " m -> " + path);
        }

        static void RigidPrefab(string prefabName, GameObject fbx, string part, Matrix4x4 rootToLocal, Quaternion modelRot)
        {
            var (mf, mats) = Part(fbx, part);
            var src = mf.sharedMesh; var toRoot = Fix * mf.transform.localToWorldMatrix;
            var v = src.vertices; var n = src.normals;
            var outV = new Vector3[v.Length]; var outN = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) { outV[i] = rootToLocal.MultiplyPoint3x4(toRoot.MultiplyPoint3x4(v[i])); outN[i] = rootToLocal.MultiplyVector(toRoot.MultiplyVector(n[i])).normalized; }
            var mesh = new Mesh { name = part };
            mesh.vertices = outV; mesh.normals = outN; mesh.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++) mesh.SetTriangles(src.GetTriangles(s), s);
            mesh.RecalculateBounds();
            mesh = Save(mesh, part + "_Mesh");
            var root = new GameObject(prefabName);
            var attach = new GameObject("attach").transform; attach.SetParent(root.transform, false);
            var model = new GameObject("model").transform; model.SetParent(attach, false); model.localRotation = modelRot;
            model.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.gameObject.AddComponent<MeshRenderer>().sharedMaterials = mats.Select(x => Mat(x.name)).ToArray();
            string path = Root + "/" + prefabName + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Tag(saved);
            var b = mesh.bounds;
            Log(prefabName + ": " + v.Length + " verts, local bounds " + b.min.ToString("F2") + " .. " + b.max.ToString("F2") + ", model rot " + modelRot.eulerAngles.ToString("F0") + " -> " + path);
        }

        static string Done(string s) { _log.AppendLine(s); var t = _log.ToString(); Debug.Log(t); return t; }
    }
}
