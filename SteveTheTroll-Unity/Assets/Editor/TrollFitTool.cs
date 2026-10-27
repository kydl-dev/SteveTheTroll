using System.Globalization;
using System.IO;
using System.Text;
using SteveTheTroll.Fit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SteveTheTroll.EditorTools
{
    /// <summary>
    /// D-014 troll fit scene. "Build fit scene" puts the reference Player and two troll copies (Assets/RipReference, git-ignored, copied
    /// from the AssetRipper project) into Assets/RipReference/TrollFit.unity with a TrollFitRig; "Save fit" writes the rig's numbers into
    /// Assets/SteveTheTroll_Fit/Runtime/TrollFitData.cs (compiled into the mod). Reference meshes never leave this project.
    /// </summary>
    public static class TrollFitTool
    {
        const string RefRoot = "Assets/RipReference";
        const string ScenePath = RefRoot + "/TrollFit.unity";
        const string MatFolder = RefRoot + "/FitMaterials";
        const string DataPath = "Assets/SteveTheTroll_Fit/Runtime/TrollFitData.cs";

        [MenuItem("SteveTheTroll/Fit/Build fit scene")]
        public static string BuildScene()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RefRoot + "/GameObject/Player.prefab");
            var trollPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RefRoot + "/GameObject/Troll.prefab");
            var logPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RefRoot + "/GameObject/troll_log_swing_v.prefab");
            if (playerPrefab == null || trollPrefab == null || logPrefab == null) return Fail("reference prefabs missing under " + RefRoot + " (run copy-rip-reference.ps1)");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var player = Strip((GameObject)Object.Instantiate(playerPrefab));
            player.name = "RefPlayer";
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var leftFoot = TrollFitMath.FindDeep(player.transform, "LeftFoot");
            var rightFoot = TrollFitMath.FindDeep(player.transform, "RightFoot");

            var rigGo = new GameObject("TrollFitRig");
            var rig = rigGo.AddComponent<TrollFitRig>();
            rig.player = player.transform; rig.leftFoot = leftFoot; rig.rightFoot = rightFoot;
            rig.leftTroll = MakeTroll(trollPrefab, logPrefab, "TrollLeft");
            rig.rightTroll = MakeTroll(trollPrefab, logPrefab, "TrollRight");
            int n = TrollFitMath.PosedBones.Length;
            rig.rest = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                var b = TrollFitMath.FindDeep(rig.leftTroll, TrollFitMath.PosedBones[i]);
                rig.rest[i] = b != null ? b.localRotation : Quaternion.identity;
                if (b == null) Debug.LogWarning("TrollFitTool: troll bone missing " + TrollFitMath.PosedBones[i]);
            }
            LoadSavedFit(rig);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) FixMaterials(r);
            rig.Apply();
            EditorSceneManager.SaveScene(scene, ScenePath);
            string msg = "TrollFitTool: scene built, feet L " + (leftFoot != null ? leftFoot.position.ToString("F3") : "null") + " R " + (rightFoot != null ? rightFoot.position.ToString("F3") : "null") + ", " + rig.lastReport;
            Debug.Log(msg);
            return msg;
        }

        static Transform MakeTroll(GameObject trollPrefab, GameObject logPrefab, string name)
        {
            var visual = trollPrefab.transform.Find("Visual");
            var t = Strip((GameObject)Object.Instantiate(visual.gameObject));
            t.name = name;
            var hand = TrollFitMath.FindDeep(t.transform, "RightHand");
            var attach = logPrefab.transform.Find("attach");
            if (hand != null && attach != null)
            {
                var log = Strip((GameObject)Object.Instantiate(attach.gameObject, t.transform, false));   // figure root, not the hand (TrollFitMath.PlaceLog)
                log.name = "TrollLog";
                log.transform.localPosition = Vector3.zero;
                log.transform.localRotation = Quaternion.identity;
            }
            else Debug.LogWarning("TrollFitTool: no log (hand " + (hand != null) + ", attach " + (attach != null) + ")");
            return t.transform;
        }

        // Same idea as the mod's TrollFigureFactory.Strip: only transforms and renderers stay (missing game scripts included).
        static GameObject Strip(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            var kinds = new[] { typeof(MonoBehaviour), typeof(Animator), typeof(LODGroup), typeof(Collider), typeof(Rigidbody), typeof(ParticleSystem), typeof(AudioSource), typeof(Light) };
            foreach (var k in kinds) foreach (var c in go.GetComponentsInChildren(k, true)) Object.DestroyImmediate(c);
            return go;
        }

        // Ripped shaders are stubs (pink): show the albedo with the Standard shader instead (materials saved in the ignored folder).
        static void FixMaterials(Renderer r)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string path = MatFolder + "/" + mats[i].name.Replace("/", "_") + "_fit.mat";
                var fixedMat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (fixedMat == null)
                {
                    if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder(RefRoot, "FitMaterials");
                    fixedMat = new Material(Shader.Find("Standard"));
                    if (mats[i].HasProperty("_MainTex")) fixedMat.mainTexture = mats[i].GetTexture("_MainTex");
                    if (mats[i].HasProperty("_Color")) fixedMat.color = mats[i].GetColor("_Color");
                    fixedMat.SetFloat("_Glossiness", 0.1f);
                    AssetDatabase.CreateAsset(fixedMat, path);
                }
                mats[i] = fixedMat;
            }
            r.sharedMaterials = mats;
        }

        static void LoadSavedFit(TrollFitRig rig)
        {
            rig.height = TrollFitData.Height; rig.forward = TrollFitData.Forward; rig.side = TrollFitData.Side; rig.up = TrollFitData.Up; rig.yaw = TrollFitData.Yaw;
            rig.logLength = TrollFitData.LogLength;
            rig.logOffset = new Vector3(TrollFitData.LogOffset[0], TrollFitData.LogOffset[1], TrollFitData.LogOffset[2]);
            rig.logEuler = new Vector3(TrollFitData.LogEuler[0], TrollFitData.LogEuler[1], TrollFitData.LogEuler[2]);
            int n = TrollFitMath.PosedBones.Length;
            rig.boneEuler = new Vector3[n];
            var d = TrollFitData.Delta;
            for (int i = 0; i < n && d.Length >= (i + 1) * 4; i++) rig.boneEuler[i] = new Quaternion(d[i * 4], d[i * 4 + 1], d[i * 4 + 2], d[i * 4 + 3]).eulerAngles;
        }

        [MenuItem("SteveTheTroll/Fit/Save fit")]
        public static string SaveFit()
        {
            var rig = Object.FindFirstObjectByType<TrollFitRig>();
            if (rig == null) return Fail("no TrollFitRig in the open scene (Build fit scene first)");
            bool ok = rig.Apply();
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("// GENERATED by SteveTheTroll > Fit > Save (Assets/Editor/TrollFitTool.cs). Do not edit by hand: re-fit in Unity and save again.");
            sb.AppendLine("// Saved: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm", inv) + ", " + rig.lastReport);
            sb.AppendLine("namespace SteveTheTroll.Fit");
            sb.AppendLine("{");
            sb.AppendLine("    public static class TrollFitData");
            sb.AppendLine("    {");
            sb.AppendLine("        public const string SavedAt = \"" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm", inv) + "\";");
            sb.AppendLine("        public const float Height = " + F(rig.height) + ";          // m, world height of the posed troll body");
            sb.AppendLine("        public const float Forward = " + F(rig.forward) + ";         // m along the foot's forward (foot -> toe) from the foot bone");
            sb.AppendLine("        public const float Side = " + F(rig.side) + ";               // m outward from the body (mirrored per foot)");
            sb.AppendLine("        public const float Up = " + F(rig.up) + ";                 // m from the foot bone up to the troll's lowest vertex");
            sb.AppendLine("        public const float Yaw = " + F(rig.yaw) + ";                // deg around up, 0 = faces the player's forward");
            sb.AppendLine("        public const float LogLength = " + F(rig.logLength) + ";        // log length / troll height");
            sb.AppendLine("        public static readonly float[] LogOffset = { " + F(rig.logOffset.x) + ", " + F(rig.logOffset.y) + ", " + F(rig.logOffset.z) + " };   // log centre from the right hand, figure frame, / troll height");
            sb.AppendLine("        public static readonly float[] LogEuler = { " + F(rig.logEuler.x) + ", " + F(rig.logEuler.y) + ", " + F(rig.logEuler.z) + " };      // log rotation in the figure frame, degrees");
            sb.AppendLine();
            sb.AppendLine("        /// Per bone of TrollFitMath.PosedBones: local rotation relative to the troll prefab's rest pose (x, y, z, w).");
            sb.AppendLine("        public static readonly float[] Delta =");
            sb.AppendLine("        {");
            for (int i = 0; i < TrollFitMath.PosedBones.Length; i++)
            {
                var q = rig.Delta(i);
                sb.AppendLine("            " + F(q.x) + ", " + F(q.y) + ", " + F(q.z) + ", " + F(q.w) + ",   // " + TrollFitMath.PosedBones[i] + " euler " + rig.boneEuler[i].ToString("F1"));
            }
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            File.WriteAllText(DataPath, sb.ToString().Replace("\r\n", "\n"));
            AssetDatabase.ImportAsset(DataPath);
            string msg = "TrollFitTool: saved " + DataPath + " (" + rig.lastReport + (ok ? "" : ", HEIGHT CHECK OFF") + ")";
            if (ok) Debug.Log(msg); else Debug.LogWarning(msg);
            return msg;
        }

        static string F(float v) { return v.ToString("0.#####", CultureInfo.InvariantCulture) + "f"; }

        static string Fail(string why) { Debug.LogWarning("TrollFitTool: " + why); return "FAIL: " + why; }
    }
}
