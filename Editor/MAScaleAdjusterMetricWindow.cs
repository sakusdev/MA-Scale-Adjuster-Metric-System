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
        private bool _adjustChildPositions = true;
        private bool _keepFeetGrounded;
        private Transform _groundReferenceA;
        private Transform _groundReferenceB;
        private Transform _groundCompensation;

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
            window.ResetGroundingTargets();
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
            ResetGroundingTargets();
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
                ResetGroundingTargets();
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
            if (EditorGUI.EndChangeCheck())
            {
                if (!_adjustChildPositions) _keepFeetGrounded = false;
                SyncTargetToCurrent();
            }

            using (new EditorGUI.DisabledScope(!_adjustChildPositions))
            {
                EditorGUI.BeginChangeCheck();
                _keepFeetGrounded = EditorGUILayout.ToggleLeft(
                    "Keep feet grounded (world Y)", _keepFeetGrounded);
                if (EditorGUI.EndChangeCheck() && _keepFeetGrounded)
                    AutoDetectGroundingTargets();
            }

            if (_keepFeetGrounded)
                DrawGroundingControls();

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

            if (!MetricScaleMath.IsFinite(currentLength))
            {
                EditorGUILayout.HelpBox("The current bone length is not finite.", MessageType.Error);
                return;
            }

            if (!MetricScaleMath.IsFinite(_targetMetres) || _targetMetres < 0f)
            {
                EditorGUILayout.HelpBox(
                    "Target length must be a finite value of zero or greater.",
                    MessageType.Error);
                return;
            }

            var resultingScale = CalculateRequiredAxisScale(_targetMetres);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.FloatField("Resulting axis scale", resultingScale);

            if (!MetricScaleMath.IsFinite(resultingScale))
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

                var scale = _adjuster.Scale;
                if (scale.x < MetricScaleMath.ScaleThreshold
                    || scale.y < MetricScaleMath.ScaleThreshold
                    || scale.z < MetricScaleMath.ScaleThreshold)
                {
                    EditorGUILayout.HelpBox(
                        "One or more MA Scale components are zero, negative, or extremely small. Modular Avatar clamps the effective value while adjusting child positions, so changing the metric length can produce a discontinuity. An unchanged target is kept as a true no-op.",
                        MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Child transforms will not move. In this mode the target describes the virtual Scale Adjuster deformation vector, not the actual parent-to-child transform distance.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(8);
            var groundingValid = !_keepFeetGrounded || TryValidateGrounding(out _);
            using (new EditorGUI.DisabledScope(
                       !MetricScaleMath.IsFinite(resultingScale)
                       || Mathf.Approximately(currentAxisScale, resultingScale)
                       || !groundingValid))
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

        private void DrawGroundingControls()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Grounding", EditorStyles.boldLabel);

            _groundReferenceA = (Transform)EditorGUILayout.ObjectField(
                "Ground reference A", _groundReferenceA, typeof(Transform), true);
            _groundReferenceB = (Transform)EditorGUILayout.ObjectField(
                "Ground reference B", _groundReferenceB, typeof(Transform), true);
            _groundCompensation = (Transform)EditorGUILayout.ObjectField(
                "Move to compensate", _groundCompensation, typeof(Transform), true);

            if (GUILayout.Button("Auto-detect Humanoid feet / hips"))
                AutoDetectGroundingTargets();

            if (!TryValidateGrounding(out var reason))
            {
                EditorGUILayout.HelpBox(reason, MessageType.Warning);
                return;
            }

            if (TryGetLowestGroundY(out var currentGroundY))
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.FloatField("Current ground reference Y", currentGroundY);
            }

            EditorGUILayout.HelpBox(
                "After the metric edit, the compensation transform is moved only on world Y so the lowest selected ground reference keeps the same height. With a Humanoid avatar, LeftFoot/RightFoot and Hips are detected automatically.",
                MessageType.None);
        }

        private void ResetGroundingTargets()
        {
            _groundReferenceA = null;
            _groundReferenceB = null;
            _groundCompensation = null;

            if (_keepFeetGrounded)
                AutoDetectGroundingTargets();
        }

        private void AutoDetectGroundingTargets()
        {
            if (_adjuster == null) return;

            var animator = _adjuster.GetComponentInParent<Animator>();
            if (animator != null && animator.isHuman)
            {
                _groundReferenceA = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                _groundReferenceB = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                _groundCompensation = animator.GetBoneTransform(HumanBodyBones.Hips);

                if (_groundCompensation == null)
                    _groundCompensation = animator.transform;

                if (_groundReferenceA == null && _groundReferenceB != null)
                {
                    _groundReferenceA = _groundReferenceB;
                    _groundReferenceB = null;
                }
            }
            else
            {
                _groundReferenceA = _child;
                _groundReferenceB = null;
                _groundCompensation = animator != null
                    ? animator.transform
                    : _adjuster.transform.root;
            }
        }

        private bool TryValidateGrounding(out string reason)
        {
            reason = null;

            if (!_keepFeetGrounded)
                return true;

            if (!_adjustChildPositions)
            {
                reason = "Keep Feet Grounded requires child-position adjustment.";
                return false;
            }

            if (_groundReferenceA == null && _groundReferenceB == null)
            {
                reason = "Assign at least one ground reference transform.";
                return false;
            }

            if (_groundCompensation == null)
            {
                reason = "Assign a compensation transform (normally Humanoid Hips).";
                return false;
            }

            if (_adjuster == null || !_adjuster.transform.IsChildOf(_groundCompensation))
            {
                reason = "The compensation transform must be an ancestor of the adjusted bone.";
                return false;
            }

            if (!IsGroundReferenceUnderCompensation(_groundReferenceA)
                || !IsGroundReferenceUnderCompensation(_groundReferenceB))
            {
                reason = "Each ground reference must be a descendant of the compensation transform.";
                return false;
            }

            return true;
        }

        private bool IsGroundReferenceUnderCompensation(Transform reference)
        {
            return reference == null
                   || (_groundCompensation != null && reference.IsChildOf(_groundCompensation));
        }

        private bool TryGetLowestGroundY(out float y)
        {
            y = float.PositiveInfinity;
            var found = false;

            if (_groundReferenceA != null)
            {
                y = _groundReferenceA.position.y;
                found = true;
            }

            if (_groundReferenceB != null)
            {
                y = found ? Mathf.Min(y, _groundReferenceB.position.y) : _groundReferenceB.position.y;
                found = true;
            }

            return found && MetricScaleMath.IsFinite(y);
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
            // A no-op metric edit should remain a true no-op, including when the
            // current MA Scale uses zero or a negative value that MA's child-position
            // conversion would otherwise clamp internally.
            if (Mathf.Approximately(target, GetCurrentLengthMetres()))
                return GetAxis(_adjuster.Scale, _axis);

            return _adjustChildPositions
                ? CalculateScaleWithChildAdjustment(target)
                : CalculateScaleWithoutChildAdjustment(target);
        }

        private float CalculateScaleWithChildAdjustment(float target)
        {
            if (_adjuster == null || _child == null) return float.NaN;

            var currentScale = GetAxis(_adjuster.Scale, _axis);
            var x0 = MetricScaleMath.ScaleThreshold;
            var x1 = x0 + 1f;

            var v0 = PredictAdjustedChildWorldVector(x0);
            var direction = PredictAdjustedChildWorldVector(x1) - v0;

            if (_measurementMode == MeasurementMode.AxisProjected)
            {
                var axisWorld = GetBoneAxisWorldVector();
                if (axisWorld.sqrMagnitude <= 1e-14f) return float.NaN;
                axisWorld.Normalize();

                return MetricScaleMath.SolveAbsoluteProjectionAlongLinearPath(
                    Vector3.Dot(v0, axisWorld),
                    Vector3.Dot(direction, axisWorld),
                    target,
                    x0,
                    currentScale,
                    MetricScaleMath.ScaleThreshold);
            }

            return MetricScaleMath.SolveMagnitudeAlongLinearPath(
                v0,
                direction,
                target,
                x0,
                currentScale,
                MetricScaleMath.ScaleThreshold);
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

                return MetricScaleMath.SolveAbsoluteProjectionAlongLinearPath(
                    Vector3.Dot(v0, axisWorld),
                    Vector3.Dot(direction, axisWorld),
                    target,
                    0f,
                    currentScale,
                    float.NegativeInfinity);
            }

            return MetricScaleMath.SolveMagnitudeAlongLinearPath(
                v0,
                direction,
                target,
                0f,
                currentScale,
                float.NegativeInfinity);
        }

        private Vector3 PredictAdjustedChildWorldVector(float selectedAxisScale)
        {
            var nextScale = _adjuster.Scale;
            SetAxis(ref nextScale, _axis, selectedAxisScale);

            var nextLocalPosition = MetricScaleMath.TransformChildPosition(
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
            if (!MetricScaleMath.IsFinite(selectedAxisScale) || _adjuster == null) return;
            if (_keepFeetGrounded && !TryValidateGrounding(out _)) return;

            var useGrounding = _keepFeetGrounded && TryGetLowestGroundY(out var groundYBefore);
            var compensation = useGrounding ? _groundCompensation : null;

            var oldScale = _adjuster.Scale;
            var newScale = oldScale;
            SetAxis(ref newScale, _axis, selectedAxisScale);

            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set MA Scale Adjuster metric length");

            if (useGrounding)
                Undo.RecordObject(compensation, "Keep feet grounded");

            if (_adjustChildPositions)
            {
                var targetL2W = _adjuster.transform.localToWorldMatrix;
                foreach (Transform child in _adjuster.transform)
                {
                    Undo.RecordObject(child, "Set MA Scale Adjuster metric length");
                    child.localPosition = MetricScaleMath.TransformChildPosition(
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

            if (useGrounding && TryGetLowestGroundY(out var groundYAfter))
            {
                var deltaY = groundYBefore - groundYAfter;
                if (MetricScaleMath.IsFinite(deltaY) && !Mathf.Approximately(deltaY, 0f))
                {
                    var worldPosition = compensation.position;
                    worldPosition.y += deltaY;
                    compensation.position = worldPosition;

                    EditorUtility.SetDirty(compensation);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(compensation);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            SceneView.RepaintAll();
            Repaint();
        }

        private bool IsNearlyAxisAligned()
        {
            if (_child == null) return true;

            var d = _child.localPosition;
            var selected = Mathf.Abs(GetAxis(d, _axis));
            return d.magnitude <= 1e-7f || selected / d.magnitude >= 0.995f;
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
