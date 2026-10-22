using UnityEngine;

namespace SakusDev.MAScaleAdjusterMetricSystem
{
    internal enum LengthUnit
    {
        Metres,
        Centimetres,
        Millimetres,
        Inches,
        Feet,
        FeetAndInches,
        Yards
    }

    internal static class MetricScaleMath
    {
        internal const float ScaleThreshold = 1f / (1 << 14);
        internal const float MetresPerCentimetre = 0.01f;
        internal const float MetresPerMillimetre = 0.001f;
        internal const float MetresPerInch = 0.0254f;
        internal const float InchesPerFoot = 12f;
        internal const float MetresPerFoot = MetresPerInch * InchesPerFoot;
        internal const float MetresPerYard = MetresPerFoot * 3f;

        internal static float GetMetresPerUnit(LengthUnit unit)
        {
            switch (unit)
            {
                case LengthUnit.Metres: return 1f;
                case LengthUnit.Centimetres: return MetresPerCentimetre;
                case LengthUnit.Millimetres: return MetresPerMillimetre;
                case LengthUnit.Inches: return MetresPerInch;
                case LengthUnit.Feet: return MetresPerFoot;
                case LengthUnit.FeetAndInches: return MetresPerInch;
                case LengthUnit.Yards: return MetresPerYard;
                default: return 1f;
            }
        }

        internal static string GetUnitSymbol(LengthUnit unit)
        {
            switch (unit)
            {
                case LengthUnit.Metres: return "m";
                case LengthUnit.Centimetres: return "cm";
                case LengthUnit.Millimetres: return "mm";
                case LengthUnit.Inches: return "in";
                case LengthUnit.Feet: return "ft";
                case LengthUnit.FeetAndInches: return "ft & in";
                case LengthUnit.Yards: return "yd";
                default: return "m";
            }
        }

        internal static float UnitToMetres(float value, LengthUnit unit)
        {
            return unit == LengthUnit.Metres ? value : value * GetMetresPerUnit(unit);
        }

        internal static float MetresToUnit(float metres, LengthUnit unit)
        {
            return unit == LengthUnit.Metres ? metres : metres / GetMetresPerUnit(unit);
        }

        internal static float FeetAndInchesToMetres(int feet, float inches)
        {
            return (feet * InchesPerFoot + inches) * MetresPerInch;
        }

        internal static void MetresToFeetAndInches(float metres, out int feet, out float inches)
        {
            if (!IsFinite(metres))
            {
                feet = 0;
                inches = float.NaN;
                return;
            }

            var totalInches = metres / MetresPerInch;
            var sign = totalInches < 0f ? -1 : 1;
            var absInches = Mathf.Abs(totalInches);
            var wholeFeet = Mathf.FloorToInt(absInches / InchesPerFoot);
            var remainingInches = absInches - wholeFeet * InchesPerFoot;

            if (InchesPerFoot - remainingInches <= 1e-4f)
            {
                wholeFeet += 1;
                remainingInches = 0f;
            }

            feet = sign * wholeFeet;
            inches = sign * remainingInches;
        }

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
