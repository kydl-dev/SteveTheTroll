using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SteveTheTroll.Fit.Tests
{
    /// <summary>
    /// TEST-007 with the real engine: SkinTransfer reads a skinned "player" body in its current pose (BakeMesh), keeps only one leg's
    /// shin / foot / toe vertices, gives points the player's weights; SkinPoint follows the bones; a slipper built with it bends with the
    /// toe AND the ankle; three skinned points give a frame that tilts with the toe (the troll's glue).
    /// </summary>
    public class SkinTransferEngineTests : EngineTestBase
    {
        Transform _root, _shin, _foot, _toe, _otherFoot;
        SkinnedMeshRenderer _body;

        // A tiny player: left shin / foot / toe bones, and a right foot that must never be used for the left slipper.
        [SetUp]
        public void Body()
        {
            _root = Go("Player").transform;
            _shin = Go("LeftLeg", _root).transform;
            _shin.position = new Vector3(0f, 0.5f, 0f);
            _foot = Go("LeftFoot", _shin).transform;
            _foot.position = new Vector3(0f, 0.1f, 0f);
            _toe = Go("LeftToeBase", _foot).transform;
            _toe.position = new Vector3(0f, 0.05f, 0.15f);
            _otherFoot = Go("RightFoot", _root).transform;
            _otherFoot.position = new Vector3(0.3f, 0.1f, 0f);
            var bones = new[] { _shin, _foot, _toe, _otherFoot };

            var v = new List<Vector3>();
            var w = new List<BoneWeight>();
            Cloud(v, w, new Vector3(-0.04f, 0.02f, -0.06f), new Vector3(0.04f, 0.12f, 0.1f), 1);    // foot
            Cloud(v, w, new Vector3(-0.04f, 0.02f, 0.13f), new Vector3(0.04f, 0.08f, 0.22f), 2);    // toes
            Cloud(v, w, new Vector3(-0.04f, 0.2f, -0.04f), new Vector3(0.04f, 0.45f, 0.04f), 0);    // shin
            Cloud(v, w, new Vector3(0.26f, 0.02f, -0.06f), new Vector3(0.34f, 0.12f, 0.1f), 3);     // the other foot
            var mesh = Track(new Mesh());
            mesh.SetVertices(v);
            var tris = new int[v.Count - v.Count % 3];
            for (int i = 0; i < tris.Length; i++) tris[i] = i;
            mesh.SetTriangles(tris, 0);
            mesh.boneWeights = w.ToArray();
            var bodyGo = Go("Body", _root);
            var binds = new Matrix4x4[bones.Length];
            for (int b = 0; b < bones.Length; b++) binds[b] = bones[b].worldToLocalMatrix * bodyGo.transform.localToWorldMatrix;
            mesh.bindposes = binds;
            _body = bodyGo.AddComponent<SkinnedMeshRenderer>();
            _body.sharedMesh = mesh;
            _body.bones = bones;
            _body.rootBone = _root;
        }

        // 4 x 4 x 4 grid of vertices in the box, all on one bone.
        static void Cloud(List<Vector3> v, List<BoneWeight> w, Vector3 min, Vector3 max, int bone)
        {
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    for (int z = 0; z < 4; z++)
                    {
                        v.Add(new Vector3(Mathf.Lerp(min.x, max.x, x / 3f), Mathf.Lerp(min.y, max.y, y / 3f), Mathf.Lerp(min.z, max.z, z / 3f)));
                        w.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f });
                    }
        }

        SkinTransfer Skin() { return new SkinTransfer(_body, new[] { _foot, _toe, _shin }); }

        [Test]
        public void Refresh_KeepsOnlyTheAllowedLeg()
        {
            var skin = Skin();
            Assert.That(skin.Refresh(), Is.EqualTo(3 * 64), "foot + toes + shin, not the other foot");
            Assert.That(new SkinTransfer(_body, new Transform[0]).Refresh(), Is.EqualTo(0));
            Assert.That(new SkinTransfer(null, new[] { _foot }).Refresh(), Is.EqualTo(0));
            skin.Dispose();
        }

        [Test]
        public void WeightsAt_TakesTheNearestSkin()
        {
            var skin = Skin();
            skin.Refresh();
            var b = new Transform[4];
            var w = new float[4];
            Assert.That(skin.WeightsAt(new Vector3(0f, 0.05f, 0.21f), b, w), Is.EqualTo(1));
            Assert.That(b[0], Is.SameAs(_toe));
            Assert.That(skin.WeightsAt(new Vector3(0f, 0.0f, -0.05f), b, w), Is.EqualTo(1));
            Assert.That(b[0], Is.SameAs(_foot), "under the heel");
            Assert.That(skin.WeightsAt(new Vector3(0.25f, 0.1f, 0f), b, w), Is.EqualTo(1));
            Assert.That(b[0], Is.SameAs(_foot), "near the other foot: still this leg");
            skin.Dispose();
        }

        [Test]
        public void Refresh_ReadsTheCurrentPose()
        {
            var skin = Skin();
            var b = new Transform[4];
            var w = new float[4];
            var high = new Vector3(0f, 0.2f, 0.15f);                            // above the toe joint: the foot's top is nearest at rest
            skin.Refresh();
            skin.WeightsAt(high, b, w);
            Assert.That(b[0], Is.SameAs(_foot), "rest pose");
            _toe.localRotation = Quaternion.Euler(-90f, 0f, 0f);                // toes point up: the toe skin is now nearest
            skin.WeightsAt(high, b, w);
            Assert.That(b[0], Is.SameAs(_foot), "not refreshed: still the old pose");
            skin.Refresh();
            skin.WeightsAt(high, b, w);
            Assert.That(b[0], Is.SameAs(_toe), "refreshed: the toe skin moved up with the bone");
            skin.Dispose();
        }

        [Test]
        public void SkinPoint_FollowsItsBones()
        {
            var skin = Skin();
            skin.Refresh();
            var at = new Vector3(0f, 0.08f, 0.2f);
            var p = skin.PointAt(at);
            Assert.That(p, Is.Not.Null);
            Near(at, p.Position, 1e-5f, "bound where it was made");
            var before = _toe.localToWorldMatrix;
            _toe.localRotation = Quaternion.Euler(40f, 0f, 0f);
            Near((_toe.localToWorldMatrix * before.inverse).MultiplyPoint3x4(at), p.Position, 1e-5f, "moved with the toe");
            var mixed = new SkinPoint(at, new[] { _foot, null, _toe }, new[] { 0.25f, 0.5f, 0.25f }, 3);
            Assert.That(mixed.BoneCount, Is.EqualTo(2), "a missing bone is dropped");
            skin.Dispose();
        }

        [Test]
        public void GlueFrame_TiltsWithTheToe()
        {
            var skin = Skin();
            skin.Refresh();
            var a = new Vector3(0f, 0.08f, 0.19f);
            var pa = skin.PointAt(a);
            var pb = skin.PointAt(a + Vector3.forward * 0.04f);
            var pc = skin.PointAt(a + Vector3.right * 0.04f);
            Vector3 f, u;
            Assert.That(FitGeometry.FrameAxes(pa.Position, pb.Position, pc.Position, out f, out u), Is.True);
            Near(Vector3.forward, f);
            Near(Vector3.up, u);
            _toe.localRotation = Quaternion.Euler(30f, 0f, 0f);                // toes bend down 30 deg
            FitGeometry.FrameAxes(pa.Position, pb.Position, pc.Position, out f, out u);
            Near(Quaternion.Euler(30f, 0f, 0f) * Vector3.forward, f, 1e-4f, "forward");
            Near(Quaternion.Euler(30f, 0f, 0f) * Vector3.up, u, 1e-4f, "up");
            skin.Dispose();
        }

        [Test]
        public void SlipperWithSkin_BendsWithToeAndAnkle_BindsExactly()
        {
            var skin = Skin();
            skin.Refresh();
            var prefab = Go("SlipperParts");
            var part = Go("SlipperUpper", prefab.transform);
            part.AddComponent<MeshFilter>().sharedMesh = BoxMesh(new Vector3(-0.05f, 0f, -0.15f), new Vector3(0.05f, 0.08f, 0.15f));
            part.AddComponent<MeshRenderer>();
            Transform root;
            var parts = SlipperFit.Build(prefab, _root, _foot, _toe, Vector3.forward, true, 0.05f, 0f, -0.1f, 0f, 1f, 0.8f, skin, out root);
            Track(root.gameObject);
            Assert.That(parts.Count, Is.EqualTo(1));
            Near(new Vector3(0.8f, 1f, 1f), root.localScale, 1e-5f, "width only across the foot");
            var smr = parts[0].Renderer;
            Track(smr.sharedMesh);
            Assert.That(Uses(smr.bones, _toe), Is.True, "toe used");
            Assert.That(Uses(smr.bones, _otherFoot), Is.False, "never the other foot");
            Assert.That(parts[0].ToeWeighted, Is.GreaterThan(0));
            foreach (var bw in smr.sharedMesh.boneWeights) Assert.That(bw.weight0 + bw.weight1 + bw.weight2 + bw.weight3, Is.EqualTo(1f).Within(1e-4f));

            var rest = World(smr);
            var verts = smr.sharedMesh.vertices;
            var toWorld = smr.transform.localToWorldMatrix;
            for (int i = 0; i < verts.Length; i++) Near(toWorld.MultiplyPoint3x4(verts[i]), rest[i], 1e-4f, "bind pose vertex " + i);

            _foot.localRotation = Quaternion.Euler(-25f, 0f, 0f);              // ankle: toes up
            var moved = World(smr);
            int changed = 0;
            for (int i = 0; i < rest.Length; i++) if ((moved[i] - rest[i]).magnitude > 0.005f) changed++;
            Assert.That(changed, Is.EqualTo(rest.Length), "the whole slipper follows the ankle");
            skin.Dispose();
        }

        [Test]
        public void SlipperWithEmptySkin_FallsBackToFootAndToe()
        {
            var skin = new SkinTransfer(null, null);
            skin.Refresh();
            var prefab = Go("SlipperParts");
            var part = Go("SlipperUpper", prefab.transform);
            part.AddComponent<MeshFilter>().sharedMesh = BoxMesh(new Vector3(-0.05f, 0f, -0.15f), new Vector3(0.05f, 0.08f, 0.15f));
            part.AddComponent<MeshRenderer>();
            Transform root;
            var parts = SlipperFit.Build(prefab, _root, _foot, _toe, Vector3.forward, true, 0.05f, 0f, -0.1f, 0f, 1f, 1f, skin, out root);
            Track(root.gameObject);
            Track(parts[0].Renderer.sharedMesh);
            SameBones(new[] { _foot, _toe }, parts[0].Renderer.bones);
        }

        Vector3[] World(SkinnedMeshRenderer smr)
        {
            var baked = Track(new Mesh());
            smr.BakeMesh(baked);
            var v = baked.vertices;
            var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(v[i]);
            return v;
        }
    }
}
