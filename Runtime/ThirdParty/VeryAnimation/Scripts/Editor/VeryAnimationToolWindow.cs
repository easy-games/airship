#if !UNITY_2019_1_OR_NEWER
#define VERYANIMATION_TIMELINE
#endif

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEditorInternal;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

#if VERYANIMATION_TIMELINE
using UnityEngine.Timeline;
#endif

namespace VeryAnimation
{
    [Serializable]
    public class VeryAnimationToolWindow : EditorWindow
    {
        public static VeryAnimationToolWindow instance;

        [MenuItem("Window/Very Animation/Tools")]
        public static void Open()
        {
            GetWindow<VeryAnimationToolWindow>();
        }

        #region Reflection
        public UEditorGUI uEditorGUI { get; private set; }
        #endregion

        #region GUIStyle
        public GUIStyle guiStyleBoldButton { get; private set; }
        public GUIStyle guiStyleIconButton { get; private set; }
        public GUIStyle guiStyleIconActiveButton { get; private set; }

        private void GUIStyleReady()
        {
            if (guiStyleBoldButton == null)
            {
                guiStyleBoldButton = new GUIStyle(GUI.skin.button);
                guiStyleBoldButton.fontStyle = FontStyle.Bold;
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
        }
        private void GUIStyleClear()
        {
            guiStyleBoldButton = null;
            guiStyleIconButton = null;
            guiStyleIconActiveButton = null;
        }
        #endregion

        public enum ToolMode
        {
            ResetPose,
            TemplatePose,
            RemoveSaveSettings,
            ReplaceReference,
            AnimationRigging,
        }
        public ToolMode toolMode;
        public bool toolsHelp = true;
        public PoseTemplate toolPoseTemplate;
        public AnimationClip toolReplaceReference_OldClip;
        public AnimationClip toolReplaceReference_NewClip;

#if VERYANIMATION_ANIMATIONRIGGING
        public enum AnimationRiggingMode
        {
            Humanoid,
            CopyFromOtherSource,
        }
        private static string[] AnimationRiggingModeStrings =
        {
            "Humanoid",
            "Copy From Other Source",
        };
        public AnimationRiggingMode toolAnimationRigging_Mode;
        public bool[] toolAnimationRigging_HumanoidTargets;
        public VeryAnimationSaveSettings toolAnimationRigging_Source;
#endif

        private GameObject activeRootObject;

        private void OnEnable()
        {
            instance = this;

#if !UNITY_2019_1_OR_NEWER
            AssemblyDefinitionChanger.Refresh();
#endif

            uEditorGUI = new UEditorGUI();

            GUIStyleClear();

            titleContent = new GUIContent("VA Tools");
            minSize = new Vector2(320, minSize.y);

            #region Initialize
            {
#if VERYANIMATION_ANIMATIONRIGGING
                toolAnimationRigging_HumanoidTargets = new bool[(int)AnimatorIKCore.IKTarget.Total];
#endif
            }
            #endregion

            OnSelectionChange();

            InternalEditorUtility.RepaintAllViews();
        }
        private void OnDisable()
        {
            GUIStyleClear();
        }
        private void OnDestroy()
        {
            instance = null;
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject != null)
            {
                #region Animator
                {
                    activeRootObject = Selection.activeGameObject;
                    while (activeRootObject.GetComponent<Animator>() == null)
                    {
                        if (activeRootObject.transform.parent == null)
                            break;
                        activeRootObject = activeRootObject.transform.parent.gameObject;
                    }
                    if (activeRootObject.GetComponent<Animator>() == null)
                        activeRootObject = null;
                }
                #endregion
                #region Animation
                if (activeRootObject == null)
                {
                    activeRootObject = Selection.activeGameObject;
                    while (activeRootObject.GetComponent<Animation>() == null)
                    {
                        if (activeRootObject.transform.parent == null)
                            break;
                        activeRootObject = activeRootObject.transform.parent.gameObject;
                    }
                    if (activeRootObject.GetComponent<Animation>() == null)
                        activeRootObject = null;
                }
                #endregion
            }
            Repaint();
        }

        private void OnGUI()
        {
            GUIStyleReady();

            var e = Event.current;

            if (VeryAnimationWindow.instance != null && VeryAnimationWindow.instance.initialized)
            {
                EditorGUILayout.HelpBox(Language.GetText(Language.Help.ToolsWindowVAEditing), MessageType.Warning);
            }
            else
            {
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUI.BeginChangeCheck();
                        var mode = (ToolMode)EditorGUILayout.EnumPopup(toolMode);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(this, "Change Tool Mode");
                            toolMode = mode;
                        }
                    }
                    {
                        if (GUILayout.Button(uEditorGUI.GetHelpIcon(), toolsHelp ? guiStyleIconActiveButton : guiStyleIconButton, GUILayout.Width(19)))
                        {
                            Undo.RecordObject(this, "Change Tool Help");
                            toolsHelp = !toolsHelp;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel++;
                if (toolsHelp)
                {
                    EditorGUILayout.HelpBox(Language.GetText(Language.Help.ToolsWindowHelpResetPose + (int)toolMode), MessageType.Info);
                }
                if (toolMode == ToolMode.ResetPose)
                {
                    #region ResetPose
#if UNITY_2018_2_OR_NEWER
                    var activePrefab = Selection.activeGameObject != null ? PrefabUtility.GetCorrespondingObjectFromSource(Selection.activeGameObject) as GameObject : null;
#else
                    var activePrefab = Selection.activeGameObject != null ? PrefabUtility.GetPrefabParent(Selection.activeGameObject) as GameObject : null;
#endif
                    bool disable = Selection.activeGameObject == null || activePrefab == null;
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ObjectField("Game Object", Selection.activeGameObject, typeof(GameObject), false);
                        EditorGUI.EndDisabledGroup();
                    }
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ObjectField("Prefab", activePrefab, typeof(GameObject), false);
                        EditorGUI.EndDisabledGroup();
                    }
                    if (activeRootObject != null)
                    {
                        var animator = activeRootObject.GetComponent<Animator>();
                        if (animator != null && !animator.hasTransformHierarchy)
                        {
                            EditorGUILayout.HelpBox("Editing on optimized transform hierarchy is not supported.\nPlease deoptimize transform hierarchy.", MessageType.Error);
                            disable = true;
                        }
                    }
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.Space();
                        EditorGUI.BeginDisabledGroup(disable);
                        if (GUILayout.Button("Reset Pose"))
                        {
                            ToolsResetPose(Selection.activeGameObject, activePrefab);
                        }
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.Space();
                        EditorGUILayout.EndHorizontal();
                    }
                    #endregion
                }
                else if (toolMode == ToolMode.TemplatePose)
                {
                    #region TemplatePose
                    bool disable = activeRootObject == null || toolPoseTemplate == null;
                    Animator animator = activeRootObject != null ? activeRootObject.GetComponent<Animator>() : null;
                    Animation animation = activeRootObject != null ? activeRootObject.GetComponent<Animation>() : null;
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        if (animation != null)
                            EditorGUILayout.ObjectField("Animation", animation, typeof(Animation), false);
                        else
                            EditorGUILayout.ObjectField("Animator", animator, typeof(Animator), false);
                        EditorGUI.EndDisabledGroup();
                    }
                    {
                        toolPoseTemplate = EditorGUILayout.ObjectField("Template", toolPoseTemplate, typeof(PoseTemplate), false) as PoseTemplate;
                    }
                    if (activeRootObject != null)
                    {
                        if (animator != null && !animator.hasTransformHierarchy)
                        {
                            EditorGUILayout.HelpBox("Editing on optimized transform hierarchy is not supported.\nPlease deoptimize transform hierarchy.", MessageType.Error);
                            disable = true;
                        }
                        if (animator == null && animation == null)
                        {
                            EditorGUILayout.HelpBox("There is no Animator or Animation in GameObject.", MessageType.Error);
                            disable = true;
                        }
                    }
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.Space();
                        EditorGUI.BeginDisabledGroup(disable);
                        if (GUILayout.Button("Set Pose"))
                        {
                            ToolsTemplatePose();
                        }
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.Space();
                        EditorGUILayout.EndHorizontal();
                    }
                    #endregion
                }
                else if (toolMode == ToolMode.RemoveSaveSettings)
                {
                    #region RemoveSaveSettings
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.Space();
                        if (GUILayout.Button("Remove All"))
                        {
                            if (EditorUtility.DisplayDialog(Language.GetText(Language.Help.DisplayDialogAnimationRemoveSaveSettings),
                                                            Language.GetTooltip(Language.Help.DisplayDialogAnimationRemoveSaveSettings), "ok", "cancel"))
                            {
                                ToolsRemoveSaveSettings();
                            }
                        }
                        EditorGUILayout.Space();
                        EditorGUILayout.EndHorizontal();
                    }
                    #endregion
                }
                else if (toolMode == ToolMode.ReplaceReference)
                {
                    #region ReplaceReference
                    {
                        EditorGUI.BeginChangeCheck();
                        var oldClip = (AnimationClip)EditorGUILayout.ObjectField("Old Animation Clip", toolReplaceReference_OldClip, typeof(AnimationClip), false);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(this, "Change Old Clip");
                            toolReplaceReference_OldClip = oldClip;
                        }
                    }
                    {
                        EditorGUI.BeginChangeCheck();
                        var newClip = (AnimationClip)EditorGUILayout.ObjectField("New Animation Clip", toolReplaceReference_NewClip, typeof(AnimationClip), false);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(this, "Change Old Clip");
                            toolReplaceReference_NewClip = newClip;
                        }
                    }
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.Space();
                        EditorGUI.BeginDisabledGroup(toolReplaceReference_OldClip == null || toolReplaceReference_NewClip == null);
                        if (GUILayout.Button("Replace Reference"))
                        {
                            if (EditorUtility.DisplayDialog(Language.GetText(Language.Help.DisplayDialogAnimationReplaceReference),
                                                            Language.GetTooltip(Language.Help.DisplayDialogAnimationReplaceReference), "ok", "cancel"))
                            {
                                ToolsReplaceReference();
                            }
                        }
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.Space();
                        EditorGUILayout.EndHorizontal();
                    }
                    #endregion
                }
                else if (toolMode == ToolMode.AnimationRigging)
                {
                    #region AnimationRigging
#if VERYANIMATION_ANIMATIONRIGGING
                    Animator animator = activeRootObject != null ? activeRootObject.GetComponent<Animator>() : null;
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ObjectField("Animator", animator, typeof(Animator), false);
                        EditorGUI.EndDisabledGroup();
                    }
                    {
                        EditorGUI.BeginChangeCheck();
                        var mode = (AnimationRiggingMode)GUILayout.Toolbar((int)toolAnimationRigging_Mode, AnimationRiggingModeStrings, EditorStyles.miniButton);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(this, "Change Mode");
                            toolAnimationRigging_Mode = mode;
                        }
                    }
                    if (toolAnimationRigging_Mode == AnimationRiggingMode.Humanoid)
                    {
                        bool disable = true;
                        EditorGUI.BeginDisabledGroup(animator == null || !animator.isHuman);
                        for (int i = 0; i < toolAnimationRigging_HumanoidTargets.Length; i++)
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.Toggle(AnimatorIKCore.IKTargetStrings[i], toolAnimationRigging_HumanoidTargets[i]);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(this, "Change Flag");
                                toolAnimationRigging_HumanoidTargets[i] = flag;
                            }
                            if (toolAnimationRigging_HumanoidTargets[i])
                                disable = false;
                        }
                        {
                            EditorGUI.BeginDisabledGroup(animator == null);
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.Space();
                            if (GUILayout.Button("Delete All"))
                            {
                                ToolsAnimationRiggingDeleteAll();
                            }
                            EditorGUILayout.Space();
                            EditorGUI.BeginDisabledGroup(disable);
                            if (GUILayout.Button("Create"))
                            {
                                ToolsAnimationRiggingHumanoidCrete();
                            }
                            EditorGUI.EndDisabledGroup();
                            EditorGUILayout.Space();
                            EditorGUILayout.EndHorizontal();
                            EditorGUI.EndDisabledGroup();
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    else if (toolAnimationRigging_Mode == AnimationRiggingMode.CopyFromOtherSource)
                    {
                        bool disable = true;
                        {
                            EditorGUI.BeginChangeCheck();
                            var source = EditorGUILayout.ObjectField("Source", toolAnimationRigging_Source, typeof(VeryAnimationSaveSettings), true);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(this, "Change Source");
                                toolAnimationRigging_Source = source as VeryAnimationSaveSettings;
                            }
                            if (toolAnimationRigging_Source != null)
                                disable = false;
                        }
                        {
                            EditorGUI.BeginDisabledGroup(animator == null);
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.Space();
                            if (GUILayout.Button("Delete All"))
                            {
                                ToolsAnimationRiggingDeleteAll();
                            }
                            EditorGUILayout.Space();
                            EditorGUI.BeginDisabledGroup(disable);
                            if (GUILayout.Button("Create"))
                            {
                                ToolsAnimationRiggingSourceCreate();
                            }
                            EditorGUI.EndDisabledGroup();
                            EditorGUILayout.Space();
                            EditorGUILayout.EndHorizontal();
                            EditorGUI.EndDisabledGroup();
                        }
                    }
#endif
                    #endregion
                }
                EditorGUI.indentLevel--;
            }
        }

        private void ToolsResetPose(GameObject activeGameObject, GameObject activePrefab)
        {
            var go = GameObject.Instantiate<GameObject>(activePrefab);
            AnimatorUtility.DeoptimizeTransformHierarchy(go);
            go.hideFlags |= HideFlags.HideAndDontSave;
            {
                Dictionary<string, Transform> srcList = new Dictionary<string, Transform>();
                Dictionary<string, Transform> dstList = new Dictionary<string, Transform>();
                {
                    Action<Dictionary<string, Transform>, Transform, Transform> SaveTransform = null;
                    SaveTransform = (transforms, t, root) =>
                    {
                        var path = AnimationUtility.CalculateTransformPath(t, root);
                        if (!transforms.ContainsKey(path))
                            transforms.Add(path, t);
                        for (int i = 0; i < t.childCount; i++)
                            SaveTransform(transforms, t.GetChild(i), root);
                    };
                    for (int i = 0; i < go.transform.childCount; i++)
                        SaveTransform(srcList, go.transform.GetChild(i), go.transform);
                    for (int i = 0; i < activeGameObject.transform.childCount; i++)
                        SaveTransform(dstList, activeGameObject.transform.GetChild(i), activeGameObject.transform);
                }
                foreach (var pair in dstList)
                {
                    Transform srcT;
                    if (srcList.TryGetValue(pair.Key, out srcT))
                    {
                        var dstT = pair.Value;
                        Undo.RecordObject(dstT, "Reset Pose");
                        dstT.localPosition = srcT.localPosition;
                        dstT.localRotation = srcT.localRotation;
                        dstT.localScale = srcT.localScale;
                        var dstR = dstT.GetComponent<SkinnedMeshRenderer>();
                        var srcR = srcT.GetComponent<SkinnedMeshRenderer>();
                        if (dstR != null && dstR.sharedMesh != null && dstR.sharedMesh.blendShapeCount > 0 &&
                            srcR != null && srcR.sharedMesh != null && srcR.sharedMesh.blendShapeCount > 0)
                        {
                            Undo.RecordObject(dstR, "Reset Pose");
                            for (int i = 0; i < dstR.sharedMesh.blendShapeCount; i++)
                            {
                                var dstName = dstR.sharedMesh.GetBlendShapeName(i);
                                var weight = 0f;
                                for (int j = 0; j < srcR.sharedMesh.blendShapeCount; j++)
                                {
                                    if (dstName == srcR.sharedMesh.GetBlendShapeName(j))
                                    {
                                        weight = srcR.GetBlendShapeWeight(j);
                                        break;
                                    }
                                }
                                dstR.SetBlendShapeWeight(i, weight);
                            }
                        }
                    }
                }
            }
            GameObject.DestroyImmediate(go);
        }
        private void ToolsTemplatePose()
        {
            var transforms = EditorCommon.GetHierarchyTransform(activeRootObject.transform);
            Undo.RecordObjects(transforms.ToArray(), "Template Pose");
            var animator = activeRootObject.GetComponent<Animator>();
            if (animator != null && !animator.isInitialized)
                animator.Rebind();
            var save = new TransformPoseSave.SaveData(activeRootObject.transform);
            string[] paths = new string[transforms.Count];
            for (int i = 0; i < transforms.Count; i++)
                paths[i] = AnimationUtility.CalculateTransformPath(transforms[i], activeRootObject.transform);
            #region Human
            if (animator != null && animator.isHuman)
            {
                var uAnimator = new UAnimator();
                var humanPoseHandler = new HumanPoseHandler(animator.avatar, uAnimator.GetAvatarRoot(animator));
                var humanPose = new HumanPose();
                humanPoseHandler.GetHumanPose(ref humanPose);
                {
                    var musclePropertyName = new MusclePropertyName();
                    if (toolPoseTemplate.haveRootT)
                    {
                        humanPose.bodyPosition = toolPoseTemplate.rootT;
                    }
                    if (toolPoseTemplate.haveRootQ)
                    {
                        humanPose.bodyRotation = toolPoseTemplate.rootQ;
                    }
                    if (toolPoseTemplate.musclePropertyNames != null && toolPoseTemplate.muscleValues != null)
                    {
                        Assert.IsTrue(toolPoseTemplate.musclePropertyNames.Length == toolPoseTemplate.muscleValues.Length);
                        for (int i = 0; i < toolPoseTemplate.musclePropertyNames.Length; i++)
                        {
                            var muscleIndex = EditorCommon.ArrayIndexOf(musclePropertyName.PropertyNames, toolPoseTemplate.musclePropertyNames[i]);
                            if (muscleIndex < 0) continue;
                            humanPose.muscles[muscleIndex] = toolPoseTemplate.muscleValues[i];
                        }
                    }
                    if (toolPoseTemplate.tdofIndices != null && toolPoseTemplate.tdofValues != null)
                    {
                        //not support
                    }
                }
                humanPoseHandler.SetHumanPose(ref humanPose);
            }
            #endregion
            #region Generic
            if (toolPoseTemplate.transformPaths != null && toolPoseTemplate.transformPaths != null)
            {
                Assert.IsTrue(toolPoseTemplate.transformPaths.Length == toolPoseTemplate.transformValues.Length);
                for (int i = 0; i < toolPoseTemplate.transformPaths.Length; i++)
                {
                    var index = EditorCommon.ArrayIndexOf(paths, toolPoseTemplate.transformPaths[i]);
                    if (index < 0) continue;
                    transforms[index].localPosition = toolPoseTemplate.transformValues[i].position;
                    transforms[index].localRotation = toolPoseTemplate.transformValues[i].rotation;
                    transforms[index].localScale = toolPoseTemplate.transformValues[i].scale;
                }
            }
            #endregion
            #region BlendShape
            if (toolPoseTemplate.blendShapePaths != null && toolPoseTemplate.blendShapeValues != null)
            {
                foreach (var renderer in activeRootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount <= 0) continue;
                    Undo.RecordObject(renderer, "Template Pose");
                    var path = AnimationUtility.CalculateTransformPath(renderer.transform, activeRootObject.transform);
                    var index = EditorCommon.ArrayIndexOf(toolPoseTemplate.blendShapePaths, path);
                    if (index < 0) continue;
                    var names = new string[renderer.sharedMesh.blendShapeCount];
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    {
                        names[i] = renderer.sharedMesh.GetBlendShapeName(i);
                    }
                    for (int i = 0; i < toolPoseTemplate.blendShapeValues[index].names.Length; i++)
                    {
                        var nameindex = EditorCommon.ArrayIndexOf(names, toolPoseTemplate.blendShapeValues[index].names[i]);
                        if (nameindex < 0) continue;
                        renderer.SetBlendShapeWeight(nameindex, toolPoseTemplate.blendShapeValues[index].weights[i]);
                    }
                }
            }
            #endregion
            save.LoadLocal(activeRootObject.transform);
        }
        private void ToolsRemoveSaveSettings()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string activeScenePath;
            string[] addScenesPath;
            SaveActiveScenes(out activeScenePath, out addScenesPath);

            var prefabAssets = AssetDatabase.FindAssets("t:Prefab");
            var sceneAssets = AssetDatabase.FindAssets("t:SceneAsset");
            int progressIndex = 0;
            int progressTotal = prefabAssets.Length + sceneAssets.Length;
            try
            {
                #region Prefab
                {
                    for (int i = 0; i < prefabAssets.Length; i++)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(prefabAssets[i]);
                        EditorUtility.DisplayProgressBar("Prefab", path, progressIndex++ / (float)progressTotal);
                        var gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (gameObject == null) continue;
                        var saveSettings = gameObject.GetComponentsInChildren<VeryAnimationSaveSettings>(true);
                        foreach (var cp in saveSettings)
                        {
                            if (cp == null)
                                continue;
                            Undo.DestroyObjectImmediate(cp);
                        }
                    }
                }
                #endregion
                #region Scene
                {
                    for (int i = 0; i < sceneAssets.Length; i++)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(sceneAssets[i]);
                        EditorUtility.DisplayProgressBar("Scene", path, progressIndex++ / (float)progressTotal);
                        EditorSceneManager.OpenScene(path);
                        var scene = EditorSceneManager.GetActiveScene();
                        bool updated = false;
                        foreach (var go in scene.GetRootGameObjects())
                        {
                            var saveSettings = go.GetComponentsInChildren<VeryAnimationSaveSettings>(true);
                            foreach (var cp in saveSettings)
                            {
                                if (cp == null)
                                    continue;
                                Undo.DestroyObjectImmediate(cp);
                                updated = true;
                            }
                        }
                        if (updated)
                        {
                            EditorSceneManager.MarkAllScenesDirty();
                            EditorSceneManager.SaveOpenScenes();
                        }
                    }
                }
                #endregion
            }
            finally
            {
                LoadActiveScenes(activeScenePath, addScenesPath);
                Resources.UnloadUnusedAssets();
                EditorUtility.ClearProgressBar();
                InternalEditorUtility.RepaintAllViews();
            }
        }
        private void ToolsReplaceReference()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string activeScenePath;
            string[] addScenesPath;
            SaveActiveScenes(out activeScenePath, out addScenesPath);

            var controllers = AssetDatabase.FindAssets("t:AnimatorController");
            var ocontrollers = AssetDatabase.FindAssets("t:AnimatorOverrideController");
#if VERYANIMATION_TIMELINE
            var timelines = AssetDatabase.FindAssets("t:TimelineAsset");
#endif
            var prefabAssets = AssetDatabase.FindAssets("t:Prefab");
            var sceneAssets = AssetDatabase.FindAssets("t:SceneAsset");
            int progressIndex = 0;
            int progressTotal = controllers.Length + ocontrollers.Length + prefabAssets.Length + sceneAssets.Length;
#if VERYANIMATION_TIMELINE
            progressTotal += timelines.Length;
#endif
            Action<Animation> ReplaceAnimation = (animation) =>
            {
                if ((animation.hideFlags & HideFlags.NotEditable) != 0) return;
                Undo.RecordObject(animation, "Replace Reference");
                bool changed = false;
                var anims = AnimationUtility.GetAnimationClips(animation.gameObject);
                for (int j = 0; j < anims.Length; j++)
                {
                    if (anims[j] == toolReplaceReference_OldClip)
                    {
                        anims[j] = toolReplaceReference_NewClip;
                        changed = true;
                    }
                }
                if (animation.clip == toolReplaceReference_OldClip)
                {
                    animation.clip = toolReplaceReference_NewClip;
                    changed = true;
                }
                if (changed)
                {
                    Debug.LogFormat("<color=blue>[Very Animation]</color>Replace Animation '{0}'", animation.name);
                    AnimationUtility.SetAnimationClips(animation, anims);
                }
            };

            try
            {
                #region AnimatorController
                for (int i = 0; i < controllers.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(controllers[i]);
                    EditorUtility.DisplayProgressBar("Replace", path, progressIndex++ / (float)progressTotal);
                    var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
                    if (controller == null) continue;
                    if ((controller.hideFlags & HideFlags.NotEditable) != 0) continue;
                    foreach (UnityEditor.Animations.AnimatorControllerLayer layer in controller.layers)
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
                                        Undo.RecordObject(blendTree, "Replace Reference");
                                        var children = blendTree.children;
                                        for (int j = 0; j < children.Length; j++)
                                        {
                                            if (children[j].motion is UnityEditor.Animations.BlendTree)
                                            {
                                                ReplaceBlendTree(children[j].motion as UnityEditor.Animations.BlendTree);
                                            }
                                            else
                                            {
                                                if (children[j].motion == toolReplaceReference_OldClip)
                                                {
                                                    children[j].motion = toolReplaceReference_NewClip;
                                                    Debug.LogFormat("<color=blue>[Very Animation]</color>Replace AnimatorController '{0} - {1}'", controller.name, state.state.name);
                                                }
                                            }
                                        }
                                        blendTree.children = children;
                                    };
                                    ReplaceBlendTree(state.state.motion as UnityEditor.Animations.BlendTree);
                                }
                                else
                                {
                                    if (state.state.motion == toolReplaceReference_OldClip)
                                    {
                                        Undo.RecordObject(state.state, "Duplicate and Replace");
                                        state.state.motion = toolReplaceReference_NewClip;
                                        Debug.LogFormat("<color=blue>[Very Animation]</color>Replace AnimatorController '{0} - {1}'", controller.name, state.state.name);
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
                #region AnimatorOverrideController
                for (int i = 0; i < ocontrollers.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(ocontrollers[i]);
                    EditorUtility.DisplayProgressBar("Replace", path, progressIndex++ / (float)progressTotal);
                    var ocontroller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
                    if (ocontroller == null) continue;
                    if ((ocontroller.hideFlags & HideFlags.NotEditable) != 0) continue;
                    Undo.RecordObject(ocontroller, "Replace Reference");
                    List<KeyValuePair<AnimationClip, AnimationClip>> srcList = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                    ocontroller.GetOverrides(srcList);
                    List<KeyValuePair<AnimationClip, AnimationClip>> dstList = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                    bool changed = false;
                    foreach (var pair in srcList)
                    {
                        if (pair.Key == toolReplaceReference_OldClip || pair.Value == toolReplaceReference_OldClip)
                            changed = true;
                        dstList.Add(new KeyValuePair<AnimationClip, AnimationClip>(pair.Key != toolReplaceReference_OldClip ? pair.Key : toolReplaceReference_NewClip,
                                                                                    pair.Value != toolReplaceReference_OldClip ? pair.Value : toolReplaceReference_NewClip));
                    }
                    if (changed)
                    {
                        Debug.LogFormat("<color=blue>[Very Animation]</color>Replace AnimatorOverrideController '{0}'", ocontroller.name);
                        ocontroller.ApplyOverrides(dstList);
                    }
                }
                #endregion
                #region TimelineAsset
                {
#if VERYANIMATION_TIMELINE
                    Action<TimelineAsset, TrackAsset> ReplaceAnimationTrack = null;
                    ReplaceAnimationTrack = (timeline, trackAsset) =>
                    {
                        var animationTrack = trackAsset as AnimationTrack;
                        if (animationTrack != null)
                        {
                            foreach (var timelineClip in animationTrack.GetClips())
                            {
                                var animationPlayableAsset = timelineClip.asset as AnimationPlayableAsset;
                                if (animationPlayableAsset == null) continue;
                                if (animationPlayableAsset.clip == toolReplaceReference_OldClip)
                                {
                                    Debug.LogFormat("<color=blue>[Very Animation]</color>Replace TimelineAsset '{0}/{1}'", timeline.name, animationTrack.name);
                                    animationPlayableAsset.clip = toolReplaceReference_NewClip;
                                }
                            }
                        }
                        foreach (var cTrackAsset in trackAsset.GetChildTracks())
                        {
                            ReplaceAnimationTrack(timeline, cTrackAsset);
                        }
                    };
                    for (int i = 0; i < timelines.Length; i++)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(timelines[i]);
                        EditorUtility.DisplayProgressBar("Replace", path, progressIndex++ / (float)progressTotal);
                        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
                        if (timeline == null) continue;
                        if ((timeline.hideFlags & HideFlags.NotEditable) != 0) continue;

                        foreach (var trackAsset in timeline.GetOutputTracks())
                        {
                            ReplaceAnimationTrack(timeline, trackAsset);
                        }
                        foreach (var trackAsset in timeline.GetRootTracks())
                        {
                            ReplaceAnimationTrack(timeline, trackAsset);
                        }
                    }
#endif
                }
                #endregion
                #region Prefab
                {
                    for (int i = 0; i < prefabAssets.Length; i++)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(prefabAssets[i]);
                        EditorUtility.DisplayProgressBar("Prefab", path, progressIndex++ / (float)progressTotal);
                        var gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (gameObject == null) continue;
                        var animations = gameObject.GetComponentsInChildren<Animation>(true);
                        foreach (var animation in animations)
                        {
                            if (animation == null)
                                continue;
                            ReplaceAnimation(animation);
                        }
                    }
                }
                #endregion
                #region Scene
                {
                    for (int i = 0; i < sceneAssets.Length; i++)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(sceneAssets[i]);
                        EditorUtility.DisplayProgressBar("Scene", path, progressIndex++ / (float)progressTotal);
                        EditorSceneManager.OpenScene(path);
                        var scene = EditorSceneManager.GetActiveScene();
                        bool updated = false;
                        foreach (var go in scene.GetRootGameObjects())
                        {
                            var animations = go.GetComponentsInChildren<Animation>(true);
                            foreach (var animation in animations)
                            {
                                if (animation == null)
                                    continue;
                                ReplaceAnimation(animation);
                                updated = true;
                            }
                        }
                        if (updated)
                        {
                            EditorSceneManager.MarkAllScenesDirty();
                            EditorSceneManager.SaveOpenScenes();
                        }
                    }
                }
                #endregion
            }
            finally
            {
                LoadActiveScenes(activeScenePath, addScenesPath);
                Resources.UnloadUnusedAssets();
                EditorUtility.ClearProgressBar();
                InternalEditorUtility.RepaintAllViews();
            }
        }
        private void ToolsAnimationRiggingHumanoidCrete()
        {
#if VERYANIMATION_ANIMATIONRIGGING
            AnimationRigging.Create(activeRootObject);

            var newObjects = new List<GameObject>();
            for (int i = 0; i < toolAnimationRigging_HumanoidTargets.Length; i++)
            {
                if (!toolAnimationRigging_HumanoidTargets[i])
                    continue;
                var target = (AnimatorIKCore.IKTarget)i;
                var go = AnimatorIKCore.GetAnimationRiggingConstraint(activeRootObject, target);
                if (go != null)
                    continue;
                go = AnimatorIKCore.AddAnimationRiggingConstraint(activeRootObject, target);
                if (go != null)
                    newObjects.Add(go);
            }
            if (newObjects.Count > 0)
            {
                Selection.objects = newObjects.ToArray();
            }
#endif
        }
        private void ToolsAnimationRiggingSourceCreate()
        {
#if VERYANIMATION_ANIMATIONRIGGING
            if (toolAnimationRigging_Source == null)
                return;

            ToolsAnimationRiggingDeleteAll();
            AnimationRigging.Create(activeRootObject);

            var newObjects = new List<GameObject>();
            for (int i = 0; i < toolAnimationRigging_HumanoidTargets.Length; i++)
            {
                var target = (AnimatorIKCore.IKTarget)i;
                var go = AnimatorIKCore.GetAnimationRiggingConstraint(toolAnimationRigging_Source.gameObject, target);
                if (go == null)
                    continue;
                go = AnimatorIKCore.AddAnimationRiggingConstraint(activeRootObject, target);
                if (go != null)
                    newObjects.Add(go);
            }
            if (newObjects.Count > 0)
            {
                Selection.objects = newObjects.ToArray();
            }
#endif
        }
        private void ToolsAnimationRiggingDeleteAll()
        {
#if VERYANIMATION_ANIMATIONRIGGING
            AnimationRigging.Delete(activeRootObject);
#endif
        }

        private void SaveActiveScenes(out string activeScenePath, out string[] addScenesPath)
        {
            activeScenePath = EditorSceneManager.GetActiveScene().path;
            addScenesPath = new String[EditorSceneManager.sceneCount];
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                addScenesPath[i] = EditorSceneManager.GetSceneAt(i).path;
            }
        }
        private void LoadActiveScenes(string activeScenePath, string[] addScenesPath)
        {
            if (string.IsNullOrEmpty(activeScenePath))
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
            else
                EditorSceneManager.OpenScene(activeScenePath);
            foreach (var path in addScenesPath)
            {
                if (path != activeScenePath)
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
            }
        }
    }
}
