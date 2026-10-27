using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SteveTheTroll.Fit.Tests
{
    /// <summary>
    /// SlipperFit with the real engine (D-025): the slipper is placed in the foot frame with its toe (-Z in the mesh) forward, every prefab
    /// part becomes a SkinnedMeshRenderer on the foot + toe bones with weights by distance in front of the toe joint, the bindposes
    /// reproduce the placed slipper exactly, and bending the toe moves only the front of the slipper.
    /// </summary>
    public class SlipperFitEngineTests : EngineTestBase
    {
        Transform _player, _knee, _foot, _toe;
        GameObject _prefab;
        Material _plush;

        // Mesh space: up +Y, origin on the ground under the middle, toe at -Z, 0.3 m long.
        static readonly Vector3 UpperMin = new Vector3(-0.05f, 0f, -0.15f), UpperMax = new Vector3(0.05f, 0.08f, 0.15f);

        [SetUp]
        public void Rig()
        {
            _player = Go("Player").transform;
            _knee = Go("RightLeg", _player).transform;
            _knee.position = new Vector3(0f, 0.5f, 0f);
            _foot = Go("RightFoot", _knee).transform;
            _foot.position = new Vector3(0f, 0.1f, 0f);
            _toe = Go("RightToeBase", _foot).transform;
            _toe.localPosition = new Vector3(0f, -0.05f, 0.15f);     // toe joint at world z 0.15

            _prefab = Go("SlipperParts");
            _plush = Track(new Material(Shader.Find("Standard")));
            Part("SlipperUpper", BoxMesh(UpperMin, UpperMax));
            Part("SlipperSole", BoxMesh(new Vector3(-0.05f, -0.01f, -0.15f), new Vector3(0.05f, 0f, 0.15f)));
            Go("NoRenderer", _prefab.transform).AddComponent<MeshFilter>().sharedMesh = CubeMesh();       // skipped: no MeshRenderer
            var noMesh = Go("NoMesh", _prefab.transform);                                                 // skipped: no mesh
            noMesh.AddComponent<MeshFilter>();
            noMesh.AddComponent<MeshRenderer>();
        }

        void Part(string name, Mesh mesh)
        {
            var go = Go(name, _prefab.transform);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _plush;
        }

        List<SlipperFit.Part> Build(bool left, float side, float yaw, float scale, out Transform root, Transform toe = null, bool useToe = true)
        {
            var parts = SlipperFit.Build(_prefab, _player, _foot, useToe ? (toe ?? _toe) : null, Vector3.forward, left, 0.05f, side, -0.1f, yaw, scale, out root);
            if (root != null) Track(root.gameObject);
            foreach (var p in parts) Track(p.Renderer.sharedMesh);
            return parts;
        }

        [Test]
        public void MeshToFoot_TurnsTheMeshToeForward()
        {
            Near(Vector3.forward, SlipperFit.MeshToFoot * Vector3.back);
            Assert.That(SlipperFit.BlendWidth, Is.EqualTo(FitGeometry.ToeBlendWidth));
        }

        [Test]
        public void Build_PlacesTheRootInTheFootFrame_ToeForward()
        {
            Transform root;
            var parts = Build(false, 0.01f, 0f, 1f, out root);
            Assert.That(parts.Count, Is.EqualTo(2), "upper + sole; parts without mesh or renderer are skipped");
            Assert.That(root.parent, Is.SameAs(_player));
            Assert.That(root.name, Is.EqualTo(SlipperFit.RootName + "_R"));
            Near(new Vector3(0.01f, 0f, 0.05f), root.position, 1e-4f, "forward 0.05, side 0.01 outward (right), up -0.1 from the foot");
            Near(Vector3.forward, root.TransformDirection(Vector3.back), 1e-4f, "mesh toe");
            Near(Vector3.up, root.up);
        }

        [Test]
        public void Build_LeftFootMirrorsTheSide_YawAndScaleApply()
        {
            Transform root;
            Build(true, 0.01f, 20f, 1.2f, out root);
            Assert.That(root.name, Is.EqualTo(SlipperFit.RootName + "_L"));
            Assert.That(root.position.x, Is.EqualTo(-0.01f).Within(1e-5f));
            Near(Quaternion.Euler(0f, 20f, 0f) * SlipperFit.MeshToFoot, root.rotation);
            Near(Vector3.one * 1.2f, root.localScale);
        }

        [Test]
        public void Build_SkinsEachPartToFootAndToe_WeightsByDistanceInFrontOfTheToe()
        {
            Transform root;
            var parts = Build(false, 0f, 0f, 1f, out root);
            var upper = parts.Find(p => p.Name == "SlipperUpper");
            var smr = upper.Renderer;
            Assert.That(smr.sharedMesh, Is.Not.SameAs(_prefab.transform.Find("SlipperUpper").GetComponent<MeshFilter>().sharedMesh), "own copy");
            Assert.That(smr.bones, Is.EqualTo(new[] { _foot, _toe }));
            Assert.That(smr.rootBone, Is.SameAs(_foot));
            Assert.That(smr.sharedMaterial, Is.SameAs(_plush));
            var verts = smr.sharedMesh.vertices;
            var weights = smr.sharedMesh.boneWeights;
            var toWorld = smr.transform.localToWorldMatrix;
            int front = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                var w = weights[i];
                Assert.That(w.boneIndex0, Is.EqualTo(0));
                Assert.That(w.boneIndex1, Is.EqualTo(1));
                Assert.That(w.weight0 + w.weight1, Is.EqualTo(1f).Within(1e-6f), "weights sum to 1");
                float along = FitGeometry.ToeAlong(toWorld.MultiplyPoint3x4(verts[i]), _toe.position, Vector3.forward);
                Assert.That(w.weight1, Is.EqualTo(FitGeometry.ToeWeight(along)).Within(1e-6f));
                if (w.weight1 > 0.5f) front++;
            }
            // the box runs from z -0.1 to 0.2 in the world, the toe joint is at 0.15: the 12 front vertices (z 0.2) are on the toe
            Assert.That(upper.ToeWeighted, Is.EqualTo(12));
            Assert.That(front, Is.EqualTo(12));
        }

        [Test]
        public void Build_BindposesReproduceThePlacedSlipper()
        {
            Transform root;
            foreach (var part in Build(false, 0.02f, 15f, 1.1f, out root))
            {
                var smr = part.Renderer;
                var binds = smr.sharedMesh.bindposes;
                for (int b = 0; b < 2; b++)
                {
                    var m = smr.bones[b].localToWorldMatrix * binds[b];
                    var want = smr.transform.localToWorldMatrix;
                    for (int k = 0; k < 16; k++) Assert.That(m[k], Is.EqualTo(want[k]).Within(1e-4f), part.Name + " bone " + b + " element " + k);
                }
            }
        }

        [Test]
        public void Build_BendingTheToeMovesOnlyTheFront()
        {
            Transform root;
            var smr = Build(false, 0f, 0f, 1f, out root).Find(p => p.Name == "SlipperUpper").Renderer;
            var before = World(smr);
            var toeBefore = _toe.localToWorldMatrix;
            _toe.localRotation = Quaternion.Euler(40f, 0f, 0f);
            var bend = _toe.localToWorldMatrix * toeBefore.inverse;
            var after = World(smr);
            var weights = smr.sharedMesh.boneWeights;
            int moved = 0;
            for (int i = 0; i < before.Length; i++)
            {
                if (weights[i].weight1 == 0f) Near(before[i], after[i], 1e-4f, "heel vertex " + i);
                else if (weights[i].weight1 == 1f) { Near(bend.MultiplyPoint3x4(before[i]), after[i], 1e-4f, "toe vertex " + i); moved++; }
            }
            Assert.That(moved, Is.EqualTo(12));
        }

        Vector3[] World(SkinnedMeshRenderer smr)
        {
            var baked = Track(new Mesh());
            smr.BakeMesh(baked);                                   // part scale is 1 here, so local -> world is the plain matrix
            var v = baked.vertices;
            var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(v[i]);
            return v;
        }

        [Test]
        public void Build_WithoutToe_AllOnTheFoot()
        {
            Transform root;
            var parts = Build(false, 0f, 0f, 1f, out root, null, false);
            Assert.That(parts.Count, Is.EqualTo(2));
            foreach (var p in parts)
            {
                Assert.That(p.ToeWeighted, Is.EqualTo(0));
                Assert.That(p.Renderer.bones, Is.EqualTo(new[] { _foot, _foot }));
                foreach (var w in p.Renderer.sharedMesh.boneWeights) Assert.That(w.weight1, Is.EqualTo(0f));
            }
        }

        [Test]
        public void Build_WithoutFootOrPrefab_BuildsNothing()
        {
            Transform root;
            Assert.That(SlipperFit.Build(_prefab, _player, null, _toe, Vector3.forward, false, 0f, 0f, 0f, 0f, 1f, out root), Is.Empty);
            Assert.That(root, Is.Null);
            Assert.That(SlipperFit.Build(null, _player, _foot, _toe, Vector3.forward, false, 0f, 0f, 0f, 0f, 1f, out root), Is.Empty);
            Assert.That(root, Is.Null);
        }
    }
}
