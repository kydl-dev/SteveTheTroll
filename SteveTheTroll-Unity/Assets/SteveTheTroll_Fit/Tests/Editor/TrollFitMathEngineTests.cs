using NUnit.Framework;
using UnityEngine;

namespace SteveTheTroll.Fit.Tests
{
    /// <summary>
    /// TrollFitMath with the real engine: foot direction from bones (D-016 / D-029), placement and its rotation, pose, BakeMesh measuring
    /// (the world-size trap in MISTAKES), log placement next to the hand (D-018 / D-019), log turn order, bone search. Also checks that
    /// FitGeometry's managed formulas give the engine's answers (they replaced Quaternion calls so the .NET tests + Stryker can run them).
    /// </summary>
    public class TrollFitMathEngineTests : EngineTestBase
    {
        static readonly Vector3[] Dirs =
        {
            new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f), new Vector3(-0.3f, 0.8f, 0.5f), new Vector3(0.7f, -2f, -0.4f), new Vector3(-1f, 0f, -1f),
        };

        // ---------------- FitGeometry == engine ----------------

        [Test]
        public void RotateY_EqualsQuaternionAngleAxis()
        {
            foreach (var v in Dirs)
                for (float a = -180f; a <= 180f; a += 7.5f)
                    Near(Quaternion.AngleAxis(a, Vector3.up) * v, FitGeometry.RotateY(v, a), 1e-4f, "angle " + a);
        }

        [Test]
        public void FigureYawDeg_EqualsLookRotationTimesYaw()
        {
            foreach (var v in Dirs)
                for (float yaw = -170f; yaw <= 170f; yaw += 34f)
                {
                    var old = Quaternion.LookRotation(Vector3.ProjectOnPlane(v, Vector3.up).normalized, Vector3.up) * Quaternion.Euler(0f, yaw, 0f);
                    Near(old, Quaternion.Euler(0f, FitGeometry.FigureYawDeg(v, yaw), 0f), 0.01f, "dir " + v + " yaw " + yaw);
                }
        }

        [Test]
        public void Flat_EqualsProjectOnPlaneNormalised()
        {
            Random.InitState(7);
            for (int i = 0; i < 200; i++)
            {
                var v = Random.insideUnitSphere * 5f;
                if (Vector3.ProjectOnPlane(v, Vector3.up).sqrMagnitude < 0.001f) continue;
                Near(Vector3.ProjectOnPlane(v, Vector3.up).normalized, TrollFitMath.Flat(v, Vector3.forward));
            }
        }

        // ---------------- FootForward with bones ----------------

        Transform _knee, _foot, _toe;

        void Leg(Vector3 kneePos)
        {
            _knee = Go("LeftLeg").transform;
            _knee.position = kneePos;
            _foot = Go("LeftFoot", _knee).transform;
            _foot.position = kneePos + new Vector3(0f, -0.4f, 0f);
            _toe = Go("LeftToeBase", _foot).transform;
            _toe.localPosition = new Vector3(0f, -0.05f, 0.15f);
        }

        [Test]
        public void FootForward_FlatFoot_IsTheToeDirection()
        {
            Leg(new Vector3(1f, 0.5f, 2f));
            Near(Vector3.forward, TrollFitMath.FootForward(_foot, _toe, Vector3.forward));
            _knee.rotation = Quaternion.Euler(0f, 30f, 0f);
            Near(Quaternion.Euler(0f, 30f, 0f) * Vector3.forward, TrollFitMath.FootForward(_foot, _toe, Vector3.forward), 1e-3f);
        }

        /// TEST-005 regression with real bones: the foot pitched 100 deg (running) - the toe points down and back, the troll must not turn round.
        [Test]
        public void FootForward_FootPitched100_StillForward()
        {
            Leg(Vector3.up);
            _foot.localRotation = Quaternion.Euler(100f, 0f, 0f);
            Assert.That(Vector3.Dot(_toe.position - _foot.position, Vector3.forward), Is.LessThan(0f), "precondition: toe behind the foot");
            Near(Vector3.forward, TrollFitMath.FootForward(_foot, _toe, Vector3.forward), 1e-3f);
        }

        [Test]
        public void FootForward_EqualsTheGeometryForAnyPose()
        {
            Leg(Vector3.up);
            Random.InitState(11);
            for (int i = 0; i < 100; i++)
            {
                _knee.rotation = Random.rotationUniform;
                _foot.localRotation = Random.rotationUniform;
                var pf = Random.onUnitSphere;
                var want = FitGeometry.FootForward(_toe.position - _foot.position, _foot.position - _knee.position, pf);
                Near(want, TrollFitMath.FootForward(_foot, _toe, pf), 1e-5f, "pose " + i);
            }
        }

        [Test]
        public void FootForward_MissingToeOrParent()
        {
            Leg(Vector3.up);
            Near(new Vector3(1f, 0f, 0f), TrollFitMath.FootForward(_foot, null, new Vector3(2f, 1f, 0f)));
            _foot.SetParent(null, true);           // no knee: the shin is taken as straight down
            Near(Vector3.forward, TrollFitMath.FootForward(_foot, _toe, Vector3.forward));
        }

        // ---------------- Place ----------------

        [Test]
        public void Place_PositionInTheFootFrame_RotationUprightAndFacingTheFoot()
        {
            Vector3 pos; Quaternion rot;
            var fwd = new Vector3(0.3f, -0.5f, 1f);
            TrollFitMath.Place(new Vector3(1f, 0.2f, 3f), fwd, true, 0.1f, 0.05f, 0.02f, 15f, 0.04f, out pos, out rot);
            Near(FitGeometry.FootFramePosition(new Vector3(1f, 0.2f, 3f), fwd, true, 0.1f, 0.05f, 0.02f, 0.04f), pos);
            Assert.That(pos.y, Is.EqualTo(0.26f).Within(1e-5f));
            Near(Vector3.up, rot * Vector3.up);
            Near(Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, Vector3.up), Vector3.up) * Quaternion.Euler(0f, 15f, 0f), rot);
        }

        // ---------------- ApplyPose ----------------

        [Test]
        public void ApplyPose_RestThenDeltaThenExtra_SkipsMissingBones()
        {
            var a = Go("A").transform;
            var c = Go("C").transform;
            var rest = new[] { Quaternion.Euler(10f, 0f, 0f), Quaternion.Euler(0f, 20f, 0f), Quaternion.Euler(0f, 0f, 30f) };
            var delta = new[] { Quaternion.Euler(0f, 5f, 0f), Quaternion.identity, Quaternion.Euler(40f, 0f, 0f) };
            var extra = new[] { Quaternion.Euler(0f, 0f, 7f) };
            int posed = TrollFitMath.ApplyPose(new[] { a, null, c }, rest, delta, extra);
            Assert.That(posed, Is.EqualTo(2));
            Near(rest[0] * delta[0] * extra[0], a.localRotation);
            Near(rest[2] * delta[2], c.localRotation, 0.01f, "no extra past the extra array");
            Assert.That(TrollFitMath.ApplyPose(new[] { a }, rest, delta, null), Is.EqualTo(1));
            Near(rest[0] * delta[0], a.localRotation, 0.01f, "extra null");
        }

        // ---------------- MeasureY (BakeMesh) ----------------

        SkinnedMeshRenderer SkinnedBox(Transform root, Vector3 bodyScale, Vector3 boneScale)
        {
            var bone = Go("Bone", root).transform;
            bone.localScale = boneScale;
            var body = Go("Body", bone);
            body.transform.localScale = bodyScale;
            var mesh = CubeMesh();
            var w = new BoneWeight[mesh.vertexCount];
            for (int i = 0; i < w.Length; i++) w[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            mesh.boneWeights = w;
            mesh.bindposes = new[] { bone.worldToLocalMatrix * body.transform.localToWorldMatrix };
            var smr = body.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = new[] { bone };
            smr.rootBone = bone;
            return smr;
        }

        /// MISTAKES "BakeMesh space": a renderer scaled x130 (the troll Body) must measure its WORLD height, not 130 x that or 1 / 130.
        [TestCase(130f, 1f, 130f)]
        [TestCase(1f, 0.01f, 0.01f)]
        [TestCase(0.5f, 2f, 1f)]
        public void MeasureY_GivesTheWorldHeight(float bodyScale, float boneScale, float worldHeight)
        {
            var root = Go("Troll").transform;
            root.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 30f, 0f));
            var smr = SkinnedBox(root, Vector3.one * bodyScale, Vector3.one * boneScale);
            float minY, maxY;
            Assert.That(TrollFitMath.MeasureY(smr, Track(new Mesh()), out minY, out maxY), Is.True);
            Assert.That(maxY - minY, Is.EqualTo(worldHeight).Within(worldHeight * 1e-3f), "height");
            Assert.That((minY + maxY) * 0.5f, Is.EqualTo(2f).Within(worldHeight * 1e-3f + 1e-4f), "middle at the root height");
        }

        [Test]
        public void MeasureY_MissingParts_ReturnsFalse()
        {
            float minY, maxY;
            Assert.That(TrollFitMath.MeasureY(null, Track(new Mesh()), out minY, out maxY), Is.False);
            var smr = SkinnedBox(Go("T").transform, Vector3.one, Vector3.one);
            Assert.That(TrollFitMath.MeasureY(smr, null, out minY, out maxY), Is.False);
        }

        // ---------------- PlaceLog (D-018 / D-019) ----------------

        Transform _figure, _hand, _logRoot;
        MeshFilter _logMesh;

        void Figure()
        {
            _figure = Go("Figure").transform;
            _figure.SetPositionAndRotation(new Vector3(2f, 0f, 1f), Quaternion.Euler(0f, 40f, 0f));
            var arm = Go("RightForeArm", _figure).transform;
            arm.localScale = Vector3.one * 130f;                    // troll bones carry ~x130 scales; the log must not inherit them
            _hand = Go("RightHand", arm).transform;
            _hand.localPosition = new Vector3(0.001f, 0.002f, 0.0005f);
            _logRoot = Go("attach", _figure).transform;
            var log = Go("log", _logRoot);
            log.transform.localScale = Vector3.one * 2f;
            _logMesh = log.AddComponent<MeshFilter>();
            _logMesh.sharedMesh = BoxMesh(new Vector3(-0.05f, -0.4f, -0.05f), new Vector3(0.05f, 0.6f, 0.05f));    // 1 long, centre off the pivot
        }

        Vector3 LogCentre() { return _logMesh.transform.TransformPoint(_logMesh.sharedMesh.bounds.center); }

        [Test]
        public void PlaceLog_LengthCentreAndRotation()
        {
            Figure();
            var offset = new Vector3(0.1f, -0.05f, 0.2f);
            var euler = new Vector3(10f, 20f, 30f);
            float length = TrollFitMath.PlaceLog(_logRoot, _logMesh, _figure, _hand, 0.3f, 0.5f, offset, euler);
            Assert.That(length, Is.EqualTo(0.15f).Within(1e-4f));
            Assert.That(_logMesh.transform.lossyScale.x, Is.EqualTo(0.15f).Within(1e-4f), "1 m mesh scaled to 0.15 m");
            Near(_hand.position + _figure.rotation * (offset * 0.3f), LogCentre(), 1e-4f, "centre");
            Near(_figure.rotation * Quaternion.Euler(euler), _logRoot.rotation);
        }

        [Test]
        public void PlaceLog_FineTuneOffsetInMetresAndTurnInTheFigureFrame()
        {
            Figure();
            var offset = new Vector3(0.1f, 0f, 0f);
            var extra = new Vector3(0.03f, -0.01f, 0f);
            var turn = TrollFitMath.LogTurn(15f, -5f, 20f);
            TrollFitMath.PlaceLog(_logRoot, _logMesh, _figure, _hand, 0.3f, 0.5f, offset, Vector3.zero, extra, turn);
            Near(_hand.position + _figure.rotation * (offset * 0.3f + extra), LogCentre(), 1e-4f, "centre");
            Near(_figure.rotation * turn, _logRoot.rotation);
        }

        [Test]
        public void PlaceLog_AgainDoesNotDrift()
        {
            Figure();
            TrollFitMath.PlaceLog(_logRoot, _logMesh, _figure, _hand, 0.3f, 0.5f, new Vector3(0.1f, 0f, 0.1f), new Vector3(0f, 0f, 80f));
            var pos = _logRoot.position; var scale = _logRoot.localScale;
            float length = TrollFitMath.PlaceLog(_logRoot, _logMesh, _figure, _hand, 0.3f, 0.5f, new Vector3(0.1f, 0f, 0.1f), new Vector3(0f, 0f, 80f));
            Assert.That(length, Is.EqualTo(0.15f).Within(1e-4f));
            Near(pos, _logRoot.position, 1e-5f, "position");
            Near(scale, _logRoot.localScale, 1e-5f, "scale");
        }

        [Test]
        public void PlaceLog_MissingPart_ReturnsZero()
        {
            Figure();
            Assert.That(TrollFitMath.PlaceLog(_logRoot, _logMesh, _figure, null, 0.3f, 0.5f, Vector3.zero, Vector3.zero), Is.EqualTo(0f));
            Assert.That(TrollFitMath.PlaceLog(null, _logMesh, _figure, _hand, 0.3f, 0.5f, Vector3.zero, Vector3.zero), Is.EqualTo(0f));
        }

        // ---------------- LogTurn / bones ----------------

        [Test]
        public void LogTurn_YawAboutUp_PitchAboutRight_RollAboutForward_InThatOrder()
        {
            Near(Vector3.right, TrollFitMath.LogTurn(90f, 0f, 0f) * Vector3.forward);
            Near(Vector3.down, TrollFitMath.LogTurn(0f, 90f, 0f) * Vector3.forward);
            Near(Vector3.left, TrollFitMath.LogTurn(0f, 0f, 90f) * Vector3.up);
            Near(Vector3.right, TrollFitMath.LogTurn(90f, 90f, 0f) * Vector3.up, 1e-4f, "pitch first, then yaw");
            Near(Quaternion.identity, TrollFitMath.LogTurn(0f, 0f, 0f));
        }

        [Test]
        public void Bones_ToeOf_FindDeep_FindBody_KnuckleLine()
        {
            Assert.That(TrollFitMath.ToeOf("LeftFoot"), Is.EqualTo("LeftToeBase"));
            Assert.That(TrollFitMath.ToeOf("RightFoot"), Is.EqualTo("RightToeBase"));
            var root = Go("Visual").transform;
            var hand = Go("RightHand", Go("Arm", root).transform).transform;
            var index = Go("RightHandIndex1", hand).transform;
            Assert.That(TrollFitMath.KnuckleLine(root), Is.EqualTo(Vector3.zero), "ring missing");
            var ring = Go("RightHandRing1", hand).transform;
            index.position = new Vector3(0f, 0f, 0f);
            ring.position = new Vector3(0f, 0f, -0.3f);
            Assert.That(TrollFitMath.FindDeep(root, "RightHandRing1"), Is.SameAs(ring));
            Assert.That(TrollFitMath.FindDeep(root, "Visual"), Is.SameAs(root));
            Assert.That(TrollFitMath.FindDeep(root, "Nope"), Is.Null);
            Near(Vector3.back, TrollFitMath.KnuckleLine(root));

            var small = Go("Hair", root).AddComponent<SkinnedMeshRenderer>();
            small.sharedMesh = BoxMesh(Vector3.zero, Vector3.one);
            var big = Go("Body", root).AddComponent<SkinnedMeshRenderer>();
            var bigMesh = Track(new Mesh());
            bigMesh.vertices = new Vector3[48];                     // more vertices than the 24 of the cube
            big.sharedMesh = bigMesh;
            Go("Empty", root).AddComponent<SkinnedMeshRenderer>();
            Assert.That(TrollFitMath.FindBody(root), Is.SameAs(big), "the renderer with the most vertices");
        }
    }
}
