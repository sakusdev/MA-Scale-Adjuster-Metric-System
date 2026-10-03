using NUnit.Framework;
using UnityEngine;

namespace SakusDev.MAScaleAdjusterMetricSystem.Tests
{
    public class MetricMathTests
    {
        [Test]
        public void UnityUnits_AreMetres()
        {
            var parent = new GameObject("parent");
            var child = new GameObject("child");
            try
            {
                child.transform.SetParent(parent.transform, false);
                child.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                Assert.That(
                    Vector3.Distance(parent.transform.position, child.transform.position),
                    Is.EqualTo(0.5f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void ChildTransform_SameScale_IsIdentity()
        {
            var l2w = Matrix4x4.TRS(
                new Vector3(1.2f, -0.7f, 3.1f),
                Quaternion.Euler(23f, 41f, -17f),
                new Vector3(1.3f, 0.6f, 2.1f));
            var position = new Vector3(0.2f, 0.5f, -0.1f);
            var scale = new Vector3(1.4f, 0.8f, 1.1f);

            var actual = MetricScaleMath.TransformChildPosition(position, l2w, scale, scale);

            Assert.That(actual.x, Is.EqualTo(position.x).Within(1e-5f));
            Assert.That(actual.y, Is.EqualTo(position.y).Within(1e-5f));
            Assert.That(actual.z, Is.EqualTo(position.z).Within(1e-5f));
        }

        [Test]
        public void ChildTransform_IdentitySpace_UsesScaleRatio()
        {
            var position = new Vector3(0.5f, 0.25f, 0.1f);

            var actual = MetricScaleMath.TransformChildPosition(
                position,
                Matrix4x4.identity,
                Vector3.one,
                new Vector3(2f, 3f, 4f));

            Assert.That(actual.x, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(actual.y, Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(actual.z, Is.EqualTo(0.4f).Within(1e-6f));
        }

        [Test]
        public void ChildTransform_ClampsZeroLikeModularAvatar()
        {
            var position = new Vector3(1f, 0f, 0f);

            var actual = MetricScaleMath.TransformChildPosition(
                position,
                Matrix4x4.identity,
                Vector3.zero,
                Vector3.zero);

            Assert.That(actual.x, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void MagnitudeSolver_FindsThreeFourFiveTriangle()
        {
            var solved = MetricScaleMath.SolveMagnitudeAlongLinearPath(
                new Vector3(0f, 3f, 0f),
                new Vector3(4f, 0f, 0f),
                5f,
                0f,
                0.9f,
                0f);

            Assert.That(solved, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void MagnitudeSolver_ReturnsNaNWhenTargetIsUnreachable()
        {
            var solved = MetricScaleMath.SolveMagnitudeAlongLinearPath(
                new Vector3(0f, 3f, 0f),
                new Vector3(4f, 0f, 0f),
                2f,
                0f,
                1f,
                0f);

            Assert.That(float.IsNaN(solved), Is.True);
        }

        [Test]
        public void ProjectionSolver_ChoosesNearestValidRoot()
        {
            var solved = MetricScaleMath.SolveAbsoluteProjectionAlongLinearPath(
                0f,
                2f,
                4f,
                0f,
                1.5f,
                0f);

            Assert.That(solved, Is.EqualTo(2f).Within(1e-6f));
        }
    }
}
