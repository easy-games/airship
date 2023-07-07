#if !UNITY_2019_1_OR_NEWER
#define VERYANIMATION_TIMELINE
#endif

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;

namespace VeryAnimation
{
    public partial class VeryAnimation
    {
        public enum ToolMode
        {
            Copy,
            Trim,
            Add,
            Combine,
            CreateNewClip,
            CreateNewKeyframe,
            BakeIK,
            AnimationRigging,
            HumanoidIK,
            RootMotion,
            ParameterRelatedCurves,
            RotationCurveInterpolation,
            KeyframeReduction,
            EnsureQuaternionContinuity,
            Cleanup,
            FixErrors,
            AnimCompression,
            Export,
        }
        public ToolMode toolMode;
        public bool toolsHelp = true;
        public int toolCopy_FirstFrame;
        public int toolCopy_LastFrame;
        public int toolCopy_WriteFrame;
        public int toolTrim_FirstFrame;
        public int toolTrim_LastFrame;
        public AnimationClip toolAdd_Clip;
        public AnimationClip toolCombine_Clip;
        public int toolCreateNewKeyframe_FirstFrame;
        public int toolCreateNewKeyframe_LastFrame;
        public int toolCreateNewKeyframe_IntervalFrame = 6;
        public bool toolCreateNewKeyframe_AnimatorRootT = true;
        public bool toolCreateNewKeyframe_AnimatorRootQ = true;
        public bool toolCreateNewKeyframe_AnimatorMuscle = true;
        public bool toolCreateNewKeyframe_AnimatorTDOF;
        public bool toolCreateNewKeyframe_TransformPosition;
        public bool toolCreateNewKeyframe_TransformRotation = true;
        public bool toolCreateNewKeyframe_TransformScale;
        public enum BakeIKMode
        {
            Simple,
            Interpolation,
        }
        public BakeIKMode toolBakeIK_Mode;
        public int toolBakeIK_FirstFrame;
        public int toolBakeIK_LastFrame;
        public bool toolBakeIK_BakeHandIK;
        public bool toolBakeIK_BakeFootIK;
        public bool toolBakeIK_BakeAnimationRigging;
        public int toolAnimationRigging_FirstFrame;
        public int toolAnimationRigging_LastFrame;
        public bool toolAnimationRigging_BakeHandIK;
        public bool toolAnimationRigging_BakeFootIK;
        public bool toolHumanoidIK_Hand;
        public bool toolHumanoidIK_Foot = true;
        public int toolHumanoidIK_FirstFrame;
        public int toolHumanoidIK_LastFrame;
        public enum RootMotionMode
        {
            MotionCurves,
            RootCurves,
        }
        public RootMotionMode toolRootMotion_Mode;
        public bool toolCleanup_RemoveRoot;
        public bool toolCleanup_RemoveIK;
        public bool toolCleanup_RemoveTDOF;
        public bool toolCleanup_RemoveMotion;
        public bool toolCleanup_RemoveFinger;
        public bool toolCleanup_RemoveEyes;
        public bool toolCleanup_RemoveJaw;
        public bool toolCleanup_RemoveToes;
        public bool toolCleanup_RemoveTransformPosition;
        public bool toolCleanup_RemoveTransformRotation;
        public bool toolCleanup_RemoveTransformScale;
        public bool toolCleanup_RemoveBlendShape;
        public bool toolCleanup_RemoveObjectReference;
        public bool toolCleanup_RemoveEvent;
        public bool toolCleanup_RemoveMissing = true;
        public bool toolCleanup_RemoveHumanoidConflict = true;
        public bool toolCleanup_RemoveRootMotionConflict = true;
        public bool toolCleanup_RemoveUnnecessary = true;
        public enum RotationCurveInterpolationMode
        {
            Quaternion,
            EulerAngles,
        };
        public RotationCurveInterpolationMode toolRotationInterpolation_Mode;
        public float toolKeyframeReduction_RotationError = 0.5f;
        public float toolKeyframeReduction_PositionError = 0.5f;
        public float toolKeyframeReduction_ScaleAndOthersError = 0.5f;
        public bool toolKeyframeReduction_EnableHumanoid = true;
        public bool toolKeyframeReduction_EnableHumanoidRootAndIKGoal = true;
        public bool toolKeyframeReduction_EnableGeneric = true;
        public bool toolKeyframeReduction_EnableOther = true;
        public bool toolAnimCompression_Compressed;
        public bool toolAnimCompression_UseHighQualityCurve = true;
        public bool toolExport_ActiveOnly = true;
        public bool toolExport_Mesh = true;
        public enum ExportAnimationMode
        {
            None,
            CurrentClip,
            AllClips,
        };
        public ExportAnimationMode toolExport_AnimationMode = ExportAnimationMode.CurrentClip;
        public bool toolExport_BakeFootIK = true;
        public bool toolExport_BakeAnimationRigging;

#pragma warning disable 0414
        private bool toolBakeIK_AnimatorIKFoldout = true;
        private bool toolBakeIK_OriginalIKFoldout = true;
        private bool toolAnimationRigging_AnimatorIKFoldout = true;
#pragma warning restore 0414

        private static readonly GUIContent[] BakeIKModeStrings =
        {
            new GUIContent("Simple"),
            new GUIContent("Interpolation"),
        };
        private static readonly GUIContent[] RootMotionModeStrings =
        {
            new GUIContent("Motion Curves", "MotionT, MotionQ"),
            new GUIContent("Root Curves", "RootT, RootQ"),
        };

        private class ParameterRelatedData
        {
            public string propertyName;
            public int parameterIndex;
            public bool enableAnimationCurve;
            public bool enableAnimatorParameter;
        }
        private List<ParameterRelatedData> toolParameterRelatedCurve_DataList;
        private bool toolParameterRelatedCurve_Update;
        private ReorderableList toolParameterRelatedCurve_List;

        public void ToolsGUI()
        {
            if (currentClip == null) return;
            var clip = currentClip;
            var e = Event.current;

            EditorGUILayout.BeginVertical(vaw.guiStyleSkinBox);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUI.BeginChangeCheck();
                    var mode = (ToolMode)EditorGUILayout.EnumPopup(toolMode);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Tool Mode");
                        toolMode = mode;
                    }
                }
                {
                    if (GUILayout.Button(vaw.uEditorGUI.GetHelpIcon(), toolsHelp ? vaw.guiStyleIconActiveButton : vaw.guiStyleIconButton, GUILayout.Width(19)))
                    {
                        Undo.RecordObject(vaw, "Change Tool Help");
                        toolsHelp = !toolsHelp;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel++;
            if (toolsHelp)
            {
                EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpToolsCopy + (int)toolMode), MessageType.Info);
            }
            if (toolMode == ToolMode.Copy)
            {
                #region Copy
                if (uAnimationClipEditor != null)
                {
                    float firstFrame = toolCopy_FirstFrame;
                    float lastFrame = toolCopy_LastFrame;
                    float additivePoseframe = 0.0f;
                    bool changedStart = false;
                    bool changedStop = false;
                    bool changedAdditivePoseframe = false;
                    uAnimationClipEditor.ClipRangeGUI(ref firstFrame, ref lastFrame, out changedStart, out changedStop, false, ref additivePoseframe, out changedAdditivePoseframe);
                    if (changedStart)
                    {
                        Undo.RecordObject(vaw, "Change First Frame");
                        toolCopy_FirstFrame = Mathf.RoundToInt(firstFrame);
                    }
                    if (changedStop)
                    {
                        Undo.RecordObject(vaw, "Change Last Frame");
                        toolCopy_LastFrame = Mathf.RoundToInt(lastFrame);
                    }
                }
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(Language.GetContent(Language.Help.ToolsCopyWriteFrame), GUILayout.Width(132));
                    {
                        EditorGUI.BeginChangeCheck();
                        var frame = EditorGUILayout.IntField(toolCopy_WriteFrame, GUILayout.Width(64));
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change Write Frame");
                            toolCopy_WriteFrame = Math.Max(frame, 0);
                        }
                        if (GUILayout.Button(new GUIContent("Current", "Set current frame"), EditorStyles.miniButton, GUILayout.Width(64), GUILayout.Height(15)))
                        {
                            Undo.RecordObject(vaw, "Change Write Frame");
                            toolCopy_WriteFrame = uAw.GetCurrentFrame();
                        }
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("to", GUILayout.Width(32));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField((toolCopy_WriteFrame + (toolCopy_LastFrame - toolCopy_FirstFrame)).ToString(), GUILayout.Width(100));
                    EditorGUILayout.EndHorizontal();
                }
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Copy"))
                    {
                        ToolsCopy(clip);
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            else if (toolMode == ToolMode.Trim)
            {
                #region Trim
                if (uAnimationClipEditor != null)
                {
                    float firstFrame = toolTrim_FirstFrame;
                    float lastFrame = toolTrim_LastFrame;
                    float additivePoseframe = 0.0f;
                    bool changedStart = false;
                    bool changedStop = false;
                    bool changedAdditivePoseframe = false;
                    uAnimationClipEditor.ClipRangeGUI(ref firstFrame, ref lastFrame, out changedStart, out changedStop, false, ref additivePoseframe, out changedAdditivePoseframe);
                    if (changedStart)
                    {
                        Undo.RecordObject(vaw, "Change First Frame");
                        toolTrim_FirstFrame = Mathf.RoundToInt(firstFrame);
                    }
                    if (changedStop)
                    {
                        Undo.RecordObject(vaw, "Change Last Frame");
                        toolTrim_LastFrame = Mathf.RoundToInt(lastFrame);
                    }
                }
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Trim"))
                    {
                        ToolsTrim(clip);
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            else if (toolMode == ToolMode.Add)
            {
                #region Add
                {
                    EditorGUI.BeginChangeCheck();
                    var addClip = EditorGUILayout.ObjectField("Add Clip", toolAdd_Clip, typeof(AnimationClip), true) as AnimationClip;
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Add Clip");
                        toolAdd_Clip = addClip;
                    }
                }
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    EditorGUI.BeginDisabledGroup(toolAdd_Clip == null);
                    if (GUILayout.Button("Add"))
                    {
                        ToolsAdd(clip);
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            else if (toolMode == ToolMode.Combine)
            {
                #region Combine
                {
                    EditorGUI.BeginChangeCheck();
                    var combineClip = EditorGUILayout.ObjectField("Combine Clip", toolCombine_Clip, typeof(AnimationClip), true) as AnimationClip;
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Combine Clip");
                        toolCombine_Clip = combineClip;
                    }
                }
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    EditorGUI.BeginDisabledGroup(toolCombine_Clip == null);
                    if (GUILayout.Button("Combine"))
                    {
                        ToolsCombine(clip);
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            else if (toolMode == ToolMode.CreateNewClip)
            {
                #region CreateNewClip
                {
                    Action<ToolsCreateNewClipMode> ActionToolsCreateNewClip = (mode) =>
                    {
                        var name = clip.name;
                        if (mode == ToolsCreateNewClipMode.Mirror)
                            name += " (mirror)";
                        var assetPath = string.Format("{0}/{1}.anim", EditorCommon.GetAssetPath(clip), name);
                        var uniquePath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                        string path = EditorUtility.SaveFilePanel("Create new animation clip", Path.GetDirectoryName(uniquePath), Path.GetFileName(uniquePath), "anim");
                        if (!string.IsNullOrEmpty(path))
                        {
                            if (!path.StartsWith(Application.dataPath))
                            {
                                EditorCommon.SaveInsideAssetsFolderDisplayDialog();
                            }
                            else
                            {
                                path = FileUtil.GetProjectRelativePath(path);
                                ToolsCreateNewClip(path, mode);
                            }
                        }
                    };

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Blank"))
                    {
                        ActionToolsCreateNewClip(ToolsCreateNewClipMode.Blank);
                    }
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Duplicate"))
                    {
                        ActionToolsCreateNewClip(ToolsCreateNewClipMode.Duplicate);
                    }
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Mirror"))
                    {
                        ActionToolsCreateNewClip(ToolsCreateNewClipMode.Mirror);
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            else if (toolMode == ToolMode.CreateNewKeyframe)
            {
                #region CreateNewKeyframe
                if (uAnimationClipEditor != null)
                {
                    float firstFrame = toolCreateNewKeyframe_FirstFrame;
                    float lastFrame = toolCreateNewKeyframe_LastFrame;
                    float additivePoseframe = 0.0f;
                    bool changedStart = false;
                    bool changedStop = false;
                    bool changedAdditivePoseframe = false;
                    uAnimationClipEditor.ClipRangeGUI(ref firstFrame, ref lastFrame, out changedStart, out changedStop, false, ref additivePoseframe, out changedAdditivePoseframe);
                    if (changedStart)
                    {
                        Undo.RecordObject(vaw, "Change First Frame");
                        toolCreateNewKeyframe_FirstFrame = Mathf.RoundToInt(firstFrame);
                    }
                    if (changedStop)
                    {
                        Undo.RecordObject(vaw, "Change Last Frame");
                        toolCreateNewKeyframe_LastFrame = Mathf.RoundToInt(lastFrame);
                    }
                }
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(Language.GetContent(Language.Help.ToolsCreateNewKeyframeIntervalFrame), GUILayout.Width(132));
                    {
                        EditorGUI.BeginChangeCheck();
                        var frame = EditorGUILayout.IntField(toolCreateNewKeyframe_IntervalFrame, GUILayout.Width(64));
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change Interval Frame");
                            toolCreateNewKeyframe_IntervalFrame = Math.Max(frame, 1);
                        }
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(string.Format("{0} Second", GetFrameTime(toolCreateNewKeyframe_IntervalFrame)), vaw.guiStyleMiddleRightGreyMiniLabel);
                    EditorGUILayout.EndHorizontal();
                }
                if (isHuman)
                {
                    EditorGUILayout.LabelField(new GUIContent("Animator", "Humanoid"));
                    EditorGUI.indentLevel++;
                    {
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft("RootT", toolCreateNewKeyframe_AnimatorRootT);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Flag");
                                toolCreateNewKeyframe_AnimatorRootT = flag;
                            }
                        }
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft("RootQ", toolCreateNewKeyframe_AnimatorRootQ);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Flag");
                                toolCreateNewKeyframe_AnimatorRootQ = flag;
                            }
                        }
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft("Muscle", toolCreateNewKeyframe_AnimatorMuscle);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Flag");
                                toolCreateNewKeyframe_AnimatorMuscle = flag;
                            }
                        }
                        if (humanoidHasTDoF)
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft("TDOF", toolCreateNewKeyframe_AnimatorTDOF);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Flag");
                                toolCreateNewKeyframe_AnimatorTDOF = flag;
                            }
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                {
                    EditorGUILayout.LabelField(new GUIContent("Transform", "Generic"));
                    EditorGUI.indentLevel++;
                    {
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft("Position", toolCreateNewKeyframe_TransformPosition);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Flag");
                                toolCreateNewKeyframe_TransformPosition = flag;
                            }
                        }
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft("Rotation", toolCreateNewKeyframe_TransformRotation);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Flag");
                                toolCreateNewKeyframe_TransformRotation = flag;
                            }
                        }
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft("Scale", toolCreateNewKeyframe_TransformScale);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Flag");
                                toolCreateNewKeyframe_TransformScale = flag;
                            }
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                bool disable = (selectionBones == null || selectionBones.Count == 0);
                {
                    EditorGUI.BeginDisabledGroup(disable);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Apply"))
                    {
                        ToolsCreateNewKeyframe(clip);
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.EndDisabledGroup();
                }
                #endregion
            }
            else if (toolMode == ToolMode.BakeIK)
            {
                #region BakeIK
                if (uAnimationClipEditor != null)
                {
                    float firstFrame = toolBakeIK_FirstFrame;
                    float lastFrame = toolBakeIK_LastFrame;
                    float additivePoseframe = 0.0f;
                    bool changedStart = false;
                    bool changedStop = false;
                    bool changedAdditivePoseframe = false;
                    uAnimationClipEditor.ClipRangeGUI(ref firstFrame, ref lastFrame, out changedStart, out changedStop, false, ref additivePoseframe, out changedAdditivePoseframe);
                    if (changedStart)
                    {
                        Undo.RecordObject(vaw, "Change First Frame");
                        toolBakeIK_FirstFrame = Mathf.RoundToInt(firstFrame);
                    }
                    if (changedStop)
                    {
                        Undo.RecordObject(vaw, "Change Last Frame");
                        toolBakeIK_LastFrame = Mathf.RoundToInt(lastFrame);
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var mode = (BakeIKMode)GUILayout.Toolbar((int)toolBakeIK_Mode, BakeIKModeStrings, EditorStyles.miniButton);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Root Motion Mode");
                        toolBakeIK_Mode = mode;
                    }
                    if (toolsHelp)
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.ToolsBakeIKSimple + (int)toolBakeIK_Mode), MessageType.Info);
                    }
                    EditorGUILayout.Space();
                }
                bool disable = !animatorIK.ikData.Any(data => data.enable) && !originalIK.ikData.Any(data => data.enable);
                {
                    {
                        EditorGUILayout.LabelField("Bake");
                        EditorGUI.indentLevel++;
                        {
                            bool disableBake = !isHuman;
                            EditorGUI.BeginDisabledGroup(disableBake);
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft(new GUIContent("Hand IK", "Animator HandT and HandQ"), toolBakeIK_BakeHandIK);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Bake Flag");
                                toolBakeIK_BakeHandIK = flag;
                            }
                            EditorGUI.EndDisabledGroup();
                        }
                        {
                            bool disableBake = !isHuman;
                            EditorGUI.BeginDisabledGroup(disableBake);
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft(new GUIContent("Foot IK", "Animator FootT and FootQ"), toolBakeIK_BakeFootIK);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Bake Flag");
                                toolBakeIK_BakeFootIK = flag;
                            }
                            EditorGUI.EndDisabledGroup();
                        }
#if VERYANIMATION_ANIMATIONRIGGING
                        {
                            bool disableBake = !animationRigging.isValid;
                            EditorGUI.BeginDisabledGroup(disableBake);
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft("Animation Rigging", toolBakeIK_BakeAnimationRigging);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Bake Flag");
                                toolBakeIK_BakeAnimationRigging = flag;
                            }
                            EditorGUI.EndDisabledGroup();
                        }
#endif
                        EditorGUI.indentLevel--;
                    }
                    #region AnimatorIK
                    if (isHuman && animatorIK.ikData != null)
                    {
                        toolBakeIK_AnimatorIKFoldout = EditorGUILayout.Foldout(toolBakeIK_AnimatorIKFoldout, "Animator IK", true);
                        if (toolBakeIK_AnimatorIKFoldout)
                        {
                            EditorGUI.indentLevel++;
                            for (int index = 0; index < animatorIK.ikData.Length; index++)
                            {
                                {
                                    EditorGUI.BeginChangeCheck();
                                    EditorGUILayout.ToggleLeft(AnimatorIKCore.IKTargetStrings[index], animatorIK.ikData[index].enable);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        animatorIK.ChangeTargetIK((AnimatorIKCore.IKTarget)index);
                                    }
                                }
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                    #endregion
                    #region OriginalIK
                    if (originalIK.ikData != null)
                    {
                        toolBakeIK_OriginalIKFoldout = EditorGUILayout.Foldout(toolBakeIK_OriginalIKFoldout, "Original IK", true);
                        if (toolBakeIK_OriginalIKFoldout)
                        {
                            EditorGUI.indentLevel++;
                            for (int index = 0; index < originalIK.ikData.Count; index++)
                            {
                                {
                                    EditorGUI.BeginChangeCheck();
                                    EditorGUILayout.ToggleLeft(originalIK.ikData[index].name, originalIK.ikData[index].enable);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        originalIK.ChangeTargetIK(index);
                                    }
                                }
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                    #endregion
                }
                EditorGUI.BeginDisabledGroup(disable);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space();
                if (GUILayout.Button("Apply"))
                {
                    ToolsGenarateBakeIK(clip);
                }
                EditorGUILayout.Space();
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
                #endregion
            }
            else if (toolMode == ToolMode.AnimationRigging)
            {
                #region AnimationRigging
#if VERYANIMATION_ANIMATIONRIGGING
                EditorGUI.BeginDisabledGroup(!animationRigging.isValid);
                if (uAnimationClipEditor != null)
                {
                    float firstFrame = toolAnimationRigging_FirstFrame;
                    float lastFrame = toolAnimationRigging_LastFrame;
                    float additivePoseframe = 0.0f;
                    bool changedStart = false;
                    bool changedStop = false;
                    bool changedAdditivePoseframe = false;
                    uAnimationClipEditor.ClipRangeGUI(ref firstFrame, ref lastFrame, out changedStart, out changedStop, false, ref additivePoseframe, out changedAdditivePoseframe);
                    if (changedStart)
                    {
                        Undo.RecordObject(vaw, "Change First Frame");
                        toolAnimationRigging_FirstFrame = Mathf.RoundToInt(firstFrame);
                    }
                    if (changedStop)
                    {
                        Undo.RecordObject(vaw, "Change Last Frame");
                        toolAnimationRigging_LastFrame = Mathf.RoundToInt(lastFrame);
                    }
                }
                bool disable = !animatorIK.ikData.Any(data => data.enable && data.rigConstraint != null);
                {
                    {
                        EditorGUILayout.LabelField("Bake");
                        EditorGUI.indentLevel++;
                        {
                            bool disableBake = !isHuman;
                            EditorGUI.BeginDisabledGroup(disableBake);
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft(new GUIContent("Hand IK", "Animator HandT and HandQ"), toolAnimationRigging_BakeHandIK);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Bake Flag");
                                toolAnimationRigging_BakeHandIK = flag;
                            }
                            EditorGUI.EndDisabledGroup();
                        }
                        {
                            bool disableBake = !isHuman;
                            EditorGUI.BeginDisabledGroup(disableBake);
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft(new GUIContent("Foot IK", "Animator FootT and FootQ"), toolAnimationRigging_BakeFootIK);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Bake Flag");
                                toolAnimationRigging_BakeFootIK = flag;
                            }
                            EditorGUI.EndDisabledGroup();
                        }
                        EditorGUI.indentLevel--;
                    }
                    #region AnimatorIK
                    if (isHuman && animatorIK.ikData != null)
                    {
                        toolAnimationRigging_AnimatorIKFoldout = EditorGUILayout.Foldout(toolAnimationRigging_AnimatorIKFoldout, "Animator IK", true);
                        if (toolAnimationRigging_AnimatorIKFoldout)
                        {
                            EditorGUI.indentLevel++;
                            for (int index = 0; index < animatorIK.ikData.Length; index++)
                            {
                                var data = animatorIK.ikData[index];
                                if (data.rigConstraint == null)
                                    continue;
                                EditorGUILayout.BeginHorizontal();
                                {
                                    EditorGUI.BeginChangeCheck();
                                    EditorGUILayout.ToggleLeft(AnimatorIKCore.IKTargetStrings[index], data.enable);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        animatorIK.ChangeTargetIK((AnimatorIKCore.IKTarget)index);
                                    }
                                }
                                GUILayout.FlexibleSpace();
                                {
                                    EditorGUILayout.LabelField(data.rigConstraintGUIContent, vaw.guiStyleMiddleRightGreyMiniLabel);
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                    #endregion
                }
                EditorGUI.BeginDisabledGroup(disable);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space();
                if (GUILayout.Button("Clear"))
                {
                    ToolsClearAnimationRigging(clip);
                }
                EditorGUILayout.Space();
                if (GUILayout.Button("Genarate"))
                {
                    ToolsGenarateAnimationRigging(clip);
                }
                EditorGUILayout.Space();
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
                EditorGUI.EndDisabledGroup();
#endif
                #endregion
            }
            else if (toolMode == ToolMode.HumanoidIK)
            {
                #region HumanoidIK
                EditorGUI.BeginDisabledGroup(!isHuman || !clip.isHumanMotion);
                if (uAnimationClipEditor != null)
                {
                    float firstFrame = toolHumanoidIK_FirstFrame;
                    float lastFrame = toolHumanoidIK_LastFrame;
                    float additivePoseframe = 0.0f;
                    bool changedStart = false;
                    bool changedStop = false;
                    bool changedAdditivePoseframe = false;
                    uAnimationClipEditor.ClipRangeGUI(ref firstFrame, ref lastFrame, out changedStart, out changedStop, false, ref additivePoseframe, out changedAdditivePoseframe);
                    if (changedStart)
                    {
                        Undo.RecordObject(vaw, "Change First Frame");
                        toolHumanoidIK_FirstFrame = Mathf.RoundToInt(firstFrame);
                    }
                    if (changedStop)
                    {
                        Undo.RecordObject(vaw, "Change Last Frame");
                        toolHumanoidIK_LastFrame = Mathf.RoundToInt(lastFrame);
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Hand IK", toolHumanoidIK_Hand);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change IK Curve setting");
                        toolHumanoidIK_Hand = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Foot IK", toolHumanoidIK_Foot);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change IK Curve setting");
                        toolHumanoidIK_Foot = flag;
                    }
                }
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(!isHuman || !clip.isHumanMotion || (!toolHumanoidIK_Hand && !toolHumanoidIK_Foot));
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space();
                if (GUILayout.Button("Clear"))
                {
                    ToolsClearHumanoidIK(clip);
                }
                EditorGUILayout.Space();
                if (GUILayout.Button("Genarate"))
                {
                    ToolsGenarateHumanoidIK(clip);
                }
                EditorGUILayout.Space();
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
                if (!isHuman || !clip.isHumanMotion)
                {
                    EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpToolsHumanoidWarning), MessageType.Warning);
                }
                #endregion
            }
            else if (toolMode == ToolMode.RootMotion)
            {
                #region RootMotion
                {
                    EditorGUI.BeginChangeCheck();
                    var mode = (RootMotionMode)GUILayout.Toolbar((int)toolRootMotion_Mode, RootMotionModeStrings, EditorStyles.miniButton);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Root Motion Mode");
                        toolRootMotion_Mode = mode;
                    }
                }
                EditorGUILayout.Space();
                if (toolRootMotion_Mode == RootMotionMode.MotionCurves)
                {
                    #region MotionCurves
                    var disable = vaw.animator == null;
                    EditorGUI.BeginDisabledGroup(disable);

                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.Space();
                        var index = -1;
                        if (selectionMotionTool)
                        {
                            if (Tools.current != Tool.View)
                            {
                                if (CurrentTool() == Tool.Move) index = 0;
                                else index = 1;
                            }
                        }
                        EditorGUI.BeginChangeCheck();
                        index = GUILayout.Toolbar(index, vaw.guiContentMoveRotateTools);
                        if (EditorGUI.EndChangeCheck())
                        {
                            switch (index)
                            {
                            case 0: lastTool = Tool.Move; break;
                            default: lastTool = Tool.Rotate; break;
                            }
                            SelectMotionTool();
                        }
                        EditorGUILayout.Space();
                        EditorGUILayout.EndHorizontal();
                    }

                    GUILayout.Space(24f);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Clear"))
                    {
                        ToolsRootMotionMotionClear(clip);
                    }
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Genarate"))
                    {
                        ToolsRootMotionMotionGenerate(clip);
                        SetAnimationWindowSynchroSelection(AnimationCurveBindingAnimatorMotionT);
                        SetAnimationWindowSynchroSelection(AnimationCurveBindingAnimatorMotionQ);
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.Space();
                    if (vaw.animator == null)
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpToolsNotAnimatorWarning), MessageType.Warning);
                    }
                    #endregion
                }
                else if (toolRootMotion_Mode == RootMotionMode.RootCurves)
                {
                    #region RootCurves
                    var disable = isHuman || rootMotionBoneIndex < 0 || vaw.animator == null;
                    EditorGUI.BeginDisabledGroup(disable);

                    GUILayout.Space(24f);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Clear"))
                    {
                        ToolsRootMotionRootClear(clip);
                    }
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Genarate"))
                    {
                        ToolsRootMotionRootGenerate(clip);
                        SetAnimationWindowSynchroSelection(AnimationCurveBindingAnimatorRootT);
                        SetAnimationWindowSynchroSelection(AnimationCurveBindingAnimatorRootQ);
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.Space();
                    if (disable)
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpToolsGenericAndRootNodeWarning), MessageType.Warning);
                    }
                    #endregion
                }
                #endregion
            }
            else if (toolMode == ToolMode.ParameterRelatedCurves)
            {
                #region ParameterRelatedCurves
                if (e.type == EventType.Layout)
                {
                    ParameterRelatedCurveUpdateList();
                }
                if (toolParameterRelatedCurve_List != null)
                {
                    toolParameterRelatedCurve_List.DoLayoutList();
                }
                if (clip.legacy || vaw.animator == null)
                {
                    EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpToolsNotAnimatorWarning), MessageType.Warning);
                }
                #endregion
            }
            else if (toolMode == ToolMode.RotationCurveInterpolation)
            {
                #region RotationCurveInterpolation
                {
                    EditorGUI.BeginChangeCheck();
                    var mode = (RotationCurveInterpolationMode)EditorGUILayout.EnumPopup("Interpolation", toolRotationInterpolation_Mode);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Rotation Curve Interpolation setting");
                        toolRotationInterpolation_Mode = mode;
                    }
                }
                if (toolRotationInterpolation_Mode == RotationCurveInterpolationMode.EulerAngles)
                {
                    EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpToolsRotationCurveInterpolationEulerAnglesWarning), MessageType.Warning);
                }
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Convert"))
                    {
                        ToolsRotationCurveInterpolation(clip, toolRotationInterpolation_Mode);
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            else if (toolMode == ToolMode.KeyframeReduction)
            {
                #region KeyframeReduction
                {
                    {
                        EditorGUILayout.BeginHorizontal();
                        {
                            EditorGUILayout.LabelField("Rotation Error", GUILayout.Width(150f));
                            EditorGUI.BeginChangeCheck();
                            var param = EditorGUILayout.FloatField(toolKeyframeReduction_RotationError, GUILayout.Width(100f));
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Keyframe Reduction setting");
                                toolKeyframeReduction_RotationError = param;
                            }
                        }
                        EditorGUILayout.Space();
                        {
                            if (GUILayout.Button("Reset"))
                            {
                                Undo.RecordObject(vaw, "Change Keyframe Reduction setting");
                                toolKeyframeReduction_RotationError = 0.5f;
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    {
                        EditorGUILayout.BeginHorizontal();
                        {
                            EditorGUILayout.LabelField("Position Error", GUILayout.Width(150f));
                            EditorGUI.BeginChangeCheck();
                            var param = EditorGUILayout.FloatField(toolKeyframeReduction_PositionError, GUILayout.Width(100f));
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Keyframe Reduction setting");
                                toolKeyframeReduction_PositionError = param;
                            }
                        }
                        EditorGUILayout.Space();
                        {
                            if (GUILayout.Button("Reset"))
                            {
                                Undo.RecordObject(vaw, "Change Keyframe Reduction setting");
                                toolKeyframeReduction_PositionError = 0.5f;
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    {
                        EditorGUILayout.BeginHorizontal();
                        {
                            EditorGUILayout.LabelField("Scale and Others Error", GUILayout.Width(150f));
                            EditorGUI.BeginChangeCheck();
                            var param = EditorGUILayout.FloatField(toolKeyframeReduction_ScaleAndOthersError, GUILayout.Width(100f));
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Keyframe Reduction setting");
                                toolKeyframeReduction_ScaleAndOthersError = param;
                            }
                        }
                        EditorGUILayout.Space();
                        {
                            if (GUILayout.Button("Reset"))
                            {
                                Undo.RecordObject(vaw, "Change Keyframe Reduction setting");
                                toolKeyframeReduction_ScaleAndOthersError = 0.5f;
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    {
                        EditorGUI.BeginChangeCheck();
                        var flag = EditorGUILayout.ToggleLeft(new GUIContent("Humanoid Curves", "Animator Type"), toolKeyframeReduction_EnableHumanoid);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change Keyframe Reduction setting");
                            toolKeyframeReduction_EnableHumanoid = flag;
                        }
                    }
                    if (toolKeyframeReduction_EnableHumanoid)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUI.BeginChangeCheck();
                        var flag = EditorGUILayout.ToggleLeft(Language.GetContent(Language.Help.ToolsKeyframeReductionRootAndIKGoalCurves), toolKeyframeReduction_EnableHumanoidRootAndIKGoal);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change Keyframe Reduction setting");
                            toolKeyframeReduction_EnableHumanoidRootAndIKGoal = flag;
                        }
                        EditorGUI.indentLevel--;
                    }
                    {
                        EditorGUI.BeginChangeCheck();
                        var flag = EditorGUILayout.ToggleLeft(new GUIContent("Generic Curves", "Transform Type"), toolKeyframeReduction_EnableGeneric);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change Keyframe Reduction setting");
                            toolKeyframeReduction_EnableGeneric = flag;
                        }
                    }
                    {
                        EditorGUI.BeginChangeCheck();
                        var flag = EditorGUILayout.ToggleLeft(new GUIContent("Other Curves", "Anything Type"), toolKeyframeReduction_EnableOther);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change Keyframe Reduction setting");
                            toolKeyframeReduction_EnableOther = flag;
                        }
                    }
                    {
                        EditorGUI.BeginDisabledGroup(!toolKeyframeReduction_EnableHumanoid && !toolKeyframeReduction_EnableGeneric && !toolKeyframeReduction_EnableOther);
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.Space();
                        if (GUILayout.Button("Reduction"))
                        {
                            ToolsKeyframeReduction(clip);
                        }
                        EditorGUILayout.Space();
                        EditorGUILayout.EndHorizontal();
                        EditorGUI.EndDisabledGroup();
                    }
                }
                #endregion
            }
            else if (toolMode == ToolMode.EnsureQuaternionContinuity)
            {
                #region EnsureQuaternionContinuity
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Execute"))
                    {
                        ToolsEnsureQuaternionContinuity(clip);
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            else if (toolMode == ToolMode.Cleanup)
            {
                #region Cleanup
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft(new GUIContent("Remove Animator Root Curves", "RootT, RootQ"), toolCleanup_RemoveRoot);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveRoot = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft(new GUIContent("Remove Animator IK Goal Curves", "LeftHandT, LeftHandQ,\nRightHandT, RightHandQ,\nLeftFootT, LeftFootQ,\nRightFootT, RightFootQ"), toolCleanup_RemoveIK);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveIK = flag;
                    }
                }
                {
                    string tooltip = "LeftUpperLegTDOF, RightUpperLegTDOF,\nSpineTDOF, ChestTDOF, NeckTDOF,\nLeftShoulderTDOF, RightShoulderTDOF,\nUpperChestTDOF";

                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft(new GUIContent("Remove Animator TDOF Curves", tooltip), toolCleanup_RemoveTDOF);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveTDOF = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft(new GUIContent("Remove Animator Motion Curves", "MotionT, MotionQ"), toolCleanup_RemoveMotion);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveMotion = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Remove Animator Finger Curves", toolCleanup_RemoveFinger);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveFinger = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Remove Animator Eye Curves", toolCleanup_RemoveEyes);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveEyes = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Remove Animator Jaw Curve", toolCleanup_RemoveJaw);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveJaw = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Remove Animator Toe Curves", toolCleanup_RemoveToes);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveToes = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Remove Transform Position Curves", toolCleanup_RemoveTransformPosition);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveTransformPosition = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Remove Transform Rotation Curves", toolCleanup_RemoveTransformRotation);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveTransformRotation = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Remove Transform Scale Curves", toolCleanup_RemoveTransformScale);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveTransformScale = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Remove BlendShape Curves", toolCleanup_RemoveBlendShape);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveBlendShape = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Remove Object Reference Curves", toolCleanup_RemoveObjectReference);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveObjectReference = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Remove Animation Events", toolCleanup_RemoveEvent);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveEvent = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Remove Missing Curves", toolCleanup_RemoveMissing);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveMissing = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Remove Humanoid and Generic conflict Curves", toolCleanup_RemoveHumanoidConflict);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveHumanoidConflict = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft("Remove Root motion conflict Curves", toolCleanup_RemoveRootMotionConflict);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveRootMotionConflict = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.ToggleLeft(new GUIContent("Remove Unnecessary Curves", "Unnecessary curves are deleted in the current state.\nThis process works with reference to 'Write Defaults' in Animator State and 'Apply Root Motion' in Animator."), toolCleanup_RemoveUnnecessary);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Cleanup setting");
                        toolCleanup_RemoveUnnecessary = flag;
                    }
                }
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    EditorGUI.BeginDisabledGroup(!toolCleanup_RemoveRoot && !toolCleanup_RemoveIK && !toolCleanup_RemoveTDOF && !toolCleanup_RemoveMotion &&
                                                    !toolCleanup_RemoveFinger && !toolCleanup_RemoveEyes && !toolCleanup_RemoveJaw && !toolCleanup_RemoveToes &&
                                                    !toolCleanup_RemoveTransformPosition && !toolCleanup_RemoveTransformRotation && !toolCleanup_RemoveTransformScale && !toolCleanup_RemoveBlendShape &&
                                                    !toolCleanup_RemoveObjectReference && !toolCleanup_RemoveEvent && !toolCleanup_RemoveMissing && !toolCleanup_RemoveHumanoidConflict && !toolCleanup_RemoveRootMotionConflict && !toolCleanup_RemoveUnnecessary);
                    if (GUILayout.Button("Cleanup"))
                    {
                        ToolsCleanup(clip);
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            else if (toolMode == ToolMode.FixErrors)
            {
                #region FixErrors
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Fix"))
                    {
                        ToolsFixErrors(clip);
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            else if (toolMode == ToolMode.AnimCompression)
            {
                #region AnimCompression
                {
                    {
                        EditorGUI.BeginChangeCheck();
                        var flag = EditorGUILayout.Toggle(Language.GetContent(Language.Help.ToolsAnimCompressionCompressed), toolAnimCompression_Compressed);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change Anim Compression setting");
                            toolAnimCompression_Compressed = flag;
                            if (toolAnimCompression_Compressed)
                                toolAnimCompression_UseHighQualityCurve = true;
                        }
                    }
                    {
                        EditorGUI.BeginDisabledGroup(clip.legacy);
                        EditorGUI.BeginChangeCheck();
                        var flag = EditorGUILayout.Toggle(Language.GetContent(Language.Help.ToolsAnimCompressionUseHighQualityCurve), toolAnimCompression_UseHighQualityCurve);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change Anim Compression setting");
                            toolAnimCompression_UseHighQualityCurve = flag;
                            if (!toolAnimCompression_UseHighQualityCurve)
                                toolAnimCompression_Compressed = false;
                        }
                        EditorGUI.EndDisabledGroup();
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Set Current Clip"))
                    {
                        ToolsAnimCompression(clip, false);
                    }
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Set All Clips"))
                    {
                        ToolsAnimCompression(clip, true);
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            else if (toolMode == ToolMode.Export)
            {
                #region Export
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.Toggle("Active Only", toolExport_ActiveOnly);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Export setting");
                        toolExport_ActiveOnly = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var flag = EditorGUILayout.Toggle("Export Mesh", toolExport_Mesh);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Export setting");
                        toolExport_Mesh = flag;
                    }
                }
                {
                    EditorGUI.BeginChangeCheck();
                    var mode = (ExportAnimationMode)EditorGUILayout.EnumPopup("Export Animation", toolExport_AnimationMode);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change Export setting");
                        toolExport_AnimationMode = mode;
                    }
                }
                if (toolExport_AnimationMode != ExportAnimationMode.None)
                {
                    EditorGUILayout.LabelField("Bake");
                    EditorGUI.indentLevel++;
                    if (isHuman)
                    {
                        EditorGUI.BeginChangeCheck();
                        var flag = EditorGUILayout.Toggle(new GUIContent("Foot IK", "Activates feet IK bake."), toolExport_BakeFootIK);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change Bake Flag");
                            toolExport_BakeFootIK = flag;
                        }
                    }
#if VERYANIMATION_ANIMATIONRIGGING
                    {
                        bool disableBake = !animationRigging.isValid;
                        EditorGUI.BeginDisabledGroup(disableBake);
                        EditorGUI.BeginChangeCheck();
                        var flag = EditorGUILayout.Toggle("Animation Rigging", toolExport_BakeAnimationRigging);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change Bake Flag");
                            toolExport_BakeAnimationRigging = flag;
                        }
                        EditorGUI.EndDisabledGroup();
                    }
#endif
                    EditorGUI.indentLevel--;
                }
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Export"))
                    {
                        ToolsExport();
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }

        public void DuplicateAndReplace()
        {
            var assetPath = string.Format("{0}/{1}.anim", EditorCommon.GetAssetPath(currentClip), currentClip.name);
            var uniquePath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            string path = EditorUtility.SaveFilePanel("Duplicate", Path.GetDirectoryName(uniquePath), Path.GetFileName(uniquePath), "anim");
            if (string.IsNullOrEmpty(path))
                return;
            if (!path.StartsWith(Application.dataPath))
            {
                EditorCommon.SaveInsideAssetsFolderDisplayDialog();
                return;
            }
            else
            {
                try
                {
                    VeryAnimationWindow.CustomAssetModificationProcessor.Pause();
                    path = FileUtil.GetProjectRelativePath(path);
                    AssetDatabase.CreateAsset(AnimationClip.Instantiate(currentClip), path);
                    var newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    #region Extra
                    {
                        ToolsReductionCurve(newClip);

                        ToolsRotationCurveInterpolation(newClip, RotationCurveInterpolationMode.Quaternion);
                    }
                    #endregion

                    bool replaced = false;
                    if (uAw.GetLinkedWithTimeline())
                    {
                        #region Timeline
#if VERYANIMATION_TIMELINE
                        var timelineClip = uAw.GetTimelineAnimationClip();
                        if (timelineClip == currentClip)
                        {
                            uAw.SetTimelineAnimationClip(newClip, "Duplicate and Replace");
                            uAw.EditSequencerClip(uAw.GetTimelineClip());
                            replaced = true;
                        }
#else
                        Assert.IsTrue(false);
#endif
                        #endregion
                    }
                    else
                    {
                        #region Animator
                        if (vaw.animator != null && vaw.animator.runtimeAnimatorController != null)
                        {
                            var ac = EditorCommon.GetAnimatorController(vaw.animator);
                            #region AnimatorOverrideController
                            if (vaw.animator.runtimeAnimatorController is AnimatorOverrideController)
                            {
                                var owc = vaw.animator.runtimeAnimatorController as AnimatorOverrideController;
                                {
                                    List<KeyValuePair<AnimationClip, AnimationClip>> srcList = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                                    owc.GetOverrides(srcList);
                                    List<KeyValuePair<AnimationClip, AnimationClip>> dstList = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                                    bool changed = false;
                                    foreach (var pair in srcList)
                                    {
                                        if (pair.Key == currentClip || pair.Value == currentClip)
                                            changed = true;
                                        dstList.Add(new KeyValuePair<AnimationClip, AnimationClip>(pair.Key != currentClip ? pair.Key : newClip,
                                                                                                    pair.Value != currentClip ? pair.Value : newClip));
                                    }
                                    if (changed)
                                    {
                                        owc.ApplyOverrides(dstList);
                                        replaced = true;
                                    }
                                }
                            }
                            #endregion
                            #region AnimatorControllerLayer
                            if (ac != null)
                            {
                                foreach (UnityEditor.Animations.AnimatorControllerLayer layer in ac.layers)
                                {
                                    Action<AnimatorStateMachine> ReplaceStateMachine = null;
                                    ReplaceStateMachine = (stateMachine) =>
                                    {
                                        foreach (var state in stateMachine.states)
                                        {
                                            if (state.state.motion is UnityEditor.Animations.BlendTree)
                                            {
                                                Undo.RecordObject(state.state, "Duplicate and Replace");
                                                Action<UnityEditor.Animations.BlendTree> ReplaceBlendTree = null;
                                                ReplaceBlendTree = (blendTree) =>
                                                {
                                                    if (blendTree.children == null) return;
                                                    Undo.RecordObject(blendTree, "Duplicate and Replace");
                                                    var children = blendTree.children;
                                                    for (int i = 0; i < children.Length; i++)
                                                    {
                                                        if (children[i].motion is UnityEditor.Animations.BlendTree)
                                                        {
                                                            ReplaceBlendTree(children[i].motion as UnityEditor.Animations.BlendTree);
                                                        }
                                                        else
                                                        {
                                                            if (children[i].motion == currentClip)
                                                            {
                                                                children[i].motion = newClip;
                                                                replaced = true;
                                                            }
                                                        }
                                                    }
                                                    blendTree.children = children;
                                                };
                                                ReplaceBlendTree(state.state.motion as UnityEditor.Animations.BlendTree);
                                            }
                                            else
                                            {
                                                if (state.state.motion == currentClip)
                                                {
                                                    Undo.RecordObject(state.state, "Duplicate and Replace");
                                                    state.state.motion = newClip;
                                                    replaced = true;
                                                }
                                            }
                                        }
                                        foreach (var childStateMachine in stateMachine.stateMachines)
                                        {
                                            ReplaceStateMachine(childStateMachine.stateMachine);
                                        }
                                    };
                                    ReplaceStateMachine(layer.stateMachine);
                                }
                            }
                            #endregion
                        }
                        #endregion
                        #region Animation
                        if (vaw.animation != null)
                        {
                            Undo.RecordObject(vaw.animation, "Duplicate and Replace");
                            bool changed = false;
                            var animations = AnimationUtility.GetAnimationClips(vaw.gameObject);
                            for (int i = 0; i < animations.Length; i++)
                            {
                                if (animations[i] == currentClip)
                                {
                                    animations[i] = newClip;
                                    changed = true;
                                }
                            }
                            if (vaw.animation.clip == currentClip)
                            {
                                vaw.animation.clip = newClip;
                                changed = true;
                            }
                            if (changed)
                            {
                                AnimationUtility.SetAnimationClips(vaw.animation, animations);
                                replaced = true;
                            }
                        }
                        #endregion

                        if (replaced)
                            uAw.SetSelectionAnimationClip(newClip);
                    }

                    if (!replaced)
                        Debug.LogWarningFormat(Language.GetText(Language.Help.LogAnimationClipReferenceReplaceError), newClip);

                    ClearEditorCurveCache();
                    OnHierarchyWindowChanged();
                    SetUpdateSampleAnimation();
                    uAw.ForceRefresh();
                }
                finally
                {
                    VeryAnimationWindow.CustomAssetModificationProcessor.Resume();
                }
            }
        }

        private void ToolsReset()
        {
            var lastFrame = GetLastFrame();
            toolCreateNewKeyframe_FirstFrame = 0;
            toolCreateNewKeyframe_LastFrame = lastFrame;
            toolBakeIK_FirstFrame = 0;
            toolBakeIK_LastFrame = lastFrame;
            toolHumanoidIK_FirstFrame = 0;
            toolHumanoidIK_LastFrame = lastFrame;
            toolAnimationRigging_FirstFrame = 0;
            toolAnimationRigging_LastFrame = lastFrame;
            toolCopy_FirstFrame = 0;
            toolCopy_LastFrame = lastFrame;
            toolCopy_WriteFrame = lastFrame + 1;
            toolTrim_FirstFrame = 0;
            toolTrim_LastFrame = lastFrame;
            if (currentClip != null)
            {
                var so = new SerializedObject(currentClip);
                toolAnimCompression_Compressed = so.FindProperty("m_Compressed").boolValue;
                toolAnimCompression_UseHighQualityCurve = so.FindProperty("m_UseHighQualityCurve").boolValue;
            }

            ToolsParameterRelatedCurveReset();
        }
        private void ToolsParameterRelatedCurveReset()
        {
            toolParameterRelatedCurve_DataList = null;
            toolParameterRelatedCurve_Update = true;
            toolParameterRelatedCurve_List = null;
        }

        private void ParameterRelatedCurveUpdateList()
        {
            if (!toolParameterRelatedCurve_Update)
                return;

            Action UpdateEnableFlagAll = () =>
            {
                if (toolParameterRelatedCurve_DataList == null) return;
                var ac = EditorCommon.GetAnimatorController(vaw.animator);
                if (ac == null) return;
                var parameters = ac.parameters;
                foreach (var data in toolParameterRelatedCurve_DataList)
                {
                    var binding = AnimationCurveBindingAnimatorCustom(data.propertyName);
                    var curve = GetEditorCurveCache(binding);
                    data.enableAnimationCurve = curve != null;
                    data.parameterIndex = ArrayUtility.FindIndex(parameters, (x) => x.name == data.propertyName);
                    data.enableAnimatorParameter = data.parameterIndex >= 0 && parameters[data.parameterIndex].type == UnityEngine.AnimatorControllerParameterType.Float;
                }
                SetUpdateSampleAnimation();
                SetAnimationWindowRefresh(AnimationWindowStateRefreshType.Everything);
            };
            Action<int> UpdateEnableFlag = (index) =>
            {
                if (toolParameterRelatedCurve_DataList == null) return;
                var ac = EditorCommon.GetAnimatorController(vaw.animator);
                if (ac == null) return;
                var parameters = ac.parameters;
                {
                    var data = toolParameterRelatedCurve_DataList[index];
                    var binding = AnimationCurveBindingAnimatorCustom(data.propertyName);
                    var curve = GetEditorCurveCache(binding);
                    data.enableAnimationCurve = curve != null;
                    data.parameterIndex = ArrayUtility.FindIndex(parameters, (x) => x.name == data.propertyName);
                    data.enableAnimatorParameter = data.parameterIndex >= 0 && parameters[data.parameterIndex].type == UnityEngine.AnimatorControllerParameterType.Float;
                }
                SetUpdateSampleAnimation();
                SetAnimationWindowRefresh(AnimationWindowStateRefreshType.Everything);
            };

            if (toolParameterRelatedCurve_DataList == null)
                toolParameterRelatedCurve_DataList = new List<ParameterRelatedData>();
            else
                toolParameterRelatedCurve_DataList.Clear();
            {
                var ac = EditorCommon.GetAnimatorController(vaw.animator);
                if (ac != null)
                {
                    var parameters = ac.parameters;
                    foreach (var binding in AnimationUtility.GetCurveBindings(currentClip))
                    {
                        if (binding.type != typeof(Animator)) continue;
                        if (IsAnimatorReservedPropertyName(binding.propertyName)) continue;
                        bool ready = false;
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            if (parameters[i].type != UnityEngine.AnimatorControllerParameterType.Float) continue;
                            if (binding.propertyName == parameters[i].name)
                            {
                                toolParameterRelatedCurve_DataList.Add(new ParameterRelatedData()
                                {
                                    propertyName = binding.propertyName,
                                    parameterIndex = i,
                                    enableAnimationCurve = true,
                                    enableAnimatorParameter = true
                                });
                                ready = true;
                                break;
                            }
                        }
                        if (!ready)
                        {
                            toolParameterRelatedCurve_DataList.Add(new ParameterRelatedData()
                            {
                                propertyName = binding.propertyName,
                                parameterIndex = -1,
                                enableAnimationCurve = true,
                                enableAnimatorParameter = false,
                            });
                        }
                    }
                }
            }
            toolParameterRelatedCurve_List = new ReorderableList(toolParameterRelatedCurve_DataList, typeof(ParameterRelatedData), false, true, true, true);
            toolParameterRelatedCurve_List.drawHeaderCallback = (Rect rect) =>
            {
                float x = rect.x;
                {
                    const float Rate = 0.4f;
                    var r = rect;
                    r.x = x;
                    r.width = rect.width * Rate;
                    x += r.width;
                    EditorGUI.LabelField(r, "Name", vaw.guiStyleCenterAlignLabel);
                }
                {
                    const float Rate = 0.2f;
                    var r = rect;
                    r.x = x;
                    r.width = rect.width * Rate;
                    x += r.width;
                    EditorGUI.LabelField(r, new GUIContent("Curve", "Animation Curve"), vaw.guiStyleCenterAlignLabel);
                }
                {
                    const float Rate = 0.2f;
                    var r = rect;
                    r.x = x;
                    r.width = rect.width * Rate;
                    x += r.width;
                    EditorGUI.LabelField(r, new GUIContent("Parameter", "Animator Controller Parameter"), vaw.guiStyleCenterAlignLabel);
                }
                {
                    const float Rate = 0.2f;
                    var r = rect;
                    r.x = x;
                    r.width = rect.width * Rate;
                    x += r.width;
                    EditorGUI.LabelField(r, "Value", vaw.guiStyleCenterAlignLabel);
                }
            };
            toolParameterRelatedCurve_List.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (index >= toolParameterRelatedCurve_DataList.Count)
                    return;

                EditorGUI.BeginDisabledGroup((currentClip.hideFlags & HideFlags.NotEditable) != HideFlags.None);

                float x = rect.x;
                {
                    const float Rate = 0.4f;
                    var r = rect;
                    r.x = x;
                    r.y += 2;
                    r.height -= 4;
                    r.width = rect.width * Rate;
                    x += r.width;
                    if (index == toolParameterRelatedCurve_List.index)
                    {
                        EditorGUI.BeginChangeCheck();
                        var text = EditorGUI.TextField(r, toolParameterRelatedCurve_DataList[index].propertyName);
                        if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(text))
                        {
                            if (ToolsCommonBefore(currentClip, "Change Parameter Related Curve"))
                            {
                                var ac = EditorCommon.GetAnimatorController(vaw.animator);
                                if (ac != null)
                                {
                                    {
                                        var origText = text;
                                        if (IsAnimatorReservedPropertyName(text))
                                            text += " 0";
                                        text = ac.MakeUniqueParameterName(text);
                                        if (origText != text)
                                        {
                                            Debug.LogWarningFormat(Language.GetText(Language.Help.LogParameterRelatedCurveNameChanged), origText, text);
                                        }
                                    }
                                    Undo.RecordObject(vaw, "Change Parameter Related Curve");
                                    Undo.RecordObject(ac, "Change Parameter Related Curve");
                                    {
                                        var binding = AnimationCurveBindingAnimatorCustom(toolParameterRelatedCurve_DataList[index].propertyName);
                                        var curve = AnimationUtility.GetEditorCurve(currentClip, binding);
                                        if (curve != null)
                                        {
                                            AnimationUtility.SetEditorCurve(currentClip, binding, null);
                                            binding = AnimationCurveBindingAnimatorCustom(text);
                                            AnimationUtility.SetEditorCurve(currentClip, binding, curve);
                                        }
                                    }
                                    {
                                        var parameters = ac.parameters;
                                        int paramIndex = toolParameterRelatedCurve_DataList[index].parameterIndex;
                                        if (paramIndex >= 0 && paramIndex < parameters.Length)
                                        {
                                            parameters[paramIndex].name = text;
                                            ac.parameters = parameters;
                                        }
                                    }
                                    toolParameterRelatedCurve_DataList[index].propertyName = text;
                                    UpdateEnableFlag(index);
                                }
                            }
                        }
                    }
                    else
                    {
                        EditorGUI.LabelField(r, toolParameterRelatedCurve_DataList[index].propertyName);
                    }
                }
                {
                    const float Rate = 0.2f;
                    var r = rect;
                    r.x = x;
                    r.y += 2;
                    r.height -= 4;
                    r.width = rect.width * Rate;
                    x += r.width;
                    if (toolParameterRelatedCurve_DataList[index].enableAnimationCurve)
                        EditorGUI.LabelField(r, "Ready", vaw.guiStyleCenterAlignLabel);
                    else
                        EditorGUI.LabelField(r, "Missing", vaw.guiStyleCenterAlignYellowLabel);
                }
                {
                    const float Rate = 0.2f;
                    var r = rect;
                    r.x = x;
                    r.y += 2;
                    r.height -= 4;
                    r.width = rect.width * Rate;
                    x += r.width;
                    if (toolParameterRelatedCurve_DataList[index].enableAnimatorParameter)
                        EditorGUI.LabelField(r, "Ready", vaw.guiStyleCenterAlignLabel);
                    else
                        EditorGUI.LabelField(r, "Missing", vaw.guiStyleCenterAlignYellowLabel);
                }
                {
                    const float Rate = 0.2f;
                    var r = rect;
                    r.x = x;
                    r.y += 2;
                    r.height -= 4;
                    r.width = rect.width * Rate;
                    x += r.width;
                    if (!toolParameterRelatedCurve_DataList[index].enableAnimationCurve || !toolParameterRelatedCurve_DataList[index].enableAnimatorParameter)
                    {
                        if (GUI.Button(r, "Fix"))
                        {
                            if (ToolsCommonBefore(currentClip, "Fix Parameter Related Curve"))
                            {
                                var ac = EditorCommon.GetAnimatorController(vaw.animator);
                                if (ac != null)
                                {
                                    Undo.RecordObject(vaw, "Fix Parameter Related Curve");
                                    Undo.RecordObject(ac, "Fix Parameter Related Curve");
                                    if (!toolParameterRelatedCurve_DataList[index].enableAnimationCurve)
                                    {
                                        var binding = AnimationCurveBindingAnimatorCustom(toolParameterRelatedCurve_DataList[index].propertyName);
                                        var curve = new AnimationCurve();
                                        SetKeyframeTangentModeLinear(curve, curve.AddKey(0f, 0f));
                                        SetKeyframeTangentModeLinear(curve, curve.AddKey(currentClip.length, 1f));
                                        AnimationUtility.SetEditorCurve(currentClip, binding, curve);
                                    }
                                    if (!toolParameterRelatedCurve_DataList[index].enableAnimatorParameter)
                                    {
                                        {
                                            var parameters = ac.parameters;
                                            var paramIndex = ArrayUtility.FindIndex(parameters, (d) => d.name == toolParameterRelatedCurve_DataList[index].propertyName);
                                            if (paramIndex >= 0 && paramIndex < parameters.Length)
                                            {
                                                ac.RemoveParameter(paramIndex);
                                            }
                                        }
                                        ac.AddParameter(toolParameterRelatedCurve_DataList[index].propertyName, UnityEngine.AnimatorControllerParameterType.Float);
                                    }
                                    UpdateEnableFlag(index);
                                }
                            }
                        }
                    }
                    else if (uAvatarPreview != null)
                    {
                        var binding = AnimationCurveBindingAnimatorCustom(toolParameterRelatedCurve_DataList[index].propertyName);
                        var curve = GetEditorCurveCache(binding);
                        if (curve != null)
                        {
                            var value = curve.Evaluate(uAvatarPreview.GetTime());
                            EditorGUI.LabelField(r, value.ToString("F2"), vaw.guiStyleCenterAlignLabel);
                        }
                    }
                }

                EditorGUI.EndDisabledGroup();
            };
            toolParameterRelatedCurve_List.onSelectCallback = (ReorderableList list) =>
            {
                UpdateEnableFlagAll();
                EditorApplication.delayCall += () =>
                {
                    SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { AnimationCurveBindingAnimatorCustom(toolParameterRelatedCurve_DataList[list.index].propertyName) });
                    vaw.Repaint();
                };
            };
            toolParameterRelatedCurve_List.onAddCallback = (ReorderableList list) =>
            {
                if (!ToolsCommonBefore(currentClip, "Add Parameter Related Curve")) return;

                var ac = EditorCommon.GetAnimatorController(vaw.animator);
                if (ac == null)
                    return;

                Undo.RecordObject(vaw, "Add Parameter Related Curve");
                {
                    var name = ac.MakeUniqueParameterName("New Parameter");
                    var data = new ParameterRelatedData() { propertyName = name };
                    {
                        var binding = AnimationCurveBindingAnimatorCustom(name);
                        var curve = new AnimationCurve();
                        SetKeyframeTangentModeLinear(curve, curve.AddKey(0f, 0f));
                        SetKeyframeTangentModeLinear(curve, curve.AddKey(currentClip.length, 1f));
                        AnimationUtility.SetEditorCurve(currentClip, binding, curve);
                    }
                    {
                        ac.AddParameter(name, UnityEngine.AnimatorControllerParameterType.Float);
                    }
                    toolParameterRelatedCurve_DataList.Add(data);
                }
                toolParameterRelatedCurve_Update = true;
                EditorApplication.delayCall += () =>
                {
                    toolParameterRelatedCurve_List.index = toolParameterRelatedCurve_DataList.Count - 1;
                    SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { AnimationCurveBindingAnimatorCustom(toolParameterRelatedCurve_DataList[toolParameterRelatedCurve_List.index].propertyName) });
                    vaw.Repaint();
                };

                ToolsCommonAfter();
                InternalEditorUtility.RepaintAllViews();
            };
            toolParameterRelatedCurve_List.onCanAddCallback = (ReorderableList list) =>
            {
                var ac = EditorCommon.GetAnimatorController(vaw.animator);
                return (ac != null);
            };
            toolParameterRelatedCurve_List.onRemoveCallback = (ReorderableList list) =>
            {
                if (!ToolsCommonBefore(currentClip, "Add Parameter Related Curve")) return;

                Undo.RecordObject(vaw, "Add Parameter Related Curve");
                {
                    var ac = EditorCommon.GetAnimatorController(vaw.animator);
                    if (ac != null)
                    {
                        var data = toolParameterRelatedCurve_DataList[list.index];
                        {
                            var binding = AnimationCurveBindingAnimatorCustom(data.propertyName);
                            AnimationUtility.SetEditorCurve(currentClip, binding, null);
                        }
                        {
                            var parameters = ac.parameters;
                            data.parameterIndex = ArrayUtility.FindIndex(parameters, (x) => x.name == data.propertyName);
                            if (data.parameterIndex >= 0)
                                ac.RemoveParameter(data.parameterIndex);
                        }
                    }
                }
                toolParameterRelatedCurve_DataList.RemoveAt(list.index);
                toolParameterRelatedCurve_Update = true;

                ToolsCommonAfter();
                InternalEditorUtility.RepaintAllViews();
            };

            UpdateEnableFlagAll();
            toolParameterRelatedCurve_Update = false;
        }

        private void ToolsCurvesWasModifiedStoppedSmoothTangents(float beginTime, float endTime)
        {
            foreach (var pair in curvesWasModifiedStopped)
            {
                if (pair.Value.deleted != AnimationUtility.CurveModifiedType.CurveModified)
                    continue;
                var curve = GetEditorCurveCache(pair.Value.binding);
                if (curve == null)
                    continue;

                for (int i = 0; i < curve.length; i++)
                {
                    if (curve[i].time >= beginTime && curve[i].time <= endTime)
                        curve.SmoothTangents(i, 0f);
                }
                SetEditorCurveCache(pair.Value.binding, curve);
            }
        }

        private bool ToolsCommonBefore(AnimationClip clip, string undoName)
        {
            if (!BeginChangeAnimationCurve(clip, undoName))
                return false;

            uAw.ClearKeySelections();
            ClearEditorCurveCache();
            SetOnCurveWasModifiedStop(true);

            return true;
        }
        private void ToolsCommonAfter()
        {
            SetOnCurveWasModifiedStop(false);
            UpdateSyncEditorCurveClip();
            curvesWasModified.Clear();
            ClearEditorCurveCache();
            SetUpdateSampleAnimation(true);
            ResetUpdateIKtargetAll();
            SetSynchroIKtargetAll();
            uAw.ForceRefresh();
        }

        private void ToolsReductionCurve(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "Reduction Curve")) return;

            try
            {
                bool allWriteDefaults = true;
                if (uAw.GetLinkedWithTimeline())
                {
                    allWriteDefaults = false;
                }
                else
                {
                    ActionAllAnimatorState(clip, (animatorState) =>
                    {
                        if (!animatorState.writeDefaultValues)
                            allWriteDefaults = false;
                    });
                }

                #region It is not necessary if AnimatorState.writeDefaultValues is enabled
                if (allWriteDefaults)
                {
                    const float eps = 0.0001f;

                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    int progressIndex = 0;
                    int progressTotal = bindings.Length;

                    bool[] doneFlags = new bool[bindings.Length];
                    for (int k = 0; k < bindings.Length; k++)
                    {
                        EditorUtility.DisplayProgressBar("Reduction Curve", string.IsNullOrEmpty(bindings[k].path) ? bindings[k].propertyName : bindings[k].path, progressIndex++ / (float)progressTotal);
                        if (doneFlags[k]) continue;
                        doneFlags[k] = true;
                        var curve = AnimationUtility.GetEditorCurve(clip, bindings[k]);
                        if (curve == null) continue;
                        var t = GetTransformFromPath(bindings[k].path);
                        if (t == null) continue;
                        if (bindings[k].type == typeof(Animator))
                        {
                            #region Animator
                            if (GetMuscleIndexFromCurveBinding(bindings[k]) >= 0)
                            {
                                bool remove = true;
                                for (float time = 0f; time <= clip.length; time += 1f / clip.frameRate)
                                {
                                    if (Mathf.Abs(curve.Evaluate(time)) >= eps)
                                    {
                                        remove = false;
                                        break;
                                    }
                                }
                                if (remove)
                                {
                                    AnimationUtility.SetEditorCurve(clip, bindings[k], null);
                                }
                            }
                            #endregion
                        }
                        else if (bindings[k].type == typeof(Transform))
                        {
                            #region Transform
                            string[] TypeNames =
                            {
                                "m_LocalPosition",
                                "m_LocalRotation",
                                "m_LocalScale",
                                "localEulerAnglesRaw",
                            };
                            int type = -1;
                            for (int i = 0; i < TypeNames.Length; i++)
                            {
                                if (bindings[k].propertyName.StartsWith(TypeNames[i]))
                                {
                                    type = i;
                                    break;
                                }
                            }
                            var boneIndex = GetBoneIndexFromCurveBinding(bindings[k]);
                            if (type >= 0 && boneIndex >= 0)
                            {
                                var save = transformPoseSave.GetOriginalTransform(bones[boneIndex].transform);
                                if (save != null)
                                {
                                    int dofCount = type == 1 ? 4 : 3;
                                    bool remove = true;
                                    int[] indexes = new int[dofCount];
                                    for (int dof = 0; dof < dofCount; dof++)
                                    {
                                        indexes[dof] = ArrayUtility.FindIndex(bindings, (x) => x.type == bindings[k].type && x.path == bindings[k].path &&
                                                                                                x.propertyName == TypeNames[type] + DofIndex2String[dof]);
                                        if (indexes[dof] >= 0)
                                            doneFlags[indexes[dof]] = true;
                                        if (remove && indexes[dof] >= 0)
                                        {
                                            curve = AnimationUtility.GetEditorCurve(clip, bindings[indexes[dof]]);
                                            if (curve != null)
                                            {
                                                float saveValue = 0f;
                                                switch (type)
                                                {
                                                case 0: saveValue = save.localPosition[dof]; break;
                                                case 1: saveValue = save.localRotation[dof]; break;
                                                case 2: saveValue = save.localScale[dof]; break;
                                                case 3: saveValue = save.localRotation.eulerAngles[dof]; break;
                                                }
                                                for (float time = 0f; time <= clip.length; time += 1f / clip.frameRate)
                                                {
                                                    if (Mathf.Abs(curve.Evaluate(time) - saveValue) >= eps)
                                                    {
                                                        remove = false;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (remove)
                                    {
                                        foreach (var index in indexes)
                                        {
                                            if (index >= 0)
                                                AnimationUtility.SetEditorCurve(clip, bindings[index], null);
                                        }
                                    }
                                }
                            }
                            #endregion
                        }
                        else if (bindings[k].type == typeof(SkinnedMeshRenderer))
                        {
                            #region SkinnedMeshRenderer
                            var renderer = t.GetComponent<SkinnedMeshRenderer>();
                            if (renderer != null && renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0)
                            {
                                if (IsBlendShapePropertyName(bindings[k].propertyName))
                                {
                                    var name = PropertyName2BlendShapeName(bindings[k].propertyName);
                                    if (blendShapeWeightSave.IsHaveOriginalWeight(renderer, name))
                                    {
                                        var weight = blendShapeWeightSave.GetOriginalWeight(renderer, name);
                                        bool remove = true;
                                        for (float time = 0f; time <= clip.length; time += 1f / clip.frameRate)
                                        {
                                            if (Mathf.Abs(curve.Evaluate(time) - weight) >= eps)
                                            {
                                                remove = false;
                                                break;
                                            }
                                        }
                                        if (remove)
                                        {
                                            AnimationUtility.SetEditorCurve(clip, bindings[k], null);
                                        }
                                    }
                                }
                            }
                            #endregion
                        }
                    }
                }
                #endregion

                #region Optional bone
                {
                    Action<HumanBodyBones> RemoveMuscleCurve = (hi) =>
                    {
                        for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                        {
                            var mi = HumanTrait.MuscleFromBone((int)hi, dofIndex);
                            if (mi < 0) continue;
                            AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorMuscle(mi), null);
                        }
                    };
                    if (!isHuman || !humanoidHasLeftHand)
                    {
                        for (var hi = HumanBodyBones.LeftThumbProximal; hi <= HumanBodyBones.LeftLittleDistal; hi++)
                            RemoveMuscleCurve(hi);
                    }
                    if (!isHuman || !humanoidHasRightHand)
                    {
                        for (var hi = HumanBodyBones.RightThumbProximal; hi <= HumanBodyBones.RightLittleDistal; hi++)
                            RemoveMuscleCurve(hi);
                    }
                    if (!isHuman || !humanoidHasTDoF)
                    {
                        for (int tdofIndex = 0; tdofIndex < (int)AnimatorTDOFIndex.Total; tdofIndex++)
                        {
                            for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                                AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorTDOF((AnimatorTDOFIndex)tdofIndex, dofIndex), null);
                        }
                    }
                    Action<HumanBodyBones> RemoveNoneMuscleCurve = (hi) =>
                    {
                        if (!isHuman || humanoidBones[(int)hi] == null)
                            RemoveMuscleCurve(hi);
                    };
                    RemoveNoneMuscleCurve(HumanBodyBones.LeftEye);
                    RemoveNoneMuscleCurve(HumanBodyBones.RightEye);
                    RemoveNoneMuscleCurve(HumanBodyBones.Jaw);
                    RemoveNoneMuscleCurve(HumanBodyBones.LeftToes);
                    RemoveNoneMuscleCurve(HumanBodyBones.RightToes);
                }
                #endregion

                #region GenericRootMotion
                if (!isHuman)
                {
                    if (rootMotionBoneIndex >= 0)
                    {
                        if (IsHaveAnimationCurveTransformPosition(0))
                        {
                            for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                                AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingTransformPosition(0, dofIndex), null);
                        }
                        if (IsHaveAnimationCurveTransformRotation(0) != URotationCurveInterpolation.Mode.Undefined)
                        {
                            for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                                AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingTransformRotation(0, dofIndex, URotationCurveInterpolation.Mode.RawEuler), null);
                            for (int dofIndex = 0; dofIndex < 4; dofIndex++)
                                AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingTransformRotation(0, dofIndex, URotationCurveInterpolation.Mode.RawQuaternions), null);
                        }
                    }
                    else
                    {
                        if (IsHaveAnimationCurveAnimatorRootT())
                        {
                            for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                                AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorRootT[dofIndex], null);
                        }
                        if (IsHaveAnimationCurveAnimatorRootQ())
                        {
                            for (int dofIndex = 0; dofIndex < 4; dofIndex++)
                                AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorRootQ[dofIndex], null);
                        }
                    }
                }
                #endregion
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private bool ToolsFixOverRotationCurve(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "Fix Over Rotation Curve")) return false;

            try
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                int progressIndex = 0;
                int progressTotal = bindings.Length;
                #region CurveBindings
                foreach (var binding in bindings)
                {
                    EditorUtility.DisplayProgressBar("Fix Over Rotation Curve", string.IsNullOrEmpty(binding.path) ? binding.propertyName : binding.path, progressIndex++ / (float)progressTotal);
                    if (!IsTransformRotationCurveBinding(binding) || uRotationCurveInterpolation.GetModeFromCurveData(binding) != URotationCurveInterpolation.Mode.RawEuler) continue;
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null) continue;
                    bool update = false;
                    for (int i = 1; i < curve.length; i++)
                    {
                        var power = curve[i].value - curve[i - 1].value;
                        if (Mathf.Abs(power) < 180f) continue;
                        var time = uAw.SnapToFrame(Mathf.Lerp(curve[i].time, curve[i - 1].time, 0.5f), clip.frameRate);
                        if (Mathf.Approximately(time, curve[i].time) || Mathf.Approximately(time, curve[i - 1].time)) continue;
                        AddKeyframe(curve, time, curve.Evaluate(time));
                        update = true;
                        i = 0;
                    }
                    if (update)
                    {
                        AnimationUtility.SetEditorCurve(clip, binding, curve);
                        Debug.LogWarningFormat(Language.GetText(Language.Help.LogFixOverRotationCurve), binding.path, binding.propertyName);
                    }
                }
                #endregion
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();

            return true;
        }

        private struct IKDataSave
        {
            public IKDataSave(AnimatorIKCore.AnimatorIKData ikData)
            {
                position = ikData.position;
                rotation = ikData.rotation;
                worldPosition = ikData.worldPosition;
                worldRotation = ikData.worldRotation;
                swivelRotation = ikData.swivelRotation;
                swivelPosition = ikData.swivelPosition;
            }
            public IKDataSave(OriginalIKCore.OriginalIKData ikData)
            {
                position = ikData.position;
                rotation = ikData.rotation;
                worldPosition = ikData.worldPosition;
                worldRotation = ikData.worldRotation;
                swivelRotation = ikData.swivel;
                swivelPosition = ikData.worldPosition;
            }
            public void Set(AnimatorIKCore.AnimatorIKData ikData)
            {
                if (ikData.spaceType == AnimatorIKCore.AnimatorIKData.SpaceType.Parent)
                {
                    ikData.position = position;
                    ikData.rotation = rotation;
                }
                else
                {
                    ikData.worldPosition = worldPosition;
                    ikData.worldRotation = worldRotation;
                }
                ikData.swivelRotation = swivelRotation;
                ikData.swivelPosition = swivelPosition;
            }
            public void Set(OriginalIKCore.OriginalIKData ikData)
            {
                if (ikData.spaceType == OriginalIKCore.OriginalIKData.SpaceType.Parent)
                {
                    ikData.position = position;
                    ikData.rotation = rotation;
                }
                else
                {
                    ikData.worldPosition = worldPosition;
                    ikData.worldRotation = worldRotation;
                }
                ikData.swivel = swivelRotation;
            }

            public static IKDataSave Lerp(IKDataSave a, IKDataSave b, float t)
            {
                var ikDataSave = new IKDataSave()
                {
                    position = Vector3.Lerp(a.position, b.position, t),
                    rotation = Quaternion.Slerp(a.rotation, b.rotation, t),
                    worldPosition = Vector3.Lerp(a.worldPosition, b.worldPosition, t),
                    worldRotation = Quaternion.Slerp(a.worldRotation, b.worldRotation, t),
                    swivelPosition = Vector3.Lerp(a.swivelPosition, b.swivelPosition, t),
                };
                if (Mathf.Abs(a.swivelRotation - b.swivelRotation) > 180f)
                {
                    var aSwivel = a.swivelRotation;
                    if (aSwivel < 0f) aSwivel += 360f;
                    var bSwivel = b.swivelRotation;
                    if (bSwivel < 0f) bSwivel += 360f;
                    ikDataSave.swivelRotation = Mathf.Lerp(aSwivel, bSwivel, t);
                    while (ikDataSave.swivelRotation < -180f || ikDataSave.swivelRotation > 180f)
                    {
                        if (ikDataSave.swivelRotation > 180f)
                            ikDataSave.swivelRotation -= 360f;
                        else if (ikDataSave.swivelRotation < -180f)
                            ikDataSave.swivelRotation += 360f;
                    }
                }
                else
                {
                    ikDataSave.swivelRotation = Mathf.Lerp(a.swivelRotation, b.swivelRotation, t);
                }
                return ikDataSave;
            }

            public Vector3 position;
            public Quaternion rotation;
            public Vector3 worldPosition;
            public Quaternion worldRotation;
            public float swivelRotation;
            public Vector3 swivelPosition;
        }
        private void ToolsGenarateBakeIK(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "Bake IK")) return;

            if (clip != currentClip) return;

            var saveCurrentTime = uAw.GetCurrentTime();
            try
            {
                var beginTime = uAw.SnapToFrame(toolBakeIK_FirstFrame >= 0 ? toolBakeIK_FirstFrame / clip.frameRate : 0f, clip.frameRate);
                var endTime = uAw.SnapToFrame(toolBakeIK_LastFrame >= 0 ? toolBakeIK_LastFrame / clip.frameRate : clip.length, clip.frameRate);
                int progressIndex = 0;
                int progressTotal = 2;

                Action<float> SampleAnimationBase = (time) =>
                {
                    SetCurrentTime(time);
                    SampleAnimation(time);
                };

                #region AnimatorIK
                EditorUtility.DisplayProgressBar("Bake IK", "Animator IK", progressIndex++ / (float)progressTotal);
                if (isHuman && animatorIK.ikData.Any(data => data.enable))
                {
                    AnimatorIKCore.SynchroSetFlags syncFlags = AnimatorIKCore.SynchroSetFlags.None;
                    {
                        if (toolBakeIK_BakeHandIK) syncFlags |= AnimatorIKCore.SynchroSetFlags.HandIK;
                        if (toolBakeIK_BakeFootIK) syncFlags |= AnimatorIKCore.SynchroSetFlags.FootIK;
                        if (toolBakeIK_BakeAnimationRigging) syncFlags |= AnimatorIKCore.SynchroSetFlags.AnimationRigging;
                    }
                    #region Save
                    IKDataSave[,] ikDataAllSave = null;
                    IKDataSave[] ikDataBeginSave = null, ikDataEndSave = null;
                    if (toolBakeIK_Mode == BakeIKMode.Simple)
                    {
                        ikDataAllSave = new IKDataSave[animatorIK.ikData.Length, GetLastFrame() + 1];
                        for (int frame = toolBakeIK_FirstFrame; frame <= toolBakeIK_LastFrame; frame++)
                        {
                            var time = uAw.GetFrameTime(frame, clip);
                            SampleAnimationBase(time);
                            for (var index = 0; index < animatorIK.ikData.Length; index++)
                            {
                                if (!animatorIK.ikData[index].enable) continue;
                                animatorIK.SynchroSet((AnimatorIKCore.IKTarget)index, syncFlags);
                                ikDataAllSave[index, frame] = new IKDataSave(animatorIK.ikData[index]);
                            }
                        }
                    }
                    else if (toolBakeIK_Mode == BakeIKMode.Interpolation)
                    {
                        ikDataBeginSave = new IKDataSave[animatorIK.ikData.Length];
                        ikDataEndSave = new IKDataSave[animatorIK.ikData.Length];
                        SampleAnimationBase(beginTime);
                        for (var index = 0; index < animatorIK.ikData.Length; index++)
                        {
                            if (!animatorIK.ikData[index].enable) continue;
                            animatorIK.SynchroSet((AnimatorIKCore.IKTarget)index, syncFlags);
                            ikDataBeginSave[index] = new IKDataSave(animatorIK.ikData[index]);
                        }
                        SampleAnimationBase(endTime);
                        for (var index = 0; index < animatorIK.ikData.Length; index++)
                        {
                            if (!animatorIK.ikData[index].enable) continue;
                            animatorIK.SynchroSet((AnimatorIKCore.IKTarget)index, syncFlags);
                            ikDataEndSave[index] = new IKDataSave(animatorIK.ikData[index]);
                        }
                    }
                    #endregion

                    ResetAnimatorRootCorrection();
                    for (int frame = toolBakeIK_FirstFrame; frame <= toolBakeIK_LastFrame; frame++)
                    {
                        var time = uAw.GetFrameTime(frame, clip);

                        SampleAnimationBase(time);

                        if (toolBakeIK_Mode == BakeIKMode.Simple)
                        {
                            #region Simple
                            for (var index = 0; index < animatorIK.ikData.Length; index++)
                            {
                                if (!animatorIK.ikData[index].enable) continue;
                                ikDataAllSave[index, frame].Set(animatorIK.ikData[index]);
                                SetUpdateIKtargetAnimatorIK((AnimatorIKCore.IKTarget)index);
                            }
                            #endregion
                        }
                        else if (toolBakeIK_Mode == BakeIKMode.Interpolation)
                        {
                            #region Interpolation
                            float rate = 0f;
                            if (toolBakeIK_LastFrame - toolBakeIK_FirstFrame > 0)
                                rate = (frame - toolBakeIK_FirstFrame) / (float)(toolBakeIK_LastFrame - toolBakeIK_FirstFrame);

                            for (var index = 0; index < animatorIK.ikData.Length; index++)
                            {
                                if (!animatorIK.ikData[index].enable) continue;
                                var ikDataSave = IKDataSave.Lerp(ikDataBeginSave[index], ikDataEndSave[index], rate);
                                ikDataSave.Set(animatorIK.ikData[index]);
                                SetUpdateIKtargetAnimatorIK((AnimatorIKCore.IKTarget)index);
                            }
                            #endregion
                        }

                        EnableAnimatorRootCorrection(time, time, time);
                        UpdateAnimatorRootCorrection();
                        animatorIK.UpdateIK(false, !toolBakeIK_BakeAnimationRigging);
                        ResetAnimatorRootCorrection();
                        ResetUpdateIKtargetAll();
                    }
                    for (int frame = toolBakeIK_FirstFrame; frame <= toolBakeIK_LastFrame; frame++)
                    {
                        var time = uAw.GetFrameTime(frame, clip);
                        EnableAnimatorRootCorrection(time, time, time);
                    }
                    UpdateAnimatorRootCorrection();
                    ResetAnimatorRootCorrection();
                    if (toolBakeIK_BakeFootIK)
                        humanoidFootIK.Clear();
                }
                #endregion
                #region OriginalIK
                EditorUtility.DisplayProgressBar("Bake IK", "Original IK", progressIndex++ / (float)progressTotal);
                if (originalIK.ikData.Any(data => data.enable))
                {
                    #region Save
                    IKDataSave[,] ikDataAllSave = null;
                    IKDataSave[] ikDataBeginSave = null, ikDataEndSave = null;
                    if (toolBakeIK_Mode == BakeIKMode.Simple)
                    {
                        ikDataAllSave = new IKDataSave[originalIK.ikData.Count, GetLastFrame() + 1];
                        for (int frame = toolBakeIK_FirstFrame; frame <= toolBakeIK_LastFrame; frame++)
                        {
                            var time = uAw.GetFrameTime(frame, clip);
                            SampleAnimationBase(time);
                            for (var index = 0; index < originalIK.ikData.Count; index++)
                            {
                                if (!originalIK.ikData[index].enable) continue;
                                originalIK.SynchroSet(index);
                                ikDataAllSave[index, frame] = new IKDataSave(originalIK.ikData[index]);
                            }
                        }
                    }
                    else if (toolBakeIK_Mode == BakeIKMode.Interpolation)
                    {
                        ikDataBeginSave = new IKDataSave[originalIK.ikData.Count];
                        ikDataEndSave = new IKDataSave[originalIK.ikData.Count];
                        SampleAnimationBase(beginTime);
                        for (var index = 0; index < originalIK.ikData.Count; index++)
                        {
                            if (!originalIK.ikData[index].enable) continue;
                            originalIK.SynchroSet(index);
                            ikDataBeginSave[index] = new IKDataSave(originalIK.ikData[index]);
                        }
                        SampleAnimationBase(endTime);
                        for (var index = 0; index < originalIK.ikData.Count; index++)
                        {
                            if (!originalIK.ikData[index].enable) continue;
                            originalIK.SynchroSet(index);
                            ikDataEndSave[index] = new IKDataSave(originalIK.ikData[index]);
                        }
                    }
                    #endregion

                    for (int frame = toolBakeIK_FirstFrame; frame <= toolBakeIK_LastFrame; frame++)
                    {
                        var time = uAw.GetFrameTime(frame, clip);

                        SampleAnimationBase(time);

                        if (toolBakeIK_Mode == BakeIKMode.Simple)
                        {
                            #region Simple
                            for (var index = 0; index < originalIK.ikData.Count; index++)
                            {
                                if (!originalIK.ikData[index].enable) continue;
                                ikDataAllSave[index, frame].Set(originalIK.ikData[index]);
                                SetUpdateIKtargetOriginalIK(index);
                            }
                            #endregion
                        }
                        else if (toolBakeIK_Mode == BakeIKMode.Interpolation)
                        {
                            #region Interpolation
                            float rate = 0f;
                            if (toolBakeIK_LastFrame - toolBakeIK_FirstFrame > 0)
                                rate = (frame - toolBakeIK_FirstFrame) / (float)(toolBakeIK_LastFrame - toolBakeIK_FirstFrame);

                            for (var index = 0; index < originalIK.ikData.Count; index++)
                            {
                                if (!originalIK.ikData[index].enable) continue;
                                var ikDataSave = IKDataSave.Lerp(ikDataBeginSave[index], ikDataEndSave[index], rate);
                                ikDataSave.Set(originalIK.ikData[index]);
                                SetUpdateIKtargetOriginalIK(index);
                            }
                            #endregion
                        }

                        originalIK.UpdateIK();
                        ResetUpdateIKtargetAll();
                    }
                }
                #endregion

                ToolsCurvesWasModifiedStoppedSmoothTangents(beginTime, endTime);
            }
            finally
            {
                SetCurrentTime(saveCurrentTime);
                ResetUpdateIKtargetAll();
                SetSynchroIKtargetAll();
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private void ToolsClearHumanoidIK(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "Clear IK Keyframe")) return;

            {
                var beginTime = uAw.SnapToFrame(toolHumanoidIK_FirstFrame >= 0 ? toolHumanoidIK_FirstFrame / clip.frameRate : 0f, clip.frameRate);
                var endTime = uAw.SnapToFrame(toolHumanoidIK_LastFrame >= 0 ? toolHumanoidIK_LastFrame / clip.frameRate : clip.length, clip.frameRate);
                float halfFrameTime = (0.5f / clip.frameRate) - 0.0001f;
                for (var ikIndex = (AnimatorIKIndex)0; ikIndex < AnimatorIKIndex.Total; ikIndex++)
                {
                    if (ikIndex == AnimatorIKIndex.LeftHand || ikIndex == AnimatorIKIndex.RightHand)
                    {
                        if (!toolHumanoidIK_Hand) continue;
                    }
                    else if (ikIndex == AnimatorIKIndex.LeftFoot || ikIndex == AnimatorIKIndex.RightFoot)
                    {
                        if (!toolHumanoidIK_Foot) continue;
                    }
                    else
                    {
                        continue;
                    }
                    Action<EditorCurveBinding> ClearHumanoidIK = (binding) =>
                    {
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        if (curve == null) return;
                        for (int i = curve.length - 1; i >= 0; i--)
                        {
                            if (curve[i].time >= beginTime - halfFrameTime && curve[i].time <= endTime + halfFrameTime)
                            {
                                curve.RemoveKey(i);
                            }
                        }
                        AnimationUtility.SetEditorCurve(clip, binding, curve.length > 0 ? curve : null);
                    };
                    for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                        ClearHumanoidIK(AnimationCurveBindingAnimatorIkT(ikIndex, dofIndex));
                    for (int dofIndex = 0; dofIndex < 4; dofIndex++)
                        ClearHumanoidIK(AnimationCurveBindingAnimatorIkQ(ikIndex, dofIndex));
                }
            }

            ToolsCommonAfter();
        }
        private void ToolsGenarateAnimationRigging(AnimationClip clip)
        {
            ToolsClearAnimationRigging(clip);

#if VERYANIMATION_ANIMATIONRIGGING
            if (!ToolsCommonBefore(clip, "AnimationRigging")) return;

            if (clip != currentClip) return;

            try
            {
                var beginTime = uAw.SnapToFrame(toolAnimationRigging_FirstFrame >= 0 ? toolAnimationRigging_FirstFrame / clip.frameRate : 0f, clip.frameRate);
                var endTime = uAw.SnapToFrame(toolAnimationRigging_LastFrame >= 0 ? toolAnimationRigging_LastFrame / clip.frameRate : clip.length, clip.frameRate);

                #region AnimatorIK
                if (isHuman && animatorIK.ikData.Any(data => data.enable && data.rigConstraint != null))
                {
                    var syncFlags = AnimatorIKCore.SynchroSetFlags.None;
                    {
                        if (toolAnimationRigging_BakeHandIK) syncFlags |= AnimatorIKCore.SynchroSetFlags.HandIK;
                        if (toolAnimationRigging_BakeFootIK) syncFlags |= AnimatorIKCore.SynchroSetFlags.FootIK;
                    }
                    for (var target = 0; target < animatorIK.ikData.Length; target++)
                    {
                        var data = animatorIK.ikData[target];
                        if (!data.enable || data.rigConstraint == null)
                            continue;

                        animatorIK.WriteCurveAnimationRiggingConstraint((AnimatorIKCore.IKTarget)target, toolAnimationRigging_FirstFrame, toolAnimationRigging_LastFrame, syncFlags);
                    }
                }
                #endregion
            }
            finally
            {
                ResetUpdateIKtargetAll();
                SetSynchroIKtargetAll();
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
#endif
        }
        private void ToolsClearAnimationRigging(AnimationClip clip)
        {
#if VERYANIMATION_ANIMATIONRIGGING
            if (!ToolsCommonBefore(clip, "Clear AnimationRigging")) return;

            {
                var beginTime = uAw.SnapToFrame(toolAnimationRigging_FirstFrame >= 0 ? toolAnimationRigging_FirstFrame / clip.frameRate : 0f, clip.frameRate);
                var endTime = uAw.SnapToFrame(toolAnimationRigging_LastFrame >= 0 ? toolAnimationRigging_LastFrame / clip.frameRate : clip.length, clip.frameRate);
                float halfFrameTime = (0.5f / clip.frameRate) - 0.0001f;

                Action<EditorCurveBinding> Clear = (binding) =>
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null) return;
                    for (int i = curve.length - 1; i >= 0; i--)
                    {
                        if (curve[i].time >= beginTime - halfFrameTime && curve[i].time <= endTime + halfFrameTime)
                        {
                            curve.RemoveKey(i);
                        }
                    }
                    AnimationUtility.SetEditorCurve(clip, binding, curve.length > 0 ? curve : null);
                };

                for (var target = 0; target < animatorIK.ikData.Length; target++)
                {
                    var data = animatorIK.ikData[target];
                    if (!data.enable || data.rigConstraint == null)
                        continue;

                    var bindings = animatorIK.GetAnimationRiggingConstraintBindings((AnimatorIKCore.IKTarget)target);
                    foreach (var binding in bindings)
                    {
                        Clear(binding);
                    }
                }
            }

            ToolsCommonAfter();
#endif
        }
        private void ToolsGenarateHumanoidIK(AnimationClip clip)
        {
            if (!isHuman || !clip.isHumanMotion) return;
            if (!ToolsCommonBefore(clip, "Genarate IK Keyframe")) return;

            try
            {
                EditorUtility.DisplayProgressBar("Genarate IK Keyframe", "", 0f);
                var beginTime = uAw.SnapToFrame(toolHumanoidIK_FirstFrame >= 0 ? toolHumanoidIK_FirstFrame / clip.frameRate : 0f, clip.frameRate);
                var endTime = uAw.SnapToFrame(toolHumanoidIK_LastFrame >= 0 ? toolHumanoidIK_LastFrame / clip.frameRate : clip.length, clip.frameRate);
                float halfFrameTime = (0.5f / clip.frameRate) - 0.0001f;
                for (var ikIndex = (AnimatorIKIndex)0; ikIndex < AnimatorIKIndex.Total; ikIndex++)
                {
                    EditorUtility.DisplayProgressBar("Genarate IK Keyframe", string.Format("{0} / {1}", (int)ikIndex, (int)AnimatorIKIndex.Total), (int)ikIndex / (float)AnimatorIKIndex.Total);
                    if (ikIndex == AnimatorIKIndex.LeftHand || ikIndex == AnimatorIKIndex.RightHand)
                    {
                        if (!toolHumanoidIK_Hand) continue;
                    }
                    else if (ikIndex == AnimatorIKIndex.LeftFoot || ikIndex == AnimatorIKIndex.RightFoot)
                    {
                        if (!toolHumanoidIK_Foot) continue;
                    }
                    else
                    {
                        continue;
                    }

                    AnimationCurve[] ikTCurves = new AnimationCurve[3];
                    AnimationCurve[] ikQCurves = new AnimationCurve[4];
                    {
                        for (int dofIndex = 0; dofIndex < ikTCurves.Length; dofIndex++)
                        {
                            ikTCurves[dofIndex] = AnimationUtility.GetEditorCurve(clip, AnimationCurveBindingAnimatorIkT((AnimatorIKIndex)ikIndex, dofIndex));
                            if (ikTCurves[dofIndex] == null)
                                ikTCurves[dofIndex] = new AnimationCurve();
                            else
                            {
                                for (int i = ikTCurves[dofIndex].length - 1; i >= 0; i--)
                                {
                                    if (ikTCurves[dofIndex][i].time >= beginTime - halfFrameTime && ikTCurves[dofIndex][i].time <= endTime + halfFrameTime)
                                    {
                                        ikTCurves[dofIndex].RemoveKey(i);
                                    }
                                }
                            }
                        }
                        for (int dofIndex = 0; dofIndex < ikQCurves.Length; dofIndex++)
                        {
                            ikQCurves[dofIndex] = AnimationUtility.GetEditorCurve(clip, AnimationCurveBindingAnimatorIkQ((AnimatorIKIndex)ikIndex, dofIndex));
                            if (ikQCurves[dofIndex] == null)
                                ikQCurves[dofIndex] = new AnimationCurve();
                            else
                            {
                                for (int i = ikQCurves[dofIndex].length - 1; i >= 0; i--)
                                {
                                    if (ikQCurves[dofIndex][i].time >= beginTime - halfFrameTime && ikQCurves[dofIndex][i].time <= endTime + halfFrameTime)
                                    {
                                        ikQCurves[dofIndex].RemoveKey(i);
                                    }
                                }
                            }
                        }
                    }
                    calcObject.SetApplyIK(false);
                    calcObject.SetTransformStart();
                    var locaToWorldRotation = transformPoseSave.startRotation;
                    var worldToLocalMatrix = transformPoseSave.startMatrix.inverse;
                    var humanScale = calcObject.animator.humanScale;
                    var leftFeetBottomHeight = calcObject.animator.leftFeetBottomHeight;
                    var rightFeetBottomHeight = calcObject.animator.rightFeetBottomHeight;
                    var postLeftFoot = GetAvatarPostRotation(HumanBodyBones.LeftFoot);
                    var postRightFoot = GetAvatarPostRotation(HumanBodyBones.RightFoot);
                    var humanoidIndex = AnimatorIKIndex2HumanBodyBones[(int)ikIndex];
                    var t = calcObject.humanoidBones[(int)humanoidIndex].transform;
                    var positionTable = new Dictionary<float, Vector3>();
                    var rotationTable = new Dictionary<float, Quaternion>();
                    #region KeyInfoTable
                    {
                        var keyTimes = GetHumanoidKeyframeTimeList(clip, AnimatorIKIndex2HumanBodyBones[(int)ikIndex]);
                        foreach (var time in keyTimes)
                        {
                            calcObject.SampleAnimation(clip, time);
                            positionTable.Add(time, t.position);
                            rotationTable.Add(time, t.rotation);
                        }
                    }
                    #endregion
                    for (int frame = toolHumanoidIK_FirstFrame; frame <= toolHumanoidIK_LastFrame; frame++)
                    {
                        var time = uAw.GetFrameTime(frame, clip);
                        calcObject.SampleAnimation(clip, time);
                        Vector3 position;
                        Quaternion rotation;
                        {
                            Vector3 positionL = Vector3.zero, positionR = Vector3.zero;
                            float nearL = float.MinValue, nearR = float.MaxValue;
                            foreach (var pair in positionTable)
                            {
                                if (pair.Key <= time && pair.Key > nearL)
                                {
                                    positionL = pair.Value;
                                    nearL = pair.Key;
                                }
                                if (pair.Key >= time && pair.Key < nearR)
                                {
                                    positionR = pair.Value;
                                    nearR = pair.Key;
                                }
                            }
                            var rate = nearR - nearL != 0f ? (time - nearL) / (nearR - nearL) : 0f;
                            position = Vector3.Lerp(positionL, positionR, rate);
                        }
                        {
                            Quaternion rotationL = Quaternion.identity, rotationR = Quaternion.identity;
                            float nearL = float.MinValue, nearR = float.MaxValue;
                            foreach (var pair in rotationTable)
                            {
                                if (pair.Key <= time && pair.Key > nearL)
                                {
                                    rotationL = pair.Value;
                                    nearL = pair.Key;
                                }
                                if (pair.Key >= time && pair.Key < nearR)
                                {
                                    rotationR = pair.Value;
                                    nearR = pair.Key;
                                }
                            }
                            var rate = nearR - nearL != 0f ? (time - nearL) / (nearR - nearL) : 0f;
                            rotation = Quaternion.Slerp(rotationL, rotationR, rate);
                        }
                        var rootT = GetAnimationValueAnimatorRootT(time);
                        var rootQ = GetAnimationValueAnimatorRootQ(time);

                        Vector3 ikT = position;
                        Quaternion ikQ = rotation;
                        {
                            Quaternion post = Quaternion.identity;
                            switch ((AnimatorIKIndex)ikIndex)
                            {
                            case AnimatorIKIndex.LeftFoot: post = postLeftFoot; break;
                            case AnimatorIKIndex.RightFoot: post = postRightFoot; break;
                            }
                            #region IkT
                            if (ikIndex == AnimatorIKIndex.LeftFoot || ikIndex == AnimatorIKIndex.RightFoot)
                            {
                                Vector3 add = Vector3.zero;
                                switch ((AnimatorIKIndex)ikIndex)
                                {
                                case AnimatorIKIndex.LeftFoot: add.x += leftFeetBottomHeight; break;
                                case AnimatorIKIndex.RightFoot: add.x += rightFeetBottomHeight; break;
                                }
                                ikT += (rotation * post) * add;
                            }
                            ikT = worldToLocalMatrix.MultiplyPoint3x4(ikT) - (rootT * humanScale);
                            ikT = Quaternion.Inverse(rootQ) * ikT;
                            ikT *= 1f / humanScale;
                            #endregion
                            #region IkQ
                            ikQ = Quaternion.Inverse(locaToWorldRotation * rootQ) * (rotation * post);
                            #endregion
                        }
                        for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                        {
                            AddKeyframe(ikTCurves[dofIndex], time, ikT[dofIndex]);
                        }
                        for (int dofIndex = 0; dofIndex < 4; dofIndex++)
                        {
                            AddKeyframe(ikQCurves[dofIndex], time, ikQ[dofIndex]);
                        }
                    }
                    for (int dofIndex = 0; dofIndex < ikTCurves.Length; dofIndex++)
                        AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorIkT(ikIndex, dofIndex), ikTCurves[dofIndex]);
                    for (int dofIndex = 0; dofIndex < ikQCurves.Length; dofIndex++)
                        AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorIkQ(ikIndex, dofIndex), ikQCurves[dofIndex]);
                }
            }
            finally
            {
                calcObject.SetTransformStart();
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private void ToolsRootMotionMotionClear(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "Clear Generic Root Motion Keyframe")) return;

            {
                for (int dof = 0; dof < 3; dof++)
                    AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorMotionT[dof], null);
                for (int dof = 0; dof < 4; dof++)
                    AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorMotionQ[dof], null);
            }

            ToolsCommonAfter();
        }
        private void ToolsRootMotionMotionGenerate(AnimationClip clip)
        {
            if (vaw.animator == null) return;
            if (!ToolsCommonBefore(clip, "Genarate Generic Root Motion Keyframe")) return;

            if (isHuman)
            {
                AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorMotionT[0], GetAnimationCurveAnimatorRootT(0));
                AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorMotionT[2], GetAnimationCurveAnimatorRootT(2));
                {
                    var curve = GetAnimationCurveAnimatorRootT(1);
                    for (int i = 0; i < curve.length; i++)
                    {
                        var key = curve[i];
                        key.value -= 1f;
                        curve.MoveKey(i, key);
                    }
                    AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorMotionT[1], curve);
                }
                for (int dof = 0; dof < 4; dof++)
                    AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorMotionQ[dof], GetAnimationCurveAnimatorRootQ(dof));
            }
            else
            {
                try
                {
                    EditorUtility.DisplayProgressBar("Genarate Generic Root Motion Keyframe", "", 0f);
                    AnimationCurve[] rootT = new AnimationCurve[3];
                    AnimationCurve[] rootQ = new AnimationCurve[4];
                    {
                        for (int dof = 0; dof < 3; dof++)
                            rootT[dof] = new AnimationCurve();
                        for (int dof = 0; dof < 4; dof++)
                            rootQ[dof] = new AnimationCurve();
                    }
                    calcObject.SetApplyIK(false);
                    calcObject.SetTransformOrigin();
                    var rootTransform = calcObject.bones[rootMotionBoneIndex >= 0 ? rootMotionBoneIndex : 0].transform;
                    Vector3 startPosition;
                    Quaternion startRotation;
                    {
                        calcObject.SampleAnimation(clip, 0f);
                        startPosition = rootTransform.position;
                        startRotation = rootTransform.rotation;
                    }
                    var lastFrame = uAw.GetLastFrame(clip);
                    for (int frame = 0; frame <= lastFrame; frame++)
                    {
                        var time = GetFrameTime(frame);

                        calcObject.SampleAnimation(clip, time);

                        var position = rootTransform.position - startPosition;
                        var rotation = Quaternion.Inverse(startRotation) * rootTransform.rotation;

                        for (int dof = 0; dof < 3; dof++)
                            SetKeyframe(rootT[dof], time, position[dof]);
                        for (int dof = 0; dof < 4; dof++)
                            SetKeyframe(rootQ[dof], time, rotation[dof]);
                    }
                    {
                        for (int dof = 0; dof < 3; dof++)
                            AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorMotionT[dof], rootT[dof]);
                        for (int dof = 0; dof < 4; dof++)
                            AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorMotionQ[dof], rootQ[dof]);
                    }
                }
                finally
                {
                    calcObject.SetTransformStart();
                    EditorUtility.ClearProgressBar();
                }
            }

            ToolsCommonAfter();
        }
        private void ToolsRootMotionRootClear(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "Clear Generic Root Motion Keyframe")) return;

            {
                for (int dof = 0; dof < 3; dof++)
                    AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorRootT[dof], null);
                for (int dof = 0; dof < 4; dof++)
                    AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorRootQ[dof], null);
            }

            ToolsCommonAfter();
        }
        private void ToolsRootMotionRootGenerate(AnimationClip clip)
        {
            if (isHuman || rootMotionBoneIndex < 0) return;
            if (!ToolsCommonBefore(clip, "Genarate Generic Root Motion Keyframe")) return;

            try
            {
                EditorUtility.DisplayProgressBar("Genarate Generic Root Motion Keyframe", "", 0f);
                AnimationCurve[] rootT = new AnimationCurve[3];
                AnimationCurve[] rootQ = new AnimationCurve[4];
                {
                    for (int dof = 0; dof < 3; dof++)
                        rootT[dof] = new AnimationCurve();
                    for (int dof = 0; dof < 4; dof++)
                        rootQ[dof] = new AnimationCurve();
                }
                calcObject.SetApplyIK(false);
                calcObject.SetTransformOrigin();
                var rootTransform = calcObject.bones[rootMotionBoneIndex >= 0 ? rootMotionBoneIndex : 0].transform;
                Vector3 startPosition;
                Quaternion startRotation;
                {
                    calcObject.SampleAnimation(clip, 0f);
                    startPosition = rootTransform.position;
                    startRotation = rootTransform.rotation;
                }
                var lastFrame = uAw.GetLastFrame(clip);
                for (int frame = 0; frame <= lastFrame; frame++)
                {
                    var time = GetFrameTime(frame);

                    calcObject.SampleAnimation(clip, time);

                    var position = rootTransform.position - startPosition;
                    var rotation = Quaternion.Inverse(startRotation) * rootTransform.rotation;

                    for (int dof = 0; dof < 3; dof++)
                        SetKeyframe(rootT[dof], time, position[dof]);
                    for (int dof = 0; dof < 4; dof++)
                        SetKeyframe(rootQ[dof], time, rotation[dof]);
                }
                {
                    for (int dof = 0; dof < 3; dof++)
                        AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorRootT[dof], rootT[dof]);
                    for (int dof = 0; dof < 4; dof++)
                        AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorRootQ[dof], rootQ[dof]);
                }
            }
            finally
            {
                calcObject.SetTransformStart();
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private void ToolsCopy(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "Copy Keyframe")) return;

            try
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var rbindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                var events = AnimationUtility.GetAnimationEvents(clip);
                var beginTime = uAw.SnapToFrame(toolCopy_FirstFrame / clip.frameRate, clip.frameRate);
                var endTime = uAw.SnapToFrame(toolCopy_LastFrame / clip.frameRate, clip.frameRate);
                var writeBeginTime = uAw.SnapToFrame(toolCopy_WriteFrame / clip.frameRate, clip.frameRate);
                var writeEndTime = writeBeginTime + (endTime - beginTime);
                float halfFrameTime = (0.5f / clip.frameRate) - 0.0001f;
                int progressIndex = 0;
                int progressTotal = 3;

                EditorUtility.DisplayProgressBar("Copy Keyframe", "Read", progressIndex++ / (float)progressTotal);
                if (writeBeginTime > clip.length)
                {
                    #region AddLastLinerKey
                    foreach (var binding in bindings)
                    {
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        var keyIndex = FindKeyframeAtTime(curve, clip.length);
                        if (keyIndex < 0)
                        {
                            keyIndex = AddKeyframe(curve, clip.length, curve.Evaluate(clip.length));
                            AnimationUtility.SetKeyLeftTangentMode(curve, keyIndex, AnimationUtility.TangentMode.Linear);
                            if (keyIndex > 0)
                                AnimationUtility.SetKeyRightTangentMode(curve, keyIndex - 1, AnimationUtility.TangentMode.Linear);
                            AnimationUtility.SetEditorCurve(clip, binding, curve);
                        }
                    }
                    #endregion
                }

                EditorUtility.DisplayProgressBar("Copy Keyframe", "Read", progressIndex++ / (float)progressTotal);
                #region CurveBindings
                List<Keyframe>[] curveCopyKeyframes = new List<Keyframe>[bindings.Length];
                List<int>[] markKeyIndexes = new List<int>[bindings.Length];
                for (int i = 0; i < bindings.Length; i++)
                {
                    curveCopyKeyframes[i] = new List<Keyframe>();
                    markKeyIndexes[i] = new List<int>();
                    bool update = false;
                    var curve = AnimationUtility.GetEditorCurve(clip, bindings[i]);
                    if (curveCopyKeyframes[i].FindIndex((x) => Mathf.Approximately(uAw.SnapToFrame(x.time, clip.frameRate), uAw.SnapToFrame(writeBeginTime, clip.frameRate))) < 0)
                    {
                        var key = new Keyframe(writeBeginTime, curve.Evaluate(beginTime));
                        markKeyIndexes[i].Add(curveCopyKeyframes[i].Count);
                        curveCopyKeyframes[i].Add(key);
                    }
                    if (curveCopyKeyframes[i].FindIndex((x) => Mathf.Approximately(uAw.SnapToFrame(x.time, clip.frameRate), uAw.SnapToFrame(writeEndTime, clip.frameRate))) < 0)
                    {
                        var key = new Keyframe(writeEndTime, curve.Evaluate(endTime));
                        markKeyIndexes[i].Add(curveCopyKeyframes[i].Count);
                        curveCopyKeyframes[i].Add(key);
                    }
                    for (int j = 0; j < curve.length; j++)
                    {
                        if (curve[j].time >= beginTime - halfFrameTime && curve[j].time <= endTime + halfFrameTime)
                        {
                            var key = curve[j];
                            key.time = uAw.SnapToFrame(writeBeginTime + (key.time - beginTime), clip.frameRate);
                            curveCopyKeyframes[i].Add(key);
                        }
                        if (curve[j].time > writeBeginTime + halfFrameTime && curve[j].time < writeEndTime - halfFrameTime)
                        {
                            curve.RemoveKey(j--);
                            update = true;
                        }
                    }
                    {
                        Action<int> ActionAddKeyframe = (frame) =>
                        {
                            var setTime = uAw.GetFrameTime(frame, clip);
                            var keyIndex = FindKeyframeAtTime(curve, setTime);
                            if (keyIndex < 0)
                            {
                                AddKeyframe(curve, setTime, curve.Evaluate(setTime));
                                update = true;
                            }
                        };
                        if (toolCopy_WriteFrame < toolCopy_LastFrame)
                        {
                            ActionAddKeyframe(toolCopy_WriteFrame);
                        }
                        if (toolCopy_WriteFrame + (toolCopy_LastFrame - toolCopy_FirstFrame) > toolCopy_FirstFrame)
                        {
                            ActionAddKeyframe(toolCopy_WriteFrame + (toolCopy_LastFrame - toolCopy_FirstFrame));
                        }
                    }
                    if (update)
                        AnimationUtility.SetEditorCurve(clip, bindings[i], curve);
                }
                #endregion
                #region ObjectReferenceCurveBindings
                List<ObjectReferenceKeyframe>[] rcurveCopyKeyframes = new List<ObjectReferenceKeyframe>[rbindings.Length];
                for (int i = 0; i < rbindings.Length; i++)
                {
                    rcurveCopyKeyframes[i] = new List<ObjectReferenceKeyframe>();
                    bool update = false;
                    var keys = new List<ObjectReferenceKeyframe>(AnimationUtility.GetObjectReferenceCurve(clip, rbindings[i]));
                    for (int j = 0; j < keys.Count; j++)
                    {
                        if (keys[j].time >= beginTime - halfFrameTime && keys[j].time <= endTime + halfFrameTime)
                        {
                            var key = keys[j];
                            key.time = uAw.SnapToFrame(writeBeginTime + (key.time - beginTime), clip.frameRate);
                            rcurveCopyKeyframes[i].Add(key);
                        }
                        if (keys[j].time > writeBeginTime + halfFrameTime && keys[j].time < writeEndTime - halfFrameTime)
                        {
                            keys.RemoveAt(j--);
                            update = true;
                        }
                    }
                    if (update)
                        AnimationUtility.SetObjectReferenceCurve(clip, rbindings[i], keys.ToArray());
                }
                #endregion
                #region AnimationEvents
                List<AnimationEvent> newEvents = new List<AnimationEvent>();
                for (int i = 0; i < events.Length; i++)
                {
                    if (events[i].time >= beginTime - halfFrameTime && events[i].time <= endTime + halfFrameTime)
                    {
                        var key = new AnimationEvent()
                        {
                            stringParameter = events[i].stringParameter,
                            floatParameter = events[i].floatParameter,
                            intParameter = events[i].intParameter,
                            objectReferenceParameter = events[i].objectReferenceParameter,
                            functionName = events[i].functionName,
                            time = writeBeginTime + (events[i].time - beginTime),
                            messageOptions = events[i].messageOptions,
                        };
                        newEvents.Add(key);
                    }
                    if (events[i].time < writeBeginTime - halfFrameTime || events[i].time > writeEndTime + halfFrameTime)
                    {
                        newEvents.Add(events[i]);
                    }
                }
                newEvents.Sort((x, y) =>
                {
                    if (x.time > y.time) return 1;
                    else if (x.time < y.time) return -1;
                    else return 0;
                });
                #endregion

                EditorUtility.DisplayProgressBar("Copy Keyframe", "Write", progressIndex++ / (float)progressTotal);
                #region CurveBindings
                for (int i = 0; i < bindings.Length; i++)
                {
                    if (curveCopyKeyframes[i] == null || curveCopyKeyframes[i].Count <= 0) continue;
                    var curve = AnimationUtility.GetEditorCurve(clip, bindings[i]);
                    for (int j = 0; j < curveCopyKeyframes[i].Count; j++)
                    {
                        if (markKeyIndexes[i].Contains(j))
                        {
                            SetKeyframe(curve, curveCopyKeyframes[i][j].time, curveCopyKeyframes[i][j].value);
                        }
                        else
                        {
                            var index = SetKeyframe(curve, curveCopyKeyframes[i][j]);
                            curve.MoveKey(index, curveCopyKeyframes[i][j]);
                        }
                    }
                    AnimationUtility.SetEditorCurve(clip, bindings[i], curve);
                }
                #endregion
                #region ObjectReferenceCurveBindings
                for (int i = 0; i < rbindings.Length; i++)
                {
                    if (rcurveCopyKeyframes[i] == null || rcurveCopyKeyframes[i].Count <= 0) continue;
                    var keys = new List<ObjectReferenceKeyframe>(AnimationUtility.GetObjectReferenceCurve(clip, rbindings[i]));
                    for (int j = 0; j < rcurveCopyKeyframes[i].Count; j++)
                    {
                        var keyIndex = FindKeyframeAtTime(keys, rcurveCopyKeyframes[i][j].time);
                        if (keyIndex >= 0)
                        {
                            keys[keyIndex] = rcurveCopyKeyframes[i][j];
                        }
                        else
                        {
                            keys.Add(rcurveCopyKeyframes[i][j]);
                        }
                    }
                    AnimationUtility.SetObjectReferenceCurve(clip, rbindings[i], keys.ToArray());
                }
                #endregion
                #region AnimationEvents
                AnimationUtility.SetAnimationEvents(clip, newEvents.ToArray());
                #endregion
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private void ToolsTrim(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "Trim Keyframe")) return;

            try
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var rbindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                var events = AnimationUtility.GetAnimationEvents(clip);
                var beginTime = uAw.SnapToFrame(toolTrim_FirstFrame / clip.frameRate, clip.frameRate);
                var endTime = uAw.SnapToFrame(toolTrim_LastFrame / clip.frameRate, clip.frameRate);
                float halfFrameTime = (0.5f / clip.frameRate) - 0.0001f;
                int progressIndex = 0;
                int progressTotal = bindings.Length * 3 + rbindings.Length + events.Length;
                {
                    AnimationCurve[] curves = new AnimationCurve[bindings.Length];
                    for (int i = 0; i < bindings.Length; i++)
                    {
                        EditorUtility.DisplayProgressBar("Trim", string.IsNullOrEmpty(bindings[i].path) ? bindings[i].propertyName : bindings[i].path, progressIndex++ / (float)progressTotal);
                        var curve = AnimationUtility.GetEditorCurve(clip, bindings[i]);
                        {
                            var keys = new List<Keyframe>();
                            for (int j = 0; j < curve.length; j++)
                            {
                                if (curve[j].time < beginTime - halfFrameTime || curve[j].time > endTime + halfFrameTime) continue;
                                var tmp = curve[j];
                                tmp.time = uAw.SnapToFrame(tmp.time - beginTime, clip.frameRate);
                                keys.Add(tmp);
                            }
                            if (keys.FindIndex((x) => Mathf.Approximately(x.time, 0f)) < 0)
                            {
                                keys.Insert(0, new Keyframe(0, curve.Evaluate(beginTime)));
                            }
                            if (keys.FindIndex((x) => Mathf.Approximately(x.time, endTime - beginTime)) < 0)
                            {
                                keys.Add(new Keyframe(endTime - beginTime, curve.Evaluate(endTime)));
                            }
                            curve.keys = keys.ToArray();
                        }
                        curves[i] = curve;
                    }
                    for (int i = 0; i < bindings.Length; i++)
                    {
                        EditorUtility.DisplayProgressBar("Trim", string.IsNullOrEmpty(bindings[i].path) ? bindings[i].propertyName : bindings[i].path, progressIndex++ / (float)progressTotal);
                        AnimationUtility.SetEditorCurve(clip, bindings[i], null);
                    }
                    for (int i = 0; i < bindings.Length; i++)
                    {
                        EditorUtility.DisplayProgressBar("Trim", string.IsNullOrEmpty(bindings[i].path) ? bindings[i].propertyName : bindings[i].path, progressIndex++ / (float)progressTotal);
                        AnimationUtility.SetEditorCurve(clip, bindings[i], curves[i]);
                    }
                }
                for (int i = 0; i < rbindings.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Trim", string.IsNullOrEmpty(rbindings[i].path) ? rbindings[i].propertyName : rbindings[i].path, progressIndex++ / (float)progressTotal);
                    var rkeys = AnimationUtility.GetObjectReferenceCurve(clip, rbindings[i]);
                    var keys = new List<ObjectReferenceKeyframe>();
                    foreach (var key in rkeys)
                    {
                        if (key.time < beginTime - halfFrameTime || key.time > endTime + halfFrameTime) continue;
                        var tmp = key;
                        tmp.time = uAw.SnapToFrame(tmp.time - beginTime, clip.frameRate);
                        keys.Add(tmp);
                    }
                    if (keys.FindIndex((x) => Mathf.Approximately(x.time, 0f)) < 0)
                    {
                        var nearIndex = FindBeforeNearKeyframeAtTime(rkeys, beginTime);
                        keys.Insert(0, new ObjectReferenceKeyframe() { time = 0f, value = rkeys[nearIndex].value });
                    }
                    AnimationUtility.SetObjectReferenceCurve(clip, rbindings[i], keys.ToArray());
                }
                {
                    List<AnimationEvent> newEvents = new List<AnimationEvent>(events.Length);
                    for (int i = 0; i < events.Length; i++)
                    {
                        EditorUtility.DisplayProgressBar("Trim", events[i].functionName, progressIndex++ / (float)progressTotal);
                        if (events[i].time < beginTime - halfFrameTime || events[i].time > endTime + halfFrameTime) continue;
                        var tmp = events[i];
                        tmp.time = uAw.SnapToFrame(tmp.time - beginTime, clip.frameRate);
                        newEvents.Add(tmp);
                    }
                    AnimationUtility.SetAnimationEvents(clip, newEvents.ToArray());
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private void ToolsAdd(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "Add Clip")) return;

            try
            {
                var addTime = clip.length + (1f / clip.frameRate);
                foreach (var binding in AnimationUtility.GetCurveBindings(toolAdd_Clip))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null)
                        curve = new AnimationCurve();
                    var srcCurve = AnimationUtility.GetEditorCurve(toolAdd_Clip, binding);
                    for (int i = 0; i < srcCurve.length; i++)
                    {
                        var key = srcCurve[i];
                        key.time += addTime;
                        curve.AddKey(key);
                    }
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(toolAdd_Clip))
                {
                    var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding).ToList();
                    if (curve == null)
                        curve = new List<ObjectReferenceKeyframe>();
                    var srcCurve = AnimationUtility.GetObjectReferenceCurve(toolAdd_Clip, binding);
                    for (int i = 0; i < srcCurve.Length; i++)
                    {
                        var key = srcCurve[i];
                        key.time += addTime;
                        curve.Add(key);
                    }
                    AnimationUtility.SetObjectReferenceCurve(clip, binding, curve.ToArray());
                }
                {
                    var events = AnimationUtility.GetAnimationEvents(clip).ToList();
                    foreach (var ev in AnimationUtility.GetAnimationEvents(toolAdd_Clip))
                    {
                        var tmp = ev;
                        tmp.time += addTime;
                        events.Add(tmp);
                    }
                    AnimationUtility.SetAnimationEvents(clip, events.ToArray());
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private void ToolsCombine(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "Combine Clip")) return;

            try
            {
                foreach (var binding in AnimationUtility.GetCurveBindings(toolCombine_Clip))
                {
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(toolCombine_Clip, binding));
                }
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(toolCombine_Clip))
                {
                    AnimationUtility.SetObjectReferenceCurve(clip, binding, AnimationUtility.GetObjectReferenceCurve(toolCombine_Clip, binding));
                }
                {
                    var events = AnimationUtility.GetAnimationEvents(clip).ToList();
                    foreach (var ev in AnimationUtility.GetAnimationEvents(toolCombine_Clip))
                    {
                        events.Add(ev);
                    }
                    events.Sort((x, y) =>
                    {
                        if (x.time > y.time) return 1;
                        else if (x.time < y.time) return -1;
                        else return 0;
                    });
                    AnimationUtility.SetAnimationEvents(clip, events.ToArray());
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private enum ToolsCreateNewClipMode
        {
            Blank,
            Duplicate,
            Mirror,
        }
        private void ToolsCreateNewClip(string clipPath, ToolsCreateNewClipMode mode)
        {
            VeryAnimationWindow.CustomAssetModificationProcessor.Pause();
            var newClip = uAnimationWindowUtility.CreateNewClipAtPath(clipPath);
            VeryAnimationWindow.CustomAssetModificationProcessor.Resume();

            if (!ToolsCommonBefore(newClip, "Create new clip")) return;

            try
            {
                int progressIndex = 0;
                int progressTotal = 1;
                EditorUtility.DisplayProgressBar("Create", clipPath, progressIndex++ / (float)progressTotal);

                AnimationUtility.SetAnimationClipSettings(newClip, AnimationUtility.GetAnimationClipSettings(currentClip));
                {
                    newClip.frameRate = currentClip.frameRate;
                    newClip.wrapMode = currentClip.wrapMode;
                    newClip.localBounds = currentClip.localBounds;
                    newClip.legacy = currentClip.legacy;
                }
                if (mode == ToolsCreateNewClipMode.Duplicate || mode == ToolsCreateNewClipMode.Mirror)
                {
                    #region Duplicate
                    foreach (var binding in AnimationUtility.GetCurveBindings(currentClip))
                    {
                        AnimationUtility.SetEditorCurve(newClip, binding, AnimationUtility.GetEditorCurve(currentClip, binding));
                    }
                    foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(currentClip))
                    {
                        AnimationUtility.SetObjectReferenceCurve(newClip, binding, AnimationUtility.GetObjectReferenceCurve(currentClip, binding));
                    }
                    AnimationUtility.SetAnimationEvents(newClip, AnimationUtility.GetAnimationEvents(currentClip));
                    #endregion
                }
                if (mode == ToolsCreateNewClipMode.Mirror)
                {
                    #region Mirror
                    {
                        #region SwapMirrorCurve
                        {
                            var bindings = AnimationUtility.GetCurveBindings(newClip);
                            Func<EditorCurveBinding, int> GetMirrorBindingIndex = (binding) =>
                            {
                                var mbinding = GetMirrorAnimationCurveBinding(binding);
                                if (!mbinding.HasValue) return -1;
                                return EditorCommon.ArrayIndexOf(bindings, mbinding.Value);
                            };
                            #region CreateMirrorCurve
                            foreach (var binding in bindings)
                            {
                                var mbinding = GetMirrorAnimationCurveBinding(binding);
                                if (!mbinding.HasValue) continue;
                                if (!EditorCommon.ArrayContains(bindings, mbinding.Value))
                                {
                                    var mcurve = new AnimationCurve();
                                    Action<float> AddKey = (value) =>
                                    {
                                        AddKeyframe(mcurve, 0, value);
                                        AddKeyframe(mcurve, newClip.length, value);
                                    };
                                    int dofIndex = GetDOFIndexFromCurveBinding(mbinding.Value);
                                    if (mbinding.Value.type == typeof(Animator))
                                    {
                                        AddKey(0f);
                                    }
                                    else if (mbinding.Value.type == typeof(Transform))
                                    {
                                        var boneIndex = GetBoneIndexFromCurveBinding(mbinding.Value);
                                        if (IsTransformPositionCurveBinding(mbinding.Value))
                                            AddKey(boneSaveTransforms[boneIndex].localPosition[dofIndex]);
                                        else if (IsTransformRotationCurveBinding(mbinding.Value))
                                        {
                                            if (uRotationCurveInterpolation.GetModeFromCurveData(mbinding.Value) == URotationCurveInterpolation.Mode.RawEuler)
                                                AddKey(boneSaveTransforms[boneIndex].localRotation.eulerAngles[dofIndex]);
                                            else if (uRotationCurveInterpolation.GetModeFromCurveData(mbinding.Value) == URotationCurveInterpolation.Mode.RawQuaternions)
                                                AddKey(boneSaveTransforms[boneIndex].localRotation[dofIndex]);
                                            else
                                                Assert.IsTrue(false);
                                        }
                                        else if (IsTransformScaleCurveBinding(mbinding.Value))
                                            AddKey(boneSaveTransforms[boneIndex].localScale[dofIndex]);
                                    }
                                    else if (IsSkinnedMeshRendererBlendShapeCurveBinding(mbinding.Value))
                                    {
                                        var boneIndex = GetBoneIndexFromCurveBinding(mbinding.Value);
                                        var renderer = bones[boneIndex].GetComponent<SkinnedMeshRenderer>();
                                        var name = PropertyName2BlendShapeName(mbinding.Value.propertyName);
                                        AddKey(blendShapeWeightSave.GetDefaultWeight(renderer, name));
                                    }
                                    else
                                    {
                                        Assert.IsTrue(false);
                                    }
                                    AnimationUtility.SetEditorCurve(newClip, mbinding.Value, mcurve);
                                }
                            }
                            bindings = AnimationUtility.GetCurveBindings(newClip);
                            #endregion

                            #region MirrorCurve
                            {
                                Action<int, int> SwapCurve = (indexA, indexB) =>
                                {
                                    var curveA = AnimationUtility.GetEditorCurve(newClip, bindings[indexA]);
                                    var curveB = AnimationUtility.GetEditorCurve(newClip, bindings[indexB]);
                                    AnimationUtility.SetEditorCurve(newClip, bindings[indexB], curveA);
                                    AnimationUtility.SetEditorCurve(newClip, bindings[indexA], curveB);
                                };
                                Action<int> MirrorCurve = (index) =>
                                {
                                    var curve = AnimationUtility.GetEditorCurve(newClip, bindings[index]);
                                    for (int i = 0; i < curve.length; i++)
                                    {
                                        var key = curve[i];
                                        key.value = -key.value;
                                        key.inTangent = -key.inTangent;
                                        key.outTangent = -key.outTangent;
                                        curve.MoveKey(i, key);
                                    }
                                    AnimationUtility.SetEditorCurve(newClip, bindings[index], curve);
                                };

                                bool[] doneFlag = new bool[bindings.Length];
                                for (int i = 0; i < bindings.Length; i++)
                                {
                                    if (doneFlag[i]) continue;
                                    doneFlag[i] = true;
                                    if (bindings[i].type == typeof(Animator))
                                    {
                                        #region Animator
                                        AnimatorIKIndex ikIndex = AnimatorIKIndex.None;
                                        AnimatorTDOFIndex tdofIndex = AnimatorTDOFIndex.None;
                                        var muscleIndex = GetMuscleIndexFromCurveBinding(bindings[i]);
                                        if (muscleIndex >= 0)
                                        {
                                            #region Muscle
                                            var mirrorMuscleIndex = GetMirrorMuscleIndex(muscleIndex);
                                            if (mirrorMuscleIndex >= 0)
                                            {
                                                var mirrorBindingIndex = GetMirrorBindingIndex(bindings[i]);
                                                doneFlag[mirrorBindingIndex] = true;
                                                SwapCurve(i, mirrorBindingIndex);
                                            }
                                            else if (muscleIndex == HumanTrait.MuscleFromBone(HumanTrait.BoneFromMuscle(muscleIndex), 0) ||
                                                    muscleIndex == HumanTrait.MuscleFromBone(HumanTrait.BoneFromMuscle(muscleIndex), 1))
                                            {
                                                MirrorCurve(i);
                                            }
                                            #endregion
                                        }
                                        else if (bindings[i].propertyName == AnimationCurveBindingAnimatorRootT[0].propertyName ||
                                                bindings[i].propertyName == AnimationCurveBindingAnimatorRootQ[1].propertyName ||
                                                bindings[i].propertyName == AnimationCurveBindingAnimatorRootQ[2].propertyName)
                                        {
                                            #region Root
                                            MirrorCurve(i);
                                            #endregion
                                        }
                                        else if ((ikIndex = GetIkTIndexFromCurveBinding(bindings[i])) != AnimatorIKIndex.None)
                                        {
                                            #region IKT
                                            var mirrorBindingIndex = GetMirrorBindingIndex(bindings[i]);
                                            if (mirrorBindingIndex >= 0)
                                            {
                                                doneFlag[mirrorBindingIndex] = true;
                                                var dofIndex = GetDOFIndexFromCurveBinding(bindings[i]);
                                                if (dofIndex == 0)
                                                {
                                                    MirrorCurve(i);
                                                }
                                                SwapCurve(i, mirrorBindingIndex);
                                                if (dofIndex == 0)
                                                {
                                                    MirrorCurve(i);
                                                }
                                            }
                                            #endregion
                                        }
                                        else if ((ikIndex = GetIkQIndexFromCurveBinding(bindings[i])) != AnimatorIKIndex.None)
                                        {
                                            #region IKQ
                                            EditorCurveBinding[] ikQBindings = new EditorCurveBinding[4];
                                            int[] bindingIndexes = new int[4];
                                            int[] mirrorBindingIndexes = new int[4];
                                            for (int dof = 0; dof < ikQBindings.Length; dof++)
                                            {
                                                ikQBindings[dof] = AnimationCurveBindingAnimatorIkQ(ikIndex, dof);
                                                bindingIndexes[dof] = ArrayUtility.FindIndex(bindings, x => x.propertyName == ikQBindings[dof].propertyName);
                                                string mirrorPropertyName = null;
                                                if (ikQBindings[dof].propertyName.IndexOf("Left") >= 0)
                                                    mirrorPropertyName = ikQBindings[dof].propertyName.Replace("Left", "Right");
                                                else if (ikQBindings[dof].propertyName.IndexOf("Right") >= 0)
                                                    mirrorPropertyName = ikQBindings[dof].propertyName.Replace("Right", "Left");
                                                Assert.IsNotNull(mirrorPropertyName);
                                                mirrorBindingIndexes[dof] = ArrayUtility.FindIndex(bindings, x => x.propertyName == mirrorPropertyName);
                                            }
                                            if (bindingIndexes[0] >= 0 && bindingIndexes[1] >= 0 && bindingIndexes[2] >= 0 && bindingIndexes[3] >= 0 &&
                                                mirrorBindingIndexes[0] >= 0 && mirrorBindingIndexes[1] >= 0 && mirrorBindingIndexes[2] >= 0 && mirrorBindingIndexes[3] >= 0)
                                            {
                                                for (int dof = 0; dof < ikQBindings.Length; dof++)
                                                {
                                                    SwapCurve(bindingIndexes[dof], mirrorBindingIndexes[QuaternionXMirrorSwapDof[dof]]);
                                                    doneFlag[bindingIndexes[dof]] = true;
                                                    doneFlag[mirrorBindingIndexes[QuaternionXMirrorSwapDof[dof]]] = true;
                                                }
                                            }
                                            #endregion
                                        }
                                        else if ((tdofIndex = GetTDOFIndexFromCurveBinding(bindings[i])) != AnimatorTDOFIndex.None)
                                        {
                                            #region TDOF
                                            var dofIndex = GetDOFIndexFromCurveBinding(bindings[i]);
                                            var mirrortdofIndex = AnimatorTDOFMirrorIndexes[(int)tdofIndex];
                                            if (mirrortdofIndex != AnimatorTDOFIndex.None)
                                            {
                                                var mirrorBindingIndex = GetMirrorBindingIndex(bindings[i]);
                                                if (mirrorBindingIndex >= 0)
                                                {
                                                    doneFlag[mirrorBindingIndex] = true;

                                                    var mirror = HumanBonesAnimatorTDOFIndex[(int)AnimatorTDOFIndex2HumanBodyBones[(int)mirrortdofIndex]].mirror;
                                                    if (mirror[dofIndex] < 0)
                                                    {
                                                        MirrorCurve(i);
                                                    }
                                                    SwapCurve(i, mirrorBindingIndex);
                                                    if (mirror[dofIndex] < 0)
                                                    {
                                                        MirrorCurve(i);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (dofIndex == 2)
                                                {
                                                    MirrorCurve(i);
                                                }
                                            }
                                            #endregion
                                        }
                                        #endregion
                                    }
                                    else if (IsSkinnedMeshRendererBlendShapeCurveBinding(bindings[i]))
                                    {
                                        #region BlendShape
                                        var boneIndex = GetBoneIndexFromCurveBinding(bindings[i]);
                                        if (boneIndex >= 0)
                                        {
                                            var mirrorBindingIndex = GetMirrorBindingIndex(bindings[i]);
                                            if (mirrorBindingIndex >= 0)
                                            {
                                                doneFlag[mirrorBindingIndex] = true;
                                                SwapCurve(i, mirrorBindingIndex);
                                            }
                                        }
                                        #endregion
                                    }
                                }
                            }
                            #endregion
                        }
                        #endregion

                        #region FullBakeKeyframe
                        {
                            var curves = new Dictionary<EditorCurveBinding, AnimationCurve>();
                            Func<EditorCurveBinding, AnimationCurve> GetCurve = (binding) =>
                            {
                                AnimationCurve curve;
                                if (!curves.TryGetValue(binding, out curve))
                                {
                                    curve = AnimationUtility.GetEditorCurve(newClip, binding);
                                    if (curve == null)
                                        curve = new AnimationCurve();
                                    curves.Add(binding, curve);
                                }
                                return curve;
                            };

                            Quaternion[] boneWroteRotation = new Quaternion[bones.Length];
                            Vector3[] boneWroteEuler = new Vector3[bones.Length];
                            for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                            {
                                boneWroteRotation[boneIndex] = boneSaveOriginalTransforms[boneIndex].localRotation;
                                boneWroteEuler[boneIndex] = boneWroteRotation[boneIndex].eulerAngles;
                            }
                            for (int frame = 0; frame <= GetLastFrame(); frame++)
                            {
                                var time = GetFrameTime(frame);
                                #region Generic
                                {
                                    for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                                    {
                                        if (isHuman && humanoidConflict[boneIndex]) continue;
                                        Vector3 position;
                                        Quaternion rotation;
                                        Vector3 scale;
                                        Vector3 positionMirrorOriginal;
                                        Quaternion rotationMirrorOriginal;
                                        Vector3 scaleMirrorOriginal;
                                        var mbi = mirrorBoneIndexes[boneIndex];
                                        if (mbi >= 0)
                                        {
                                            position = GetMirrorBoneLocalPosition(mbi, GetAnimationValueTransformPosition(mbi, time));
                                            rotation = GetMirrorBoneLocalRotation(mbi, GetAnimationValueTransformRotation(mbi, time));
                                            scale = GetMirrorBoneLocalScale(mbi, GetAnimationValueTransformScale(mbi, time));
                                            positionMirrorOriginal = GetMirrorBoneLocalPosition(mbi, boneSaveTransforms[mbi].localPosition);
                                            rotationMirrorOriginal = GetMirrorBoneLocalRotation(mbi, boneSaveTransforms[mbi].localRotation);
                                            scaleMirrorOriginal = GetMirrorBoneLocalScale(mbi, boneSaveTransforms[mbi].localScale);
                                        }
                                        else
                                        {
                                            position = GetMirrorBoneLocalPosition(boneIndex, GetAnimationValueTransformPosition(boneIndex, time));
                                            rotation = GetMirrorBoneLocalRotation(boneIndex, GetAnimationValueTransformRotation(boneIndex, time));
                                            scale = GetMirrorBoneLocalScale(boneIndex, GetAnimationValueTransformScale(boneIndex, time));
                                            positionMirrorOriginal = GetMirrorBoneLocalPosition(boneIndex, boneSaveTransforms[boneIndex].localPosition);
                                            rotationMirrorOriginal = GetMirrorBoneLocalRotation(boneIndex, boneSaveTransforms[boneIndex].localRotation);
                                            scaleMirrorOriginal = GetMirrorBoneLocalScale(boneIndex, boneSaveTransforms[boneIndex].localScale);
                                        }
                                        bool positionMirrorDifferent = false;
                                        bool rotationMirrorDifferent = false;
                                        bool scaleMirrorDifferent = false;
                                        {
                                            positionMirrorDifferent = Mathf.Abs(positionMirrorOriginal.x - boneSaveOriginalTransforms[boneIndex].localPosition.x) >= TransformPositionApproximatelyThreshold ||
                                                                        Mathf.Abs(positionMirrorOriginal.y - boneSaveOriginalTransforms[boneIndex].localPosition.y) >= TransformPositionApproximatelyThreshold ||
                                                                        Mathf.Abs(positionMirrorOriginal.z - boneSaveOriginalTransforms[boneIndex].localPosition.z) >= TransformPositionApproximatelyThreshold; ;
                                            {
                                                var eulerAngles = rotationMirrorOriginal.eulerAngles;
                                                var originalEulerAngles = boneSaveTransforms[boneIndex].localRotation.eulerAngles;
                                                rotationMirrorDifferent = Mathf.Abs(eulerAngles.x - originalEulerAngles.x) >= TransformRotationApproximatelyThreshold ||
                                                                            Mathf.Abs(eulerAngles.y - originalEulerAngles.y) >= TransformRotationApproximatelyThreshold ||
                                                                            Mathf.Abs(eulerAngles.z - originalEulerAngles.z) >= TransformRotationApproximatelyThreshold;
                                            }
                                            scaleMirrorDifferent = Mathf.Abs(scaleMirrorOriginal.x - boneSaveOriginalTransforms[boneIndex].localScale.x) >= TransformScaleApproximatelyThreshold ||
                                                                    Mathf.Abs(scaleMirrorOriginal.y - boneSaveOriginalTransforms[boneIndex].localScale.y) >= TransformScaleApproximatelyThreshold ||
                                                                    Mathf.Abs(scaleMirrorOriginal.z - boneSaveOriginalTransforms[boneIndex].localScale.z) >= TransformScaleApproximatelyThreshold;
                                        }
                                        if (IsHaveAnimationCurveTransformPosition(boneIndex) || IsHaveAnimationCurveTransformPosition(mbi) || positionMirrorDifferent)
                                        {
                                            for (int dof = 0; dof < 3; dof++)
                                            {
                                                var curve = GetCurve(AnimationCurveBindingTransformPosition(boneIndex, dof));
                                                SetKeyframe(curve, time, position[dof]);
                                            }
                                        }
                                        {
                                            var rotationMode = IsHaveAnimationCurveTransformRotation(boneIndex);
                                            var mrotationMode = IsHaveAnimationCurveTransformRotation(mbi);
                                            if (rotationMode != URotationCurveInterpolation.Mode.Undefined || mrotationMode != URotationCurveInterpolation.Mode.Undefined || rotationMirrorDifferent)
                                            {
                                                if (rotationMode == URotationCurveInterpolation.Mode.RawEuler)
                                                {
                                                    var eulerAngles = rotation.eulerAngles;
                                                    eulerAngles = FixReverseRotationEuler(eulerAngles, boneWroteEuler[boneIndex]);
                                                    boneWroteEuler[boneIndex] = eulerAngles;
                                                    for (int dof = 0; dof < 3; dof++)
                                                    {
                                                        var curve = GetCurve(AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.RawEuler));
                                                        SetKeyframe(curve, time, eulerAngles[dof]);
                                                    }
                                                }
                                                else
                                                {
                                                    rotation = FixReverseRotationQuaternion(rotation, boneWroteRotation[boneIndex]);
                                                    boneWroteRotation[boneIndex] = rotation;
                                                    for (int dof = 0; dof < 4; dof++)
                                                    {
                                                        var curve = GetCurve(AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.RawQuaternions));
                                                        SetKeyframe(curve, time, rotation[dof]);
                                                    }
                                                }
                                            }
                                        }
                                        if (IsHaveAnimationCurveTransformScale(boneIndex) || IsHaveAnimationCurveTransformScale(mbi) || scaleMirrorDifferent)
                                        {
                                            if (vaw.editorSettings.settingGenericMirrorScale)
                                            {
                                                for (int dof = 0; dof < 3; dof++)
                                                {
                                                    var curve = GetCurve(AnimationCurveBindingTransformScale(boneIndex, dof));
                                                    SetKeyframe(curve, time, scale[dof]);
                                                }
                                            }
                                        }
                                    }
                                }
                                #endregion
                            }

                            #region SmoothTangents
                            foreach (var pair in curves)
                            {
                                var curve = pair.Value;
                                if (curve == null || curve.length <= 0) continue;

                                for (int i = 0; i < curve.length; i++)
                                    curve.SmoothTangents(i, 0f);
                                AnimationUtility.SetEditorCurve(newClip, pair.Key, curve);
                            }
                            #endregion
                        }
                        #endregion
                    }
                    #endregion
                }

                bool added = false;
                if (uAw.GetLinkedWithTimeline())
                {
                    #region Timeline
#if VERYANIMATION_TIMELINE
                    Undo.RecordObject(uAw.GetTimelineCurrentDirector(), "Create New Clip");
                    var animationTrack = uAw.GetTimelineAnimationTrack();
                    Undo.RecordObject(animationTrack, "Create New Clip");
                    var timelineClip = animationTrack.CreateClip(newClip);
                    timelineClip.displayName = Path.GetFileNameWithoutExtension(clipPath);
                    uAw.ForceRefresh();
                    uAw.EditSequencerClip(timelineClip);
                    added = true;
#else
                    Assert.IsTrue(false);
#endif
                    #endregion
                }
                else
                {
                    #region Animator
                    if (vaw.animator != null && vaw.animator.runtimeAnimatorController != null)
                    {
                        var ac = EditorCommon.GetAnimatorController(vaw.animator);
                        AnimationClip virtualClip = null;
                        #region AnimatorOverrideController
                        if (vaw.animator.runtimeAnimatorController is AnimatorOverrideController)
                        {
                            var owc = vaw.animator.runtimeAnimatorController as AnimatorOverrideController;
                            {
                                var srcList = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                                owc.GetOverrides(srcList);
                                foreach (var pair in srcList)
                                {
                                    if (pair.Value == currentClip)
                                    {
                                        virtualClip = pair.Key;
                                        added = true;
                                        break;
                                    }
                                }
                            }
                        }
                        #endregion
                        #region AnimatorControllerLayer
                        if (ac != null)
                        {
                            Undo.RecordObject(ac, "Create New Clip");
                            int layerIndex = 0;
                            AnimatorState srcState = null;
                            for (int index = 0; index < ac.layers.Length; index++)
                            {
                                var layer = ac.layers[index];
                                Action<AnimatorStateMachine> FindStateMachine = null;
                                FindStateMachine = (stateMachine) =>
                                {
                                    foreach (var state in stateMachine.states)
                                    {
                                        if (state.state.motion is UnityEditor.Animations.BlendTree)
                                        {
                                            Action<UnityEditor.Animations.BlendTree> FindBlendTree = null;
                                            FindBlendTree = (blendTree) =>
                                            {
                                                if (blendTree.children == null) return;
                                                var children = blendTree.children;
                                                for (int i = 0; i < children.Length; i++)
                                                {
                                                    if (children[i].motion is UnityEditor.Animations.BlendTree)
                                                    {
                                                        FindBlendTree(children[i].motion as UnityEditor.Animations.BlendTree);
                                                    }
                                                    else
                                                    {
                                                        if (children[i].motion == currentClip || (virtualClip != null && children[i].motion == virtualClip))
                                                        {
                                                            layerIndex = index;
                                                            srcState = state.state;
                                                            break;
                                                        }
                                                    }
                                                }
                                                blendTree.children = children;
                                            };
                                            FindBlendTree(state.state.motion as UnityEditor.Animations.BlendTree);
                                        }
                                        else
                                        {
                                            if (state.state.motion == currentClip || (virtualClip != null && state.state.motion == virtualClip))
                                            {
                                                layerIndex = index;
                                                srcState = state.state;
                                                break;
                                            }
                                        }
                                    }
                                    foreach (var childStateMachine in stateMachine.stateMachines)
                                    {
                                        FindStateMachine(childStateMachine.stateMachine);
                                    }
                                };
                                FindStateMachine(layer.stateMachine);
                            }
                            var animatorState = ac.AddMotion(newClip, layerIndex);
                            if (srcState != null)
                            {
                                animatorState.behaviours = srcState.behaviours;
                                animatorState.transitions = srcState.transitions;
                                animatorState.mirrorParameterActive = srcState.mirrorParameterActive;
                                animatorState.cycleOffsetParameterActive = srcState.cycleOffsetParameterActive;
                                animatorState.speedParameterActive = srcState.speedParameterActive;
                                animatorState.mirrorParameter = srcState.mirrorParameter;
                                animatorState.cycleOffsetParameter = srcState.cycleOffsetParameter;
                                animatorState.speedParameter = srcState.speedParameter;
                                animatorState.tag = srcState.tag;
                                animatorState.writeDefaultValues = srcState.writeDefaultValues;
                                animatorState.iKOnFeet = srcState.iKOnFeet;
                                animatorState.mirror = srcState.mirror;
                                animatorState.cycleOffset = srcState.cycleOffset;
                                animatorState.speed = srcState.speed;
                                animatorState.motion = newClip;
                                animatorState.timeParameter = srcState.timeParameter;
                                animatorState.timeParameterActive = srcState.timeParameterActive;
                                added = true;
                            }
                        }
                        #endregion
                    }
                    #endregion
                    #region Animation
                    if (vaw.animation != null)
                    {
                        Undo.RecordObject(vaw.animation, "Create New Clip");
                        var animations = AnimationUtility.GetAnimationClips(vaw.gameObject);
                        ArrayUtility.Add(ref animations, newClip);
                        AnimationUtility.SetAnimationClips(vaw.animation, animations);
                        added = true;
                    }
                    #endregion

                    if (!added)
                        Debug.LogWarningFormat(Language.GetText(Language.Help.LogAnimationClipAddError), newClip);

                    uAw.ForceRefresh();
                    uAw.SetSelectionAnimationClip(newClip);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private void ToolsCreateNewKeyframe(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "Create New Keyframe")) return;

            try
            {
                int progressIndex = 0;
                int progressTotal = 1;
                EditorUtility.DisplayProgressBar("Create New Keyframe", "", progressIndex++ / (float)progressTotal);

                bool selectRoot = selectionBones != null && selectionBones.Contains(0);
                var humanoidIndexes = SelectionGameObjectsHumanoidIndex();
                var boneIndexes = SelectionGameObjectsOtherHumanoidBoneIndex();
                List<float> times = new List<float>();
                {
                    int interval = 0;
                    for (int frame = toolCreateNewKeyframe_FirstFrame; frame <= toolCreateNewKeyframe_LastFrame; frame++)
                    {
                        bool set = frame == toolCreateNewKeyframe_FirstFrame || frame == toolCreateNewKeyframe_LastFrame || toolCreateNewKeyframe_IntervalFrame == 0;
                        if (!set)
                        {
                            set = ++interval >= toolCreateNewKeyframe_IntervalFrame;
                        }
                        if (!set) continue;
                        interval = 0;
                        var time = GetFrameTime(frame);
                        times.Add(time);
                    }
                }

                if (isHuman)
                {
                    if (selectRoot)
                    {
                        if (toolCreateNewKeyframe_AnimatorRootT)
                        {
                            var saveValues = new Dictionary<float, Vector3>();
                            foreach (var time in times)
                                saveValues.Add(time, GetAnimationValueAnimatorRootT(time));
                            foreach (var pair in saveValues)
                                SetAnimationValueAnimatorRootT(pair.Value, pair.Key);
                        }
                        if (toolCreateNewKeyframe_AnimatorRootQ)
                        {
                            var saveValues = new Dictionary<float, Quaternion>();
                            foreach (var time in times)
                                saveValues.Add(time, GetAnimationValueAnimatorRootQ(time));
                            foreach (var pair in saveValues)
                                SetAnimationValueAnimatorRootQ(pair.Value, pair.Key);
                        }
                    }
                    foreach (var humanoidIndex in humanoidIndexes)
                    {
                        if (toolCreateNewKeyframe_AnimatorMuscle)
                        {
                            for (int dof = 0; dof < 3; dof++)
                            {
                                var muscleIndex = HumanTrait.MuscleFromBone((int)humanoidIndex, dof);
                                if (muscleIndex < 0)
                                    continue;
                                var saveValues = new Dictionary<float, float>();
                                foreach (var time in times)
                                    saveValues.Add(time, GetAnimationValueAnimatorMuscle(muscleIndex, time));
                                foreach (var pair in saveValues)
                                    SetAnimationValueAnimatorMuscle(muscleIndex, pair.Value, pair.Key);
                            }
                        }
                        if (humanoidHasTDoF && toolCreateNewKeyframe_AnimatorTDOF)
                        {
                            if (HumanBonesAnimatorTDOFIndex[(int)humanoidIndex] != null)
                            {
                                var tdof = HumanBonesAnimatorTDOFIndex[(int)humanoidIndex].index;
                                var saveValues = new Dictionary<float, Vector3>();
                                foreach (var time in times)
                                    saveValues.Add(time, GetAnimationValueAnimatorTDOF(tdof, time));
                                foreach (var pair in saveValues)
                                    SetAnimationValueAnimatorTDOF(tdof, pair.Value, pair.Key);
                            }
                        }
                    }
                }

                foreach (var boneIndex in boneIndexes)
                {
                    if (isHuman && humanoidConflict[boneIndex])
                        continue;
                    if (rootMotionBoneIndex >= 0 && boneIndex == 0)
                        continue;

                    if (toolCreateNewKeyframe_TransformPosition)
                    {
                        var saveValues = new Dictionary<float, Vector3>();
                        foreach (var time in times)
                            saveValues.Add(time, GetAnimationValueTransformPosition(boneIndex, time));
                        foreach (var pair in saveValues)
                            SetAnimationValueTransformPosition(boneIndex, pair.Value, pair.Key);
                    }
                    if (toolCreateNewKeyframe_TransformRotation)
                    {
                        var saveValues = new Dictionary<float, Quaternion>();
                        foreach (var time in times)
                            saveValues.Add(time, GetAnimationValueTransformRotation(boneIndex, time));
                        foreach (var pair in saveValues)
                            SetAnimationValueTransformRotation(boneIndex, pair.Value, pair.Key);
                    }
                    if (toolCreateNewKeyframe_TransformScale)
                    {
                        var saveValues = new Dictionary<float, Vector3>();
                        foreach (var time in times)
                            saveValues.Add(time, GetAnimationValueTransformScale(boneIndex, time));
                        foreach (var pair in saveValues)
                            SetAnimationValueTransformScale(boneIndex, pair.Value, pair.Key);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private void ToolsRotationCurveInterpolation(AnimationClip clip, RotationCurveInterpolationMode rotationCurveInterpolationMode)
        {
            if (!ToolsFixOverRotationCurve(clip)) return;

            if (!ToolsCommonBefore(clip, "RotationCurveInterpolation")) return;

            try
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                int progressIndex = 0;
                int progressTotal = bindings.Length + 1;

                {
                    List<EditorCurveBinding> convertBindings = new List<EditorCurveBinding>();
                    for (int i = 0; i < bindings.Length; i++)
                    {
                        EditorUtility.DisplayProgressBar("Read", string.IsNullOrEmpty(bindings[i].path) ? bindings[i].propertyName : bindings[i].path, progressIndex++ / (float)progressTotal);
                        if (!IsTransformRotationCurveBinding(bindings[i])) continue;
                        var mode = uRotationCurveInterpolation.GetModeFromCurveData(bindings[i]);
                        if (convertBindings.FindIndex((x) => x.path == bindings[i].path) < 0)
                        {
                            var boneIndex = GetBoneIndexFromCurveBinding(bindings[i]);
                            if (boneIndex >= 0)
                            {
                                switch (mode)
                                {
                                case URotationCurveInterpolation.Mode.RawQuaternions:
                                    if (rotationCurveInterpolationMode != RotationCurveInterpolationMode.Quaternion)
                                    {
                                        for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                                            convertBindings.Add(AnimationCurveBindingTransformRotation(boneIndex, dofIndex, URotationCurveInterpolation.Mode.Baked));
                                        for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                                            convertBindings.Add(AnimationCurveBindingTransformRotation(boneIndex, dofIndex, URotationCurveInterpolation.Mode.NonBaked));
                                    }
                                    else
                                    {
                                        for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                                            convertBindings.Add(AnimationCurveBindingTransformRotation(boneIndex, dofIndex, URotationCurveInterpolation.Mode.Baked));
                                    }
                                    break;
                                case URotationCurveInterpolation.Mode.RawEuler:
                                    if (rotationCurveInterpolationMode != RotationCurveInterpolationMode.EulerAngles)
                                    {
                                        for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                                            convertBindings.Add(AnimationCurveBindingTransformRotation(boneIndex, dofIndex, URotationCurveInterpolation.Mode.RawEuler));
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    {
                        URotationCurveInterpolation.Mode mode = (URotationCurveInterpolation.Mode)(-1);
                        switch (rotationCurveInterpolationMode)
                        {
                        case RotationCurveInterpolationMode.Quaternion: mode = URotationCurveInterpolation.Mode.NonBaked; break;
                        case RotationCurveInterpolationMode.EulerAngles: mode = URotationCurveInterpolation.Mode.RawEuler; break;
                        default: Assert.IsTrue(false); break;
                        }
                        EditorUtility.DisplayProgressBar("Convert", "", progressIndex++ / (float)progressTotal);
                        if (convertBindings.Count > 0)
                            uRotationCurveInterpolation.SetInterpolation(clip, convertBindings.ToArray(), mode);
                    }
                }
                #region FixReverseRotation
                if (rotationCurveInterpolationMode == RotationCurveInterpolationMode.EulerAngles)
                {
                    bindings = AnimationUtility.GetCurveBindings(clip);
                    foreach (var binding in bindings)
                    {
                        if (!IsTransformRotationCurveBinding(binding)) continue;
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        if (FixReverseRotationEuler(curve))
                            AnimationUtility.SetEditorCurve(clip, binding, curve);
                    }
                }
                #endregion
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private void ToolsKeyframeReduction(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "KeyframeReduction")) return;

            ToolsRotationCurveInterpolation(clip, RotationCurveInterpolationMode.Quaternion);

            AnimationClip tmpClip = null;
            GameObject tmpObject = null;

            try
            {
                VeryAnimationWindow.CustomAssetModificationProcessor.Pause();

                var assetPath = string.Format("{0}/{1}_tmp.dae", EditorCommon.GetAssetPath(clip), clip.name);
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                var path = Application.dataPath + assetPath.Remove(0, "Assets".Length);

                if (!transformPoseSave.ResetPrefabTransform())
                    transformPoseSave.ResetOriginalTransform();
                if (!blendShapeWeightSave.ResetPrefabWeight())
                    blendShapeWeightSave.ResetOriginalWeight();

                tmpClip = AnimationClip.Instantiate(clip);
                tmpClip.hideFlags |= HideFlags.HideAndDontSave;
                tmpObject = GameObject.Instantiate(vaw.gameObject);
                tmpObject.hideFlags |= HideFlags.HideAndDontSave;
                EditorCommon.DisableOtherBehaviors(tmpObject);

                #region AddOtherCurve
                var otherCurveDic = new Dictionary<EditorCurveBinding, EditorCurveBinding>();
                {
                    var bindings = AnimationUtility.GetCurveBindings(tmpClip);
                    foreach (var binding in bindings)
                    {
                        if (binding.type == typeof(Transform))
                            continue;
                        if (binding.type == typeof(Animator))
                        {
                            if (!isHuman && (binding.propertyName.StartsWith("RootT.") || binding.propertyName.StartsWith("RootQ.")))
                            {
                                //Root
                            }
                            else if (binding.propertyName.StartsWith("MotionT.") || binding.propertyName.StartsWith("MotionQ."))
                            {
                                //Motion
                            }
                            else if (!IsAnimatorReservedPropertyName(binding.propertyName))
                            {
                                //ParameterRelatedCurves
                            }
                            else
                            {
                                continue;
                            }
                        }
                        var valueType = AnimationUtility.GetEditorCurveValueType(vaw.gameObject, binding);
                        if (valueType != typeof(float))
                            continue;
                        var curve = AnimationUtility.GetEditorCurve(tmpClip, binding);
                        if (curve == null)
                            continue;
                        AnimationUtility.SetEditorCurve(tmpClip, binding, null);
                        var add = new GameObject(binding.GetHashCode().ToString());
                        add.hideFlags |= HideFlags.HideAndDontSave;
                        add.transform.SetParent(tmpObject.transform);
                        var addBinding = new EditorCurveBinding()
                        {
                            type = typeof(Transform),
                            path = AnimationUtility.CalculateTransformPath(add.transform, tmpObject.transform),
                            propertyName = EditorCurveBindingTransformScalePropertyNames[0],
                        };
                        AnimationUtility.SetEditorCurve(tmpClip, addBinding, curve);
                        otherCurveDic.Add(binding, addBinding);
                    }
                }
                #endregion

                List<Transform> transforms;
                {
                    var tmpBones = EditorCommon.GetHierarchyGameObject(tmpObject);
                    transforms = new List<Transform>();
                    foreach (var b in tmpBones)
                        transforms.Add(b.transform);
                }

                AnimationClip[] clips = new AnimationClip[] { tmpClip };

                DaeExporter exporter = new DaeExporter()
                {
                    settings_activeOnly = false,
                    settings_exportMesh = false,
                    settings_iKOnFeet = false,
                    settings_animationRigging = false,
                    settings_animationType = isHuman ? ModelImporterAnimationType.Human : (vaw.animator != null ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.Legacy),
                    settings_avatar = vaw.animator != null ? vaw.animator.avatar : null,
                    settings_motionNodePath = rootMotionBoneIndex >= 0 ? bonePaths[rootMotionBoneIndex] : null,
                };
                var result = exporter.Export(path, transforms, clips);
                if (result)
                {
                    Assert.IsTrue(exporter.exportedFiles.Count == 2);
                    try
                    {
                        AnimationClip reductionClip = null;
                        {
                            var subAssetPath = FileUtil.GetProjectRelativePath(exporter.exportedFiles[1]);
                            var importer = AssetImporter.GetAtPath(subAssetPath);
                            if (importer is ModelImporter)
                            {
                                var modelImporter = importer as ModelImporter;
                                modelImporter.importAnimation = true;
                                modelImporter.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
                                modelImporter.animationRotationError = toolKeyframeReduction_RotationError;
                                modelImporter.animationPositionError = toolKeyframeReduction_PositionError;
                                modelImporter.animationScaleError = toolKeyframeReduction_ScaleAndOthersError;
                                modelImporter.SaveAndReimport();
                            }
                            reductionClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(subAssetPath);
                        }
                        Assert.IsNotNull(reductionClip);
                        {
                            Dictionary<EditorCurveBinding, AnimationCurve> curves = new Dictionary<EditorCurveBinding, AnimationCurve>();
                            var bindings = AnimationUtility.GetCurveBindings(clip);
                            foreach (var binding in bindings)
                            {
                                var srcCurve = AnimationUtility.GetEditorCurve(clip, binding);
                                if (srcCurve == null) continue;
                                var reductionCurve = AnimationUtility.GetEditorCurve(reductionClip, binding);
                                if (reductionCurve != null)
                                {
                                    #region CopyCurve
                                    if (!toolKeyframeReduction_EnableHumanoid && binding.type == typeof(Animator))
                                        continue;
                                    if (toolKeyframeReduction_EnableHumanoid && binding.type == typeof(Animator) &&
                                        !toolKeyframeReduction_EnableHumanoidRootAndIKGoal && (IsAnimatorRootCurveBinding(binding) || GetIkTIndexFromCurveBinding(binding) != AnimatorIKIndex.None || GetIkQIndexFromCurveBinding(binding) != AnimatorIKIndex.None))
                                        continue;
                                    if (!toolKeyframeReduction_EnableGeneric && binding.type == typeof(Transform))
                                        continue;
                                    if (srcCurve.length > reductionCurve.length)
                                    {
                                        if (GetRootQDofIndexFromCurveBinding(binding) >= 0 ||
                                             GetIkQIndexFromCurveBinding(binding) >= 0 ||
                                             uRotationCurveInterpolation.GetModeFromCurveData(binding) == URotationCurveInterpolation.Mode.RawQuaternions)
                                        {
                                            #region Quaternion
                                            bool allClear = true;
                                            for (int dof = 0; dof < 4; dof++)
                                            {
                                                var subBinding = GetDOFIndexChangeCurveBinding(binding, dof);
                                                var subSrcCurve = AnimationUtility.GetEditorCurve(clip, subBinding);
                                                var subReductionCurve = AnimationUtility.GetEditorCurve(reductionClip, subBinding);
                                                if (subSrcCurve == null || subReductionCurve == null ||
                                                    subSrcCurve.length <= subReductionCurve.length)
                                                {
                                                    allClear = false;
                                                    break;
                                                }
                                            }
                                            if (!allClear)
                                                continue;
                                            #endregion
                                        }
                                        curves.Add(binding, reductionCurve);
                                    }
                                    #endregion
                                }
                                else
                                {
                                    #region CopyOtherCurve
                                    if (!toolKeyframeReduction_EnableOther)
                                        continue;
                                    EditorCurveBinding origBinding;
                                    if (otherCurveDic.TryGetValue(binding, out origBinding))
                                    {
                                        reductionCurve = AnimationUtility.GetEditorCurve(reductionClip, origBinding);
                                        if (reductionCurve != null)
                                        {
                                            if (srcCurve.length > reductionCurve.length)
                                            {
                                                curves.Add(binding, reductionCurve);
                                            }
                                        }
                                    }
                                    #endregion
                                }
                            }
                            foreach (var pair in curves)
                            {
                                AnimationUtility.SetEditorCurve(clip, pair.Key, null);
                            }
                            foreach (var pair in curves)
                            {
                                AnimationUtility.SetEditorCurve(clip, pair.Key, pair.Value);
                            }
                        }
                    }
                    finally
                    {
                        foreach (var p in exporter.exportedFiles)
                        {
                            var pTmp = FileUtil.GetProjectRelativePath(p);
                            AssetDatabase.DeleteAsset(pTmp);
                        }
                        AssetDatabase.Refresh();
                    }
                }
                #region SimpleReductionKeyframe
                if (toolKeyframeReduction_EnableOther)
                {
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (binding.type == typeof(Animator) || binding.type == typeof(Transform))
                            continue;
                        var valueType = AnimationUtility.GetEditorCurveValueType(vaw.gameObject, binding);
                        if (valueType == null || valueType == typeof(float))
                            continue;
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        if (curve == null)
                            continue;
                        bool update = false;
                        for (int i = 1; i < curve.length - 1; i++)
                        {
                            if (Mathf.Approximately(curve[i - 1].value, curve[i].value) &&
                                Mathf.Approximately(curve[i + 1].value, curve[i].value))
                            {
                                curve.RemoveKey(i--);
                                update = true;
                            }
                        }
                        if (update)
                            AnimationUtility.SetEditorCurve(clip, binding, curve);
                    }
                    foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    {
                        var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                        if (curve == null)
                            continue;
                        bool update = false;
                        var keys = new List<ObjectReferenceKeyframe>(curve);
                        for (int i = 1; i < keys.Count - 1; i++)
                        {
                            if (keys[i - 1].value == keys[i].value)
                            {
                                keys.RemoveAt(i--);
                                update = true;
                            }
                        }
                        if (update)
                            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
                    }
                }
                #endregion
            }
            finally
            {
                if (tmpClip != null)
                    AnimationClip.DestroyImmediate(tmpClip);
                if (tmpObject != null)
                    GameObject.DestroyImmediate(tmpObject);
                VeryAnimationWindow.CustomAssetModificationProcessor.Resume();
            }

            ToolsCommonAfter();
        }
        private void ToolsEnsureQuaternionContinuity(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "EnsureQuaternionContinuity")) return;

            {
                clip.EnsureQuaternionContinuity();
            }

            ToolsCommonAfter();
        }
        private void ToolsCleanup(AnimationClip clip)
        {
            if (!ToolsCommonBefore(clip, "Cleanup")) return;

            try
            {
                Action<HumanBodyBones> RemoveMuscleCurve = (hi) =>
                {
                    for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                    {
                        var mi = HumanTrait.MuscleFromBone((int)hi, dofIndex);
                        if (mi < 0) continue;
                        AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorMuscle(mi), null);
                    }
                };

                int progressIndex = 0;
                int progressTotal = 16;

                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveRoot)
                {
                    for (int i = 0; i < AnimationCurveBindingAnimatorRootT.Length; i++)
                        AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorRootT[i], null);
                    for (int i = 0; i < AnimationCurveBindingAnimatorRootQ.Length; i++)
                        AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorRootQ[i], null);
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveIK)
                {
                    for (int ikIndex = 0; ikIndex < (int)AnimatorIKIndex.Total; ikIndex++)
                    {
                        for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                            AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorIkT((AnimatorIKIndex)ikIndex, dofIndex), null);
                        for (int dofIndex = 0; dofIndex < 4; dofIndex++)
                            AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorIkQ((AnimatorIKIndex)ikIndex, dofIndex), null);
                    }
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveTDOF)
                {
                    for (int tdofIndex = 0; tdofIndex < (int)AnimatorTDOFIndex.Total; tdofIndex++)
                    {
                        for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                            AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorTDOF((AnimatorTDOFIndex)tdofIndex, dofIndex), null);
                    }
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveMotion)
                {
                    for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                        AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorMotionT[dofIndex], null);
                    for (int dofIndex = 0; dofIndex < 4; dofIndex++)
                        AnimationUtility.SetEditorCurve(clip, AnimationCurveBindingAnimatorMotionQ[dofIndex], null);
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveFinger)
                {
                    for (var hi = HumanBodyBones.LeftThumbProximal; hi <= HumanBodyBones.RightLittleDistal; hi++)
                        RemoveMuscleCurve(hi);
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveEyes)
                {
                    RemoveMuscleCurve(HumanBodyBones.LeftEye);
                    RemoveMuscleCurve(HumanBodyBones.RightEye);
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveJaw)
                {
                    RemoveMuscleCurve(HumanBodyBones.Jaw);
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveToes)
                {
                    RemoveMuscleCurve(HumanBodyBones.LeftToes);
                    RemoveMuscleCurve(HumanBodyBones.RightToes);
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveTransformPosition || toolCleanup_RemoveTransformRotation || toolCleanup_RemoveTransformScale)
                {
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (binding.type == typeof(Transform))
                        {
                            if ((toolCleanup_RemoveTransformPosition && binding.propertyName.StartsWith("m_LocalPosition.")) ||
                                (toolCleanup_RemoveTransformRotation && (binding.propertyName.StartsWith("m_LocalRotation.") || binding.propertyName.StartsWith("localEulerAngles"))) ||
                                (toolCleanup_RemoveTransformScale && binding.propertyName.StartsWith("m_LocalScale.")))
                            {
                                AnimationUtility.SetEditorCurve(clip, binding, null);
                            }
                        }
                    }
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveBlendShape)
                {
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (IsSkinnedMeshRendererBlendShapeCurveBinding(binding))
                        {
                            AnimationUtility.SetEditorCurve(clip, binding, null);
                        }
                    }
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveObjectReference)
                {
                    foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    {
                        AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                    }
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveEvent)
                {
                    AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveMissing)
                {
                    foreach (var binding in uAw.GetMissingCurveBindings())
                    {
                        if (!binding.isPPtrCurve)
                            AnimationUtility.SetEditorCurve(clip, binding, null);
                        else
                            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                    }
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveHumanoidConflict && isHuman)
                {
                    List<string> paths = new List<string>();
                    for (int i = 0; i < bones.Length; i++)
                    {
                        if (humanoidConflict[i])
                        {
                            paths.Add(AnimationUtility.CalculateTransformPath(bones[i].transform, vaw.gameObject.transform.transform));
                        }
                    }
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (binding.type != typeof(Transform)) continue;
                        if (!paths.Contains(binding.path)) continue;
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                    }
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveRootMotionConflict && rootMotionBoneIndex >= 0)
                {
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (!IsTransformPositionCurveBinding(binding) && !IsTransformRotationCurveBinding(binding)) continue;
                        var boneIndex = GetBoneIndexFromCurveBinding(binding);
                        if (boneIndex == 0)
                            AnimationUtility.SetEditorCurve(clip, binding, null);
                    }
                }
                EditorUtility.DisplayProgressBar("Cleanup", "", progressIndex++ / (float)progressTotal);
                if (toolCleanup_RemoveUnnecessary)
                {
                    ToolsReductionCurve(clip);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private void ToolsFixErrors(AnimationClip clip)
        {
            if (!ToolsFixOverRotationCurve(clip)) return;

            if (!ToolsCommonBefore(clip, "Fix Errors")) return;

            try
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);

                int progressIndex = 0;
                int progressTotal = bindings.Length;

                foreach (var binding in bindings)
                {
                    EditorUtility.DisplayProgressBar("Fix Errors", "", progressIndex++ / (float)progressTotal);

                    #region There must be at least two keyframes. If not, an Assert will occur.[AnimationUtility.GetEditorCurve]
                    if (IsTransformRotationCurveBinding(binding) && uRotationCurveInterpolation.GetModeFromCurveData(binding) == URotationCurveInterpolation.Mode.RawQuaternions)
                    {
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        if (curve.length <= 1)
                        {
                            Action<float> ErrorAvoidance = (time) =>
                            {
                                while (curve.length < 2)
                                {
                                    var addTime = 0f;
                                    if (time != 0f) addTime = 0f;
                                    else if (clip.length != 0f) addTime = clip.length;
                                    else addTime = 1f;
                                    AddKeyframe(curve, addTime, curve.Evaluate(addTime));
                                }
                            };
                            ErrorAvoidance(0f);
                            AnimationUtility.SetEditorCurve(clip, binding, curve);
                            Debug.LogWarningFormat(Language.GetText(Language.Help.LogFixErrors), binding.path, binding.propertyName);
                        }
                    }
                    #endregion
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private void ToolsAnimCompression(AnimationClip clip, bool isAllClip)
        {
            if (!ToolsCommonBefore(clip, "Anim Compression")) return;

            AnimationClip[] allClips;
            if (!isAllClip)
                allClips = new AnimationClip[] { clip };
            else
                allClips = AnimationUtility.GetAnimationClips(vaw.gameObject).Distinct().ToArray();

            try
            {
                int progressIndex = 0;
                int progressTotal = allClips.Length;
                foreach (var c in allClips)
                {
                    EditorUtility.DisplayProgressBar("Anim Compression", "", progressIndex++ / (float)progressTotal);

                    if ((c.hideFlags & HideFlags.NotEditable) != HideFlags.None)
                    {
                        EditorCommon.ShowNotification("Read-Only");
                        Debug.LogErrorFormat(Language.GetText(Language.Help.LogAnimationClipReadOnlyError), c.name);
                        continue;
                    }

                    bool changed = false;

                    var so = new SerializedObject(c);
                    {
                        var sp = so.FindProperty("m_Compressed");
                        if (sp.boolValue != toolAnimCompression_Compressed)
                        {
                            sp.boolValue = toolAnimCompression_Compressed;
                            changed = true;
                        }
                    }
                    if (!c.legacy)
                    {
                        var sp = so.FindProperty("m_UseHighQualityCurve");
                        if (sp.boolValue != toolAnimCompression_UseHighQualityCurve)
                        {
                            sp.boolValue = toolAnimCompression_UseHighQualityCurve;
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        so.ApplyModifiedProperties();
                        Debug.LogFormat("<color=blue>[Very Animation]</color>Anim Compression Settings Changed. [{0}]", c.name);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ToolsCommonAfter();
        }
        private void ToolsExport()
        {
            string path = EditorUtility.SaveFilePanel("Export",
                                                        EditorCommon.GetAssetPath(currentClip),
                                                        vaw.gameObject.name + ".dae", "dae");
            if (string.IsNullOrEmpty(path))
                return;

            if (!transformPoseSave.ResetPrefabTransform())
                transformPoseSave.ResetOriginalTransform();
            if (!blendShapeWeightSave.ResetPrefabWeight())
                blendShapeWeightSave.ResetOriginalWeight();

            var transforms = new List<Transform>(bones.Length);
            foreach (var b in bones)
                transforms.Add(b.transform);

            AnimationClip[] clips = null;
            switch (toolExport_AnimationMode)
            {
            case ExportAnimationMode.None:
                clips = null;
                break;
            case ExportAnimationMode.CurrentClip:
                clips = new AnimationClip[] { currentClip };
                break;
            case ExportAnimationMode.AllClips:
                clips = AnimationUtility.GetAnimationClips(vaw.gameObject).Distinct().ToArray();
                break;
            }

            try
            {
                VeryAnimationWindow.CustomAssetModificationProcessor.Pause();

                DaeExporter exporter = new DaeExporter()
                {
                    settings_activeOnly = toolExport_ActiveOnly,
                    settings_exportMesh = toolExport_Mesh,
                    settings_iKOnFeet = toolExport_BakeFootIK,
                    settings_animationRigging = toolExport_BakeAnimationRigging,
                    settings_animationType = isHuman ? ModelImporterAnimationType.Human : (vaw.animator != null ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.Legacy),
                    settings_avatar = vaw.animator != null ? vaw.animator.avatar : null,
                    settings_motionNodePath = rootMotionBoneIndex >= 0 ? bonePaths[rootMotionBoneIndex] : null,
                };
                exporter.Export(path, transforms, clips);
            }
            finally
            {
                VeryAnimationWindow.CustomAssetModificationProcessor.Resume();
            }
            SetUpdateSampleAnimation();
            uAw.ForceRefresh();
        }
    }
}
