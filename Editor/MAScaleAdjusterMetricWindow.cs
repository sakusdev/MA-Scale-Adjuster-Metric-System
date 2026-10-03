using System;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace SakusDev.MAScaleAdjusterMetricSystem
{
    public sealed class MAScaleAdjusterMetricWindow : EditorWindow
    {
        private enum BoneAxis { X, Y, Z }
        private enum MeasurementMode { FullLength, AxisProjected }

        private ModularAvatarScaleAdjuster _adjuster;
        private Transform _child;
        private BoneAxis _axis = BoneAxis.Y;
        private MeasurementMode _measurementMode = MeasurementMode.FullLength;
        private float _targetMetres = 0.1f;
        private bool _followSelection = true;

        [MenuItem("Tools/MA Scale Adjuster Metric System")]
        public static void Open()
        {
            var window = GetWindow<MAScaleAdjusterMetricWindow>();
            window.titleContent = new GUIContent("MA Metric");
            window.minSize = new Vector2(390, 330);
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
            EditorGUILayout.LabelField("Metric helper for Modular Avatar Scale Adjuster", EditorStyles.miniLabel);
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
                EditorGUILayout.HelpBox("Select a GameObject with MA Scale Adjuster, or assign one above.", MessageType.Info);
                return;
            }

            var bone = _adjuster.transform;
            var children = Enumerable.Range(0, bone.childCount).Select(i => bone.GetChild(i)).ToArray();
            if (children.Length == 0)
            {
                EditorGUILayout.HelpBox("This transform has no direct child to use as a bone endpoint.", MessageType.Warning);
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
            if (GUILayout.Button("Auto", GUILayout.Width(52))) AutoDetectAxis();
            var axisChanged = EditorGUI.EndChangeCheck();
            EditorGUILayout.EndHorizontal();
            if (axisChanged) SyncTargetToCurrent();

            EditorGUI.BeginChangeCheck();
            _measurementMode = (MeasurementMode)EditorGUILayout.EnumPopup("Measurement", _measurementMode);
            if (EditorGUI.EndChangeCheck()) SyncTargetToCurrent();

            EditorGUILayout.Space(8);

            var baseLength = GetBaseLengthMetres();
            var currentLength = GetCurrentLengthMetres();
            var currentAxisScale = GetAxis(_adjuster.Scale, _axis);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("Base length (m)", baseLength);
                EditorGUILayout.FloatField("Current length (m)", currentLength);
                EditorGUILayout.Vector3Field("MA Scale", _adjuster.Scale);
            }

            EditorGUILayout.Space(4);
            _targetMetres = EditorGUILayout.FloatField("Target length (m)", _targetMetres);

            if (!IsFinite(baseLength) || baseLength <= 1e-7f)
            {
                EditorGUILayout.HelpBox("The selected endpoint cannot produce a usable length. Try another child or axis.", MessageType.Warning);
                return;
            }

            if (!IsFinite(_targetMetres) || _targetMetres < 0f)
            {
                EditorGUILayout.HelpBox("Target length must be a finite value of zero or greater.", MessageType.Error);
                return;
            }

            var resultingScale = CalculateRequiredAxisScale(_targetMetres);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.FloatField("Resulting axis scale", resultingScale);

            if (_measurementMode == MeasurementMode.FullLength && !IsNearlyAxisAligned())
            {
                EditorGUILayout.HelpBox(
                    "This bone is not aligned to a single local axis. Full Length solves the selected axis while preserving the other two MA Scale components.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(!IsFinite(resultingScale) || Mathf.Approximately(currentAxisScale, resultingScale)))
            {
                if (GUILayout.Button("Apply to MA Scale Adjuster", GUILayout.Height(30)))
                    Apply(resultingScale);
            }

            if (GUILayout.Button("Reset target to current length"))
                SyncTargetToCurrent();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                _measurementMode == MeasurementMode.FullLength
                    ? "Full Length measures the complete 3D parent-to-child vector after MA's X/Y/Z scale is applied. 1 Unity unit = 1 metre."
                    : "Axis Projected measures only the endpoint displacement on the selected local axis. 1 Unity unit = 1 metre.",
                MessageType.None);
        }

        private Vector3 GetWorldVectorForScale(Vector3 maScale)
        {
            if (_adjuster == null || _child == null) return Vector3.zero;

            // MA inserts a scale proxy directly under the adjusted bone. Conceptually,
            // the endpoint vector is therefore scaled in the bone's local coordinate
            // system first, then transformed by the bone's complete local-to-world
            // linear transform. Using localToWorldMatrix here is important: it retains
            // the bone's own scale and all rotated/non-uniform parent scales.
            var scaledLocalEndpoint = Vector3.Scale(_child.localPosition, maScale);
            return _adjuster.transform.localToWorldMatrix.MultiplyVector(scaledLocalEndpoint);
        }

        private Vector3 GetSelectedAxisWorldVectorPerUnitScale()
        {
            if (_adjuster == null || _child == null) return Vector3.zero;

            var local = Vector3.zero;
            SetAxis(ref local, _axis, GetAxis(_child.localPosition, _axis));
            return _adjuster.transform.localToWorldMatrix.MultiplyVector(local);
        }

        private float GetBaseLengthMetres()
        {
            if (_measurementMode == MeasurementMode.FullLength)
                return GetWorldVectorForScale(Vector3.one).magnitude;

            return GetSelectedAxisWorldVectorPerUnitScale().magnitude;
        }

        private float GetCurrentLengthMetres()
        {
            if (_measurementMode == MeasurementMode.FullLength)
                return GetWorldVectorForScale(_adjuster.Scale).magnitude;

            return GetSelectedAxisWorldVectorPerUnitScale().magnitude
                   * Mathf.Abs(GetAxis(_adjuster.Scale, _axis));
        }

        private float CalculateRequiredAxisScale(float target)
        {
            var axisVector = GetSelectedAxisWorldVectorPerUnitScale();
            var axisSquared = Vector3.Dot(axisVector, axisVector);
            if (axisSquared <= 1e-14f) return float.NaN;

            var currentScale = GetAxis(_adjuster.Scale, _axis);

            if (_measurementMode == MeasurementMode.AxisProjected)
            {
                var magnitude = target / Mathf.Sqrt(axisSquared);
                return currentScale < 0f ? -magnitude : magnitude;
            }

            // Keep the other two MA Scale components fixed. The resulting world-space
            // endpoint is:
            //
            //     fixedVector + axisVector * x
            //
            // where x is the selected MA Scale component. Under a rotated non-uniform
            // parent scale these vectors are generally not orthogonal, so the previous
            // Pythagorean solution was incorrect. Solve the exact quadratic instead:
            //
            //     |fixedVector + axisVector*x|^2 = target^2
            var fixedScale = _adjuster.Scale;
            SetAxis(ref fixedScale, _axis, 0f);
            var fixedVector = GetWorldVectorForScale(fixedScale);

            var a = axisSquared;
            var b = 2f * Vector3.Dot(fixedVector, axisVector);
            var cc = Vector3.Dot(fixedVector, fixedVector) - Square(target);
            var discriminant = b * b - 4f * a * cc;

            // Small negative values can arise from float roundoff at a tangent.
            var tolerance = 1e-6f * Mathf.Max(1f, b * b + Mathf.Abs(4f * a * cc));
            if (discriminant < -tolerance) return float.NaN;
            discriminant = Mathf.Max(0f, discriminant);

            var sqrtDiscriminant = Mathf.Sqrt(discriminant);
            var denominator = 2f * a;
            var rootA = (-b + sqrtDiscriminant) / denominator;
            var rootB = (-b - sqrtDiscriminant) / denominator;

            // Both roots produce the requested length. Choosing the one nearest the
            // current value avoids an unexpected sign flip or a large discontinuity.
            return Mathf.Abs(rootA - currentScale) <= Mathf.Abs(rootB - currentScale)
                ? rootA
                : rootB;
        }

        private bool IsNearlyAxisAligned()
        {
            if (_child == null) return true;
            var d = _child.localPosition;
            var selected = Mathf.Abs(GetAxis(d, _axis));
            return d.magnitude <= 1e-7f || selected / d.magnitude >= 0.995f;
        }

        private void SyncTargetToCurrent()
        {
            if (_adjuster == null || _child == null) return;
            _targetMetres = GetCurrentLengthMetres();
            Repaint();
        }

        private void Apply(float scale)
        {
            if (!IsFinite(scale)) return;
            Undo.RecordObject(_adjuster, "Set MA Scale Adjuster metric length");
            var value = _adjuster.Scale;
            SetAxis(ref value, _axis, scale);
            _adjuster.Scale = value;
            EditorUtility.SetDirty(_adjuster);
            PrefabUtility.RecordPrefabInstancePropertyModifications(_adjuster);
            SceneView.RepaintAll();
        }

        private static float Square(float value) => value * value;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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
                case BoneAxis.X: value.x = component; break;
                case BoneAxis.Y: value.y = component; break;
                default: value.z = component; break;
            }
        }
    }
}
