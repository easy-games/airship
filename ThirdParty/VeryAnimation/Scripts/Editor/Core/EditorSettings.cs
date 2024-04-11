#if !UNITY_2019_1_OR_NEWER
#define VERYANIMATION_TIMELINE
#endif

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace VeryAnimation
{
    public class EditorSettings
    {
        private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
        private VeryAnimation va { get { return VeryAnimation.instance; } }

        public Language.LanguageType settingLanguageType { get; private set; }
        public bool settingComponentSaveSettings { get; private set; }
        public float settingBoneButtonSize { get; private set; }
        public Color settingBoneNormalColor { get; private set; }
        public Color settingBoneActiveColor { get; private set; }
        public bool settingBoneMuscleLimit { get; private set; }
        public enum SkeletonType
        {
            None,
            Line,
            Lines,
            Mesh,
        }
        private static readonly string[] SkeletonTypeString =
        {
            SkeletonType.None.ToString(),
            SkeletonType.Line.ToString(),
            SkeletonType.Lines.ToString(),
            SkeletonType.Mesh.ToString(),
        };

        public SkeletonType settingsSkeletonFKType { get; private set; }
        public Color settingSkeletonFKColor { get; private set; }
        public SkeletonType settingsSkeletonIKType { get; private set; }
        public Color settingSkeletonIKColor { get; private set; }
        public Color settingRootMotionColor { get; private set; }
        public float settingIKTargetSize { get; private set; }
        public Color settingIKTargetNormalColor { get; private set; }
        public Color settingIKTargetActiveColor { get; private set; }
        public enum EditorWindowStyle
        {
            Floating,
            Docking,
        }
        private static readonly string[] EditorWindowStyleString =
        {
            EditorWindowStyle.Floating.ToString(),
            EditorWindowStyle.Docking.ToString(),
        };
        public EditorWindowStyle settingEditorWindowStyle { get; private set; }
        public float settingEditorNameFieldWidth { get; private set; }
        public bool settingHierarchyExpandSelectObject { get; private set; }
        public enum PropertyStyle
        {
            Default,
            Filter,
        }
        private GUIContent[] PropertyStyleString = null;
        public PropertyStyle settingPropertyStyle { get; private set; }
        public bool settingAutorunFrameAll { get; private set; }
        public bool settingGenericMirrorScale { get; private set; }
        public bool settingGenericMirrorName { get; private set; }
        public string settingGenericMirrorNameDifferentCharacters { get; private set; }
        public bool settingGenericMirrorNameIgnoreCharacter { get; private set; }
        public string settingGenericMirrorNameIgnoreCharacterString { get; private set; }
        public bool settingBlendShapeMirrorName { get; private set; }
        public string settingBlendShapeMirrorNameDifferentCharacters { get; private set; }
        public bool settingExtraSynchronizeAnimation { get; private set; }
        public bool settingExtraOnionSkin { get; private set; }
        public enum OnionSkinMode
        {
            Keyframes,
            Frames,
        }
        private static readonly string[] OnionSkinModeStrings =
        {
            OnionSkinMode.Keyframes.ToString(),
            OnionSkinMode.Frames.ToString(),
        };
        public OnionSkinMode settingExtraOnionSkinMode { get; private set; }
        public int settingExtraOnionSkinFrameIncrement { get; private set; }
        public int settingExtraOnionSkinNextCount { get; private set; }
        public Color settingExtraOnionSkinNextColor { get; private set; }
        private static readonly Color DefaultOnionSkinNextColor = new Color(0.6039216f, 0.9529412f, 0.282353f, 0.5f);
        public float settingExtraOnionSkinNextMinAlpha { get; private set; }
        private const float DefaultOnionSkinNextMinAlpha = 0.15f;
        public int settingExtraOnionSkinPrevCount { get; private set; }
        public Color settingExtraOnionSkinPrevColor { get; private set; }
        private static readonly Color DefaultOnionSkinPrevColor = new Color(0.8588235f, 0.2431373f, 0.1137255f, 0.5f);
        public float settingExtraOnionSkinPrevMinAlpha { get; private set; }
        private const float DefaultOnionSkinPrevMinAlpha = 0.15f;
        public bool settingExtraRootTrail { get; private set; }
        public Color settingExtraRootTrailColor { get; private set; }
        private static readonly Color DefaultRootTrailColor = new Color(1f, 0.5f, 0.5f, 0.5f);

        private bool componentFoldout;
        private bool gizmosFoldout;
        private bool gizmosBoneFoldout;
        private bool gizmosSkeletonFoldout;
        private bool gizmosIkFoldout;
        private bool editorWindowFoldout;
        private bool controlWindowFoldout;
        private bool controlWindowHierarchyFoldout;
        private bool animationWindowFoldout;
        private bool mirrorFoldout;
        private bool mirrorAutomapFoldout;
        private bool extraFoldout;
        private bool extraOnionSkinningFoldout;
        private bool extraRootTrailFoldout;

        #region RestartOnly
        private EditorWindowStyle settingEditorWindowStyleBefore;
        #endregion

        public EditorSettings()
        {
            settingLanguageType = (Language.LanguageType)EditorPrefs.GetInt("VeryAnimation_LanguageType", 0);
            settingComponentSaveSettings = EditorPrefs.GetBool("VeryAnimation_ComponentSaveSettings", true);
            settingBoneButtonSize = EditorPrefs.GetFloat("VeryAnimation_BoneButtonSize", 16f);
            settingBoneNormalColor = GetEditorPrefsColor("VeryAnimation_BoneNormalColor", Color.white);
            settingBoneActiveColor = GetEditorPrefsColor("VeryAnimation_BoneActiveColor", Color.yellow);
            settingBoneMuscleLimit = EditorPrefs.GetBool("VeryAnimation_BoneMuscleLimit", true);
            settingsSkeletonFKType = (SkeletonType)EditorPrefs.GetInt("VeryAnimation_SkeletonType", (int)SkeletonType.Lines);
            settingSkeletonFKColor = GetEditorPrefsColor("VeryAnimation_SkeletonColor", Color.green);
            settingsSkeletonIKType = (SkeletonType)EditorPrefs.GetInt("VeryAnimation_SkeletonIKType", (int)SkeletonType.Line);
            settingSkeletonIKColor = GetEditorPrefsColor("VeryAnimation_SkeletonIKColor", Color.magenta);
            settingRootMotionColor = GetEditorPrefsColor("VeryAnimation_RootMotionColor", Color.cyan);
            settingIKTargetSize = EditorPrefs.GetFloat("VeryAnimation_IKTargetSize", 0.15f);
            settingIKTargetNormalColor = GetEditorPrefsColor("VeryAnimation_IKTargetNormalColor", new Color(1f, 1f, 1f, 0.5f));
            settingIKTargetActiveColor = GetEditorPrefsColor("VeryAnimation_IKTargetActiveColor", new Color(1f, 0.92f, 0.016f, 0.5f));
            settingEditorWindowStyle = (EditorWindowStyle)EditorPrefs.GetInt("VeryAnimation_EditorWindowStyle", (int)EditorWindowStyle.Docking);
            settingEditorNameFieldWidth = EditorPrefs.GetFloat("VeryAnimation_EditorNameFieldWidth", 180f);
            settingHierarchyExpandSelectObject = EditorPrefs.GetBool("VeryAnimation_HierarchyExpandSelectObject", true);
            settingPropertyStyle = (PropertyStyle)EditorPrefs.GetInt("VeryAnimation_PropertyStyle", 1);
            settingAutorunFrameAll = EditorPrefs.GetBool("VeryAnimation_AutorunFrameAll", true);            
            settingGenericMirrorScale = EditorPrefs.GetBool("VeryAnimation_GenericMirrorScale", false);
            settingGenericMirrorName = EditorPrefs.GetBool("VeryAnimation_GenericMirrorName", true);
            settingGenericMirrorNameDifferentCharacters = EditorPrefs.GetString("VeryAnimation_GenericMirrorNameDifferentCharacters", "Left,Right,Hidari,Migi,L,R");
            settingGenericMirrorNameIgnoreCharacter = EditorPrefs.GetBool("VeryAnimation_GenericMirrorNameIgnoreCharacter", false);
            settingGenericMirrorNameIgnoreCharacterString = EditorPrefs.GetString("VeryAnimation_GenericMirrorNameIgnoreCharacterString", ".");
            settingBlendShapeMirrorName = EditorPrefs.GetBool("VeryAnimation_BlendShapeMirrorName", true);
            settingBlendShapeMirrorNameDifferentCharacters = EditorPrefs.GetString("VeryAnimation_BlendShapeMirrorNameDifferentCharacters", "Left,Right,Hidari,Migi,L,R");
            settingExtraSynchronizeAnimation = EditorPrefs.GetBool("VeryAnimation_ExtraSynchronizeAnimation", false);
            settingExtraOnionSkin = EditorPrefs.GetBool("VeryAnimation_ExtraOnionSkin", false);
            settingExtraOnionSkinMode = (OnionSkinMode)EditorPrefs.GetInt("VeryAnimation_ExtraOnionSkinMode", 0);
            settingExtraOnionSkinFrameIncrement = EditorPrefs.GetInt("VeryAnimation_ExtraOnionSkinFrameIncrement", 1);
            settingExtraOnionSkinNextCount = EditorPrefs.GetInt("VeryAnimation_ExtraOnionSkinNextCount", 2);
            settingExtraOnionSkinNextColor = GetEditorPrefsColor("VeryAnimation_ExtraOnionSkinNextColor", DefaultOnionSkinNextColor);
            settingExtraOnionSkinNextMinAlpha = EditorPrefs.GetFloat("VeryAnimation_ExtraOnionSkinNextMinAlpha", DefaultOnionSkinNextMinAlpha);
            settingExtraOnionSkinPrevCount = EditorPrefs.GetInt("VeryAnimation_ExtraOnionSkinPrevCount", 2);
            settingExtraOnionSkinPrevColor = GetEditorPrefsColor("VeryAnimation_ExtraOnionSkinPrevColor", DefaultOnionSkinPrevColor);
            settingExtraOnionSkinPrevMinAlpha = EditorPrefs.GetFloat("VeryAnimation_ExtraOnionSkinPrevMinAlpha", DefaultOnionSkinPrevMinAlpha);
            settingExtraRootTrail = EditorPrefs.GetBool("VeryAnimation_ExtraRootTrail", false);
            settingExtraRootTrailColor = GetEditorPrefsColor("VeryAnimation_ExtraRootTrailColor", DefaultRootTrailColor);

            if (settingPropertyStyle > PropertyStyle.Filter)
                settingPropertyStyle = PropertyStyle.Filter;

            Language.SetLanguage(settingLanguageType);
        }
        public void Reset()
        {
            EditorPrefs.SetInt("VeryAnimation_LanguageType", (int)(settingLanguageType = (Language.LanguageType)0));
            EditorPrefs.SetBool("VeryAnimation_ComponentSaveSettings", settingComponentSaveSettings = true);
            EditorPrefs.SetFloat("VeryAnimation_BoneButtonSize", settingBoneButtonSize = 16f);
            SetEditorPrefsColor("VeryAnimation_BoneNormalColor", settingBoneNormalColor = Color.white);
            SetEditorPrefsColor("VeryAnimation_BoneActiveColor", settingBoneActiveColor = Color.yellow);
            EditorPrefs.SetBool("VeryAnimation_BoneMuscleLimit", settingBoneMuscleLimit = true);
            EditorPrefs.SetInt("VeryAnimation_SkeletonType", (int)(settingsSkeletonFKType = SkeletonType.Lines));
            SetEditorPrefsColor("VeryAnimation_SkeletonColor", settingSkeletonFKColor = Color.green);
            EditorPrefs.SetInt("VeryAnimation_SkeletonIKType", (int)(settingsSkeletonIKType = SkeletonType.Line));
            SetEditorPrefsColor("VeryAnimation_SkeletonIKColor", settingSkeletonIKColor = Color.magenta);
            SetEditorPrefsColor("VeryAnimation_RootMotionColor", settingRootMotionColor = Color.cyan);
            EditorPrefs.SetFloat("VeryAnimation_IKTargetSize", settingIKTargetSize = 0.15f);
            SetEditorPrefsColor("VeryAnimation_IKTargetNormalColor", settingIKTargetNormalColor = new Color(1f, 1f, 1f, 0.5f));
            SetEditorPrefsColor("VeryAnimation_IKTargetActiveColor", settingIKTargetActiveColor = new Color(1f, 0.92f, 0.016f, 0.5f));
            EditorPrefs.SetInt("VeryAnimation_EditorWindowStyle", (int)(settingEditorWindowStyle = EditorWindowStyle.Docking));
            EditorPrefs.SetFloat("VeryAnimation_EditorNameFieldWidth", settingEditorNameFieldWidth = 180f);
            EditorPrefs.SetBool("VeryAnimation_HierarchyExpandSelectObject", settingHierarchyExpandSelectObject = true);
            EditorPrefs.SetInt("VeryAnimation_PropertyStyle", (int)(settingPropertyStyle = (PropertyStyle)1));
            EditorPrefs.SetBool("VeryAnimation_AutorunFrameAll", settingAutorunFrameAll = true);
            EditorPrefs.SetBool("VeryAnimation_GenericMirrorScale", settingGenericMirrorScale = false);
            EditorPrefs.SetBool("VeryAnimation_GenericMirrorName", settingGenericMirrorName = true);
            EditorPrefs.SetString("VeryAnimation_GenericMirrorNameDifferentCharacters", settingGenericMirrorNameDifferentCharacters = "Left,Right,Hidari,Migi,L,R");
            EditorPrefs.SetBool("VeryAnimation_GenericMirrorNameIgnoreCharacter", settingGenericMirrorNameIgnoreCharacter = false);
            EditorPrefs.SetString("VeryAnimation_GenericMirrorNameIgnoreCharacterString", settingGenericMirrorNameIgnoreCharacterString = ".");
            EditorPrefs.SetBool("VeryAnimation_BlendShapeMirrorName", settingBlendShapeMirrorName = true);
            EditorPrefs.SetString("VeryAnimation_BlendShapeMirrorNameDifferentCharacters", settingBlendShapeMirrorNameDifferentCharacters = "Left,Right,Hidari,Migi,L,R");
            EditorPrefs.SetBool("VeryAnimation_ExtraSynchronizeAnimation", settingExtraSynchronizeAnimation = false);
            EditorPrefs.SetBool("VeryAnimation_ExtraOnionSkin", settingExtraOnionSkin = false);
            EditorPrefs.SetInt("VeryAnimation_ExtraOnionSkinMode", (int)(settingExtraOnionSkinMode = (OnionSkinMode)0));
            EditorPrefs.SetInt("VeryAnimation_ExtraOnionSkinFrameIncrement", settingExtraOnionSkinFrameIncrement = 1);
            EditorPrefs.SetInt("VeryAnimation_ExtraOnionSkinNextCount", settingExtraOnionSkinNextCount = 2);
            SetEditorPrefsColor("VeryAnimation_ExtraOnionSkinNextColor", settingExtraOnionSkinNextColor = DefaultOnionSkinNextColor);
            EditorPrefs.SetFloat("VeryAnimation_ExtraOnionSkinNextMinAlpha", settingExtraOnionSkinNextMinAlpha = DefaultOnionSkinNextMinAlpha);
            EditorPrefs.SetInt("VeryAnimation_ExtraOnionSkinPrevCount", settingExtraOnionSkinPrevCount = 2);
            SetEditorPrefsColor("VeryAnimation_ExtraOnionSkinPrevColor", settingExtraOnionSkinPrevColor = DefaultOnionSkinPrevColor);
            EditorPrefs.SetFloat("VeryAnimation_ExtraOnionSkinPrevMinAlpha", settingExtraOnionSkinPrevMinAlpha = DefaultOnionSkinPrevMinAlpha);
            EditorPrefs.SetBool("VeryAnimation_ExtraRootTrail", settingExtraRootTrail = false);
            SetEditorPrefsColor("VeryAnimation_ExtraRootTrailColor", settingExtraRootTrailColor = DefaultRootTrailColor);

            Language.SetLanguage(settingLanguageType);
            va.SetUpdateSampleAnimation();
            va.onionSkin.Update();
            va.SetSynchronizeAnimation(settingExtraSynchronizeAnimation);
            va.SetAnimationWindowSynchroSelection();
            va.SetAnimationWindowRefresh(VeryAnimation.AnimationWindowStateRefreshType.Everything);
            InternalEditorUtility.RepaintAllViews();
        }

        public void Initialize()
        {
            Release();

            #region RestartOnly
            settingEditorWindowStyleBefore = settingEditorWindowStyle;
            #endregion

            UpdateGUIContentStrings();

            Language.OnLanguageChanged += UpdateGUIContentStrings;
        }
        public void Release()
        {
            Language.OnLanguageChanged -= UpdateGUIContentStrings;
        }

        private void UpdateGUIContentStrings()
        {
            PropertyStyleString = new GUIContent[]
            {
                Language.GetContent(Language.Help.SettingsPropertyStyle_Default),
                Language.GetContent(Language.Help.SettingsPropertyStyle_Filter),
            };
        }

        public void SettingsGUI()
        {
            EditorGUILayout.BeginVertical(vaw.guiStyleSkinBox);
            {
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Language");
                    EditorGUI.BeginChangeCheck();
                    settingLanguageType = (Language.LanguageType)GUILayout.Toolbar((int)settingLanguageType, Language.LanguageTypeString, EditorStyles.miniButton);
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorPrefs.SetInt("VeryAnimation_LanguageType", (int)(settingLanguageType));
                        Language.SetLanguage(settingLanguageType);
                        InternalEditorUtility.RepaintAllViews();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                componentFoldout = EditorGUILayout.Foldout(componentFoldout, "Component", true);
                if (componentFoldout)
                {
                    EditorGUI.indentLevel++;
                    {
                        #region settingComponentSaveSettings
                        {
                            EditorGUI.BeginChangeCheck();
                            settingComponentSaveSettings = EditorGUILayout.Toggle(Language.GetContent(Language.Help.SettingsSaveSettings), settingComponentSaveSettings);
                            if (EditorGUI.EndChangeCheck())
                            {
                                EditorPrefs.SetBool("VeryAnimation_ComponentSaveSettings", settingComponentSaveSettings);
                            }
                        }
                        #endregion
                    }
                    EditorGUI.indentLevel--;
                }
                gizmosFoldout = EditorGUILayout.Foldout(gizmosFoldout, "Gizmos", true);
                if (gizmosFoldout)
                {
                    EditorGUI.indentLevel++;
                    {
                        gizmosBoneFoldout = EditorGUILayout.Foldout(gizmosBoneFoldout, "Bone", true);
                        if (gizmosBoneFoldout)
                        {
                            EditorGUI.indentLevel++;
                            {
                                #region Button Size
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingBoneButtonSize = EditorGUILayout.Slider("Button Size", settingBoneButtonSize, 1f, 32f);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        EditorPrefs.SetFloat("VeryAnimation_BoneButtonSize", settingBoneButtonSize);
                                        InternalEditorUtility.RepaintAllViews();
                                    }
                                }
                                #endregion
                                #region Button Normal Color
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingBoneNormalColor = EditorGUILayout.ColorField("Button Normal Color", settingBoneNormalColor);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        SetEditorPrefsColor("VeryAnimation_BoneNormalColor", settingBoneNormalColor);
                                        InternalEditorUtility.RepaintAllViews();
                                    }
                                }
                                #endregion
                                #region Button Active Color
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingBoneActiveColor = EditorGUILayout.ColorField("Button Active Color", settingBoneActiveColor);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        SetEditorPrefsColor("VeryAnimation_BoneActiveColor", settingBoneActiveColor);
                                        InternalEditorUtility.RepaintAllViews();
                                    }
                                }
                                #endregion
                                #region MuscleLimit
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingBoneMuscleLimit = EditorGUILayout.Toggle("Muscle Limit Gizmo", settingBoneMuscleLimit);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        EditorPrefs.SetBool("VeryAnimation_BoneMuscleLimit", settingBoneMuscleLimit);
                                        InternalEditorUtility.RepaintAllViews();
                                    }
                                }
                                #endregion
                            }
                            EditorGUI.indentLevel--;
                        }
                        gizmosSkeletonFoldout = EditorGUILayout.Foldout(gizmosSkeletonFoldout, "Skeleton", true);
                        if (gizmosSkeletonFoldout)
                        {
                            EditorGUI.indentLevel++;
                            {
                                #region FK
                                EditorGUILayout.LabelField("FK");
                                {
                                    EditorGUI.indentLevel++;
                                    #region SkeletonType
                                    {
                                        EditorGUILayout.BeginHorizontal();
                                        EditorGUI.BeginChangeCheck();
                                        EditorGUILayout.PrefixLabel("Preview Type");
                                        settingsSkeletonFKType = (SkeletonType)GUILayout.Toolbar((int)settingsSkeletonFKType, SkeletonTypeString, EditorStyles.miniButton);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            EditorPrefs.SetInt("VeryAnimation_SkeletonType", (int)settingsSkeletonFKType);
                                            InternalEditorUtility.RepaintAllViews();
                                        }
                                        EditorGUILayout.EndHorizontal();
                                    }
                                    #endregion
                                    #region Skeleton Color
                                    {
                                        EditorGUI.BeginChangeCheck();
                                        settingSkeletonFKColor = EditorGUILayout.ColorField("Preview Color", settingSkeletonFKColor);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            SetEditorPrefsColor("VeryAnimation_SkeletonColor", settingSkeletonFKColor);
                                            InternalEditorUtility.RepaintAllViews();
                                        }
                                    }
                                    #endregion
                                    EditorGUI.indentLevel--;
                                }
                                #endregion
                                #region IK
                                EditorGUILayout.LabelField(new GUIContent("IK", "Foot IK and Animation Rigging"));
                                {
                                    EditorGUI.indentLevel++;
                                    #region SkeletonType
                                    {
                                        EditorGUILayout.BeginHorizontal();
                                        EditorGUI.BeginChangeCheck();
                                        EditorGUILayout.PrefixLabel("Preview Type");
                                        settingsSkeletonIKType = (SkeletonType)GUILayout.Toolbar((int)settingsSkeletonIKType, SkeletonTypeString, EditorStyles.miniButton);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            EditorPrefs.SetInt("VeryAnimation_SkeletonIKType", (int)settingsSkeletonIKType);
                                            InternalEditorUtility.RepaintAllViews();
                                        }
                                        EditorGUILayout.EndHorizontal();
                                    }
                                    #endregion
                                    #region Skeleton Color
                                    {
                                        EditorGUI.BeginChangeCheck();
                                        settingSkeletonIKColor = EditorGUILayout.ColorField("Preview Color", settingSkeletonIKColor);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            SetEditorPrefsColor("VeryAnimation_SkeletonIKColor", settingSkeletonIKColor);
                                            InternalEditorUtility.RepaintAllViews();
                                        }
                                    }
                                    #endregion
                                    EditorGUI.indentLevel--;
                                }
                                #endregion
                                #region RootMotion Color
                                EditorGUILayout.LabelField("Root Motion");
                                {
                                    EditorGUI.indentLevel++;
                                    {
                                        EditorGUI.BeginChangeCheck();
                                        settingRootMotionColor = EditorGUILayout.ColorField("Preview Color", settingRootMotionColor);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            SetEditorPrefsColor("VeryAnimation_RootMotionColor", settingRootMotionColor);
                                            InternalEditorUtility.RepaintAllViews();
                                        }
                                    }
                                    EditorGUI.indentLevel--;
                                }
                                #endregion
                            }
                            EditorGUI.indentLevel--;
                        }
                        gizmosIkFoldout = EditorGUILayout.Foldout(gizmosIkFoldout, "IK", true);
                        if (gizmosIkFoldout)
                        {
                            EditorGUI.indentLevel++;
                            {
                                #region IK Target Size
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingIKTargetSize = EditorGUILayout.Slider("Button Size", settingIKTargetSize, 0.01f, 1f);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        EditorPrefs.SetFloat("VeryAnimation_IKTargetSize", settingIKTargetSize);
                                        InternalEditorUtility.RepaintAllViews();
                                    }
                                }
                                #endregion
                                #region IK Target Normal Color
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingIKTargetNormalColor = EditorGUILayout.ColorField("Button Normal Color", settingIKTargetNormalColor);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        SetEditorPrefsColor("VeryAnimation_IKTargetNormalColor", settingIKTargetNormalColor);
                                        InternalEditorUtility.RepaintAllViews();
                                    }
                                }
                                #endregion
                                #region IK Target Active Color
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingIKTargetActiveColor = EditorGUILayout.ColorField("Button Active Color", settingIKTargetActiveColor);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        SetEditorPrefsColor("VeryAnimation_IKTargetActiveColor", settingIKTargetActiveColor);
                                        InternalEditorUtility.RepaintAllViews();
                                    }
                                }
                                #endregion
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                editorWindowFoldout = EditorGUILayout.Foldout(editorWindowFoldout, "Editor Window", true);
                if (editorWindowFoldout)
                {
                    EditorGUI.indentLevel++;
                    {
                        #region Window Style
                        EditorGUILayout.BeginHorizontal();
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PrefixLabel("Window Style");
                        settingEditorWindowStyle = (EditorWindowStyle)GUILayout.Toolbar((int)settingEditorWindowStyle, EditorWindowStyleString, EditorStyles.miniButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorPrefs.SetInt("VeryAnimation_EditorWindowStyle", (int)settingEditorWindowStyle);
                        }
                        EditorGUILayout.EndHorizontal();
                        #endregion
                    }
                    {
                        #region NameFieldWidth
                        {
                            EditorGUI.BeginChangeCheck();
                            settingEditorNameFieldWidth = EditorGUILayout.Slider("Name Field Width", settingEditorNameFieldWidth, 50f, 500f);
                            if (EditorGUI.EndChangeCheck())
                            {
                                EditorPrefs.SetFloat("VeryAnimation_EditorNameFieldWidth", settingEditorNameFieldWidth);
                                InternalEditorUtility.RepaintAllViews();
                            }
                        }
                        #endregion
                    }
                    EditorGUI.indentLevel--;
                }
                controlWindowFoldout = EditorGUILayout.Foldout(controlWindowFoldout, "Control Window", true);
                if (controlWindowFoldout)
                {
                    EditorGUI.indentLevel++;
                    {
                        controlWindowHierarchyFoldout = EditorGUILayout.Foldout(controlWindowHierarchyFoldout, "Hierarchy", true);
                        if (controlWindowHierarchyFoldout)
                        {
                            EditorGUI.indentLevel++;
                            {
                                #region ExpandSelectObject
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingHierarchyExpandSelectObject = EditorGUILayout.Toggle(new GUIContent("Expand select object"), settingHierarchyExpandSelectObject);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        EditorPrefs.SetBool("VeryAnimation_HierarchyExpandSelectObject", settingHierarchyExpandSelectObject);
                                    }
                                }
                                #endregion
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                animationWindowFoldout = EditorGUILayout.Foldout(animationWindowFoldout, "Animation Window", true);
                if (animationWindowFoldout)
                {
                    EditorGUI.indentLevel++;
                    {
                        #region Property Style
                        EditorGUILayout.BeginHorizontal();
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PrefixLabel("Property Style");
                        settingPropertyStyle = (PropertyStyle)GUILayout.Toolbar((int)settingPropertyStyle, PropertyStyleString, EditorStyles.miniButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorPrefs.SetInt("VeryAnimation_PropertyStyle", (int)settingPropertyStyle);
                            va.SetAnimationWindowSynchroSelection();
                        }
                        EditorGUILayout.EndHorizontal();
                        #endregion
                    }
                    {
                        #region AutorunFrameAll
                        EditorGUI.BeginChangeCheck();
                        settingAutorunFrameAll = EditorGUILayout.Toggle(Language.GetContent(Language.Help.SettingsAutorunFrameAll), settingAutorunFrameAll);
                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorPrefs.SetBool("VeryAnimation_AutorunFrameAll", settingAutorunFrameAll);
                        }
                        #endregion
                    }
                    EditorGUI.indentLevel--;
                }
                mirrorFoldout = EditorGUILayout.Foldout(mirrorFoldout, "Mirror", true);
                if (mirrorFoldout)
                {
                    EditorGUI.indentLevel++;
                    {
                        EditorGUI.BeginChangeCheck();
                        settingGenericMirrorScale = EditorGUILayout.Toggle(Language.GetContent(Language.Help.SettingsMirrorScale), settingGenericMirrorScale);
                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorPrefs.SetBool("VeryAnimation_GenericMirrorScale", settingGenericMirrorScale);
                        }
                    }

                    mirrorAutomapFoldout = EditorGUILayout.Foldout(mirrorAutomapFoldout, "Automap", true);
                    if (mirrorAutomapFoldout)
                    {
                        EditorGUI.indentLevel++;
                        {
                            EditorGUILayout.LabelField("Generic");
                            EditorGUI.indentLevel++;
                            {
                                #region settingGenericMirrorName
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingGenericMirrorName = EditorGUILayout.Toggle(Language.GetContent(Language.Help.SettingsSearchByName), settingGenericMirrorName);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        EditorPrefs.SetBool("VeryAnimation_GenericMirrorName", settingGenericMirrorName);
                                    }
                                    if (settingGenericMirrorName)
                                    {
                                        EditorGUI.indentLevel++;
                                        #region settingGenericMirrorNameDifferentCharacters
                                        {
                                            EditorGUI.BeginChangeCheck();
                                            settingGenericMirrorNameDifferentCharacters = EditorGUILayout.TextField(new GUIContent("Characters", "Different Characters"), settingGenericMirrorNameDifferentCharacters);
                                            if (EditorGUI.EndChangeCheck())
                                            {
                                                EditorPrefs.SetString("VeryAnimation_GenericMirrorNameDifferentCharacters", settingGenericMirrorNameDifferentCharacters);
                                            }
                                        }
                                        #endregion
                                        #region settingGenericMirrorNameIgnoreCharacter
                                        {
                                            EditorGUILayout.BeginHorizontal();
                                            {
                                                EditorGUI.BeginChangeCheck();
                                                settingGenericMirrorNameIgnoreCharacter = EditorGUILayout.ToggleLeft(Language.GetContent(Language.Help.SettingsIgnoreUpToTheSpecifiedCharacter), settingGenericMirrorNameIgnoreCharacter);
                                                if (EditorGUI.EndChangeCheck())
                                                {
                                                    EditorPrefs.SetBool("VeryAnimation_GenericMirrorNameIgnoreCharacter", settingGenericMirrorNameIgnoreCharacter);
                                                }
                                            }
                                            if (settingGenericMirrorNameIgnoreCharacter)
                                            {
                                                EditorGUI.BeginChangeCheck();
                                                settingGenericMirrorNameIgnoreCharacterString = EditorGUILayout.TextField(settingGenericMirrorNameIgnoreCharacterString, GUILayout.Width(100));
                                                if (EditorGUI.EndChangeCheck())
                                                {
                                                    EditorPrefs.SetString("VeryAnimation_GenericMirrorNameIgnoreCharacterString", settingGenericMirrorNameIgnoreCharacterString);
                                                }
                                            }
                                            EditorGUILayout.EndHorizontal();
                                        }
                                        #endregion
                                        EditorGUI.indentLevel--;
                                    }
                                }
                                #endregion
                            }
                            EditorGUI.indentLevel--;

                            EditorGUILayout.LabelField("Blend Shape");
                            EditorGUI.indentLevel++;
                            {
                                #region settingBlendShapeMirrorName
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingBlendShapeMirrorName = EditorGUILayout.Toggle(Language.GetContent(Language.Help.SettingsSearchByName), settingBlendShapeMirrorName);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        EditorPrefs.SetBool("VeryAnimation_BlendShapeMirrorName", settingBlendShapeMirrorName);
                                    }
                                    if (settingBlendShapeMirrorName)
                                    {
                                        EditorGUI.indentLevel++;
                                        #region settingBlendShapeMirrorNameDifferentCharacters
                                        {
                                            EditorGUI.BeginChangeCheck();
                                            settingBlendShapeMirrorNameDifferentCharacters = EditorGUILayout.TextField(new GUIContent("Characters", "Different Characters"), settingBlendShapeMirrorNameDifferentCharacters);
                                            if (EditorGUI.EndChangeCheck())
                                            {
                                                EditorPrefs.SetString("VeryAnimation_BlendShapeMirrorNameDifferentCharacters", settingBlendShapeMirrorNameDifferentCharacters);
                                            }
                                        }
                                        #endregion
                                        EditorGUI.indentLevel--;
                                    }
                                }
                                #endregion
                            }
                            EditorGUI.indentLevel--;
                        }
                        EditorGUI.indentLevel--;
                    }

                    EditorGUI.indentLevel--;
                }
                extraFoldout = EditorGUILayout.Foldout(extraFoldout, "Extra functions", true);
                if (extraFoldout)
                {
                    EditorGUI.indentLevel++;
                    {
                        #region SynchronizeAnimation
                        {
                            var enable = !EditorApplication.isPlaying && !va.uAw.GetLinkedWithTimeline();
                            EditorGUI.BeginDisabledGroup(!enable);
                            EditorGUI.BeginChangeCheck();
                            settingExtraSynchronizeAnimation = EditorGUILayout.ToggleLeft(Language.GetContent(Language.Help.SettingsSynchronizeAnimation), settingExtraSynchronizeAnimation);
                            if (EditorGUI.EndChangeCheck())
                            {
                                EditorPrefs.SetBool("VeryAnimation_ExtraSynchronizeAnimation", settingExtraSynchronizeAnimation);
                                va.SetSynchronizeAnimation(settingExtraSynchronizeAnimation);
                            }
                            EditorGUI.EndDisabledGroup();
                        }
                        #endregion
                        #region OnionSkin
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingExtraOnionSkin = EditorGUILayout.ToggleLeft("", settingExtraOnionSkin, GUILayout.Width(28f));
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        EditorPrefs.SetBool("VeryAnimation_ExtraOnionSkin", settingExtraOnionSkin);
                                        va.onionSkin.Update();
                                    }
                                }
                                {
                                    var saveLevel = EditorGUI.indentLevel;
                                    EditorGUI.indentLevel = 0;
                                    extraOnionSkinningFoldout = EditorGUILayout.Foldout(extraOnionSkinningFoldout, Language.GetContent(Language.Help.SettingsOnionSkin), true);
                                    EditorGUI.indentLevel = saveLevel;
                                    if (!settingExtraOnionSkin)
                                        extraOnionSkinningFoldout = false;
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                            if (settingExtraOnionSkin && extraOnionSkinningFoldout)
                            {
                                EditorGUI.indentLevel++;
                                #region settingExtraOnionSkinMode
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUILayout.PrefixLabel("Mode");
                                    EditorGUI.BeginChangeCheck();
                                    settingExtraOnionSkinMode = (OnionSkinMode)GUILayout.Toolbar((int)settingExtraOnionSkinMode, OnionSkinModeStrings, EditorStyles.miniButton);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        EditorPrefs.SetInt("VeryAnimation_ExtraOnionSkinMode", (int)(settingExtraOnionSkinMode));
                                        va.onionSkin.Update();
                                    }
                                    EditorGUILayout.EndHorizontal();

                                    EditorGUI.indentLevel++;
                                    #region settingExtraOnionSkinFrameIncrement
                                    if (settingExtraOnionSkinMode == OnionSkinMode.Frames)
                                    {
                                        EditorGUI.BeginChangeCheck();
                                        settingExtraOnionSkinFrameIncrement = EditorGUILayout.IntSlider("Frame Increment", settingExtraOnionSkinFrameIncrement, 1, 60);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            EditorPrefs.SetInt("VeryAnimation_ExtraOnionSkinFrameIncrement", settingExtraOnionSkinFrameIncrement);
                                            va.onionSkin.Update();
                                        }
                                    }
                                    #endregion
                                    EditorGUI.indentLevel--;
                                }
                                #endregion
                                #region Next
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingExtraOnionSkinNextCount = EditorGUILayout.IntSlider("Next", settingExtraOnionSkinNextCount, 0, 10);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        EditorPrefs.SetInt("VeryAnimation_ExtraOnionSkinNextCount", settingExtraOnionSkinNextCount);
                                        va.onionSkin.Update();
                                    }
                                    EditorGUI.indentLevel++;
                                    {
                                        EditorGUILayout.BeginHorizontal();
                                        EditorGUILayout.PrefixLabel(new GUIContent("Color", "Near Color + Far Alpha"));
                                        {
                                            EditorGUI.BeginChangeCheck();
                                            settingExtraOnionSkinNextColor = EditorGUILayout.ColorField(settingExtraOnionSkinNextColor, GUILayout.Width(80f));
                                            if (EditorGUI.EndChangeCheck())
                                            {
                                                SetEditorPrefsColor("VeryAnimation_ExtraOnionSkinNextColor", settingExtraOnionSkinNextColor);
                                                va.onionSkin.Update();
                                            }
                                        }
                                        {
                                            EditorGUI.BeginChangeCheck();
                                            settingExtraOnionSkinNextMinAlpha = EditorGUILayout.Slider(settingExtraOnionSkinNextMinAlpha, 0f, 1f);
                                            if (EditorGUI.EndChangeCheck())
                                            {
                                                EditorPrefs.SetFloat("VeryAnimation_ExtraOnionSkinNextMinAlpha", settingExtraOnionSkinNextMinAlpha);
                                                va.onionSkin.Update();
                                            }
                                        }
                                        EditorGUILayout.EndHorizontal();
                                    }
                                    EditorGUI.indentLevel--;
                                }
                                #endregion
                                #region Prev
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingExtraOnionSkinPrevCount = EditorGUILayout.IntSlider("Previous", settingExtraOnionSkinPrevCount, 0, 10);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        EditorPrefs.SetInt("VeryAnimation_ExtraOnionSkinPrevCount", settingExtraOnionSkinPrevCount);
                                        va.onionSkin.Update();
                                    }
                                    EditorGUI.indentLevel++;
                                    {
                                        EditorGUILayout.BeginHorizontal();
                                        EditorGUILayout.PrefixLabel(new GUIContent("Color", "Near Color + Far Alpha"));
                                        {
                                            EditorGUI.BeginChangeCheck();
                                            settingExtraOnionSkinPrevColor = EditorGUILayout.ColorField(settingExtraOnionSkinPrevColor, GUILayout.Width(80f));
                                            if (EditorGUI.EndChangeCheck())
                                            {
                                                SetEditorPrefsColor("VeryAnimation_ExtraOnionSkinPrevColor", settingExtraOnionSkinPrevColor);
                                                va.onionSkin.Update();
                                            }
                                        }
                                        {
                                            EditorGUI.BeginChangeCheck();
                                            settingExtraOnionSkinPrevMinAlpha = EditorGUILayout.Slider(settingExtraOnionSkinPrevMinAlpha, 0f, 1f);
                                            if (EditorGUI.EndChangeCheck())
                                            {
                                                EditorPrefs.SetFloat("VeryAnimation_ExtraOnionSkinPrevMinAlpha", settingExtraOnionSkinPrevMinAlpha);
                                                va.onionSkin.Update();
                                            }
                                        }
                                        EditorGUILayout.EndHorizontal();
                                    }
                                    EditorGUI.indentLevel--;
                                }
                                #endregion
                                EditorGUI.indentLevel--;
                            }
                        }
                        #endregion
                        #region RootTrail
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingExtraRootTrail = EditorGUILayout.ToggleLeft("", settingExtraRootTrail, GUILayout.Width(28f));
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        EditorPrefs.SetBool("VeryAnimation_ExtraRootTrail", settingExtraRootTrail);
                                        SceneView.RepaintAll();
                                    }
                                }
                                EditorGUI.BeginDisabledGroup(!va.isHuman);
                                {
                                    var saveLevel = EditorGUI.indentLevel;
                                    EditorGUI.indentLevel = 0;
                                    extraRootTrailFoldout = EditorGUILayout.Foldout(extraRootTrailFoldout, Language.GetContent(Language.Help.SettingsRootTrail), true);
                                    EditorGUI.indentLevel = saveLevel;
                                    if (!settingExtraRootTrail)
                                        extraRootTrailFoldout = false;
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                            if (settingExtraRootTrail && extraRootTrailFoldout && va.isHuman)
                            {
                                EditorGUI.indentLevel++;
                                {
                                    EditorGUI.BeginChangeCheck();
                                    settingExtraRootTrailColor = EditorGUILayout.ColorField("Color", settingExtraRootTrailColor);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        SetEditorPrefsColor("VeryAnimation_ExtraRootTrailColor", settingExtraRootTrailColor);
                                        SceneView.RepaintAll();
                                    }
                                }
                                EditorGUI.indentLevel--;
                            }
                            EditorGUI.EndDisabledGroup();
                        }
                        #endregion
                    }
                    EditorGUI.indentLevel--;
                }

                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Reset"))
                    {
                        Reset();
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(4);
                }

                #region RestartOnly
                if (settingEditorWindowStyleBefore != settingEditorWindowStyle)
                {
                    EditorGUILayout.HelpBox(Language.GetText(Language.Help.SettingsRestartOnly), MessageType.Warning);
                }
                #endregion
            }
            EditorGUILayout.EndVertical();
        }

        public float GetSkeletonTypeLinesRadius(Vector3 position)
        {
            return HandleUtility.GetHandleSize(position) * (settingBoneButtonSize / 200f);
        }

        private Color GetEditorPrefsColor(string name, Color defcolor)
        {
            return new Color(EditorPrefs.GetFloat(name + "_r", defcolor.r),
                            EditorPrefs.GetFloat(name + "_g", defcolor.g),
                            EditorPrefs.GetFloat(name + "_b", defcolor.b),
                            EditorPrefs.GetFloat(name + "_a", defcolor.a));
        }
        private void SetEditorPrefsColor(string name, Color color)
        {
            EditorPrefs.SetFloat(name + "_r", color.r);
            EditorPrefs.SetFloat(name + "_g", color.g);
            EditorPrefs.SetFloat(name + "_b", color.b);
            EditorPrefs.SetFloat(name + "_a", color.a);
        }

        private Vector3 GetEditorPrefsVector3(string name, Vector3 defvec)
        {
            return new Vector3(EditorPrefs.GetFloat(name + "_x", defvec.x),
                            EditorPrefs.GetFloat(name + "_y", defvec.y),
                            EditorPrefs.GetFloat(name + "_z", defvec.z));
        }
        private void SetEditorPrefsVector3(string name, Vector3 vec)
        {
            EditorPrefs.SetFloat(name + "_x", vec.x);
            EditorPrefs.SetFloat(name + "_y", vec.y);
            EditorPrefs.SetFloat(name + "_z", vec.z);
        }
    }
}
