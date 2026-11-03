using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SteveTheTroll.Fit.Tests
{
    /// <summary>
    /// Unity EditMode tests (real engine: Quaternion, Transform, Mesh, SkinnedMeshRenderer.BakeMesh) for the fit code the mod and the fit
    /// scene share. The pure maths (FitGeometry) is tested in plain .NET with coverage + Stryker (SteveTheTroll-Mod/tests/Unit); these tests
    /// check the engine-side wrappers and that FitGeometry's managed formulas equal the engine's own (Quaternion.AngleAxis, LookRotation).
    /// Run: Unity Test Runner (EditMode) or the Unity MCP run_tests tool. Every object a test makes is destroyed in TearDown.
    /// </summary>
    public abstract class EngineTestBase
    {
        readonly List<Object> _made = new List<Object>();

        protected T Track<T>(T o) where T : Object { _made.Add(o); return o; }

        protected GameObject Go(string name, Transform parent = null)
        {
            var go = Track(new GameObject(name));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        /// The built-in 1 x 1 x 1 cube mesh as an editable copy (24 vertices, centred).
        protected Mesh CubeMesh()
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = Track(Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh));
            Object.DestroyImmediate(tmp);
            return mesh;
        }

        /// A box mesh spanning min..max (the cube's vertices moved), bounds recalculated.
        protected Mesh BoxMesh(Vector3 min, Vector3 max)
        {
            var mesh = CubeMesh();
            var v = mesh.vertices;
            for (int i = 0; i < v.Length; i++)
                v[i] = new Vector3(v[i].x < 0 ? min.x : max.x, v[i].y < 0 ? min.y : max.y, v[i].z < 0 ? min.z : max.z);
            mesh.vertices = v;
            mesh.RecalculateBounds();
            return mesh;
        }

        [TearDown]
        public void DestroyMade()
        {
            for (int i = _made.Count - 1; i >= 0; i--) if (_made[i] != null) Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }

        protected static void Near(Vector3 expected, Vector3 actual, float eps = 1e-4f, string what = "")
        {
            Assert.That((expected - actual).magnitude, Is.LessThan(eps), what + " expected " + expected.ToString("F4") + ", got " + actual.ToString("F4"));
        }

        /// Same Transform objects in the same order (NUnit's EqualTo compares Transforms as collections of their children - too weak).
        protected static void SameBones(Transform[] expected, Transform[] actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), "bone count");
            for (int i = 0; i < expected.Length; i++) Assert.That(ReferenceEquals(expected[i], actual[i]), Is.True, "bone " + i + ": expected " + (expected[i] != null ? expected[i].name : "null") + ", got " + (actual[i] != null ? actual[i].name : "null"));
        }

        protected static bool Uses(Transform[] bones, Transform bone) { return System.Array.Exists(bones, b => ReferenceEquals(b, bone)); }

        protected static void Near(Quaternion expected, Quaternion actual, float epsDeg = 0.01f, string what = "")
        {
            Assert.That(Quaternion.Angle(expected, actual), Is.LessThan(epsDeg), what + " expected " + expected.eulerAngles.ToString("F2") + ", got " + actual.eulerAngles.ToString("F2"));
        }
    }
}
