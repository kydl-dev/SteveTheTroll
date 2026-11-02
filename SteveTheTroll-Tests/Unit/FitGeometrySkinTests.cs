using System.Linq;
using System;
using FsCheck;
using UnityEngine;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>
    /// FitGeometry skin transfer + glue frame (TEST-007): the slipper copies the player's own weights from the nearest foot skin vertices
    /// (NearestK, BlendWeights, StrongestBone) and the troll follows a frame of three skinned points (FrameAxes).
    /// Expected values from the documented rules (XML docs of FitGeometry), not from the formulas.
    /// </summary>
    [TestFixture]
    public class FitGeometrySkinTests
    {
        const string T = "SteveTheTroll.Fit.FitGeometry";
        const float Eps = 1e-4f;

        static object C(string m, params object[] a) { return ModAssembly.Call(T, m, a); }
        static float Const(string name) { return (float)ModAssembly.GetStatic(T, name); }
        static BoneWeight W(int b0, float w0, int b1 = 0, float w1 = 0f, int b2 = 0, float w2 = 0f, int b3 = 0, float w3 = 0f)
        {
            return new BoneWeight { boneIndex0 = b0, weight0 = w0, boneIndex1 = b1, weight1 = w1, boneIndex2 = b2, weight2 = w2, boneIndex3 = b3, weight3 = w3 };
        }

        // ---------------- NearestK ----------------

        static int Nearest(Vector3 p, Vector3[] pts, int k, out int[] idx, out float[] dist)
        {
            idx = new int[Math.Max(k, 1)];
            dist = new float[Math.Max(k, 1)];
            return (int)C("NearestK", p, pts, k, idx, dist);
        }

        [Test]
        public void NearestK_ClosestFirst_WithDistances()
        {
            var pts = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0.2f, 0f, 0f), new Vector3(5f, 0f, 0f) };
            int[] idx; float[] dist;
            Assert.That(Nearest(new Vector3(0.05f, 0f, 0f), pts, 2, out idx, out dist), Is.EqualTo(2));
            Assert.That(idx, Is.EqualTo(new[] { 0, 2 }));
            Assert.That(dist[0], Is.EqualTo(0.05f).Within(Eps));
            Assert.That(dist[1], Is.EqualTo(0.15f).Within(Eps));
        }

        [Test]
        public void NearestK_FarthestFirstInTheArray_StillSorted()
        {
            var pts = new[] { new Vector3(3f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 0f) };
            int[] idx; float[] dist;
            Assert.That(Nearest(Vector3.zero, pts, 3, out idx, out dist), Is.EqualTo(3));
            Assert.That(idx, Is.EqualTo(new[] { 3, 2, 1 }));
            Assert.That(dist, Is.EqualTo(new[] { 0f, 1f, 2f }).Within(Eps));
        }

        [Test]
        public void NearestK_EqualDistances_EarlierPointFirst()
        {
            var pts = new[] { new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f), new Vector3(0f, 1f, 0f) };
            int[] idx; float[] dist;
            Nearest(Vector3.zero, pts, 1, out idx, out dist);
            Assert.That(idx[0], Is.EqualTo(0), "k = 1");
            Nearest(Vector3.zero, pts, 2, out idx, out dist);
            Assert.That(idx, Is.EqualTo(new[] { 0, 1 }), "k = 2");
        }

        [Test]
        public void NearestK_FewerPointsThanK_ReturnsAllOfThem()
        {
            int[] idx; float[] dist;
            Assert.That(Nearest(Vector3.zero, new[] { Vector3.up * 2f, Vector3.up }, 4, out idx, out dist), Is.EqualTo(2));
            Assert.That(idx.Take(2), Is.EqualTo(new[] { 1, 0 }));
        }

        [Test]
        public void NearestK_NothingToSearch_ReturnsZero()
        {
            int[] idx; float[] dist;
            Assert.That(Nearest(Vector3.zero, null, 4, out idx, out dist), Is.EqualTo(0));
            Assert.That(Nearest(Vector3.zero, new Vector3[0], 4, out idx, out dist), Is.EqualTo(0));
            Assert.That(Nearest(Vector3.zero, new[] { Vector3.one }, 0, out idx, out dist), Is.EqualTo(0));
        }

        [Test]
        public void NearestK_EqualsSortingAllDistances()
        {
            var vec = from x in Props.GameFloat(1f) from y in Props.GameFloat(1f) from z in Props.GameFloat(1f) select new Vector3(x, y, z);
            var gen = from count in Gen.Choose(1, 40) from pts in Gen.ArrayOf(count, vec)
                      from p in vec from k in Gen.Choose(1, 6) select Tuple.Create(pts, p, k);
            Props.Check(Prop.ForAll(gen.ToArbitrary(), q =>
            {
                int[] idx; float[] dist;
                int n = Nearest(q.Item2, q.Item1, q.Item3, out idx, out dist);
                var want = q.Item1.Select(v => (v - q.Item2).magnitude).OrderBy(d => d).Take(q.Item3).ToArray();
                if (n != want.Length) return false;
                for (int i = 0; i < n; i++)
                    if (Math.Abs(dist[i] - want[i]) > 1e-4f || Math.Abs((q.Item1[idx[i]] - q.Item2).magnitude - dist[i]) > 1e-4f) return false;
                return true;
            }));
        }

        // ---------------- BlendWeights ----------------

        static int Blend(BoneWeight[] src, int[] idx, float[] dist, int count, int slots, out int[] bones, out float[] weights)
        {
            bones = new int[slots];
            weights = new float[slots];
            return (int)C("BlendWeights", src, idx, dist, count, bones, weights);
        }

        [Test]
        public void BlendWeights_OneVertex_CopiesItsWeights()
        {
            int[] b; float[] w;
            Assert.That(Blend(new[] { W(3, 0.7f, 5, 0.3f) }, new[] { 0 }, new[] { 0.01f }, 1, 4, out b, out w), Is.EqualTo(2));
            Assert.That(b.Take(2), Is.EqualTo(new[] { 3, 5 }));
            Assert.That(w[0], Is.EqualTo(0.7f).Within(Eps));
            Assert.That(w[1], Is.EqualTo(0.3f).Within(Eps));
        }

        [Test]
        public void BlendWeights_CloserVertexCountsMore_InverseDistancePlusEps()
        {
            Assert.That(Const("SkinBlendEps"), Is.EqualTo(0.002f));
            int[] b; float[] w;
            var src = new[] { W(9, 1f), W(1, 1f), W(2, 1f) };
            Assert.That(Blend(src, new[] { 1, 2 }, new[] { 0f, 0.018f }, 2, 4, out b, out w), Is.EqualTo(2));
            Assert.That(b.Take(2), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(w[0], Is.EqualTo(500f / 550f).Within(Eps));          // 1 / 0.002 vs 1 / 0.02
            Assert.That(w[1], Is.EqualTo(50f / 550f).Within(Eps));
        }

        [Test]
        public void BlendWeights_SameBoneFromSeveralVertices_IsSummed()
        {
            int[] b; float[] w;
            Assert.That(Blend(new[] { W(1, 0.5f, 2, 0.5f), W(1, 1f) }, new[] { 0, 1 }, new[] { 0f, 0f }, 2, 4, out b, out w), Is.EqualTo(2));
            Assert.That(b.Take(2), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(w[0], Is.EqualTo(0.75f).Within(Eps));
            Assert.That(w[1], Is.EqualTo(0.25f).Within(Eps));
        }

        [Test]
        public void BlendWeights_KeepsTheFourStrongest_OrFewerSlots_Renormalised()
        {
            var src = new[] { W(1, 0.4f, 2, 0.3f, 3, 0.2f, 4, 0.1f), W(5, 0.05f, 6, 0.95f) };
            int[] b; float[] w;
            Assert.That(Blend(src, new[] { 0, 1 }, new[] { 0f, 0f }, 2, 6, out b, out w), Is.EqualTo(4));
            Assert.That(b.Take(4), Is.EqualTo(new[] { 6, 1, 2, 3 }));
            Assert.That(w.Take(4).Sum(), Is.EqualTo(1f).Within(Eps));
            Assert.That(w[0], Is.EqualTo(0.95f / 1.85f).Within(Eps));
            Assert.That(w[4], Is.EqualTo(0f), "never more than 4 bones");
            Assert.That(Blend(src, new[] { 0, 1 }, new[] { 0f, 0f }, 2, 2, out b, out w), Is.EqualTo(2));
            Assert.That(b, Is.EqualTo(new[] { 6, 1 }));
            Assert.That(w[0] + w[1], Is.EqualTo(1f).Within(Eps));
        }

        [Test]
        public void BlendWeights_AllFourSlotsOfAVertexCount()
        {
            int[] b; float[] w;
            Assert.That(Blend(new[] { W(1, 0.25f, 2, 0.25f, 3, 0.25f, 4, 0.25f) }, new[] { 0 }, new[] { 0.01f }, 1, 4, out b, out w), Is.EqualTo(4));
            Assert.That(b, Is.EqualTo(new[] { 1, 2, 3, 4 }), "equal weights: in the order met");
            Assert.That(w, Is.EqualTo(new[] { 0.25f, 0.25f, 0.25f, 0.25f }).Within(Eps));
        }

        [Test]
        public void BlendWeights_BoneZeroIsABone()
        {
            int[] b; float[] w;
            Assert.That(Blend(new[] { W(0, 1f) }, new[] { 0 }, new[] { 0f }, 1, 4, out b, out w), Is.EqualTo(1));
            Assert.That(b[0], Is.EqualTo(0));
            Assert.That(w[0], Is.EqualTo(1f).Within(Eps));
            Assert.That(Blend(new[] { W(2, 0.5f, 0, 0.5f) }, new[] { 0 }, new[] { 0f }, 1, 4, out b, out w), Is.EqualTo(2));
            Assert.That(b.Take(2), Is.EqualTo(new[] { 2, 0 }));
        }

        [Test]
        public void BlendWeights_ZeroWeightsAndNoVertices_GiveNothing()
        {
            int[] b; float[] w;
            Assert.That(Blend(new[] { W(1, 0f) }, new[] { 0 }, new[] { 0f }, 1, 4, out b, out w), Is.EqualTo(0));
            Assert.That(Blend(new[] { W(1, 1f) }, new int[0], new float[0], 0, 4, out b, out w), Is.EqualTo(0));
            Assert.That(Blend(new[] { W(1, 0.6f, 7, 0f) }, new[] { 0 }, new[] { 0f }, 1, 4, out b, out w), Is.EqualTo(1), "a 0 weight is no bone");
        }

        [Test]
        public void BlendWeights_AlwaysSumTo1_StrongestFirst_AtMost4()
        {
            var bw = from b0 in Gen.Choose(0, 6) from b1 in Gen.Choose(0, 6) from b2 in Gen.Choose(0, 6) from w0 in Gen.Choose(1, 100) from w1 in Gen.Choose(0, 100) from w2 in Gen.Choose(0, 100)
                     select W(b0, w0, b1, w1, b2, w2);
            var gen = from src in Gen.ArrayOf(4, bw) from d in Gen.ArrayOf(4, Gen.Choose(0, 100).Select(i => i / 1000f)) from n in Gen.Choose(1, 4) select Tuple.Create(src, d, n);
            Props.Check(Prop.ForAll(gen.ToArbitrary(), q =>
            {
                int[] b; float[] w;
                int n = Blend(q.Item1, new[] { 0, 1, 2, 3 }, q.Item2, q.Item3, 4, out b, out w);
                if (n < 1 || n > 4 || Math.Abs(w.Take(n).Sum() - 1f) > 1e-4f) return false;
                for (int i = 1; i < n; i++) if (w[i] > w[i - 1] + 1e-6f || b[i] == b[i - 1]) return false;
                return true;
            }));
        }

        // ---------------- StrongestBone ----------------

        [TestCase(1, 0.5f, 2, 0.3f, 3, 0.1f, 4, 0.1f, 1)]
        [TestCase(1, 0.2f, 2, 0.5f, 3, 0.2f, 4, 0.1f, 2)]
        [TestCase(1, 0.2f, 2, 0.1f, 3, 0.6f, 4, 0.1f, 3)]
        [TestCase(1, 0.1f, 2, 0.2f, 3, 0.3f, 4, 0.4f, 4)]
        [TestCase(1, 0.4f, 2, 0.4f, 3, 0.1f, 4, 0.1f, 1)]
        [TestCase(1, 0.1f, 2, 0.3f, 3, 0.3f, 4, 0.3f, 2)]
        public void StrongestBone_LargestWeight_FirstOnATie(int b0, float w0, int b1, float w1, int b2, float w2, int b3, float w3, int want)
        {
            Assert.That((int)C("StrongestBone", W(b0, w0, b1, w1, b2, w2, b3, w3)), Is.EqualTo(want));
        }

        // ---------------- FrameAxes ----------------

        static bool Frame(Vector3 a, Vector3 b, Vector3 c, out Vector3 f, out Vector3 u)
        {
            var args = new object[] { a, b, c, null, null };
            bool ok = (bool)C("FrameAxes", args);
            f = (Vector3)args[3];
            u = (Vector3)args[4];
            return ok;
        }

        [Test]
        public void FrameAxes_AheadAndRight_GiveForwardAndUp()
        {
            Vector3 f, u;
            Assert.That(Frame(new Vector3(1f, 2f, 3f), new Vector3(1f, 2f, 3.5f), new Vector3(1.2f, 2f, 3f), out f, out u), Is.True);
            Assert.That((f - Vector3.forward).magnitude, Is.LessThan(Eps));
            Assert.That((u - Vector3.up).magnitude, Is.LessThan(Eps));
        }

        [Test]
        public void FrameAxes_TiltedSlipper_UpTiltsWithIt()
        {
            Vector3 f, u;
            Assert.That(Frame(Vector3.zero, new Vector3(0f, -1f, 1f), new Vector3(1f, 0f, 0f), out f, out u), Is.True);   // toe pitched down 45 deg
            Assert.That((f - new Vector3(0f, -1f, 1f).normalized).magnitude, Is.LessThan(Eps));
            Assert.That((u - new Vector3(0f, 1f, 1f).normalized).magnitude, Is.LessThan(Eps));
        }

        [Test]
        public void FrameAxes_PointsInALine_FalseAndDefaults()
        {
            Vector3 f, u;
            Assert.That(Frame(Vector3.zero, Vector3.forward, Vector3.forward * 2f, out f, out u), Is.False);
            Assert.That(f, Is.EqualTo(Vector3.forward));
            Assert.That(u, Is.EqualTo(Vector3.up));
            Assert.That(Frame(Vector3.one, Vector3.one, Vector3.zero, out f, out u), Is.False, "b on a");
        }

        [Test]
        public void FrameAxes_UnitAndPerpendicular_UpAtRightAnglesToTheRightPoint()
        {
            var vec = from x in Props.GameFloat(2f) from y in Props.GameFloat(2f) from z in Props.GameFloat(2f) select new Vector3(x, y, z);
            var gen = from a in vec from b in vec from c in vec select new[] { a, b, c };
            Props.Check(Prop.ForAll(gen.ToArbitrary(), p =>
            {
                Vector3 f, u;
                if (Vector3.Cross(p[1] - p[0], p[2] - p[0]).magnitude < 1e-3f) return true;           // (nearly) one line: covered above
                if (!Frame(p[0], p[1], p[2], out f, out u)) return false;
                return Math.Abs(f.magnitude - 1f) < 1e-3f && Math.Abs(u.magnitude - 1f) < 1e-3f && Math.Abs(Vector3.Dot(f, u)) < 1e-3f
                    && Math.Abs(Vector3.Dot(u, (p[2] - p[0]).normalized)) < 1e-3f;
            }));
        }
    }
}
