//#define Enable_Profiler

#if !UNITY_2019_1_OR_NEWER
#define VERYANIMATION_TIMELINE
#endif

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
#if UNITY_2018_3_OR_NEWER && !UNITY_2021_2_OR_NEWER
using UnityEditor.Experimental.SceneManagement;
#endif
#if VERYANIMATION_ANIMATIONRIGGING
using UnityEngine.Animations.Rigging;
using UnityEditor.Animations.Rigging;
#endif

namespace VeryAnimation
{
    [Serializable]
    public partial class VeryAnimation
    {
        public static VeryAnimation instance;

        private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }

        #region Reflection
        public UAnimationWindow uAw { get; private set; }
        public UAvatar uAvatar { get; private set; }
        public UAnimator uAnimator { get; private set; }
        public UAnimatorControllerTool uAnimatorControllerTool { get; private set; }
        public UParameterControllerEditor uParameterControllerEditor { get; private set; }
        public UAnimationUtility uAnimationUtility { get; private set; }
        public UAnimationWindowUtility uAnimationWindowUtility { get; private set; }
        public UCurveUtility uCurveUtility { get; private set; }
        public URotationCurveInterpolation uRotationCurveInterpolation { get; private set; }
        public USceneView uSceneView { get; private set; }

        public UAvatarPreview uAvatarPreview { get; private set; }
        public UAnimationClipEditor uAnimationClipEditor { get; private set; }

#if UNITY_2018_1_OR_NEWER
        public UAnimationWindow_2018_1 uAw_2018_1 { get; private set; }
#endif
#if UNITY_2019_1_OR_NEWER
        public UAnimationWindow_2019_1 uAw_2019_1 { get; private set; }
#endif
#if UNITY_2019_2_OR_NEWER
        public UAnimationWindow_2019_2 uAw_2019_2 { get; private set; }
#endif
#if UNITY_2020_1_OR_NEWER
        public UAnimationWindow_2020_1 uAw_2020_1 { get; private set; }
#endif
#if UNITY_2023_1_OR_NEWER
        public UAnimationWindow_2023_1 uAw_2023_1 { get; private set; }
#endif
#if UNITY_2018_1_OR_NEWER
        public UAnimationUtility_2018_1 uAnimationUtility_2018_1 { get; private set; }
#endif
        #endregion

        #region Core
        public MusclePropertyName musclePropertyName { get; private set; }
#if VERYANIMATION_ANIMATIONRIGGING
        public AnimationRigging animationRigging { get; private set; }
#endif
        public AnimatorIKCore animatorIK;
        public OriginalIKCore originalIK;
        private SynchronizeAnimation synchronizeAnimation;
        #endregion

        public bool edit;

        #region Selection
        public List<GameObject> selectionGameObjects { get; private set; }
        public List<int> selectionBones { get; private set; }
        public GameObject selectionActiveGameObject { get { return selectionGameObjects != null && selectionGameObjects.Count > 0 ? selectionGameObjects[0] : null; } }
        public int selectionActiveBone { get { return selectionBones != null && selectionBones.Count > 0 ? selectionBones[0] : -1; } }
        public List<HumanBodyBones> selectionHumanVirtualBones { get; private set; }
        public bool selectionMotionTool { get; private set; }
        #endregion

        #region Cache
        public class MirrorBoneData
        {
            public int rootBoneIndex;
            public bool[] positionTangentInverse;
            public bool[] rotationTangentInverse;
            public bool[] eulerAnglesTangentInverse;
            public bool[] scaleTangentInverse;
        }
        public TransformPoseSave transformPoseSave { get; private set; }
        public BlendShapeWeightSave blendShapeWeightSave { get; private set; }
        public Renderer[] renderers { get; private set; }
        public bool isHuman { get; private set; }
        public bool animatorApplyRootMotion { get; private set; }
        public Avatar animatorAvatar { get; private set; }
        public Transform animatorAvatarRoot { get; private set; }
        public GameObject[] bones { get; private set; }
        public Dictionary<GameObject, int> boneDic { get; private set; }
        public GameObject[] humanoidBones { get; private set; }
        public int[] parentBoneIndexes { get; private set; }
        public int[] boneHierarchyLevels { get; private set; }
        public int boneHierarchyMaxLevel { get; private set; }
        public int[] mirrorBoneIndexes { get; private set; }
        public MirrorBoneData[] mirrorBoneData { get; private set; }
        public Dictionary<SkinnedMeshRenderer, Dictionary<string, string>> mirrorBlendShape { get; private set; }
        public HumanBodyBones[] boneIndex2humanoidIndex { get; private set; }
        public int[] humanoidIndex2boneIndex { get; private set; }
        public bool[] humanoidConflict { get; private set; }
        public string[] bonePaths { get; private set; }
        public Dictionary<string, int> bonePathDic { get; private set; }
        public UAvatar.Transform[] boneDefaultPose { get; private set; }
        public TransformPoseSave.SaveData[] boneSaveTransforms { get; private set; }
        public TransformPoseSave.SaveData[] boneSaveOriginalTransforms { get; private set; }
        public HumanPose saveHumanPose { get; private set; }
        public UAvatar.MuscleLimit[] humanoidMuscleLimit { get; private set; }
        public bool humanoidHasLeftHand { get; private set; }
        public bool humanoidHasRightHand { get; private set; }
        public bool humanoidHasTDoF { get; private set; }
        public Quaternion humanoidPreHipRotationInverse { get; private set; }
        public Quaternion humanoidPostHipRotation { get; private set; }
        public float humanoidLeftLowerLegLengthSq { get; private set; }
        public bool[] humanoidMuscleContains { get; private set; }
        public HumanPoseHandler humanPoseHandler { get; private set; }
        public bool isHumanAvatarReady { get; private set; }
        public int rootMotionBoneIndex { get; private set; }
        public Vector3 humanWorldRootPositionCache { get; set; }
        public Quaternion humanWorldRootRotationCache { get; set; }
        public Vector3 animatorWorldRootPositionCache { get; set; }
        public Quaternion animatorWorldRootRotationCache { get; set; }
        public Bounds gameObjectBounds { get; set; }
        public bool prefabMode { get; set; }
        public bool hasDummyObject { get { return true; } } //Allways
        public DummyObject calcObject { get; private set; }
        public OnionSkin onionSkin { get; private set; }
        #endregion

        #region DummyObject
        public GameObject editGameObject { get { return hasDummyObject ? calcObject.gameObject : vaw.gameObject; } }
        public Animator editAnimator { get { return hasDummyObject ? calcObject.animator : vaw.animator; } }
        public GameObject[] editBones { get { return hasDummyObject ? calcObject.bones : bones; } }
        public Dictionary<GameObject, int> editBoneDic { get { return hasDummyObject ? calcObject.boneDic : boneDic; } }
        public GameObject[] editHumanoidBones { get { return hasDummyObject ? calcObject.humanoidBones : humanoidBones; } }

        [Flags]
        public enum EditObjectFlag
        {
            Base = (1 << 0),
            Dummy = (1 << 1),
            Edit = (1 << 2),
            All = Base | Dummy,
        }

        public void GetEditGameObjectHumanPose(ref HumanPose humanPose, EditObjectFlag editFlags = EditObjectFlag.Edit)
        {
            if (!isHuman) return;

            HumanPoseHandler handler = null;
            Transform t = null;
            if (hasDummyObject && (editFlags & (EditObjectFlag.Edit | EditObjectFlag.Dummy)) != 0)
            {
                handler = calcObject.humanPoseHandler;
                t = calcObject.gameObjectTransform;
            }
            else
            {
                handler = humanPoseHandler;
                t = vaw.gameObject.transform;
            }

            TransformPoseSave.SaveData save = new TransformPoseSave.SaveData(t);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            handler.GetHumanPose(ref humanPose);
            save.LoadLocal(t);
        }

        public int EditBonesIndexOf(GameObject go)
        {
            var tmpBoneDic = editBoneDic;
            if (tmpBoneDic != null && go != null)
            {
                int boneIndex;
                if (tmpBoneDic.TryGetValue(go, out boneIndex))
                {
                    return boneIndex;
                }
            }
            return -1;
        }
        #endregion

        #region Current
        public AnimationClip currentClip { get; private set; }
        public float currentTime { get; private set; }
        public bool currentLinkedWithTimeline { get; private set; }
        #endregion
        #region Before
        private bool beforePlaying;
        private AnimationClip beforeClip;
        private float beforeTime;
        private float beforeLength;
        private Tool beforeCurrentTool;
        private bool beforeShowSceneGizmo;
        private EditorWindow beforeMouseOverWindow;
        private EditorWindow beforeFocusedWindow;
        private bool beforeEnableHumanoidFootIK;
        private bool beforeEnableAnimationRiggingIK;
#pragma warning disable 0414
        private bool beforeRemoveStartOffset;
        private Vector3 beforeOffsetPosition;
        private Quaternion beforeOffsetRotation;
        private AnimationClipSettings beforeAnimationClipSettings;
#pragma warning restore 0414
        #endregion

        #region Refresh
        public enum AnimationWindowStateRefreshType
        {
            None,
            CurvesOnly,
            Everything,
        }
        private AnimationWindowStateRefreshType animationWindowRefresh;
        public void SetAnimationWindowRefresh(AnimationWindowStateRefreshType type)
        {
            if (type > animationWindowRefresh)
                animationWindowRefresh = type;
        }
        private bool updateSampleAnimation;
        private bool updatePoseFixAnimation;
        private bool updateStartTransform;
        private bool updateSaveForce;
        #endregion

        #region AnimationWindow
        private List<EditorCurveBinding> animationWindowFilterBindings;
        private bool animationWindowSynchroSelection;
        private List<EditorCurveBinding> animationWindowSynchroSelectionBindings;
        #endregion

        #region CopyPaste
        private enum CopyDataType
        {
            None = -1,
            SelectionPose,
            FullPose,
            AnimatorIKTarget,
            OriginalIKTarget,
        }
        private CopyDataType copyDataType = CopyDataType.None;

        private PoseTemplate copyPaste;

        private class CopyAnimatorIKTargetData
        {
            public AnimatorIKCore.IKTarget ikTarget;
            public bool autoRotation;
            public AnimatorIKCore.AnimatorIKData.SpaceType spaceType;
            public GameObject parent;
            public Vector3 position;
            public Quaternion rotation;
            public float swivelRotation;
        }
        private CopyAnimatorIKTargetData[] copyAnimatorIKTargetData;

        private class CopyOriginalIKTargetData
        {
            public int ikTarget;
            public bool autoRotation;
            public OriginalIKCore.OriginalIKData.SpaceType spaceType;
            public GameObject parent;
            public Vector3 position;
            public Quaternion rotation;
            public float swivel;
        }
        private CopyOriginalIKTargetData[] copyOriginalIKTargetData;
        #endregion

        #region EditorWindow
        public bool clampMuscle;
        public bool autoFootIK;
        public bool mirrorEnable;
        public bool collisionEnable;
        public enum RootCorrectionMode
        {
            Disable,
            Single,
            Full,
            Total
        }
        public RootCorrectionMode rootCorrectionMode;
        #endregion

        #region ControlWindow
        public List<VeryAnimationSaveSettings.SelectionData> selectionSetList;
        #endregion

        #region AnimationWindow
        private EditorWindow autoLockedAnimationWindow;
#if UNITY_2018_3_OR_NEWER
        private Vector3 animationWindowSampleAnimationOverrideCheckPosition;
#endif
        #endregion

        public void OnEnable()
        {
            instance = this;

            edit = false;
#if UNITY_2023_1_OR_NEWER
            uAw = uAw_2018_1 = uAw_2019_1 = uAw_2019_2 = uAw_2020_1 = uAw_2023_1 = new UAnimationWindow_2023_1();
            uAnimationUtility = uAnimationUtility_2018_1 = new UAnimationUtility_2018_1();
#elif UNITY_2020_1_OR_NEWER
            uAw = uAw_2018_1 = uAw_2019_1 = uAw_2019_2 = uAw_2020_1 = new UAnimationWindow_2020_1();
            uAnimationUtility = uAnimationUtility_2018_1 = new UAnimationUtility_2018_1();
#elif UNITY_2019_2_OR_NEWER
            uAw = uAw_2018_1 = uAw_2019_1 = uAw_2019_2 = new UAnimationWindow_2019_2();
            uAnimationUtility = uAnimationUtility_2018_1 = new UAnimationUtility_2018_1();
#elif UNITY_2019_1_OR_NEWER
            uAw = uAw_2018_1 = uAw_2019_1 = new UAnimationWindow_2019_1();
            uAnimationUtility = uAnimationUtility_2018_1 = new UAnimationUtility_2018_1();
#elif UNITY_2018_1_OR_NEWER
            uAw = uAw_2018_1 = new UAnimationWindow_2018_1();
            uAnimationUtility = uAnimationUtility_2018_1 = new UAnimationUtility_2018_1();
#else
            uAw = new UAnimationWindow();
            uAnimationUtility = new UAnimationUtility();
#endif
            uAnimationWindowUtility = new UAnimationWindowUtility();
            uAvatar = new UAvatar();
            uAnimator = new UAnimator();
            uAnimatorControllerTool = new UAnimatorControllerTool();
            uParameterControllerEditor = new UParameterControllerEditor();
            uCurveUtility = new UCurveUtility();
            uRotationCurveInterpolation = new URotationCurveInterpolation();
            uSceneView = new USceneView();

            musclePropertyName = new MusclePropertyName();
#if VERYANIMATION_ANIMATIONRIGGING
            animationRigging = new AnimationRigging();
#endif
            animatorIK = new AnimatorIKCore();
            originalIK = new OriginalIKCore();

            lastTool = Tools.current;

            CreateEditorCurveBindingPropertyNames();

            OnBoneShowFlagsUpdated += UpdateSkeletonShowBoneList;

            InternalEditorUtility.RepaintAllViews();
        }
        public void OnDisable()
        {
            OnBoneShowFlagsUpdated -= UpdateSkeletonShowBoneList;
        }
        public void OnDestroy()
        {
            instance = null;
        }
        public void OnFocus()
        {
            instance = this;    //Measures against the problem that OnEnable may not come when repeating Shift + Space.
        }

        public void Initialize()
        {
            Release();

            UpdateCurrentInfo();

            StopAllRecording();

            edit = true;

            #region AutoLock
            {
                autoLockedAnimationWindow = null;
                if (!uAw.GetLinkedWithTimeline())
                {
                    if (!uAw.GetLock(uAw.instance))
                    {
                        uAw.SetLock(uAw.instance, true);
                        autoLockedAnimationWindow = uAw.instance;
                    }
                }
            }
            #endregion

            beforeCurrentTool = lastTool = Tools.current;
            beforeShowSceneGizmo = false;
            beforeMouseOverWindow = null;
            beforeFocusedWindow = null;
            beforeEnableHumanoidFootIK = false;
            beforeEnableAnimationRiggingIK = false;
            beforeRemoveStartOffset = uAw.GetRemoveStartOffset();
            beforeOffsetPosition = Vector3.zero;
            beforeOffsetRotation = Quaternion.identity;
            beforeAnimationClipSettings = null;

            #region Animator
            UnityEditor.Animations.AnimatorController ac = null;
            if (vaw.animator != null)
            {
                if (!vaw.animator.isInitialized)
                    vaw.animator.Rebind();
                ac = EditorCommon.GetAnimatorController(vaw.animator);
                uAnimatorControllerTool.SetAnimatorController(ac);
                uParameterControllerEditor.SetAnimatorController(ac);
            }
            #endregion

            #region AnimationWindow
            animationWindowFilterBindings = null;
            animationWindowSynchroSelectionBindings = new List<EditorCurveBinding>();
            #endregion

            UpdateBones(true);

            #region PreviewDefaultSettings
            {
                #region AvatarpreviewShowIK
                if (uAw.GetLinkedWithTimeline())
                {
#if VERYANIMATION_TIMELINE
                    EditorPrefs.SetBool("AvatarpreviewShowIK", uAw.GetTimelineAnimationApplyFootIK());
#else
                    Assert.IsTrue(false);
#endif
                }
                else if (vaw.animator != null && ac != null && ac.layers.Length > 0)
                {
                    bool enable = false;
                    if (EditorApplication.isPlaying)
                    {
                        var state = vaw.animator.GetCurrentAnimatorStateInfo(0);
                        var index = ArrayUtility.FindIndex(ac.layers[0].stateMachine.states, (x) => x.state.nameHash == state.shortNameHash);
                        if (index >= 0)
                            enable = ac.layers[0].stateMachine.states[index].state.iKOnFeet;
                    }
                    else
                    {
                        foreach (var layer in ac.layers)
                        {
                            Func<Motion, bool> FindMotion = null;
                            FindMotion = (motion) =>
                            {
                                if (motion != null)
                                {
                                    if (motion is UnityEditor.Animations.BlendTree)
                                    {
                                        var blendTree = motion as UnityEditor.Animations.BlendTree;
                                        foreach (var c in blendTree.children)
                                        {
                                            if (FindMotion(c.motion))
                                                return true;
                                        }
                                    }
                                    else
                                    {
                                        if (motion == currentClip)
                                        {
                                            return true;
                                        }
                                    }
                                }
                                return false;
                            };
                            foreach (var state in layer.stateMachine.states)
                            {
                                if (FindMotion(state.state.motion))
                                {
                                    enable = state.state.iKOnFeet;
                                    break;
                                }
                            }
                        }
                    }
                    EditorPrefs.SetBool("AvatarpreviewShowIK", enable);
                }
                else
                {
                    EditorPrefs.SetBool("AvatarpreviewShowIK", false);
                }
                #endregion
                if (vaw.animator != null)
                {
                    EditorPrefs.SetBool(UAvatarPreview.EditorPrefsApplyRootMotion, uAw.GetLinkedWithTimeline() || vaw.animator.applyRootMotion);
#if VERYANIMATION_ANIMATIONRIGGING
                    EditorPrefs.SetBool(UAvatarPreview.EditorPrefsARConstraint, animationRigging != null && animationRigging.isValid);
#endif
                }
            }
            #endregion

            SelectGameObjectEvent();

            #region gameObjectBounds
            {
                bool first = true;
                var bounds = new Bounds();
                foreach (var renderer in vaw.gameObject.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null)
                        continue;
                    if (first)
                    {
                        bounds = renderer.bounds;
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
                gameObjectBounds = bounds;
            }
            #endregion

#if UNITY_2018_3_OR_NEWER
            prefabMode = PrefabStageUtility.GetCurrentPrefabStage() != null;
#else
            prefabMode = false;
#endif
            InitializeAnimatorRootCorrection();
            InitializeHumanoidFootIK();
            InitializeAnimationPlayable();
            InitializeHandPoseSetList();
            InitializeBlendShapeSetList();

            selectionSetList = new List<VeryAnimationSaveSettings.SelectionData>();

            ResetOnCurveWasModifiedStop();

            SetSynchronizeAnimation(vaw.editorSettings.settingExtraSynchronizeAnimation);

            if (EditorApplication.isPlaying && vaw.playingAnimationClip != null)
            {
                #region SetCurrentClipAndTime
                uAw.SetSelectionAnimationClip(vaw.playingAnimationClip);
                if (vaw.playingAnimationLength > 0f)
                    uAw.SetCurrentTime((vaw.playingAnimationTime / vaw.playingAnimationLength) * vaw.playingAnimationClip.length);
                UpdateCurrentInfo();
                if (vaw.animator != null)
                {
#if UNITY_2019_1_OR_NEWER
                    var t = vaw.gameObject.transform;
                    SampleAnimation(currentTime);
                    var offsetRotation = transformPoseSave.originalRotation * Quaternion.Inverse(t.rotation);
                    var offsetMatrix = transformPoseSave.originalMatrix * t.worldToLocalMatrix;
                    SampleAnimation(0f);
                    t.rotation = offsetRotation * transformPoseSave.originalRotation;
                    t.position = offsetMatrix.MultiplyPoint3x4(transformPoseSave.originalPosition);
                    transformPoseSave = null;
                    UpdateBones(false);
                    SampleAnimation(currentTime);
#else
                    int boneIndex = -1;
                    if (isHuman)
                        boneIndex = humanoidIndex2boneIndex[(int)HumanBodyBones.Hips];
                    else if (rootMotionBoneIndex >= 0)
                        boneIndex = rootMotionBoneIndex;
                    if (boneIndex >= 0)
                    {
                        var t = vaw.gameObject.transform;
                        SampleAnimation(currentTime);
                        var originalTransform = transformPoseSave.GetOriginalTransform(bones[boneIndex].transform);
                        var offsetRotation = originalTransform.rotation * Quaternion.Inverse(bones[boneIndex].transform.rotation);
                        var offsetMatrix = originalTransform.matrix * bones[boneIndex].transform.worldToLocalMatrix;
                        SampleAnimation(0f);
                        t.rotation = offsetRotation * transformPoseSave.originalRotation;
                        t.position = offsetMatrix.MultiplyPoint3x4(transformPoseSave.originalPosition);
                        transformPoseSave = null;
                        UpdateBones(false);
                        SampleAnimation(currentTime);
                    }
#endif
                }
                #endregion
            }

            SetUpdateSampleAnimation();
            updateStartTransform = uAw.GetLinkedWithTimeline();

            Undo.undoRedoPerformed += UndoRedoPerformed;
            AnimationUtility.onCurveWasModified += OnCurveWasModified;
#if UNITY_2018_1_OR_NEWER
            EditorApplication.hierarchyChanged += OnHierarchyWindowChanged;
#else
            EditorApplication.hierarchyWindowChanged += OnHierarchyWindowChanged;
#endif
        }
        public void Release()
        {
            UpdateSyncEditorCurveClip();

            Undo.undoRedoPerformed -= UndoRedoPerformed;
            AnimationUtility.onCurveWasModified -= OnCurveWasModified;
#if UNITY_2018_1_OR_NEWER
            EditorApplication.hierarchyChanged -= OnHierarchyWindowChanged;
#else
            EditorApplication.hierarchyWindowChanged -= OnHierarchyWindowChanged;
#endif

            StopRecording();

            edit = false;
            selectionGameObjects = null;
            selectionBones = null;
            selectionHumanVirtualBones = null;
            selectionMotionTool = false;
            renderers = null;
            isHuman = false;
            animatorApplyRootMotion = false;
            animatorAvatar = null;
            animatorAvatarRoot = null;
            bones = null;
            boneDic = null;
            humanoidBones = null;
            parentBoneIndexes = null;
            boneHierarchyLevels = null;
            boneHierarchyMaxLevel = 0;
            mirrorBoneIndexes = null;
            mirrorBoneData = null;
            mirrorBlendShape = null;
            boneIndex2humanoidIndex = null;
            humanoidIndex2boneIndex = null;
            humanoidConflict = null;
            bonePaths = null;
            bonePathDic = null;
            boneDefaultPose = null;
            boneSaveTransforms = null;
            boneSaveOriginalTransforms = null;
            humanoidMuscleLimit = null;
            humanoidMuscleContains = null;
            humanPoseHandler = null;
            isHumanAvatarReady = false;
            prefabMode = false;

            beforePlaying = false;
            beforeClip = null;
            beforeTime = 0f;
            beforeLength = 0f;

            animationWindowRefresh = AnimationWindowStateRefreshType.None;
            updateSampleAnimation = false;
            updatePoseFixAnimation = false;
            updateStartTransform = false;
            updateSaveForce = false;
            animationWindowFilterBindings = null;
            animationWindowSynchroSelection = false;
            animationWindowSynchroSelectionBindings = null;

            copyDataType = CopyDataType.None;
            copyPaste = null;
            copyAnimatorIKTargetData = null;
            copyOriginalIKTargetData = null;

            boneShowFlags = null;

            editorCurveCacheClip = null;
            editorCurveCacheDic = null;
            editorCurveDelayWriteDic = null;
            editorCurveWasModifiedDic = null;

            if (uAnimationClipEditor != null)
            {
                uAnimationClipEditor.Release();
                uAnimationClipEditor = null;
            }
            if (uAvatarPreview != null)
            {
                uAvatarPreview.Release();
                uAvatarPreview = null;
            }

            if (animatorIK != null)
                animatorIK.Release();   //Not to be null
            if (originalIK != null)
                originalIK.Release();   //Not to be null
#if VERYANIMATION_ANIMATIONRIGGING
            if (animationRigging != null)
                animationRigging.Release();   //Not to be null
#endif
            if (synchronizeAnimation != null)
            {
                synchronizeAnimation.Release();
                synchronizeAnimation = null;
            }

            if (calcObject != null)
            {
                calcObject.Release();
                calcObject = null;
            }
            if (onionSkin != null)
            {
                onionSkin.Release();
                onionSkin = null;
            }

            curvesWasModified.Clear();

            selectionSetList = null;

            ReleaseAnimatorRootCorrection();
            ReleaseHumanoidFootIK();
            ReleaseCollision();
            ReleaseAnimationPlayable();
            ReleaseHandPoseSetList();
            ReleaseBlendShapeSetList();

            #region OriginalSave
            if (transformPoseSave != null)
            {
                transformPoseSave.ResetOriginalTransform();
                transformPoseSave.ResetRootOriginalTransform();
                transformPoseSave = null;
            }
            if (blendShapeWeightSave != null)
            {
                blendShapeWeightSave.ResetOriginalWeight();
                blendShapeWeightSave = null;
            }
            #endregion

            DisableCustomTools();

            if (uAw != null && uAw.GetLinkedWithTimeline())
            {
                uAw.StartPreviewing();
            }

            #region AutoLock
            if (uAw != null && autoLockedAnimationWindow != null)
            {
                uAw.SetLock(autoLockedAnimationWindow, false);
                autoLockedAnimationWindow = null;
            }
            #endregion
        }

        private bool StartRecording()
        {
            bool result = true;
            if (!uAw.GetRecording())
            {
                if (!uAw.GetCanRecord() && uAw.GetPreviewing())
                {
                }
                else
                {
#if VERYANIMATION_TIMELINE
                    if (uAw.GetLinkedWithTimeline() && !uAw.IsTimelineArmedForRecord())
                    {
                        uAw.SetTimelineRecording(false);
                        result = uAw.StartRecording();
                    }
                    else
#endif
                    {
                        result = uAw.StartRecording();
                    }
                }
            }

            #region Unusual error
            if (!result)
            {
                Debug.LogError(Language.GetText(Language.Help.LogAnimationWindowRecordingStartError));
            }
            #endregion

            return result;
        }
        private void StopAllRecording()
        {
            uAw.CleanAnimationModeEvents();

            StopRecording();

            uAw.StopAllRecording();
        }
        public void StopRecording()
        {
            if (uAw == null) return;

            uAw.OnSelectionChange();    //Added to be sure to call StopPreview

#if VERYANIMATION_TIMELINE
            uAw.SetTimelineRecording(false);
            uAw.SetTimelinePreviewMode(false);
#endif
            uAw.StopRecording();
        }

        public void SetCurrentTime(float time)
        {
            uAw.SetCurrentTime(time);
            currentTime = uAw.GetCurrentTime();
        }

        public void UpdateCurrentInfo()
        {
            currentClip = uAw.GetSelectionAnimationClip();
            currentTime = uAw.GetCurrentTime();
            currentLinkedWithTimeline = uAw.GetLinkedWithTimeline();
        }

        public bool isEditError
        {
            get
            {
                return !edit || isError;
            }
        }
        public bool isError
        {
            get
            {
                return getErrorCode < 0;
            }
        }
        public int getErrorCode
        {
            get
            {
                if (uAw == null || uAw.instance == null || !uAw.HasFocus() || currentClip == null)
                    return -1;
                if (vaw == null || vaw.gameObject == null || (vaw.animator == null && vaw.animation == null))
                    return -2;
                if (vaw.animator != null && !vaw.animator.hasTransformHierarchy)
                    return -3;
                if (Application.isPlaying && vaw.animator != null && vaw.animator.runtimeAnimatorController == null)
                    return -4;
                if (Application.isPlaying && vaw.animation != null)
                    return -5;
                if (edit && vaw.gameObject != uAw.GetActiveRootGameObject())
                    return -6;
                if (edit && vaw.animator != null && animatorApplyRootMotion != vaw.animator.applyRootMotion)
                    return -7;
                if (edit && vaw.animator != null && animatorAvatar != vaw.animator.avatar)
                    return -8;
                if (edit && vaw.animator != null && isHuman && !isHumanAvatarReady)
                    return -9;
                if (edit && currentLinkedWithTimeline != uAw.GetLinkedWithTimeline())
                    return -10;
                if (!uAw.GetLinkedWithTimeline())
                {
                    if (!vaw.gameObject.activeInHierarchy)
                        return -110;
                    if (!edit && vaw.animator != null && vaw.animator.runtimeAnimatorController != null && (vaw.animator.runtimeAnimatorController.hideFlags & (HideFlags.DontSave | HideFlags.NotEditable)) != 0)
                        return -112;
                }
#if VERYANIMATION_TIMELINE
                else
                {
                    if (!edit && !vaw.gameObject.activeInHierarchy)
                        return -120;
                    if (!uAw.GetTimelineTrackAssetEditable())
                        return -121;
                    if (Application.isPlaying)
                        return -122;
                    var currentDirector = uAw.GetTimelineCurrentDirector();
                    if (currentDirector != null)
                    {
                        if (!currentDirector.gameObject.activeInHierarchy)
                            return -123;
                        if (!currentDirector.enabled)
                            return -124;
                    }
                    if (!uAw.GetTimelineHasFocus())
                        return -125;
                }
#endif
#if UNITY_2018_3_OR_NEWER
                if (edit && prefabMode != (PrefabStageUtility.GetCurrentPrefabStage() != null))
                    return -140;
                if (vaw.uPrefabStage.GetAutoSave(PrefabStageUtility.GetCurrentPrefabStage()))
                    return -141;
                if (PrefabStageUtility.GetCurrentPrefabStage() != null &&
                    !EditorCommon.IsAncestorObject(uAw.GetActiveRootGameObject(), PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot))
                    return -142;
#endif
                if (edit && VeryAnimationEditorWindow.instance == null)
                    return -1000;
                if (edit && VeryAnimationControlWindow.instance == null)
                    return -1001;
                return 0;
            }
        }

        #region Update
        public void OnInspectorUpdate()
        {
            if (isEditError) return;

            #region AnimationWindowRefresh
            if (animationWindowRefresh != AnimationWindowStateRefreshType.None)
            {
                if (animationWindowRefresh == AnimationWindowStateRefreshType.CurvesOnly)
                {
#if !UNITY_2020_2_OR_NEWER
                   uAw.Repaint();
#endif
                }
                else if (animationWindowRefresh == AnimationWindowStateRefreshType.Everything)
                {
                    uAw.ForceRefresh();
                }
                animationWindowRefresh = AnimationWindowStateRefreshType.None;
            }
            #endregion

            #region SettingChange
            {
                var footIK = IsEnableUpdateHumanoidFootIK();
                var arIK = false;
#if VERYANIMATION_ANIMATIONRIGGING
                arIK = animationRigging.isValid;
#endif
                if (beforeEnableHumanoidFootIK != footIK ||
                    beforeEnableAnimationRiggingIK != arIK)
                {
                    beforeEnableHumanoidFootIK = footIK;
                    beforeEnableAnimationRiggingIK = arIK;
                    UpdateSkeletonShowBoneList();
                }
            }
            if (uAw.GetLinkedWithTimeline())
            {
#if VERYANIMATION_TIMELINE
                {
                    var removeStartOffset = uAw.GetRemoveStartOffset();
                    if (beforeRemoveStartOffset != removeStartOffset)
                    {
                        beforeRemoveStartOffset = removeStartOffset;
                        updateStartTransform = true;
                    }
                }
                {
                    Vector3 offsetPosition;
                    Quaternion offsetRotation;
                    uAw.GetTimelineRootMotionOffsets(out offsetPosition, out offsetRotation);
                    if (beforeOffsetPosition != offsetPosition || beforeOffsetRotation != offsetRotation)
                    {
                        beforeOffsetPosition = offsetPosition;
                        beforeOffsetRotation = offsetRotation;
                        updateStartTransform = true;
                    }
                }
                {
                    var animationClipSettings = AnimationUtility.GetAnimationClipSettings(currentClip);
                    if (beforeAnimationClipSettings == null ||
                        beforeAnimationClipSettings.keepOriginalPositionXZ != animationClipSettings.keepOriginalPositionXZ ||
                        beforeAnimationClipSettings.keepOriginalPositionY != animationClipSettings.keepOriginalPositionY ||
                        beforeAnimationClipSettings.keepOriginalOrientation != animationClipSettings.keepOriginalOrientation ||
                        beforeAnimationClipSettings.orientationOffsetY != animationClipSettings.orientationOffsetY ||
                        beforeAnimationClipSettings.level != animationClipSettings.level)
                    {
                        beforeAnimationClipSettings = animationClipSettings;
                        updateStartTransform = true;
                    }
                }
#else
                Assert.IsTrue(false);
#endif
            }
            #endregion
        }
        public void Update()
        {
            UpdateCurrentInfo();

            if (isEditError) return;

#if Enable_Profiler
            Profiler.BeginSample("****VeryAnimation.Update");
#endif

            bool awForceRefresh = false;

            UpdateSyncEditorCurveClip();

            #region SnapToFrame
            if (!uAw.GetPlaying())
            {
                var snapTime = currentClip != null ? uAw.SnapToFrame(currentTime, currentClip.frameRate) : currentTime;
                if (currentTime != snapTime)
                {
                    SetCurrentTime(snapTime);
                }
            }
            #endregion

            #region RecordingChange
#if Enable_Profiler
            Profiler.BeginSample("RecordingChange");
#endif
            {
                if (!StartRecording())
                {
                    EditorApplication.delayCall += () =>
                    {
                        vaw.Release();
                    };
                    return;
                }
            }
#if Enable_Profiler
            Profiler.EndSample();
#endif
            #endregion

            #region PlayingChange
            if (beforePlaying != uAw.GetPlaying())
            {
                SetUpdateSampleAnimation();

                beforePlaying = uAw.GetPlaying();
            }
            #endregion

            #region ClipChange
#if Enable_Profiler
            Profiler.BeginSample("ClipChange");
#endif
            {
                if (currentClip != null && (currentClip != beforeClip || uAvatarPreview == null || uAnimationClipEditor == null))
                {
                    if (transformPoseSave != null)
                        transformPoseSave.ResetOriginalTransform();
                    if (blendShapeWeightSave != null)
                        blendShapeWeightSave.ResetOriginalWeight();
                    {
                        if (uAnimationClipEditor != null)
                        {
                            uAnimationClipEditor.Release();
                            uAnimationClipEditor = null;
                        }
                        if (uAvatarPreview != null)
                        {
                            var previewDir = uAvatarPreview.PreviewDir;
                            var zoomFactor = uAvatarPreview.ZoomFactor;
                            var playing = uAvatarPreview.playing;
                            uAvatarPreview.Release();
                            uAvatarPreview = new UAvatarPreview(currentClip, vaw.gameObject);
                            uAvatarPreview.SetTime(currentTime);
                            uAvatarPreview.PreviewDir = previewDir;
                            uAvatarPreview.ZoomFactor = zoomFactor;
                            uAvatarPreview.playing = playing;
                        }
                        else
                        {
                            uAvatarPreview = new UAvatarPreview(currentClip, vaw.gameObject);
                            uAvatarPreview.SetTime(currentTime);
                        }
                        uAvatarPreview.onAvatarChange += () =>
                        {
                            if (transformPoseSave != null)
                                transformPoseSave.ResetOriginalTransform();
                            if (blendShapeWeightSave != null)
                                blendShapeWeightSave.ResetOriginalWeight();
                            SetUpdateSampleAnimation();
                        };
                        uAnimationClipEditor = new UAnimationClipEditor(currentClip, uAvatarPreview);
                    }
                    ClearEditorCurveCache();
                    SetUpdateSampleAnimation();
                    SetSynchroIKtargetAll();
                    SetAnimationWindowSynchroSelection();
                    beforeClip = currentClip;
                    beforeTime = -1f;
                    beforeLength = -1f;
                    ToolsReset();
                }
            }
#if Enable_Profiler
            Profiler.EndSample();
#endif
            #endregion

            #region TimeChange
#if Enable_Profiler
            Profiler.BeginSample("TimeChange");
#endif
            {
                if (currentTime != beforeTime)
                {
                    if (!uAw.GetPlaying())
                    {
                        SetUpdateSampleAnimation();
                        SetSynchroIKtargetAll();
                        if (!uAvatarPreview.playing)
                        {
                            uAvatarPreview.SetTime(currentTime);
                        }
                    }
                    if (synchronizeAnimation != null)
                    {
                        synchronizeAnimation.SetTime(currentTime);
                    }
                    beforeTime = currentTime;
                }
            }
#if Enable_Profiler
            Profiler.EndSample();
#endif
            #endregion

            #region LengthChange
#if Enable_Profiler
            Profiler.BeginSample("LengthChange");
#endif
            {
                if (currentClip != null && beforeLength != currentClip.length)
                {
                    if (uAnimationClipEditor != null)
                    {
                        uAnimationClipEditor.Release();
                        uAnimationClipEditor = null;
                    }
                    beforeLength = currentClip.length;
                    if (uAvatarPreview != null)
                        uAnimationClipEditor = new UAnimationClipEditor(currentClip, uAvatarPreview);
                }
            }
#if Enable_Profiler
            Profiler.EndSample();
#endif
            #endregion

            #region GizmoChange
            if (beforeShowSceneGizmo != vaw.IsShowSceneGizmo())
            {
                if (!vaw.IsShowSceneGizmo())
                {
                    onionSkin.Release();
                }
                SetUpdateSampleAnimation();
                SetSynchroIKtargetAll();
                beforeShowSceneGizmo = vaw.IsShowSceneGizmo();
            }
            #endregion

            #region ToolChange
            if (Tools.current == Tool.View)
            {
                if (beforeCurrentTool != Tools.current)
                {
                    beforeCurrentTool = Tools.current;
                    vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.All);
                }
            }
            else if (Tools.current == Tool.None)
            {
                if (beforeCurrentTool != lastTool)
                {
                    SetAnimationWindowSynchroSelection();
                    beforeCurrentTool = lastTool;
                    vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.All);
                }
            }
            else
            {
                if (beforeCurrentTool != Tools.current)
                {
                    SetAnimationWindowSynchroSelection();
                    beforeCurrentTool = Tools.current;
                    EnableCustomTools(Tool.None);
                    vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.All);
                }
            }
            #endregion

            #region WindowChange
            {
                var mouseOverWindow = EditorWindow.mouseOverWindow;
                var focusedWindow = EditorWindow.focusedWindow;
                if (mouseOverWindow != beforeMouseOverWindow || focusedWindow != beforeFocusedWindow)
                {
                    #region SceneView
                    if (mouseOverWindow is SceneView)
                    {
                        if (Tools.current != Tool.View)
                            EnableCustomTools(Tool.None);
                    }
                    #endregion

                    #region AnimationWindow
                    if (mouseOverWindow == uAw.instance || focusedWindow == uAw.instance)
                    {
                        editorCurveCacheDirty = true;
                    }
                    #endregion

                    beforeMouseOverWindow = mouseOverWindow;
                    beforeFocusedWindow = focusedWindow;
                }
            }
            #endregion

            #region AnimationWindow
#if Enable_Profiler
            Profiler.BeginSample("AnimationWindow");
#endif
            if (animationWindowSynchroSelection)
            {
                var animationWindowRefreshDone = uAw.IsDoneRefresh();

                #region Disable AnimationWindow Filter
                if (animationWindowRefreshDone)
                {
                    if (vaw.editorSettings.settingPropertyStyle == EditorSettings.PropertyStyle.Filter)
                    {
                        if (uAw.GetFilterBySelection())
                        {
                            uAw.SetFilterBySelection(false);
                            animationWindowRefreshDone = uAw.IsDoneRefresh();
                        }
                    }
                }
                #endregion

                if (animationWindowRefreshDone)
                {
                    if (EditorWindow.focusedWindow != uAw.instance)
                    {
                        SelectGameObjectEvent();    //UpdateSelection

                        List<EditorCurveBinding> syncBindings = null;
                        if (animationWindowSynchroSelectionBindings.Count > 0)
                            syncBindings = animationWindowSynchroSelectionBindings;
                        else
                            syncBindings = GetSelectionEditorCurveBindings();

                        #region PropertyFilterByBindings
                        if (vaw.editorSettings.settingPropertyStyle == EditorSettings.PropertyStyle.Filter)
                        {
                            if (syncBindings.Count > 0)
                            {
                                var filterBindings = new HashSet<EditorCurveBinding>(syncBindings);
                                Action AddRootTBindings = () =>
                                {
                                    for (int dof = 0; dof < 3; dof++)
                                        filterBindings.Add(AnimationCurveBindingAnimatorRootT[dof]);
                                };
                                Action AddRootQBindings = () =>
                                {
                                    for (int dof = 0; dof < 4; dof++)
                                        filterBindings.Add(AnimationCurveBindingAnimatorRootQ[dof]);
                                };
                                Action AddMotionTBindings = () =>
                                {
                                    for (int dof = 0; dof < 3; dof++)
                                        filterBindings.Add(AnimationCurveBindingAnimatorMotionT[dof]);
                                };
                                Action AddMotionQBindings = () =>
                                {
                                    for (int dof = 0; dof < 4; dof++)
                                        filterBindings.Add(AnimationCurveBindingAnimatorMotionQ[dof]);
                                };
                                Action AddFootIKBindings = () =>
                                {
                                    if (IsEnableUpdateHumanoidFootIK())
                                    {
                                        for (var ik = AnimatorIKIndex.LeftFoot; ik <= AnimatorIKIndex.RightFoot; ik++)
                                        {
                                            for (int dof = 0; dof < 3; dof++)
                                                filterBindings.Add(AnimationCurveBindingAnimatorIkT(ik, dof));
                                            for (int dof = 0; dof < 4; dof++)
                                                filterBindings.Add(AnimationCurveBindingAnimatorIkQ(ik, dof));
                                        }
                                    }
                                };
                                Action<HumanBodyBones> AddMuscleBindings = (humanoidIndex) =>
                                {
                                    for (int dof = 0; dof < 3; dof++)
                                    {
                                        var mi = HumanTrait.MuscleFromBone((int)humanoidIndex, dof);
                                        if (mi < 0)
                                            continue;
                                        filterBindings.Add(AnimationCurveBindingAnimatorMuscle(mi));
                                    }
                                };
                                Action<HumanBodyBones> AddTDofBindings = (humanoidIndex) =>
                                {
                                    if (HumanBonesAnimatorTDOFIndex[(int)humanoidIndex] != null)
                                    {
                                        var tdof = HumanBonesAnimatorTDOFIndex[(int)humanoidIndex].index;
                                        for (int dof = 0; dof < 3; dof++)
                                            filterBindings.Add(AnimationCurveBindingAnimatorTDOF(tdof, dof));
                                    }
                                };
                                Action<int> AddTransformPositionBindings = (boneIndex) =>
                                {
                                    for (int dof = 0; dof < 3; dof++)
                                        filterBindings.Add(AnimationCurveBindingTransformPosition(boneIndex, dof));
                                };
                                Action<int> AddTransformRotationBindings = (boneIndex) =>
                                {
                                    for (int dof = 0; dof < 3; dof++)
                                        filterBindings.Add(AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.Baked));
                                    for (int dof = 0; dof < 3; dof++)
                                        filterBindings.Add(AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.NonBaked));
                                    for (int dof = 0; dof < 4; dof++)
                                        filterBindings.Add(AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.RawQuaternions));
                                    for (int dof = 0; dof < 3; dof++)
                                        filterBindings.Add(AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.RawEuler));
                                };
                                Action<int> AddTransformScaleBindings = (boneIndex) =>
                                {
                                    for (int dof = 0; dof < 3; dof++)
                                        filterBindings.Add(AnimationCurveBindingTransformScale(boneIndex, dof));
                                };
                                #region Bone binding
                                foreach (var binding in syncBindings)
                                {
                                    var boneIndex = GetBoneIndexFromCurveBinding(binding);
                                    if (boneIndex < 0)
                                        continue;
                                    if (isHuman && humanoidConflict[boneIndex])
                                    {
                                        #region Humanoid
                                        if (boneIndex == 0)
                                        {
                                            AddRootTBindings();
                                            AddRootQBindings();
                                            AddFootIKBindings();
                                        }
                                        else if (boneIndex > 0 && boneIndex2humanoidIndex[boneIndex] >= 0)
                                        {
                                            var humanoidIndex = boneIndex2humanoidIndex[boneIndex];
                                            AddMuscleBindings(humanoidIndex);
                                            AddTDofBindings(humanoidIndex);
                                            if (rootCorrectionMode != RootCorrectionMode.Disable)
                                            {
                                                if (IsAnimatorRootCorrectionBone(boneIndex2humanoidIndex[boneIndex]))
                                                {
                                                    AddRootTBindings();
                                                    AddRootQBindings();
                                                }
                                            }
                                            AddFootIKBindings();
                                        }
                                        #endregion
                                    }
                                    else
                                    {
                                        #region Generic & Legacy
                                        AddTransformPositionBindings(boneIndex);
                                        AddTransformRotationBindings(boneIndex);
                                        AddTransformScaleBindings(boneIndex);
                                        #endregion
                                    }
                                }
                                #endregion
                                #region Mirror binding
                                if (mirrorEnable)
                                {
                                    List<EditorCurveBinding> mirrorBindings = new List<EditorCurveBinding>();
                                    foreach (var binding in filterBindings)
                                    {
                                        var mbinding = GetMirrorAnimationCurveBinding(binding);
                                        if (!mbinding.HasValue)
                                            continue;

                                        mirrorBindings.Add(mbinding.Value);
                                    }
                                    foreach (var binding in mirrorBindings)
                                    {
                                        filterBindings.Add(binding);
                                    }
                                }
                                #endregion
                                #region MotionTool
                                if (selectionMotionTool)
                                {
                                    AddMotionTBindings();
                                    AddMotionQBindings();
                                }
                                #endregion
                                #region Path
                                {
                                    var allBindings = AnimationUtility.GetCurveBindings(currentClip).ToList();
                                    allBindings.AddRange(AnimationUtility.GetObjectReferenceCurveBindings(currentClip));
                                    foreach (var binding in allBindings)
                                    {
                                        var boneIndex = GetBoneIndexFromCurveBinding(binding);
                                        if (boneIndex < 0)
                                            continue;
                                        if (!selectionBones.Contains(boneIndex))
                                            continue;

                                        if (binding.type == typeof(Animator) &&
                                            IsAnimatorReservedPropertyName(binding.propertyName))
                                            continue;

                                        filterBindings.Add(binding);
                                    }
                                }
                                #endregion
                                animationWindowFilterBindings = filterBindings.ToList();
                            }
                            else
                            {
                                animationWindowFilterBindings = null;
                            }
                        }
                        else
                        {
                            animationWindowFilterBindings = null;
                        }
                        uAw.PropertySortOrFilterByBindings(animationWindowFilterBindings);
                        #endregion

                        uAw.SynchroCurveSelection(syncBindings);
                    }
                    animationWindowSynchroSelection = false;
                }
                else
                {
                    uAw.Repaint();
                }
            }

#if Enable_Profiler
            Profiler.EndSample();
#endif
            #endregion

            AnimationWindowSampleAnimationOverride();

            #region CurveChange Step1
#if Enable_Profiler
            Profiler.BeginSample("CurveChange Step1");
#endif
            bool rootUpdated = false;
            if (curvesWasModified.Count > 0)
            {
                SetOnCurveWasModifiedStop(true);
                foreach (var pair in curvesWasModified)
                {
                    #region CheckConflictCurve
                    if (pair.Value.deleted == AnimationUtility.CurveModifiedType.CurveModified)
                    {
                        if (pair.Value.binding.type == typeof(Transform))
                        {
                            var boneIndex = GetBoneIndexFromCurveBinding(pair.Value.binding);
                            if (boneIndex >= 0)
                            {
                                if (isHuman && humanoidConflict[boneIndex])
                                {
                                    EditorCommon.ShowNotification("Conflict");
                                    Debug.LogErrorFormat(Language.GetText(Language.Help.LogGenericCurveHumanoidConflictError), editBones[boneIndex].name);
                                    SetEditorCurveCache(pair.Value.binding, null);
                                    continue;
                                }
                                else if (rootMotionBoneIndex >= 0 && boneIndex == 0 &&
                                        (IsTransformPositionCurveBinding(pair.Value.binding) || IsTransformRotationCurveBinding(pair.Value.binding)))
                                {
                                    EditorCommon.ShowNotification("Conflict");
                                    Debug.LogErrorFormat(Language.GetText(Language.Help.LogGenericCurveRootConflictError), editBones[boneIndex].name);
                                    SetEditorCurveCache(pair.Value.binding, null);
                                    continue;
                                }
                            }
                        }
                    }
                    #endregion

                    #region EditorOptions - rootCorrectionMode
                    if (isHuman && rootCorrectionMode != RootCorrectionMode.Disable)
                    {
                        #region DisableAnimatorRootCorrection
                        if (IsAnimatorRootCurveBinding(pair.Value.binding))
                        {
                            if (pair.Value.deleted == AnimationUtility.CurveModifiedType.CurveModified)
                            {
                                DisableAnimatorRootCorrection();
                            }
                            else if (pair.Value.deleted == AnimationUtility.CurveModifiedType.CurveDeleted)
                            {
                                DisableAnimatorRootCorrection();
                            }
                        }
                        #endregion
                    }
                    #endregion

                    #region UpdateIK
                    {
                        AnimatorTDOFIndex tdofIndex;
                        int muscleIndex;
                        if (IsAnimatorRootCurveBinding(pair.Value.binding))
                        {
                            ActionCurrentChangedKeyframes(pair.Value, (curve, keyIndex) =>
                            {
                                if (Mathf.Approximately(currentTime, curve[keyIndex].time))
                                {
                                    SetUpdateIKtargetAll();
                                    if (!rootUpdated && pair.Value.beforeCurve != null)
                                    {
                                        var valueNow = curve.Evaluate(currentTime);
                                        var valueBefore = pair.Value.beforeCurve.Evaluate(currentTime);
                                        rootUpdated = !Mathf.Approximately(valueNow, valueBefore);
                                    }
                                }
                            });
                        }
                        else if ((tdofIndex = GetTDOFIndexFromCurveBinding(pair.Value.binding)) >= 0)
                        {
                            ActionCurrentChangedKeyframes(pair.Value, (curve, keyIndex) =>
                            {
                                if (Mathf.Approximately(currentTime, curve[keyIndex].time))
                                {
                                    SetUpdateIKtargetTdofIndex(tdofIndex);
                                }
                            });
                        }
                        else if ((muscleIndex = GetMuscleIndexFromCurveBinding(pair.Value.binding)) >= 0)
                        {
                            ActionCurrentChangedKeyframes(pair.Value, (curve, keyIndex) =>
                            {
                                if (Mathf.Approximately(currentTime, curve[keyIndex].time))
                                {
                                    SetUpdateIKtargetMuscle(muscleIndex);
                                }
                            });
                        }
                        else if (IsTransformPositionCurveBinding(pair.Value.binding) ||
                                IsTransformRotationCurveBinding(pair.Value.binding) ||
                                IsTransformScaleCurveBinding(pair.Value.binding))
                        {
                            ActionCurrentChangedKeyframes(pair.Value, (curve, keyIndex) =>
                            {
                                if (Mathf.Approximately(currentTime, curve[keyIndex].time))
                                {
                                    var boneIndex = GetBoneIndexFromCurveBinding(pair.Value.binding);
                                    SetUpdateIKtargetBone(boneIndex);
                                }
                            });
                        }
                    }
                    #endregion
                }
                SetOnCurveWasModifiedStop(false);
            }
#if Enable_Profiler
            Profiler.EndSample();
#endif
            #endregion

            #region IK
#if Enable_Profiler
            Profiler.BeginSample("IK");
#endif
            if (GetUpdateIKtargetAll() && !updatePoseFixAnimation)
            {
                if (isHuman)
                {
                    #region Humanoid
                    if (animatorIK.GetUpdateIKtargetAll())
                    {
                        EnableAnimatorRootCorrection(currentTime, currentTime, currentTime);
                        UpdateAnimatorRootCorrection();
                        animatorIK.UpdateIK(rootUpdated);
                    }
                    #endregion
                }
                originalIK.UpdateIK();
                SetUpdateSampleAnimation();
            }
            else if (GetSynchroIKtargetAll())
            {
                SetUpdateSampleAnimation();
            }
#if Enable_Profiler
            Profiler.EndSample();
#endif
            #endregion

            #region CurveChange Step2
#if Enable_Profiler
            Profiler.BeginSample("CurveChange Step2");
#endif
            if (curvesWasModified.Count > 0 && ((isHuman && clampMuscle) || mirrorEnable) && !updatePoseFixAnimation)
            {
                SetOnCurveWasModifiedStop(true);
                foreach (var pair in curvesWasModified)
                {
                    if (pair.Value.deleted == AnimationUtility.CurveModifiedType.CurveModified)
                    {
                        #region EditorOptions - clampMuscle
                        if ((isHuman && clampMuscle))
                        {
                            if (GetMuscleIndexFromCurveBinding(pair.Value.binding) >= 0)
                            {
                                AnimationCurve changedCurve = null;
                                ActionCurrentChangedKeyframes(pair.Value, (curve, keyIndex) =>
                                {
                                    var key = curve[keyIndex];
                                    var clampValue = Mathf.Clamp(key.value, -1f, 1f);
                                    if (key.value != clampValue)
                                    {
                                        key.value = clampValue;
                                        curve.MoveKey(keyIndex, key);
                                        changedCurve = curve;
                                    }
                                });
                                if (changedCurve != null)
                                {
                                    SetEditorCurveCache(pair.Value.binding, changedCurve);
                                }
                            }
                        }
                        #endregion

                        #region EditorOptions - mirrorEnable
                        if (mirrorEnable)
                        {
                            var mbinding = GetMirrorAnimationCurveBinding(pair.Value.binding);
                            if (mbinding.HasValue)
                            {
                                var hash = GetEditorCurveBindingHashCode(mbinding.Value);
                                if (!curvesWasModified.ContainsKey(hash))
                                {
                                    bool updated = false;
                                    var boneIndex = GetBoneIndexFromCurveBinding(pair.Value.binding);
                                    var mcurve = GetEditorCurveCache(mbinding.Value);
                                    if (mcurve == null)
                                    {
                                        #region CreateMirrorCurves
                                        if (IsTransformPositionCurveBinding(pair.Value.binding))
                                        {
                                            SetAnimationValueTransformPosition(mirrorBoneIndexes[boneIndex], GetAnimationValueTransformPosition(mirrorBoneIndexes[boneIndex]));
                                        }
                                        else if (IsTransformRotationCurveBinding(pair.Value.binding))
                                        {
                                            var mode = IsHaveAnimationCurveTransformRotation(boneIndex);
                                            var mmode = IsHaveAnimationCurveTransformRotation(mirrorBoneIndexes[boneIndex]);
                                            if (mmode == URotationCurveInterpolation.Mode.Undefined)
                                            {
                                                SetAnimationValueTransformRotation(mirrorBoneIndexes[boneIndex], GetAnimationValueTransformRotation(mirrorBoneIndexes[boneIndex]));
                                            }
                                            else if (mode == URotationCurveInterpolation.Mode.RawQuaternions && mmode == URotationCurveInterpolation.Mode.RawEuler)
                                            {
                                                EditorCurveBinding[] convertBindings = new EditorCurveBinding[3];
                                                for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                                                {
                                                    convertBindings[dofIndex] = mbinding.Value;
                                                    convertBindings[dofIndex].propertyName = EditorCurveBindingTransformRotationPropertyNames[(int)URotationCurveInterpolation.Mode.RawEuler][dofIndex];
                                                    RemoveEditorCurveCache(convertBindings[dofIndex]);
                                                }
                                                uRotationCurveInterpolation.SetInterpolation(currentClip, convertBindings, URotationCurveInterpolation.Mode.NonBaked);
                                            }
                                            else if (mode == URotationCurveInterpolation.Mode.RawEuler && mmode == URotationCurveInterpolation.Mode.RawQuaternions)
                                            {
                                                {
                                                    EditorCurveBinding[] convertBindings = new EditorCurveBinding[3];
                                                    for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                                                    {
                                                        convertBindings[dofIndex] = mbinding.Value;
                                                        convertBindings[dofIndex].propertyName = EditorCurveBindingTransformRotationPropertyNames[(int)URotationCurveInterpolation.Mode.NonBaked][dofIndex];
                                                        RemoveEditorCurveCache(convertBindings[dofIndex]);
                                                    }
                                                    uRotationCurveInterpolation.SetInterpolation(currentClip, convertBindings, URotationCurveInterpolation.Mode.RawEuler);
                                                }
                                            }
                                        }
                                        else if (IsTransformScaleCurveBinding(pair.Value.binding))
                                        {
                                            SetAnimationValueTransformScale(mirrorBoneIndexes[boneIndex], GetAnimationValueTransformScale(mirrorBoneIndexes[boneIndex]));
                                        }
                                        else
                                        {
                                            var curve = GetEditorCurveCache(pair.Value.binding);
                                            SetEditorCurveCache(mbinding.Value, new AnimationCurve(curve.keys));
                                        }
                                        mcurve = GetEditorCurveCache(mbinding.Value);
                                        #endregion
                                    }
                                    if (mcurve != null)
                                    {
                                        #region RemoveMirrorCurveKeyframe
                                        ActionBeforeChangedKeyframes(pair.Value, (curve, keyIndex) =>
                                        {
                                            var index = FindKeyframeAtTime(mcurve, curve[keyIndex].time);
                                            if (index >= 0)
                                            {
                                                mcurve.RemoveKey(index);
                                                updated = true;
                                            }
                                        });
                                        #endregion

                                        #region UpdateMirrorSyncro
                                        Action UpdateMirrorSyncro = () =>
                                        {
                                            if (isHuman)
                                            {
                                                SetSynchroIKtargetMuscle(GetMuscleIndexFromCurveBinding(mbinding.Value));
                                                SetSynchroIKtargetTdofIndex(GetTDOFIndexFromCurveBinding(mbinding.Value));
                                            }
                                            SetSynchroIKtargetBone(GetBoneIndexFromCurveBinding(mbinding.Value));
                                        };
                                        #endregion

                                        AnimatorTDOFIndex tdofIndex;
                                        if (GetIkTIndexFromCurveBinding(pair.Value.binding) >= 0 ||
                                            GetIkQIndexFromCurveBinding(pair.Value.binding) >= 0)
                                        {
                                            #region IK
                                            ActionCurrentChangedKeyframes(pair.Value, (curve, keyIndex) =>
                                            {
                                                AddHumanoidFootIK(curve[keyIndex].time);
                                            });
                                            #endregion
                                        }
                                        else if ((tdofIndex = GetTDOFIndexFromCurveBinding(pair.Value.binding)) >= 0)
                                        {
                                            #region TDOF
                                            var mtdofIndex = AnimatorTDOFMirrorIndexes[(int)tdofIndex];
                                            if (mtdofIndex != AnimatorTDOFIndex.None)
                                            {
                                                var dof = GetDOFIndexFromCurveBinding(pair.Value.binding);
                                                var mirrorScale = HumanBonesAnimatorTDOFIndex[(int)AnimatorTDOFIndex2HumanBodyBones[(int)mtdofIndex]].mirror;
                                                ActionCurrentChangedKeyframes(pair.Value, (curve, keyIndex) =>
                                                {
                                                    var key = curve[keyIndex];
                                                    key.value *= mirrorScale[dof];
                                                    key.inTangent *= mirrorScale[dof];
                                                    key.outTangent *= mirrorScale[dof];
                                                    SetKeyframe(mcurve, key);
                                                    updated = true;
                                                });
                                            }
                                            #endregion
                                        }
                                        else if (IsTransformPositionCurveBinding(pair.Value.binding))
                                        {
                                            #region Position
                                            LoadTmpCurvesFullDof(mbinding.Value, 3);
                                            LoadTmpSubCurvesFullDof(pair.Value.binding, 3);
                                            ActionCurrentChangedKeyframes(pair.Value, (curve, keyIndex) =>
                                            {
                                                var position = GetAnimationValueTransformPosition(boneIndex, curve[keyIndex].time);
                                                var mposition = GetMirrorBoneLocalPosition(boneIndex, position);
                                                for (int dof = 0; dof < 3; dof++)
                                                {
                                                    if (tmpCurves.curves[dof] == null || tmpCurves.subCurves[dof] == null)
                                                        continue;
                                                    var mkeyIndex = FindKeyframeAtTime(tmpCurves.subCurves[dof], curve[keyIndex].time);
                                                    if (mkeyIndex >= 0)
                                                    {
                                                        var key = tmpCurves.subCurves[dof][mkeyIndex];
                                                        key.value = mposition[dof];
                                                        if (mirrorBoneData[mirrorBoneIndexes[boneIndex]].positionTangentInverse[dof])
                                                        {
                                                            key.inTangent *= -1f;
                                                            key.outTangent *= -1f;
                                                        }
                                                        SetKeyframe(tmpCurves.curves[dof], key);
                                                    }
                                                    else
                                                    {
                                                        SetKeyframe(tmpCurves.curves[dof], curve[keyIndex].time, mposition[dof]);
                                                    }
                                                }
                                                updated = true;
                                            });
                                            if (updated)
                                            {
                                                for (int dof = 0; dof < 3; dof++)
                                                    SetEditorCurveCache(tmpCurves.bindings[dof], tmpCurves.curves[dof]);
                                                updated = false;
                                                UpdateMirrorSyncro();
                                            }
                                            tmpCurves.Clear();
                                            #endregion
                                        }
                                        else if (IsTransformRotationCurveBinding(pair.Value.binding))
                                        {
                                            #region Rotation
                                            if (mbinding.Value.propertyName.StartsWith(URotationCurveInterpolation.PrefixForInterpolation[(int)URotationCurveInterpolation.Mode.RawQuaternions]))
                                            {
                                                LoadTmpCurvesFullDof(mbinding.Value, 4);
                                                LoadTmpSubCurvesFullDof(pair.Value.binding, 4);
                                                ActionCurrentChangedKeyframes(pair.Value, (curve, keyIndex) =>
                                                {
                                                    var localRotation = GetAnimationValueTransformRotation(boneIndex, curve[keyIndex].time);
                                                    var mlocalRotation = GetMirrorBoneLocalRotation(boneIndex, localRotation);
                                                    mlocalRotation = FixReverseRotationQuaternion(tmpCurves.curves, curve[keyIndex].time, mlocalRotation);
                                                    for (int dof = 0; dof < 4; dof++)
                                                    {
                                                        if (tmpCurves.curves[dof] == null || tmpCurves.subCurves[dof] == null)
                                                            continue;
                                                        var mkeyIndex = FindKeyframeAtTime(tmpCurves.subCurves[dof], curve[keyIndex].time);
                                                        if (mkeyIndex >= 0)
                                                        {
                                                            var key = tmpCurves.subCurves[dof][mkeyIndex];
                                                            key.value = mlocalRotation[dof];
                                                            if (mirrorBoneData[mirrorBoneIndexes[boneIndex]].rotationTangentInverse[dof])
                                                            {
                                                                key.inTangent *= -1f;
                                                                key.outTangent *= -1f;
                                                            }
                                                            SetKeyframe(tmpCurves.curves[dof], key);
                                                        }
                                                        else
                                                        {
                                                            SetKeyframe(tmpCurves.curves[dof], curve[keyIndex].time, mlocalRotation[dof]);
                                                        }
                                                    }
                                                    updated = true;
                                                });
                                                if (updated)
                                                {
                                                    for (int dof = 0; dof < 4; dof++)
                                                        SetEditorCurveCache(tmpCurves.bindings[dof], tmpCurves.curves[dof]);
                                                    updated = false;
                                                    UpdateMirrorSyncro();
                                                }
                                                tmpCurves.Clear();
                                            }
                                            else if (mbinding.Value.propertyName.StartsWith(URotationCurveInterpolation.PrefixForInterpolation[(int)URotationCurveInterpolation.Mode.RawEuler]))
                                            {
                                                LoadTmpCurvesFullDof(mbinding.Value, 3);
                                                LoadTmpSubCurvesFullDof(pair.Value.binding, 3);
                                                ActionCurrentChangedKeyframes(pair.Value, (curve, keyIndex) =>
                                                {
                                                    var localRotation = GetAnimationValueTransformRotation(boneIndex, curve[keyIndex].time);
                                                    var mlocalRotation = GetMirrorBoneLocalRotation(boneIndex, localRotation);
                                                    var meulerAngles = FixReverseRotationEuler(tmpCurves.curves, curve[keyIndex].time, mlocalRotation.eulerAngles);
                                                    for (int dof = 0; dof < 3; dof++)
                                                    {
                                                        if (tmpCurves.curves[dof] == null || tmpCurves.subCurves[dof] == null)
                                                            continue;
                                                        var mkeyIndex = FindKeyframeAtTime(tmpCurves.subCurves[dof], curve[keyIndex].time);
                                                        if (mkeyIndex >= 0)
                                                        {
                                                            var key = tmpCurves.subCurves[dof][mkeyIndex];
                                                            key.value = meulerAngles[dof];
                                                            if (mirrorBoneData[mirrorBoneIndexes[boneIndex]].eulerAnglesTangentInverse[dof])
                                                            {
                                                                key.inTangent *= -1f;
                                                                key.outTangent *= -1f;
                                                            }
                                                            SetKeyframe(tmpCurves.curves[dof], key);
                                                        }
                                                        else
                                                        {
                                                            SetKeyframe(tmpCurves.curves[dof], curve[keyIndex].time, meulerAngles[dof]);
                                                        }
                                                    }
                                                    updated = true;
                                                });
                                                if (updated)
                                                {
                                                    for (int dof = 0; dof < 3; dof++)
                                                        SetEditorCurveCache(tmpCurves.bindings[dof], tmpCurves.curves[dof]);
                                                    updated = false;
                                                    UpdateMirrorSyncro();
                                                }
                                                tmpCurves.Clear();
                                            }
                                            #endregion
                                        }
                                        else if (IsTransformScaleCurveBinding(pair.Value.binding))
                                        {
                                            #region Scale
                                            LoadTmpCurvesFullDof(mbinding.Value, 3);
                                            LoadTmpSubCurvesFullDof(pair.Value.binding, 3);
                                            ActionCurrentChangedKeyframes(pair.Value, (curve, keyIndex) =>
                                            {
                                                var scale = GetAnimationValueTransformScale(boneIndex, curve[keyIndex].time);
                                                var mscale = GetMirrorBoneLocalScale(boneIndex, scale);
                                                for (int dof = 0; dof < 3; dof++)
                                                {
                                                    if (tmpCurves.curves[dof] == null || tmpCurves.subCurves[dof] == null)
                                                        continue;
                                                    var mkeyIndex = FindKeyframeAtTime(tmpCurves.subCurves[dof], curve[keyIndex].time);
                                                    if (mkeyIndex >= 0)
                                                    {
                                                        var key = tmpCurves.subCurves[dof][mkeyIndex];
                                                        key.value = mscale[dof];
                                                        if (mirrorBoneData[mirrorBoneIndexes[boneIndex]].scaleTangentInverse[dof])
                                                        {
                                                            key.inTangent *= -1f;
                                                            key.outTangent *= -1f;
                                                        }
                                                        SetKeyframe(tmpCurves.curves[dof], key);
                                                    }
                                                    else
                                                    {
                                                        SetKeyframe(tmpCurves.curves[dof], curve[keyIndex].time, mscale[dof]);
                                                    }
                                                }
                                                updated = true;
                                            });
                                            if (updated)
                                            {
                                                for (int dof = 0; dof < 3; dof++)
                                                    SetEditorCurveCache(tmpCurves.bindings[dof], tmpCurves.curves[dof]);
                                                updated = false;
                                                UpdateMirrorSyncro();
                                            }
                                            tmpCurves.Clear();
                                            #endregion
                                        }
                                        else
                                        {
                                            #region Other (As it is)
                                            ActionCurrentChangedKeyframes(pair.Value, (curve, keyIndex) =>
                                            {
                                                var key = curve[keyIndex];
                                                SetKeyframe(mcurve, key);
                                                updated = true;
                                            });
                                            #endregion
                                        }
                                        if (updated)
                                        {
                                            SetEditorCurveCache(mbinding.Value, mcurve);
                                            updated = false;
                                            UpdateMirrorSyncro();
                                        }
                                    }
                                }
                            }
                        }
                        #endregion
                    }
                    else if (pair.Value.deleted == AnimationUtility.CurveModifiedType.CurveDeleted)
                    {
                        #region EditorOptions - mirrorEnable
                        if (mirrorEnable)
                        {
                            var mbinding = GetMirrorAnimationCurveBinding(pair.Value.binding);
                            if (mbinding.HasValue)
                            {
                                var hash = GetEditorCurveBindingHashCode(mbinding.Value);
                                if (!curvesWasModified.ContainsKey(hash))
                                {
                                    SetEditorCurveCache(mbinding.Value, null);
                                }
                            }
                        }
                        #endregion
                    }
                }
                SetOnCurveWasModifiedStop(false);
            }
#if Enable_Profiler
            Profiler.EndSample();
#endif
            #endregion

            #region CurveChange Step3
#if Enable_Profiler
            Profiler.BeginSample("CurveChange Step3");
#endif
            if (curvesWasModified.Count > 0)
            {
                foreach (var pair in curvesWasModified)
                {
                    #region EditorOptions - rootCorrectionMode
                    if (isHuman && rootCorrectionMode != RootCorrectionMode.Disable)
                    {
                        #region EnableAnimatorRootCorrection
                        {
                            bool updatedMuscle = false;
                            {
                                var muscleIndex = GetMuscleIndexFromCurveBinding(pair.Value.binding);
                                if (muscleIndex >= 0)
                                {
                                    var humanoidIndex = (HumanBodyBones)HumanTrait.BoneFromMuscle(muscleIndex);
                                    if (IsAnimatorRootCorrectionBone(humanoidIndex))
                                    {
                                        updatedMuscle = true;
                                    }
                                }
                            }
                            if (updatedMuscle ||
                                GetTDOFIndexFromCurveBinding(pair.Value.binding) >= 0)
                            {
                                if (pair.Value.deleted == AnimationUtility.CurveModifiedType.CurveModified)
                                {
                                    Action<AnimationCurve, int> ChangedKeyframe = (curve, keyIndex) =>
                                    {
                                        EnableAnimatorRootCorrection(curve, keyIndex);
                                    };
                                    ActionCurrentChangedKeyframes(pair.Value, ChangedKeyframe);
                                    ActionBeforeChangedKeyframes(pair.Value, ChangedKeyframe);
                                }
                                else if (pair.Value.deleted == AnimationUtility.CurveModifiedType.CurveDeleted)
                                {
                                    EnableAnimatorRootCorrection(currentTime, 0f, currentClip.length);
                                }
                            }
                        }
                        #endregion
                    }
                    #endregion

                    #region HumanoidFootIK
                    if (IsEnableUpdateHumanoidFootIK())
                    {
                        if (IsAnimatorRootCurveBinding(pair.Value.binding) ||
                            GetMuscleIndexFromCurveBinding(pair.Value.binding) >= 0 ||
                            GetTDOFIndexFromCurveBinding(pair.Value.binding) >= 0)
                        {
                            ActionCurrentChangedKeyframes(pair.Value, (curve, keyIndex) =>
                            {
                                AddHumanoidFootIK(curve[keyIndex].time);
                            });
                        }
                    }
                    #endregion
                }

                SetUpdateSampleAnimation();
            }
#if Enable_Profiler
            Profiler.EndSample();
#endif
            #endregion

            #region UpdateAnimation
#if Enable_Profiler
            Profiler.BeginSample("UpdateAnimation");
#endif
            bool updateAnimation = false;
            if (!uAw.GetPlaying())
            {
                #region updateStartTransform
                if (updateStartTransform)
                {
                    if (uAw.GetLinkedWithTimeline())
                    {
#if VERYANIMATION_TIMELINE
                        var saveTime = currentTime;
                        SetCurrentTime(0f);
                        {
                            PlayableDirectorEvaluateImmediate();
                            {
                                Vector3 offsetPosition;
                                Quaternion offsetRotation;
                                if (uAw.GetTimelineRootMotionOffsets(out offsetPosition, out offsetRotation))
                                {
                                    vaw.gameObject.transform.localPosition = offsetPosition;
                                    vaw.gameObject.transform.localRotation = offsetRotation;
                                }
                            }
                            transformPoseSave.ChangeStartTransform();
                        }
                        SetCurrentTime(saveTime);
                        updateAnimation = true;
                        SetSynchroIKtargetAll();
#else
                        Assert.IsTrue(false);
#endif
                    }
                    updateStartTransform = false;
                }
                #endregion
                #region updateSampleAnimation
                if (updateSampleAnimation)
                {
                    transformPoseSave.ResetOriginalTransform();
                    blendShapeWeightSave.ResetOriginalWeight();
                    UpdateAnimatorRootCorrection();
#if Enable_Profiler
                    Profiler.BeginSample("UpdateCollisionData");
#endif
                    UpdateCollision();
#if Enable_Profiler
                    Profiler.EndSample();
#endif
                    updateAnimation = true;
                }
                #endregion
                #region FootIK
                if (UpdateHumanoidFootIK())
                {
                    updateAnimation = true;
                }
                #endregion

                if (updateAnimation)
                {
                    if (uAw.GetLinkedWithTimeline())
                    {
                        if (collisionEnable || GetSynchroIKtargetAll())
                        {
                            PlayableDirectorEvaluateImmediate();    //For SaveCollision
                        }
                        PlayableDirectorEvaluate();
                    }

                    #region Save
                    {
                        updateSaveForce |= curvesWasModified.Count > 0;
                        SaveAnimatorRootCorrection(updateSaveForce);
                        SaveCollision(updateSaveForce);
                        updateSaveForce = false;
                    }
                    #endregion

                    SampleAnimation(currentTime);

                    UpdateSynchroIKSet();
                    onionSkin.Update();

                    if (uAvatarPreview != null)
                    {
                        uAvatarPreview.Reset();
                    }
                    if (synchronizeAnimation != null)
                    {
                        synchronizeAnimation.UpdateSameClip(currentClip);
                    }

#if UNITY_2018_3_OR_NEWER
                    if (isHuman)
                        animationWindowSampleAnimationOverrideCheckPosition = humanoidBones[(int)HumanBodyBones.LeftFoot].transform.position;
#endif
                    #region Cache
                    humanWorldRootPositionCache = GetHumanWorldRootPosition();
                    humanWorldRootRotationCache = GetHumanWorldRootRotation();
                    animatorWorldRootPositionCache = GetAnimatorWorldMotionPosition();
                    animatorWorldRootRotationCache = GetAnimatorWorldMotionRotation();
                    #endregion
                }
                updateSampleAnimation = false;

                EndChangeAnimationCurve();
            }

#if Enable_Profiler
            Profiler.EndSample();
#endif
            #endregion

            #region AllCurveCache
#if Enable_Profiler
            Profiler.BeginSample("AllCurveCache");
#endif
            if (editorCurveCacheDirty)
            {
                var bindings = AnimationUtility.GetCurveBindings(currentClip);
                foreach (var binding in bindings)
                {
                    GetEditorCurveCache(binding);
                    if (binding.type == typeof(Transform) &&
                        binding.propertyName == EditorCurveBindingTransformRotationPropertyNames[(int)URotationCurveInterpolation.Mode.RawQuaternions][0])
                    {
                        var tmpBinding = binding;
                        foreach (var propertyName in EditorCurveBindingTransformRotationPropertyNames[(int)URotationCurveInterpolation.Mode.NonBaked])
                        {
                            tmpBinding.propertyName = propertyName;
                            GetEditorCurveCache(tmpBinding);
                        }
                    }
                }
                editorCurveCacheDirty = false;
            }
#if Enable_Profiler
            Profiler.EndSample();
#endif
            #endregion

            #region Repaint
#if Enable_Profiler
            Profiler.BeginSample("Repaint");
#endif
            if (updateAnimation)
            {
                vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.Edit);
                if (uAw.IsShowCurveEditor())
                    SetAnimationWindowRefresh(AnimationWindowStateRefreshType.CurvesOnly);
                if (EditorApplication.isPlaying && EditorApplication.isPaused) //Is there a bug that will not be updated while pausing? Therefore, it forcibly updates it.
                    RendererForceUpdate();
            }
            else
            {
                if (EditorApplication.isPlaying && EditorApplication.isPaused && uAw.GetPlaying())  //Is there a bug that will not be updated while pausing? Therefore, it forcibly updates it.
                    RendererForceUpdate();
            }
            if (awForceRefresh)
            {
                uAw.ForceRefresh();
            }
#if Enable_Profiler
            Profiler.EndSample();
#endif
            #endregion

            #region Clear
#if Enable_Profiler
            Profiler.BeginSample("Clear");
#endif
            {
                curvesWasModified.Clear();  //Do it last
                updatePoseFixAnimation = false;
                ResetUpdateIKtargetAll();
                ResetSynchroIKtargetAll();
            }
#if Enable_Profiler
            Profiler.EndSample();
#endif
            #endregion

            UpdateSyncEditorCurveClip();

#if Enable_Profiler
            Profiler.EndSample();
#endif
        }

        private void UpdateBones(bool initialize)
        {
#if UNITY_2019_1_OR_NEWER
            if (!uAw.GetLinkedWithTimeline())
                uAw_2019_1.DestroyPlayableGraph();
            if (animationPlayable != null)
                animationPlayable.Release();
#endif

            #region Reload
            List<GameObject> reloadShowBones = null;
            Dictionary<GameObject, GameObject> reloadMirrorBonePaths = null;
            VeryAnimationSaveSettings.AnimatorIKData[] reloadAnimatorIKData = null;
            VeryAnimationSaveSettings.OriginalIKData[] reloadOriginalIKData = null;
            if (!initialize)
            {
                reloadShowBones = GetReloadBoneShowFlags();
                reloadMirrorBonePaths = GetReloadBonesMirror();
                if (animatorIK != null)
                    reloadAnimatorIKData = animatorIK.SaveIKSaveSettings();
                if (originalIK != null)
                    reloadOriginalIKData = originalIK.SaveIKSaveSettings();
            }
            #endregion

            #region OriginalSave
            if (transformPoseSave != null)
            {
                transformPoseSave.ResetOriginalTransform();
                transformPoseSave.ResetRootOriginalTransform();
                transformPoseSave = null;
            }
            transformPoseSave = new TransformPoseSave(vaw.gameObject);
            transformPoseSave.CreateExtraTransforms();

            if (blendShapeWeightSave != null)
            {
                blendShapeWeightSave.ResetOriginalWeight();
                blendShapeWeightSave = null;
            }
            blendShapeWeightSave = new BlendShapeWeightSave(vaw.gameObject);
            blendShapeWeightSave.CreateExtraValues();
            #endregion

            renderers = vaw.gameObject.GetComponentsInChildren<Renderer>(true);
            isHuman = vaw.animator != null && vaw.animator.isHuman;
            animatorApplyRootMotion = vaw.animator != null && vaw.animator.applyRootMotion;
            animatorAvatar = vaw.animator != null ? vaw.animator.avatar : null;
            animatorAvatarRoot = vaw.animator != null ? uAnimator.GetAvatarRoot(vaw.animator) : null;
            #region Humanoid
            if (isHuman)
            {
                vaw.animator.Rebind();

                humanoidBones = new GameObject[HumanTrait.BoneCount];
                humanoidMuscleLimit = new UAvatar.MuscleLimit[HumanTrait.BoneCount];
                humanoidMuscleContains = new bool[HumanTrait.MuscleCount];
                for (int bone = 0; bone < HumanTrait.BoneCount; bone++)
                {
                    var t = vaw.animator.GetBoneTransform((HumanBodyBones)bone);
                    if (t != null)
                    {
                        humanoidBones[bone] = t.gameObject;
                    }
                    humanoidMuscleLimit[bone] = uAvatar.GetMuscleLimitNonError(animatorAvatar, (HumanBodyBones)bone);
                }
                humanoidHasLeftHand = uAvatar.GetHasLeftHand(animatorAvatar);
                humanoidHasRightHand = uAvatar.GetHasRightHand(animatorAvatar);
                humanoidHasTDoF = uAvatar.GetHasTDoF(animatorAvatar);
                humanoidPreHipRotationInverse = Quaternion.Inverse(GetAvatarPreRotation(HumanBodyBones.Hips));
                humanoidPostHipRotation = GetAvatarPostRotation(HumanBodyBones.Hips);
                humanoidLeftLowerLegLengthSq = Mathf.Pow(uAvatar.GetAxisLength(animatorAvatar, (int)HumanBodyBones.LeftLowerLeg), 2f);
                for (int mi = 0; mi < HumanTrait.MuscleCount; mi++)
                {
                    bool flag = false;
                    var humanoidIndex = (HumanBodyBones)HumanTrait.BoneFromMuscle(mi);
                    if (humanoidIndex >= 0)
                    {
                        if (humanoidIndex >= HumanBodyBones.LeftThumbProximal && humanoidIndex <= HumanBodyBones.LeftLittleDistal && humanoidHasLeftHand)
                            flag = true;
                        else if (humanoidIndex >= HumanBodyBones.RightThumbProximal && humanoidIndex <= HumanBodyBones.RightLittleDistal && humanoidHasRightHand)
                            flag = true;
                        else
                            flag = humanoidBones[(int)humanoidIndex] != null || HumanVirtualBones[(int)humanoidIndex] != null;
                    }
                    humanoidMuscleContains[mi] = flag;
                }
                humanPoseHandler = new HumanPoseHandler(animatorAvatar, animatorAvatarRoot);
                #region Avoiding Unity's bug
                {
                    //Hips You need to call SetHumanPose once if there is a scale in the top. Otherwise, the result of GetHumanPose becomes abnormal.
                    var hp = new HumanPose()
                    {
                        bodyPosition = new Vector3(0f, 1f, 0f),
                        bodyRotation = Quaternion.identity,
                        muscles = new float[HumanTrait.MuscleCount],
                    };
                    humanPoseHandler.SetHumanPose(ref hp);
                }
                #endregion

                #region CheckHumanoid
                {
                    isHumanAvatarReady = true;
                    var humanoidBonesSet = uAvatar.GetHumanoidBonesSet(animatorAvatar);
                    Action<HumanBodyBones> CheckHumanoidBone = (humanoidIndex) =>
                    {
                        if (humanoidBonesSet[(int)humanoidIndex] && humanoidBones[(int)humanoidIndex] == null)
                        {
                            isHumanAvatarReady = false;
                            Debug.LogErrorFormat(Language.GetText(Language.Help.LogAvatarHumanoidSetBoneError), humanoidIndex);
                        }
                    };
                    for (var humanoidIndex = HumanBodyBones.Hips; humanoidIndex <= HumanBodyBones.Jaw; humanoidIndex++)
                        CheckHumanoidBone(humanoidIndex);
                    CheckHumanoidBone(HumanBodyBones.UpperChest);
                    if (humanoidHasLeftHand)
                    {
                        for (var humanoidIndex = HumanBodyBones.LeftThumbProximal; humanoidIndex <= HumanBodyBones.LeftLittleDistal; humanoidIndex++)
                            CheckHumanoidBone(humanoidIndex);
                    }
                    if (humanoidHasRightHand)
                    {
                        for (var humanoidIndex = HumanBodyBones.RightThumbProximal; humanoidIndex <= HumanBodyBones.RightLittleDistal; humanoidIndex++)
                            CheckHumanoidBone(humanoidIndex);
                    }
                }
                #endregion
            }
            else
            {
                humanoidBones = null;
                humanoidMuscleLimit = null;
                humanoidHasLeftHand = false;
                humanoidHasRightHand = false;
                humanoidHasTDoF = false;
                humanoidPreHipRotationInverse = Quaternion.identity;
                humanoidPostHipRotation = Quaternion.identity;
                humanoidLeftLowerLegLengthSq = 0f;
                humanoidMuscleContains = null;
                humanPoseHandler = null;
                isHumanAvatarReady = false;
            }
            #endregion
            #region bones
            bones = EditorCommon.GetHierarchyGameObject(vaw.gameObject).ToArray();
            boneDic = new Dictionary<GameObject, int>(bones.Length);
            for (int i = 0; i < bones.Length; i++)
            {
                boneDic.Add(bones[i], i);
            }
            #endregion
            #region boneIndex2humanoidIndex, humanoidIndex2boneIndex
            if (isHuman)
            {
                boneIndex2humanoidIndex = new HumanBodyBones[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    boneIndex2humanoidIndex[i] = (HumanBodyBones)EditorCommon.ArrayIndexOf(humanoidBones, bones[i]);
                humanoidIndex2boneIndex = new int[HumanTrait.BoneCount];
                for (int i = 0; i < humanoidBones.Length; i++)
                    humanoidIndex2boneIndex[i] = EditorCommon.ArrayIndexOf(bones, humanoidBones[i]);
            }
            else
            {
                boneIndex2humanoidIndex = null;
                humanoidIndex2boneIndex = null;
            }
            #endregion
            #region bonePaths, bonePathDic, boneDefaultPose, boneSaveTransforms, boneSaveOriginalTransforms
            bonePaths = new string[bones.Length];
            bonePathDic = new Dictionary<string, int>(bonePaths.Length);
            boneSaveTransforms = new TransformPoseSave.SaveData[bones.Length];
            boneSaveOriginalTransforms = new TransformPoseSave.SaveData[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bonePaths[i] = AnimationUtility.CalculateTransformPath(bones[i].transform, vaw.gameObject.transform);
                if (!bonePathDic.ContainsKey(bonePaths[i]))
                {
                    bonePathDic.Add(bonePaths[i], i);
                }
                else
                {
                    Debug.LogWarningFormat(Language.GetText(Language.Help.LogMultipleGameObjectsWithSameName), bonePaths[i]);
                }
                boneSaveTransforms[i] = transformPoseSave.GetBindTransform(bones[i].transform);
                if (boneSaveTransforms[i] == null)
                    boneSaveTransforms[i] = transformPoseSave.GetPrefabTransform(bones[i].transform);
                if (boneSaveTransforms[i] == null)
                    boneSaveTransforms[i] = transformPoseSave.GetOriginalTransform(bones[i].transform);
                Assert.IsNotNull(boneSaveTransforms[i]);
                boneSaveOriginalTransforms[i] = transformPoseSave.GetOriginalTransform(bones[i].transform);
                Assert.IsNotNull(boneSaveOriginalTransforms[i]);
            }
            if (animatorAvatar != null)
            {
                boneDefaultPose = new UAvatar.Transform[bones.Length];
                var defaultPose = uAvatar.GetDefaultPose(animatorAvatar);
                foreach (var pair in defaultPose)
                {
                    int boneIndex;
                    if (bonePathDic.TryGetValue(pair.Key, out boneIndex))
                        boneDefaultPose[boneIndex] = pair.Value;
                }
            }
            else
            {
                boneDefaultPose = null;
            }
            if (isHuman)
            {
                HumanPose humanPose = new HumanPose();
                GetEditGameObjectHumanPose(ref humanPose, EditObjectFlag.Base);
                saveHumanPose = humanPose;
            }
            #endregion
            #region rootMotionBoneIndex
            rootMotionBoneIndex = -1;
            if (vaw.animator != null)
            {
                if (vaw.animator.isHuman)
                {
                    rootMotionBoneIndex = 0;
                }
                else
                {
                    var genericRootMotionBonePath = uAvatar.GetGenericRootMotionBonePath(animatorAvatar);
                    if (!string.IsNullOrEmpty(genericRootMotionBonePath))
                    {
                        int boneIndex;
                        if (bonePathDic.TryGetValue(genericRootMotionBonePath, out boneIndex))
                        {
                            rootMotionBoneIndex = boneIndex;
                        }
                    }
                }
            }
            #endregion
            #region parentBone
            {
                parentBoneIndexes = new int[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i].transform.parent != null)
                        parentBoneIndexes[i] = BonesIndexOf(bones[i].transform.parent.gameObject);
                    else
                        parentBoneIndexes[i] = -1;
                }
            }
            #endregion
            #region boneHierarchyLevels
            {
                boneHierarchyLevels = new int[bones.Length];
                boneHierarchyMaxLevel = 0;
                for (int i = 0; i < bones.Length; i++)
                {
                    var parentIndex = parentBoneIndexes[i];
                    while (parentIndex >= 0)
                    {
                        parentIndex = parentBoneIndexes[parentIndex];
                        boneHierarchyLevels[i]++;
                    }
                    boneHierarchyMaxLevel = Math.Max(boneHierarchyMaxLevel, boneHierarchyLevels[i]);
                }
            }
            #endregion
            #region humanoidConflict
            if (isHuman)
            {
                humanoidConflict = new bool[bones.Length];
                Action<int> SetHumanoidConflict = null;
                SetHumanoidConflict = (index) =>
                {
                    if (index < 0) return;
                    humanoidConflict[index] = true;
                    if (parentBoneIndexes[index] >= 0)
                        SetHumanoidConflict(parentBoneIndexes[index]);
                };
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.Head]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.Jaw]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.LeftHand]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.LeftThumbDistal]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.LeftIndexDistal]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.LeftMiddleDistal]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.LeftRingDistal]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.LeftLittleDistal]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.RightHand]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.RightThumbDistal]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.RightIndexDistal]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.RightMiddleDistal]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.RightRingDistal]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.RightLittleDistal]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.LeftFoot]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.RightFoot]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.LeftToes]);
                SetHumanoidConflict(humanoidIndex2boneIndex[(int)HumanBodyBones.RightToes]);
                foreach (var index in humanoidIndex2boneIndex)
                {
                    if (index >= 0)
                        humanoidConflict[index] = true;
                }
            }
            else
            {
                humanoidConflict = null;
            }
            #endregion
            #region BlendShapeConflictCheck
            foreach (var renderer in renderers)
            {
                var smRenderer = renderer as SkinnedMeshRenderer;
                if (smRenderer == null)
                    continue;
                var mesh = smRenderer.sharedMesh;
                if (mesh == null || mesh.blendShapeCount == 0) continue;
                List<string> list = new List<string>(mesh.blendShapeCount);
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    var name = mesh.GetBlendShapeName(i);
                    if (list.Contains(name))
                        Debug.LogWarningFormat(Language.GetText(Language.Help.LogMultipleBlendShapesWithSameName), mesh.name, name);
                    list.Add(name);
                }
            }
            #endregion

            #region AnimationRigging
#if VERYANIMATION_ANIMATIONRIGGING
            {
                if (animationRigging == null)
                    animationRigging = new AnimationRigging();
                animationRigging.Initialize();
            }
#endif
            #endregion

            #region calcObject
            {
                if (calcObject != null)
                {
                    calcObject.Release();
                    calcObject = null;
                }
                calcObject = new DummyObject();
                calcObject.Initialize(vaw.gameObject);
                calcObject.AddEditComponent();
                calcObject.SetRendererEnabled(false);
                calcObject.SetTransformStart();
            }
            #endregion

            #region AnimatorIK
            {
                if (animatorIK == null)
                    animatorIK = new AnimatorIKCore();
                animatorIK.Initialize();
            }
            #endregion

            #region OriginalIK
            {
                if (originalIK == null)
                    originalIK = new OriginalIKCore();
                originalIK.Initialize();
            }
            #endregion

            #region OnionSkin
            {
                if (onionSkin != null)
                {
                    onionSkin.Release();
                    onionSkin = null;
                }
                onionSkin = new OnionSkin();
            }
            #endregion

            #region Preview
            if (uAvatarPreview != null)
            {
                uAvatarPreview.Release();
                uAvatarPreview = null;
            }
            #endregion

            IKUpdateBones();

            if (initialize)
            {
                InitializeBoneShowFlags();
                BonesMirrorAutomap();
                BlendShapeMirrorAutomap();
            }
            else
            {
                SetReloadBoneShowFlags(reloadShowBones);
                SetReloadBonesMirror(reloadMirrorBonePaths);
                animatorIK.LoadIKSaveSettings(reloadAnimatorIKData);
                originalIK.LoadIKSaveSettings(reloadOriginalIKData);
            }
            UpdateSkeletonShowBoneList();
        }

        public void BonesMirrorInitialize()
        {
            mirrorBoneIndexes = new int[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                mirrorBoneIndexes[i] = -1;

                #region Humanoid
                if (isHuman)
                {
                    var humanoidIndex = boneIndex2humanoidIndex[i];
                    if (humanoidIndex >= 0)
                    {
                        var mhi = HumanBodyMirrorBones[(int)humanoidIndex];
                        if (mhi >= 0)
                        {
                            mirrorBoneIndexes[i] = BonesIndexOf(humanoidBones[(int)mhi]);
                        }
                    }
                }
                #endregion
            }

            UpdateBonesMirrorOther();
        }
        public void BonesMirrorAutomap()
        {
            BonesMirrorInitialize();

            #region Name
            if (vaw.editorSettings.settingGenericMirrorName)
            {
                var boneLRIgnorePaths = new string[bones.Length];
                {
                    {
                        var splits = !string.IsNullOrEmpty(vaw.editorSettings.settingGenericMirrorNameDifferentCharacters) ? vaw.editorSettings.settingGenericMirrorNameDifferentCharacters.Split(new string[] { "," }, System.StringSplitOptions.RemoveEmptyEntries) : new string[0];
                        for (int i = 0; i < bones.Length; i++)
                        {
                            boneLRIgnorePaths[i] = bonePaths[i];
                            foreach (var split in splits)
                            {
                                boneLRIgnorePaths[i] = Regex.Replace(boneLRIgnorePaths[i], split, "*", RegexOptions.IgnoreCase);
                            }
                        }
                    }
                    if (vaw.editorSettings.settingGenericMirrorNameIgnoreCharacter && !string.IsNullOrEmpty(vaw.editorSettings.settingGenericMirrorNameIgnoreCharacterString))
                    {
                        for (int i = 0; i < bones.Length; i++)
                        {
                            var splits = boneLRIgnorePaths[i].Split(new string[] { "/" }, System.StringSplitOptions.RemoveEmptyEntries);
                            if (splits.Length <= 0) continue;
                            for (int j = 0; j < splits.Length; j++)
                            {
                                var index = splits[j].IndexOf(vaw.editorSettings.settingGenericMirrorNameIgnoreCharacterString);
                                if (index < 0) continue;
                                splits[j] = splits[j].Remove(0, (index + 1));
                            }
                            boneLRIgnorePaths[i] = string.Join("/", splits);
                        }
                    }
                }
                {
                    bool[] doneFlag = new bool[bones.Length];
                    for (int i = 0; i < bones.Length; i++)
                    {
                        if (doneFlag[i])
                            continue;
                        doneFlag[i] = true;

                        if (mirrorBoneIndexes[i] < 0)
                        {
                            for (int j = 0; j < bones.Length; j++)
                            {
                                if (i == j || boneLRIgnorePaths[i] != boneLRIgnorePaths[j])
                                    continue;
                                if (isHuman)
                                {
                                    if (boneIndex2humanoidIndex[j] >= 0)
                                        continue;
                                }
                                var rootIndex = GetMirrorRootNode(i, j);
                                if (rootIndex < 0)
                                    continue;
                                mirrorBoneIndexes[i] = j;
                                mirrorBoneIndexes[mirrorBoneIndexes[i]] = i;
                                doneFlag[mirrorBoneIndexes[i]] = true;
                                break;
                            }
                        }
                    }
                }
            }
            #endregion

            UpdateBonesMirrorOther();
        }
        private Dictionary<GameObject, GameObject> GetReloadBonesMirror()
        {
            var reload = new Dictionary<GameObject, GameObject>();
            for (int i = 0; i < mirrorBoneIndexes.Length; i++)
            {
                if (mirrorBoneIndexes[i] < 0)
                    continue;
                if (bones[i] == null || bones[mirrorBoneIndexes[i]] == null)
                    continue;
                reload.Add(bones[i], bones[mirrorBoneIndexes[i]]);
            }
            return reload;
        }
        private void SetReloadBonesMirror(Dictionary<GameObject, GameObject> reload)
        {
            BonesMirrorInitialize();
            foreach (var pair in reload)
            {
                int boneIndex = BonesIndexOf(pair.Key);
                if (boneIndex >= 0)
                {
                    int mirrorBoneIndex = BonesIndexOf(pair.Value);
                    if (mirrorBoneIndex >= 0)
                    {
                        mirrorBoneIndexes[boneIndex] = mirrorBoneIndex;
                    }
                }
            }
            UpdateBonesMirrorOther();
        }
        private void UpdateBonesMirrorOther()
        {
            #region Other
            mirrorBoneData = new MirrorBoneData[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                if (mirrorBoneIndexes[i] < 0)
                    continue;
                mirrorBoneData[i] = new MirrorBoneData();

                mirrorBoneData[i].rootBoneIndex = GetMirrorRootNode(i, mirrorBoneIndexes[i]);

                #region position
                {
                    mirrorBoneData[i].positionTangentInverse = new bool[3];
                    var zeroPosition = boneSaveTransforms[i].localPosition;
                    var mirrorZeroPosition = GetMirrorBoneLocalPosition(i, zeroPosition);
                    for (int dof = 0; dof < 3; dof++)
                    {
                        var plusPosition = zeroPosition;
                        plusPosition[dof] += 1f;
                        var mirrorPlusPosition = GetMirrorBoneLocalPosition(i, plusPosition);
                        mirrorBoneData[i].positionTangentInverse[dof] = Math.Sign((mirrorPlusPosition[dof] - mirrorZeroPosition[dof]) * (plusPosition[dof] - zeroPosition[dof])) < 0;
                    }
                }
                #endregion
                #region rotation
                {
                    mirrorBoneData[i].rotationTangentInverse = new bool[4];
                    var zeroRotation = boneSaveTransforms[i].localRotation;
                    var mirrorZeroRotation = GetMirrorBoneLocalRotation(i, zeroRotation);
                    for (int dof = 0; dof < 4; dof++)
                    {
                        var plusRotation = zeroRotation;
                        {
                            plusRotation[dof] += 1f * Mathf.Deg2Rad;
                            var tmp = new Vector4(plusRotation.x, plusRotation.y, plusRotation.z, plusRotation.w).normalized;
                            plusRotation = new Quaternion(tmp.x, tmp.y, tmp.z, tmp.w);
                        }
                        var mirrorPlusRotation = GetMirrorBoneLocalRotation(i, plusRotation);
                        mirrorBoneData[i].rotationTangentInverse[dof] = Math.Sign((mirrorPlusRotation[dof] - mirrorZeroRotation[dof]) * (plusRotation[dof] - zeroRotation[dof])) < 0;
                    }
                }
                #endregion
                #region eulerAngles
                {
                    mirrorBoneData[i].eulerAnglesTangentInverse = new bool[3];
                    var zeroRotation = boneSaveTransforms[i].localRotation;
                    var mirrorZeroRotation = GetMirrorBoneLocalRotation(i, boneSaveTransforms[i].localRotation);
                    for (int dof = 0; dof < 3; dof++)
                    {
                        var plusRotation = zeroRotation;
                        {
                            var add = Vector3.zero;
                            add[dof] += 1f;
                            plusRotation = Quaternion.Euler(add) * plusRotation;
                        }
                        var mirrorPlusRotation = GetMirrorBoneLocalRotation(i, plusRotation);

                        Func<Quaternion, Vector3> ToEulerAngles = (rot) =>
                        {
                            var euler = rot.eulerAngles;
                            for (int k = 0; k < 3; k++)
                            {
                                if (euler[k] > 180f)
                                    euler[k] = euler[k] - 360f;
                            }
                            return euler;
                        };
                        var zeroRotationE = ToEulerAngles(zeroRotation);
                        var mirrorZeroRotationE = ToEulerAngles(mirrorZeroRotation);
                        var plusRotationE = ToEulerAngles(plusRotation);
                        var mirrorPlusRotationE = ToEulerAngles(mirrorPlusRotation);
                        mirrorBoneData[i].eulerAnglesTangentInverse[dof] = Math.Sign((mirrorPlusRotationE[dof] - mirrorZeroRotationE[dof]) * (plusRotationE[dof] - zeroRotationE[dof])) < 0;
                    }
                }
                #endregion
                #region scale
                {
                    mirrorBoneData[i].scaleTangentInverse = new bool[3];
                    var zeroScale = boneSaveTransforms[i].localScale;
                    var mirrorZeroScale = GetMirrorBoneLocalScale(i, zeroScale);
                    for (int dof = 0; dof < 3; dof++)
                    {
                        var plusScale = zeroScale;
                        plusScale[dof] += 1f;
                        var mirrorPlusScale = GetMirrorBoneLocalScale(i, plusScale);
                        mirrorBoneData[i].scaleTangentInverse[dof] = Math.Sign((mirrorPlusScale[dof] - mirrorZeroScale[dof]) * (plusScale[dof] - zeroScale[dof])) < 0;
                    }
                }
                #endregion
            }
            #endregion
        }
        private int GetMirrorRootNode(int b1, int b2)
        {
            if (b1 < 0 || b2 < 0)
                return -1;

            var b1s = b1;
            while (parentBoneIndexes[b1s] >= 0)
            {
                var b2s = b2;
                while (parentBoneIndexes[b2s] >= 0)
                {
                    if (parentBoneIndexes[b1s] == parentBoneIndexes[b2s])
                    {
                        return parentBoneIndexes[b1s];
                    }
                    b2s = parentBoneIndexes[b2s];
                }
                b1s = parentBoneIndexes[b1s];
            }
            return -1;
        }
        public void ChangeBonesMirror(int boneIndex, int mirrorBoneIndex)
        {
            Action<int, int> ActionChildren = null;
            ActionChildren = (bi, mbi) =>
            {
                if (boneIndex < 0)
                    return;

                mirrorBoneIndexes[bi] = mbi;

                #region ParentCheck
                if (mirrorBoneIndexes[bi] >= 0)
                {
                    var index = bi;
                    while (parentBoneIndexes[index] >= 0)
                    {
                        if (mbi == parentBoneIndexes[index])
                        {
                            mirrorBoneIndexes[bi] = -1;
                            break;
                        }
                        index = parentBoneIndexes[index];
                    }
                }
                if (mirrorBoneIndexes[bi] >= 0)
                {
                    var index = mbi;
                    while (parentBoneIndexes[index] >= 0)
                    {
                        if (bi == parentBoneIndexes[index])
                        {
                            mirrorBoneIndexes[bi] = -1;
                            break;
                        }
                        index = parentBoneIndexes[index];
                    }
                }
                #endregion

                #region RootCheck
                if (mirrorBoneIndexes[bi] >= 0)
                {
                    if (GetMirrorRootNode(bi, mbi) < 0)
                    {
                        mirrorBoneIndexes[bi] = -1;
                    }
                }
                #endregion

                if (mirrorBoneIndexes[bi] >= 0)
                {
                    mirrorBoneIndexes[mirrorBoneIndexes[bi]] = bi;
                    {
                        var t = bones[bi].transform;
                        var mt = bones[mirrorBoneIndexes[bi]].transform;
                        if (t.childCount == mt.childCount)
                        {
                            for (int i = 0; i < t.childCount; i++)
                            {
                                var ci = BonesIndexOf(t.GetChild(i).gameObject);
                                var mci = BonesIndexOf(mt.GetChild(i).gameObject);
                                ActionChildren(ci, mci);
                            }
                        }
                    }
                }
            };
            ActionChildren(boneIndex, mirrorBoneIndex);

            UpdateBonesMirrorOther();
        }

        public void BlendShapeMirrorInitialize()
        {
            mirrorBlendShape = new Dictionary<SkinnedMeshRenderer, Dictionary<string, string>>();
        }
        public void BlendShapeMirrorAutomap()
        {
            BlendShapeMirrorInitialize();

            if (vaw.editorSettings.settingBlendShapeMirrorName)
            {
                foreach (var renderer in vaw.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount <= 0)
                        continue;
                    var nameTable = new Dictionary<string, string>();
                    {
                        var nameLRIgnorePaths = new string[renderer.sharedMesh.blendShapeCount];
                        {
                            var splits = !string.IsNullOrEmpty(vaw.editorSettings.settingBlendShapeMirrorNameDifferentCharacters) ? vaw.editorSettings.settingBlendShapeMirrorNameDifferentCharacters.Split(new string[] { "," }, System.StringSplitOptions.RemoveEmptyEntries) : new string[0];
                            for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                            {
                                nameLRIgnorePaths[i] = renderer.sharedMesh.GetBlendShapeName(i);
                                foreach (var split in splits)
                                {
                                    nameLRIgnorePaths[i] = Regex.Replace(nameLRIgnorePaths[i], split, "*", RegexOptions.IgnoreCase);
                                }
                            }
                        }
                        bool[] doneFlag = new bool[renderer.sharedMesh.blendShapeCount];
                        for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                        {
                            if (doneFlag[i]) continue;
                            doneFlag[i] = true;
                            var nameI = renderer.sharedMesh.GetBlendShapeName(i);
                            if (nameTable.ContainsKey(nameI))
                                continue;
                            for (int j = 0; j < renderer.sharedMesh.blendShapeCount; j++)
                            {
                                if (i == j || nameLRIgnorePaths[i] != nameLRIgnorePaths[j])
                                    continue;
                                var nameJ = renderer.sharedMesh.GetBlendShapeName(j);
                                if (nameI == nameJ || nameTable.ContainsKey(nameJ))
                                    continue;
                                nameTable.Add(nameI, nameJ);
                                nameTable.Add(nameJ, nameI);
                                doneFlag[j] = true;
                                break;
                            }
                        }
                    }
                    mirrorBlendShape.Add(renderer, nameTable);
                }
            }
        }
        public void ChangeBlendShapeMirror(SkinnedMeshRenderer renderer, string name, string mirrorName)
        {
            Dictionary<string, string> nameTable;
            if (mirrorBlendShape.TryGetValue(renderer, out nameTable))
            {
                if (string.IsNullOrEmpty(mirrorName))
                {
                    nameTable.Remove(name);
                }
                else
                {
                    nameTable[name] = mirrorName;
                }
            }
            else if (renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0)
            {
                nameTable = new Dictionary<string, string>();
                nameTable.Add(name, mirrorName);
                mirrorBlendShape.Add(renderer, nameTable);
            }
        }

        public void OnHierarchyWindowChanged()
        {
            if (isEditError) return;

            bool changed = false;
            List<GameObject> list = EditorCommon.GetHierarchyGameObject(vaw.gameObject);
            if (bones.Length != list.Count)
            {
                changed = true;
            }
            if (!changed)
            {
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] != list[i] ||
                        bonePaths[i] != AnimationUtility.CalculateTransformPath(bones[i].transform, vaw.gameObject.transform))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                StopRecording();

                Selection.activeObject = null;
                SelectGameObjectEvent();

                UpdateBones(false);

                if (OnHierarchyUpdated != null)
                {
                    OnHierarchyUpdated.Invoke();
                }

                SetUpdateSampleAnimation();
                SetSynchroIKtargetAll();
                EditorApplication.delayCall += () =>
                {
                    SetUpdateSampleAnimation();
                    SetSynchroIKtargetAll();
                };
            }
        }
        #endregion

        #region SampleAnimation
        public void SetUpdateSampleAnimation(bool full = false)
        {
            updateSampleAnimation = true;

            if (full)
            {
                updateSaveForce = true;
            }
        }
        public void SampleAnimation(float time = -1, EditObjectFlag flags = EditObjectFlag.All)
        {
#if UNITY_2019_1_OR_NEWER
            UpdateSyncEditorCurveClip();

            if (time < 0f)
                time = currentTime;

            if ((flags & EditObjectFlag.Base) != 0 ||
                ((flags & EditObjectFlag.Edit) != 0 && !hasDummyObject))
            {
                if (!uAw.GetLinkedWithTimeline())
                {
                    transformPoseSave.ResetRootStartTransform();

                    if (vaw.animator != null)
                    {
                        #region AnimationWindowPlayable
                        var playableGraph = uAw_2019_1.GetPlayableGraph();
                        if (!playableGraph.IsValid())
                        {
                            uAw_2019_1.ResampleAnimation();
                            playableGraph = uAw_2019_1.GetPlayableGraph();
                        }
                        if (playableGraph.IsValid())
                        {
                            var output = playableGraph.GetOutput(0);
                            if (output.IsOutputValid())
                            {
                                var sourcePlayable = output.GetSourcePlayable();

                                #region DisconnectCandidateClipPlayable
                                {
                                    var candidateClipPlayable = uAw_2019_1.GetCandidateClipPlayable();
                                    if (candidateClipPlayable.IsValid())
                                    {
                                        for (int i = 0; i < candidateClipPlayable.GetOutputCount(); i++)
                                        {
                                            var mixerPlayable = candidateClipPlayable.GetOutput(i);
                                            if (mixerPlayable.IsValid())
                                            {
                                                for (int j = 0; j < mixerPlayable.GetInputCount(); j++)
                                                {
                                                    var playable = mixerPlayable.GetInput(j);
                                                    if (playable.Equals(candidateClipPlayable))
                                                    {
                                                        playableGraph.Disconnect(mixerPlayable, j);
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                #endregion

                                #region InsertAnimationOffsetPlayable
                                if (rootMotionBoneIndex >= 0 &&
                                    sourcePlayable.GetPlayableType() == animationPlayable.uAnimationMotionXToDeltaPlayable.type &&
                                    sourcePlayable.GetInputCount() > 0 && sourcePlayable.GetInput(0).GetPlayableType() != animationPlayable.uAnimationOffsetPlayable.type)
                                {
                                    animationPlayable.animationOffsetPlayable = animationPlayable.uAnimationOffsetPlayable.Create(playableGraph, transformPoseSave.startLocalPosition, transformPoseSave.startLocalRotation, 1);
                                    animationPlayable.animationOffsetPlayable.SetInputWeight(0, 1);
                                    for (int i = 0; i < sourcePlayable.GetInputCount(); i++)
                                    {
                                        var playable = sourcePlayable.GetInput(i);
                                        playableGraph.Disconnect(sourcePlayable, i);
                                        playableGraph.Connect(playable, 0, animationPlayable.animationOffsetPlayable, i);
                                    }
                                    playableGraph.Connect(animationPlayable.animationOffsetPlayable, 0, sourcePlayable, 0);
                                }
                                #endregion

                                if (isHuman)
                                {
#if UNITY_2019_2_OR_NEWER
                                    #region DisableDefaultPosePlayableFootIK
                                    {
                                        var defaultPosePlayable = uAw_2019_2.GetDefaultPosePlayable();
                                        if (defaultPosePlayable.IsValid())
                                        {
                                            if (defaultPosePlayable.GetApplyFootIK())
                                                defaultPosePlayable.SetApplyFootIK(false);
                                        }
                                    }
                                    #endregion
#endif
                                    #region FootIK
                                    {
                                        var clipPlayable = uAw_2019_1.GetClipPlayable();
                                        clipPlayable.SetApplyFootIK(autoFootIK);
                                    }
                                    #endregion
                                }

                                if (time != currentTime)
                                    uAw_2019_1.SetCurrentTimeOnly(time);
                                uAw_2019_1.ResampleAnimation();
                                if (time != currentTime)
                                    uAw_2019_1.SetCurrentTimeOnly(currentTime);
                            }
                        }
                        else
                        {
                            Assert.IsTrue(false);
                        }
                        #endregion
                    }
                    else if (vaw.animation != null)
                    {
                        WrapMode? beforeWrapMode = null;
                        try
                        {
                            if (currentClip.wrapMode != WrapMode.Default)
                            {
                                beforeWrapMode = currentClip.wrapMode;
                                currentClip.wrapMode = WrapMode.Default;
                            }
                            currentClip.SampleAnimation(vaw.gameObject, time);
                        }
                        finally
                        {
                            if (beforeWrapMode.HasValue)
                            {
                                currentClip.wrapMode = beforeWrapMode.Value;
                            }
                        }
                    }
                }
            }

            #region DummyObject
            if ((flags & EditObjectFlag.Dummy) != 0 ||
                ((flags & EditObjectFlag.Edit) != 0 && hasDummyObject))
            {
                calcObject.SetApplyIK(false);
                calcObject.SetTransformStart();
                calcObject.SampleAnimation(currentClip, time);
            }
            #endregion
#else
            SampleAnimationLegacy(time, flags);
#endif
        }
        public void SampleAnimationLegacy(float time = -1, EditObjectFlag flags = EditObjectFlag.All)
        {
            UpdateSyncEditorCurveClip();

            if (time < 0f)
                time = currentTime;

            if ((flags & EditObjectFlag.Base) != 0 ||
                ((flags & EditObjectFlag.Edit) != 0 && !hasDummyObject))
            {
                if (!uAw.GetLinkedWithTimeline())
                {
                    transformPoseSave.ResetRootStartTransform();

                    if (vaw.animator != null)
                    {
#if UNITY_2018_3_OR_NEWER
                        var changedRootMotion = vaw.animator.applyRootMotion;
                        if (changedRootMotion)
                        {
                            vaw.animator.applyRootMotion = false;
                        }
#endif

                        if (!vaw.animator.isInitialized)
                            vaw.animator.Rebind();
                        currentClip.SampleAnimation(vaw.gameObject, time);

#if UNITY_2018_3_OR_NEWER
                        if (changedRootMotion)
                        {
                            vaw.animator.applyRootMotion = true;
                        }
#endif
                    }
                    else if (vaw.animation != null)
                    {
                        WrapMode? beforeWrapMode = null;
                        try
                        {
                            if (currentClip.wrapMode != WrapMode.Default)
                            {
                                beforeWrapMode = currentClip.wrapMode;
                                currentClip.wrapMode = WrapMode.Default;
                            }
                            currentClip.SampleAnimation(vaw.gameObject, time);
                        }
                        finally
                        {
                            if (beforeWrapMode.HasValue)
                            {
                                currentClip.wrapMode = beforeWrapMode.Value;
                            }
                        }
                    }
                }
            }

            #region DummyObject
            if ((flags & EditObjectFlag.Dummy) != 0 ||
                ((flags & EditObjectFlag.Edit) != 0 && hasDummyObject))
            {
                calcObject.SetApplyIK(false);
                calcObject.SetTransformStart();
                calcObject.SampleAnimationLegacy(currentClip, time);
            }
            #endregion
        }
        public void PlayableDirectorEvaluate()
        {
            if (!uAw.GetLinkedWithTimeline())
                return;

#if VERYANIMATION_TIMELINE
            UpdateSyncEditorCurveClip();

            uAw.uTimelineWindow.OnCurveModified(currentClip, new EditorCurveBinding(), AnimationUtility.CurveModifiedType.CurveModified);
#endif
        }
        public void PlayableDirectorEvaluateImmediate()
        {
            if (!uAw.GetLinkedWithTimeline())
                return;

#if VERYANIMATION_TIMELINE
            UpdateSyncEditorCurveClip();

            uAw.uTimelineWindow.GetCurrentDirector().Evaluate();
#endif
        }

        public void AnimationWindowSampleAnimationOverride()
        {
            if (uAw.GetLinkedWithTimeline())
                return;

#if UNITY_2019_1_OR_NEWER
            var playableGraph = uAw_2019_1.GetPlayableGraph();
            if (playableGraph.IsValid())
            {
                var output = playableGraph.GetOutput(0);
                if (output.IsOutputValid())
                {
                    var sourcePlayable = output.GetSourcePlayable();

                    bool update = false;

#if UNITY_2019_2_OR_NEWER
                    #region CheckDisconnectCandidateClipPlayable
                    if (!update)
                    {
                        var candidateClipPlayable = uAw_2019_1.GetCandidateClipPlayable();
                        if (candidateClipPlayable.IsValid())
                        {
                            for (int i = 0; i < candidateClipPlayable.GetOutputCount(); i++)
                            {
                                var mixerPlayable = candidateClipPlayable.GetOutput(i);
                                if (mixerPlayable.IsValid())
                                {
                                    update = true;
                                    break;
                                }
                            }
                        }
                    }
                    #endregion

                    #region CheckDisableDefaultPosePlayableFootIK
                    if (!update)
                    {
                        var defaultPosePlayable = uAw_2019_2.GetDefaultPosePlayable();
                        if (defaultPosePlayable.IsValid())
                        {
                            if (defaultPosePlayable.GetApplyFootIK())
                                update = true;
                        }
                    }
                    #endregion
#endif

                    #region CheckAnimationOffsetPlayable
                    if (!update)
                    {
                        if (sourcePlayable.GetPlayableType() == animationPlayable.uAnimationMotionXToDeltaPlayable.type &&
                             sourcePlayable.GetInputCount() > 0 && sourcePlayable.GetInput(0).GetPlayableType() != animationPlayable.uAnimationOffsetPlayable.type)
                        {
                            update = true;
                        }
                    }
                    #endregion

                    if (update)
                    {
                        SampleAnimation();
                    }
                }
            }

#elif UNITY_2018_3_OR_NEWER
            {
                bool update = !transformPoseSave.IsRootStartTransform();
                if (isHuman)
                {
                    if (!update)
                    {
                        var lengthSq = (animationWindowSampleAnimationOverrideCheckPosition - humanoidBones[(int)HumanBodyBones.LeftFoot].transform.position).sqrMagnitude;
                        if (lengthSq > 0.0001f)
                            update = true;
                    }
                    if (!update)
                    {
                        var langthSq = (humanoidBones[(int)HumanBodyBones.LeftLowerLeg].transform.position - humanoidBones[(int)HumanBodyBones.LeftFoot].transform.position).sqrMagnitude;
                        if (langthSq - humanoidLeftLowerLegLengthSq > 0.0001f)
                            update = true;
                    }
                }
                if (update)
                {
                    SampleAnimation();
                }
            }
#endif
        }
        #endregion

        #region HotKey
        public void HotKeys()
        {
            if (vaw.uEditorGUI.IsEditingTextField())
                return;

            Event e = Event.current;

            Action KeyCommmon = () =>
            {
                vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.All);
                e.Use();
            };

            #region Exit edit
            if (!Shortcuts.IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.Escape)
            {
                EditorApplication.delayCall += () =>
                {
                    vaw.Release();
                };
                KeyCommmon();
            }
            #endregion
            #region Change Clamp
            else if (Shortcuts.IsChangeClamp(e))
            {
                Undo.RecordObject(vaw, "Change Clamp");
                clampMuscle = !clampMuscle;
                KeyCommmon();
            }
            #endregion
            #region Change Foot IK
            else if (Shortcuts.IsChangeFootIK(e))
            {
                Undo.RecordObject(vaw, "Change Foot IK");
                autoFootIK = !autoFootIK;
                SetUpdateSampleAnimation();
                SetSynchroIKtargetAll();
                SetAnimationWindowSynchroSelection();
                KeyCommmon();
            }
            #endregion
            #region Change Mirror
            else if (Shortcuts.IsChangeMirror(e))
            {
                Undo.RecordObject(vaw, "Change Mirror");
                mirrorEnable = !mirrorEnable;
                SetAnimationWindowSynchroSelection();
                KeyCommmon();
            }
            #endregion
            #region Change Collision
            else if (Shortcuts.IsChangeCollision(e))
            {
                Undo.RecordObject(vaw, "Change Collision");
                collisionEnable = !collisionEnable;
                KeyCommmon();
            }
            #endregion
            #region Change Root Correction Mode
            else if (Shortcuts.IsChangeRootCorrectionMode(e))
            {
                Undo.RecordObject(vaw, "Change Root Correction Mode");
                rootCorrectionMode = (RootCorrectionMode)((int)(++rootCorrectionMode) % ((int)RootCorrectionMode.Full + 1));
                SetAnimationWindowSynchroSelection();
                KeyCommmon();
            }
            #endregion
            #region Change selection bone IK
            else if (Shortcuts.IsChangeSelectionBoneIK(e))
            {
                IKChangeSelection();
                SetAnimationWindowSynchroSelection();
                KeyCommmon();
            }
            #endregion
            #region Force refresh
            else if (Shortcuts.IsForceRefresh(e))
            {
                SetUpdateSampleAnimation(true);
                SetSynchroIKtargetAll();
                uAw.ForceRefresh();
                SetAnimationWindowSynchroSelection();
                KeyCommmon();
            }
            #endregion
            #region Next animation clip
            else if (Shortcuts.IsNextAnimationClip(e))
            {
                if (!uAw.GetLinkedWithTimeline())
                {
                    vaw.MoveChangeSelectionAnimationClip(1);
                    KeyCommmon();
                }
            }
            #endregion
            #region Previous animation clip
            else if (Shortcuts.IsPreviousAnimationClip(e))
            {
                if (!uAw.GetLinkedWithTimeline())
                {
                    vaw.MoveChangeSelectionAnimationClip(-1);
                    KeyCommmon();
                }
            }
            #endregion
            #region AddInbetween
            else if (Shortcuts.IsAddInbetween(e))
            {
                AddRemoveInbetween(1);
                KeyCommmon();
            }
            #endregion
            #region RemoveInbetween
            else if (Shortcuts.IsRemoveInbetween(e))
            {
                AddRemoveInbetween(-1);
                KeyCommmon();
            }
            #endregion
            #region Hide select bones
            else if (Shortcuts.IsHideSelectBones(e))
            {
                Undo.RecordObject(vaw, "Change Show Flag");
                if (selectionBones != null)
                {
                    foreach (var boneIndex in selectionBones)
                    {
                        boneShowFlags[boneIndex] = false;
                    }
                }
                OnBoneShowFlagsUpdated.Invoke();
                KeyCommmon();
            }
            #endregion
            #region Show select bones
            else if (Shortcuts.IsShowSelectBones(e))
            {
                Undo.RecordObject(vaw, "Change Show Flag");
                if (selectionBones != null)
                {
                    foreach (var boneIndex in selectionBones)
                    {
                        boneShowFlags[boneIndex] = true;
                    }
                }
                OnBoneShowFlagsUpdated.Invoke();
                KeyCommmon();
            }
            #endregion
            #region Preview/Change playing
            else if (Shortcuts.IsPreviewChangePlaying(e))
            {
                if (uAvatarPreview != null)
                {
                    uAvatarPreview.playing = !uAvatarPreview.playing;
                    if (uAvatarPreview.playing)
                        uAvatarPreview.SetTime(0f);
                    else
                        uAvatarPreview.SetTime(uAw.GetCurrentTime());
                }
                KeyCommmon();
            }
            #endregion
            #region Add IK - Level / Direction
            else if (Shortcuts.IsAddIKLevel(e))
            {
                if (originalIK.ikActiveTarget >= 0)
                {
                    Undo.RecordObject(vaw, "Change Original IK Data");
                    for (int i = 0; i < originalIK.ikTargetSelect.Length; i++)
                    {
                        originalIK.ChangeTypeSetting(originalIK.ikTargetSelect[i], 1);
                    }
                }
                KeyCommmon();
            }
            #endregion
            #region Sub IK - Level / Direction
            else if (Shortcuts.IsSubIKLevel(e))
            {
                if (originalIK.ikActiveTarget >= 0)
                {
                    Undo.RecordObject(vaw, "Change Original IK Data");
                    for (int i = 0; i < originalIK.ikTargetSelect.Length; i++)
                    {
                        originalIK.ChangeTypeSetting(originalIK.ikTargetSelect[i], -1);
                    }
                }
                KeyCommmon();
            }
            #endregion
            #region Change playing
            else if (Shortcuts.IsAnimationChangePlaying(e))
            {
                uAw.PlayingChange();
                KeyCommmon();
            }
            #endregion
            #region Switch between curves and dope sheet
            else if (Shortcuts.IsAnimationSwitchBetweenCurvesAndDopeSheet(e))
            {
                uAw.SwitchBetweenCurvesAndDopesheet();
                EditorApplication.delayCall += () =>
                {
                    SetAnimationWindowSynchroSelection();
                };
                KeyCommmon();
            }
            #endregion
            #region Add keyframe
            else if (Shortcuts.IsAddKeyframe(e))
            {
                var tool = CurrentTool();
                if (isHuman)
                {
                    foreach (var humanoidIndex in SelectionGameObjectsHumanoidIndex())
                    {
                        switch (tool)
                        {
                            case Tool.Move:
                                if (HumanBonesAnimatorTDOFIndex[(int)humanoidIndex] != null)
                                {
                                    SetAnimationValueAnimatorTDOF(HumanBonesAnimatorTDOFIndex[(int)humanoidIndex].index, GetAnimationValueAnimatorTDOF(HumanBonesAnimatorTDOFIndex[(int)humanoidIndex].index));
                                }
                                break;
                            case Tool.Rotate:
                                for (int dof = 0; dof < 3; dof++)
                                {
                                    var muscleIndex = HumanTrait.MuscleFromBone((int)humanoidIndex, dof);
                                    if (muscleIndex >= 0)
                                    {
                                        SetAnimationValueAnimatorMuscle(muscleIndex, GetAnimationValueAnimatorMuscle(muscleIndex));
                                    }
                                }
                                break;
                        }
                    }
                    if (SelectionGameObjectsIndexOf(vaw.gameObject) >= 0)
                    {
                        switch (tool)
                        {
                            case Tool.Move:
                                SetAnimationValueAnimatorRootT(GetAnimationValueAnimatorRootT());
                                break;
                            case Tool.Rotate:
                                SetAnimationValueAnimatorRootQ(GetAnimationValueAnimatorRootQ());
                                break;
                        }
                    }
                }
                foreach (var boneIndex in SelectionGameObjectsOtherHumanoidBoneIndex())
                {
                    switch (tool)
                    {
                        case Tool.Move:
                            SetAnimationValueTransformPosition(boneIndex, GetAnimationValueTransformPosition(boneIndex));
                            break;
                        case Tool.Rotate:
                            SetAnimationValueTransformRotation(boneIndex, GetAnimationValueTransformRotation(boneIndex));
                            break;
                        case Tool.Scale:
                            SetAnimationValueTransformScale(boneIndex, GetAnimationValueTransformScale(boneIndex));
                            break;
                    }
                }
                KeyCommmon();
            }
            #endregion
            #region Move to next frame
            else if (Shortcuts.IsMoveToNextFrame(e))
            {
                uAw.MoveToNextFrame();
                KeyCommmon();
            }
            #endregion
            #region Move to previous frame
            else if (Shortcuts.IsMoveToPrevFrame(e))
            {
                uAw.MoveToPrevFrame();
                KeyCommmon();
            }
            #endregion
            #region Move to next keyframe
            else if (Shortcuts.IsMoveToNextKeyframe(e))
            {
                uAw.MoveToNextKeyframe();
                KeyCommmon();
            }
            #endregion
            #region Move to previous keyframe
            else if (Shortcuts.IsMoveToPreviousKeyframe(e))
            {
                uAw.MoveToPreviousKeyframe();
                KeyCommmon();
            }
            #endregion
            #region Move to first keyframe
            else if (Shortcuts.IsMoveToFirstKeyframe(e))
            {
                uAw.MoveToFirstKeyframe();
                KeyCommmon();
            }
            #endregion
            #region Move to last keyframe
            else if (Shortcuts.IsMoveToLastKeyframe(e))
            {
                uAw.MoveToLastKeyframe();
                KeyCommmon();
            }
            #endregion
        }
        public void Commands()
        {
            Event e = Event.current;
            switch (e.type)
            {
                case EventType.ValidateCommand:
                    {
                        if (e.commandName == "Cut" ||
                            e.commandName == "Copy" ||
                            e.commandName == "Paste" ||
                            e.commandName == "SelectAll" ||
                            e.commandName == "FrameSelected" ||
                            e.commandName == "FrameSelectedWithLock" ||
                            e.commandName == "Delete" ||
                            e.commandName == "SoftDelete" ||
                            e.commandName == "Duplicate")
                        {
                            e.Use();
                        }
                    }
                    break;
                case EventType.ExecuteCommand:
                    {
                        if (e.commandName == "Cut" ||
                            e.commandName == "Delete" ||
                            e.commandName == "SoftDelete" ||
                            e.commandName == "Duplicate")
                        {
                            e.Use();
                        }
                        else if (e.commandName == "Copy")
                        {
                            if (CommandCopy())
                                e.Use();
                        }
                        else if (e.commandName == "Paste")
                        {
                            if (CommandPaste())
                                e.Use();
                        }
                        else if (e.commandName == "SelectAll")
                        {
                            if (CommandSelectAll())
                                e.Use();
                        }
                        else if (e.commandName == "FrameSelected")
                        {
                            if (CommandFrameSelected(false))
                                e.Use();
                        }
                        else if (e.commandName == "FrameSelectedWithLock")
                        {
                            if (CommandFrameSelected(true))
                                e.Use();
                        }
                    }
                    break;
            }
        }
        private bool CommandCopy()
        {
            if (copyPaste != null)
            {
                GameObject.DestroyImmediate(copyPaste);
                copyPaste = null;
            }
            copyAnimatorIKTargetData = null;
            copyOriginalIKTargetData = null;

            if (selectionActiveGameObject != null)
            {
                copyPaste = ScriptableObject.CreateInstance<PoseTemplate>();
                SaveSelectionPoseTemplate(copyPaste);
                copyDataType = CopyDataType.SelectionPose;
            }
            else if (animatorIK.ikTargetSelect != null && animatorIK.ikTargetSelect.Length > 0)
            {
                copyAnimatorIKTargetData = new CopyAnimatorIKTargetData[animatorIK.ikTargetSelect.Length];
                for (int i = 0; i < animatorIK.ikTargetSelect.Length; i++)
                {
                    var index = (int)animatorIK.ikTargetSelect[i];
                    var data = animatorIK.ikData[index];
                    copyAnimatorIKTargetData[i] = new CopyAnimatorIKTargetData()
                    {
                        ikTarget = (AnimatorIKCore.IKTarget)index,
                        autoRotation = data.autoRotation,
                        spaceType = data.spaceType,
                        parent = data.parent,
                        position = data.position,
                        rotation = data.rotation,
                        swivelRotation = data.swivelRotation,
                    };
                }
                copyDataType = CopyDataType.AnimatorIKTarget;
            }
            else if (originalIK.ikTargetSelect != null && originalIK.ikTargetSelect.Length > 0)
            {
                copyOriginalIKTargetData = new CopyOriginalIKTargetData[originalIK.ikTargetSelect.Length];
                for (int i = 0; i < originalIK.ikTargetSelect.Length; i++)
                {
                    var index = originalIK.ikTargetSelect[i];
                    var data = originalIK.ikData[index];
                    copyOriginalIKTargetData[i] = new CopyOriginalIKTargetData()
                    {
                        ikTarget = index,
                        autoRotation = data.autoRotation,
                        spaceType = data.spaceType,
                        parent = data.parent,
                        position = data.position,
                        rotation = data.rotation,
                        swivel = data.swivel,
                    };
                }
                copyDataType = CopyDataType.OriginalIKTarget;
            }
            else
            {
                copyPaste = ScriptableObject.CreateInstance<PoseTemplate>();
                SavePoseTemplate(copyPaste);
                copyDataType = CopyDataType.FullPose;
            }
            return true;
        }
        private bool CommandPaste()
        {
            switch (copyDataType)
            {
                case CopyDataType.None:
                    break;
                case CopyDataType.SelectionPose:
                    if (copyPaste != null)
                    {
                        Undo.RegisterCompleteObjectUndo(currentClip, "Paste");
                        LoadPoseTemplate(copyPaste, true);
                    }
                    break;
                case CopyDataType.FullPose:
                    if (copyPaste != null)
                    {
                        Undo.RegisterCompleteObjectUndo(currentClip, "Paste");
                        LoadPoseTemplate(copyPaste);
                    }
                    break;
                case CopyDataType.AnimatorIKTarget:
                    if (copyAnimatorIKTargetData != null)
                    {
                        Undo.RecordObject(vaw, "Paste");
                        foreach (var copyData in copyAnimatorIKTargetData)
                        {
                            var index = (int)copyData.ikTarget;
                            if (index < 0 || index >= animatorIK.ikData.Length)
                                continue;
                            var data = animatorIK.ikData[index];
                            {
                                data.autoRotation = copyData.autoRotation;
                                data.spaceType = copyData.spaceType;
                                data.parent = copyData.parent;
                                data.position = copyData.position;
                                data.rotation = copyData.rotation;
                                data.swivelRotation = copyData.swivelRotation;
                            }
                            animatorIK.UpdateOptionPosition(copyData.ikTarget);
                            animatorIK.UpdateSwivelPosition(copyData.ikTarget);
                            SetUpdateIKtargetAnimatorIK(copyData.ikTarget);
                        }
                    }
                    break;
                case CopyDataType.OriginalIKTarget:
                    if (copyOriginalIKTargetData != null)
                    {
                        Undo.RecordObject(vaw, "Paste");
                        foreach (var copyData in copyOriginalIKTargetData)
                        {
                            var index = copyData.ikTarget;
                            if (index < 0 || index >= originalIK.ikData.Count)
                                continue;
                            var data = originalIK.ikData[index];
                            {
                                data.autoRotation = copyData.autoRotation;
                                data.spaceType = copyData.spaceType;
                                data.parent = copyData.parent;
                                data.position = copyData.position;
                                data.rotation = copyData.rotation;
                                data.swivel = copyData.swivel;
                            }
                            SetUpdateIKtargetOriginalIK(copyData.ikTarget);
                        }
                    }
                    break;
                default:
                    break;
            }
            return true;
        }
        private bool CommandSelectAll()
        {
            List<GameObject> selectObjects = new List<GameObject>(bones.Length);
            for (int i = 0; i < bones.Length; i++)
            {
                if (!IsShowBone(i)) continue;
                selectObjects.Add(bones[i]);
            }
            List<HumanBodyBones> selectVirtual = new List<HumanBodyBones>(HumanVirtualBones.Length);
            for (int i = 0; i < HumanVirtualBones.Length; i++)
            {
                if (!IsShowVirtualBone((HumanBodyBones)i)) continue;
                selectVirtual.Add((HumanBodyBones)i);
            }
            SelectGameObjects(selectObjects.ToArray(), selectVirtual.ToArray());
            return true;
        }
        private bool CommandFrameSelected(bool withLock)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return false;
            var bounds = GetSelectionBounds(0.333f);
            uSceneView.SetViewIsLockedToObject(sceneView, withLock);
            sceneView.FixNegativeSize();
            uSceneView.Frame(sceneView, bounds, EditorApplication.isPlaying);

            return true;
        }
        #endregion

        #region SelectionGameObject
        public void SelectGameObjectEvent()
        {
            #region selectionGameObjects
            {
                if (selectionGameObjects == null)
                    selectionGameObjects = new List<GameObject>();
                selectionGameObjects.Clear();
                foreach (var go in Selection.gameObjects)
                    selectionGameObjects.Add(go);
                if (Selection.activeGameObject != null)
                {
                    selectionGameObjects.Remove(Selection.activeGameObject);
                    selectionGameObjects.Insert(0, Selection.activeGameObject);
                }
            }
            #endregion

            #region selectionBones
            {
                if (selectionBones == null)
                    selectionBones = new List<int>();
                selectionBones.Clear();
                foreach (var go in selectionGameObjects)
                {
                    var boneIndex = BonesIndexOf(go);
                    if (boneIndex < 0) continue;
                    selectionBones.Add(boneIndex);
                }
            }
            #endregion

            #region CheckParentChange
            {
                bool changed = false;
                foreach (var boneIndex in selectionBones)
                {
                    var parent = bones[boneIndex].transform.parent;
                    int parentBoneIndex = -1;
                    if (parent != null)
                        parentBoneIndex = BonesIndexOf(parent.gameObject);
                    if (parentBoneIndexes[boneIndex] != parentBoneIndex)
                    {
                        changed = true;
                        break;
                    }
                }
                if (changed)
                {
                    OnHierarchyWindowChanged();
                }
            }
            #endregion

            #region SceneWindow
            vaw.editorWindowSelectionRect.size = Vector2.zero;
            #endregion

            if (EditorWindow.focusedWindow == uAw.instance)
            {
                selectionHumanVirtualBones = null;
                ClearIkTargetSelect();
            }
        }
        public void SelectGameObjectMouseDrag(GameObject[] go, HumanBodyBones[] virtualBones, AnimatorIKCore.IKTarget[] animatorIKTarget, int[] originalIKTarget)
        {
            Undo.RecordObject(vaw, "Change Selection");
            Selection.objects = go;
            selectionHumanVirtualBones = virtualBones != null ? new List<HumanBodyBones>(virtualBones) : null;
            selectionMotionTool = false;
            animatorIK.ikTargetSelect = animatorIKTarget;
            animatorIK.OnSelectionChange();
            originalIK.ikTargetSelect = originalIKTarget;
            originalIK.OnSelectionChange();
            SetAnimationWindowSynchroSelection();
        }
        public void SelectGameObjectPlusKey(GameObject go)
        {
            var select = new List<GameObject>();
            if (go != null)
                select.Add(go);
            var selectVirtual = new List<HumanBodyBones>();
            var e = Event.current;
            if (e.alt)
            {
                if (go != null)
                {
                    var boneIndex = BonesIndexOf(go);
                    ActionAllBoneChildren(boneIndex, (ci) =>
                    {
                        select.Add(bones[ci]);
                    });
                    ActionAllVirtualBoneChildren(boneIndex, (cvhi) =>
                    {
                        selectVirtual.Add(cvhi);
                    });
                }
            }
            if (Shortcuts.IsKeyControl(e) || e.shift)
            {
                if (selectionHumanVirtualBones != null)
                    selectVirtual.AddRange(selectionHumanVirtualBones);
                if (selectionGameObjects != null)
                {
                    foreach (var o in selectionGameObjects)
                    {
                        if (select.Contains(o))
                            select.Remove(o);
                        else
                            select.Add(o);
                    }
                }
            }
            if (go != null && select.Contains(go))
                Selection.activeGameObject = go;
            SelectGameObjects(select.ToArray(), selectVirtual.ToArray());
        }
        public void SelectGameObject(GameObject go)
        {
            Undo.RecordObject(vaw, "Change Selection");
            Selection.objects = new UnityEngine.Object[] { go };
            selectionHumanVirtualBones = null;
            selectionMotionTool = false;
            ClearIkTargetSelect();
            SetUpdateSampleAnimation();
            SetAnimationWindowSynchroSelection();
            vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.All);
        }
        public void SelectGameObjects(GameObject[] go, HumanBodyBones[] virtualBones = null)
        {
            Undo.RecordObject(vaw, "Change Selection");
            Selection.objects = go;
            selectionHumanVirtualBones = virtualBones != null ? new List<HumanBodyBones>(virtualBones) : null;
            selectionMotionTool = false;
            ClearIkTargetSelect();
            SetUpdateSampleAnimation();
            SetAnimationWindowSynchroSelection();
            vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.All);
        }
        public void SelectVirtualBonePlusKey(HumanBodyBones humanoidIndex)
        {
            if (humanoidIndex2boneIndex[(int)humanoidIndex] >= 0)
                return;

            var select = new List<GameObject>();
            var selectVirtual = new List<HumanBodyBones>();
            selectVirtual.Add(humanoidIndex);
            var e = Event.current;
            if (e.alt)
            {
                Action VirtualNeck = () =>
                {
                    int boneIndex;
                    if (humanoidIndex2boneIndex[(int)HumanBodyBones.Neck] >= 0)
                        boneIndex = humanoidIndex2boneIndex[(int)HumanBodyBones.Neck];
                    else
                    {
                        selectVirtual.Add(HumanBodyBones.Neck);
                        boneIndex = humanoidIndex2boneIndex[(int)HumanBodyBones.Head];
                    }
                    select.Add(bones[boneIndex]);
                    ActionAllBoneChildren(boneIndex, (ci) =>
                    {
                        select.Add(bones[ci]);
                    });
                };
                Action VirtualLeftShoulder = () =>
                {
                    int boneIndex;
                    if (humanoidIndex2boneIndex[(int)HumanBodyBones.LeftShoulder] >= 0)
                        boneIndex = humanoidIndex2boneIndex[(int)HumanBodyBones.LeftShoulder];
                    else
                    {
                        selectVirtual.Add(HumanBodyBones.LeftShoulder);
                        boneIndex = humanoidIndex2boneIndex[(int)HumanBodyBones.LeftUpperArm];
                    }
                    select.Add(bones[boneIndex]);
                    ActionAllBoneChildren(boneIndex, (ci) =>
                    {
                        select.Add(bones[ci]);
                    });
                };
                Action VirtualRightShoulder = () =>
                {
                    int boneIndex;
                    if (humanoidIndex2boneIndex[(int)HumanBodyBones.RightShoulder] >= 0)
                        boneIndex = humanoidIndex2boneIndex[(int)HumanBodyBones.RightShoulder];
                    else
                    {
                        selectVirtual.Add(HumanBodyBones.RightShoulder);
                        boneIndex = humanoidIndex2boneIndex[(int)HumanBodyBones.RightUpperArm];
                    }
                    select.Add(bones[boneIndex]);
                    ActionAllBoneChildren(boneIndex, (ci) =>
                    {
                        select.Add(bones[ci]);
                    });
                };
                switch (humanoidIndex)
                {
                    case HumanBodyBones.Chest:
                        selectVirtual.Add(HumanBodyBones.UpperChest);
                        VirtualNeck();
                        VirtualLeftShoulder();
                        VirtualRightShoulder();
                        break;
                    case HumanBodyBones.Neck:
                        VirtualNeck();
                        break;
                    case HumanBodyBones.LeftShoulder:
                        VirtualLeftShoulder();
                        break;
                    case HumanBodyBones.RightShoulder:
                        VirtualRightShoulder();
                        break;
                    case HumanBodyBones.UpperChest:
                        VirtualNeck();
                        VirtualLeftShoulder();
                        VirtualRightShoulder();
                        break;
                    default:
                        Assert.IsTrue(false);
                        break;
                }
            }
            if (Shortcuts.IsKeyControl(e) || e.shift)
            {
                if (selectionGameObjects != null)
                    select.AddRange(selectionGameObjects);
                if (selectionHumanVirtualBones != null)
                {
                    foreach (var h in selectionHumanVirtualBones)
                    {
                        if (selectVirtual.Contains(h))
                            selectVirtual.Remove(h);
                        else
                            selectVirtual.Add(h);
                    }
                }
            }
            SelectGameObjects(select.ToArray(), selectVirtual.ToArray());
        }
        public void SelectHumanoidBones(HumanBodyBones[] bones)
        {
            List<GameObject> goList = new List<GameObject>();
            List<HumanBodyBones> virtualList = new List<HumanBodyBones>();
            foreach (var hi in bones)
            {
                if (hi < 0)
                    goList.Add(vaw.gameObject);
                else if (humanoidBones[(int)hi] != null)
                    goList.Add(humanoidBones[(int)hi]);
                else if (HumanVirtualBones[(int)hi] != null)
                    virtualList.Add(hi);
            }
            Undo.RecordObject(vaw, "Change Selection");
            Selection.objects = goList.ToArray();
            selectionHumanVirtualBones = virtualList;
            selectionMotionTool = false;
            ClearIkTargetSelect();
            SetUpdateSampleAnimation();
            SetAnimationWindowSynchroSelection();
            vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.All);
        }
        public void SelectAnimatorIKTargetPlusKey(AnimatorIKCore.IKTarget ikTarget)
        {
            List<AnimatorIKCore.IKTarget> select = new List<AnimatorIKCore.IKTarget>();
            select.Add(ikTarget);
            var e = Event.current;
            if (e != null && (Shortcuts.IsKeyControl(e) || e.shift))
            {
                if (animatorIK.ikTargetSelect != null)
                {
                    select = new List<AnimatorIKCore.IKTarget>(animatorIK.ikTargetSelect);
                    if (EditorCommon.ArrayContains(animatorIK.ikTargetSelect, ikTarget))
                        select.Remove(ikTarget);
                    else
                        select.Add(ikTarget);
                }
            }
            SelectIKTargets(select.ToArray(), null);
        }
        public void SelectOriginalIKTargetPlusKey(int ikTarget)
        {
            List<int> select = new List<int>();
            select.Add(ikTarget);
            var e = Event.current;
            if (e != null && (Shortcuts.IsKeyControl(e) || e.shift))
            {
                if (originalIK.ikTargetSelect != null)
                {
                    select = new List<int>(originalIK.ikTargetSelect);
                    if (EditorCommon.ArrayContains(originalIK.ikTargetSelect, ikTarget))
                        select.Remove(ikTarget);
                    else
                        select.Add(ikTarget);
                }
            }
            SelectIKTargets(null, select.ToArray());
        }
        public void SelectIKTargets(AnimatorIKCore.IKTarget[] animatorIKTargets, int[] originalIKTargets)
        {
            Undo.RecordObject(vaw, "Change Selection");
            Selection.activeGameObject = null;
            selectionHumanVirtualBones = null;
            selectionMotionTool = false;
            animatorIK.ikTargetSelect = animatorIKTargets;
            animatorIK.OnSelectionChange();
            originalIK.ikTargetSelect = originalIKTargets;
            originalIK.OnSelectionChange();
            SetUpdateSampleAnimation();
            SetAnimationWindowSynchroSelection();
            vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.All);
        }
        public void SelectMotionTool()
        {
            Undo.RecordObject(vaw, "Change Selection");
            Selection.activeGameObject = null;
            selectionHumanVirtualBones = null;
            selectionMotionTool = true;
            ClearIkTargetSelect();
            SetUpdateSampleAnimation();
            SetAnimationWindowSynchroSelection();
            vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.All);
        }
        public void SetAnimationWindowSynchroSelection(EditorCurveBinding[] bindings = null)
        {
            animationWindowSynchroSelection = true;
            animationWindowSynchroSelectionBindings.Clear();
            if (bindings != null)
            {
                foreach (var binding in bindings)
                {
                    if (!animationWindowSynchroSelectionBindings.Contains(binding))
                        animationWindowSynchroSelectionBindings.Add(binding);
                }
            }
        }
        public void ResetAnimationWindowSynchroSelection()
        {
            animationWindowSynchroSelection = true;
        }
        public List<EditorCurveBinding> GetSelectionEditorCurveBindings()
        {
            var bindings = new List<EditorCurveBinding>();

            Action<Tool, int> AddGeneric = (currentTool, boneIndex) =>
            {
                switch (currentTool)
                {
                    case Tool.Move:
                        for (int dof = 0; dof < 3; dof++)
                            bindings.Add(AnimationCurveBindingTransformPosition(boneIndex, dof));
                        break;
                    case Tool.Rotate:
                        for (int dof = 0; dof < 3; dof++)
                            bindings.Add(AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.Baked));
                        for (int dof = 0; dof < 3; dof++)
                            bindings.Add(AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.NonBaked));
                        for (int dof = 0; dof < 4; dof++)
                            bindings.Add(AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.RawQuaternions));
                        for (int dof = 0; dof < 3; dof++)
                            bindings.Add(AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.RawEuler));
                        break;
                    case Tool.Scale:
                        for (int dof = 0; dof < 3; dof++)
                            bindings.Add(AnimationCurveBindingTransformScale(boneIndex, dof));
                        break;
                }
            };

            Tool tool = CurrentTool();

            #region Humanoid
            if (isHuman)
            {
                Action<HumanBodyBones> AddMuscle = (humanoidIndex) =>
                {
                    switch (tool)
                    {
                        case Tool.Move:
                            if (HumanBonesAnimatorTDOFIndex[(int)humanoidIndex] != null)
                            {
                                for (int dof = 0; dof < 3; dof++)
                                    bindings.Add(AnimationCurveBindingAnimatorTDOF(HumanBonesAnimatorTDOFIndex[(int)humanoidIndex].index, dof));
                            }
                            break;
                        case Tool.Rotate:
                            for (int dof = 0; dof < 3; dof++)
                            {
                                var muscleIndex = HumanTrait.MuscleFromBone((int)humanoidIndex, dof);
                                if (muscleIndex < 0) continue;
                                bindings.Add(AnimationCurveBindingAnimatorMuscle(muscleIndex));
                            }
                            break;
                    }
                };
                {
                    foreach (var go in selectionGameObjects)
                    {
                        HumanBodyBones humanoidIndex;
                        if (vaw.gameObject == go)
                        {
                            switch (tool)
                            {
                                case Tool.Move:
                                    foreach (var binding in AnimationCurveBindingAnimatorRootT)
                                        bindings.Add(binding);
                                    break;
                                case Tool.Rotate:
                                    foreach (var binding in AnimationCurveBindingAnimatorRootQ)
                                        bindings.Add(binding);
                                    break;
                            }
                        }
                        else if ((humanoidIndex = HumanoidBonesIndexOf(go)) >= 0)
                        {
                            AddMuscle(humanoidIndex);
                        }
                    }
                }
                if (selectionHumanVirtualBones != null)
                {
                    foreach (var humanoidIndex in selectionHumanVirtualBones)
                    {
                        AddMuscle(humanoidIndex);
                    }
                }
                #region AnimatorIK
                if (animatorIK.ikTargetSelect != null)
                {
                    foreach (var ikTarget in animatorIK.ikTargetSelect)
                    {
                        var data = animatorIK.ikData[(int)ikTarget];
                        if (!data.enable) continue;
                        switch (ikTarget)
                        {
                            case AnimatorIKCore.IKTarget.Head:
                                if (data.headWeight > 0f)
                                {
                                    AddMuscle(HumanBodyBones.Head);
                                    AddMuscle(HumanBodyBones.Neck);
                                }
                                if (data.eyesWeight > 0f)
                                {
                                    AddMuscle(HumanBodyBones.LeftEye);
                                    AddMuscle(HumanBodyBones.RightEye);
                                }
                                break;
                            case AnimatorIKCore.IKTarget.LeftHand:
                                AddMuscle(HumanBodyBones.LeftHand);
                                AddMuscle(HumanBodyBones.LeftLowerArm);
                                AddMuscle(HumanBodyBones.LeftUpperArm);
                                break;
                            case AnimatorIKCore.IKTarget.RightHand:
                                AddMuscle(HumanBodyBones.RightHand);
                                AddMuscle(HumanBodyBones.RightLowerArm);
                                AddMuscle(HumanBodyBones.RightUpperArm);
                                break;
                            case AnimatorIKCore.IKTarget.LeftFoot:
                                AddMuscle(HumanBodyBones.LeftFoot);
                                AddMuscle(HumanBodyBones.LeftLowerLeg);
                                AddMuscle(HumanBodyBones.LeftUpperLeg);
                                AddMuscle(HumanBodyBones.LeftToes);
                                break;
                            case AnimatorIKCore.IKTarget.RightFoot:
                                AddMuscle(HumanBodyBones.RightFoot);
                                AddMuscle(HumanBodyBones.RightLowerLeg);
                                AddMuscle(HumanBodyBones.RightUpperLeg);
                                AddMuscle(HumanBodyBones.RightToes);
                                break;
                        }
#if VERYANIMATION_ANIMATIONRIGGING
                        #region AnimationRigging
                        switch (ikTarget)
                        {
                            case AnimatorIKCore.IKTarget.Head:
                                {
                                    var constraint = data.rigConstraint as MultiAimConstraint;
                                    if (constraint != null)
                                    {
                                        foreach (var item in constraint.data.sourceObjects)
                                        {
                                            var boneIndex = BonesIndexOf(item.transform.gameObject);
                                            if (boneIndex >= 0)
                                                AddGeneric(Tool.Move, boneIndex);
                                        }
                                        bindings.Add(data.rigConstraintWeight);
                                    }
                                }
                                break;
                            case AnimatorIKCore.IKTarget.LeftHand:
                            case AnimatorIKCore.IKTarget.RightHand:
                            case AnimatorIKCore.IKTarget.LeftFoot:
                            case AnimatorIKCore.IKTarget.RightFoot:
                                {
                                    var constraint = data.rigConstraint as TwoBoneIKConstraint;
                                    if (constraint != null)
                                    {
                                        if (constraint.data.target != null)
                                        {
                                            var boneIndex = BonesIndexOf(constraint.data.target.gameObject);
                                            if (boneIndex >= 0)
                                            {
                                                AddGeneric(Tool.Move, boneIndex);
                                                AddGeneric(Tool.Rotate, boneIndex);
                                            }
                                        }
                                        if (constraint.data.hint != null)
                                        {
                                            var boneIndex = BonesIndexOf(constraint.data.hint.gameObject);
                                            if (boneIndex >= 0)
                                                AddGeneric(Tool.Move, boneIndex);
                                        }
                                        bindings.Add(data.rigConstraintWeight);
                                    }
                                }
                                break;
                        }
                        #endregion
#endif
                    }
                }
                #endregion
            }
            #endregion
            #region Generic
            {
                if (selectionBones != null)
                {
                    foreach (var boneIndex in selectionBones)
                    {
                        AddGeneric(tool, boneIndex);
                    }
                }
                #region OriginalIK
                if (originalIK.ikTargetSelect != null)
                {
                    foreach (var ikTarget in originalIK.ikTargetSelect)
                    {
                        if (ikTarget < 0 || ikTarget >= originalIK.ikData.Count) continue;
                        if (!originalIK.ikData[ikTarget].enable) continue;
                        for (int i = 0; i < originalIK.ikData[ikTarget].joints.Count; i++)
                        {
                            var boneIndex = BonesIndexOf(originalIK.ikData[ikTarget].joints[i].bone);
                            if (boneIndex >= 0)
                                AddGeneric(tool, boneIndex);
                        }
                    }
                }
                #endregion
            }
            #endregion
            #region Motion
            if (selectionMotionTool)
            {
                switch (tool)
                {
                    case Tool.Move:
                        foreach (var binding in AnimationCurveBindingAnimatorMotionT)
                            bindings.Add(binding);
                        break;
                    case Tool.Rotate:
                        foreach (var binding in AnimationCurveBindingAnimatorMotionQ)
                            bindings.Add(binding);
                        break;
                }
            }
            #endregion

            return bindings;
        }

        public int SelectionGameObjectsIndexOf(GameObject go)
        {
            if (selectionGameObjects != null)
            {
                for (int i = 0; i < selectionGameObjects.Count; i++)
                {
                    if (selectionGameObjects[i] == go)
                        return i;
                }
            }
            return -1;
        }
        public bool SelectionGameObjectsContains(HumanBodyBones humanIndex)
        {
            if (selectionBones != null)
            {
                foreach (var boneIndex in selectionBones)
                {
                    if (boneIndex2humanoidIndex[boneIndex] == humanIndex)
                        return true;
                }
            }
            if (selectionHumanVirtualBones != null)
            {
                foreach (var vb in selectionHumanVirtualBones)
                {
                    if (vb == humanIndex)
                        return true;
                }
            }
            return false;
        }
        public HumanBodyBones SelectionGameObjectHumanoidIndex()
        {
            var humanoidIndex = HumanoidBonesIndexOf(selectionActiveGameObject);
            if (humanoidIndex < 0 && selectionHumanVirtualBones != null && selectionHumanVirtualBones.Count > 0)
                humanoidIndex = selectionHumanVirtualBones[0];
            return humanoidIndex;
        }
        public List<HumanBodyBones> SelectionGameObjectsHumanoidIndex()
        {
            List<HumanBodyBones> list = new List<HumanBodyBones>();
            if (isHuman)
            {
                if (selectionBones != null)
                {
                    foreach (var boneIndex in selectionBones)
                    {
                        var humanoidIndex = boneIndex2humanoidIndex[boneIndex];
                        if (humanoidIndex < 0) continue;
                        list.Add(humanoidIndex);
                    }
                }
                if (selectionHumanVirtualBones != null)
                {
                    foreach (var humanoidIndex in selectionHumanVirtualBones)
                    {
                        if (humanoidIndex < 0) continue;
                        list.Add(humanoidIndex);
                    }
                }
            }
            return list;
        }
        public bool IsSelectionGameObjectsHumanoidIndexContains(HumanBodyBones humanoidIndex)
        {
            if (isHuman)
            {
                if (selectionBones != null)
                {
                    foreach (var boneIndex in selectionBones)
                    {
                        if (boneIndex2humanoidIndex[boneIndex] == humanoidIndex)
                            return true;
                    }
                }
                if (selectionHumanVirtualBones != null)
                {
                    foreach (var hi in selectionHumanVirtualBones)
                    {
                        if (hi == humanoidIndex)
                            return true;
                    }
                }
            }
            return false;
        }
        public List<int> SelectionGameObjectsOtherHumanoidBoneIndex()
        {
            List<int> list = new List<int>();
            if (isHuman)
            {
                if (selectionBones != null)
                {
                    foreach (var boneIndex in selectionBones)
                    {
                        if (boneIndex == rootMotionBoneIndex ||
                            boneIndex2humanoidIndex[boneIndex] >= 0) continue;
                        list.Add(boneIndex);
                    }
                }
            }
            else
            {
                if (selectionBones != null)
                {
                    list.AddRange(selectionBones);
                }
            }
            return list;
        }
        public List<int> SelectionGameObjectsMuscleIndex(int dofIndex = -1)
        {
            List<int> list = new List<int>();
            var humanoidIndexs = SelectionGameObjectsHumanoidIndex();
            if (dofIndex < 0)
            {
                foreach (var humanoidIndex in humanoidIndexs)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        var muscleIndex = HumanTrait.MuscleFromBone((int)humanoidIndex, j);
                        if (muscleIndex < 0) continue;
                        list.Add(muscleIndex);
                    }
                }
            }
            else if (dofIndex >= 0 && dofIndex <= 2)
            {
                foreach (var humanoidIndex in humanoidIndexs)
                {
                    int muscleIndex = HumanTrait.MuscleFromBone((int)humanoidIndex, dofIndex);
                    if (muscleIndex < 0) continue;
                    list.Add(muscleIndex);
                }
            }
            return list;
        }
        public float GetSelectionSuppressPowerRate()
        {
            int maxLevel = 1;
            if (selectionBones != null)
            {
                foreach (var boneIndex in selectionBones)
                {
                    int level = 1;
                    var bi = boneIndex;
                    while (parentBoneIndexes[bi] >= 0)
                    {
                        if (selectionBones.Contains(parentBoneIndexes[bi]))
                            level++;
                        bi = parentBoneIndexes[bi];
                    }
                    maxLevel = Math.Max(maxLevel, level);
                }
            }
            if (isHuman)
            {
                foreach (var humanoidIndex in SelectionGameObjectsHumanoidIndex())
                {
                    int level = 1;
                    var hi = humanoidIndex;
                    var parentHi = (HumanBodyBones)HumanTrait.GetParentBone((int)hi);
                    while (parentHi >= 0)
                    {
                        if (IsSelectionGameObjectsHumanoidIndexContains(parentHi))
                            level++;
                        hi = parentHi;
                        parentHi = (HumanBodyBones)HumanTrait.GetParentBone((int)hi);
                    }
                    maxLevel = Math.Max(maxLevel, level);
                }
            }

            if (maxLevel > 1)
            {
                return 1f / maxLevel;
            }
            else
            {
                return 1f;
            }
        }

        public int BonesIndexOf(GameObject go)
        {
            if (boneDic != null && go != null)
            {
                int boneIndex;
                if (boneDic.TryGetValue(go, out boneIndex))
                {
                    return boneIndex;
                }
            }
            return -1;
        }
        public HumanBodyBones HumanoidBonesIndexOf(GameObject go)
        {
            if (go == null || !isHuman) return (HumanBodyBones)(-1);
            if (humanoidBones != null)
            {
                var boneIndex = BonesIndexOf(go);
                if (boneIndex >= 0)
                {
                    return boneIndex2humanoidIndex[boneIndex];
                }
            }
            return (HumanBodyBones)(-1);
        }
        #endregion

        #region Bounds
        public Bounds GetSelectionBounds(float sizeAdjustment = 0f)
        {
            bool done = false;
            var bounds = new Bounds(editGameObject.transform.position, gameObjectBounds.size * sizeAdjustment);
            Action<Vector3> SetBounds = (pos) =>
            {
                if (!done)
                {
                    bounds.center = pos;
                    done = true;
                }
                else
                {
                    bounds.Encapsulate(pos);
                }
            };
            #region Bone
            if (selectionBones != null)
            {
                foreach (var boneIndex in selectionBones)
                {
                    if (isHuman && boneIndex == 0) continue;
                    SetBounds(editBones[boneIndex].transform.position);
                }
            }
            #endregion
            if (isHuman)
            {
                #region Root
                if (SelectionGameObjectsIndexOf(vaw.gameObject) >= 0)
                {
                    SetBounds(humanWorldRootPositionCache);
                }
                #endregion
                #region VirtualBone
                if (selectionHumanVirtualBones != null)
                {
                    foreach (var virtualBone in selectionHumanVirtualBones)
                    {
                        SetBounds(GetHumanVirtualBonePosition(virtualBone));
                    }
                }
                #endregion
                #region AnimatorIK
                if (animatorIK.ikActiveTarget != AnimatorIKCore.IKTarget.None)
                {
                    foreach (var ikTarget in animatorIK.ikTargetSelect)
                    {
                        SetBounds(animatorIK.ikData[(int)ikTarget].worldPosition);
                    }
                }
                #endregion
            }
            #region OriginalIK
            if (originalIK.ikActiveTarget >= 0)
            {
                foreach (var ikTarget in originalIK.ikTargetSelect)
                {
                    SetBounds(originalIK.ikData[ikTarget].worldPosition);
                }
            }
            #endregion
            return bounds;
        }
        public Quaternion GetSelectionBoundsRotation()
        {
            Quaternion rotation = Quaternion.identity;
            List<Vector3> posList = new List<Vector3>();
            #region Bone
            if (selectionBones != null)
            {
                foreach (var boneIndex in selectionBones)
                {
                    if (isHuman && boneIndex == 0) continue;
                    posList.Add(editBones[boneIndex].transform.position);
                    rotation = editBones[boneIndex].transform.rotation;
                }
            }
            #endregion
            if (isHuman)
            {
                #region Root
                if (SelectionGameObjectsIndexOf(vaw.gameObject) >= 0)
                {
                    posList.Add(humanWorldRootPositionCache);
                    rotation = humanWorldRootRotationCache;
                }
                #endregion
                #region VirtualBone
                if (selectionHumanVirtualBones != null)
                {
                    foreach (var virtualBone in selectionHumanVirtualBones)
                    {
                        posList.Add(GetHumanVirtualBonePosition(virtualBone));
                        rotation = GetHumanVirtualBoneRotation(virtualBone);
                    }
                }
                #endregion
                #region AnimatorIK
                if (animatorIK.ikActiveTarget != AnimatorIKCore.IKTarget.None)
                {
                    foreach (var ikTarget in animatorIK.ikTargetSelect)
                    {
                        posList.Add(animatorIK.ikData[(int)ikTarget].worldPosition);
                        rotation = animatorIK.ikData[(int)ikTarget].worldRotation;
                    }
                }
                #endregion
            }
            #region OriginalIK
            if (originalIK.ikActiveTarget >= 0)
            {
                foreach (var ikTarget in originalIK.ikTargetSelect)
                {
                    posList.Add(originalIK.ikData[ikTarget].worldPosition);
                    rotation = originalIK.ikData[ikTarget].worldRotation;
                }
            }
            #endregion

            if (posList.Count > 0)
            {
                var center = GetSelectionBounds().center;
                Vector3 normal = Vector3.zero;
                {
                    for (int i = 0; i < posList.Count; i++)
                    {
                        for (int j = 0; j < posList.Count; j++)
                        {
                            if (i == j) continue;
                            var cross = Vector3.Cross(posList[i] - center, posList[j] - center);
                            var dot = Vector3.Dot(normal, cross);
                            if (dot < 0f)
                                cross = -cross;
                            normal += cross;
                        }
                    }
                    var vecActive = posList[0] - center;
                    vecActive.Normalize();
                    if (vecActive.sqrMagnitude > 0f)
                    {
                        normal.Normalize();
                        if (normal.sqrMagnitude > 0f)
                        {
                            rotation = Quaternion.LookRotation(normal);
                        }
                        else
                        {
                            rotation = Quaternion.LookRotation(vecActive);
                            normal = rotation * Vector3.up;
                        }
                        var angle = Vector3.SignedAngle(rotation * Vector3.right, vecActive, normal);
                        var rightRotation = Quaternion.AngleAxis(angle, normal);
                        rotation = rightRotation * rotation;
                    }
                }
            }
            return rotation;
        }
        #endregion

        #region ShowBone
        public bool[] boneShowFlags;
        public List<int> skeletonFKShowBoneList { get; private set; }
        public bool[] skeletonFKShowBoneFlag { get; private set; }
        public List<Vector2Int> skeletonIKShowBoneList { get; private set; }
        public int boneShowCount { get; private set; }

        private void InitializeBoneShowFlags()
        {
            boneShowFlags = new bool[bones.Length];
            if (isHuman)
            {
                ActionBoneShowFlagsHumanoidBody((index) =>
                {
                    boneShowFlags[index] = true;
                });
            }
            else
            {
                bool done = false;
                ActionBoneShowFlagsHaveWeight((index) =>
                {
                    boneShowFlags[index] = true;
                    done = true;
                });
                if (!done)
                {
                    ActionBoneShowFlagsHaveRendererParent((index) =>
                    {
                        boneShowFlags[index] = true;
                        done = true;
                    });
                }
                if (!done)
                {
                    ActionBoneShowFlagsHaveRenderer((index) =>
                    {
                        boneShowFlags[index] = true;
                        done = true;
                    });
                }
                if (!done)
                {
                    ActionBoneShowFlagsAll((index) =>
                    {
                        boneShowFlags[index] = true;
                        done = true;
                    });
                }
                {
                    if (rootMotionBoneIndex >= 0)
                    {
                        boneShowFlags[rootMotionBoneIndex] = true;
                        boneShowFlags[0] = false;
                    }
                    else if (boneShowFlags.Length > 0)
                    {
                        boneShowFlags[0] = true;
                    }
                }
            }
            {
                var animators = vaw.gameObject.GetComponentsInChildren<Animator>(true);
                foreach (var animator in animators)
                {
                    if (animator == null || animator == vaw.animator)
                        continue;
                    Action<int> HideFlag = null;
                    HideFlag = (bi) =>
                    {
                        if (bi < 0) return;
                        boneShowFlags[bi] = false;
                        for (int i = 0; i < bones[bi].transform.childCount; i++)
                        {
                            HideFlag(BonesIndexOf(bones[bi].transform.GetChild(i).gameObject));
                        }
                    };
                    HideFlag(BonesIndexOf(animator.gameObject));
                }
            }
            OnBoneShowFlagsUpdated.Invoke();
        }
        private List<GameObject> GetReloadBoneShowFlags()
        {
            var reload = new List<GameObject>();
            for (int i = 0; i < boneShowFlags.Length; i++)
            {
                if (!boneShowFlags[i] || bones[i] == null)
                    continue;
                reload.Add(bones[i]);
            }
            return reload;
        }
        private void SetReloadBoneShowFlags(List<GameObject> reload)
        {
            boneShowFlags = new bool[bones.Length];
            foreach (var go in reload)
            {
                int boneIndex = BonesIndexOf(go);
                if (boneIndex < 0)
                    continue;
                boneShowFlags[boneIndex] = true;
            }
            OnBoneShowFlagsUpdated.Invoke();
        }

        public void ActionBoneShowFlagsAll(Action<int> action)
        {
            if (boneShowFlags == null) return;
            for (int i = 0; i < boneShowFlags.Length; i++)
                action(i);
        }
        public void ActionBoneShowFlagsHumanoidBody(Action<int> action)
        {
            action(0);    //Root
            for (int i = (int)HumanBodyBones.LeftUpperLeg; i <= (int)HumanBodyBones.RightToes; i++)
            {
                var boneIndex = humanoidIndex2boneIndex[i];
                if (boneIndex < 0) continue;
                action(boneIndex);
            }
            {
                var boneIndex = humanoidIndex2boneIndex[(int)HumanBodyBones.UpperChest];
                if (boneIndex >= 0)
                    action(boneIndex);
            }
        }
        public void ActionBoneShowFlagsHumanoidFace(Action<int> action)
        {
            for (int i = (int)HumanBodyBones.LeftEye; i <= (int)HumanBodyBones.Jaw; i++)
            {
                var boneIndex = humanoidIndex2boneIndex[i];
                if (boneIndex < 0) continue;
                action(boneIndex);
            }
        }
        public void ActionBoneShowFlagsHumanoidLeftHand(Action<int> action)
        {
            for (int i = (int)HumanBodyBones.LeftThumbProximal; i <= (int)HumanBodyBones.LeftLittleDistal; i++)
            {
                var boneIndex = humanoidIndex2boneIndex[i];
                if (boneIndex < 0) continue;
                action(boneIndex);
            }
        }
        public void ActionBoneShowFlagsHumanoidRightHand(Action<int> action)
        {
            for (int i = (int)HumanBodyBones.RightThumbProximal; i <= (int)HumanBodyBones.RightLittleDistal; i++)
            {
                var boneIndex = humanoidIndex2boneIndex[i];
                if (boneIndex < 0) continue;
                action(boneIndex);
            }
        }
        public void ActionBoneShowFlagsHaveWeight(Action<int> action)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                if (renderer is SkinnedMeshRenderer)
                {
                    var skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
                    var mesh = skinnedMeshRenderer.sharedMesh;
                    if (mesh != null)
                    {
                        var meshBones = skinnedMeshRenderer.bones;
                        Dictionary<int, int> list = new Dictionary<int, int>();
                        Action<int> SetBoneIndex = (index) =>
                        {
                            if (index < 0 || index >= meshBones.Length)
                                return;
                            if (list.ContainsKey(index))
                                return;
                            if (meshBones[index] != null)
                                list.Add(index, BonesIndexOf(meshBones[index].gameObject));
                            else
                                list.Add(index, -1);
                        };
                        foreach (var boneWeight in mesh.boneWeights)
                        {
                            if (boneWeight.weight0 > 0f)
                                SetBoneIndex(boneWeight.boneIndex0);
                            if (boneWeight.weight1 > 0f)
                                SetBoneIndex(boneWeight.boneIndex1);
                            if (boneWeight.weight2 > 0f)
                                SetBoneIndex(boneWeight.boneIndex2);
                            if (boneWeight.weight3 > 0f)
                                SetBoneIndex(boneWeight.boneIndex3);
                        }
                        foreach (var pair in list)
                        {
                            if (pair.Value >= 0)
                                action(pair.Value);
                        }
                    }
                }
            }
        }
        public void ActionBoneShowFlagsHaveRenderer(Action<int> action)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var boneIndex = BonesIndexOf(renderer.transform.gameObject);
                if (boneIndex >= 0)
                    action(boneIndex);
            }
        }
        public void ActionBoneShowFlagsHaveRendererParent(Action<int> action)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var parent = renderer.transform.parent;
                if (parent == null) continue;
                var boneIndex = BonesIndexOf(parent.gameObject);
                if (boneIndex >= 0)
                    action(boneIndex);
            }
        }

        public bool IsShowBone(int boneIndex)
        {
            if (boneIndex < 0 || boneIndex >= bones.Length || bones[boneIndex] == null || !boneShowFlags[boneIndex])
                return false;
            if (isHuman)
            {
                if (animatorIK.IsIKBone(boneIndex2humanoidIndex[boneIndex]) != AnimatorIKCore.IKTarget.None)
                    return false;
            }
            if (originalIK.IsIKBone(boneIndex) >= 0)
                return false;
            return true;
        }
        public bool IsShowVirtualBone(HumanBodyBones humanoidIndex)
        {
            if (!isHuman)
                return false;
            if (humanoidBones[(int)humanoidIndex] != null || HumanVirtualBones[(int)humanoidIndex] == null)
                return false;
            {
                var ikIndex = animatorIK.IsIKBone(humanoidIndex);
                if (ikIndex >= 0 && ikIndex < AnimatorIKCore.IKTarget.Total)
                {
                    if (animatorIK.ikData[(int)ikIndex].enable)
                        return false;
                }
            }
            {
                var phi = GetHumanVirtualBoneParentBone(humanoidIndex);
                if (phi < 0 || humanoidIndex2boneIndex[(int)phi] < 0) return false;
                if (!IsShowBone(humanoidIndex2boneIndex[(int)phi])) return false;
            }
            return true;
        }
        public Action OnHierarchyUpdated;
        public Action OnBoneShowFlagsUpdated;
        public void UpdateSkeletonShowBoneList()
        {
            if (isEditError) return;

            skeletonFKShowBoneFlag = new bool[bones.Length];

            Action<int, bool> SetParentFlags = null;
            SetParentFlags = (boneIndex, flag) =>
            {
                if (parentBoneIndexes[boneIndex] < 0 || parentBoneIndexes[parentBoneIndexes[boneIndex]] < 0) return;
                skeletonFKShowBoneFlag[parentBoneIndexes[boneIndex]] = flag;
                SetParentFlags(parentBoneIndexes[boneIndex], flag);
            };
            for (int i = 0; i < bones.Length; i++)
            {
                skeletonFKShowBoneFlag[i] = boneShowFlags[i] && parentBoneIndexes[i] >= 0;
                if (skeletonFKShowBoneFlag[i])
                    SetParentFlags(i, true);
            }
            {
                if (skeletonFKShowBoneList == null)
                    skeletonFKShowBoneList = new List<int>(skeletonFKShowBoneFlag.Length);
                else
                    skeletonFKShowBoneList.Clear();
                for (int i = 0; i < skeletonFKShowBoneFlag.Length; i++)
                {
                    if (skeletonFKShowBoneFlag[i])
                        skeletonFKShowBoneList.Add(i);
                }
            }
            {
                if (skeletonIKShowBoneList == null)
                    skeletonIKShowBoneList = new List<Vector2Int>(skeletonFKShowBoneFlag.Length);
                else
                    skeletonIKShowBoneList.Clear();
                {
                    Action<int> AddBone = (boneIndex) =>
                    {
                        if (!skeletonFKShowBoneFlag[boneIndex])
                            return;
                        skeletonIKShowBoneList.Add(new Vector2Int(boneIndex, -1));
                    };
#if UNITY_2019_1_OR_NEWER
                    if (IsEnableUpdateHumanoidFootIK())
#else
                    if (IsEnableUpdateHumanoidFootIK() && uAw.GetLinkedWithTimeline())
#endif
                    {
                        AddBone(humanoidIndex2boneIndex[(int)HumanBodyBones.LeftLowerLeg]);
                        AddBone(humanoidIndex2boneIndex[(int)HumanBodyBones.LeftFoot]);
                        AddBone(humanoidIndex2boneIndex[(int)HumanBodyBones.RightLowerLeg]);
                        AddBone(humanoidIndex2boneIndex[(int)HumanBodyBones.RightFoot]);
                    }
                }
            }
            boneShowCount = boneShowFlags.Count(n => n);
        }
        #endregion

        #region UnityTool
        public Tool lastTool { get; set; }

        public void EnableCustomTools(Tool t)
        {
            if (Tools.current != Tool.None)
            {
                lastTool = Tools.current;
                Tools.current = t;
            }
        }
        public void DisableCustomTools()
        {
            if (lastTool != Tool.None)
            {
                Tools.current = lastTool;
                lastTool = Tool.None;
            }
        }
        public Tool CurrentTool()
        {
            Tool tool = lastTool;
            var humanoidIndex = SelectionGameObjectHumanoidIndex();
            if (animatorIK.ikActiveTarget != AnimatorIKCore.IKTarget.None)
            {
                tool = Tool.Rotate;
            }
            else if (originalIK.ikActiveTarget >= 0)
            {
                tool = Tool.Rotate;
            }
            else if (isHuman && selectionActiveBone >= 0 && selectionActiveBone == rootMotionBoneIndex)
            {
                if (lastTool == Tool.Move) tool = Tool.Move;
                else tool = Tool.Rotate;
            }
            else if (humanoidIndex >= 0)
            {
                switch (lastTool)
                {
                    case Tool.Move:
                        if (!humanoidHasTDoF || HumanBonesAnimatorTDOFIndex[(int)humanoidIndex] == null ||
                            editHumanoidBones[(int)humanoidIndex] == null || editHumanoidBones[(int)HumanBonesAnimatorTDOFIndex[(int)humanoidIndex].parent] == null)
                        {
                            tool = Tool.Rotate;
                        }
                        break;
                    default:
                        tool = Tool.Rotate;
                        break;
                }
            }
            else if (selectionMotionTool)
            {
                if (lastTool == Tool.Move) tool = Tool.Move;
                else tool = Tool.Rotate;
            }
            else
            {
                switch (lastTool)
                {
                    case Tool.Move:
                    case Tool.Scale:
                        break;
                    default:
                        tool = Tool.Rotate;
                        break;
                }
            }
            return tool;
        }
        #endregion

        #region Preview
        public void PreviewGUI()
        {
            if (uAvatarPreview != null)
            {
                {
                    EditorGUILayout.BeginHorizontal("preToolbar", GUILayout.Height(17f));
                    GUILayout.FlexibleSpace();
                    Rect lastRect = GUILayoutUtility.GetLastRect();
                    if (currentClip != null)
                        GUI.Label(lastRect, currentClip.name, "preToolbar2");
                    uAvatarPreview.OnPreviewSettings();
                    EditorGUILayout.EndHorizontal();
                }
                if (uAvatarPreview.playing)
                {
                    vaw.Repaint();
                }
                else
                {
                    if (Event.current.type == EventType.Repaint)
                        uAvatarPreview.ForceUpdate();
                }

                {
                    var rect = EditorGUILayout.GetControlRect(false, 0);
                    rect.height = Math.Max(vaw.position.height - rect.y, 0);
                    uAvatarPreview.OnGUI(rect, "preBackground");
                }
            }
        }
        #endregion

        #region SynchronizeAnimation
        public void SetSynchronizeAnimation(bool enable)
        {
            if (EditorApplication.isPlaying)
                return;
            if (uAw.GetLinkedWithTimeline())
                return;

            if (enable && synchronizeAnimation == null)
            {
                synchronizeAnimation = new SynchronizeAnimation();
                synchronizeAnimation.SetTime(currentTime);
            }
            else if (!enable && synchronizeAnimation != null)
            {
                synchronizeAnimation.Release();
                synchronizeAnimation = null;
            }
        }
        #endregion

        #region SaveSettings
        public void LoadSaveSettings()
        {
            var saveSettings = vaw.gameObject != null ? vaw.gameObject.GetComponent<VeryAnimationSaveSettings>() : null;
            if (saveSettings == null)
                return;

            #region bones
            if (saveSettings.bonePaths != null && saveSettings.bonePaths.Length > 0)
            {
                #region Show
                if (saveSettings.showBones != null && saveSettings.showBones.Length > 0)
                {
                    for (int i = 0; i < boneShowFlags.Length; i++)
                        boneShowFlags[i] = false;
                    foreach (var index in saveSettings.showBones)
                    {
                        if (index < 0 || index >= saveSettings.bonePaths.Length) continue;
                        var boneIndex = GetBoneIndexFromPath(saveSettings.bonePaths[index]);
                        if (boneIndex < 0) continue;
                        boneShowFlags[boneIndex] = true;
                    }
                }
                #endregion
                #region Foldout
                if (saveSettings.foldoutBones != null && saveSettings.foldoutBones.Length > 0)
                {
                    if (VeryAnimationControlWindow.instance != null)
                    {
                        VeryAnimationControlWindow.instance.CollapseAll();
                        foreach (var index in saveSettings.foldoutBones)
                        {
                            if (index < 0 || index >= saveSettings.bonePaths.Length) continue;
                            var boneIndex = GetBoneIndexFromPath(saveSettings.bonePaths[index]);
                            if (boneIndex < 0) continue;
                            VeryAnimationControlWindow.instance.SetExpand(bones[boneIndex], true);
                        }
                    }
                }
                #endregion
                #region MirrorBone
                if (saveSettings.mirrorBones != null && saveSettings.mirrorBones.Length > 0)
                {
                    BonesMirrorInitialize();
                    for (int i = 0; i < saveSettings.mirrorBones.Length; i++)
                    {
                        var bi = saveSettings.mirrorBones[i];
                        if (bi < 0 || bi >= saveSettings.bonePaths.Length) continue;
                        var boneIndex = GetBoneIndexFromPath(saveSettings.bonePaths[i]);
                        var mboneIndex = GetBoneIndexFromPath(saveSettings.bonePaths[bi]);
                        if (boneIndex < 0 || mboneIndex < 0 || boneIndex == mboneIndex) continue;
                        mirrorBoneIndexes[boneIndex] = mboneIndex;
                    }
                    UpdateBonesMirrorOther();
                }
                #endregion
            }
            #endregion
            #region MirrorBlendShape
            if (saveSettings.mirrorBlendShape != null && saveSettings.mirrorBlendShape.Length > 0)
            {
                BlendShapeMirrorInitialize();
                var renderers = vaw.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var data in saveSettings.mirrorBlendShape)
                {
                    if (renderers.Contains(data.renderer))
                    {
                        for (int i = 0; i < data.names.Length && i < data.mirrorNames.Length; i++)
                        {
                            if (i < 0 || i >= data.mirrorNames.Length) continue;
                            ChangeBlendShapeMirror(data.renderer, data.names[i], data.mirrorNames[i]);
                        }
                    }
                }
            }
            #endregion

            animatorIK.LoadIKSaveSettings(saveSettings.animatorIkData);
            originalIK.LoadIKSaveSettings(saveSettings.originalIkData);

            #region SelectionSet
            selectionSetList = LoadSelectionSaveSettings(saveSettings.selectionData).ToList();
            #endregion

            #region Animation
            if (!EditorApplication.isPlaying && !uAw.GetLinkedWithTimeline())
            {
                if (saveSettings.lastSelectAnimationClip != null)
                {
                    if (vaw.IsContainsSelectionAnimationClip(saveSettings.lastSelectAnimationClip))
                        uAw.SetSelectionAnimationClip(saveSettings.lastSelectAnimationClip);
                }
            }
            #endregion

            #region HandPoseSet
            handPoseSetList.Clear();
            if (saveSettings.handPoseList != null && saveSettings.handPoseList.Length > 0)
            {
                foreach (var set in saveSettings.handPoseList)
                {
                    var poseTemplate = ScriptableObject.CreateInstance<PoseTemplate>();
                    poseTemplate.name = set.name;
                    {
                        poseTemplate.musclePropertyNames = new string[set.musclePropertyNames.Length];
                        set.musclePropertyNames.CopyTo(poseTemplate.musclePropertyNames, 0);
                        poseTemplate.muscleValues = new float[set.muscleValues.Length];
                        set.muscleValues.CopyTo(poseTemplate.muscleValues, 0);
                    }
                    poseTemplate.isHuman = true;
                    string[] rightMusclePropertyNames = new string[poseTemplate.musclePropertyNames.Length];
                    for (int i = 0; i < poseTemplate.musclePropertyNames.Length; i++)
                    {
                        rightMusclePropertyNames[i] = poseTemplate.musclePropertyNames[i].Replace("Left", "Right");
                    }
                    handPoseSetList.Add(new HandPoseSet()
                    {
                        poseTemplate = poseTemplate,
                        leftMusclePropertyNames = poseTemplate.musclePropertyNames,
                        rightMusclePropertyNames = rightMusclePropertyNames,
                    });
                }
            }
            #endregion

            #region BlendShapeSet
            blendShapeSetList.Clear();
            if (saveSettings.blendShapeList != null && saveSettings.blendShapeList.Length > 0)
            {
                foreach (var set in saveSettings.blendShapeList)
                {
                    var poseTemplate = ScriptableObject.CreateInstance<PoseTemplate>();
                    {
                        poseTemplate.name = set.name;
                        poseTemplate.blendShapePaths = set.blendShapePaths.ToArray();
                        poseTemplate.blendShapeValues = new PoseTemplate.BlendShapeData[set.blendShapeValues.Length];
                        for (int i = 0; i < set.blendShapeValues.Length; i++)
                        {
                            poseTemplate.blendShapeValues[i] = new PoseTemplate.BlendShapeData()
                            {
                                names = set.blendShapeValues[i].names.ToArray(),
                                weights = set.blendShapeValues[i].weights.ToArray(),
                            };
                        }
                    }
                    blendShapeSetList.Add(new BlendShapeSet()
                    {
                        poseTemplate = poseTemplate,
                    });
                }
            }
            #endregion
        }
        public void SaveSaveSettings()
        {
            var saveSettings = vaw.gameObject != null ? vaw.gameObject.GetComponent<VeryAnimationSaveSettings>() : null;
            if (saveSettings == null)
                return;

            #region bones
            {
                saveSettings.bonePaths = new string[bonePaths.Length];
                bonePaths.CopyTo(saveSettings.bonePaths, 0);
                #region Show
                if (boneShowFlags != null && bones != null && boneShowFlags.Length == bones.Length)
                {
                    var list = new List<int>();
                    for (int i = 0; i < boneShowFlags.Length; i++)
                    {
                        if (bones[i] == null || !boneShowFlags[i]) continue;
                        list.Add(i);
                    }
                    saveSettings.showBones = list.ToArray();
                }
                #endregion
                #region Foldout
                if (VeryAnimationControlWindow.instance != null)
                {
                    var list = new List<int>();
                    VeryAnimationControlWindow.instance.ActionAllExpand((go) =>
                    {
                        var boneIndex = BonesIndexOf(go);
                        if (boneIndex >= 0)
                            list.Add(boneIndex);
                    });
                    saveSettings.foldoutBones = list.ToArray();
                }
                #endregion
                #region MirrorBone
                if (mirrorBoneIndexes != null && bones != null && mirrorBoneIndexes.Length == bones.Length)
                {
                    var list = new int[mirrorBoneIndexes.Length];
                    for (int i = 0; i < mirrorBoneIndexes.Length; i++)
                    {
                        list[i] = -1;
                        if (i != mirrorBoneIndexes[i])
                            list[i] = mirrorBoneIndexes[i];
                    }
                    saveSettings.mirrorBones = list;
                }
                #endregion
            }
            #endregion
            #region MirrorBlendShape
            if (mirrorBlendShape != null)
            {
                var list = new List<VeryAnimationSaveSettings.MirrorBlendShape>(mirrorBlendShape.Count);
                foreach (var pair in mirrorBlendShape)
                {
                    var data = new VeryAnimationSaveSettings.MirrorBlendShape()
                    {
                        renderer = pair.Key,
                        names = pair.Value.Keys.ToArray(),
                        mirrorNames = pair.Value.Values.ToArray(),
                    };
                    list.Add(data);
                }
                saveSettings.mirrorBlendShape = list.ToArray();
            }
            #endregion

            saveSettings.animatorIkData = animatorIK.SaveIKSaveSettings();
            saveSettings.originalIkData = originalIK.SaveIKSaveSettings();

            #region SelectionSet
            saveSettings.selectionData = SaveSelectionSaveSettings();
            #endregion

            #region Animation
            saveSettings.lastSelectAnimationClip = uAw.GetSelectionAnimationClip();
            #endregion

            #region HandPoseSet
            saveSettings.handPoseList = new VeryAnimationSaveSettings.HandPoseSet[handPoseSetList != null ? handPoseSetList.Count : 0];
            if (handPoseSetList != null)
            {
                for (int i = 0; i < handPoseSetList.Count; i++)
                {
                    if (handPoseSetList[i].poseTemplate == null)
                        continue;
                    handPoseSetList[i].SetLeft();
                    var srcPoseTemplate = handPoseSetList[i].poseTemplate;
                    var set = new VeryAnimationSaveSettings.HandPoseSet()
                    {
                        name = srcPoseTemplate.name,
                    };
                    {
                        var muscleDic = new Dictionary<string, float>();
                        if (srcPoseTemplate.musclePropertyNames != null && musclePropertyName.PropertyNames != null)
                        {
                            var beginMuscle = HumanTrait.MuscleFromBone((int)HumanBodyBones.LeftThumbProximal, 2);
                            var endMuscle = HumanTrait.MuscleFromBone((int)HumanBodyBones.LeftLittleDistal, 2);
                            for (int muscle = beginMuscle; muscle <= endMuscle; muscle++)
                            {
                                var index = ArrayUtility.IndexOf(srcPoseTemplate.musclePropertyNames, musclePropertyName.PropertyNames[muscle]);
                                if (index < 0) continue;
                                muscleDic.Add(srcPoseTemplate.musclePropertyNames[index], srcPoseTemplate.muscleValues[index]);
                            }
                        }
                        set.musclePropertyNames = muscleDic.Keys.ToArray();
                        set.muscleValues = muscleDic.Values.ToArray();
                    }
                    saveSettings.handPoseList[i] = set;
                }
            }
            #endregion

            #region BlendShapeSet
            saveSettings.blendShapeList = new VeryAnimationSaveSettings.BlendShapeSet[blendShapeSetList != null ? blendShapeSetList.Count : 0];
            if (blendShapeSetList != null)
            {
                for (int i = 0; i < blendShapeSetList.Count; i++)
                {
                    if (blendShapeSetList[i].poseTemplate == null)
                        continue;
                    var set = new VeryAnimationSaveSettings.BlendShapeSet()
                    {
                        name = blendShapeSetList[i].poseTemplate.name,
                        blendShapePaths = blendShapeSetList[i].poseTemplate.blendShapePaths.ToArray(),
                    };
                    set.blendShapeValues = new VeryAnimationSaveSettings.BlendShapeSet.BlendShapeData[blendShapeSetList[i].poseTemplate.blendShapeValues.Length];
                    for (int j = 0; j < blendShapeSetList[i].poseTemplate.blendShapeValues.Length; j++)
                    {
                        set.blendShapeValues[j] = new VeryAnimationSaveSettings.BlendShapeSet.BlendShapeData()
                        {
                            names = blendShapeSetList[i].poseTemplate.blendShapeValues[j].names.ToArray(),
                            weights = blendShapeSetList[i].poseTemplate.blendShapeValues[j].weights.ToArray(),
                        };
                    }
                    saveSettings.blendShapeList[i] = set;
                }
            }
            #endregion
        }

        public VeryAnimationSaveSettings.SelectionData[] LoadSelectionSaveSettings(VeryAnimationSaveSettings.SelectionData[] selectionData)
        {
            var list = new List<VeryAnimationSaveSettings.SelectionData>();
            if (selectionData != null)
            {
                foreach (var data in selectionData)
                {
                    var newData = new VeryAnimationSaveSettings.SelectionData()
                    {
                        name = data.name,
                    };
                    {
                        var bones = new List<GameObject>();
                        if (data.bones != null)
                        {
                            foreach (var bone in data.bones)
                            {
                                if (bone == null) continue;
                                bones.Add(bone);
                            }
                            if (data.bonePaths != null && data.bones.Length == data.bonePaths.Length)
                            {
                                bones.Clear();
                                foreach (var bonePath in data.bonePaths)
                                {
                                    var bone = vaw.gameObject.transform.Find(bonePath);
                                    if (bone == null) continue;
                                    bones.Add(bone.gameObject);
                                }
                            }
                        }
                        newData.bones = bones.ToArray();
                    }
                    if (isHuman)
                    {
                        var vbones = new List<HumanBodyBones>();
                        if (data.virtualBones != null)
                        {
                            foreach (var vbone in data.virtualBones)
                            {
                                if (vbone < 0 || vbone >= HumanBodyBones.LastBone || humanoidBones[(int)vbone] != null) continue;
                                vbones.Add(vbone);
                            }
                        }
                        newData.virtualBones = vbones.ToArray();
                    }
                    list.Add(newData);
                }
            }
            return list.ToArray();
        }
        public VeryAnimationSaveSettings.SelectionData[] SaveSelectionSaveSettings()
        {
            if (selectionSetList == null)
                return new VeryAnimationSaveSettings.SelectionData[0];
            var array = selectionSetList.ToArray();
            for (int i = 0; i < array.Length; i++)
            {
                array[i].bonePaths = new string[array[i].bones.Length];
                for (int j = 0; j < array[i].bones.Length; j++)
                {
                    if (array[i].bones[j] == null)
                        continue;
                    array[i].bonePaths[j] = AnimationUtility.CalculateTransformPath(array[i].bones[j].transform, vaw.gameObject.transform);
                }
            }
            return array;
        }

        #endregion

        #region Etc
        public void ActionAllBoneChildren(int boneIndex, Action<int> action)
        {
            var t = bones[boneIndex].transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var childIndex = BonesIndexOf(t.GetChild(i).gameObject);
                if (childIndex < 0) continue;
                action.Invoke(childIndex);
                ActionAllBoneChildren(childIndex, action);
            }
        }
        public void ActionAllVirtualBoneChildren(int boneIndex, Action<HumanBodyBones> action)
        {
            if (!isHuman) return;
            Func<HumanBodyBones, bool> Check = (hi) =>
            {
                Action<HumanBodyBones> Invoke = (hhi) =>
                {
                    if (humanoidBones[(int)hhi] == null)
                        action.Invoke(hhi);
                };
                switch (hi)
                {
                    case HumanBodyBones.Hips:
                    case HumanBodyBones.Spine:
                        Invoke(HumanBodyBones.Chest);
                        Invoke(HumanBodyBones.Neck);
                        Invoke(HumanBodyBones.LeftShoulder);
                        Invoke(HumanBodyBones.RightShoulder);
                        Invoke(HumanBodyBones.UpperChest);
                        return true;
                    case HumanBodyBones.Chest:
                        Invoke(HumanBodyBones.Neck);
                        Invoke(HumanBodyBones.LeftShoulder);
                        Invoke(HumanBodyBones.RightShoulder);
                        Invoke(HumanBodyBones.UpperChest);
                        return true;
                    case HumanBodyBones.UpperChest:
                        Invoke(HumanBodyBones.Neck);
                        Invoke(HumanBodyBones.LeftShoulder);
                        Invoke(HumanBodyBones.RightShoulder);
                        return true;
                }
                return false;
            };

            if (Check(boneIndex2humanoidIndex[boneIndex]))
                return;
            var t = bones[boneIndex].transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var childIndex = BonesIndexOf(t.GetChild(i).gameObject);
                if (childIndex < 0) continue;
                if (Check(boneIndex2humanoidIndex[childIndex]))
                    return;
                ActionAllVirtualBoneChildren(childIndex, action);
            }
        }

        public Type GetBoneType(int boneIndex)
        {
            if (isHuman && (vaw.gameObject == bones[boneIndex] || boneIndex2humanoidIndex[boneIndex] >= 0))
            {
                return typeof(Animator);
            }
            else if (rootMotionBoneIndex >= 0 && rootMotionBoneIndex == boneIndex)
            {
                return typeof(Animator);
            }
            else
            {
                var renderer = bones[boneIndex].GetComponent<Renderer>();
                if (renderer != null)
                    return renderer.GetType();
                else
                    return typeof(Transform);
            }
        }

        private void RendererForceUpdate()
        {
            if (renderers == null) return;
            //It is necessary to avoid situations where only display is not updated.
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                renderer.enabled = !renderer.enabled;
                renderer.enabled = !renderer.enabled;
            }
        }
        #endregion

        #region Undo
        private void UndoRedoPerformed()
        {
            if (isEditError) return;

            UpdateSkeletonShowBoneList();
            ToolsParameterRelatedCurveReset();

            ResetAnimationWindowSynchroSelection();

            SetUpdateSampleAnimation(true);
            SetSynchroIKtargetAll();
            EditorApplication.delayCall += () =>
            {
                SetUpdateSampleAnimation();
                SetSynchroIKtargetAll();
            };

            InternalEditorUtility.RepaintAllViews();
        }
        #endregion
    }
}
