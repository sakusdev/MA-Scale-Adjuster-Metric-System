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

        private ModularAvatarScaleAdjuster _adjuster;
        private Transform _child;
        private BoneAxis _axis = BoneAxis.Y;
        private float _targetMetres = 0.1f;
        private bool _followSelection = true;

        [MenuItem("Tools/MA Scale Adjuster Metric System")]
        public static void Open()
        {
            var window = GetWindow<MAScaleAdjusterMetricWindow>();
            window.titleContent = new GUIContent("MA Metric");
            window.minSize = new Vector2(360, 260);
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
            window.SyncTargetToCurrent();
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            if (_adjuster == null) TryUseSelection();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

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
            if (candidate == null) return;

            _adjuster = candidate;
            PickDefaultChild();
            SyncTargetToCurrent();
        }

        private void PickDefaultChild()
        {
            if (_adjuster == null) return;
            var bone = _adjuster.transform;
            if (_child != null && _child.parent == bone) return;
            _child = bone.childCount > 0 ? bone.GetChild(0) : null;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("MA Scale Adjuster Metric System", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _followSelection = EditorGUILayout.ToggleLeft("Follow Unity selection", _followSelection);

            EditorGUI.BeginChangeCheck();
            var newAdjuster = (ModularAvatarScaleAdjuster)EditorGUILayout.ObjectField(
                "Scale Adjuster", _adjuster, typeof(ModularAvatarScaleAdjuster), true);
            if (EditorGUI.EndChangeCheck())
            {
                _adjuster = newAdjuster;
                _child = null;
                PickDefaultChild();
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
            var directChildren = Enumerable.Range(0, bone.childCount)
                .Select(bone.GetChild)
                .ToArray();

            if (directChildren.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "This bone has no direct child, so a bone length cannot be measured.",
                    MessageType.Warning);
                return;
            }

            var names = directChildren.Select(t => t.name).ToArray();
            var currentIndex = Math.Max(0, Array.IndexOf(directChildren, _child));
            var nextIndex = EditorGUILayout.Popup("Measure to child", currentIndex, names);
            if (_child != directChildren[nextIndex])
            {
                _child = directChildren[nextIndex];
                SyncTargetToCurrent();
            }

            EditorGUI.BeginChangeCheck();
            _axis = (BoneAxis)EditorGUILayout.EnumPopup("Scale axis", _axis);
            if (EditorGUI.EndChangeCheck()) SyncTargetToCurrent();

            EditorGUILayout.Space(8);

            var baseLength = GetBaseProjectedLengthMetres();
            var currentLength = GetCurrentLengthMetres();
            var currentScale = GetAxis(_adjuster.Scale, _axis);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("Base projected length", baseLength);
                EditorGUILayout.FloatField("Current metric length", currentLength);
                EditorGUILayout.FloatField("Current MA scale", currentScale);
            }

            _targetMetres = EditorGUILayout.FloatField("Target length (m)", _targetMetres);

            if (baseLength <= 1e-7f)
            {
                EditorGUILayout.HelpBox(
                    "The selected child has almost no displacement on this axis. Choose another axis or child.",
                    MessageType.Warning);
                return;
            }

            if (_targetMetres < 0f)
            {
                EditorGUILayout.HelpBox("Target length must be zero or greater.", MessageType.Error);
                return;
            }

            var resultingScale = _targetMetres / baseLength;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.FloatField("Resulting MA scale", resultingScale);

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(Mathf.Approximately(currentScale, resultingScale)))
            {
                if (GUILayout.Button("Apply to MA Scale Adjuster", GUILayout.Height(30)))
                    Apply(resultingScale);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "1 Unity unit is treated as 1 metre. Length is measured as the selected child's displacement projected onto the selected local bone axis, including parent world scale. Only the selected MA Scale axis is changed.",
                MessageType.None);
        }

        private float GetBaseProjectedLengthMetres()
        {
            if (_adjuster == null || _child == null) return 0f;

            var bone = _adjuster.transform;
            var localDelta = bone.InverseTransformPoint(_child.position);
            var localAxisDistance = Mathf.Abs(GetAxis(localDelta, _axis));

            var axis = AxisVector(_axis);
            var worldAxis = bone.parent != null
                ? bone.parent.TransformVector(bone.localRotation * axis)
                : bone.localRotation * axis;

            return localAxisDistance * worldAxis.magnitude;
        }

        private float GetCurrentLengthMetres()
        {
            return GetBaseProjectedLengthMetres() * Mathf.Abs(GetAxis(_adjuster.Scale, _axis));
        }

        private void SyncTargetToCurrent()
        {
            if (_adjuster == null || _child == null) return;
            _targetMetres = GetCurrentLengthMetres();
        }

        private void Apply(float scale)
        {
            Undo.RecordObject(_adjuster, "Set MA Scale Adjuster metric length");

            var value = _adjuster.Scale;
            SetAxis(ref value, _axis, scale);
            _adjuster.Scale = value;

            EditorUtility.SetDirty(_adjuster);
            PrefabUtility.RecordPrefabInstancePropertyModifications(_adjuster);
            SceneView.RepaintAll();
        }

        private static Vector3 AxisVector(BoneAxis axis)
        {
            switch (axis)
            {
                case BoneAxis.X: return Vector3.right;
                case BoneAxis.Y: return Vector3.up;
                default: return Vector3.forward;
            }
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
                case BoneAxis.X: value.x = component; break;
                case BoneAxis.Y: value.y = component; break;
                default: value.z = component; break;
            }
        }
    }
}
