using System.Globalization;
using System.Text;
using SteveTheTroll.Fit;
using UnityEditor;
using UnityEngine;

namespace SteveTheTroll.EditorTools
{
    /// <summary>
    /// D-041 (user 2026-10-27: "copy it to mini trolls", screenshot 154608 shot-003): reads the game's troll idle clips (copied from the
    /// AssetRipper project into Assets/RipReference/AnimationClip, git-ignored) on a temporary copy of the reference troll and copies the LEFT
    /// arm / hand / finger pose of one frame into the fit rig (relative to the rest pose, as the fit stores it). Then Save fit as usual.
    /// Scan = a table per clip (left hand height / forwardness in the chest frame) to pick the frame; Apply = copy that frame.
    /// </summary>
    public static class TrollPoseSampler
    {
        const string ClipFolder = "Assets/RipReference/AnimationClip";
        const string TrollPrefab = "Assets/RipReference/GameObject/Troll.prefab";
        // the clips are HUMANOID (muscle curves): they need the troll's avatar, copied from the rip too (the prefab copy has no avatar)
        const string AvatarPath = "Assets/RipReference/Avatar/_troll_baseAvatar.asset";
        static readonly string[] LeftChain =
        {
            "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
            "LeftHandIndex1", "LeftHandIndex2", "LeftHandIndex3", "LeftHandMiddle1", "LeftHandMiddle2", "LeftHandMiddle3",
            "LeftHandRing1", "LeftHandRing2", "LeftHandRing3", "LeftHandThumb1", "LeftHandThumb2", "LeftHandThumb3",
        };

        /// Every clip in the folder, every `step` seconds: left hand position in Spine2's frame (x right, y up, z forward, metres of the
        /// unscaled troll). Expected outcome: the table text (also in the Console).
        public static string Scan(float step = 0.1f)
        {
            var sb = new StringBuilder("TrollPoseSampler: left hand in the Spine2 frame (m), per clip and time\n");
            GameObject troll;
            Animator anim;
            if (!Make(out troll, out anim)) return Fail("no reference troll / animator");
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipFolder }))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                    if (clip == null) continue;
                    sb.Append("== ").Append(clip.name).Append(" (").Append(F(clip.length)).Append(" s)\n");
                    float bestY = float.MinValue, bestT = 0f;
                    for (float t = 0f; t <= clip.length + 1e-4f; t += step)
                    {
                        clip.SampleAnimation(anim.gameObject, t);
                        var p = HandInChest(troll);
                        sb.Append("  t ").Append(F(t)).Append(": x ").Append(F(p.x)).Append(" y ").Append(F(p.y)).Append(" z ").Append(F(p.z)).Append('\n');
                        if (p.y > bestY) { bestY = p.y; bestT = t; }
                    }
                    sb.Append("  highest left hand at t ").Append(F(bestT)).Append(" (y ").Append(F(bestY)).Append(")\n");
                }
            }
            finally { Object.DestroyImmediate(troll); }
            Debug.Log(sb.ToString());
            return sb.ToString();
        }

        /// Copies the left chain of `clipName` at `time` into the rig's boneEuler (relative to rig.rest). Expected outcome: "OK ..." with the
        /// copied bone count, or "FAIL: ...".
        public static string Apply(string clipName, float time)
        {
            var rig = Object.FindFirstObjectByType<TrollFitRig>();
            if (rig == null) return Fail("no TrollFitRig in the open scene (open RipReference/TrollFit.unity)");
            if (rig.rest.Length != TrollFitMath.PosedBones.Length) return Fail("rig rest pose missing (Build fit scene)");
            AnimationClip clip = null;
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipFolder }))
            {
                var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null && c.name == clipName) clip = c;
            }
            if (clip == null) return Fail("clip '" + clipName + "' not in " + ClipFolder);
            GameObject troll;
            Animator anim;
            if (!Make(out troll, out anim)) return Fail("no reference troll / animator");
            var sb = new StringBuilder();
            int copied = 0;
            try
            {
                clip.SampleAnimation(anim.gameObject, time);
                var hand = HandInChest(troll);
                Undo.RecordObject(rig, "Copy left arm pose");
                foreach (var bone in LeftChain)
                {
                    int i = System.Array.IndexOf(TrollFitMath.PosedBones, bone);
                    var b = TrollFitMath.FindDeep(troll.transform, bone);
                    if (i < 0 || b == null) { sb.Append(bone).Append(" skipped (").Append(i < 0 ? "not posed by the fit" : "not found").Append("); "); continue; }
                    var before = rig.boneEuler[i];
                    rig.boneEuler[i] = (Quaternion.Inverse(rig.rest[i]) * b.localRotation).eulerAngles;
                    sb.Append(bone).Append(' ').Append(before.ToString("F1")).Append(" -> ").Append(rig.boneEuler[i].ToString("F1")).Append("; ");
                    copied++;
                }
                EditorUtility.SetDirty(rig);
                bool ok = rig.Apply();
                string msg = "OK: TrollPoseSampler: " + clip.name + " t " + F(time) + " (left hand in the chest frame " + hand.ToString("F3") + " m): " + copied + " bone(s) copied, rig "
                    + rig.lastReport + (ok ? "" : " HEIGHT CHECK OFF") + "\n" + sb;
                Debug.Log(msg);
                return msg;
            }
            finally { Object.DestroyImmediate(troll); }
        }

        /// Renders the left-foot troll from the front and from its left side into Assets/RipReference/Shots/<name>_front|side.png (git-ignored).
        /// Expected outcome: the two file paths.
        public static string Shot(string name)
        {
            var rig = Object.FindFirstObjectByType<TrollFitRig>();
            if (rig != null) rig.Apply();
            var t = rig != null ? rig.leftTroll : null;
            if (t == null) return Fail("no rig / left troll");
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return Fail("left troll has no renderers");
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            // Camera.Render outside the player loop does not re-skin SkinnedMeshRenderers (shots stayed in the old pose): bake every skinned
            // renderer of both trolls into a static copy for the shots (Unity 6000.0, useScale false: world = TRS(position, rotation, one) * baked vertex).
            var baked = new System.Collections.Generic.List<GameObject>();
            var hidden = new System.Collections.Generic.List<SkinnedMeshRenderer>();
            foreach (var troll in new[] { rig.leftTroll, rig.rightTroll })
            {
                if (troll == null) continue;
                foreach (var smr in troll.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!smr.enabled) continue;
                    var mesh = new Mesh();
                    smr.BakeMesh(mesh);                         // useScale false = world size in the renderer frame (TrollFitMath.MeasureY)
                    var copy = new GameObject("TrollPoseSampler_baked_" + smr.name) { hideFlags = HideFlags.DontSave };
                    copy.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                    copy.AddComponent<MeshFilter>().sharedMesh = mesh;
                    copy.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                    baked.Add(copy);
                    smr.enabled = false;
                    hidden.Add(smr);
                }
            }
            var sb = new StringBuilder();
            try
            {
            foreach (var view in new[] { new Vector3(0f, 0.15f, 1f), new Vector3(-1f, 0.2f, 0.5f) })
            {
                var go = new GameObject("TrollPoseSampler_cam") { hideFlags = HideFlags.DontSave };
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = 30f; cam.nearClipPlane = 0.01f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.5f, 0.55f, 0.6f);
                var dir = (t.right * view.x + t.up * view.y + t.forward * view.z).normalized;
                go.transform.position = b.center + dir * (b.extents.magnitude * 4f);
                go.transform.LookAt(b.center);
                var rt = new RenderTexture(900, 900, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(900, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                string path = System.IO.Path.GetFullPath("Assets/RipReference/Shots/" + name + (view.x < 0f ? "_side" : "_front") + ".png");
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
                sb.Append(path).Append("; ");
            }
            }
            finally
            {
                foreach (var smr in hidden) if (smr != null) smr.enabled = true;
                foreach (var go in baked) { var mf = go.GetComponent<MeshFilter>(); if (mf != null) Object.DestroyImmediate(mf.sharedMesh); Object.DestroyImmediate(go); }
            }
            Debug.Log("TrollPoseSampler: shots " + sb + "(bounds " + b.size.ToString("F3") + " m)");
            return sb.ToString();
        }

        static bool Make(out GameObject troll, out Animator anim)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrollPrefab);
            troll = prefab != null ? (GameObject)Object.Instantiate(prefab) : null;
            anim = troll != null ? troll.GetComponentInChildren<Animator>(true) : null;
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
            if (anim != null) anim.avatar = avatar;
            if (troll != null) { troll.name = "TrollPoseSampler_temp"; troll.hideFlags = HideFlags.DontSave; troll.transform.position = new Vector3(0f, -200f, 0f); }
            if (anim == null && troll != null) { Object.DestroyImmediate(troll); troll = null; }
            Debug.Log("TrollPoseSampler: temp troll " + (troll != null) + ", animator on '" + (anim != null ? anim.gameObject.name : "-") + "', avatar " + (avatar != null ? avatar.name + " human " + avatar.isHuman : "MISSING " + AvatarPath));
            return troll != null && anim != null;
        }

        static Vector3 HandInChest(GameObject troll)
        {
            var chest = TrollFitMath.FindDeep(troll.transform, "Spine2");
            var hand = TrollFitMath.FindDeep(troll.transform, "LeftHand");
            if (chest == null || hand == null) return Vector3.zero;
            var root = troll.transform;
            // chest frame with the troll's own axes (x right, y up, z forward) at the chest position, unscaled
            var local = Quaternion.Inverse(root.rotation) * (hand.position - chest.position);
            return local / Mathf.Max(1e-4f, root.lossyScale.x);
        }

        static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        static string Fail(string why) { Debug.LogWarning("TrollPoseSampler: " + why); return "FAIL: " + why; }
    }
}
