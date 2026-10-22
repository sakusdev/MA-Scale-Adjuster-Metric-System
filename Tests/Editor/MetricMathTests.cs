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

        [Test]
        public void UnitConversion_MetricAndImperial_RoundTripsAccurately()
        {
            Assert.That(MetricScaleMath.UnitToMetres(100f, LengthUnit.Centimetres), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(MetricScaleMath.UnitToMetres(1000f, LengthUnit.Millimetres), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(MetricScaleMath.UnitToMetres(1f, LengthUnit.Inches), Is.EqualTo(0.0254f).Within(1e-6f));
            Assert.That(MetricScaleMath.UnitToMetres(1f, LengthUnit.Feet), Is.EqualTo(0.3048f).Within(1e-6f));
            Assert.That(MetricScaleMath.UnitToMetres(1f, LengthUnit.Yards), Is.EqualTo(0.9144f).Within(1e-6f));

            var metres = MetricScaleMath.FeetAndInchesToMetres(1, 6.5f);
            Assert.That(metres, Is.EqualTo(18.5f * 0.0254f).Within(1e-6f));

            MetricScaleMath.MetresToFeetAndInches(metres, out var feet, out var inches);
            Assert.That(feet, Is.EqualTo(1));
            Assert.That(inches, Is.EqualTo(6.5f).Within(1e-5f));
        }

        [Test]
        public void BuildTargetScale_RespectsCrossSectionMode()
        {
            var oldScale = new Vector3(1f, 2f, 1.5f);

            var preserve = MetricScaleMath.BuildTargetScale(oldScale, 1, 4f, CrossSectionMode.Preserve);
            Assert.That(preserve.x, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(preserve.y, Is.EqualTo(4f).Within(1e-6f));
            Assert.That(preserve.z, Is.EqualTo(1.5f).Within(1e-6f));

            var proportional = MetricScaleMath.BuildTargetScale(oldScale, 1, 4f, CrossSectionMode.Proportional);
            Assert.That(proportional.x, Is.EqualTo(2f).Within(1e-6f));
            Assert.That(proportional.y, Is.EqualTo(4f).Within(1e-6f));
            Assert.That(proportional.z, Is.EqualTo(3f).Within(1e-6f));

            var volume = MetricScaleMath.BuildTargetScale(oldScale, 1, 8f, CrossSectionMode.MaintainVolume);
            Assert.That(volume.x, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(volume.y, Is.EqualTo(8f).Within(1e-6f));
            Assert.That(volume.z, Is.EqualTo(0.75f).Within(1e-6f));
        }

        [Test]
        public void CalculateBoneLengthForHeightDelta_HandlesTiltAndDirect()
        {
            var direct = MetricScaleMath.CalculateBoneLengthForHeightDelta(0.4f, -0.3f, 0.05f, compensateBoneTilt: false);
            Assert.That(direct, Is.EqualTo(0.45f).Within(1e-6f));

            var tilted = MetricScaleMath.CalculateBoneLengthForHeightDelta(0.4f, -0.2f, 0.05f, compensateBoneTilt: true);
            Assert.That(tilted, Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void TryGetMirroredBoneName_DetectsCommonNamingConventions()
        {
            Assert.That(MetricScaleMath.TryGetMirroredBoneName("UpperLeg_Left", out var m1), Is.True);
            Assert.That(m1, Is.EqualTo("UpperLeg_Right"));

            Assert.That(MetricScaleMath.TryGetMirroredBoneName("Leg.L", out var m2), Is.True);
            Assert.That(m2, Is.EqualTo("Leg.R"));

            Assert.That(MetricScaleMath.TryGetMirroredBoneName("L_Arm", out var m3), Is.True);
            Assert.That(m3, Is.EqualTo("R_Arm"));

            Assert.That(MetricScaleMath.TryGetMirroredBoneName("Spine", out _), Is.False);
        }
    }
}
