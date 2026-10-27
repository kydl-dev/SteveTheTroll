using UnityEngine;

namespace SteveTheTroll.Fit
{
    /// <summary>
    /// Unity-only fit rig (D-014, not in the mod): holds the fit values and shows them live in edit mode on the reference player's feet
    /// with exactly the mod's math (TrollFitMath). The agent changes the values (D-015), "SteveTheTroll > Fit > Save fit" writes them into
    /// TrollFitData.cs, which the mod compiles. Per-bone pose = Euler degrees on top of the troll's rest rotation (stored as quaternion).
    /// </summary>
    [ExecuteAlways]
    public sealed class TrollFitRig : MonoBehaviour
    {
        public Transform player;
        public Transform leftFoot, rightFoot;
        public Transform leftTroll, rightTroll;

        [Header("Placement (metres / degrees)")]
        public float height = 0.28f;
        public float forward = 0.06f;
        public float side;
        public float up;
        public float yaw;

        [Header("Log (ratios of the troll height, degrees in the figure frame)")]
        public float logLength = 0.5f;
        public Vector3 logOffset = new Vector3(0f, 0f, 0.1f);
        public Vector3 logEuler;

        [Header("Slipper (D-025): placement in the foot frame, metres / degrees")]
        public float slipperForward = 0.086f;
        public float slipperUp = -0.125f;
        public float slipperSide;
        public float slipperYaw;
        public float slipperScale = 1f;
        public int slipperFurShells = 16;
        public float slipperFurLength = 0.006f;

        /// (Re)builds both slippers with the shared SlipperFit (as the mod does). Agent: call after changing the slipper fields.
        public string BuildSlippers()
        {
#if UNITY_EDITOR
            foreach (var old in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (old != null && old.name.StartsWith(SlipperFit.RootName)) DestroyImmediate(old.gameObject);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SteveTheTroll_Slipper/SlipperParts.prefab");
            var fur = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/SteveTheTroll_Fur/TrollFur.mat");
            string report = "";
            foreach (var left in new[] { true, false })
            {
                var foot = left ? leftFoot : rightFoot;
                Transform root;
                var parts = SlipperFit.Build(prefab, transform, foot, TrollFitMath.FindDeep(player, TrollFitMath.ToeOf(foot.name)), player.forward, left,
                    slipperForward, slipperSide, slipperUp, slipperYaw, slipperScale, out root);
                foreach (var p in parts)
                {
                    if (p.Name == "SlipperSole" || fur == null) continue;
                    var col = p.Renderer.sharedMaterials[0].GetColor("_Color");
                    var shells = TrollFur.AddShells(p.Renderer, fur, slipperFurShells);
                    TrollFur.Apply(shells, slipperFurLength, 900f, 1.2f, 0.0008f, col, Color.white * 1.15f);
                }
                report += (left ? "L " : "R ") + parts.Count + " part(s); ";
            }
            return report;
#else
            return "editor only";
#endif
        }

        [Header("Pose: Euler degrees per TrollFitMath.PosedBones, on top of rest")]
        public Vector3[] boneEuler = new Vector3[TrollFitMath.PosedBones.Length];
        [HideInInspector] public Quaternion[] rest = new Quaternion[0];

        public string lastReport = "";
        Mesh _scratch;

        /// Delta quaternion of bone i (what Save writes).
        public Quaternion Delta(int i)
        {
            return i < boneEuler.Length ? Quaternion.Euler(boneEuler[i]) : Quaternion.identity;
        }

        void OnEnable() { TrollFitMath.Log = s => { }; TrollFitMath.Warn = s => Debug.LogWarning(s); }

        void Update() { Apply(); }

        /// Poses, scales and places both trolls. Expected outcome: both measured heights equal `height` within 1 cm (returns that check).
        public bool Apply()
        {
            if (player == null || leftFoot == null || rightFoot == null || leftTroll == null || rightTroll == null) { lastReport = "rig not wired"; return false; }
            if (boneEuler.Length != TrollFitMath.PosedBones.Length) System.Array.Resize(ref boneEuler, TrollFitMath.PosedBones.Length);
            float hl = One(leftTroll, leftFoot, true), hr = One(rightTroll, rightFoot, false);
            lastReport = "left " + hl.ToString("F3") + " m, right " + hr.ToString("F3") + " m (target " + height.ToString("F3") + ")";
            return Mathf.Abs(hl - height) < 0.01f && Mathf.Abs(hr - height) < 0.01f;
        }

        float One(Transform troll, Transform foot, bool left)
        {
            int n = TrollFitMath.PosedBones.Length;
            var bones = new Transform[n];
            var delta = new Quaternion[n];
            for (int i = 0; i < n; i++) { bones[i] = TrollFitMath.FindDeep(troll, TrollFitMath.PosedBones[i]); delta[i] = Delta(i); }
            if (rest.Length != n) { lastReport = "rest pose missing: rebuild the fit scene"; return 0f; }
            TrollFitMath.ApplyPose(bones, rest, delta, null);
            var body = TrollFitMath.FindBody(troll);
            if (_scratch == null) _scratch = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            float min, max;
            Vector3 pos; Quaternion rot;
            TrollFitMath.Place(foot.position, TrollFitMath.FootForward(foot, TrollFitMath.FindDeep(player, TrollFitMath.ToeOf(foot.name)), player.forward), left, forward, side, up, yaw, 0f, out pos, out rot);
            troll.SetPositionAndRotation(pos, rot);
            if (!TrollFitMath.MeasureY(body, _scratch, out min, out max) || max - min < 0.0001f) return 0f;
            troll.localScale *= height / (max - min);
            TrollFitMath.MeasureY(body, _scratch, out min, out max);
            float bottom = troll.position.y - min;
            TrollFitMath.Place(foot.position, TrollFitMath.FootForward(foot, TrollFitMath.FindDeep(player, TrollFitMath.ToeOf(foot.name)), player.forward), left, forward, side, up, yaw, bottom, out pos, out rot);
            troll.SetPositionAndRotation(pos, rot);
            var log = troll.Find("TrollLog");
            var logMesh = log != null ? log.GetComponentInChildren<MeshFilter>() : null;
            TrollFitMath.PlaceLog(log, logMesh, troll, TrollFitMath.FindDeep(troll, "RightHand"), height, logLength, logOffset, logEuler);
            return max - min;
        }

        /// Fitting helper (agent, D-015): turns `bone` by `deg` around an axis given in the FIGURE frame (x = troll's right, y = up,
        /// z = forward) and stores the result as that bone's Euler. No sign guessing: the axis is in world terms.
        /// Expected outcome: returns the new Euler of the bone.
        public Vector3 Turn(string bone, Vector3 figureAxis, float deg)
        {
            Apply();
            int i = System.Array.IndexOf(TrollFitMath.PosedBones, bone);
            var b = i >= 0 ? TrollFitMath.FindDeep(leftTroll, bone) : null;
            if (b == null) { Debug.LogWarning("TrollFitRig: Turn unknown bone " + bone); return Vector3.zero; }
            var axis = leftTroll.rotation * figureAxis.normalized;
            b.rotation = Quaternion.AngleAxis(deg, axis) * b.rotation;
            boneEuler[i] = (Quaternion.Inverse(rest[i]) * b.localRotation).eulerAngles;
            Apply();
            return boneEuler[i];
        }
    }
}
