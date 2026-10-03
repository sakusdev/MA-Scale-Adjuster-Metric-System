using UnityEngine;

namespace SakusDev.MAScaleAdjusterMetricSystem
{
    internal static class MetricScaleMath
    {
        internal const float ScaleThreshold = 1f / (1 << 14);

        internal static Vector3 TransformChildPosition(
            Vector3 childLocalPosition,
            Matrix4x4 targetLocalToWorld,
            Vector3 oldScale,
            Vector3 newScale)
        {
            var clampedOld = ClampScale(oldScale);
            var clampedNew = ClampScale(newScale);

            var baseToScaleCoord =
                (targetLocalToWorld * Matrix4x4.Scale(clampedOld)).inverse
                * targetLocalToWorld;
            var updateTransform = Matrix4x4.Scale(clampedNew) * baseToScaleCoord;

            return updateTransform.MultiplyPoint(childLocalPosition);
        }

        internal static Vector3 ClampScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Max(ScaleThreshold, scale.x),
                Mathf.Max(ScaleThreshold, scale.y),
                Mathf.Max(ScaleThreshold, scale.z));
        }

        internal static float SolveMagnitudeAlongLinearPath(
            Vector3 origin,
            Vector3 direction,
            float targetMagnitude,
            float parameterOffset,
            float currentParameter,
            float minimumParameter)
        {
            var a = Vector3.Dot(direction, direction);
            if (a <= 1e-14f) return float.NaN;

            var b = 2f * Vector3.Dot(origin, direction);
            var c = Vector3.Dot(origin, origin) - targetMagnitude * targetMagnitude;
            var discriminant = b * b - 4f * a * c;

            var tolerance = 1e-6f * Mathf.Max(1f, b * b + Mathf.Abs(4f * a * c));
            if (discriminant < -tolerance) return float.NaN;
            discriminant = Mathf.Max(0f, discriminant);

            var sqrt = Mathf.Sqrt(discriminant);
            var rootA = parameterOffset + (-b + sqrt) / (2f * a);
            var rootB = parameterOffset + (-b - sqrt) / (2f * a);

            return PickNearestValid(rootA, rootB, currentParameter, minimumParameter);
        }

        internal static float SolveAbsoluteProjectionAlongLinearPath(
            float originProjection,
            float directionProjection,
            float targetMagnitude,
            float parameterOffset,
            float currentParameter,
            float minimumParameter)
        {
            if (Mathf.Abs(directionProjection) <= 1e-7f) return float.NaN;

            var rootA = parameterOffset + (targetMagnitude - originProjection) / directionProjection;
            var rootB = parameterOffset + (-targetMagnitude - originProjection) / directionProjection;
            return PickNearestValid(rootA, rootB, currentParameter, minimumParameter);
        }

        internal static float PickNearestValid(
            float a,
            float b,
            float current,
            float minimum = float.NegativeInfinity)
        {
            var aValid = IsFinite(a) && a >= minimum;
            var bValid = IsFinite(b) && b >= minimum;

            if (!aValid && !bValid) return float.NaN;
            if (!aValid) return b;
            if (!bValid) return a;

            return Mathf.Abs(a - current) <= Mathf.Abs(b - current) ? a : b;
        }

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
