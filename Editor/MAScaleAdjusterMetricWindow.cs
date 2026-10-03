using System;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace SakusDev.MAScaleAdjusterMetricSystem
{
    public sealed class MAScaleAdjusterMetricWindow : EditorWindow
    {
        private const float ScaleThreshold = 1f / (1 << 14);

        private enum BoneAxis { X, Y, Z }
        private enum MeasurementMode { FullLength, AxisProjected }

        private ModularAvatarScaleAdjuster _adjuster;
        private Transform _child;
        private BoneAxis _axis = BoneAxis.Y;
        private MeasurementMode _measurementMode = MeasurementMode.FullLength;
        private float _targetMetres = 0.1f;
        private bool _followSelection = true;
        private bool _adjustChildPositions = true;

        [MenuItem("Tools/MA Scale Adjuster Metric System")]
        public static void Open()
        {
            var window = GetWindow<MAScaleAdjusterMetricWindow>();
            window.titleContent = new GUIContent("MA Metric");
            window.minSize = new Vector2(400, 390);
            window.TryUseSelection();
            window.Show();
        }

        [MenuItem("CONTEXT/ModularAvatarScaleAdjuster/Edit in Metric System")]
        private static void OpenFromContext(MenuCommand command)
        {
            var window = GetWindow<MAScaleAdjusterMetricWindow>();
            window.titleContent = new GUIContent("MA Metric");
            window._adjuster = command.context as ModularAvatarScaleAdjuster;
            window.PickDefaultChild();
            window.AutoDetectAxis();
            window.SyncTargetToCurrent();
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            if (_adjuster == null) TryUseSelection();
        }

        private void OnDisable() => Selection.selectionChanged -= OnSelectionChanged;

        private void OnSelectionChanged()
        {
            if (!_followSelection) return;
            TryUseSelection();
            Repaint();
        }

        private void TryUseSelection()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;

            var candidate = go.GetComponent<ModularAvatarScaleAdjuster>();
            if (candidate == null || candidate == _adjuster) return;

            _adjuster = candidate;
            _child = null;
            PickDefaultChild();
            AutoDetectAxis();
            SyncTargetToCurrent();
        }

        private void PickDefaultChild()
        {
            if (_adjuster == null) return;

            var bone = _adjuster.transform;
            if (_child != null && _child.parent == bone) return;

            _child = bone.childCount > 0 ? bone.GetChild(0) : null;
        }

        private void AutoDetectAxis()
        {
            if (_adjuster == null || _child == null) return;

            var delta = _child.localPosition;
            var x = Mathf.Abs(delta.x);
            var y = Mathf.Abs(delta.y);
            var z = Mathf.Abs(delta.z);
            _axis = x >= y && x >= z ? BoneAxis.X : y >= z ? BoneAxis.Y : BoneAxis.Z;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("MA Scale Adjuster Metric System", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Set MA Scale Adjuster from a real bone length in metres", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);

            _followSelection = EditorGUILayout.ToggleLeft("Follow Unity selection", _followSelection);

            EditorGUI.BeginChangeCheck();
            var newAdjuster = (ModularAvatarScaleAdjuster)EditorGUILayout.ObjectField(
                "Scale Adjuster", _adjuster, typeof(ModularAvatarScaleAdjuster), true);
            if (EditorGUI.EndChangeCheck())
            {
                _adjuster = newAdjuster;
                _child = null;
                PickDefaultChild();
                AutoDetectAxis();
                SyncTargetToCurrent();
            }

            if (_adjuster == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a GameObject with MA Scale Adjuster, or assign one above.",
                    MessageType.Info);
                return;
            }

            var bone = _adjuster.transform;
            var children = Enumerable.Range(0, bone.childCount).Select(i => bone.GetChild(i)).ToArray();

            if (children.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "This transform has no direct child to use as a bone endpoint.",
                    MessageType.Warning);
                return;
            }

            var names = children.Select(t => t.name).ToArray();
            var index = Array.IndexOf(children, _child);
            if (index < 0) index = 0;

            EditorGUI.BeginChangeCheck();
            index = EditorGUILayout.Popup("Bone endpoint", index, names);
            if (EditorGUI.EndChangeCheck())
            {
                _child = children[index];
                AutoDetectAxis();
                SyncTargetToCurrent();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Primary axis");
            EditorGUI.BeginChangeCheck();
            _axis = (BoneAxis)EditorGUILayout.EnumPopup(_axis);
            if (GUILayout.Button("Auto", GUILayout.Width(52)))
                AutoDetectAxis();
            var axisChanged = EditorGUI.EndChangeCheck();
            EditorGUILayout.EndHorizontal();
            if (axisChanged) SyncTargetToCurrent();

            EditorGUI.BeginChangeCheck();
            _measurementMode = (MeasurementMode)EditorGUILayout.EnumPopup("Measurement", _measurementMode);
            if (EditorGUI.EndChangeCheck()) SyncTargetToCurrent();

            EditorGUI.BeginChangeCheck();
            _adjustChildPositions = EditorGUILayout.ToggleLeft(
                "Adjust child positions like Modular Avatar", _adjustChildPositions);
            if (EditorGUI.EndChangeCheck()) SyncTargetToCurrent();

            EditorGUILayout.Space(8);

            var currentLength = GetCurrentLengthMetres();
            var currentAxisScale = GetAxis(_adjuster.Scale, _axis);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("Current length (m)", currentLength);
                EditorGUILayout.Vector3Field("MA Scale", _adjuster.Scale);
            }

            EditorGUILayout.Space(4);
            _targetMetres = EditorGUILayout.FloatField("Target length (m)", _targetMetres);

            if (!IsFinite(currentLength))
            {
                EditorGUILayout.HelpBox("The current bone length is not finite.", MessageType.Error);
                return;
            }

            if (!IsFinite(_targetMetres) || _targetMetres < 0f)
            {
                EditorGUILayout.HelpBox(
                    "Target length must be a finite value of zero or greater.",
                    MessageType.Error);
                return;
            }

            var resultingScale = CalculateRequiredAxisScale(_targetMetres);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.FloatField("Resulting axis scale", resultingScale);

            if (!IsFinite(resultingScale))
            {
                EditorGUILayout.HelpBox(
                    "The requested length cannot be reached by changing only the selected MA Scale axis.",
                    MessageType.Warning);
            }

            if (_measurementMode == MeasurementMode.FullLength && !IsNearlyAxisAligned())
            {
                EditorGUILayout.HelpBox(
                    "This bone is not aligned to a single local axis. The solver changes only the selected axis and preserves the other two MA Scale components.",
                    MessageType.Info);
            }

            if (_adjustChildPositions)
            {
                EditorGUILayout.HelpBox(
                    "Child transforms will be repositioned with the same coordinate conversion used by Modular Avatar's Scale Adjuster tool. All direct children are adjusted, not only the measurement endpoint.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Child transforms will not move. In this mode the target describes the virtual Scale Adjuster deformation vector, not the actual parent-to-child transform distance.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(
                       !IsFinite(resultingScale) || Mathf.Approximately(currentAxisScale, resultingScale)))
            {
                if (GUILayout.Button("Apply to MA Scale Adjuster", GUILayout.Height(30)))
                    Apply(resultingScale);
            }

            if (GUILayout.Button("Reset target to current length"))
                SyncTargetToCurrent();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "1 Unity unit is treated as 1 metre. Full Length measures the complete 3D endpoint distance; Axis Projected measures the component along the selected bone-local axis.",
                MessageType.None);
        }

        private float GetCurrentLengthMetres()
        {
            if (_adjuster == null || _child == null) return 0f;

            if (_adjustChildPositions)
                return MeasureActualChildVector(_child.position - _adjuster.transform.position);

            return MeasureVirtualScaleVector(_adjuster.Scale);
        }

        private float MeasureActualChildVector(Vector3 worldVector)
        {
            if (_measurementMode == MeasurementMode.FullLength)
                return worldVector.magnitude;

            var axisWorld = GetBoneAxisWorldVector();
            if (axisWorld.sqrMagnitude <= 1e-14f) return 0f;
            return Mathf.Abs(Vector3.Dot(worldVector, axisWorld.normalized));
        }

        private float MeasureVirtualScaleVector(Vector3 maScale)
        {
            if (_adjuster == null || _child == null) return 0f;

            var local = Vector3.Scale(_child.localPosition, maScale);
            var world = _adjuster.transform.localToWorldMatrix.MultiplyVector(local);

            if (_measurementMode == MeasurementMode.FullLength)
                return world.magnitude;

            var axisWorld = GetBoneAxisWorldVector();
            if (axisWorld.sqrMagnitude <= 1e-14f) return 0f;
            return Mathf.Abs(Vector3.Dot(world, axisWorld.normalized));
        }

        private Vector3 GetBoneAxisWorldVector()
        {
            if (_adjuster == null) return Vector3.zero;

            var local = _axis == BoneAxis.X
                ? Vector3.right
                : _axis == BoneAxis.Y
                    ? Vector3.up
                    : Vector3.forward;

            return _adjuster.transform.localToWorldMatrix.MultiplyVector(local);
        }

        private float CalculateRequiredAxisScale(float target)
        {
            return _adjustChildPositions
                ? CalculateScaleWithChildAdjustment(target)
                : CalculateScaleWithoutChildAdjustment(target);
        }

        private float CalculateScaleWithChildAdjustment(float target)
        {
            if (_adjuster == null || _child == null) return float.NaN;

            var currentScale = GetAxis(_adjuster.Scale, _axis);

            // MA's own child-adjustment logic clamps effective scale components to a
            // small positive value before converting child coordinates. We solve in
            // the same positive domain so applying the result is deterministic.
            var x0 = ScaleThreshold;
            var x1 = ScaleThreshold + 1f;

            var v0 = PredictAdjustedChildWorldVector(x0);
            var v1 = PredictAdjustedChildWorldVector(x1);
            var direction = v1 - v0;

            if (_measurementMode == MeasurementMode.AxisProjected)
            {
                var axisWorld = GetBoneAxisWorldVector();
                if (axisWorld.sqrMagnitude <= 1e-14f) return float.NaN;
                axisWorld.Normalize();

                var a0 = Vector3.Dot(v0, axisWorld);
                var ad = Vector3.Dot(direction, axisWorld);
                if (Mathf.Abs(ad) <= 1e-7f) return float.NaN;

                var candidateA = x0 + (target - a0) / ad;
                var candidateB = x0 + (-target - a0) / ad;
                return PickValidNearest(candidateA, candidateB, currentScale);
            }

            // v(x) = v0 + direction * (x - x0)
            // Solve |v(x)|^2 = target^2 exactly.
            var a = Vector3.Dot(direction, direction);
            if (a <= 1e-14f) return float.NaN;

            var b = 2f * Vector3.Dot(v0, direction);
            var c = Vector3.Dot(v0, v0) - Square(target);
            var discriminant = b * b - 4f * a * c;

            var tolerance = 1e-6f * Mathf.Max(1f, b * b + Mathf.Abs(4f * a * c));
            if (discriminant < -tolerance) return float.NaN;
            discriminant = Mathf.Max(0f, discriminant);

            var sqrt = Mathf.Sqrt(discriminant);
            var yA = (-b + sqrt) / (2f * a);
            var yB = (-b - sqrt) / (2f * a);
            return PickValidNearest(x0 + yA, x0 + yB, currentScale);
        }

        private float CalculateScaleWithoutChildAdjustment(float target)
        {
            if (_adjuster == null || _child == null) return float.NaN;

            var currentScale = GetAxis(_adjuster.Scale, _axis);
            var scale0 = _adjuster.Scale;
            var scale1 = _adjuster.Scale;
            SetAxis(ref scale0, _axis, 0f);
            SetAxis(ref scale1, _axis, 1f);

            var v0 = GetVirtualWorldVector(scale0);
            var direction = GetVirtualWorldVector(scale1) - v0;

            if (_measurementMode == MeasurementMode.AxisProjected)
            {
                var axisWorld = GetBoneAxisWorldVector();
                if (axisWorld.sqrMagnitude <= 1e-14f) return float.NaN;
                axisWorld.Normalize();

                var a0 = Vector3.Dot(v0, axisWorld);
                var ad = Vector3.Dot(direction, axisWorld);
                if (Mathf.Abs(ad) <= 1e-7f) return float.NaN;

                var candidateA = (target - a0) / ad;
                var candidateB = (-target - a0) / ad;
                return PickNearest(candidateA, candidateB, currentScale);
            }

            var a = Vector3.Dot(direction, direction);
            if (a <= 1e-14f) return float.NaN;

            var b = 2f * Vector3.Dot(v0, direction);
            var c = Vector3.Dot(v0, v0) - Square(target);
            var discriminant = b * b - 4f * a * c;

            var tolerance = 1e-6f * Mathf.Max(1f, b * b + Mathf.Abs(4f * a * c));
            if (discriminant < -tolerance) return float.NaN;
            discriminant = Mathf.Max(0f, discriminant);

            var sqrt = Mathf.Sqrt(discriminant);
            var rootA = (-b + sqrt) / (2f * a);
            var rootB = (-b - sqrt) / (2f * a);
            return PickNearest(rootA, rootB, currentScale);
        }

        private Vector3 PredictAdjustedChildWorldVector(float selectedAxisScale)
        {
            var nextScale = _adjuster.Scale;
            SetAxis(ref nextScale, _axis, selectedAxisScale);

            var nextLocalPosition = TransformChildPosition(
                _child.localPosition,
                _adjuster.transform.localToWorldMatrix,
                _adjuster.Scale,
                nextScale);

            var nextWorldPosition = _adjuster.transform.localToWorldMatrix.MultiplyPoint(nextLocalPosition);
            return nextWorldPosition - _adjuster.transform.position;
        }

        private Vector3 GetVirtualWorldVector(Vector3 scale)
        {
            var scaledLocal = Vector3.Scale(_child.localPosition, scale);
            return _adjuster.transform.localToWorldMatrix.MultiplyVector(scaledLocal);
        }

        private void SyncTargetToCurrent()
        {
            if (_adjuster == null || _child == null) return;
            _targetMetres = GetCurrentLengthMetres();
            Repaint();
        }

        private void Apply(float selectedAxisScale)
        {
            if (!IsFinite(selectedAxisScale) || _adjuster == null) return;

            var oldScale = _adjuster.Scale;
            var newScale = oldScale;
            SetAxis(ref newScale, _axis, selectedAxisScale);

            Undo.SetCurrentGroupName("Set MA Scale Adjuster metric length");
            var undoGroup = Undo.GetCurrentGroup();

            if (_adjustChildPositions)
            {
                var targetL2W = _adjuster.transform.localToWorldMatrix;
                foreach (Transform child in _adjuster.transform)
                {
                    Undo.RecordObject(child, "Set MA Scale Adjuster metric length");
                    child.localPosition = TransformChildPosition(
                        child.localPosition,
                        targetL2W,
                        oldScale,
                        newScale);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                }
            }

            Undo.RecordObject(_adjuster, "Set MA Scale Adjuster metric length");
            _adjuster.Scale = newScale;
            EditorUtility.SetDirty(_adjuster);
            PrefabUtility.RecordPrefabInstancePropertyModifications(_adjuster);

            Undo.CollapseUndoOperations(undoGroup);
            SceneView.RepaintAll();
            Repaint();
        }

        private static Vector3 TransformChildPosition(
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

        private static Vector3 ClampScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Max(ScaleThreshold, scale.x),
                Mathf.Max(ScaleThreshold, scale.y),
                Mathf.Max(ScaleThreshold, scale.z));
        }

        private static float PickValidNearest(float a, float b, float current)
        {
            var aValid = IsFinite(a) && a >= ScaleThreshold;
            var bValid = IsFinite(b) && b >= ScaleThreshold;

            if (!aValid && !bValid) return float.NaN;
            if (!aValid) return b;
            if (!bValid) return a;
            return PickNearest(a, b, current);
        }

        private static float PickNearest(float a, float b, float current)
        {
            if (!IsFinite(a)) return b;
            if (!IsFinite(b)) return a;
            return Mathf.Abs(a - current) <= Mathf.Abs(b - current) ? a : b;
        }

        private bool IsNearlyAxisAligned()
        {
            if (_child == null) return true;

            var d = _child.localPosition;
            var selected = Mathf.Abs(GetAxis(d, _axis));
            return d.magnitude <= 1e-7f || selected / d.magnitude >= 0.995f;
        }

        private static float Square(float value) => value * value;

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float GetAxis(Vector3 value, BoneAxis axis)
        {
            switch (axis)
            {
                case BoneAxis.X: return value.x;
                case BoneAxis.Y: return value.y;
                default: return value.z;
            }
        }

        private static void SetAxis(ref Vector3 value, BoneAxis axis, float component)
        {
            switch (axis)
            {
                case BoneAxis.X:
                    value.x = component;
                    break;
                case BoneAxis.Y:
                    value.y = component;
                    break;
                default:
                    value.z = component;
                    break;
            }
        }
    }
}
