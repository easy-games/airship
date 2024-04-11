//#define Enable_Profiler
//#define Enable_MemoryLeakCheck

#if !UNITY_2019_1_OR_NEWER
#define VERYANIMATION_TIMELINE
#endif

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEditor.IMGUI.Controls;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
#if UNITY_2018_3_OR_NEWER && !UNITY_2021_2_OR_NEWER
using UnityEditor.Experimental.SceneManagement;
#endif

#if VERYANIMATION_TIMELINE
using UnityEngine.Playables;
using UnityEngine.Timeline;
#endif
#if VERYANIMATION_ANIMATIONRIGGING
using UnityEngine.Animations.Rigging;
using UnityEditor.Animations.Rigging;
#endif

namespace VeryAnimation
{
    [Serializable]
    public class VeryAnimationWindow : EditorWindow
    {
        public const string Version = "1.2.19";
        public const int AsmdefVersion = 31;

#if UNITY_2020_1_OR_NEWER
        [MenuItem("Window/Very Animation/Main")]
#else
        [MenuItem("Window/Very Animation/Main %#v")]
#endif
        public static void Open()
        {
            Action ReOpen = () =>
            {
                if (instance != null)
                {
                    try
                    {
                        instance.Close();
                    }
                    catch
                    {
                        EditorWindow.DestroyImmediate(instance, true);
                    }
                }
                EditorApplication.delayCall += () =>
                {
                    Open();
                };
            };

            if (instance == null)
            {
                EditorWindow window = null;
                foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    if (w.GetType().Name == "InspectorWindow")
                    {
                        window = w;
                        break;
                    }
                }
                if (window != null)
                    GetWindow<VeryAnimationWindow>(window.GetType());
                else
                    GetWindow<VeryAnimationWindow>();
            }
            else if (instance.va != null)
            {
                instance.SetGameObject();
                instance.va.UpdateCurrentInfo();
                if (!instance.va.isError && !instance.va.edit)
                {
                    instance.Initialize();
                    if (!instance.uEditorWindow.HasFocus(instance))
                    {
                        ReOpen();
                    }
                }
                else
                {
                    ReOpen();
                }
            }
            else
            {
                ReOpen();
            }
        }

        public static VeryAnimationWindow instance;

        public GameObject gameObject { get; private set; }
        public Animator animator { get; private set; }
        public Animation animation { get; private set; }
        public AnimationClip playingAnimationClip { get; private set; }
        public float playingAnimationTime { get; private set; }
        public float playingAnimationLength { get; private set; }

        #region Core
        [SerializeField]
        private VeryAnimation va;
        private VeryAnimationEditorWindow vae { get { return VeryAnimationEditorWindow.instance; } }

        public EditorSettings editorSettings { get; private set; }
        #endregion

        #region Reflection
        public UEditorWindow uEditorWindow { get; private set; }
        public USceneView uSceneView { get; private set; }
        public UEditorGUIUtility uEditorGUIUtility { get; private set; }
        public USnapSettings uSnapSettings { get; private set; }
        public UDisc uDisc { get; private set; }
        public UMuscleClipEditorUtilities uMuscleClipQualityInfo { get; private set; }
        public UAnimationUtility uAnimationUtility { get; private set; }
        public UEditorGUI uEditorGUI { get; private set; }
        public UHandleUtility uHandleUtility { get; private set; }
#if UNITY_2018_3_OR_NEWER
        public UPrefabStage uPrefabStage { get; private set; }
#endif
        #endregion

        #region Editor
        public bool initialized { get; private set; }
        private int undoGroupID = -1;
        private AnimatorStateSave pauseAnimatorStateSave;
        private int beforeErrorCode;
        private AnimationClip beforeAnimationClip;
        private bool handleTransformUpdate = true;
        private Vector3 handlePosition;
        private Quaternion handleRotation;
        private Vector3 handleScale;
        private Vector3 handlePositionSave;
        private Quaternion handleRotationSave;

        private int[] muscleRotationHandleIds;
        [NonSerialized]
        public int[] muscleRotationSliderIds;

        private EditorCommon.ArrowMesh arrowMesh;
        private RootTrail rootTrail;

        public enum RepaintGUI
        {
            None,
            Edit,
            All,
        }
        private RepaintGUI repaintGUI;
        public void SetRepaintGUI(RepaintGUI type)
        {
            if (repaintGUI < type)
                repaintGUI = type;
        }

        private int beforeSelectedTab;

        private Vector2 errorLogScrollPosition;

        private GameObject forceChangeObject;
        #endregion

        #region ClipSelector
        private class ClipSelectorTreeView : TreeView
        {
            private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
            private VeryAnimation va { get { return VeryAnimation.instance; } }
            private VeryAnimationControlWindow vcw { get { return VeryAnimationControlWindow.instance; } }

            public AnimationClip[] animationClips { get; private set; }

            private UTreeView uTreeView;

            public ClipSelectorTreeView(TreeViewState state) : base(state)
            {
                uTreeView = new UTreeView();

                showBorder = true;
            }

            protected override TreeViewItem BuildRoot()
            {
                animationClips = null;

                var root = new TreeViewItem(int.MinValue, -1, "Root");
                root.children = new List<TreeViewItem>();
                if (vaw.gameObject != null)
                {
                    var clips = AnimationUtility.GetAnimationClips(vaw.gameObject).Distinct().ToList();
                    clips.RemoveAll(x => x == null);
                    clips.Sort((clipA, clipB) => clipA.name.CompareTo(clipB.name));
                    animationClips = clips.ToArray();
                    foreach (var clip in animationClips)
                    {
                        root.children.Add(new TreeViewItem(clip.GetInstanceID(), 0, clip.name));
                    }
                }
                return root;
            }

            protected override void SelectionChanged(IList<int> selectedIds)
            {
                var clip = EditorUtility.InstanceIDToObject(state.lastClickedID) as AnimationClip;
                if (clip != null)
                {
                    va.uAw.SetSelectionAnimationClip(clip);
                    if (va.uAw.GetPlaying())
                        va.SetCurrentTime(0f);
                }
            }

            public void UpdateSelectedIds()
            {
                state.selectedIDs.Clear();
                if (animationClips != null)
                {
                    var index = ArrayUtility.IndexOf(animationClips, va.uAw.GetSelectionAnimationClip());
                    if (index >= 0 && animationClips[index] != null)
                        state.selectedIDs.Add(animationClips[index].GetInstanceID());
                }
            }

            public void OffsetSelection(int offset)
            {
                uTreeView.OffsetSelection(this, offset);
            }
        };

        [SerializeField]
        private bool clipSelectorFoldout = false;
        [SerializeField]
        private bool clipSelectorEditFoldout = false;

        private bool clipSelectorUpdate;
        private TreeViewState clipSelectorTreeState;
        private SearchField clipSelectorTreeSearchField;
        private ClipSelectorTreeView clipSelectorTreeView;

        private void InitializeClipSelector()
        {
            clipSelectorTreeState = new TreeViewState();
            clipSelectorTreeSearchField = new SearchField();
            clipSelectorTreeView = new ClipSelectorTreeView(clipSelectorTreeState);
            clipSelectorTreeSearchField.downOrUpArrowKeyPressed += clipSelectorTreeView.SetFocusAndEnsureSelectedItem;
        }
        private void UpdateClipSelectorTree()
        {
            if (clipSelectorTreeView == null) return;
            clipSelectorTreeView.Reload();
            clipSelectorTreeView.ExpandAll();
            clipSelectorTreeView.UpdateSelectedIds();
        }
        #endregion

        #region SelectionRect
        private struct SelectionRect
        {
            public void Reset()
            {
                Enable = false;
                start = Vector2.zero;
                end = Vector2.zero;
                distance = 0f;
                if (calcList == null) calcList = new List<GameObject>();
                else calcList.Clear();
                if (virtualCalcList == null) virtualCalcList = new List<HumanBodyBones>();
                else virtualCalcList.Clear();
                if (animatorIKCalcList == null) animatorIKCalcList = new List<AnimatorIKCore.IKTarget>();
                else animatorIKCalcList.Clear();
                if (originalIKCalcList == null) originalIKCalcList = new List<int>();
                else originalIKCalcList.Clear();
                beforeSelection = null;
                virtualBeforeSelection = null;
                beforeAnimatorIKSelection = null;
                beforeOriginalIKSelection = null;
            }
            public void SetStart(Vector2 add)
            {
                Enable = true;
                start = add;
                end = add;
                distance = 0f;
            }
            public void SetEnd(Vector2 add)
            {
                distance += Vector2.Distance(end, add);
                end = add;
            }
            public bool Enable { get; private set; }
            public Vector2 min { get { return Vector2.Min(start, end); } }
            public Vector2 max { get { return Vector2.Max(start, end); } }
            public Rect rect { get { return new Rect(min.x, min.y, max.x - min.x, max.y - min.y); } }

            public Vector2 start { get; private set; }
            public Vector2 end { get; private set; }
            public float distance { get; private set; }

            public List<GameObject> calcList;
            public List<HumanBodyBones> virtualCalcList;
            public List<AnimatorIKCore.IKTarget> animatorIKCalcList;
            public List<int> originalIKCalcList;
            public GameObject[] beforeSelection;
            public HumanBodyBones[] virtualBeforeSelection;
            public AnimatorIKCore.IKTarget[] beforeAnimatorIKSelection;
            public int[] beforeOriginalIKSelection;
        }
        private SelectionRect selectionRect;
        #endregion

        #region DisableEditor
        public class CustomAssetModificationProcessor : UnityEditor.AssetModificationProcessor
        {
            private static bool enable = true;

            static string[] OnWillSaveAssets(string[] paths)
            {
                if (enable)
                {
                    foreach (var w in Resources.FindObjectsOfTypeAll<VeryAnimationWindow>())
                    {
                        if (w.initialized)
                        {
                            w.Release();
                            Debug.Log("<color=blue>[Very Animation]</color>Editing ended : OnWillSaveAssets");
                        }
                    }
                }
                return paths;
            }

            public static void Pause()
            {
                enable = false;
            }
            public static void Resume()
            {
                enable = true;
            }
        }

        [InitializeOnLoadMethod]
        static void InitializeOnLoadMethod()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<VeryAnimationWindow>())
            {
                if (w.initialized)
                {
                    w.Release();
                    Debug.Log("<color=blue>[Very Animation]</color>Editing ended : InitializeOnLoadMethod");
                }
            }
        }

        static void CloseOtherWindows()
        {
            Action<EditorWindow> ForceCloseWindow = (w) =>
            {
                if (w != null)
                {
                    try
                    {
                        w.Close();
                    }
                    catch
                    {
                        EditorWindow.DestroyImmediate(w, true);
                    }
                }
            };
            ForceCloseWindow(VeryAnimationControlWindow.instance);
            ForceCloseWindow(VeryAnimationEditorWindow.instance);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange mode)
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<VeryAnimationWindow>())
            {
                if (w.initialized)
                {
                    w.Release();
                    Debug.Log("<color=blue>[Very Animation]</color>Editing ended : OnPlayModeStateChanged");
                }
            }
        }
        private void OnPauseStateChanged(PauseState mode)
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<VeryAnimationWindow>())
            {
                if (w.initialized)
                {
                    w.Release();
                    Debug.Log("<color=blue>[Very Animation]</color>Editing ended : OnPauseStateChanged");
                }
            }
        }
        #endregion

        #region Texture
        private Texture2D circleNormalTex;
        private Texture2D circleActiveTex;
        private Texture2D circle3NormalTex;
        private Texture2D circle3ActiveTex;
        private Texture2D diamondNormalTex;
        private Texture2D diamondActiveTex;
        private Texture2D circleDotNormalTex;
        private Texture2D circleDotActiveTex;
        public Texture2D redLightTex { get; private set; }
        public Texture2D orangeLightTex { get; private set; }
        public Texture2D greenLightTex { get; private set; }
        public Texture2D lightRimTex { get; private set; }
        public Texture2D mirrorTex { get; private set; }

        private void TextureReady()
        {
            circleNormalTex = EditorCommon.LoadTexture2DAssetAtPath("Assets/VeryAnimation/Textures/Editor/Circle_normal.psd");
            circleActiveTex = EditorCommon.LoadTexture2DAssetAtPath("Assets/VeryAnimation/Textures/Editor/Circle_active.psd");
            circle3NormalTex = EditorCommon.LoadTexture2DAssetAtPath("Assets/VeryAnimation/Textures/Editor/Circle3_normal.psd");
            circle3ActiveTex = EditorCommon.LoadTexture2DAssetAtPath("Assets/VeryAnimation/Textures/Editor/Circle3_active.psd");
            diamondNormalTex = EditorCommon.LoadTexture2DAssetAtPath("Assets/VeryAnimation/Textures/Editor/Diamond_normal.psd");
            diamondActiveTex = EditorCommon.LoadTexture2DAssetAtPath("Assets/VeryAnimation/Textures/Editor/Diamond_active.psd");
            circleDotNormalTex = EditorCommon.LoadTexture2DAssetAtPath("Assets/VeryAnimation/Textures/Editor/CircleDot_normal.psd");
            circleDotActiveTex = EditorCommon.LoadTexture2DAssetAtPath("Assets/VeryAnimation/Textures/Editor/CircleDot_active.psd");
            redLightTex = EditorGUIUtility.IconContent("lightMeter/redLight").image as Texture2D;
            orangeLightTex = EditorGUIUtility.IconContent("lightMeter/orangeLight").image as Texture2D;
            greenLightTex = EditorGUIUtility.IconContent("lightMeter/greenLight").image as Texture2D;
            lightRimTex = EditorGUIUtility.IconContent("lightMeter/lightRim").image as Texture2D;
            mirrorTex = EditorGUIUtility.IconContent("mirror").image as Texture2D;
        }
        #endregion

        #region GUIStyle
        public bool guiStyleReady { get; private set; }
        public GUISkin guiSkinSceneWindow { get; private set; }
        public GUIStyle guiStyleSceneWindow { get; private set; }
        public GUIStyle guiStyleSkinBox { get; private set; }
        public GUIStyle guiStyleBoldButton { get; private set; }
        public GUIStyle guiStyleCircleButton { get; private set; }
        public GUIStyle guiStyleCircle3Button { get; private set; }
        public GUIStyle guiStyleDiamondButton { get; private set; }
        public GUIStyle guiStyleCircleDotButton { get; private set; }
        public GUIStyle guiStyleCenterAlignLabel { get; private set; }
        public GUIStyle guiStyleCenterAlignItalicLabel { get; private set; }
        public GUIStyle guiStyleCenterAlignYellowLabel { get; private set; }
        public GUIStyle guiStyleBoldFoldout { get; private set; }
        public GUIStyle guiStyleDropDown { get; private set; }
        public GUIStyle guiStyleToolbarBoldButton { get; private set; }
        public GUIStyle guiStyleAnimationRowEvenStyle { get; private set; }
        public GUIStyle guiStyleAnimationRowOddStyle { get; private set; }
        public GUIStyle guiStyleMiddleRightMiniLabel { get; private set; }
        public GUIStyle guiStyleMiddleLeftGreyMiniLabel { get; private set; }
        public GUIStyle guiStyleMiddleRightGreyMiniLabel { get; private set; }
        public GUIStyle guiStyleMirrorButton { get; private set; }
        public GUIStyle guiStyleIconButton { get; private set; }
        public GUIStyle guiStyleIconActiveButton { get; private set; }
        public GUIContent[] guiContentMoveRotateTools { get; private set; }

        private void GUIStyleReady()
        {
            if (guiSkinSceneWindow == null)
            {
                if (EditorGUIUtility.isProSkin)
                    guiSkinSceneWindow = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene);
                else
                    guiSkinSceneWindow = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
            }
            if (guiStyleSceneWindow == null)
            {
                if (EditorGUIUtility.isProSkin)
                    guiStyleSceneWindow = new GUIStyle(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene).window);
                else
                    guiStyleSceneWindow = new GUIStyle(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).window);
            }
            if (guiStyleSkinBox == null)
            {
                guiStyleSkinBox = new GUIStyle(GUI.skin.box);
                var olBox = new GUIStyle("OL box");
                guiStyleSkinBox.normal = olBox.normal;
                guiStyleSkinBox.hover = olBox.hover;
                guiStyleSkinBox.focused = olBox.focused;
                guiStyleSkinBox.active = olBox.active;
            }
            if (guiStyleBoldButton == null)
            {
                guiStyleBoldButton = new GUIStyle(GUI.skin.button);
                guiStyleBoldButton.fontStyle = FontStyle.Bold;
            }
            if (guiStyleCircleButton == null || guiStyleCircleButton.normal.background != circleNormalTex || guiStyleCircleButton.active.background != circleActiveTex)
            {
                guiStyleCircleButton = new GUIStyle(GUI.skin.button);
                guiStyleCircleButton.normal.background = circleNormalTex;
                guiStyleCircleButton.normal.scaledBackgrounds = null;
                guiStyleCircleButton.active.background = circleActiveTex;
                guiStyleCircleButton.active.scaledBackgrounds = null;
                guiStyleCircleButton.border = new RectOffset(0, 0, 0, 0);
                guiStyleCircleButton.margin = new RectOffset(0, 0, 0, 0);
                guiStyleCircleButton.overflow = new RectOffset(0, 0, 0, 0);
                guiStyleCircleButton.padding = new RectOffset(0, 0, 0, 0);
                guiStyleCircleButton.imagePosition = ImagePosition.ImageOnly;
            }
            if (guiStyleCircle3Button == null || guiStyleCircle3Button.normal.background != circle3NormalTex || guiStyleCircle3Button.active.background != circle3ActiveTex)
            {
                guiStyleCircle3Button = new GUIStyle(GUI.skin.button);
                guiStyleCircle3Button.normal.background = circle3NormalTex;
                guiStyleCircle3Button.normal.scaledBackgrounds = null;
                guiStyleCircle3Button.active.background = circle3ActiveTex;
                guiStyleCircle3Button.active.scaledBackgrounds = null;
                guiStyleCircle3Button.border = new RectOffset(0, 0, 0, 0);
                guiStyleCircle3Button.margin = new RectOffset(0, 0, 0, 0);
                guiStyleCircle3Button.overflow = new RectOffset(0, 0, 0, 0);
                guiStyleCircle3Button.padding = new RectOffset(0, 0, 0, 0);
                guiStyleCircleButton.imagePosition = ImagePosition.ImageOnly;
            }
            if (guiStyleDiamondButton == null || guiStyleDiamondButton.normal.background != diamondNormalTex || guiStyleDiamondButton.active.background != diamondActiveTex)
            {
                guiStyleDiamondButton = new GUIStyle(GUI.skin.button);
                guiStyleDiamondButton.normal.background = diamondNormalTex;
                guiStyleDiamondButton.normal.scaledBackgrounds = null;
                guiStyleDiamondButton.active.background = diamondActiveTex;
                guiStyleDiamondButton.active.scaledBackgrounds = null;
                guiStyleDiamondButton.border = new RectOffset(0, 0, 0, 0);
                guiStyleDiamondButton.margin = new RectOffset(0, 0, 0, 0);
                guiStyleDiamondButton.overflow = new RectOffset(0, 0, 0, 0);
                guiStyleDiamondButton.padding = new RectOffset(0, 0, 0, 0);
                guiStyleCircleButton.imagePosition = ImagePosition.ImageOnly;
            }
            if (guiStyleCircleDotButton == null || guiStyleCircleDotButton.normal.background != circleDotNormalTex || guiStyleCircleDotButton.active.background != circleDotActiveTex)
            {
                guiStyleCircleDotButton = new GUIStyle(GUI.skin.button);
                guiStyleCircleDotButton.normal.background = circleDotNormalTex;
                guiStyleCircleDotButton.normal.scaledBackgrounds = null;
                guiStyleCircleDotButton.active.background = circleDotActiveTex;
                guiStyleCircleDotButton.active.scaledBackgrounds = null;
                guiStyleCircleDotButton.border = new RectOffset(0, 0, 0, 0);
                guiStyleCircleDotButton.margin = new RectOffset(0, 0, 0, 0);
                guiStyleCircleDotButton.overflow = new RectOffset(0, 0, 0, 0);
                guiStyleCircleDotButton.padding = new RectOffset(0, 0, 0, 0);
                guiStyleCircleButton.imagePosition = ImagePosition.ImageOnly;
            }
            if (guiStyleCenterAlignLabel == null)
            {
                guiStyleCenterAlignLabel = new GUIStyle(EditorStyles.label);
                guiStyleCenterAlignLabel.alignment = TextAnchor.MiddleCenter;
            }
            if (guiStyleCenterAlignItalicLabel == null)
            {
                guiStyleCenterAlignItalicLabel = new GUIStyle(EditorStyles.label);
                guiStyleCenterAlignItalicLabel.alignment = TextAnchor.MiddleCenter;
                guiStyleCenterAlignItalicLabel.fontStyle = FontStyle.Italic;
            }
            if (guiStyleCenterAlignYellowLabel == null)
            {
                guiStyleCenterAlignYellowLabel = new GUIStyle(EditorStyles.label);
                guiStyleCenterAlignYellowLabel.alignment = TextAnchor.MiddleCenter;
                guiStyleCenterAlignYellowLabel.normal.textColor = Color.yellow;
            }
            if (guiStyleBoldFoldout == null)
            {
                guiStyleBoldFoldout = new GUIStyle(EditorStyles.foldout);
                guiStyleBoldFoldout.fontStyle = FontStyle.Bold;
            }
            if (guiStyleDropDown == null)
            {
                guiStyleDropDown = new GUIStyle("DropDown");
                guiStyleDropDown.alignment = TextAnchor.MiddleCenter;
            }
            if (guiStyleToolbarBoldButton == null)
            {
                guiStyleToolbarBoldButton = new GUIStyle(EditorStyles.toolbarButton);
                guiStyleToolbarBoldButton.fontStyle = FontStyle.Bold;
            }
            if (guiStyleAnimationRowEvenStyle == null)
            {
                guiStyleAnimationRowEvenStyle = new GUIStyle("AnimationRowEven");
                if (guiStyleAnimationRowEvenStyle.normal.background == null && guiStyleAnimationRowEvenStyle.normal.scaledBackgrounds != null && guiStyleAnimationRowEvenStyle.normal.scaledBackgrounds.Length > 0)
                    guiStyleAnimationRowEvenStyle.normal.background = guiStyleAnimationRowEvenStyle.normal.scaledBackgrounds[0];
            }
            if (guiStyleAnimationRowOddStyle == null)
            {
                guiStyleAnimationRowOddStyle = new GUIStyle("AnimationRowOdd");
                if (guiStyleAnimationRowOddStyle.normal.background == null && guiStyleAnimationRowOddStyle.normal.scaledBackgrounds != null && guiStyleAnimationRowOddStyle.normal.scaledBackgrounds.Length > 0)
                    guiStyleAnimationRowOddStyle.normal.background = guiStyleAnimationRowOddStyle.normal.scaledBackgrounds[0];
            }
            if (guiStyleMiddleRightMiniLabel == null)
            {
                guiStyleMiddleRightMiniLabel = new GUIStyle(EditorStyles.miniLabel);
                guiStyleMiddleRightMiniLabel.alignment = TextAnchor.MiddleRight;
            }
            if (guiStyleMiddleLeftGreyMiniLabel == null)
            {
                guiStyleMiddleLeftGreyMiniLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                guiStyleMiddleLeftGreyMiniLabel.alignment = TextAnchor.MiddleLeft;
            }
            if (guiStyleMiddleRightGreyMiniLabel == null)
            {
                guiStyleMiddleRightGreyMiniLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                guiStyleMiddleRightGreyMiniLabel.alignment = TextAnchor.MiddleRight;
            }
            if (guiStyleMirrorButton == null)
            {
                guiStyleMirrorButton = new GUIStyle(GUI.skin.button);
                guiStyleMirrorButton.normal.background = mirrorTex;
                guiStyleMirrorButton.normal.scaledBackgrounds = null;
            }
            if (guiStyleIconButton == null)
            {
                guiStyleIconButton = new GUIStyle("IconButton");
            }
            if (guiStyleIconActiveButton == null)
            {
                guiStyleIconActiveButton = new GUIStyle(GUI.skin.button);
                guiStyleIconActiveButton.normal = guiStyleIconActiveButton.active;
                guiStyleIconActiveButton.padding = new RectOffset(0, 0, 0, 0);
            }

            if (guiContentMoveRotateTools == null)
            {
                guiContentMoveRotateTools = new GUIContent[]
                {
                    EditorGUIUtility.IconContent("MoveTool"),
                    EditorGUIUtility.IconContent("RotateTool"),
                };
            }

            guiStyleReady = true;
        }
        private void GUIStyleClear()
        {
            guiSkinSceneWindow = null;
            guiStyleSceneWindow = null;
            guiStyleSkinBox = null;
            guiStyleBoldButton = null;
            guiStyleCircleButton = null;
            guiStyleCircle3Button = null;
            guiStyleDiamondButton = null;
            guiStyleCircleDotButton = null;
            guiStyleCenterAlignLabel = null;
            guiStyleCenterAlignItalicLabel = null;
            guiStyleBoldFoldout = null;
            guiStyleDropDown = null;
            guiStyleToolbarBoldButton = null;
            guiStyleAnimationRowEvenStyle = null;
            guiStyleAnimationRowOddStyle = null;
            guiStyleMiddleRightMiniLabel = null;
            guiStyleMiddleLeftGreyMiniLabel = null;
            guiStyleMiddleRightGreyMiniLabel = null;
            guiStyleMirrorButton = null;
            guiStyleIconButton = null;
            guiStyleIconActiveButton = null;
            guiContentMoveRotateTools = null;
            guiStyleReady = false;
        }
        #endregion

        #region GUI
        private bool guiAnimationFoldout;
        private bool guiAnimationLoopFoldout;
        private bool guiAnimationWarningFoldout = true;
        private bool guiToolsFoldout;
        private bool guiSettingsFoldout;
        private bool guiHelpFoldout;
        private bool guiPreviewFoldout;

        private bool guiAnimationHelp;
        private bool guiToolsHelp;
        private bool guiSettingsHelp;
        private bool guiHelpHelp;
        private bool guiPreviewHelp;

        public Rect editorWindowSelectionRect = new Rect(8, 17 + 8, 0, 0);
        #endregion

        #region MemoryLeakCheck
#if Enable_MemoryLeakCheck
        private List<UnityEngine.Object> memoryLeakDontSaveList;
#endif
        #endregion

        private void OnEnable()
        {
            instance = this;

#if !UNITY_2019_1_OR_NEWER
            AssemblyDefinitionChanger.Refresh();
#endif

            {
                if (va == null)
                    va = new VeryAnimation();
                va.OnEnable();

                editorSettings = new EditorSettings();

                uSceneView = new USceneView();
                uSnapSettings = new USnapSettings();
                uDisc = new UDisc();
                uEditorGUI = new UEditorGUI();
                uHandleUtility = new UHandleUtility();
#if UNITY_2020_1_OR_NEWER
                uEditorWindow = new UEditorWindow_2020_1();
                uEditorGUIUtility = new UEditorGUIUtility_2018_1();
                uMuscleClipQualityInfo = new UMuscleClipEditorUtilities_2018_1();
                uAnimationUtility = new UAnimationUtility_2018_1();
#elif UNITY_2018_1_OR_NEWER
                uEditorWindow = new UEditorWindow();
                uEditorGUIUtility = new UEditorGUIUtility_2018_1();
                uMuscleClipQualityInfo = new UMuscleClipEditorUtilities_2018_1();
                uAnimationUtility = new UAnimationUtility_2018_1();
#else
                uEditorWindow = new UEditorWindow();
                uEditorGUIUtility = new UEditorGUIUtility();
                uMuscleClipQualityInfo = new UMuscleClipEditorUtilities();
                uAnimationUtility = new UAnimationUtility();
#endif
#if UNITY_2018_3_OR_NEWER
                uPrefabStage = new UPrefabStage();
#endif
                InitializeClipSelector();
                TextureReady();
                GUIStyleClear();
            }

            titleContent = new GUIContent("VeryAnimation");
            minSize = new Vector2(320, minSize.y);

            InternalEditorUtility.RepaintAllViews();
        }
        private void OnDisable()
        {
            if (va != null)
                va.OnDisable();
        }
        private void OnDestroy()
        {
            if (va != null)
                va.OnDestroy();
            instance = null;
        }

        private void OnSelectionChange()
        {
            clipSelectorUpdate = true;

            if (!initialized || va.isEditError) return;

            va.SelectGameObjectEvent();

            Repaint();
        }
        private void OnFocus()
        {
            instance = this;    //Measures against the problem that OnEnable may not come when repeating Shift + Space.

            clipSelectorUpdate = true;

            va.OnFocus();
        }
        private void OnLostFocus()
        {
            if (!initialized || va.isEditError) return;

            if (!uEditorWindow.HasFocus(this))
            {
                Release();
                Debug.Log("<color=blue>[Very Animation]</color>Editing ended : OnLostFocus");
                return;
            }
        }

        private void OnGUI()
        {
            if (va == null || va.uAw == null)
                return;

#if Enable_Profiler
            Profiler.BeginSample(string.Format("****VeryAnimationWindow.OnGUI {0}", Event.current));
#endif

            GUIStyleReady();

            Event e = Event.current;

            if (e.type == EventType.Layout)
            {
                if (clipSelectorUpdate)
                {
                    UpdateClipSelectorTree();
                    clipSelectorUpdate = false;
                }
            }

            if (va.uAw.instance == null)
            {
                #region Animation Window is not open
                EditorGUILayout.HelpBox(Language.GetText(Language.Help.AnimationWindowisnotopen), MessageType.Error);
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Open Animation Window"))
                    {
#if UNITY_2018_2_OR_NEWER
                        EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
#else
                        EditorApplication.ExecuteMenuItem("Window/Animation");
#endif
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                }

                VersionInfoGUI();
                #endregion
            }
            else if (!va.uAw.HasFocus())
            {
                #region Animation Window is not focus
                EditorGUILayout.HelpBox(Language.GetText(Language.Help.AnimationWindowisnotfocus), MessageType.Error);
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Focus Animation Window"))
                    {
                        va.uAw.instance.Focus();
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndHorizontal();
                }

                VersionInfoGUI();
                #endregion
            }
            else if (gameObject == null || (animator == null && animation == null))
            {
                #region Selection Error
                if (va.uAw.GetLinkedWithTimeline())
                    EditorGUILayout.LabelField(Language.GetText(Language.Help.TheSequenceEditortowhichAnimationislinkedisnotenabled), EditorStyles.centeredGreyMiniLabel, GUILayout.Height(48));
                else
                    EditorGUILayout.LabelField(Language.GetText(Language.Help.Noanimatableobjectselected), EditorStyles.centeredGreyMiniLabel, GUILayout.Height(48));

                VersionInfoGUI();
                #endregion
            }
            else if (!va.edit)
            {
                #region Ready
                va.UpdateCurrentInfo();
                var clip = EditorApplication.isPlaying ? playingAnimationClip : va.currentClip;

                #region Animation
                {
                    EditorGUILayout.BeginVertical(guiStyleSkinBox);
                    if (va.uAw.GetLinkedWithTimeline())
                    {
#if VERYANIMATION_TIMELINE
                        EditorGUILayout.LabelField("Linked with Sequence Editor", EditorStyles.centeredGreyMiniLabel);
                        var currentDirector = va.uAw.GetTimelineCurrentDirector();
                        if (currentDirector != null)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("Playable Director", GUILayout.Width(116));
                            GUILayout.FlexibleSpace();
                            EditorGUI.BeginDisabledGroup(true);
                            EditorGUILayout.ObjectField(currentDirector, typeof(PlayableDirector), false, GUILayout.Width(180));
                            EditorGUI.EndDisabledGroup();
                            EditorGUILayout.EndHorizontal();
                        }
#else
                        Assert.IsTrue(false);
#endif
                    }
                    else
                    {
                        if (EditorApplication.isPlaying)
                        {
                            if (animator != null)
                                EditorGUILayout.LabelField("Linked with Animator Controller", EditorStyles.centeredGreyMiniLabel);
                            else if (animation != null)
                                EditorGUILayout.LabelField("Linked with Animation Component", EditorStyles.centeredGreyMiniLabel);
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Linked with Animation Window", EditorStyles.centeredGreyMiniLabel);
                        }
                    }
                    {
                        #region Animatable
                        if (animator != null)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("Animator", GUILayout.Width(116));
                            GUILayout.FlexibleSpace();
                            EditorGUI.BeginDisabledGroup(true);
                            EditorGUILayout.ObjectField(animator, typeof(Animator), false, GUILayout.Width(180));
                            EditorGUI.EndDisabledGroup();
                            EditorGUILayout.EndHorizontal();
                        }
                        else if (animation != null)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("Animation", GUILayout.Width(116));
                            GUILayout.FlexibleSpace();
                            EditorGUI.BeginDisabledGroup(true);
                            EditorGUILayout.ObjectField(animation, typeof(Animation), false, GUILayout.Width(180));
                            EditorGUI.EndDisabledGroup();
                            EditorGUILayout.EndHorizontal();
                        }
                        #endregion

                        #region Animation Clip
                        if (va.uAw.GetLinkedWithTimeline() || playingAnimationClip != null)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUI.BeginChangeCheck();
                            EditorGUILayout.LabelField("Animation Clip", GUILayout.Width(116));
                            GUILayout.FlexibleSpace();
                            var isReadOnly = clip != null && (clip.hideFlags & HideFlags.NotEditable) != HideFlags.None;
                            EditorGUI.BeginDisabledGroup(true);
                            EditorGUILayout.ObjectField(clip, typeof(AnimationClip), false, GUILayout.Width(isReadOnly ? 98 : 180));
                            EditorGUI.EndDisabledGroup();
                            if (isReadOnly)
                                EditorGUILayout.LabelField("(Read-Only)", GUILayout.Width(78));
                            EditorGUILayout.EndHorizontal();
                            if (playingAnimationClip != null)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUI.BeginDisabledGroup(true);
                                EditorGUILayout.Slider("Time", playingAnimationTime, 0f, playingAnimationLength);
                                EditorGUI.EndDisabledGroup();
                                EditorGUI.indentLevel--;
                            }
                        }
                        else
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUI.BeginChangeCheck();
                            clipSelectorFoldout = EditorGUILayout.Foldout(clipSelectorFoldout, "Animation Clip", true);
                            if (EditorGUI.EndChangeCheck())
                            {
                                UpdateClipSelectorTree();
                            }
                            GUILayout.FlexibleSpace();
                            var isReadOnly = clip != null && (clip.hideFlags & HideFlags.NotEditable) != HideFlags.None;
                            EditorGUI.BeginDisabledGroup(true);
                            EditorGUILayout.ObjectField(clip, typeof(AnimationClip), false, GUILayout.Width(isReadOnly ? 98 : 180));
                            EditorGUI.EndDisabledGroup();
                            if (isReadOnly)
                                EditorGUILayout.LabelField("(Read-Only)", GUILayout.Width(78));
                            EditorGUILayout.EndHorizontal();
                            if (clipSelectorFoldout)
                            {
                                #region ClipSelector
                                {
                                    clipSelectorTreeView.UpdateSelectedIds();
                                    {
                                        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                                        EditorGUI.indentLevel++;
                                        clipSelectorTreeView.searchString = clipSelectorTreeSearchField.OnToolbarGUI(clipSelectorTreeView.searchString);
                                        EditorGUI.indentLevel--;
                                        EditorGUILayout.EndHorizontal();
                                    }
                                    {
                                        var rect = EditorGUILayout.GetControlRect(false, position.height * 0.4f);
                                        clipSelectorTreeView.OnGUI(rect);
                                    }
                                }
                                #endregion
                            }
                        }
                        #endregion
                    }
                    EditorGUILayout.EndVertical();
                }
                #endregion

                EditorGUI.BeginDisabledGroup(va.isError);
                if (GUILayout.Button("Edit Animation", guiStyleBoldButton, GUILayout.Height(32)))
                {
                    Initialize();
                }
                EditorGUI.EndDisabledGroup();

                errorLogScrollPosition = EditorGUILayout.BeginScrollView(errorLogScrollPosition);
                {
                    #region UnityVersion
                    {
#if UNITY_2023_1_OR_NEWER || !UNITY_2019_1_OR_NEWER
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.NotSupportUnityMessage), MessageType.Error);
#endif
                    }
                    #endregion

                    #region Error
                    if (va.uAw.GetSelectionAnimationClip() == null)
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.ThereisnoAnimationClip), MessageType.Error);
                    }
                    if (!gameObject.activeInHierarchy)
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.GameObjectisnotActive), MessageType.Error);
                    }
                    if (animator != null && !animator.hasTransformHierarchy)
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.Editingonoptimizedtransformhierarchyisnotsupported), MessageType.Error);
                    }
                    if (Application.isPlaying && animation != null)
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.EditingLegacywhileplayingisnotsupported), MessageType.Error);
                    }
                    if (Application.isPlaying && animator != null && animator.runtimeAnimatorController == null)
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.EditingNoAnimatorControllernotsupported), MessageType.Error);
                    }
                    if (!va.uAw.GetLinkedWithTimeline())
                    {
                        if (animator != null && animator.runtimeAnimatorController != null && (animator.runtimeAnimatorController.hideFlags & (HideFlags.DontSave | HideFlags.NotEditable)) != 0)
                        {
                            EditorGUILayout.HelpBox(Language.GetText(Language.Help.AnimatorControllerisnoteditable), MessageType.Error);
                        }
                    }
                    else
                    {
#if VERYANIMATION_TIMELINE
                        if (!va.uAw.GetTimelineTrackAssetEditable())
                        {
                            EditorGUILayout.HelpBox(Language.GetText(Language.Help.TheAnimationTracktowhichAnimationislinkedisnotenabled), MessageType.Error);
                        }
                        if (Application.isPlaying)
                        {
                            EditorGUILayout.HelpBox(Language.GetText(Language.Help.EditingTimelinewhileplayingisnotsupported), MessageType.Error);
                        }
                        {
                            var currentDirector = va.uAw.GetTimelineCurrentDirector();
                            if (currentDirector != null && !currentDirector.gameObject.activeInHierarchy)
                            {
                                EditorGUILayout.HelpBox(Language.GetText(Language.Help.TimelineGameObjectisnotActive), MessageType.Error);
                            }
                            if (currentDirector != null && !currentDirector.enabled)
                            {
                                EditorGUILayout.HelpBox(Language.GetText(Language.Help.TimelinePlayableDirectorisnotEnable), MessageType.Error);
                            }
                        }
                        if (!va.uAw.GetTimelineHasFocus())
                        {
                            EditorGUILayout.HelpBox(Language.GetText(Language.Help.TimelineWindowisnotfocus), MessageType.Error);
                        }
#else
                        Assert.IsTrue(false);
#endif
                    }
#if UNITY_2018_3_OR_NEWER
                    if (uPrefabStage.GetAutoSave(PrefabStageUtility.GetCurrentPrefabStage()))
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.PrefabModeEnableAutoSave), MessageType.Error);
                    }
                    if (PrefabStageUtility.GetCurrentPrefabStage() != null &&
                        !EditorCommon.IsAncestorObject(va.uAw.GetActiveRootGameObject(), PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot))
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.PrefabModeObjectNotMatch), MessageType.Error);
                    }
#endif
                    #endregion

                    #region Warning
                    if (gameObject != null && gameObject.activeInHierarchy && animator != null && animator.isHuman && animator.hasTransformHierarchy && va.uAvatar.GetHasTDoF(animator.avatar))
                    {
                        #region TDOF
                        if (!animator.isInitialized)
                            animator.Rebind();
                        for (int i = 0; i < VeryAnimation.HumanBonesAnimatorTDOFIndex.Length; i++)
                        {
                            if (VeryAnimation.HumanBonesAnimatorTDOFIndex[i] == null) continue;
                            var hi = (HumanBodyBones)i;
                            if (animator.GetBoneTransform(hi) != null)
                            {
                                if (animator.GetBoneTransform(VeryAnimation.HumanBonesAnimatorTDOFIndex[i].parent) == null)
                                    EditorGUILayout.HelpBox(string.Format(Language.GetText(Language.Help.TranslationDoFisdisabled), VeryAnimation.HumanBonesAnimatorTDOFIndex[i].parent, hi), MessageType.Warning);
                            }
                            else
                            {
                                EditorGUILayout.HelpBox(string.Format(Language.GetText(Language.Help.TranslationDoFisdisabled), hi, hi), MessageType.Warning);
                            }
                        }
                        #endregion
                    }
                    #endregion
                }
                EditorGUILayout.EndScrollView();

                VersionInfoGUI();
                #endregion
            }
            else if (!va.isEditError)
            {
                #region Editing
                #region Toolbar
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                    {
                        EditorGUI.BeginChangeCheck();
                        guiAnimationFoldout = GUILayout.Toggle(guiAnimationFoldout, "Animation", EditorStyles.toolbarButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorPrefs.SetBool("VeryAnimation_Main_Animation", guiAnimationFoldout);
                        }
                    }
                    {
                        EditorGUI.BeginChangeCheck();
                        guiToolsFoldout = GUILayout.Toggle(guiToolsFoldout, "Tools", EditorStyles.toolbarButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorPrefs.SetBool("VeryAnimation_Main_Tools", guiToolsFoldout);
                        }
                    }
                    {
                        EditorGUI.BeginChangeCheck();
                        guiSettingsFoldout = GUILayout.Toggle(guiSettingsFoldout, "Settings", EditorStyles.toolbarButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorPrefs.SetBool("VeryAnimation_Main_Settings", guiSettingsFoldout);
                        }
                    }
                    {
                        EditorGUI.BeginChangeCheck();
                        guiHelpFoldout = GUILayout.Toggle(guiHelpFoldout, "Help", EditorStyles.toolbarButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorPrefs.SetBool("VeryAnimation_Main_Help", guiHelpFoldout);
                        }
                    }
                    {
                        EditorGUI.BeginChangeCheck();
                        guiPreviewFoldout = GUILayout.Toggle(guiPreviewFoldout, "Preview", EditorStyles.toolbarButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorPrefs.SetBool("VeryAnimation_Main_Preview", guiPreviewFoldout);
                        }
                    }
                    EditorGUILayout.Space();
                    #region Edit
                    if (GUILayout.Button("Exit", guiStyleToolbarBoldButton, GUILayout.Width(48)))
                    {
                        Release();
                        return;
                    }
                    #endregion
                    EditorGUILayout.EndHorizontal();
                }
                #endregion

                #region Animation
                if (guiAnimationFoldout)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    guiAnimationFoldout = EditorGUILayout.Foldout(guiAnimationFoldout, "Animation", true, guiStyleBoldFoldout);
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorPrefs.SetBool("VeryAnimation_Main_Animation", guiAnimationFoldout);
                    }
                    {
                        EditorGUILayout.Space();
                        if (GUILayout.Button(uEditorGUI.GetHelpIcon(), guiAnimationHelp ? guiStyleIconActiveButton : guiStyleIconButton, GUILayout.Width(19)))
                        {
                            guiAnimationHelp = !guiAnimationHelp;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    {
                        if (guiAnimationHelp)
                        {
                            EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpAnimation), MessageType.Info);
                        }

                        EditorGUILayout.BeginVertical(guiStyleSkinBox);
                        {
                            #region Animatable
                            if (animator != null)
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("Animator", GUILayout.Width(116));
                                GUILayout.FlexibleSpace();
                                EditorGUI.BeginDisabledGroup(true);
                                EditorGUILayout.ObjectField(animator, typeof(Animator), false, GUILayout.Width(180));
                                EditorGUI.EndDisabledGroup();
                                EditorGUILayout.EndHorizontal();
                            }
                            else if (animation != null)
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("Animation", GUILayout.Width(116));
                                GUILayout.FlexibleSpace();
                                EditorGUI.BeginDisabledGroup(true);
                                EditorGUILayout.ObjectField(animation, typeof(Animation), false, GUILayout.Width(180));
                                EditorGUI.EndDisabledGroup();
                                EditorGUILayout.EndHorizontal();
                            }
                            #endregion

                            #region Animation Clip
                            {
                                if (va.uAw.GetLinkedWithTimeline())
                                {
#if VERYANIMATION_TIMELINE
                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUI.BeginChangeCheck();
                                    EditorGUILayout.LabelField("Animation Clip", GUILayout.Width(116));
                                    GUILayout.FlexibleSpace();
                                    var isReadOnly = va.currentClip != null && (va.currentClip.hideFlags & HideFlags.NotEditable) != HideFlags.None;
                                    EditorGUI.BeginDisabledGroup(true);
                                    EditorGUILayout.ObjectField(va.currentClip, typeof(AnimationClip), false, GUILayout.Width(isReadOnly ? 98 : 180));
                                    EditorGUI.EndDisabledGroup();
                                    if (isReadOnly)
                                        EditorGUILayout.LabelField("(Read-Only)", GUILayout.Width(78));
                                    EditorGUILayout.EndHorizontal();
#else
                                    Assert.IsTrue(false);
#endif
                                }
                                else
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUI.BeginChangeCheck();
                                    clipSelectorEditFoldout = EditorGUILayout.Foldout(clipSelectorEditFoldout, "Animation Clip", true);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        UpdateClipSelectorTree();
                                    }
                                    GUILayout.FlexibleSpace();
                                    var isReadOnly = va.currentClip != null && (va.currentClip.hideFlags & HideFlags.NotEditable) != HideFlags.None;
                                    EditorGUI.BeginDisabledGroup(true);
                                    EditorGUILayout.ObjectField(va.currentClip, typeof(AnimationClip), false, GUILayout.Width(isReadOnly ? 98 : 180));
                                    EditorGUI.EndDisabledGroup();
                                    if (isReadOnly)
                                        EditorGUILayout.LabelField("(Read-Only)", GUILayout.Width(78));
                                    EditorGUILayout.EndHorizontal();
                                    if (clipSelectorEditFoldout)
                                    {
                                        #region ClipSelector
                                        {
                                            clipSelectorTreeView.UpdateSelectedIds();
                                            {
                                                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                                                EditorGUI.indentLevel++;
                                                clipSelectorTreeView.searchString = clipSelectorTreeSearchField.OnToolbarGUI(clipSelectorTreeView.searchString);
                                                EditorGUI.indentLevel--;
                                                EditorGUILayout.EndHorizontal();
                                            }
                                            {
                                                var rect = EditorGUILayout.GetControlRect(false, position.height * 0.4f);
                                                clipSelectorTreeView.OnGUI(rect);
                                            }
                                        }
                                        #endregion
                                    }
                                }
                            }
                            #endregion

                            if (va.currentClip != null)
                            {
                                AnimationClipSettings animationClipSettings = AnimationUtility.GetAnimationClipSettings(va.currentClip);
                                bool hasMotionCurves = uAnimationUtility.HasMotionCurves(va.currentClip);
                                bool hasRootCurves = uAnimationUtility.HasRootCurves(va.currentClip);
                                EditorGUI.indentLevel++;
                                if ((va.currentClip.hideFlags & HideFlags.NotEditable) != 0)
                                {
                                    EditorGUILayout.BeginHorizontal(guiStyleSkinBox);
                                    EditorGUILayout.HelpBox(Language.GetText(Language.Help.AnimationclipisReadOnly), MessageType.Warning);
                                    {
                                        EditorGUILayout.BeginVertical();
                                        EditorGUILayout.Space();
                                        if (GUILayout.Button("Duplicate and Replace"))
                                        {
                                            va.DuplicateAndReplace();
                                        }
                                        EditorGUILayout.Space();
                                        EditorGUILayout.EndVertical();
                                    }
                                    EditorGUILayout.EndHorizontal();
                                }
                                else
                                {
                                    if (animator != null && animator.isHuman && va.currentClip.isHumanMotion)
                                        EditorGUILayout.LabelField("Humanoid Motion");
                                    else if (!va.currentClip.legacy)
                                        EditorGUILayout.LabelField("Generic Motion");
                                    else
                                        EditorGUILayout.LabelField("Legacy Motion");
                                    #region Loop
                                    if (va.currentClip.isLooping && !va.currentClip.legacy)
                                    {
                                        guiAnimationLoopFoldout = EditorGUILayout.Foldout(guiAnimationLoopFoldout, "Loop", true);
                                        EditorGUI.indentLevel++;
                                        if (guiAnimationLoopFoldout)
                                        {
                                            if (animator != null && !animator.isInitialized)
                                                animator.Rebind();
                                            var info = uMuscleClipQualityInfo.GetMuscleClipQualityInfo(va.currentClip, 0f, va.currentClip.length);
                                            var hasRootCurve = va.uAnimationUtility.HasRootCurves(va.currentClip) || va.uAnimationUtility.HasMotionCurves(va.currentClip);
                                            {
                                                EditorGUILayout.BeginHorizontal();
                                                if (animationClipSettings.loopBlend)
                                                    EditorGUILayout.LabelField(new GUIContent("Loop Pose", "Loop Blend"), GUILayout.Width(160f));
                                                else
                                                    EditorGUILayout.LabelField("Loop", GUILayout.Width(160f));
                                                GUILayout.FlexibleSpace();
                                                if (hasRootCurve && va.currentClip.isHumanMotion)
                                                {
                                                    EditorGUILayout.LabelField("loop match", guiStyleMiddleRightMiniLabel, GUILayout.Width(90f));
                                                    var rect = EditorGUILayout.GetControlRect(false, 16f, GUILayout.Width(16f));
                                                    if (animationClipSettings.loopBlend)
                                                    {
                                                        if (info.loop < 0.33f)
                                                            GUI.DrawTexture(rect, redLightTex);
                                                        else if (info.loop < 0.66f)
                                                            GUI.DrawTexture(rect, orangeLightTex);
                                                        else
                                                            GUI.DrawTexture(rect, greenLightTex);
                                                    }
                                                    else
                                                    {
                                                        if (info.loop < 0.66f)
                                                            GUI.DrawTexture(rect, redLightTex);
                                                        else if (info.loop < 0.99f)
                                                            GUI.DrawTexture(rect, orangeLightTex);
                                                        else
                                                            GUI.DrawTexture(rect, greenLightTex);
                                                    }
                                                    GUI.DrawTexture(rect, lightRimTex);
                                                }
                                                EditorGUILayout.EndHorizontal();
                                            }
                                            if (hasRootCurve)
                                            {
                                                Action<string, float, bool> LoopMatchGUI = (name, value, bake) =>
                                                {
                                                    EditorGUILayout.BeginHorizontal();
                                                    EditorGUILayout.LabelField(name, GUILayout.Width(160f));
                                                    EditorGUILayout.Space();
                                                    if (bake)
                                                    {
                                                        EditorGUILayout.LabelField("loop match", guiStyleMiddleRightMiniLabel, GUILayout.Width(90f));
                                                        var rect = EditorGUILayout.GetControlRect(false, 16f, GUILayout.Width(16f));
                                                        if (animationClipSettings.loopBlend)
                                                        {
                                                            if (value < 0.33f)
                                                                GUI.DrawTexture(rect, redLightTex);
                                                            else if (value < 0.66f)
                                                                GUI.DrawTexture(rect, orangeLightTex);
                                                            else
                                                                GUI.DrawTexture(rect, greenLightTex);
                                                        }
                                                        else
                                                        {
                                                            if (value < 0.66f)
                                                                GUI.DrawTexture(rect, redLightTex);
                                                            else if (value < 0.99f)
                                                                GUI.DrawTexture(rect, orangeLightTex);
                                                            else
                                                                GUI.DrawTexture(rect, greenLightTex);
                                                        }
                                                        GUI.DrawTexture(rect, lightRimTex);
                                                    }
                                                    else
                                                    {
                                                        EditorGUILayout.LabelField("root motion", guiStyleMiddleRightMiniLabel, GUILayout.Width(90f));
                                                    }
                                                    EditorGUILayout.EndHorizontal();
                                                };
                                                LoopMatchGUI("Loop Orientation", info.loopOrientation, animationClipSettings.loopBlendOrientation);
                                                LoopMatchGUI("Loop Position (Y)", info.loopPositionY, animationClipSettings.loopBlendPositionY);
                                                LoopMatchGUI("Loop Position (XZ)", info.loopPositionXZ, animationClipSettings.loopBlendPositionXZ);
                                            }
                                        }
                                        EditorGUI.indentLevel--;
                                    }
                                    #endregion
                                    #region Warning
                                    {
                                        int count = 0;
                                        {
                                            if (animationClipSettings.loopTime && animationClipSettings.loopBlend) count++;
                                            if (!animationClipSettings.keepOriginalPositionY && animationClipSettings.heightFromFeet && !va.IsHaveAnimationCurveAnimatorIkT(VeryAnimation.AnimatorIKIndex.LeftFoot) && !va.IsHaveAnimationCurveAnimatorIkT(VeryAnimation.AnimatorIKIndex.RightFoot)) count++;
                                            if (hasRootCurves && !hasMotionCurves)
                                            {
                                                if (!animationClipSettings.keepOriginalOrientation || !animationClipSettings.keepOriginalPositionXZ || !animationClipSettings.keepOriginalPositionY ||
                                                    animationClipSettings.level != 0f || animationClipSettings.orientationOffsetY != 0f)
                                                    count++;
                                            }
                                            if (va.uAw.GetLinkedWithTimeline())
                                            {
#if VERYANIMATION_TIMELINE
                                                if (va.uAw.GetTimelineAnimationPlayableAssetHasRootTransforms() && va.uAw.GetRemoveStartOffset())
                                                    count++;
#else
                                                Assert.IsTrue(false);
#endif
                                            }
                                        }
                                        if (count > 0)
                                        {
                                            guiAnimationWarningFoldout = EditorGUILayout.Foldout(guiAnimationWarningFoldout, "Warning", true);
                                            EditorGUI.indentLevel++;
                                            if (guiAnimationWarningFoldout)
                                            {
                                                if (animationClipSettings.loopTime && animationClipSettings.loopBlend)
                                                {
                                                    EditorGUILayout.HelpBox(Language.GetText(Language.Help.AnimationClipSettingsLoopPoseisenabled), MessageType.Warning);
                                                }
                                                if (!animationClipSettings.keepOriginalPositionY && animationClipSettings.heightFromFeet && !va.IsHaveAnimationCurveAnimatorIkT(VeryAnimation.AnimatorIKIndex.LeftFoot) && !va.IsHaveAnimationCurveAnimatorIkT(VeryAnimation.AnimatorIKIndex.RightFoot))
                                                {
                                                    EditorGUILayout.HelpBox(Language.GetText(Language.Help.AnimationClipSettingsRootTransformPositionYisFeet), MessageType.Warning);
                                                }
                                                if (hasRootCurves && !hasMotionCurves)
                                                {
                                                    if (!animationClipSettings.keepOriginalOrientation || !animationClipSettings.keepOriginalPositionXZ || !animationClipSettings.keepOriginalPositionY ||
                                                        animationClipSettings.level != 0f || animationClipSettings.orientationOffsetY != 0f)
                                                    {
                                                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.AnimationClipSettingsBasedUponisnotOriginal), MessageType.Warning);
                                                    }
                                                }
                                                if (va.uAw.GetLinkedWithTimeline())
                                                {
#if VERYANIMATION_TIMELINE
                                                    if (va.uAw.GetTimelineAnimationPlayableAssetHasRootTransforms() && va.uAw.GetRemoveStartOffset())
                                                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.AnimationTrackSettingRemoveStartOffsetsEnable), MessageType.Warning);
#else
                                                    Assert.IsTrue(false);
#endif
                                                }
                                            }
                                            EditorGUI.indentLevel--;
                                        }
                                    }
                                    #endregion
                                    #region Error
                                    {
                                        int count = 0;
                                        {
                                            if (animationClipSettings.cycleOffset != 0f) count++;
                                            if (animationClipSettings.mirror) count++;
                                            if (!va.animatorApplyRootMotion && hasRootCurves && !hasMotionCurves &&
                                                (!animationClipSettings.loopBlendOrientation || !animationClipSettings.loopBlendPositionXZ || !animationClipSettings.loopBlendPositionY)) count++;
                                            if (va.uAw.GetLinkedWithTimeline())
                                            {
#if VERYANIMATION_TIMELINE
                                                var timelineFrameRate = va.uAw.GetTimelineFrameRate();
                                                if (va.currentClip != null && va.currentClip.frameRate != timelineFrameRate)
                                                    count++;
#else
                                                Assert.IsTrue(false);
#endif
                                            }
                                        }
                                        if (count > 0)
                                        {
                                            EditorGUILayout.LabelField("Error");
                                            EditorGUI.indentLevel++;
                                            {
                                                if (animationClipSettings.cycleOffset != 0f)
                                                {
                                                    EditorGUILayout.HelpBox(Language.GetText(Language.Help.AnimationClipSettingsCycleOffsetisnot0), MessageType.Error);
                                                }
                                                if (animationClipSettings.mirror)
                                                {
                                                    EditorGUILayout.HelpBox(Language.GetText(Language.Help.AnimationClipSettingsMirrorisenabled), MessageType.Error);
                                                }
                                                if (!va.animatorApplyRootMotion && hasRootCurves && !hasMotionCurves &&
                                                    (!animationClipSettings.loopBlendOrientation || !animationClipSettings.loopBlendPositionXZ || !animationClipSettings.loopBlendPositionY))
                                                {
                                                    EditorGUILayout.HelpBox(Language.GetText(Language.Help.AnimationClipSettingsBakeIntoPoseDisableRootMotion), MessageType.Error);
                                                }
                                                if (va.uAw.GetLinkedWithTimeline())
                                                {
#if VERYANIMATION_TIMELINE
                                                    var timelineFrameRate = va.uAw.GetTimelineFrameRate();
                                                    if (va.currentClip != null && va.currentClip.frameRate != timelineFrameRate)
                                                    {
                                                        EditorGUILayout.HelpBox(string.Format(Language.GetText(Language.Help.AnimationClipSettingsFramerateofTimelineandAnimationClipdonotmatch), va.currentClip.frameRate, timelineFrameRate), MessageType.Error);
                                                    }
#else
                                                    Assert.IsTrue(false);
#endif
                                                }
                                            }
                                            EditorGUI.indentLevel--;
                                        }
                                    }
                                    #endregion
                                }
                                EditorGUI.indentLevel--;
                            }
                        }
                        GUILayout.Space(3);
                        EditorGUILayout.EndVertical();
                    }
                }
                #endregion

                #region Tools
                if (guiToolsFoldout)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    guiToolsFoldout = EditorGUILayout.Foldout(guiToolsFoldout, "Tools", true, guiStyleBoldFoldout);
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorPrefs.SetBool("VeryAnimation_Main_Tools", guiToolsFoldout);
                    }
                    {
                        EditorGUILayout.Space();
                        if (GUILayout.Button(uEditorGUI.GetHelpIcon(), guiToolsHelp ? guiStyleIconActiveButton : guiStyleIconButton, GUILayout.Width(19)))
                        {
                            guiToolsHelp = !guiToolsHelp;
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    if (guiToolsHelp)
                    {
                        EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpTools), MessageType.Info);
                    }

                    va.ToolsGUI();
                }
                #endregion

                #region Settings
                if (guiSettingsFoldout)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    guiSettingsFoldout = EditorGUILayout.Foldout(guiSettingsFoldout, "Settings", true, guiStyleBoldFoldout);
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorPrefs.SetBool("VeryAnimation_Main_Settings", guiSettingsFoldout);
                    }
                    {
                        EditorGUILayout.Space();
                        if (GUILayout.Button(uEditorGUI.GetHelpIcon(), guiSettingsHelp ? guiStyleIconActiveButton : guiStyleIconButton, GUILayout.Width(19)))
                        {
                            guiSettingsHelp = !guiSettingsHelp;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    {
                        if (guiSettingsHelp)
                        {
                            EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpSettings), MessageType.Info);
                        }

                        editorSettings.SettingsGUI();
                    }
                }
                #endregion

                #region Help
                if (guiHelpFoldout)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    guiHelpFoldout = EditorGUILayout.Foldout(guiHelpFoldout, "Help", true, guiStyleBoldFoldout);
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorPrefs.SetBool("VeryAnimation_Main_Help", guiHelpFoldout);
                    }
                    {
                        EditorGUILayout.Space();
                        if (GUILayout.Button(uEditorGUI.GetHelpIcon(), guiHelpHelp ? guiStyleIconActiveButton : guiStyleIconButton, GUILayout.Width(19)))
                        {
                            guiHelpHelp = !guiHelpHelp;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    {
                        if (guiHelpHelp)
                        {
                            EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpHelp), MessageType.Info);
                        }

                        EditorGUILayout.BeginVertical(guiStyleSkinBox);
                        {
                            EditorGUILayout.LabelField("Version", Version);
#if UNITY_2019_1_OR_NEWER
                            EditorGUILayout.LabelField(Language.GetContent(Language.Help.HelpShortcuts));
#else
                            EditorGUILayout.LabelField("Shortcuts");
#endif
                            EditorGUI.indentLevel++;
                            {
                                EditorGUILayout.LabelField("Esc", "[Editor] Exit edit");
                                EditorGUILayout.LabelField("O", "[Editor] Change Clamp");
                                EditorGUILayout.LabelField("J", "[Editor] Change Foot IK");
                                EditorGUILayout.LabelField("M", "[Editor] Change Mirror");
                                EditorGUILayout.LabelField("N", "[Editor] Change Collision");
                                EditorGUILayout.LabelField("L", "[Editor] Change Root Correction Mode");
                                EditorGUILayout.LabelField("I", "[Editor] Change selection bone IK");
                                EditorGUILayout.LabelField("F5", "[AnimationWindow] Force refresh");
                                EditorGUILayout.LabelField("Page Down", "[AnimationWindow] Next animation clip");
                                EditorGUILayout.LabelField("Page Up", "[AnimationWindow] Previous animation clip");
                                EditorGUILayout.LabelField("Space", "[AnimationWindow] Change playing");
                                EditorGUILayout.LabelField("C", "[AnimationWindow] Switch between curves and dope sheet");
                                EditorGUILayout.LabelField("K", "[AnimationWindow] Add keyframe");
                                EditorGUILayout.LabelField(",", "[AnimationWindow] Move to next frame");
                                EditorGUILayout.LabelField(".", "[AnimationWindow] Move to previous frame");
                                EditorGUILayout.LabelField("Alt + ,", "[AnimationWindow] Move to next keyframe");
                                EditorGUILayout.LabelField("Alt + .", "[AnimationWindow] Move to previous keyframe");
                                EditorGUILayout.LabelField("Shift + ,", "[AnimationWindow] Move to first keyframe");
                                EditorGUILayout.LabelField("Shift + .", "[AnimationWindow] Move to last keyframe");
                                EditorGUILayout.LabelField("Keypad Plus", "[AnimationWindow] Edit Keys/Add In between");
                                EditorGUILayout.LabelField("Keypad Minus", "[AnimationWindow] Edit Keys/Remove In between");
#if UNITY_EDITOR_OSX
                                EditorGUILayout.LabelField("H", "[Hierarchy] Hide select bones");
                                EditorGUILayout.LabelField("Shift + H", "[Hierarchy] Show select bones");
                                EditorGUILayout.LabelField("Alt + Space", "[Preview] Change playing");
                                EditorGUILayout.LabelField("Command + Keypad Plus", "[IK] Add IK - Level / Direction");
                                EditorGUILayout.LabelField("Command + Keypad Minus", "[IK] Sub IK - Level / Direction");
#else
                                EditorGUILayout.LabelField("H", "[Hierarchy] Hide select bones");
                                EditorGUILayout.LabelField("Shift + H", "[Hierarchy] Show select bones");
                                EditorGUILayout.LabelField("Ctrl + Space", "[Preview] Change playing");
                                EditorGUILayout.LabelField("Ctrl + Keypad Plus", "[IK] Add IK - Level / Direction");
                                EditorGUILayout.LabelField("Ctrl + Keypad Minus", "[IK] Sub IK - Level / Direction");
#endif
                            }
                            EditorGUI.indentLevel--;
                            EditorGUILayout.LabelField("Icons");
                            EditorGUI.indentLevel++;
                            {
                                Action<string, Texture2D> IconGUI = (s, t) =>
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUILayout.LabelField(s, GUILayout.Width(146));
                                    var rect = EditorGUILayout.GetControlRect();
                                    rect.width = rect.height;
                                    GUI.DrawTexture(rect, t);
                                    EditorGUILayout.EndHorizontal();
                                };
                                IconGUI("Humanoid / Normal", circleNormalTex);
                                IconGUI("Root", circle3NormalTex);
                                IconGUI("Non Humanoid", diamondNormalTex);
                                IconGUI("Humanoid Virtual", circleDotNormalTex);
                            }
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.EndVertical();
                    }
                }
                #endregion

                #region Preview
                if (guiPreviewFoldout)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    guiPreviewFoldout = EditorGUILayout.Foldout(guiPreviewFoldout, "Preview", true, guiStyleBoldFoldout);
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorPrefs.SetBool("VeryAnimation_Main_Preview", guiPreviewFoldout);
                    }
                    {
                        EditorGUILayout.Space();
                        if (GUILayout.Button(uEditorGUI.GetHelpIcon(), guiPreviewHelp ? guiStyleIconActiveButton : guiStyleIconButton, GUILayout.Width(19)))
                        {
                            guiPreviewHelp = !guiPreviewHelp;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    {
                        if (guiPreviewHelp)
                        {
                            EditorGUILayout.HelpBox(Language.GetText(Language.Help.HelpPreview), MessageType.Info);
                        }
                        else
                        {
                            GUILayout.Space(2f);
                        }

                        {
                            va.PreviewGUI();
                        }
                    }
                }
                #endregion
                #endregion
            }
            else
            {
                #region Error
                Release();
                #endregion
            }

#if Enable_Profiler
            Profiler.EndSample();
#endif
        }
        private void VersionInfoGUI()
        {
            GUILayout.FlexibleSpace();
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Version " + VeryAnimationWindow.Version, guiStyleMiddleLeftGreyMiniLabel, GUILayout.Width(100f));
#if UNITY_2019_1_OR_NEWER
                {
                    string packageText = "";
                    Action<string> AddPackageText = (text) =>
                    {
                        if (!string.IsNullOrEmpty(packageText))
                            packageText += ", ";
                        packageText += text;
                    };
#if VERYANIMATION_TIMELINE
                    AddPackageText("Timeline");
#endif
#if VERYANIMATION_ANIMATIONRIGGING
                    AddPackageText("Animation Rigging");
#endif
                    EditorGUILayout.LabelField(packageText, guiStyleMiddleRightGreyMiniLabel);
                }
#endif
                EditorGUILayout.EndHorizontal();
            }
        }

        private void OnPreSceneGUI(SceneView sceneView)
        {
            if (va.isEditError || !guiStyleReady) return;

            Event e = Event.current;

            #region AnimationWindowSampleAnimationOverride
            if (e.type == EventType.Layout)
            {
                va.AnimationWindowSampleAnimationOverride();
            }
            #endregion

            if (sceneView != SceneView.lastActiveSceneView) return;

            if (sceneView == EditorWindow.focusedWindow)
            {
                va.Commands();
            }
        }
        private void OnSceneGUI(SceneView sceneView)
        {
            if (va.isEditError || !guiStyleReady) return;
            if (sceneView != SceneView.lastActiveSceneView) return;

#if Enable_Profiler
            Profiler.BeginSample(string.Format("****VeryAnimationWindow.OnSceneGUI {0}", Event.current));
#endif

            Handles.matrix = Matrix4x4.identity;
            Event e = Event.current;
            var showGizmo = IsShowSceneGizmo();
            bool repaintScene = false;
            var controlID = GUIUtility.GetControlID(FocusType.Passive);

            #region Event
            switch (e.type)
            {
            case EventType.Layout:
                HandleUtility.AddDefaultControl(controlID);
                break;
            case EventType.KeyDown:
                if (focusedWindow is SceneView)
                    va.HotKeys();
                break;
            case EventType.KeyUp:
                break;
            case EventType.MouseMove:
                handleTransformUpdate = true;
                selectionRect.Reset();
                break;
            case EventType.MouseDown:
                if (IsShowSceneGizmo())
                {
                    if (e.clickCount == 1)
                    {
                        if (e.button == 0)
                        {
                            handleTransformUpdate = true;
                        }
                        if (!e.alt && e.button == 0)
                        {
                            selectionRect.Reset();
                            selectionRect.SetStart(e.mousePosition);
                            if (Shortcuts.IsKeyControl(e) || e.shift)
                            {
                                selectionRect.beforeSelection = va.selectionGameObjects != null ? va.selectionGameObjects.ToArray() : null;
                                selectionRect.virtualBeforeSelection = va.selectionHumanVirtualBones != null ? va.selectionHumanVirtualBones.ToArray() : null;
                                selectionRect.beforeAnimatorIKSelection = va.isHuman ? va.animatorIK.ikTargetSelect : null;
                                selectionRect.beforeOriginalIKSelection = va.originalIK.ikTargetSelect;
                            }
                        }
                    }
                    else if (e.clickCount == 2)
                    {
                        #region ChangeOtherObject
                        if (!EditorApplication.isPlaying && !va.uAw.GetLinkedWithTimeline())
                        {
                            if (forceChangeObject == null)
                            {
                                GameObject go = HandleUtility.PickGameObject(e.mousePosition, false);
                                if (go != null && va.BonesIndexOf(go) < 0)
                                {
                                    #region Check
                                    bool enable = false;
                                    {
                                        var t = go.transform;
                                        while (t != null)
                                        {
                                            if (t.gameObject.activeInHierarchy)
                                            {
                                                if (t.GetComponent<Animator>() != null || t.GetComponent<Animation>() != null)
                                                {
                                                    if (AnimationUtility.GetAnimationClips(t.gameObject).Length > 0)
                                                    {
                                                        enable = true;
                                                        go = t.gameObject;
                                                        break;
                                                    }
                                                }
                                            }
                                            t = t.parent;
                                        }
                                    }
                                    #endregion
                                    if (enable)
                                    {
                                        forceChangeObject = go;
                                    }
                                }
                            }
                        }
                        #endregion
                    }
                }
                SetRepaintGUI(RepaintGUI.Edit);
                repaintScene = true;
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl != 0)
                    handleTransformUpdate = false;
                else
                    handleTransformUpdate = true;
                if (selectionRect.Enable)
                {
                    if (GUIUtility.hotControl == 0 && IsShowSceneGizmo())
                    {
                        selectionRect.SetEnd(e.mousePosition);
                        #region Selection
                        {
                            var rect = selectionRect.rect;
                            #region Now
                            #region Bone
                            {
                                selectionRect.calcList.Clear();
                                for (int i = 0; i < va.bones.Length; i++)
                                {
                                    if (!va.IsShowBone(i) || (va.isHuman && i == 0)) continue;
                                    if (rect.Contains(HandleUtility.WorldToGUIPoint(va.editBones[i].transform.position)))
                                    {
                                        selectionRect.calcList.Add(va.bones[i]);
                                    }
                                }
                            }
                            #endregion
                            #region VirtualBone
                            {
                                selectionRect.virtualCalcList.Clear();
                                if (va.isHuman)
                                {
                                    if (va.IsShowBone(va.rootMotionBoneIndex))
                                    {
                                        if (rect.Contains(HandleUtility.WorldToGUIPoint(va.humanWorldRootPositionCache)))
                                        {
                                            selectionRect.calcList.Add(gameObject);
                                        }
                                    }
                                    for (int i = 0; i < VeryAnimation.HumanVirtualBones.Length; i++)
                                    {
                                        if (!va.IsShowVirtualBone((HumanBodyBones)i)) continue;

                                        if (rect.Contains(HandleUtility.WorldToGUIPoint(va.GetHumanVirtualBonePosition((HumanBodyBones)i))))
                                        {
                                            selectionRect.virtualCalcList.Add((HumanBodyBones)i);
                                        }
                                    }
                                }
                            }
                            #endregion
                            #region AnimatorIK
                            {
                                selectionRect.animatorIKCalcList.Clear();
                                if (va.isHuman && selectionRect.calcList.Count == 0 && selectionRect.virtualCalcList.Count == 0)
                                {
                                    for (int i = 0; i < va.animatorIK.ikData.Length; i++)
                                    {
                                        var data = va.animatorIK.ikData[i];
                                        if (!data.enable) continue;
                                        var guiPoint = HandleUtility.WorldToGUIPoint(data.worldPosition);
                                        if (!selectionRect.rect.Contains(guiPoint)) continue;
                                        selectionRect.animatorIKCalcList.Add((AnimatorIKCore.IKTarget)i);
                                    }
                                }
                            }
                            #endregion
                            #region OriginalIK
                            {
                                selectionRect.originalIKCalcList.Clear();
                                if (selectionRect.calcList.Count == 0 && selectionRect.virtualCalcList.Count == 0 && selectionRect.animatorIKCalcList.Count == 0)
                                {
                                    for (int i = 0; i < va.originalIK.ikData.Count; i++)
                                    {
                                        var data = va.originalIK.ikData[i];
                                        if (!data.enable) continue;
                                        var guiPoint = HandleUtility.WorldToGUIPoint(data.worldPosition);
                                        if (!selectionRect.rect.Contains(guiPoint)) continue;
                                        selectionRect.originalIKCalcList.Add(i);
                                    }
                                }
                            }
                            #endregion
                            #endregion
                            #region Before
                            #region Bone
                            if ((Shortcuts.IsKeyControl(e) || e.shift) && selectionRect.beforeSelection != null)
                            {
                                if (e.shift)
                                {
                                    foreach (var go in selectionRect.beforeSelection)
                                    {
                                        if (go == null) continue;
                                        if (!selectionRect.calcList.Contains(go))
                                            selectionRect.calcList.Add(go);
                                    }
                                }
                                else if (Shortcuts.IsKeyControl(e))
                                {
                                    foreach (var go in selectionRect.beforeSelection)
                                    {
                                        if (go == null) continue;
                                        Vector3 pos;
                                        if (va.isHuman && go == gameObject)
                                        {
                                            pos = va.humanWorldRootPositionCache;
                                        }
                                        else
                                        {
                                            var boneIndex = va.BonesIndexOf(go);
                                            if (boneIndex >= 0)
                                                pos = va.editBones[boneIndex].transform.position;
                                            else
                                                pos = go.transform.position;
                                        }
                                        if (!rect.Contains(HandleUtility.WorldToGUIPoint(pos)))
                                        {
                                            if (!selectionRect.calcList.Contains(go))
                                                selectionRect.calcList.Add(go.gameObject);
                                        }
                                        else
                                        {
                                            selectionRect.calcList.Remove(go.gameObject);
                                        }
                                    }
                                }
                            }
                            #endregion
                            #region VirtualBone
                            if (va.isHuman)
                            {
                                if ((Shortcuts.IsKeyControl(e) || e.shift) && selectionRect.virtualBeforeSelection != null)
                                {
                                    if (e.shift)
                                    {
                                        foreach (var go in selectionRect.virtualBeforeSelection)
                                        {
                                            if (!selectionRect.virtualCalcList.Contains(go))
                                                selectionRect.virtualCalcList.Add(go);
                                        }
                                    }
                                    else if (Shortcuts.IsKeyControl(e))
                                    {
                                        foreach (var go in selectionRect.virtualBeforeSelection)
                                        {
                                            if (!rect.Contains(HandleUtility.WorldToGUIPoint(va.GetHumanVirtualBonePosition(go))))
                                            {
                                                if (!selectionRect.virtualCalcList.Contains(go))
                                                    selectionRect.virtualCalcList.Add(go);
                                            }
                                            else
                                            {
                                                selectionRect.virtualCalcList.Remove(go);
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion
                            #region AnimatorIK
                            if (va.isHuman)
                            {
                                if ((Shortcuts.IsKeyControl(e) || e.shift) && selectionRect.beforeAnimatorIKSelection != null)
                                {
                                    if (e.shift)
                                    {
                                        foreach (var target in selectionRect.beforeAnimatorIKSelection)
                                        {
                                            if (!selectionRect.animatorIKCalcList.Contains(target))
                                                selectionRect.animatorIKCalcList.Add(target);
                                        }
                                    }
                                    else if (Shortcuts.IsKeyControl(e))
                                    {
                                        foreach (var target in selectionRect.beforeAnimatorIKSelection)
                                        {
                                            Vector3 pos = va.animatorIK.ikData[(int)target].worldPosition;
                                            if (!rect.Contains(HandleUtility.WorldToGUIPoint(pos)))
                                            {
                                                if (!selectionRect.animatorIKCalcList.Contains(target))
                                                    selectionRect.animatorIKCalcList.Add(target);
                                            }
                                            else
                                            {
                                                selectionRect.animatorIKCalcList.Remove(target);
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion
                            #region OriginalIK
                            {
                                if ((Shortcuts.IsKeyControl(e) || e.shift) && selectionRect.beforeOriginalIKSelection != null)
                                {
                                    if (e.shift)
                                    {
                                        foreach (var target in selectionRect.beforeOriginalIKSelection)
                                        {
                                            if (!selectionRect.originalIKCalcList.Contains(target))
                                                selectionRect.originalIKCalcList.Add(target);
                                        }
                                    }
                                    else if (Shortcuts.IsKeyControl(e))
                                    {
                                        foreach (var target in selectionRect.beforeOriginalIKSelection)
                                        {
                                            Vector3 pos = va.originalIK.ikData[target].worldPosition;
                                            if (!rect.Contains(HandleUtility.WorldToGUIPoint(pos)))
                                            {
                                                if (!selectionRect.originalIKCalcList.Contains(target))
                                                    selectionRect.originalIKCalcList.Add(target);
                                            }
                                            else
                                            {
                                                selectionRect.originalIKCalcList.Remove(target);
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion
                            #endregion
                            {
                                bool selectionChange = false;
                                #region IsChanged
                                {
                                    #region Bone
                                    {
                                        if (va.selectionGameObjects == null || va.selectionGameObjects.Count != selectionRect.calcList.Count)
                                            selectionChange = true;
                                        else if (va.selectionGameObjects != null)
                                        {
                                            for (int i = 0; i < va.selectionGameObjects.Count; i++)
                                            {
                                                if (va.selectionGameObjects[i] != selectionRect.calcList[i])
                                                {
                                                    selectionChange = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    #endregion
                                    #region VirtualBone
                                    if (va.isHuman)
                                    {
                                        if (va.selectionHumanVirtualBones == null || va.selectionHumanVirtualBones.Count != selectionRect.virtualCalcList.Count)
                                            selectionChange = true;
                                        else if (va.selectionHumanVirtualBones != null)
                                        {
                                            for (int i = 0; i < va.selectionHumanVirtualBones.Count; i++)
                                            {
                                                if (va.selectionHumanVirtualBones[i] != selectionRect.virtualCalcList[i])
                                                {
                                                    selectionChange = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    #endregion
                                    #region AnimatorIK
                                    if (va.isHuman)
                                    {
                                        if (va.animatorIK.ikTargetSelect == null || va.animatorIK.ikTargetSelect.Length != selectionRect.animatorIKCalcList.Count)
                                            selectionChange = true;
                                        else if (va.animatorIK.ikTargetSelect != null)
                                        {
                                            for (int i = 0; i < va.animatorIK.ikTargetSelect.Length; i++)
                                            {
                                                if (va.animatorIK.ikTargetSelect[i] != selectionRect.animatorIKCalcList[i])
                                                {
                                                    selectionChange = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    #endregion
                                    #region OriginalIK
                                    {
                                        if (va.originalIK.ikTargetSelect == null || va.originalIK.ikTargetSelect.Length != selectionRect.originalIKCalcList.Count)
                                            selectionChange = true;
                                        else if (va.originalIK.ikTargetSelect != null)
                                        {
                                            for (int i = 0; i < va.originalIK.ikTargetSelect.Length; i++)
                                            {
                                                if (va.originalIK.ikTargetSelect[i] != selectionRect.originalIKCalcList[i])
                                                {
                                                    selectionChange = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    #endregion
                                }
                                #endregion
                                if (selectionChange)
                                {
                                    va.SelectGameObjectMouseDrag(selectionRect.calcList.ToArray(), selectionRect.virtualCalcList.ToArray(), selectionRect.animatorIKCalcList.ToArray(), selectionRect.originalIKCalcList.ToArray());
                                    VeryAnimationControlWindow.ForceSelectionChange();
                                }
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        selectionRect.Reset();
                    }
                }
                if (e.button == 0 && GUIUtility.hotControl != 0)
                    SetRepaintGUI(RepaintGUI.Edit);
                repaintScene = true;
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl < 0)
                    GUIUtility.hotControl = 0;
                else if (GUIUtility.hotControl == 0 && selectionRect.Enable && selectionRect.distance < 10f)
                {
                    #region SelectMesh
                    {
                        GameObject go = null;
                        var animatorIKTarget = AnimatorIKCore.IKTarget.None;
                        int originalIKTarget = -1;
                        {
                            var worldRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                            float lengthSqMin = float.MaxValue;
                            foreach (var renderer in va.renderers)
                            {
                                if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                                    continue;
                                if (renderer is SkinnedMeshRenderer)
                                {
                                    #region SkinnedMeshRenderer
                                    var skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
                                    if (skinnedMeshRenderer.sharedMesh != null)
                                    {
                                        var worldToLocalMatrix = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one).inverse;
                                        var localRay = new Ray(worldToLocalMatrix.MultiplyPoint3x4(worldRay.origin), worldToLocalMatrix.MultiplyVector(worldRay.direction));
                                        Mesh mesh = new Mesh();
                                        mesh.hideFlags |= HideFlags.HideAndDontSave;
                                        skinnedMeshRenderer.BakeMesh(mesh);
                                        var vertices = mesh.vertices;
                                        BoneWeight[] boneWeights = null;
                                        Transform[] boneTransforms = null;
                                        var indices = mesh.triangles;
                                        for (int i = 0; i < indices.Length; i += 3)
                                        {
                                            Vector3 posP;
                                            if (!EditorCommon.Ray_Triangle(localRay,
                                                                            vertices[indices[i + 0]],
                                                                            vertices[indices[i + 1]],
                                                                            vertices[indices[i + 2]],
                                                                            out posP)) continue;
                                            var lengthSq = (posP - localRay.origin).sqrMagnitude;
                                            if (lengthSq > lengthSqMin)
                                                continue;

                                            if (boneWeights == null)
                                                boneWeights = skinnedMeshRenderer.sharedMesh.boneWeights;
                                            if (boneTransforms == null)
                                                boneTransforms = skinnedMeshRenderer.bones;

                                            Transform bone = null;
                                            {
                                                Dictionary<int, float> bonePoints = new Dictionary<int, float>();
                                                Action<int, float> AddBonePoint = (boneIndex, boneWeight) =>
                                                {
                                                    if (boneWeight <= 0f || boneIndex < 0 || boneIndex >= boneTransforms.Length)
                                                        return;
                                                    var t = boneTransforms[boneIndex];
                                                    var point = Vector2.Distance(HandleUtility.WorldToGUIPoint(t.position), e.mousePosition);
                                                    point = point + (point * (1f - boneWeight));
                                                    if (!bonePoints.ContainsKey(boneIndex))
                                                        bonePoints.Add(boneIndex, point);
                                                    else
                                                        bonePoints[boneIndex] = Mathf.Min(bonePoints[boneIndex], point);
                                                };
                                                for (int v = 0; v < 3; v++)
                                                {
                                                    var index = indices[i + v];
                                                    if (index >= boneWeights.Length) continue;
                                                    AddBonePoint(boneWeights[index].boneIndex0, boneWeights[index].weight0);
                                                    AddBonePoint(boneWeights[index].boneIndex1, boneWeights[index].weight1);
                                                    AddBonePoint(boneWeights[index].boneIndex2, boneWeights[index].weight2);
                                                    AddBonePoint(boneWeights[index].boneIndex3, boneWeights[index].weight3);
                                                }
                                                foreach (var pair in bonePoints.OrderBy((x) => x.Value))
                                                {
                                                    bone = boneTransforms[pair.Key];
                                                    break;
                                                }
                                            }
                                            if (bone != null)
                                            {
                                                int boneIndex = va.BonesIndexOf(bone.gameObject);
                                                var animatorIKTargetSub = AnimatorIKCore.IKTarget.None;
                                                int originalIKTargetSub = -1;
                                                while (boneIndex >= 0 && !va.IsShowBone(boneIndex))
                                                {
                                                    #region IKTarget
                                                    if (va.isHuman)
                                                    {
                                                        var target = va.animatorIK.IsIKBone(va.boneIndex2humanoidIndex[boneIndex]);
                                                        if (target != AnimatorIKCore.IKTarget.None)
                                                        {
                                                            animatorIKTargetSub = target;
                                                            originalIKTargetSub = -1;
                                                            break;
                                                        }
                                                    }
                                                    {
                                                        var target = va.originalIK.IsIKBone(boneIndex);
                                                        if (target >= 0)
                                                        {
                                                            animatorIKTargetSub = AnimatorIKCore.IKTarget.None;
                                                            originalIKTargetSub = target;
                                                            break;
                                                        }
                                                    }
                                                    #endregion
                                                    boneIndex = va.parentBoneIndexes[boneIndex];
                                                    if (boneIndex == va.rootMotionBoneIndex)
                                                    {
                                                        if (!va.IsShowBone(boneIndex))
                                                            boneIndex = -1;
                                                        break;
                                                    }
                                                }
                                                if (boneIndex >= 0)
                                                {
                                                    lengthSqMin = lengthSq;
                                                    go = va.bones[boneIndex];
                                                    animatorIKTarget = animatorIKTargetSub;
                                                    originalIKTarget = originalIKTargetSub;
                                                }
                                            }
                                        }
                                        Mesh.DestroyImmediate(mesh);
                                    }
                                    #endregion
                                }
                                else if (renderer is MeshRenderer)
                                {
                                    #region MeshRenderer
                                    var worldToLocalMatrix = renderer.transform.worldToLocalMatrix;
                                    var localRay = new Ray(worldToLocalMatrix.MultiplyPoint3x4(worldRay.origin), worldToLocalMatrix.MultiplyVector(worldRay.direction));
                                    var meshFilter = renderer.GetComponent<MeshFilter>();
                                    if (meshFilter != null && meshFilter.sharedMesh != null)
                                    {
                                        var vertices = meshFilter.sharedMesh.vertices;
                                        var indices = meshFilter.sharedMesh.triangles;
                                        for (int i = 0; i < indices.Length; i += 3)
                                        {
                                            Vector3 posP;
                                            if (!EditorCommon.Ray_Triangle(localRay,
                                                                            vertices[indices[i + 0]],
                                                                            vertices[indices[i + 1]],
                                                                            vertices[indices[i + 2]],
                                                                            out posP)) continue;
                                            posP = renderer.transform.localToWorldMatrix.MultiplyPoint3x4(posP);
                                            var lengthSq = (posP - worldRay.origin).sqrMagnitude;
                                            if (lengthSq > lengthSqMin)
                                                continue;

                                            var bone = renderer.transform;
                                            {
                                                int boneIndex = va.BonesIndexOf(bone.gameObject);
                                                var animatorIKTargetSub = AnimatorIKCore.IKTarget.None;
                                                int originalIKTargetSub = -1;
                                                while (boneIndex >= 0 && !va.IsShowBone(boneIndex))
                                                {
                                                    #region IKTarget
                                                    if (va.isHuman)
                                                    {
                                                        var target = va.animatorIK.IsIKBone(va.boneIndex2humanoidIndex[boneIndex]);
                                                        if (target != AnimatorIKCore.IKTarget.None)
                                                        {
                                                            animatorIKTargetSub = target;
                                                            originalIKTargetSub = -1;
                                                            break;
                                                        }
                                                    }
                                                    {
                                                        var target = va.originalIK.IsIKBone(boneIndex);
                                                        if (target >= 0)
                                                        {
                                                            animatorIKTargetSub = AnimatorIKCore.IKTarget.None;
                                                            originalIKTargetSub = target;
                                                            break;
                                                        }
                                                    }
                                                    #endregion
                                                    boneIndex = va.parentBoneIndexes[boneIndex];
                                                    if (boneIndex <= va.rootMotionBoneIndex)
                                                    {
                                                        if (!va.IsShowBone(boneIndex))
                                                            boneIndex = -1;
                                                        break;
                                                    }
                                                }
                                                if (boneIndex >= 0)
                                                {
                                                    lengthSqMin = lengthSq;
                                                    go = va.bones[boneIndex];
                                                    animatorIKTarget = animatorIKTargetSub;
                                                    originalIKTarget = originalIKTargetSub;
                                                }
                                            }
                                        }
                                    }
                                    #endregion
                                }
                            }
                        }
                        if (animatorIKTarget != AnimatorIKCore.IKTarget.None)
                        {
                            va.SelectAnimatorIKTargetPlusKey(animatorIKTarget);
                        }
                        else if (originalIKTarget >= 0)
                        {
                            va.SelectOriginalIKTargetPlusKey(originalIKTarget);
                        }
                        else
                        {
                            va.SelectGameObjectPlusKey(go);
                        }
                    }
                    #endregion
                }
                if (e.button == 0)
                {
                    handleTransformUpdate = true;
                }
                selectionRect.Reset();
                SetRepaintGUI(RepaintGUI.Edit);
                repaintScene = true;
                break;
            }
            #endregion

            #region RootTrail
            if (editorSettings.settingExtraRootTrail && showGizmo)
            {
                if (e.type == EventType.Repaint)
                {
                    DrawRootTrail();
                }
            }
            #endregion

            #region SelectionRect
            if (selectionRect.Enable && selectionRect.rect.width > 0f && selectionRect.rect.height > 0f)
            {
                Handles.BeginGUI();
                GUI.Box(selectionRect.rect, "", "SelectionRect");
                Handles.EndGUI();
            }
            #endregion

            #region Tools
            if (showGizmo)
            {
                #region Handle
                {
                    bool genericHandle = false;
                    if (va.isHuman)
                    {
                        #region Humanoid
                        var humanoidIndex = va.SelectionGameObjectHumanoidIndex();
                        if (va.selectionActiveBone == va.rootMotionBoneIndex)
                        {
                            #region Root
                            if (handleTransformUpdate)
                            {
                                handlePosition = va.humanWorldRootPositionCache;
                                handleRotation = Tools.pivotRotation == PivotRotation.Local ? va.humanWorldRootRotationCache : Tools.handleRotation;
                                handlePositionSave = handlePosition;
                                handleRotationSave = handleRotation;
                            }
                            va.EnableCustomTools(Tool.None);
                            var currentTool = va.CurrentTool();
                            if (currentTool == Tool.Move)
                            {
                                EditorGUI.BeginChangeCheck();
                                var position = Handles.PositionHandle(handlePosition, handleRotation);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    va.SetAnimationValueAnimatorRootT(va.GetHumanLocalRootPosition(position));
                                    handlePosition = position;
                                }
                            }
                            else if (currentTool == Tool.Rotate)
                            {
                                EditorGUI.BeginChangeCheck();
                                var rotation = Handles.RotationHandle(handleRotation, handlePosition);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    if (Tools.pivotRotation == PivotRotation.Local)
                                    {
                                        va.SetAnimationValueAnimatorRootQ(va.GetHumanLocalRootRotation(rotation));
                                    }
                                    else
                                    {
                                        float angle;
                                        Vector3 axis;
                                        (Quaternion.Inverse(handleRotation) * rotation).ToAngleAxis(out angle, out axis);
                                        var bodyRotation = va.humanWorldRootRotationCache;
                                        bodyRotation = bodyRotation * Quaternion.Inverse(bodyRotation) * Quaternion.AngleAxis(angle, handleRotation * axis) * bodyRotation;
                                        va.SetAnimationValueAnimatorRootQ(va.GetHumanLocalRootRotation(bodyRotation));
                                    }
                                    Tools.handleRotation = handleRotation = rotation;
                                }
                            }
                            #endregion
                        }
                        else if (humanoidIndex == HumanBodyBones.Hips)
                        {
                            va.EnableCustomTools(Tool.None);
                        }
                        else if (humanoidIndex > HumanBodyBones.Hips)
                        {
                            #region Muscle
                            va.EnableCustomTools(Tool.None);
                            var currentTool = va.CurrentTool();
                            #region handleTransformUpdate
                            if (handleTransformUpdate)
                            {
                                if (va.editHumanoidBones[(int)humanoidIndex] != null)
                                {
                                    handlePosition = va.editHumanoidBones[(int)humanoidIndex].transform.position;
                                    if (Tools.pivotRotation == PivotRotation.Local)
                                    {
                                        handleRotation = va.editHumanoidBones[(int)humanoidIndex].transform.rotation;
                                    }
                                    else
                                    {
                                        handleRotation = Tools.handleRotation;
                                    }
                                    if (Tools.pivotMode == PivotMode.Center)
                                    {
                                        handlePosition = va.GetSelectionBounds().center;
                                        if (Tools.pivotRotation == PivotRotation.Local)
                                        {
                                            handleRotation = va.GetSelectionBoundsRotation();
                                        }
                                    }
                                }
                                else
                                {
                                    handlePosition = va.GetHumanVirtualBonePosition(humanoidIndex);
                                    handleRotation = Tools.pivotRotation == PivotRotation.Local ? va.GetHumanVirtualBoneRotation(humanoidIndex) : Tools.handleRotation;
                                }
                                handlePositionSave = handlePosition;
                                handleRotationSave = handleRotation;
                            }
                            #endregion
                            #region CenterLine
                            if (Tools.pivotMode == PivotMode.Center && (currentTool == Tool.Move || currentTool == Tool.Rotate))
                            {
                                var saveColor = Handles.color;
                                Handles.color = editorSettings.settingBoneActiveColor;
                                Vector3 pos2;
                                if (va.editHumanoidBones[(int)humanoidIndex] != null)
                                    pos2 = va.editHumanoidBones[(int)humanoidIndex].transform.position;
                                else
                                    pos2 = va.GetHumanVirtualBonePosition(humanoidIndex);
                                Handles.DrawLine(handlePositionSave, pos2);
                                Handles.color = saveColor;
                            }
                            #endregion
                            Action<Action<HumanBodyBones, Quaternion>> SetCenterRotationAction = (action) =>
                            {
                                var normal = handleRotationSave * Vector3.forward;
                                var vecBase = handleRotationSave * Vector3.right;
                                var hiList = va.SelectionGameObjectsHumanoidIndex();
                                if (hiList.Count > 1)
                                {
                                    foreach (var hi in hiList)
                                    {
                                        var boneIndex = va.humanoidIndex2boneIndex[(int)hi];
                                        if (boneIndex < 0) continue;
                                        Quaternion rotation = Quaternion.identity;
                                        {
                                            var vecSub = va.editBones[boneIndex].transform.position - handlePositionSave;
                                            vecSub.Normalize();
                                            if (vecSub.sqrMagnitude > 0f)
                                            {
                                                rotation = Quaternion.AngleAxis(Vector3.SignedAngle(vecBase, vecSub, normal), normal);
                                            }
                                        }
                                        action(hi, rotation);
                                    }
                                }
                                else if (hiList.Count == 1)
                                {
                                    var boneIndex = va.humanoidIndex2boneIndex[(int)hiList[0]];
                                    if (boneIndex >= 0)
                                    {
                                        action(hiList[0], Quaternion.identity);
                                    }
                                }
                            };
                            if (currentTool == Tool.Move)
                            {
                                #region Move
                                EditorGUI.BeginChangeCheck();
                                var position = Handles.PositionHandle(handlePosition, handleRotation);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Action<HumanBodyBones, Vector3> ChangeTDOF = (hi, move) =>
                                    {
                                        if (va.editHumanoidBones[(int)hi] == null)
                                            return;
                                        if (VeryAnimation.HumanBonesAnimatorTDOFIndex[(int)hi] == null)
                                            return;
                                        Quaternion rotation;
                                        {
                                            var parentHi = VeryAnimation.HumanBonesAnimatorTDOFIndex[(int)hi].parent;
                                            if (va.editHumanoidBones[(int)parentHi] == null)
                                                return;
                                            rotation = va.editHumanoidBones[(int)parentHi].transform.rotation * va.GetAvatarPostRotation(parentHi);
                                        }
                                        var localAdd = Quaternion.Inverse(rotation) * move;
                                        for (int i = 0; i < 3; i++) //Delete tiny value
                                        {
                                            if (Mathf.Abs(localAdd[i]) < 0.0001f)
                                                localAdd[i] = 0f;
                                        }
                                        {
                                            var mat = (va.editGameObject.transform.worldToLocalMatrix * va.editHumanoidBones[(int)humanoidIndex].transform.localToWorldMatrix).inverse;
                                            Vector3 lposition, lscale;
                                            Quaternion lrotation;
                                            EditorCommon.GetTRS(mat, out lposition, out lrotation, out lscale);
                                            localAdd = Vector3.Scale(localAdd, lscale);
                                        }
                                        if (va.editAnimator.humanScale > 0f)
                                            localAdd *= 1f / va.editAnimator.humanScale;
                                        else
                                            localAdd = Vector3.zero;
                                        va.SetAnimationValueAnimatorTDOF(VeryAnimation.HumanBonesAnimatorTDOFIndex[(int)hi].index, va.GetAnimationValueAnimatorTDOF(VeryAnimation.HumanBonesAnimatorTDOFIndex[(int)hi].index) + localAdd);
                                    };
                                    var offset = position - handlePosition;
                                    offset *= va.GetSelectionSuppressPowerRate();
                                    if (Tools.pivotMode == PivotMode.Center)
                                    {
                                        #region Center
                                        SetCenterRotationAction((hi, different) =>
                                        {
                                            ChangeTDOF(hi, different * offset);
                                        });
                                        #endregion
                                    }
                                    else
                                    {
                                        #region Pivot
                                        foreach (var hi in va.SelectionGameObjectsHumanoidIndex())
                                        {
                                            if (va.editHumanoidBones[(int)hi] == null)
                                                continue;
                                            ChangeTDOF(hi, offset);
                                        }
                                        #endregion
                                    }
                                    handlePosition = position;
                                }
                                #endregion
                            }
                            else if (currentTool == Tool.Rotate)
                            {
                                #region Rotate
                                Action<Quaternion> CalcRotation = (afterRot) =>
                                {
                                    {
                                        float angle;
                                        Vector3 axis;
                                        (Quaternion.Inverse(handleRotation) * afterRot).ToAngleAxis(out angle, out axis);
                                        angle *= va.GetSelectionSuppressPowerRate();
                                        if (Tools.pivotMode == PivotMode.Center)
                                        {
                                            #region Center
                                            SetCenterRotationAction((hi, different) =>
                                            {
                                                var handleRotationSub = different * handleRotation;
                                                va.editHumanoidBones[(int)hi].transform.Rotate(handleRotationSub * axis, angle, Space.World);
                                            });
                                            #endregion
                                        }
                                        else
                                        {
                                            #region Pivot
                                            foreach (var hi in va.SelectionGameObjectsHumanoidIndex())
                                            {
                                                if (va.editHumanoidBones[(int)hi] == null)
                                                    continue;
                                                va.editHumanoidBones[(int)hi].transform.Rotate(handleRotation * axis, angle, Space.World);
                                            }
                                            #endregion
                                        }
                                    }
                                    HumanPose hpAfter = new HumanPose();
                                    va.GetEditGameObjectHumanPose(ref hpAfter);
                                    if (va.editHumanoidBones[(int)HumanBodyBones.Neck] == null)
                                    {
                                        if (va.IsSelectionGameObjectsHumanoidIndexContains(HumanBodyBones.Head))
                                        {
                                            for (int dof = 0; dof < 3; dof++)
                                            {
                                                var muscleIndex = HumanTrait.MuscleFromBone((int)HumanBodyBones.Neck, dof);
                                                if (muscleIndex < 0) continue;
                                                va.SetAnimationValueAnimatorMuscle(muscleIndex, 0f);
                                            }
                                        }
                                    }
                                    foreach (var muscleIndex in va.SelectionGameObjectsMuscleIndex(-1))
                                    {
                                        var hi = (HumanBodyBones)HumanTrait.BoneFromMuscle(muscleIndex);
                                        if (va.editHumanoidBones[(int)hi] == null)
                                            continue;
                                        var muscle = hpAfter.muscles[muscleIndex];
                                        if (va.clampMuscle)
                                            muscle = Mathf.Clamp(muscle, -1f, 1f);
                                        va.SetAnimationValueAnimatorMuscle(muscleIndex, muscle);
                                    }
                                };
                                {
                                    if (muscleRotationHandleIds == null || muscleRotationHandleIds.Length != 3)
                                        muscleRotationHandleIds = new int[3];
                                    for (int i = 0; i < muscleRotationHandleIds.Length; i++)
                                        muscleRotationHandleIds[i] = -1;
                                }
                                if (Tools.pivotRotation == PivotRotation.Local && Tools.pivotMode == PivotMode.Pivot)
                                {
                                    #region LocalPivot
                                    Color saveColor = Handles.color;
                                    float handleSize = HandleUtility.GetHandleSize(handlePosition);
                                    {
                                        Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                                        EditorGUI.BeginChangeCheck();
                                        var rotation = Handles.FreeRotateHandle(handleRotation, handlePosition, handleSize);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            CalcRotation(rotation);
                                            Tools.handleRotation = handleRotation = rotation;
                                        }
                                    }
                                    {
                                        EditorGUI.BeginChangeCheck();
                                        int rotDofMode = -1;
                                        float rotDofDist = 0f;
                                        Quaternion rotDofHandleRotation = Quaternion.identity;
                                        Quaternion rotDofAfterRotation = Quaternion.identity;
                                        #region MuscleRotationHandle
                                        Transform t = null;
                                        if (va.selectionActiveBone >= 0)
                                            t = va.editBones[va.selectionActiveBone].transform;
                                        Quaternion preRotation = va.GetAvatarPreRotation(humanoidIndex);
                                        var snapRotation = uSnapSettings.rotation;
                                        {
                                            if (HumanTrait.MuscleFromBone((int)humanoidIndex, 0) >= 0)
                                            {
                                                Handles.color = Handles.xAxisColor;
                                                EditorGUI.BeginChangeCheck();
                                                Quaternion hRotation;
                                                if (t != null)
                                                    hRotation = va.uAvatar.GetZYPostQ(va.editAnimator.avatar, (int)humanoidIndex, t.parent.rotation, t.rotation);
                                                else
                                                    hRotation = va.GetHumanVirtualBoneRotation(humanoidIndex);
                                                var rotDofDistSave = uDisc.GetRotationDist();
                                                var rotation = Handles.Disc(hRotation, handlePosition, hRotation * Vector3.right, handleSize, true, snapRotation);
                                                muscleRotationHandleIds[0] = uEditorGUIUtility.GetLastControlID();
                                                if (EditorGUI.EndChangeCheck())
                                                {
                                                    rotDofMode = 0;
                                                    rotDofDist = uDisc.GetRotationDist() - rotDofDistSave;
                                                    rotDofHandleRotation = hRotation;
                                                    rotDofAfterRotation = rotation;
                                                }
                                            }
                                            if (HumanTrait.MuscleFromBone((int)humanoidIndex, 1) >= 0)
                                            {
                                                Handles.color = Handles.yAxisColor;
                                                EditorGUI.BeginChangeCheck();
                                                Quaternion hRotation;
                                                if (t != null)
                                                    hRotation = t.parent.rotation * preRotation;
                                                else
                                                    hRotation = va.GetHumanVirtualBoneParentRotation(humanoidIndex);
                                                var rotDofDistSave = uDisc.GetRotationDist();
                                                var rotation = Handles.Disc(hRotation, handlePosition, hRotation * Vector3.up, handleSize, true, snapRotation);
                                                muscleRotationHandleIds[1] = uEditorGUIUtility.GetLastControlID();
                                                if (EditorGUI.EndChangeCheck())
                                                {
                                                    rotDofMode = 1;
                                                    rotDofDist = uDisc.GetRotationDist() - rotDofDistSave;
                                                    rotDofHandleRotation = hRotation;
                                                    rotDofAfterRotation = rotation;
                                                }
                                            }
                                            if (HumanTrait.MuscleFromBone((int)humanoidIndex, 2) >= 0)
                                            {
                                                Handles.color = Handles.zAxisColor;
                                                EditorGUI.BeginChangeCheck();
                                                Quaternion hRotation;
                                                if (t != null)
                                                    hRotation = t.parent.rotation * preRotation;
                                                else
                                                    hRotation = va.GetHumanVirtualBoneParentRotation(humanoidIndex);
                                                var rotDofDistSave = uDisc.GetRotationDist();
                                                var rotation = Handles.Disc(hRotation, handlePosition, hRotation * Vector3.forward, handleSize, true, snapRotation);
                                                muscleRotationHandleIds[2] = uEditorGUIUtility.GetLastControlID();
                                                if (EditorGUI.EndChangeCheck())
                                                {
                                                    rotDofMode = 2;
                                                    rotDofDist = uDisc.GetRotationDist() - rotDofDistSave;
                                                    rotDofHandleRotation = hRotation;
                                                    rotDofAfterRotation = rotation;
                                                }
                                            }
                                        }
                                        #endregion
                                        rotDofDist *= va.GetSelectionSuppressPowerRate();
                                        if (rotDofMode >= 0 && rotDofMode <= 2)
                                        {
                                            foreach (var hi in va.SelectionGameObjectsHumanoidIndex())
                                            {
                                                var muscleIndex = HumanTrait.MuscleFromBone((int)hi, rotDofMode);
                                                var muscle = va.GetAnimationValueAnimatorMuscle(muscleIndex);
                                                {
                                                    var muscleLimit = va.humanoidMuscleLimit[(int)hi];
                                                    var value = muscleLimit.max[rotDofMode] - muscleLimit.min[rotDofMode];
                                                    if (value > 0f)
                                                    {
                                                        var add = rotDofDist / (value / 2f);
                                                        Vector3 limitSign;
                                                        if (va.editHumanoidBones[(int)hi] != null)
                                                            limitSign = va.GetLimitSign(hi);
                                                        else
                                                            limitSign = va.GetHumanVirtualBoneLimitSign(hi);
                                                        muscle -= add * limitSign[rotDofMode];
                                                    }
                                                }
                                                if (va.clampMuscle)
                                                    muscle = Mathf.Clamp(muscle, -1f, 1f);
                                                va.SetAnimationValueAnimatorMuscle(muscleIndex, muscle);
                                            }
                                        }
                                    }
                                    if (va.editHumanoidBones[(int)humanoidIndex] != null)
                                    {
                                        Handles.color = Handles.centerColor;
                                        EditorGUI.BeginChangeCheck();
                                        var rotation = Handles.Disc(handleRotation, handlePosition, Camera.current.transform.forward, handleSize * 1.1f, false, 0f);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            CalcRotation(rotation);
                                            Tools.handleRotation = handleRotation = rotation;
                                        }
                                    }
                                    Handles.color = saveColor;
                                    #endregion
                                }
                                else
                                {
                                    #region Other
                                    if (va.editHumanoidBones[(int)humanoidIndex] != null)
                                    {
                                        EditorGUI.BeginChangeCheck();
                                        var rotation = Handles.RotationHandle(handleRotation, handlePosition);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            CalcRotation(rotation);
                                            Tools.handleRotation = handleRotation = rotation;
                                        }
                                    }
                                    #endregion
                                }

                                #endregion
                            }
                            #endregion
                        }
                        else if (va.selectionActiveBone >= 0)
                        {
                            genericHandle = true;
                        }
                        else
                        {
                            va.EnableCustomTools(Tool.None);
                        }
                        #endregion
                    }
                    else if (va.selectionActiveBone >= 0)
                    {
                        #region Generic
                        genericHandle = true;
                        #endregion
                    }
                    else
                    {
                        va.EnableCustomTools(Tool.None);
                    }
                    if (genericHandle && va.selectionActiveBone >= 0)
                    {
                        #region GenericHandle
                        va.EnableCustomTools(Tool.None);
                        var currentTool = va.CurrentTool();
                        #region handleTransformUpdate
                        if (handleTransformUpdate)
                        {
                            if (Tools.pivotMode == PivotMode.Pivot)
                            {
                                handlePosition = va.editBones[va.selectionActiveBone].transform.position;
                                if (Tools.pivotRotation == PivotRotation.Local)
                                    handleRotation = va.editBones[va.selectionActiveBone].transform.rotation;
                                else
                                    handleRotation = Tools.handleRotation;
                            }
                            else if (Tools.pivotMode == PivotMode.Center)
                            {
                                handlePosition = va.GetSelectionBounds().center;
                                if (Tools.pivotRotation == PivotRotation.Local)
                                    handleRotation = va.GetSelectionBoundsRotation();
                                else
                                    handleRotation = Tools.handleRotation;
                            }
                            handleScale = Vector3.one;
                            handlePositionSave = handlePosition;
                            handleRotationSave = handleRotation;
                        }
                        #endregion
                        #region CenterLine
                        if (Tools.pivotMode == PivotMode.Center && (currentTool == Tool.Move || currentTool == Tool.Rotate))
                        {
                            var saveColor = Handles.color;
                            Handles.color = editorSettings.settingBoneActiveColor;
                            Handles.DrawLine(handlePositionSave, va.editBones[va.selectionActiveBone].transform.position);
                            Handles.color = saveColor;
                        }
                        #endregion
                        Action<Action<int, Quaternion>> SetCenterRotationAction = (action) =>
                        {
                            var normal = handleRotationSave * Vector3.forward;
                            var vecBase = handleRotationSave * Vector3.right;
                            if (va.selectionBones.Count > 1)
                            {
                                foreach (var boneIndex in va.SelectionGameObjectsOtherHumanoidBoneIndex())
                                {
                                    Quaternion rotation = Quaternion.identity;
                                    {
                                        var vecSub = va.editBones[boneIndex].transform.position - handlePositionSave;
                                        vecSub.Normalize();
                                        if (vecSub.sqrMagnitude > 0f)
                                        {
                                            rotation = Quaternion.AngleAxis(Vector3.SignedAngle(vecBase, vecSub, normal), normal);
                                        }
                                    }
                                    action(boneIndex, rotation);
                                }
                            }
                            else if (va.selectionBones.Count == 1)
                            {
                                action(va.selectionBones[0], Quaternion.identity);
                            }
                        };
                        if (currentTool == Tool.Move)
                        {
                            #region Move
                            EditorGUI.BeginChangeCheck();
                            var position = Handles.PositionHandle(handlePosition, handleRotation);
                            if (EditorGUI.EndChangeCheck())
                            {
                                var offset = position - handlePosition;
                                offset *= va.GetSelectionSuppressPowerRate();
#if UNITY_2018_3_OR_NEWER
                                {
                                    va.SampleAnimationLegacy(va.currentTime, VeryAnimation.EditObjectFlag.Edit);
                                }
#endif
                                if (Tools.pivotMode == PivotMode.Center)
                                {
                                    #region Center
                                    SetCenterRotationAction((boneIndex, different) =>
                                    {
                                        var t = va.editBones[boneIndex].transform;
                                        var save = t.localPosition;
                                        if (va.uAw.GetLinkedWithTimeline() && boneIndex == 0)
                                        {
#if VERYANIMATION_TIMELINE
                                            Vector3 offsetPosition;
                                            Quaternion offsetRotation;
                                            va.uAw.GetTimelineRootMotionOffsets(out offsetPosition, out offsetRotation);
                                            t.Translate(Quaternion.Inverse(offsetRotation) * (different * offset), Space.World);
                                            var localPosition = va.GetAnimationValueTransformPosition(boneIndex) + (t.localPosition - save);
                                            va.SetAnimationValueTransformPosition(boneIndex, localPosition);
#else
                                            Assert.IsTrue(false);
#endif
                                        }
                                        else
                                        {
                                            t.Translate(different * offset, Space.World);
                                            va.SetAnimationValueTransformPosition(boneIndex, t.localPosition);
                                        }
                                        t.localPosition = save;
                                    });
                                    #endregion
                                }
                                else
                                {
                                    #region Pivot
                                    foreach (var boneIndex in va.SelectionGameObjectsOtherHumanoidBoneIndex())
                                    {
                                        var t = va.editBones[boneIndex].transform;
                                        var save = t.localPosition;
                                        if (va.uAw.GetLinkedWithTimeline() && boneIndex == 0)
                                        {
#if VERYANIMATION_TIMELINE
                                            Vector3 offsetPosition;
                                            Quaternion offsetRotation;
                                            va.uAw.GetTimelineRootMotionOffsets(out offsetPosition, out offsetRotation);
                                            t.Translate(Quaternion.Inverse(offsetRotation) * offset, Space.World);
                                            var localPosition = va.GetAnimationValueTransformPosition(boneIndex) + (t.localPosition - save);
                                            va.SetAnimationValueTransformPosition(boneIndex, localPosition);
#else
                                            Assert.IsTrue(false);
#endif
                                        }
                                        else
                                        {
                                            t.Translate(offset, Space.World);
                                            va.SetAnimationValueTransformPosition(boneIndex, t.localPosition);
                                        }
                                        t.localPosition = save;
                                    }
                                    #endregion
                                }
                                handlePosition = position;
                            }
                            #endregion
                        }
                        else if (currentTool == Tool.Rotate)
                        {
                            #region Rotate
                            EditorGUI.BeginChangeCheck();
                            var rotation = Handles.RotationHandle(handleRotation, handlePosition);
                            if (EditorGUI.EndChangeCheck())
                            {
                                var offset = Quaternion.Inverse(handleRotation) * rotation;
                                float angle;
                                Vector3 axis;
                                offset.ToAngleAxis(out angle, out axis);
                                angle *= va.GetSelectionSuppressPowerRate();
#if UNITY_2018_3_OR_NEWER
                                {
                                    va.SampleAnimationLegacy(va.currentTime, VeryAnimation.EditObjectFlag.Edit);
                                }
#endif
                                if (Tools.pivotMode == PivotMode.Center)
                                {
                                    #region Center
                                    SetCenterRotationAction((boneIndex, different) =>
                                    {
                                        var t = va.editBones[boneIndex].transform;
                                        var save = t.localRotation;
                                        var handleRotationSub = different * handleRotation;
                                        if (va.uAw.GetLinkedWithTimeline() && boneIndex == 0)
                                        {
                                            t.Rotate(handleRotationSub * axis, angle, Space.World);
                                            var localRotation = va.GetAnimationValueTransformRotation(boneIndex) * (Quaternion.Inverse(save) * t.localRotation);
                                            va.SetAnimationValueTransformRotation(boneIndex, localRotation);
                                        }
                                        else
                                        {
                                            t.Rotate(handleRotationSub * axis, angle, Space.World);
                                            va.SetAnimationValueTransformRotation(boneIndex, t.localRotation);
                                        }
                                        t.localRotation = save;
                                    });
                                    #endregion
                                }
                                else
                                {
                                    #region Pivot
                                    foreach (var boneIndex in va.SelectionGameObjectsOtherHumanoidBoneIndex())
                                    {
                                        var t = va.editBones[boneIndex].transform;
                                        var save = t.localRotation;
                                        if (va.uAw.GetLinkedWithTimeline() && boneIndex == 0)
                                        {
                                            t.Rotate(handleRotation * axis, angle, Space.World);
                                            var localRotation = va.GetAnimationValueTransformRotation(boneIndex) * (Quaternion.Inverse(save) * t.localRotation);
                                            va.SetAnimationValueTransformRotation(boneIndex, localRotation);
                                        }
                                        else
                                        {
                                            t.Rotate(handleRotation * axis, angle, Space.World);
                                            va.SetAnimationValueTransformRotation(boneIndex, t.localRotation);
                                        }
                                        t.localRotation = save;
                                    }
                                    #endregion
                                }
                                Tools.handleRotation = handleRotation = rotation;
                            }
                            #endregion
                        }
                        else if (currentTool == Tool.Scale)
                        {
                            #region Scale
                            if (Tools.pivotRotation == PivotRotation.Local)
                            {
                                EditorGUI.BeginChangeCheck();
                                var scale = Handles.ScaleHandle(handleScale, handlePosition, handleRotation, HandleUtility.GetHandleSize(handlePosition));
                                if (EditorGUI.EndChangeCheck())
                                {
                                    var offset = scale - handleScale;
                                    offset *= va.GetSelectionSuppressPowerRate();
                                    foreach (var boneIndex in va.SelectionGameObjectsOtherHumanoidBoneIndex())
                                    {
                                        var t = va.editBones[boneIndex].transform;
                                        va.SetAnimationValueTransformScale(boneIndex, t.localScale + offset);
                                    }
                                    handleScale = scale;
                                }
                            }
                            #endregion
                        }
                        #endregion
                    }
                }
                #endregion
                #region MotionHandle
                if (va.selectionMotionTool)
                {
                    if (handleTransformUpdate)
                    {
                        handlePosition = va.animatorWorldRootPositionCache;
                        handleRotation = Tools.pivotRotation == PivotRotation.Local ? va.animatorWorldRootRotationCache : Tools.handleRotation;
                        handlePositionSave = handlePosition;
                        handleRotationSave = handleRotation;
                    }
                    va.EnableCustomTools(Tool.None);
                    var currentTool = va.CurrentTool();
                    if (currentTool == Tool.Move)
                    {
                        EditorGUI.BeginChangeCheck();
                        var position = Handles.PositionHandle(handlePosition, handleRotation);
                        if (EditorGUI.EndChangeCheck())
                        {
                            va.SetAnimationValueAnimatorMotionT(va.GetAnimatorLocalMotionPosition(position));
                            handlePosition = position;
                        }
                    }
                    else if (currentTool == Tool.Rotate)
                    {
                        EditorGUI.BeginChangeCheck();
                        var rotation = Handles.RotationHandle(handleRotation, handlePosition);
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (Tools.pivotRotation == PivotRotation.Local)
                            {
                                va.SetAnimationValueAnimatorMotionQ(va.GetAnimatorLocalMotionRotation(rotation));
                            }
                            else
                            {
                                float angle;
                                Vector3 axis;
                                (Quaternion.Inverse(handleRotation) * rotation).ToAngleAxis(out angle, out axis);
                                var bodyRotation = va.animatorWorldRootRotationCache;
                                bodyRotation = bodyRotation * Quaternion.Inverse(bodyRotation) * Quaternion.AngleAxis(angle, handleRotation * axis) * bodyRotation;
                                va.SetAnimationValueAnimatorMotionQ(va.GetAnimatorLocalMotionRotation(bodyRotation));
                            }
                            Tools.handleRotation = handleRotation = rotation;
                        }
                    }
                }
                #endregion
                #region IKHandle
                va.IKHandleGUI();
                va.IKTargetGUI();
                #endregion

                if (e.type == EventType.Repaint)
                {
                    #region Skeleton
                    if (editorSettings.settingsSkeletonFKType != EditorSettings.SkeletonType.None && va.skeletonFKShowBoneList != null)
                    {
                        DrawSkeleton();
                    }
                    #endregion

                    #region MuscleLimit
                    if (va.isHuman && editorSettings.settingBoneMuscleLimit && va.clampMuscle &&
                        Tools.pivotMode == PivotMode.Pivot &&
                        muscleRotationHandleIds != null && muscleRotationHandleIds.Length == 3 &&
                        muscleRotationSliderIds != null && muscleRotationSliderIds.Length == 3)
                    {
                        var humanoidIndex = (int)va.SelectionGameObjectHumanoidIndex();
                        if (humanoidIndex >= 0 && va.CurrentTool() == Tool.Rotate)
                        {
                            Transform t = null;
                            if (va.editHumanoidBones[humanoidIndex] != null)
                                t = va.editHumanoidBones[humanoidIndex].transform;
                            int index1 = HumanTrait.MuscleFromBone(humanoidIndex, 0);
                            int index2 = HumanTrait.MuscleFromBone(humanoidIndex, 1);
                            int index3 = HumanTrait.MuscleFromBone(humanoidIndex, 2);
                            float axisLength = HandleUtility.GetHandleSize(handlePosition);
                            Quaternion quaternion1, quaternion2;
                            {
                                Quaternion preRotation = va.GetAvatarPreRotation((HumanBodyBones)humanoidIndex);
                                Quaternion postRotation = va.GetAvatarPostRotation((HumanBodyBones)humanoidIndex);
                                quaternion1 = t != null ? t.parent.rotation * preRotation : va.GetHumanVirtualBoneParentRotation((HumanBodyBones)humanoidIndex);
                                quaternion2 = t != null ? t.rotation * postRotation : va.GetHumanVirtualBoneRotation((HumanBodyBones)humanoidIndex);
                            }
                            Quaternion zyRoll = va.GetZYRoll((HumanBodyBones)humanoidIndex);
                            Vector3 limitSign = t != null ? va.GetLimitSign((HumanBodyBones)humanoidIndex) : va.GetHumanVirtualBoneLimitSign((HumanBodyBones)humanoidIndex);
                            //X
                            Vector3 normalX = Vector3.zero, fromX = Vector3.zero, lineX = Vector3.zero;
                            float angleX = 0f, radiusX = 0f;
                            if (index1 != -1)
                            {
                                Quaternion zyPostQ = t != null ? va.GetZYPostQ((HumanBodyBones)humanoidIndex, t.parent.rotation, t.rotation) : quaternion1;
                                float angle = va.humanoidMuscleLimit[humanoidIndex].min.x;
                                float num = va.humanoidMuscleLimit[humanoidIndex].max.x;
                                float length = axisLength;
                                if (va.musclePropertyName.Names[index1].StartsWith("Left") || va.musclePropertyName.Names[index1].StartsWith("Right")) //why?
                                {
                                    angle *= 0.5f;
                                    num *= 0.5f;
                                }
                                Vector3 vector3_3 = zyPostQ * Vector3.forward;
                                Vector3 vector3_5 = quaternion2 * Vector3.right * limitSign.x;
                                Vector3 from = Quaternion.AngleAxis(angle, vector3_5) * vector3_3;

                                normalX = vector3_5;
                                fromX = from;
                                angleX = num - angle;
                                radiusX = length;
                                Vector3 lineVec = Quaternion.AngleAxis(Mathf.Lerp(angle, num, (va.GetAnimationValueAnimatorMuscle(index1) + 1f) / 2f), vector3_5) * vector3_3;
                                lineX = handlePosition + lineVec * length;
                            }
                            //Y
                            Vector3 normalY = Vector3.zero, fromY = Vector3.zero, lineY = Vector3.zero;
                            float angleY = 0f, radiusY = 0f;
                            if (index2 != -1)
                            {
                                float angle = va.humanoidMuscleLimit[humanoidIndex].min.y;
                                float num = va.humanoidMuscleLimit[humanoidIndex].max.y;
                                float length = axisLength;
                                Vector3 vector3_2 = quaternion1 * Vector3.up * limitSign.y;
                                Vector3 vector3_3 = quaternion1 * zyRoll * Vector3.right;
                                Vector3 from = Quaternion.AngleAxis(angle, vector3_2) * vector3_3;

                                normalY = vector3_2;
                                fromY = from;
                                angleY = num - angle;
                                radiusY = length;
                                Vector3 lineVec = Quaternion.AngleAxis(Mathf.Lerp(angle, num, (va.GetAnimationValueAnimatorMuscle(index2) + 1f) / 2f), vector3_2) * vector3_3;
                                lineY = handlePosition + lineVec * length;
                            }
                            //Z
                            Vector3 normalZ = Vector3.zero, fromZ = Vector3.zero, lineZ = Vector3.zero;
                            float angleZ = 0f, radiusZ = 0f;
                            if (index3 != -1)
                            {
                                float angle = va.humanoidMuscleLimit[humanoidIndex].min.z;
                                float num = va.humanoidMuscleLimit[humanoidIndex].max.z;
                                float length = axisLength;
                                Vector3 vector3_7 = quaternion1 * Vector3.forward * limitSign.z;
                                Vector3 vector3_8 = quaternion1 * zyRoll * Vector3.right;
                                Vector3 from = Quaternion.AngleAxis(angle, vector3_7) * vector3_8;

                                normalZ = vector3_7;
                                fromZ = from;
                                angleZ = num - angle;
                                radiusZ = length;
                                Vector3 lineVec = Quaternion.AngleAxis(Mathf.Lerp(angle, num, (va.GetAnimationValueAnimatorMuscle(index3) + 1f) / 2f), vector3_7) * vector3_8;
                                lineZ = handlePosition + lineVec * length;
                            }
                            if (GUIUtility.hotControl == muscleRotationHandleIds[0])
                            {
                                #region DrawY
                                if (index2 != -1)
                                {
                                    Color color = muscleRotationHandleIds[1] == GUIUtility.hotControl || muscleRotationSliderIds[1] == GUIUtility.hotControl ? new Color(1f, 1f, 1f, 0.5f) : new Color(1, 1, 1, 0.2f);
                                    Handles.color = Handles.yAxisColor * color;
                                    Handles.DrawSolidArc(handlePosition, normalY, fromY, angleY, radiusY);
                                    Handles.color = new Color(1f, 0f, 1f, Handles.color.a);
                                    Handles.DrawLine(handlePosition, lineY);
                                }
                                #endregion
                                #region DrawZ
                                if (index3 != -1)
                                {
                                    Color color = muscleRotationHandleIds[2] == GUIUtility.hotControl || muscleRotationSliderIds[2] == GUIUtility.hotControl ? new Color(1f, 1f, 1f, 0.5f) : new Color(1, 1, 1, 0.2f);
                                    Handles.color = Handles.zAxisColor * color;
                                    Handles.DrawSolidArc(handlePosition, normalZ, fromZ, angleZ, radiusZ);
                                    Handles.color = new Color(1f, 1f, 0f, Handles.color.a);
                                    Handles.DrawLine(handlePosition, lineZ);
                                }
                                #endregion
                                #region DrawX
                                if (index1 != -1)
                                {
                                    Color color = muscleRotationHandleIds[0] == GUIUtility.hotControl || muscleRotationSliderIds[0] == GUIUtility.hotControl ? new Color(1f, 1f, 1f, 0.5f) : new Color(1, 1, 1, 0.2f);
                                    Handles.color = Handles.xAxisColor * color;
                                    Handles.DrawSolidArc(handlePosition, normalX, fromX, angleX, radiusX);
                                    Handles.color = new Color(0f, 1f, 1f, Handles.color.a);
                                    Handles.DrawLine(handlePosition, lineX);
                                }
                                #endregion
                            }
                            else if (GUIUtility.hotControl == muscleRotationHandleIds[1])
                            {
                                #region DrawX
                                if (index1 != -1)
                                {
                                    Color color = muscleRotationHandleIds[0] == GUIUtility.hotControl || muscleRotationSliderIds[0] == GUIUtility.hotControl ? new Color(1f, 1f, 1f, 0.5f) : new Color(1, 1, 1, 0.2f);
                                    Handles.color = Handles.xAxisColor * color;
                                    Handles.DrawSolidArc(handlePosition, normalX, fromX, angleX, radiusX);
                                    Handles.color = new Color(0f, 1f, 1f, Handles.color.a);
                                    Handles.DrawLine(handlePosition, lineX);
                                }
                                #endregion
                                #region DrawZ
                                if (index3 != -1)
                                {
                                    Color color = muscleRotationHandleIds[2] == GUIUtility.hotControl || muscleRotationSliderIds[2] == GUIUtility.hotControl ? new Color(1f, 1f, 1f, 0.5f) : new Color(1, 1, 1, 0.2f);
                                    Handles.color = Handles.zAxisColor * color;
                                    Handles.DrawSolidArc(handlePosition, normalZ, fromZ, angleZ, radiusZ);
                                    Handles.color = new Color(1f, 1f, 0f, Handles.color.a);
                                    Handles.DrawLine(handlePosition, lineZ);
                                }
                                #endregion
                                #region DrawY
                                if (index2 != -1)
                                {
                                    Color color = muscleRotationHandleIds[1] == GUIUtility.hotControl || muscleRotationSliderIds[1] == GUIUtility.hotControl ? new Color(1f, 1f, 1f, 0.5f) : new Color(1, 1, 1, 0.2f);
                                    Handles.color = Handles.yAxisColor * color;
                                    Handles.DrawSolidArc(handlePosition, normalY, fromY, angleY, radiusY);
                                    Handles.color = new Color(1f, 0f, 1f, Handles.color.a);
                                    Handles.DrawLine(handlePosition, lineY);
                                }
                                #endregion
                            }
                            else
                            {
                                #region DrawX
                                if (index1 != -1)
                                {
                                    Color color = muscleRotationHandleIds[0] == GUIUtility.hotControl || muscleRotationSliderIds[0] == GUIUtility.hotControl ? new Color(1f, 1f, 1f, 0.5f) : new Color(1, 1, 1, 0.2f);
                                    Handles.color = Handles.xAxisColor * color;
                                    Handles.DrawSolidArc(handlePosition, normalX, fromX, angleX, radiusX);
                                    Handles.color = new Color(0f, 1f, 1f, Handles.color.a);
                                    Handles.DrawLine(handlePosition, lineX);
                                }
                                #endregion
                                #region DrawY
                                if (index2 != -1)
                                {
                                    Color color = muscleRotationHandleIds[1] == GUIUtility.hotControl || muscleRotationSliderIds[1] == GUIUtility.hotControl ? new Color(1f, 1f, 1f, 0.5f) : new Color(1, 1, 1, 0.2f);
                                    Handles.color = Handles.yAxisColor * color;
                                    Handles.DrawSolidArc(handlePosition, normalY, fromY, angleY, radiusY);
                                    Handles.color = new Color(1f, 0f, 1f, Handles.color.a);
                                    Handles.DrawLine(handlePosition, lineY);
                                }
                                #endregion
                                #region DrawZ
                                if (index3 != -1)
                                {
                                    Color color = muscleRotationHandleIds[2] == GUIUtility.hotControl || muscleRotationSliderIds[2] == GUIUtility.hotControl ? new Color(1f, 1f, 1f, 0.5f) : new Color(1, 1, 1, 0.2f);
                                    Handles.color = Handles.zAxisColor * color;
                                    Handles.DrawSolidArc(handlePosition, normalZ, fromZ, angleZ, radiusZ);
                                    Handles.color = new Color(1f, 1f, 0f, Handles.color.a);
                                    Handles.DrawLine(handlePosition, lineZ);
                                }
                                #endregion
                            }
                        }
                    }
                    #endregion

                    #region Collision
                    if (va.DrawCollision())
                    {
                        repaintScene = true;
                    }
                    #endregion
                }

                #region Bones
                {
                    var bkColor = GUI.color;
                    Handles.BeginGUI();

                    #region Bones
                    for (int i = 0; i < va.bones.Length; i++)
                    {
                        if (i == va.rootMotionBoneIndex) continue;
                        if (!va.IsShowBone(i)) continue;

                        var pos = HandleUtility.WorldToGUIPoint(va.editBones[i].transform.position);
                        var rect = new Rect(pos.x + 2 - editorSettings.settingBoneButtonSize / 2f, pos.y - 2 - editorSettings.settingBoneButtonSize / 2f, editorSettings.settingBoneButtonSize, editorSettings.settingBoneButtonSize);     //Why is it shifted. I do not know the cause 2
                        bool selected = va.SelectionGameObjectsIndexOf(va.bones[i]) >= 0;
                        GUIStyle guiStyle = guiStyleCircleButton;
                        if (va.isHuman)
                        {
                            if (i == va.rootMotionBoneIndex)
                                guiStyle = guiStyleCircle3Button;
                            else if (va.boneIndex2humanoidIndex[i] < 0)
                                guiStyle = guiStyleDiamondButton;
                        }
                        else
                        {
                            if (va.rootMotionBoneIndex >= 0)
                            {
                                if (i == va.rootMotionBoneIndex)
                                    guiStyle = guiStyleCircle3Button;
                            }
                            else
                            {
                                if (i == 0)
                                    guiStyle = guiStyleCircle3Button;
                            }
                        }
                        if (selected) GUI.color = editorSettings.settingBoneActiveColor;
                        else GUI.color = editorSettings.settingBoneNormalColor;

                        if (GUI.Button(rect, "", guiStyle))
                        {
                            va.SelectGameObjectPlusKey(va.bones[i]);
                        }
                    }
                    #endregion

                    if (va.isHuman)
                    {
                        #region Virtual
                        {
                            for (int i = 0; i < VeryAnimation.HumanVirtualBones.Length; i++)
                            {
                                if (!va.IsShowVirtualBone((HumanBodyBones)i)) continue;

                                var pos = HandleUtility.WorldToGUIPoint(va.GetHumanVirtualBonePosition((HumanBodyBones)i));
                                var rect = new Rect(pos.x - editorSettings.settingBoneButtonSize / 2f, pos.y - editorSettings.settingBoneButtonSize / 2f, editorSettings.settingBoneButtonSize, editorSettings.settingBoneButtonSize);
                                bool selected = va.SelectionGameObjectsContains((HumanBodyBones)i);
                                if (selected) GUI.color = editorSettings.settingBoneActiveColor;
                                else GUI.color = editorSettings.settingBoneNormalColor;
                                GUIStyle guiStyle = guiStyleCircleDotButton;
                                if (GUI.Button(rect, "", guiStyle))
                                {
                                    va.SelectVirtualBonePlusKey((HumanBodyBones)i);
                                    VeryAnimationControlWindow.ForceSelectionChange();
                                }
                            }
                        }
                        #endregion

                        #region Root
                        if (va.IsShowBone(va.rootMotionBoneIndex))
                        {
                            var pos = HandleUtility.WorldToGUIPoint(va.humanWorldRootPositionCache);
                            var rect = new Rect(pos.x - editorSettings.settingBoneButtonSize / 2f, pos.y - editorSettings.settingBoneButtonSize / 2f, editorSettings.settingBoneButtonSize, editorSettings.settingBoneButtonSize);
                            bool selected = va.SelectionGameObjectsIndexOf(gameObject) >= 0;
                            if (selected) GUI.color = editorSettings.settingBoneActiveColor;
                            else GUI.color = editorSettings.settingBoneNormalColor;
                            if (GUI.Button(rect, "", guiStyleCircle3Button))
                            {
                                va.SelectGameObjectPlusKey(gameObject);
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        #region Root
                        if (va.IsShowBone(va.rootMotionBoneIndex))
                        {
                            var pos = HandleUtility.WorldToGUIPoint(va.editBones[va.rootMotionBoneIndex].transform.position);
                            var rect = new Rect(pos.x - editorSettings.settingBoneButtonSize / 2f, pos.y - editorSettings.settingBoneButtonSize / 2f, editorSettings.settingBoneButtonSize, editorSettings.settingBoneButtonSize);
                            bool selected = va.SelectionGameObjectsIndexOf(va.bones[va.rootMotionBoneIndex]) >= 0;
                            if (selected) GUI.color = editorSettings.settingBoneActiveColor;
                            else GUI.color = editorSettings.settingBoneNormalColor;
                            if (GUI.Button(rect, "", guiStyleCircle3Button))
                            {
                                va.SelectGameObjectPlusKey(va.bones[va.rootMotionBoneIndex]);
                            }
                        }
                        #endregion
                    }

                    Handles.EndGUI();
                    GUI.color = bkColor;
                }
                #endregion
            }
            #endregion

            #region SceneWindow
            #region Selection
            if (vae.editorSelectionOnScene)
            {
                editorWindowSelectionRect.width = vae.position.width;
                editorWindowSelectionRect = ResizeSceneViewRect(sceneView, editorWindowSelectionRect);
                editorWindowSelectionRect = GUILayout.Window(EditorGUIUtility.GetControlID(FocusType.Passive, editorWindowSelectionRect), editorWindowSelectionRect, (id) =>
                {
                    var saveSkin = GUI.skin;
                    GUI.skin = guiSkinSceneWindow;
                    {
                        vae.EditorGUI_SelectionGUI(true);
                    }
                    GUI.skin = saveSkin;
                    GUI.DragWindow();

                }, "Selection", guiStyleSceneWindow);
            }
            #endregion
            #endregion

            if (repaintScene)
            {
                sceneView.Repaint();
            }

#if Enable_Profiler
            Profiler.EndSample();
#endif
        }

        private Rect ResizeSceneViewRect(SceneView sceneView, Rect rect)
        {
            var position = sceneView.position;
            if (rect.x + rect.width >= position.width)
                rect.x -= (rect.x + rect.width) - position.width;
            if (rect.y + rect.height >= position.height)
                rect.y -= (rect.y + rect.height) - position.height;
            if (rect.x < 0)
                rect.x -= rect.x;
            if (rect.y < 0)
                rect.y -= rect.y;
            return rect;
        }

        private void DrawRootTrail()
        {
            if (rootTrail == null)
                rootTrail = new RootTrail();
            rootTrail.Draw();
        }

        private void DrawSkeleton()
        {
            if (editorSettings.settingsSkeletonFKType == EditorSettings.SkeletonType.Mesh ||
                editorSettings.settingsSkeletonIKType == EditorSettings.SkeletonType.Mesh)
            {
                if (arrowMesh == null)
                    arrowMesh = new EditorCommon.ArrowMesh();
            }
            
#if VERYANIMATION_ANIMATIONRIGGING
            #region AnimationRigging
            {
                if (editorSettings.settingsSkeletonIKType == EditorSettings.SkeletonType.Line)
                {
                    uHandleUtility.ApplyWireMaterial();
                    GL.PushMatrix();
                    GL.MultMatrix(Handles.matrix);
                    GL.Begin(GL.LINES);
                }
                else if (editorSettings.settingsSkeletonIKType == EditorSettings.SkeletonType.Lines)
                {
                    uHandleUtility.ApplyWireMaterial();
                    GL.PushMatrix();
                    GL.MultMatrix(Handles.matrix);
                    GL.Begin(GL.LINES);
                }

                for (int target = 0; target < va.animatorIK.ikData.Length; target++)
                {
                    if (va.animatorIK.ikData[target].rigConstraint == null)
                        continue;
                    switch ((AnimatorIKCore.IKTarget)target)
                    {
                        case AnimatorIKCore.IKTarget.Head:
                            {
                                var constraint = va.animatorIK.ikData[target].rigConstraint as MultiAimConstraint;
                                if (constraint != null)
                                {
                                    if (constraint.data.constrainedObject != null && constraint.data.sourceObjects.Count > 0 && constraint.data.sourceObjects[0].transform != null)
                                    {
                                        var targetIndex = va.BonesIndexOf(constraint.data.sourceObjects[0].transform.gameObject);
                                        if (targetIndex >= 0)
                                            DrawSkeletonAnimationRiggingBone(va.BonesIndexOf(constraint.data.constrainedObject.gameObject), targetIndex, constraint.weight);
                                    }
                                }
                            }
                            break;
                        case AnimatorIKCore.IKTarget.LeftHand:
                        case AnimatorIKCore.IKTarget.RightHand:
                        case AnimatorIKCore.IKTarget.LeftFoot:
                        case AnimatorIKCore.IKTarget.RightFoot:
                            {
                                var constraint = va.animatorIK.ikData[target].rigConstraint as TwoBoneIKConstraint;
                                if (constraint != null)
                                {
                                    if (constraint.data.mid != null)
                                        DrawSkeletonAnimationRiggingBone(va.BonesIndexOf(constraint.data.mid.gameObject), -1, constraint.weight);
                                    if (constraint.data.tip != null)
                                        DrawSkeletonAnimationRiggingBone(va.BonesIndexOf(constraint.data.tip.gameObject), -1, constraint.weight);
                                }
                            }
                            break;
                    }
                }

                if (editorSettings.settingsSkeletonIKType == EditorSettings.SkeletonType.Line ||
                    editorSettings.settingsSkeletonIKType == EditorSettings.SkeletonType.Lines)
                {
                    GL.End();
                    GL.PopMatrix();
                }
            }
            #endregion
#endif
            #region IK
            if (va.skeletonIKShowBoneList.Count > 0)
            {
                if (editorSettings.settingsSkeletonIKType == EditorSettings.SkeletonType.Line)
                {
                    uHandleUtility.ApplyWireMaterial();
                    GL.PushMatrix();
                    GL.MultMatrix(Handles.matrix);
                    GL.Begin(GL.LINES);
                    GL.Color(editorSettings.settingSkeletonIKColor);
                    foreach (var pair in va.skeletonIKShowBoneList)
                    {
                        var boneA = va.bones[pair.y >= 0 ? pair.y : va.parentBoneIndexes[pair.x]];
                        var boneB = va.bones[pair.x];
                        if (boneA == null || boneB == null)
                            continue;
                        var posA = boneA.transform.position;
                        var posB = boneB.transform.position;
                        GL.Vertex(posA);
                        GL.Vertex(posB);
                    }
                    GL.End();
                    GL.PopMatrix();
                }
                else if (editorSettings.settingsSkeletonIKType == EditorSettings.SkeletonType.Lines)
                {
                    var cameraForward = SceneView.currentDrawingSceneView.camera.transform.forward;
                    float radius = editorSettings.GetSkeletonTypeLinesRadius(va.editGameObject.transform.position);

                    uHandleUtility.ApplyWireMaterial();
                    GL.PushMatrix();
                    GL.MultMatrix(Handles.matrix);
                    GL.Begin(GL.LINES);
                    GL.Color(editorSettings.settingSkeletonIKColor);
                    foreach (var pair in va.skeletonIKShowBoneList)
                    {
                        var boneA = va.bones[pair.y >= 0 ? pair.y : va.parentBoneIndexes[pair.x]];
                        var boneB = va.bones[pair.x];
                        if (boneA == null || boneB == null)
                            continue;
                        var posA = boneA.transform.position;
                        var posB = boneB.transform.position;

                        var vec = posB - posA;
                        var cross = Vector3.Cross(vec, cameraForward);
                        cross.Normalize();
                        vec.Normalize();

                        var posAL = posA + cross * radius + vec * radius;
                        var posAR = posA - cross * radius + vec * radius;

                        GL.Vertex(posA); GL.Vertex(posAL);
                        GL.Vertex(posAL); GL.Vertex(posB);
                        GL.Vertex(posB); GL.Vertex(posAR);
                        GL.Vertex(posAR); GL.Vertex(posA);
                    }
                    GL.End();
                    GL.PopMatrix();
                }
                else if (editorSettings.settingsSkeletonIKType == EditorSettings.SkeletonType.Mesh)
                {
                    foreach (var pair in va.skeletonIKShowBoneList)
                    {
                        var boneA = va.bones[pair.y >= 0 ? pair.y : va.parentBoneIndexes[pair.x]];
                        var boneB = va.bones[pair.x];
                        if (boneA == null || boneB == null)
                            continue;
                        var posA = boneA.transform.position;
                        var posB = boneB.transform.position;

                        arrowMesh.material.color = editorSettings.settingSkeletonIKColor;
                        arrowMesh.material.SetPass(0);
                        var vec = posB - posA;
                        var length = vec.magnitude;
                        Quaternion qat = posB != posA ? Quaternion.LookRotation(vec) : Quaternion.identity;
                        Matrix4x4 mat = Matrix4x4.TRS(posA, qat, new Vector3(length, length, length));
                        Graphics.DrawMeshNow(arrowMesh.mesh, mat);
                    }
                }
            }
            #endregion

            #region FK
            if (va.skeletonFKShowBoneList.Count > 0)
            {
                if (editorSettings.settingsSkeletonFKType == EditorSettings.SkeletonType.Line)
                {
                    uHandleUtility.ApplyWireMaterial();
                    GL.PushMatrix();
                    GL.MultMatrix(Handles.matrix);
                    GL.Begin(GL.LINES);
                    GL.Color(editorSettings.settingSkeletonFKColor);
                    foreach (var boneIndex in va.skeletonFKShowBoneList)
                    {
                        var boneA = va.editBones[va.parentBoneIndexes[boneIndex]];
                        var boneB = va.editBones[boneIndex];
                        if (boneA == null || boneB == null)
                            continue;
                        var posA = boneA.transform.position;
                        var posB = boneB.transform.position;
                        GL.Vertex(posA);
                        GL.Vertex(posB);
                    }
                    GL.End();
                    GL.PopMatrix();
                }
                else if (editorSettings.settingsSkeletonFKType == EditorSettings.SkeletonType.Lines)
                {
                    var cameraForward = SceneView.currentDrawingSceneView.camera.transform.forward;
                    float radius = editorSettings.GetSkeletonTypeLinesRadius(va.editGameObject.transform.position);

                    uHandleUtility.ApplyWireMaterial();
                    GL.PushMatrix();
                    GL.MultMatrix(Handles.matrix);
                    GL.Begin(GL.LINES);
                    GL.Color(editorSettings.settingSkeletonFKColor);
                    foreach (var boneIndex in va.skeletonFKShowBoneList)
                    {
                        var boneA = va.editBones[va.parentBoneIndexes[boneIndex]];
                        var boneB = va.editBones[boneIndex];
                        if (boneA == null || boneB == null)
                            continue;
                        var posA = boneA.transform.position;
                        var posB = boneB.transform.position;

                        var vec = posB - posA;
                        var cross = Vector3.Cross(vec, cameraForward);
                        cross.Normalize();
                        vec.Normalize();

                        var posAL = posA + cross * radius + vec * radius;
                        var posAR = posA - cross * radius + vec * radius;

                        GL.Vertex(posA); GL.Vertex(posAL);
                        GL.Vertex(posAL); GL.Vertex(posB);
                        GL.Vertex(posB); GL.Vertex(posAR);
                        GL.Vertex(posAR); GL.Vertex(posA);
                    }
                    GL.End();
                    GL.PopMatrix();
                }
                else if (editorSettings.settingsSkeletonFKType == EditorSettings.SkeletonType.Mesh)
                {
                    foreach (var boneIndex in va.skeletonFKShowBoneList)
                    {
                        var boneA = va.editBones[va.parentBoneIndexes[boneIndex]];
                        var boneB = va.editBones[boneIndex];
                        if (boneA == null || boneB == null)
                            continue;
                        var posA = boneA.transform.position;
                        var posB = boneB.transform.position;

                        arrowMesh.material.color = editorSettings.settingSkeletonFKColor;
                        arrowMesh.material.SetPass(0);
                        var vec = posB - posA;
                        var length = vec.magnitude;
                        Quaternion qat = posB != posA ? Quaternion.LookRotation(vec) : Quaternion.identity;
                        Matrix4x4 mat = Matrix4x4.TRS(posA, qat, new Vector3(length, length, length));
                        Graphics.DrawMeshNow(arrowMesh.mesh, mat);
                    }
                }
            }
            #endregion

            #region RootMotion
            {
                var posA = va.transformPoseSave.startPosition;
                var posB = va.editGameObject.transform.position;
                uHandleUtility.ApplyWireMaterial();
                GL.PushMatrix();
                GL.MultMatrix(Handles.matrix);
                GL.Begin(GL.LINES);
                GL.Color(editorSettings.settingRootMotionColor);
                {
                    GL.Vertex(posA);
                    GL.Vertex(posB);
                }
                GL.End();
                GL.PopMatrix();
            }
            #endregion
        }
#if VERYANIMATION_ANIMATIONRIGGING
        private void DrawSkeletonAnimationRiggingBone(int boneIndex, int targetIndex, float alpha)
        {
            if (!va.skeletonFKShowBoneFlag[boneIndex])
                return;

            var boneA = va.bones[targetIndex >= 0 ? targetIndex : va.parentBoneIndexes[boneIndex]];
            var boneB = va.bones[boneIndex];
            if (boneA == null || boneB == null)
                return;
            var posA = boneA.transform.position;
            var posB = boneB.transform.position;

            var color = editorSettings.settingSkeletonIKColor;
            color.a *= alpha;

            if (editorSettings.settingsSkeletonIKType == EditorSettings.SkeletonType.Line)
            {
                GL.Color(color);

                GL.Vertex(posA);
                GL.Vertex(posB);
            }
            else if (editorSettings.settingsSkeletonIKType == EditorSettings.SkeletonType.Lines)
            {
                var cameraForward = SceneView.currentDrawingSceneView.camera.transform.forward;
                float radius = editorSettings.GetSkeletonTypeLinesRadius(va.editGameObject.transform.position);

                GL.Color(color);

                var vec = posB - posA;
                var cross = Vector3.Cross(vec, cameraForward);
                cross.Normalize();
                vec.Normalize();

                var posAL = posA + cross * radius + vec * radius;
                var posAR = posA - cross * radius + vec * radius;

                GL.Vertex(posA); GL.Vertex(posAL);
                GL.Vertex(posAL); GL.Vertex(posB);
                GL.Vertex(posB); GL.Vertex(posAR);
                GL.Vertex(posAR); GL.Vertex(posA);
            }
            else if (editorSettings.settingsSkeletonIKType == EditorSettings.SkeletonType.Mesh)
            {
                arrowMesh.material.color = color;
                arrowMesh.material.SetPass(0);

                var vec = posB - posA;
                var length = vec.magnitude;
                Quaternion qat = posB != posA ? Quaternion.LookRotation(vec) : Quaternion.identity;
                Matrix4x4 mat = Matrix4x4.TRS(posA, qat, new Vector3(length, length, length));
                Graphics.DrawMeshNow(arrowMesh.mesh, mat);
            }
        }
#endif

        private void OnInspectorUpdate()
        {
#if Enable_Profiler
            Profiler.BeginSample("****VeryAnimationWindow.OnInspectorUpdate");
#endif
            if (initialized)
            {
                va.OnInspectorUpdate();

                #region Repaint
                switch (repaintGUI)
                {
                case RepaintGUI.Edit:
                    Repaint();
                    VeryAnimationEditorWindow.ForceRepaint();
                    break;
                case RepaintGUI.All:
                    Repaint();
                    VeryAnimationEditorWindow.ForceRepaint();
                    VeryAnimationControlWindow.ForceRepaint();
                    SceneView.RepaintAll();
                    break;
                }
                repaintGUI = RepaintGUI.None;
                #endregion
            }
            else
            {
                SetGameObject();

                #region Repaint
                {
                    var errorCode = va.getErrorCode;
                    if (beforeErrorCode != errorCode)
                    {
                        Repaint();
                        beforeErrorCode = errorCode;
                    }
                }
                #endregion

                #region LastSelectAnimationClip
                if (gameObject != null && !va.edit && !AnimationMode.InAnimationMode())
                {
                    var clip = va.uAw.GetSelectionAnimationClip();
                    if (beforeAnimationClip != clip)
                    {
                        var saveSettings = gameObject.GetComponent<VeryAnimationSaveSettings>();
                        if (saveSettings != null && saveSettings.lastSelectAnimationClip != clip)
                            saveSettings.lastSelectAnimationClip = clip;
                        beforeAnimationClip = clip;
                    }
                }
                #endregion

                #region PlayingAnimation
                if (UpdatePlayingAnimation())
                {
                    Repaint();
                }
                #endregion
            }

#if Enable_Profiler
            Profiler.EndSample();
#endif
        }
        private void CustomUpdate()
        {
            if (initialized)
            {
                if (va.isEditError)
                {
                    var lastTime = va.currentTime;
                    var errorCode = va.getErrorCode;
                    Release();
                    if (va.uAw.GetLinkedWithTimeline() && va.uAw.GetActiveRootGameObject() != null)
                    {
#if VERYANIMATION_TIMELINE
                        #region ChangeTimelineAnimationTrack
                        SetGameObject();
                        if (!va.isError)
                        {
                            Initialize();
                            va.SetCurrentTime(lastTime);
                            va.uAw.StopRecording();     //Update immediately with the next Update
                        }
                        #endregion
#else
                        Assert.IsTrue(false);
#endif
                    }
                    else
                    {
                        Debug.LogFormat("<color=blue>[Very Animation]</color>Editing ended : Error code {0}", errorCode);
                    }
                }
                else if (forceChangeObject != null)
                {
                    #region ChangeOtherObject
                    var lastTime = va.currentTime;
                    Release();
                    Selection.activeGameObject = forceChangeObject;
                    forceChangeObject = null;
                    va.uAw.OnSelectionChange();
                    SetGameObject();
                    if (!va.isError)
                    {
                        Initialize();
                        va.SetCurrentTime(lastTime);
                        va.uAw.StopRecording();     //Update immediately with the next Update
                    }
                    #endregion
                }
            }
            else
            {
                return;
            }

#if Enable_Profiler
            Profiler.BeginSample("****VeryAnimationWindow.Update");
#endif

            va.Update();

#if Enable_Profiler
            Profiler.EndSample();
#endif
        }

        public void Initialize()
        {
            Release();

            #region MemoryLeakCheck
#if Enable_MemoryLeakCheck
            memoryLeakDontSaveList = new List<UnityEngine.Object>();
            foreach (var obj in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            {
                if ((obj.hideFlags & HideFlags.DontSave) == 0) continue;
                memoryLeakDontSaveList.Add(obj);
            }
#endif
            #endregion

            if (EditorApplication.isPlaying)
            {
                if (!EditorApplication.isPaused)
                    EditorApplication.isPaused = true;
                if (animator != null)
                    pauseAnimatorStateSave = new AnimatorStateSave(animator);
            }
            else
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }

            UpdatePlayingAnimation();
            UpdateClipSelectorTree();

            Selection.activeObject = null;

            undoGroupID = Undo.GetCurrentGroup();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Very Animation Edit");
            Undo.RecordObject(this, "Very Animation Edit");

            va.Initialize();
            editorSettings.Initialize();

            if (!uEditorWindow.HasFocus(this))
            {
                beforeSelectedTab = uEditorWindow.GetSelectedTab(this);
                Focus();
            }
            else
            {
                beforeSelectedTab = -1;
            }

            #region VeryAnimationEditorWindow
            if (VeryAnimationEditorWindow.instance == null)
            {
                if (editorSettings.settingEditorWindowStyle == EditorSettings.EditorWindowStyle.Floating)
                {
                    var hew = EditorWindow.CreateInstance<VeryAnimationEditorWindow>();
                    hew.ShowUtility();
                }
                else if (editorSettings.settingEditorWindowStyle == EditorSettings.EditorWindowStyle.Docking)
                {
                    EditorWindow window = null;
                    foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    {
                        if (w.GetType().Name == "InspectorWindow")
                        {
                            if (uEditorWindow.HasFocus(w))
                            {
                                window = w;
                                break;
                            }
                        }
                    }
                    if (window != null)
                        GetWindow<VeryAnimationEditorWindow>(window.GetType());
                    if (VeryAnimationEditorWindow.instance == null)
                        GetWindow<VeryAnimationEditorWindow>();
                }
            }
            if (VeryAnimationEditorWindow.instance != null)
                VeryAnimationEditorWindow.instance.Initialize();
            #endregion
            #region VeryAnimationControlWindow
            if (VeryAnimationControlWindow.instance == null)
            {
                EditorWindow dockWindow = null;
                foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    if (w.GetType().Name == "SceneHierarchyWindow" &&
                        !uEditorWindow.IsDockBrother(va.uAw.instance, w))
                    {
                        dockWindow = w;
                        break;
                    }
                }
                if (dockWindow != null)
                {
                    var controlWindow = CreateInstance<VeryAnimationControlWindow>();
                    uEditorWindow.AddTab(dockWindow, controlWindow);
                }
                if (VeryAnimationControlWindow.instance == null)
                    GetWindow<VeryAnimationControlWindow>();
            }
            if (VeryAnimationControlWindow.instance != null)
                VeryAnimationControlWindow.instance.Initialize();
            #endregion

            #region SaveSettings
            {
                #region EditorPref
                {
                    guiAnimationFoldout = EditorPrefs.GetBool("VeryAnimation_Main_Animation", true);
                    guiToolsFoldout = EditorPrefs.GetBool("VeryAnimation_Main_Tools", false);
                    guiSettingsFoldout = EditorPrefs.GetBool("VeryAnimation_Main_Settings", false);
                    guiHelpFoldout = EditorPrefs.GetBool("VeryAnimation_Main_Help", false);
                    guiPreviewFoldout = EditorPrefs.GetBool("VeryAnimation_Main_Preview", true);
                }
                #endregion
                va.LoadSaveSettings();
            }
            va.OnBoneShowFlagsUpdated.Invoke();
            #endregion

            #region SceneWindow
            editorWindowSelectionRect.size = Vector2.zero;
            #endregion

            EditorApplication.update += CustomUpdate;
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.beforeSceneGui += OnPreSceneGUI;
#else
            SceneView.onSceneGUIDelegate += OnSceneGUI;
            {
                var del = uSceneView.GetOnPreSceneGUIDelegate();
                del += OnPreSceneGUI;
                uSceneView.SetOnPreSceneGUIDelegate(del);
            }
#endif
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.pauseStateChanged += OnPauseStateChanged;
            Undo.undoRedoPerformed += UndoRedoPerformed;
            initialized = true;

            OnSelectionChange();

            InternalEditorUtility.RepaintAllViews();
            EditorApplication.delayCall += () =>
            {
                InternalEditorUtility.RepaintAllViews();
                if (VeryAnimationEditorWindow.instance != null)
                    VeryAnimationEditorWindow.instance.Focus();
            };
        }
        public void Release()
        {
            if (instance == null || va == null || (!initialized && !va.edit)) return;
            initialized = false;

            Undo.SetCurrentGroupName("Very Animation Edit");
            Undo.RecordObject(this, "Very Animation Edit");

            #region SaveSettings
            if (gameObject != null)
            {
                var saveSettings = gameObject.GetComponent<VeryAnimationSaveSettings>();
                if (editorSettings.settingComponentSaveSettings)
                {
                    #region Disconnect Prefab Component
                    if (saveSettings != null)
                    {
#if UNITY_2018_2_OR_NEWER
                        var prefabSaveSettings = PrefabUtility.GetCorrespondingObjectFromSource(saveSettings);
#else
                        var prefabSaveSettings = PrefabUtility.GetPrefabParent(saveSettings);
#endif
                        if (prefabSaveSettings != null)
                        {
                            Undo.DestroyObjectImmediate(saveSettings);
                            saveSettings = null;
                        }
                    }
                    #endregion
                    if (saveSettings == null)
                    {
                        //saveSettings = Undo.AddComponent<VeryAnimationSaveSettings>(gameObject);  Unexplained cause, Unity is crash depending on data, so change to the following.
                        saveSettings = gameObject.AddComponent<VeryAnimationSaveSettings>();
                        if (saveSettings != null)
                        {
                            InternalEditorUtility.SetIsInspectorExpanded(saveSettings, false);
                            Undo.RegisterCreatedObjectUndo(saveSettings, "Very Animation Edit");
                        }
                    }
                    if (saveSettings != null)
                    {
                        Undo.RecordObject(saveSettings, "Very Animation Edit");
                        va.SaveSaveSettings();
                    }
                }
                else
                {
                    if (saveSettings != null)
                        Undo.DestroyObjectImmediate(saveSettings);
                }
            }
            #endregion

            EditorApplication.update -= CustomUpdate;
#if UNITY_2019_1_OR_NEWER
            SceneView.beforeSceneGui -= OnPreSceneGUI;
            SceneView.duringSceneGui -= OnSceneGUI;
#else
            if (uSceneView != null)
            {
                var del = uSceneView.GetOnPreSceneGUIDelegate();
                del -= OnPreSceneGUI;
                uSceneView.SetOnPreSceneGUIDelegate(del);
            }
            SceneView.onSceneGUIDelegate -= OnSceneGUI;
#endif

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.pauseStateChanged -= OnPauseStateChanged;
            Undo.undoRedoPerformed -= UndoRedoPerformed;
            Language.OnLanguageChanged = null;
            #region Editor
            handleTransformUpdate = true;
            arrowMesh = null;
            rootTrail = null;
            #endregion

            editorSettings.Release();
            va.Release();

            if (undoGroupID >= 0)
            {
                Undo.CollapseUndoOperations(undoGroupID);
                undoGroupID = -1;
            }

            if (pauseAnimatorStateSave != null && animator != null)
                pauseAnimatorStateSave.Load(animator);
            pauseAnimatorStateSave = null;

            if (beforeSelectedTab >= 0 && beforeSelectedTab < uEditorWindow.GetNumTabs(this))
            {
                uEditorWindow.SetSelectedTab(this, beforeSelectedTab);
            }

            EditorApplication.delayCall += () =>
            {
                if (va == null || va.isEditError)
                    CloseOtherWindows();
            };

            GC.Collect();

            #region MemoryLeakCheck
#if Enable_MemoryLeakCheck
            if (memoryLeakDontSaveList != null)
            {
                EditorApplication.delayCall += () =>
                {
                    EditorApplication.delayCall += () =>
                    {
                        foreach (var obj in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
                        {
                            if ((obj.hideFlags & HideFlags.DontSave) == 0) continue;
                            if (!memoryLeakDontSaveList.Contains(obj))
                                Debug.LogWarningFormat("Memory Leak = Type({0}), Name({1})", obj.GetType(), obj.name);

                        }
                        memoryLeakDontSaveList = null;
                    };
                };
            }
#endif
            #endregion

            if (!va.uAw.GetLinkedWithTimeline())
                Selection.activeObject = va.uAw.GetActiveRootGameObject();

            va.uAw.ForceRefresh();
            InternalEditorUtility.RepaintAllViews();
        }

        public bool IsShowSceneGizmo()
        {
            if (Tools.current == Tool.View) return false;
            if (va.uAw.GetPlaying()) return false;
            return true;
        }

        public bool UpdatePlayingAnimation()
        {
            AnimationClip clip;
            float time, length;
            if (va.GetPlayingAnimationInfo(out clip, out time, out length))
            {
                if (playingAnimationClip != clip || playingAnimationTime != time)
                {
                    playingAnimationClip = clip;
                    playingAnimationTime = time;
                    playingAnimationLength = length;
                    return true;
                }
            }
            else if (playingAnimationClip != null || playingAnimationTime != 0f)
            {
                playingAnimationClip = null;
                playingAnimationTime = 0f;
                playingAnimationLength = 0;
                return true;
            }
            return false;
        }

        public bool IsContainsSelectionAnimationClip(AnimationClip clip)
        {
            var index = ArrayUtility.IndexOf(clipSelectorTreeView.animationClips, clip);
            return index >= 0;
        }
        public void MoveChangeSelectionAnimationClip(int move)
        {
            clipSelectorTreeView.OffsetSelection(move);
        }

        private void SetGameObject()
        {
            bool updated = false;
            var go = va.uAw != null ? va.uAw.GetActiveRootGameObject() : null;
            if (go != gameObject)
            {
                gameObject = go;
                updated = true;
            }
            var ap = va.uAw != null ? va.uAw.GetActiveAnimationPlayer() : null;
            if (ap is Animator)
            {
                var apa = ap as Animator;
                if (animator != apa)
                {
                    animator = apa;
                    updated = true;
                }
                animation = null;
            }
            else if (ap is Animation)
            {
                var apa = ap as Animation;
                if (animation != apa)
                {
                    animation = apa;
                    updated = true;
                }
                animator = null;
            }
            else
            {
                if (animation != null)
                {
                    animation = null;
                    updated = true;
                }
                if (animator != null)
                {
                    animator = null;
                    updated = true;
                }
            }
            if (updated)
            {
                #region ClipSelector
                UpdateClipSelectorTree();
                #region LastSelectAnimationClip
                if (gameObject != null)
                {
                    if (!EditorApplication.isPlaying && !va.uAw.GetLinkedWithTimeline())
                    {
                        var saveSettings = gameObject.GetComponent<VeryAnimationSaveSettings>();
                        if (saveSettings != null)
                        {
                            if (IsContainsSelectionAnimationClip(saveSettings.lastSelectAnimationClip))
                            {
                                va.uAw.SetSelectionAnimationClip(saveSettings.lastSelectAnimationClip);
                                clipSelectorTreeView.UpdateSelectedIds();
                            }
                        }
                    }
                }
                #endregion
                #endregion

                Repaint();
            }
        }

        #region Undo
        private void UndoRedoPerformed()
        {
            clipSelectorUpdate = true;

            Repaint();
        }
        #endregion
    }
}
