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
        public void MagnitudeSolver_ReachesTargetWithRotatedNonUniformTransform()
        {
            var l2w = Matrix4x4.TRS(
                new Vector3(0.4f, -1.2f, 2.3f),
                Quaternion.Euler(31f, -22f, 47f),
                new Vector3(1.7f, 0.55f, 2.2f));

            var childLocal = new Vector3(0.32f, 0.41f, -0.18f);
            var oldScale = new Vector3(1.15f, 0.82f, 1.27f);
            var x0 = MetricScaleMath.ScaleThreshold;

            Vector3 Predict(float x)
            {
                var nextScale = oldScale;
                nextScale.x = x;
                var nextLocal = MetricScaleMath.TransformChildPosition(
                    childLocal,
                    l2w,
                    oldScale,
                    nextScale);
                return l2w.MultiplyPoint(nextLocal) - l2w.MultiplyPoint(Vector3.zero);
            }

            var desiredScale = 1.63f;
            var target = Predict(desiredScale).magnitude;
            var origin = Predict(x0);
            var direction = Predict(x0 + 1f) - origin;

            var solved = MetricScaleMath.SolveMagnitudeAlongLinearPath(
                origin,
                direction,
                target,
                x0,
                oldScale.x,
                x0);

            Assert.That(float.IsNaN(solved), Is.False);
            Assert.That(Predict(solved).magnitude, Is.EqualTo(target).Within(1e-5f));
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
