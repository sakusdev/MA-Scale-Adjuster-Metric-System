using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace SakusDev.MAScaleAdjusterMetricSystem
{
    public sealed class MAScaleAdjusterMetricWindow : EditorWindow
    {
        private enum BoneAxis { X, Y, Z }
        private enum MeasurementMode { FullLength, AxisProjected }
        private enum UiLanguage { Auto, Japanese, English }

        private static readonly string[] LengthUnitLabelsEn =
        {
            "Metres (m)",
            "Centimetres (cm)",
            "Millimetres (mm)",
            "Inches (in)",
            "Feet (ft)",
            "Feet & Inches (ft, in)",
            "Yards (yd)"
        };

        private static readonly string[] LengthUnitLabelsJa =
        {
            "メートル (m)",
            "センチメートル (cm)",
            "ミリメートル (mm)",
            "インチ (in)",
            "フィート (ft)",
            "フィート＆インチ (ft, in)",
            "ヤード (yd)"
        };

        private static readonly (HumanBodyBones Left, HumanBodyBones Right)[] HumanoidLeftRightPairs =
        {
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg),
            (HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg),
            (HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot),
            (HumanBodyBones.LeftToes, HumanBodyBones.RightToes),
            (HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm),
            (HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm),
            (HumanBodyBones.LeftHand, HumanBodyBones.RightHand),
            (HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal),
            (HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.RightThumbIntermediate),
            (HumanBodyBones.LeftThumbDistal, HumanBodyBones.RightThumbDistal),
            (HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal),
            (HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate),
            (HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal),
            (HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal),
            (HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.RightMiddleIntermediate),
            (HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal),
            (HumanBodyBones.LeftRingProximal, HumanBodyBones.RightRingProximal),
            (HumanBodyBones.LeftRingIntermediate, HumanBodyBones.RightRingIntermediate),
            (HumanBodyBones.LeftRingDistal, HumanBodyBones.RightRingDistal),
            (HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal),
            (HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.RightLittleIntermediate),
            (HumanBodyBones.LeftLittleDistal, HumanBodyBones.RightLittleDistal)
        };

        private ModularAvatarScaleAdjuster _adjuster;
        private Transform _child;
        private BoneAxis _axis = BoneAxis.Y;
        private MeasurementMode _measurementMode = MeasurementMode.FullLength;
        private LengthUnit _lengthUnit = LengthUnit.Metres;
        private CrossSectionMode _crossSectionMode = CrossSectionMode.Preserve;
        private UiLanguage _language = UiLanguage.Auto;

        private float _targetMetres = 0.1f;
        private int _targetFeet;
        private float _targetInches = 0.1f / MetricScaleMath.MetresPerInch;
        private float _deltaInput;
        private float _ratioPercent = 100f;

        private bool _followSelection = true;
        private bool _adjustChildPositions = true;
        private bool _showSceneGizmos = true;

        private bool _mirrorOpposite;
        private Transform _mirrorBone;
        private Transform _mirrorChild;
        private BoneAxis _mirrorAxis = BoneAxis.Y;

        private bool _keepFeetGrounded;
        private Transform _groundReferenceA;
        private Transform _groundReferenceB;
        private Transform _groundCompensation;

        private bool _showAvatarHeight = true;
        private float _desiredAvatarHeightMetres = 1.55f;
        private int _desiredAvatarHeightFeet = 5;
        private float _desiredAvatarHeightInches = 1.02f;

        private Vector2 _scrollPosition;

        private bool IsJapanese =>
            _language == UiLanguage.Japanese
            || (_language == UiLanguage.Auto && Application.systemLanguage == SystemLanguage.Japanese);

        private string L(string en, string ja) => IsJapanese ? ja : en;

        [MenuItem("Tools/MA Scale Adjuster Metric System")]
        public static void Open()
        {
            var window = GetWindow<MAScaleAdjusterMetricWindow>();
            window.titleContent = new GUIContent("MA Metric");
            window.minSize = new Vector2(440, 560);
            window.TryUseSelection();
            window.Show();
        }

        [MenuItem("CONTEXT/ModularAvatarScaleAdjuster/Edit in Metric System")]
        private static void OpenFromContext(MenuCommand command)
        {
            var window = GetWindow<MAScaleAdjusterMetricWindow>();
            window.titleContent = new GUIContent("MA Metric");
            window._adjuster = command.context as ModularAvatarScaleAdjuster;
            window.OnAdjusterChanged();
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            SceneView.duringSceneGui += OnSceneGUI;
            if (_adjuster == null) TryUseSelection();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSelectionChanged()
        {
            if (!_followSelection)
            {
                Repaint();
                return;
            }

            TryUseSelection();
            Repaint();
            SceneView.RepaintAll();
        }

        private void TryUseSelection()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;

            var candidate = go.GetComponent<ModularAvatarScaleAdjuster>();
            if (candidate == null || candidate == _adjuster) return;

            _adjuster = candidate;
            OnAdjusterChanged();
        }

        private void OnAdjusterChanged()
        {
            _child = null;
            PickDefaultChild();
            AutoDetectAxis();
            AutoDetectMirrorTargets();
            ResetGroundingTargets();
            SyncTargetToCurrent();
            SyncDesiredAvatarHeightToCurrent();
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
            _axis = DetectDominantAxis(_child.localPosition);
        }

        private static BoneAxis DetectDominantAxis(Vector3 localDelta)
        {
            var x = Mathf.Abs(localDelta.x);
            var y = Mathf.Abs(localDelta.y);
            var z = Mathf.Abs(localDelta.z);
            return x >= y && x >= z ? BoneAxis.X : y >= z ? BoneAxis.Y : BoneAxis.Z;
        }

        private void OnGUI()
        {
            DrawHeaderBar();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            try
            {
                DrawMainContent();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawHeaderBar()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("MA Scale Adjuster Metric System", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L("Set MA Scale Adjuster from real bone lengths (Metric & Imperial)",
                  "実寸（メートル法・ヤードポンド法）から MA Scale Adjuster を設定"),
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 58f;
            try
            {
                _language = (UiLanguage)EditorGUILayout.EnumPopup(
                    L("Lang", "言語"),
                    _language,
                    GUILayout.Width(135f));
            }
            finally
            {
                EditorGUIUtility.labelWidth = prevLabelWidth;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);
        }

        private void DrawMainContent()
        {
            EditorGUILayout.BeginHorizontal();
            _followSelection = EditorGUILayout.ToggleLeft(
                L("Follow Unity selection", "Unityの選択オブジェクトに追従"),
                _followSelection);
            EditorGUI.BeginChangeCheck();
            _showSceneGizmos = EditorGUILayout.ToggleLeft(
                L("Show Scene View gizmos", "Sceneビューに寸法ギズモを表示"),
                _showSceneGizmos);
            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            var newAdjuster = (ModularAvatarScaleAdjuster)EditorGUILayout.ObjectField(
                L("Scale Adjuster", "対象 Scale Adjuster"),
                _adjuster,
                typeof(ModularAvatarScaleAdjuster),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                _adjuster = newAdjuster;
                OnAdjusterChanged();
            }

            DrawQuickAddComponentBox();

            if (_adjuster == null)
            {
                EditorGUILayout.HelpBox(
                    L("Select a GameObject with MA Scale Adjuster, assign one above, or add one to the selected bone.",
                      "MA Scale Adjuster が付いたボーンを選択するか、上のボタンから選択中のボーンへ追加してください。"),
                    MessageType.Info);
                return;
            }

            var bone = _adjuster.transform;
            var children = Enumerable.Range(0, bone.childCount).Select(i => bone.GetChild(i)).ToArray();

            if (children.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    L("This transform has no direct child to use as a bone endpoint.",
                      "このボーンには終端として計測できる直接の子ボーンがありません。"),
                    MessageType.Warning);
                return;
            }

            var names = children.Select(t => t.name).ToArray();
            var index = Array.IndexOf(children, _child);
            if (index < 0) index = 0;
            _child = children[index];

            EditorGUI.BeginChangeCheck();
            index = EditorGUILayout.Popup(L("Bone endpoint", "計測終端の子ボーン"), index, names);
            if (EditorGUI.EndChangeCheck())
            {
                _child = children[index];
                AutoDetectAxis();
                AutoDetectMirrorTargets();
                SyncTargetToCurrent();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(L("Primary axis", "主軸（長軸）"));
            EditorGUI.BeginChangeCheck();
            _axis = (BoneAxis)EditorGUILayout.EnumPopup(_axis);
            if (GUILayout.Button(L("Auto", "自動"), GUILayout.Width(52)))
                AutoDetectAxis();
            var axisChanged = EditorGUI.EndChangeCheck();
            EditorGUILayout.EndHorizontal();
            if (axisChanged) SyncTargetToCurrent();

            EditorGUI.BeginChangeCheck();
            _measurementMode = (MeasurementMode)EditorGUILayout.EnumPopup(
                L("Measurement", "計測モード"),
                _measurementMode);
            if (EditorGUI.EndChangeCheck()) SyncTargetToCurrent();

            EditorGUI.BeginChangeCheck();
            var unitLabels = IsJapanese ? LengthUnitLabelsJa : LengthUnitLabelsEn;
            _lengthUnit = (LengthUnit)EditorGUILayout.Popup(
                L("Length unit", "長さの単位"),
                (int)_lengthUnit,
                unitLabels);
            if (EditorGUI.EndChangeCheck())
            {
                SyncFeetAndInchesFromTargetMetres();
                SyncDesiredAvatarHeightFeetAndInches();
                SceneView.RepaintAll();
            }

            var crossSectionLabels = IsJapanese
                ? new[]
                {
                    "維持（長軸のみ変更）",
                    "比例（太さも同じ倍率で拡縮）",
                    "体積維持（伸ばした分だけ細くする）"
                }
                : new[]
                {
                    "Preserve (Change primary axis only)",
                    "Proportional (Scale thickness equally)",
                    "Maintain Volume (Inverse square root)"
                };
            _crossSectionMode = (CrossSectionMode)EditorGUILayout.Popup(
                L("Cross-section (other 2 axes)", "直交2軸（太さ）の扱い"),
                (int)_crossSectionMode,
                crossSectionLabels);

            EditorGUI.BeginChangeCheck();
            _adjustChildPositions = EditorGUILayout.ToggleLeft(
                L("Adjust child positions like Modular Avatar", "Modular Avatarと同様に子ボーン位置を追従させる"),
                _adjustChildPositions);
            if (EditorGUI.EndChangeCheck())
            {
                if (!_adjustChildPositions) _keepFeetGrounded = false;
                SyncTargetToCurrent();
            }

            EditorGUI.BeginChangeCheck();
            _mirrorOpposite = EditorGUILayout.ToggleLeft(
                L("Mirror to opposite side (Left / Right)", "左右対称（反対側の対応ボーンにも同じ長さを同時適用）"),
                _mirrorOpposite);
            if (EditorGUI.EndChangeCheck() && _mirrorOpposite)
                AutoDetectMirrorTargets();

            if (_mirrorOpposite)
                DrawMirrorControls();

            using (new EditorGUI.DisabledScope(!_adjustChildPositions))
            {
                EditorGUI.BeginChangeCheck();
                _keepFeetGrounded = EditorGUILayout.ToggleLeft(
                    L("Keep feet grounded (world Y)", "足裏の高さを維持する（Hips等のワールドY座標を自動補正）"),
                    _keepFeetGrounded);
                if (EditorGUI.EndChangeCheck() && _keepFeetGrounded)
                    AutoDetectGroundingTargets();
            }

            if (_keepFeetGrounded)
                DrawGroundingControls();

            EditorGUILayout.Space(8);

            var currentLength = GetCurrentLengthMetres();
            var unitSymbol = MetricScaleMath.GetUnitSymbol(_lengthUnit);

            using (new EditorGUI.DisabledScope(true))
            {
                if (_lengthUnit == LengthUnit.FeetAndInches)
                {
                    MetricScaleMath.MetresToFeetAndInches(currentLength, out var currentFeet, out var currentInches);
                    DrawFeetAndInchesField(L("Current length (ft / in)", "現在の長さ (ft / in)"), ref currentFeet, ref currentInches);
                }
                else
                {
                    EditorGUILayout.FloatField(
                        L($"Current length ({unitSymbol})", $"現在の長さ ({unitSymbol})"),
                        MetricScaleMath.MetresToUnit(currentLength, _lengthUnit));
                }

                if (_lengthUnit != LengthUnit.Metres)
                    EditorGUILayout.FloatField(L("Current length (m)", "現在の長さ (m)"), currentLength);

                EditorGUILayout.Vector3Field(L("Current MA Scale", "現在の MA Scale"), _adjuster.Scale);
            }

            EditorGUILayout.Space(4);
            DrawTargetLengthInput(unitSymbol);
            DrawQuickStepAndDeltaControls(currentLength, unitSymbol);
            DrawAvatarHeightControls(currentLength);

            if (!MetricScaleMath.IsFinite(currentLength))
            {
                EditorGUILayout.HelpBox(
                    L("The current bone length is not finite.", "現在のボーン長が有限値ではありません。"),
                    MessageType.Error);
                return;
            }

            if (!MetricScaleMath.IsFinite(_targetMetres) || _targetMetres < 0f)
            {
                EditorGUILayout.HelpBox(
                    L("Target length must be a finite value of zero or greater.",
                      "目標の長さには 0 以上の有限値を指定してください。"),
                    MessageType.Error);
                return;
            }

            var resultingScale = CalculateRequiredAxisScale(_targetMetres);
            var resultingScaleVector = MetricScaleMath.BuildTargetScale(
                _adjuster.Scale,
                (int)_axis,
                resultingScale,
                _crossSectionMode);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField(L("Resulting axis scale", "計算された主軸スケール"), resultingScale);
                if (_crossSectionMode != CrossSectionMode.Preserve)
                    EditorGUILayout.Vector3Field(L("Resulting MA Scale", "適用後の MA Scale"), resultingScaleVector);
            }

            if (!MetricScaleMath.IsFinite(resultingScale))
            {
                EditorGUILayout.HelpBox(
                    L("The requested length cannot be reached by changing the selected MA Scale axis.",
                      "指定された目標長は、選択された主軸スケールの変更だけでは到達できません。"),
                    MessageType.Warning);
            }

            if (_measurementMode == MeasurementMode.FullLength && !IsNearlyAxisAligned())
            {
                EditorGUILayout.HelpBox(
                    L("This bone is not aligned to a single local axis. Full Length solves the 3D distance while changing the selected primary axis.",
                      "このボーンは単一のローカル軸と平行ではありません。Full Length モードでは選択した主軸を変化させて3D距離を目標値に合わせます。"),
                    MessageType.Info);
            }

            if (_adjustChildPositions)
            {
                var scale = _adjuster.Scale;
                if (scale.x < MetricScaleMath.ScaleThreshold
                    || scale.y < MetricScaleMath.ScaleThreshold
                    || scale.z < MetricScaleMath.ScaleThreshold)
                {
                    EditorGUILayout.HelpBox(
                        L("One or more MA Scale components are zero, negative, or extremely small. Modular Avatar clamps the effective value while adjusting child positions, so changing the length can produce a discontinuity.",
                          "MA Scale のいずれかの成分が 0 以下または極小です。Modular Avatar の仕様により子ボーン移動時は微小正数にクランプされるため、変更時に値がジャンプする場合があります。"),
                        MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    L("Child transforms will not move. In this mode the target describes the virtual Scale Adjuster deformation vector, not the actual parent-to-child transform distance.",
                      "子ボーンの Transform 座標を移動しません。このモードではボーン間の実距離ではなく、仮想的な Scale Adjuster 変形ベクトル長を編集します。"),
                    MessageType.Warning);
            }

            EditorGUILayout.Space(8);
            var groundingValid = !_keepFeetGrounded || TryValidateGrounding(out _);
            var mirrorValid = !_mirrorOpposite || TryValidateMirror(out _);
            var primaryChanged = !AreScalesApproximatelyEqual(_adjuster.Scale, resultingScaleVector);
            var mirrorWillChange = _mirrorOpposite && mirrorValid && WillMirrorChange(_targetMetres);

            using (new EditorGUI.DisabledScope(
                       !MetricScaleMath.IsFinite(resultingScale)
                       || (!primaryChanged && !mirrorWillChange)
                       || !groundingValid
                       || !mirrorValid))
            {
                var applyLabel = _mirrorOpposite
                    ? L("Apply to Both Sides (MA Scale Adjuster)", "左右両方の MA Scale Adjuster に適用")
                    : L("Apply to MA Scale Adjuster", "MA Scale Adjuster に適用");
                if (GUILayout.Button(applyLabel, GUILayout.Height(30)))
                    Apply(resultingScale);
            }

            if (GUILayout.Button(L("Reset target to current length", "目標長を現在のボーン長にリセット")))
                SyncTargetToCurrent();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                L("1 Unity unit is treated as 1 metre internally. Metric (m, cm, mm) and Imperial (in, ft, yd) units are supported.",
                  "内部では 1 Unity unit = 1 m として計算し、メートル法 (m, cm, mm) とヤード・ポンド法 (in, ft, yd) の相互変換に対応しています。"),
                MessageType.None);
        }

        private void DrawQuickAddComponentBox()
        {
            var selectedGo = Selection.activeGameObject;
            if (selectedGo == null) return;
            if (selectedGo.GetComponent<ModularAvatarScaleAdjuster>() != null) return;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                L($"Selected object '{selectedGo.name}' does not have MA Scale Adjuster.",
                  $"選択中の '{selectedGo.name}' には MA Scale Adjuster がまだ付いていません。"),
                EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(
                    L($"Add MA Scale Adjuster to '{selectedGo.name}'",
                      $"'{selectedGo.name}' に MA Scale Adjuster を追加して編集する")))
            {
                var added = Undo.AddComponent<ModularAvatarScaleAdjuster>(selectedGo);
                EditorUtility.SetDirty(selectedGo);
                _adjuster = added;
                OnAdjusterChanged();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawTargetLengthInput(string unitSymbol)
        {
            if (_lengthUnit == LengthUnit.FeetAndInches)
            {
                EditorGUI.BeginChangeCheck();
                DrawFeetAndInchesField(L("Target length (ft / in)", "目標の長さ (ft / in)"), ref _targetFeet, ref _targetInches);
                if (EditorGUI.EndChangeCheck())
                {
                    _targetMetres = MetricScaleMath.FeetAndInchesToMetres(_targetFeet, _targetInches);
                    SceneView.RepaintAll();
                }
            }
            else
            {
                var displayTarget = MetricScaleMath.MetresToUnit(_targetMetres, _lengthUnit);
                EditorGUI.BeginChangeCheck();
                var nextDisplayTarget = EditorGUILayout.FloatField(
                    L($"Target length ({unitSymbol})", $"目標の長さ ({unitSymbol})"),
                    displayTarget);
                if (EditorGUI.EndChangeCheck())
                {
                    _targetMetres = MetricScaleMath.UnitToMetres(nextDisplayTarget, _lengthUnit);
                    SyncFeetAndInchesFromTargetMetres();
                    SceneView.RepaintAll();
                }
            }

            if (_lengthUnit != LengthUnit.Metres)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.FloatField(L("Target in metres (m)", "目標の長さ (m換算)"), _targetMetres);
            }
        }

        private void DrawQuickStepAndDeltaControls(float currentLengthMetres, string unitSymbol)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(L("Quick adjust", "クイック増減"));
            var isImperial = _lengthUnit == LengthUnit.Inches
                             || _lengthUnit == LengthUnit.Feet
                             || _lengthUnit == LengthUnit.FeetAndInches
                             || _lengthUnit == LengthUnit.Yards;

            if (isImperial)
            {
                DrawStepButton("-1 in", -1f * MetricScaleMath.MetresPerInch);
                DrawStepButton("-0.5 in", -0.5f * MetricScaleMath.MetresPerInch);
                DrawStepButton("-0.1 in", -0.1f * MetricScaleMath.MetresPerInch);
                DrawStepButton("+0.1 in", 0.1f * MetricScaleMath.MetresPerInch);
                DrawStepButton("+0.5 in", 0.5f * MetricScaleMath.MetresPerInch);
                DrawStepButton("+1 in", 1f * MetricScaleMath.MetresPerInch);
            }
            else
            {
                DrawStepButton("-5 cm", -0.05f);
                DrawStepButton("-1 cm", -0.01f);
                DrawStepButton("-1 mm", -0.001f);
                DrawStepButton("+1 mm", 0.001f);
                DrawStepButton("+1 cm", 0.01f);
                DrawStepButton("+5 cm", 0.05f);
            }
            EditorGUILayout.EndHorizontal();

            var deltaUnit = _lengthUnit == LengthUnit.FeetAndInches ? LengthUnit.Inches : _lengthUnit;
            var deltaSymbol = MetricScaleMath.GetUnitSymbol(deltaUnit);

            EditorGUILayout.BeginHorizontal();
            _deltaInput = EditorGUILayout.FloatField(
                L($"Delta (+/- {deltaSymbol})", $"増減値 (+/- {deltaSymbol})"),
                _deltaInput);
            if (GUILayout.Button(L("Current + Δ", "現在値 + Δ"), GUILayout.Width(86)))
            {
                SetTargetMetresClamped(currentLengthMetres + MetricScaleMath.UnitToMetres(_deltaInput, deltaUnit));
            }
            if (GUILayout.Button(L("Target + Δ", "目標値 + Δ"), GUILayout.Width(86)))
            {
                SetTargetMetresClamped(_targetMetres + MetricScaleMath.UnitToMetres(_deltaInput, deltaUnit));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _ratioPercent = EditorGUILayout.FloatField(
                L("Scale ratio (%)", "現在長に対する倍率 (%)"),
                _ratioPercent);
            if (GUILayout.Button("95%", GUILayout.Width(42)))
            {
                _ratioPercent = 95f;
                SetTargetMetresClamped(currentLengthMetres * 0.95f);
            }
            if (GUILayout.Button("105%", GUILayout.Width(46)))
            {
                _ratioPercent = 105f;
                SetTargetMetresClamped(currentLengthMetres * 1.05f);
            }
            if (GUILayout.Button(L("Set", "反映"), GUILayout.Width(46)))
            {
                if (MetricScaleMath.IsFinite(_ratioPercent) && _ratioPercent >= 0f)
                    SetTargetMetresClamped(currentLengthMetres * (_ratioPercent * 0.01f));
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStepButton(string label, float deltaMetres)
        {
            if (GUILayout.Button(label, EditorStyles.miniButton))
                SetTargetMetresClamped(_targetMetres + deltaMetres);
        }

        private void SetTargetMetresClamped(float nextMetres)
        {
            if (!MetricScaleMath.IsFinite(nextMetres)) return;
            _targetMetres = Mathf.Max(0f, nextMetres);
            SyncFeetAndInchesFromTargetMetres();
            SceneView.RepaintAll();
            Repaint();
        }

        private void DrawMirrorControls()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("Mirror (Left / Right)", "左右対称設定"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _mirrorBone = (Transform)EditorGUILayout.ObjectField(
                L("Opposite bone", "反対側のボーン"),
                _mirrorBone,
                typeof(Transform),
                true);
            if (EditorGUI.EndChangeCheck())
                PickDefaultMirrorChildAndAxis();

            _mirrorChild = (Transform)EditorGUILayout.ObjectField(
                L("Opposite endpoint", "反対側の終端子ボーン"),
                _mirrorChild,
                typeof(Transform),
                true);

            _mirrorAxis = (BoneAxis)EditorGUILayout.EnumPopup(
                L("Opposite primary axis", "反対側の主軸"),
                _mirrorAxis);

            if (GUILayout.Button(L("Auto-detect opposite bone", "反対側の対応ボーンを自動検出")))
                AutoDetectMirrorTargets();

            if (!TryValidateMirror(out var mirrorReason))
            {
                EditorGUILayout.HelpBox(mirrorReason, MessageType.Warning);
            }
            else if (_mirrorBone.GetComponent<ModularAvatarScaleAdjuster>() == null)
            {
                EditorGUILayout.HelpBox(
                    L($"'{_mirrorBone.name}' does not have MA Scale Adjuster yet. It will be added automatically when you click Apply.",
                      $"'{_mirrorBone.name}' には MA Scale Adjuster がまだありません。適用時に自動で追加されます。"),
                    MessageType.Info);
            }
            EditorGUILayout.EndVertical();
        }

        private void AutoDetectMirrorTargets()
        {
            _mirrorBone = null;
            _mirrorChild = null;
            if (_adjuster == null) return;

            var bone = _adjuster.transform;
            var animator = _adjuster.GetComponentInParent<Animator>();

            if (animator != null && animator.isHuman)
            {
                _mirrorBone = FindMirroredHumanoidBone(animator, bone);
                if (_child != null)
                    _mirrorChild = FindMirroredHumanoidBone(animator, _child);
            }

            var searchRoot = animator != null ? animator.transform : bone.root;
            if (_mirrorBone == null && MetricScaleMath.TryGetMirroredBoneName(bone.name, out var mirroredBoneName))
            {
                _mirrorBone = FindTransformByName(searchRoot, mirroredBoneName, bone);
            }

            PickDefaultMirrorChildAndAxis();
        }

        private void PickDefaultMirrorChildAndAxis()
        {
            if (_mirrorBone == null)
            {
                _mirrorChild = null;
                return;
            }

            if (_mirrorChild == null || _mirrorChild.parent != _mirrorBone)
            {
                if (_child != null && MetricScaleMath.TryGetMirroredBoneName(_child.name, out var mirroredChildName))
                {
                    for (var i = 0; i < _mirrorBone.childCount; i++)
                    {
                        var candidate = _mirrorBone.GetChild(i);
                        if (string.Equals(candidate.name, mirroredChildName, StringComparison.Ordinal))
                        {
                            _mirrorChild = candidate;
                            break;
                        }
                    }
                }

                if (_mirrorChild == null && _child != null && _adjuster != null)
                {
                    var siblingIndex = _child.GetSiblingIndex();
                    if (siblingIndex >= 0 && siblingIndex < _mirrorBone.childCount)
                        _mirrorChild = _mirrorBone.GetChild(siblingIndex);
                }

                if (_mirrorChild == null && _mirrorBone.childCount > 0)
                    _mirrorChild = _mirrorBone.GetChild(0);
            }

            if (_mirrorChild != null)
                _mirrorAxis = DetectDominantAxis(_mirrorChild.localPosition);
            else
                _mirrorAxis = _axis;
        }

        private static Transform FindMirroredHumanoidBone(Animator animator, Transform target)
        {
            if (animator == null || !animator.isHuman || target == null) return null;

            foreach (var (left, right) in HumanoidLeftRightPairs)
            {
                var leftTransform = animator.GetBoneTransform(left);
                var rightTransform = animator.GetBoneTransform(right);
                if (leftTransform == target) return rightTransform;
                if (rightTransform == target) return leftTransform;
            }

            return null;
        }

        private static Transform FindTransformByName(Transform root, string targetName, Transform exclude)
        {
            if (root == null || string.IsNullOrEmpty(targetName)) return null;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == exclude) continue;
                if (string.Equals(t.name, targetName, StringComparison.Ordinal))
                    return t;
            }

            return null;
        }

        private bool TryValidateMirror(out string reason)
        {
            reason = null;
            if (!_mirrorOpposite) return true;

            if (_mirrorBone == null)
            {
                reason = L("Could not find an opposite bone automatically. Assign one manually or disable Mirror.",
                           "反対側の対応ボーンが見つかりませんでした。手動で指定するか、左右対称オプションをオフにしてください。");
                return false;
            }

            if (_adjuster != null && _mirrorBone == _adjuster.transform)
            {
                reason = L("Opposite bone must be different from the currently selected bone.",
                           "反対側のボーンには現在選択中のボーンとは別の Transform を指定してください。");
                return false;
            }

            if (_mirrorChild == null || _mirrorChild.parent != _mirrorBone)
            {
                reason = L("Opposite endpoint must be a direct child of the opposite bone.",
                           "反対側の終端子ボーンには、反対側ボーンの直接の子を指定してください。");
                return false;
            }

            return true;
        }

        private void DrawAvatarHeightControls(float currentBoneLengthMetres)
        {
            EditorGUILayout.Space(4);
            _showAvatarHeight = EditorGUILayout.Foldout(
                _showAvatarHeight,
                L("Avatar Height Estimation & Solver", "アバター身長計測・目標身長からの逆算"),
                true);
            if (!_showAvatarHeight) return;

            if (!TryMeasureAvatarHeight(out var meshHeightMetres, out var eyeHeightMetres))
            {
                EditorGUILayout.HelpBox(
                    L("Could not estimate avatar height for this hierarchy.",
                      "この階層からアバターの身長を推定できませんでした。"),
                    MessageType.None);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var unitSymbol = MetricScaleMath.GetUnitSymbol(_lengthUnit);

            using (new EditorGUI.DisabledScope(true))
            {
                if (_lengthUnit == LengthUnit.FeetAndInches)
                {
                    MetricScaleMath.MetresToFeetAndInches(meshHeightMetres, out var meshFt, out var meshIn);
                    DrawFeetAndInchesField(L("Current total height", "現在の推定身長（頭頂）"), ref meshFt, ref meshIn);
                    if (eyeHeightMetres > 0f)
                    {
                        MetricScaleMath.MetresToFeetAndInches(eyeHeightMetres, out var eyeFt, out var eyeIn);
                        DrawFeetAndInchesField(L("Current eye height", "現在の視点高 (View)"), ref eyeFt, ref eyeIn);
                    }
                }
                else
                {
                    EditorGUILayout.FloatField(
                        L($"Current total height ({unitSymbol})", $"現在の推定身長 ({unitSymbol})"),
                        MetricScaleMath.MetresToUnit(meshHeightMetres, _lengthUnit));
                    if (eyeHeightMetres > 0f)
                    {
                        EditorGUILayout.FloatField(
                            L($"Current eye height ({unitSymbol})", $"現在の視点高 ({unitSymbol})"),
                            MetricScaleMath.MetresToUnit(eyeHeightMetres, _lengthUnit));
                    }
                }
            }

            if (_lengthUnit == LengthUnit.FeetAndInches)
            {
                EditorGUI.BeginChangeCheck();
                DrawFeetAndInchesField(
                    L("Desired total height", "目標の身長（頭頂）"),
                    ref _desiredAvatarHeightFeet,
                    ref _desiredAvatarHeightInches);
                if (EditorGUI.EndChangeCheck())
                {
                    _desiredAvatarHeightMetres = MetricScaleMath.FeetAndInchesToMetres(
                        _desiredAvatarHeightFeet,
                        _desiredAvatarHeightInches);
                }
            }
            else
            {
                var displayDesired = MetricScaleMath.MetresToUnit(_desiredAvatarHeightMetres, _lengthUnit);
                EditorGUI.BeginChangeCheck();
                var nextDesired = EditorGUILayout.FloatField(
                    L($"Desired total height ({unitSymbol})", $"目標の身長 ({unitSymbol})"),
                    displayDesired);
                if (EditorGUI.EndChangeCheck())
                {
                    _desiredAvatarHeightMetres = MetricScaleMath.UnitToMetres(nextDesired, _lengthUnit);
                    SyncDesiredAvatarHeightFeetAndInches();
                }
            }

            var heightDelta = _desiredAvatarHeightMetres - meshHeightMetres;
            var boneWorldDeltaY = _child != null && _adjuster != null
                ? _child.position.y - _adjuster.transform.position.y
                : 0f;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L("Solve bone length (Tilt-aware)", "身長差を目標長へ反映（傾き考慮）")))
            {
                var nextLen = MetricScaleMath.CalculateBoneLengthForHeightDelta(
                    currentBoneLengthMetres,
                    boneWorldDeltaY,
                    heightDelta,
                    compensateBoneTilt: true);
                SetTargetMetresClamped(nextLen);
            }
            if (GUILayout.Button(L("Solve bone length (1:1)", "身長差を目標長へ反映（1:1）")))
            {
                var nextLen = MetricScaleMath.CalculateBoneLengthForHeightDelta(
                    currentBoneLengthMetres,
                    boneWorldDeltaY,
                    heightDelta,
                    compensateBoneTilt: false);
                SetTargetMetresClamped(nextLen);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void SyncDesiredAvatarHeightToCurrent()
        {
            if (TryMeasureAvatarHeight(out var meshHeightMetres, out _) && meshHeightMetres > 0.05f)
            {
                _desiredAvatarHeightMetres = meshHeightMetres;
                SyncDesiredAvatarHeightFeetAndInches();
            }
        }

        private void SyncDesiredAvatarHeightFeetAndInches()
        {
            MetricScaleMath.MetresToFeetAndInches(
                _desiredAvatarHeightMetres,
                out _desiredAvatarHeightFeet,
                out _desiredAvatarHeightInches);
        }

        private bool TryMeasureAvatarHeight(out float totalHeightMetres, out float eyeHeightMetres)
        {
            totalHeightMetres = 0f;
            eyeHeightMetres = 0f;
            if (_adjuster == null) return false;

            var animator = _adjuster.GetComponentInParent<Animator>();
            var root = animator != null ? animator.transform : _adjuster.transform.root;
            if (root == null) return false;

            var lowestY = root.position.y;
            if (animator != null && animator.isHuman)
            {
                var footCandidates = new[]
                {
                    animator.GetBoneTransform(HumanBodyBones.LeftFoot),
                    animator.GetBoneTransform(HumanBodyBones.RightFoot),
                    animator.GetBoneTransform(HumanBodyBones.LeftToes),
                    animator.GetBoneTransform(HumanBodyBones.RightToes)
                };
                var anyFoot = false;
                var minFootY = float.PositiveInfinity;
                foreach (var f in footCandidates)
                {
                    if (f == null) continue;
                    minFootY = Mathf.Min(minFootY, f.position.y);
                    anyFoot = true;
                }
                if (anyFoot && MetricScaleMath.IsFinite(minFootY))
                    lowestY = Mathf.Min(lowestY, minFootY);
            }

            var renderers = root.GetComponentsInChildren<Renderer>(false);
            var hasBounds = false;
            var highestY = float.NegativeInfinity;
            var minBoundsY = float.PositiveInfinity;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var b = r.bounds;
                if (b.size.sqrMagnitude <= 1e-8f) continue;
                highestY = Mathf.Max(highestY, b.max.y);
                minBoundsY = Mathf.Min(minBoundsY, b.min.y);
                hasBounds = true;
            }

            if (hasBounds)
            {
                lowestY = Mathf.Min(lowestY, minBoundsY);
            }
            else if (animator != null && animator.isHuman)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    highestY = head.position.y + 0.12f;
                    hasBounds = true;
                }
            }

            if (TryGetVrcDescriptorViewWorldY(root, out var viewWorldY))
            {
                eyeHeightMetres = Mathf.Max(0f, viewWorldY - lowestY);
                if (!hasBounds)
                {
                    highestY = viewWorldY + 0.08f;
                    hasBounds = true;
                }
            }

            if (!hasBounds || highestY <= lowestY)
                return false;

            totalHeightMetres = highestY - lowestY;
            return MetricScaleMath.IsFinite(totalHeightMetres) && totalHeightMetres > 0f;
        }

        private static bool TryGetVrcDescriptorViewWorldY(Transform root, out float viewWorldY)
        {
            viewWorldY = 0f;
            if (root == null) return false;

            foreach (var component in root.GetComponentsInParent<Component>(true).Concat(root.GetComponents<Component>()))
            {
                if (component == null) continue;
                var type = component.GetType();
                if (!string.Equals(type.Name, "VRCAvatarDescriptor", StringComparison.Ordinal)
                    && !string.Equals(type.Name, "VRC_AvatarDescriptor", StringComparison.Ordinal))
                {
                    continue;
                }

                var field = type.GetField("ViewPosition", BindingFlags.Instance | BindingFlags.Public);
                if (field != null && field.FieldType == typeof(Vector3))
                {
                    var localView = (Vector3)field.GetValue(component);
                    viewWorldY = component.transform.TransformPoint(localView).y;
                    return true;
                }
            }

            return false;
        }

        private void DrawGroundingControls()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("Grounding", "接地補正設定"), EditorStyles.boldLabel);

            _groundReferenceA = (Transform)EditorGUILayout.ObjectField(
                L("Ground reference A", "接地基準 A"),
                _groundReferenceA,
                typeof(Transform),
                true);
            _groundReferenceB = (Transform)EditorGUILayout.ObjectField(
                L("Ground reference B", "接地基準 B"),
                _groundReferenceB,
                typeof(Transform),
                true);
            _groundCompensation = (Transform)EditorGUILayout.ObjectField(
                L("Move to compensate", "高さ補正で動かすボーン"),
                _groundCompensation,
                typeof(Transform),
                true);

            if (GUILayout.Button(L("Auto-detect Humanoid feet / hips", "Humanoid の両足・Hips を自動検出")))
                AutoDetectGroundingTargets();

            if (!TryValidateGrounding(out var reason))
            {
                EditorGUILayout.HelpBox(reason, MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            if (TryGetLowestGroundY(out var currentGroundY))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    if (_lengthUnit == LengthUnit.FeetAndInches)
                    {
                        MetricScaleMath.MetresToFeetAndInches(currentGroundY, out var groundFeet, out var groundInches);
                        DrawFeetAndInchesField(L("Current ground Y (ft / in)", "現在の接地基準Y (ft / in)"), ref groundFeet, ref groundInches);
                    }
                    else
                    {
                        var groundSymbol = MetricScaleMath.GetUnitSymbol(_lengthUnit);
                        EditorGUILayout.FloatField(
                            L($"Current ground reference Y ({groundSymbol})", $"現在の接地基準Y ({groundSymbol})"),
                            MetricScaleMath.MetresToUnit(currentGroundY, _lengthUnit));
                    }
                }
            }

            EditorGUILayout.HelpBox(
                L("After the edit, the compensation transform is moved only on world Y so the lowest selected ground reference keeps the same height.",
                  "長さ変更後、選択された接地基準のうち最も低い位置のワールドY高が変わらないよう、補正ボーン（Hips等）をワールドY方向に自動移動します。"),
                MessageType.None);
            EditorGUILayout.EndVertical();
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

                if (_groundReferenceA == null && _groundReferenceB == null)
                    _groundReferenceA = _child;
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
                reason = L("Keep Feet Grounded requires child-position adjustment.",
                           "接地補正を使用するには子ボーン位置の追従が必要です。");
                return false;
            }

            if (_groundReferenceA == null && _groundReferenceB == null)
            {
                reason = L("Assign at least one ground reference transform.",
                           "少なくとも1つの接地基準 Transform を指定してください。");
                return false;
            }

            if (_groundCompensation == null)
            {
                reason = L("Assign a compensation transform (normally Humanoid Hips).",
                           "高さ補正を行う Transform（通常は Hips）を指定してください。");
                return false;
            }

            if (_adjuster == null
                || (_adjuster.transform != _groundCompensation
                    && !_adjuster.transform.IsChildOf(_groundCompensation)))
            {
                reason = L("The compensation transform must be the adjusted bone itself or one of its ancestors.",
                           "高さ補正 Transform は調整対象ボーン自身またはその親（祖先）である必要があります。");
                return false;
            }

            if (_mirrorOpposite && _mirrorBone != null
                && _mirrorBone != _groundCompensation
                && !_mirrorBone.IsChildOf(_groundCompensation))
            {
                reason = L("The compensation transform must also be an ancestor of the mirrored bone.",
                           "左右対称適用時は、高さ補正 Transform が反対側ボーンの親（祖先）でもある必要があります。");
                return false;
            }

            if (!IsGroundReferenceUnderCompensation(_groundReferenceA)
                || !IsGroundReferenceUnderCompensation(_groundReferenceB))
            {
                reason = L("Each ground reference must be a descendant of the compensation transform.",
                           "すべての接地基準 Transform は高さ補正 Transform の子孫である必要があります。");
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
            return GetBoneLengthMetres(_adjuster.transform, _child, _adjuster.Scale, _axis);
        }

        private float GetBoneLengthMetres(Transform bone, Transform endpoint, Vector3 maScale, BoneAxis axis)
        {
            if (bone == null || endpoint == null) return 0f;

            if (_adjustChildPositions)
                return MeasureActualChildVector(bone, endpoint.position - bone.position, axis);

            return MeasureVirtualScaleVector(bone, endpoint, maScale, axis);
        }

        private float MeasureActualChildVector(Transform bone, Vector3 worldVector, BoneAxis axis)
        {
            if (_measurementMode == MeasurementMode.FullLength)
                return worldVector.magnitude;

            var axisWorld = GetBoneAxisWorldVector(bone, axis);
            if (axisWorld.sqrMagnitude <= 1e-14f) return 0f;
            return Mathf.Abs(Vector3.Dot(worldVector, axisWorld.normalized));
        }

        private float MeasureVirtualScaleVector(Transform bone, Transform endpoint, Vector3 maScale, BoneAxis axis)
        {
            if (bone == null || endpoint == null) return 0f;

            var local = Vector3.Scale(endpoint.localPosition, maScale);
            var world = bone.localToWorldMatrix.MultiplyVector(local);

            if (_measurementMode == MeasurementMode.FullLength)
                return world.magnitude;

            var axisWorld = GetBoneAxisWorldVector(bone, axis);
            if (axisWorld.sqrMagnitude <= 1e-14f) return 0f;
            return Mathf.Abs(Vector3.Dot(world, axisWorld.normalized));
        }

        private static Vector3 GetBoneAxisWorldVector(Transform bone, BoneAxis axis)
        {
            if (bone == null) return Vector3.zero;

            var local = axis == BoneAxis.X
                ? Vector3.right
                : axis == BoneAxis.Y
                    ? Vector3.up
                    : Vector3.forward;

            return bone.localToWorldMatrix.MultiplyVector(local);
        }

        private float CalculateRequiredAxisScale(float target)
        {
            if (_adjuster == null || _child == null) return float.NaN;
            return CalculateRequiredAxisScaleForBone(_adjuster.transform, _child, _adjuster.Scale, _axis, target);
        }

        private float CalculateRequiredAxisScaleForBone(
            Transform bone,
            Transform endpoint,
            Vector3 currentMaScale,
            BoneAxis axis,
            float target)
        {
            var currentLength = GetBoneLengthMetres(bone, endpoint, currentMaScale, axis);
            if (Mathf.Approximately(target, currentLength))
                return GetAxis(currentMaScale, axis);

            var solved = _adjustChildPositions
                ? CalculateScaleWithChildAdjustment(bone, endpoint, currentMaScale, axis, target)
                : CalculateScaleWithoutChildAdjustment(bone, endpoint, currentMaScale, axis, target);

            if (!MetricScaleMath.IsFinite(solved) || _crossSectionMode == CrossSectionMode.Preserve)
                return solved;

            return RefineScaleWithCrossSection(bone, endpoint, currentMaScale, axis, target, solved);
        }

        private float RefineScaleWithCrossSection(
            Transform bone,
            Transform endpoint,
            Vector3 currentMaScale,
            BoneAxis axis,
            float target,
            float initialGuess)
        {
            if (target <= 1e-7f) return initialGuess;

            var minParam = _adjustChildPositions ? MetricScaleMath.ScaleThreshold : 0f;
            var current = Mathf.Max(minParam, initialGuess);

            for (var i = 0; i < 6; i++)
            {
                var candidateScale = MetricScaleMath.BuildTargetScale(
                    currentMaScale,
                    (int)axis,
                    current,
                    _crossSectionMode);

                var predictedVec = _adjustChildPositions
                    ? PredictAdjustedChildWorldVectorWithFullScale(bone, endpoint, currentMaScale, candidateScale)
                    : GetVirtualWorldVector(bone, endpoint, candidateScale);

                var measured = MeasureActualChildVector(bone, predictedVec, axis);
                if (!MetricScaleMath.IsFinite(measured) || measured <= 1e-7f)
                    break;

                if (Mathf.Abs(measured - target) <= 1e-6f)
                    break;

                current = Mathf.Max(minParam, current * (target / measured));
            }

            return current;
        }

        private float CalculateScaleWithChildAdjustment(
            Transform bone,
            Transform endpoint,
            Vector3 currentMaScale,
            BoneAxis axis,
            float target)
        {
            var currentScale = GetAxis(currentMaScale, axis);
            var x0 = MetricScaleMath.ScaleThreshold;
            var x1 = x0 + 1f;

            var v0 = PredictAdjustedChildWorldVector(bone, endpoint, currentMaScale, axis, x0);
            var direction = PredictAdjustedChildWorldVector(bone, endpoint, currentMaScale, axis, x1) - v0;

            if (_measurementMode == MeasurementMode.AxisProjected)
            {
                var axisWorld = GetBoneAxisWorldVector(bone, axis);
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

        private float CalculateScaleWithoutChildAdjustment(
            Transform bone,
            Transform endpoint,
            Vector3 currentMaScale,
            BoneAxis axis,
            float target)
        {
            var currentScale = GetAxis(currentMaScale, axis);
            var scale0 = currentMaScale;
            var scale1 = currentMaScale;
            SetAxis(ref scale0, axis, 0f);
            SetAxis(ref scale1, axis, 1f);

            var v0 = GetVirtualWorldVector(bone, endpoint, scale0);
            var direction = GetVirtualWorldVector(bone, endpoint, scale1) - v0;

            if (_measurementMode == MeasurementMode.AxisProjected)
            {
                var axisWorld = GetBoneAxisWorldVector(bone, axis);
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

        private static Vector3 PredictAdjustedChildWorldVector(
            Transform bone,
            Transform endpoint,
            Vector3 currentMaScale,
            BoneAxis axis,
            float selectedAxisScale)
        {
            var nextScale = currentMaScale;
            SetAxis(ref nextScale, axis, selectedAxisScale);
            return PredictAdjustedChildWorldVectorWithFullScale(bone, endpoint, currentMaScale, nextScale);
        }

        private static Vector3 PredictAdjustedChildWorldVectorWithFullScale(
            Transform bone,
            Transform endpoint,
            Vector3 currentMaScale,
            Vector3 nextScale)
        {
            var nextLocalPosition = MetricScaleMath.TransformChildPosition(
                endpoint.localPosition,
                bone.localToWorldMatrix,
                currentMaScale,
                nextScale);

            var nextWorldPosition = bone.localToWorldMatrix.MultiplyPoint(nextLocalPosition);
            return nextWorldPosition - bone.position;
        }

        private static Vector3 GetVirtualWorldVector(Transform bone, Transform endpoint, Vector3 scale)
        {
            var scaledLocal = Vector3.Scale(endpoint.localPosition, scale);
            return bone.localToWorldMatrix.MultiplyVector(scaledLocal);
        }

        private void SyncTargetToCurrent()
        {
            if (_adjuster == null || _child == null) return;
            _targetMetres = GetCurrentLengthMetres();
            SyncFeetAndInchesFromTargetMetres();
            _ratioPercent = 100f;
            SceneView.RepaintAll();
            Repaint();
        }

        private void SyncFeetAndInchesFromTargetMetres()
        {
            MetricScaleMath.MetresToFeetAndInches(_targetMetres, out _targetFeet, out _targetInches);
        }

        private static void DrawFeetAndInchesField(string label, ref int feet, ref float inches)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 18f;
            try
            {
                feet = EditorGUILayout.IntField("ft", feet, GUILayout.MinWidth(60f));
                inches = EditorGUILayout.FloatField("in", inches, GUILayout.MinWidth(70f));
            }
            finally
            {
                EditorGUIUtility.labelWidth = prevLabelWidth;
            }
            EditorGUILayout.EndHorizontal();
        }

        private bool WillMirrorChange(float targetMetres)
        {
            if (!_mirrorOpposite || !TryValidateMirror(out _)) return false;
            var existingAdjuster = _mirrorBone.GetComponent<ModularAvatarScaleAdjuster>();
            var currentMirrorScale = existingAdjuster != null ? existingAdjuster.Scale : Vector3.one;
            var solvedMirrorAxis = CalculateRequiredAxisScaleForBone(
                _mirrorBone,
                _mirrorChild,
                currentMirrorScale,
                _mirrorAxis,
                targetMetres);
            if (!MetricScaleMath.IsFinite(solvedMirrorAxis)) return false;

            var targetMirrorScale = MetricScaleMath.BuildTargetScale(
                currentMirrorScale,
                (int)_mirrorAxis,
                solvedMirrorAxis,
                _crossSectionMode);
            return existingAdjuster == null || !AreScalesApproximatelyEqual(currentMirrorScale, targetMirrorScale);
        }

        private static bool AreScalesApproximatelyEqual(Vector3 a, Vector3 b)
        {
            return Mathf.Approximately(a.x, b.x)
                   && Mathf.Approximately(a.y, b.y)
                   && Mathf.Approximately(a.z, b.z);
        }

        private void Apply(float selectedAxisScale)
        {
            if (!MetricScaleMath.IsFinite(selectedAxisScale) || _adjuster == null) return;
            if (_keepFeetGrounded && !TryValidateGrounding(out _)) return;
            if (_mirrorOpposite && !TryValidateMirror(out _)) return;

            var groundYBefore = 0f;
            var useGrounding = _keepFeetGrounded && TryGetLowestGroundY(out groundYBefore);
            var compensation = useGrounding ? _groundCompensation : null;

            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set MA Scale Adjuster metric length");

            if (useGrounding)
                Undo.RecordObject(compensation, "Keep feet grounded");

            var newPrimaryScale = MetricScaleMath.BuildTargetScale(
                _adjuster.Scale,
                (int)_axis,
                selectedAxisScale,
                _crossSectionMode);
            ApplyScaleToSingleAdjuster(_adjuster, newPrimaryScale);

            if (_mirrorOpposite && _mirrorBone != null && _mirrorChild != null)
            {
                var mirrorAdjuster = _mirrorBone.GetComponent<ModularAvatarScaleAdjuster>();
                if (mirrorAdjuster == null)
                    mirrorAdjuster = Undo.AddComponent<ModularAvatarScaleAdjuster>(_mirrorBone.gameObject);

                var mirrorAxisScale = CalculateRequiredAxisScaleForBone(
                    _mirrorBone,
                    _mirrorChild,
                    mirrorAdjuster.Scale,
                    _mirrorAxis,
                    _targetMetres);
                if (MetricScaleMath.IsFinite(mirrorAxisScale))
                {
                    var newMirrorScale = MetricScaleMath.BuildTargetScale(
                        mirrorAdjuster.Scale,
                        (int)_mirrorAxis,
                        mirrorAxisScale,
                        _crossSectionMode);
                    ApplyScaleToSingleAdjuster(mirrorAdjuster, newMirrorScale);
                }
            }

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
            SyncDesiredAvatarHeightToCurrent();
            SceneView.RepaintAll();
            Repaint();
        }

        private void ApplyScaleToSingleAdjuster(ModularAvatarScaleAdjuster adjuster, Vector3 newScale)
        {
            if (adjuster == null) return;
            var oldScale = adjuster.Scale;

            if (_adjustChildPositions)
            {
                var targetL2W = adjuster.transform.localToWorldMatrix;
                foreach (Transform child in adjuster.transform)
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

            Undo.RecordObject(adjuster, "Set MA Scale Adjuster metric length");
            adjuster.Scale = newScale;
            EditorUtility.SetDirty(adjuster);
            PrefabUtility.RecordPrefabInstancePropertyModifications(adjuster);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_showSceneGizmos || _adjuster == null || _child == null) return;

            var bone = _adjuster.transform;
            var startPos = bone.position;
            var currentEndPos = _child.position;
            var handleSize = HandleUtility.GetHandleSize(startPos);
            var cameraForward = sceneView != null && sceneView.camera != null
                ? sceneView.camera.transform.forward
                : Vector3.forward;

            var prevColor = Handles.color;
            try
            {
                Handles.color = new Color(0.15f, 0.85f, 0.95f, 0.95f);
                Handles.DrawAAPolyLine(3f, startPos, currentEndPos);
                Handles.DrawSolidDisc(startPos, cameraForward, handleSize * 0.04f);
                Handles.DrawSolidDisc(currentEndPos, cameraForward, handleSize * 0.04f);

                var axisWorld = GetBoneAxisWorldVector(bone, _axis);
                if (axisWorld.sqrMagnitude > 1e-8f)
                {
                    Handles.color = new Color(0.3f, 1f, 0.4f, 0.8f);
                    Handles.DrawLine(startPos, startPos + axisWorld.normalized * (handleSize * 0.25f));
                }

                var solvedScale = CalculateRequiredAxisScale(_targetMetres);
                if (MetricScaleMath.IsFinite(solvedScale))
                {
                    var nextScaleVec = MetricScaleMath.BuildTargetScale(
                        _adjuster.Scale,
                        (int)_axis,
                        solvedScale,
                        _crossSectionMode);
                    var predictedOffset = _adjustChildPositions
                        ? PredictAdjustedChildWorldVectorWithFullScale(bone, _child, _adjuster.Scale, nextScaleVec)
                        : GetVirtualWorldVector(bone, _child, nextScaleVec);
                    var targetEndPos = startPos + predictedOffset;

                    if ((targetEndPos - currentEndPos).sqrMagnitude > 1e-8f)
                    {
                        Handles.color = new Color(1f, 0.75f, 0.15f, 0.95f);
                        Handles.DrawDottedLine(currentEndPos, targetEndPos, 4f);
                        Handles.DrawWireDisc(targetEndPos, cameraForward, handleSize * 0.055f);
                    }
                }

                var currentLen = GetCurrentLengthMetres();
                var unitSymbol = MetricScaleMath.GetUnitSymbol(_lengthUnit);
                var labelText = _lengthUnit == LengthUnit.FeetAndInches
                    ? FormatFeetAndInches(currentLen) + " -> " + FormatFeetAndInches(_targetMetres)
                    : $"{MetricScaleMath.MetresToUnit(currentLen, _lengthUnit):F3} {unitSymbol} -> {MetricScaleMath.MetresToUnit(_targetMetres, _lengthUnit):F3} {unitSymbol}";
                Handles.Label((startPos + currentEndPos) * 0.5f, labelText, EditorStyles.helpBox);

                if (_mirrorOpposite && _mirrorBone != null && _mirrorChild != null)
                {
                    Handles.color = new Color(0.95f, 0.45f, 0.9f, 0.85f);
                    Handles.DrawAAPolyLine(2.5f, _mirrorBone.position, _mirrorChild.position);
                }
            }
            finally
            {
                Handles.color = prevColor;
            }
        }

        private static string FormatFeetAndInches(float metres)
        {
            MetricScaleMath.MetresToFeetAndInches(metres, out var ft, out var inches);
            return $"{ft}' {inches:F2}\"";
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
