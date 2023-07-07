using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
using System;
using System.Reflection;
using System.Collections.Generic;
#if VERYANIMATION_ANIMATIONRIGGING
using UnityEngine.Animations.Rigging;
using UnityEditor.Animations.Rigging;
#endif

namespace VeryAnimation
{
    public class UAvatarPreview
    {
        public object instance { get; private set; }

        public Action onAvatarChange;

        private FieldInfo fi_m_PreviewDir;
        private FieldInfo fi_m_ZoomFactor;
        private Action<object, int> dg_set_fps;
        private Func<object, AnimationClip> dg_get_m_SourcePreviewMotion;
        private Func<bool> dg_get_IKOnFeet;
        private Func<Animator> dg_get_Animator;
        private Func<bool> dg_get_ShowIKOnFeetButton;
        private Action<bool> dg_set_ShowIKOnFeetButton;
        private Func<GameObject> dg_get_PreviewObject;
        private Func<ModelImporterAnimationType> dg_get_animationClipType;
        private Action dg_DoPreviewSettings;
        private Action<Rect, GUIStyle> dg_DoAvatarPreview;
        private Action dg_OnDestroy;
        private Action<GameObject> dg_SetPreview;
        private PropertyInfo pi_OnAvatarChangeFunc;

        private UTimeControl uTimeControl;

#if UNITY_2019_1_OR_NEWER
        private PlayableGraph m_PlayableGraph;
        private AnimationClipPlayable m_AnimationClipPlayable;
        private Playable m_AnimationMotionXToDeltaPlayable;
        private Playable m_AnimationOffsetPlayable;
        private UAnimationOffsetPlayable m_UAnimationOffsetPlayable;
        private UAnimationMotionXToDeltaPlayable m_UAnimationMotionXToDeltaPlayable;
        private UAnimationClipPlayable m_UAnimationClipPlayable;
        private int loopCount;
#if VERYANIMATION_ANIMATIONRIGGING
        private VeryAnimationRigBuilder m_VARigBuilder;
        private RigBuilder m_RigBuilder;
#endif
#else
        private UnityEditor.Animations.AnimatorController m_Controller;
        private AnimatorStateMachine m_StateMachine;
        private AnimatorState m_State;
        private UAnimatorController uAnimatorController = new UAnimatorController();
        private UAnimatorStateMachine uAnimatorStateMachine = new UAnimatorStateMachine();
        private UAnimatorState uAnimatorState = new UAnimatorState();
#endif

        private GameObject gameObject;
        private GameObject originalGameObject;
        private Animator animator;
        private Animation animation;

        private TransformPoseSave transformPoseSave;
        private BlendShapeWeightSave blendShapeWeightSave;

#if UNITY_2019_3_OR_NEWER
        private GUIStyle guiStylePreButton = "toolbarbutton";
#else
        private GUIStyle guiStylePreButton = "prebutton";
#endif

        private class UAvatarPreviewSelection
        {
            private Func<ModelImporterAnimationType, GameObject> dg_get_GetPreview;

            public UAvatarPreviewSelection(Assembly asmUnityEditor)
            {
                var avatarPreviewSelectionType = asmUnityEditor.GetType("UnityEditor.AvatarPreviewSelection");
                Assert.IsNotNull(dg_get_GetPreview = (Func<ModelImporterAnimationType, GameObject>)Delegate.CreateDelegate(typeof(Func<ModelImporterAnimationType, GameObject>), null, avatarPreviewSelectionType.GetMethod("GetPreview", BindingFlags.Public | BindingFlags.Static)));
            }

            public GameObject GetPreview(ModelImporterAnimationType type)
            {
                return dg_get_GetPreview(type);
            }
        }
        private UAvatarPreviewSelection uAvatarPreviewSelection;

        public const string EditorPrefs2D = "AvatarpreviewCustom2D";
        public const string EditorPrefsApplyRootMotion = "AvatarpreviewCustomApplyRootMotion";
        public const string EditorPrefsARConstraint = "AvatarpreviewCustomARConstraint";

        public UAvatarPreview(AnimationClip clip, GameObject gameObject)
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var avatarPreviewType = asmUnityEditor.GetType("UnityEditor.AvatarPreview");
            Assert.IsNotNull(instance = Activator.CreateInstance(avatarPreviewType, new object[] { null, clip }));
            Assert.IsNotNull(fi_m_PreviewDir = avatarPreviewType.GetField("m_PreviewDir", BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNotNull(fi_m_ZoomFactor = avatarPreviewType.GetField("m_ZoomFactor", BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNotNull(dg_set_fps = EditorCommon.CreateSetFieldDelegate<int>(avatarPreviewType.GetField("fps")));
            Assert.IsNotNull(dg_get_m_SourcePreviewMotion = EditorCommon.CreateGetFieldDelegate<AnimationClip>(avatarPreviewType.GetField("m_SourcePreviewMotion", BindingFlags.NonPublic | BindingFlags.Instance)));
            Assert.IsNotNull(dg_get_IKOnFeet = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, avatarPreviewType.GetProperty("IKOnFeet").GetGetMethod()));
            Assert.IsNotNull(dg_get_Animator = (Func<Animator>)Delegate.CreateDelegate(typeof(Func<Animator>), instance, avatarPreviewType.GetProperty("Animator").GetGetMethod()));
            Assert.IsNotNull(dg_get_ShowIKOnFeetButton = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, avatarPreviewType.GetProperty("ShowIKOnFeetButton").GetGetMethod()));
            Assert.IsNotNull(dg_set_ShowIKOnFeetButton = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), instance, avatarPreviewType.GetProperty("ShowIKOnFeetButton").GetSetMethod()));
            Assert.IsNotNull(dg_get_PreviewObject = (Func<GameObject>)Delegate.CreateDelegate(typeof(Func<GameObject>), instance, avatarPreviewType.GetProperty("PreviewObject").GetGetMethod()));
            Assert.IsNotNull(dg_get_animationClipType = (Func<ModelImporterAnimationType>)Delegate.CreateDelegate(typeof(Func<ModelImporterAnimationType>), instance, avatarPreviewType.GetProperty("animationClipType").GetGetMethod()));
            Assert.IsNotNull(dg_DoPreviewSettings = (Action)Delegate.CreateDelegate(typeof(Action), instance, avatarPreviewType.GetMethod("DoPreviewSettings")));
            Assert.IsNotNull(dg_DoAvatarPreview = (Action<Rect, GUIStyle>)Delegate.CreateDelegate(typeof(Action<Rect, GUIStyle>), instance, avatarPreviewType.GetMethod("DoAvatarPreview")));
#if UNITY_2018_2_OR_NEWER
            Assert.IsNotNull(dg_OnDestroy = (Action)Delegate.CreateDelegate(typeof(Action), instance, avatarPreviewType.GetMethod("OnDisable")));
#else
            Assert.IsNotNull(dg_OnDestroy = (Action)Delegate.CreateDelegate(typeof(Action), instance, avatarPreviewType.GetMethod("OnDestroy")));
#endif
            Assert.IsNotNull(dg_SetPreview = (Action<GameObject>)Delegate.CreateDelegate(typeof(Action<GameObject>), instance, avatarPreviewType.GetMethod("SetPreview", BindingFlags.NonPublic | BindingFlags.Instance)));
            Assert.IsNotNull(pi_OnAvatarChangeFunc = avatarPreviewType.GetProperty("OnAvatarChangeFunc"));

            {
                var fi_timeControl = avatarPreviewType.GetField("timeControl");
                uTimeControl = new UTimeControl(fi_timeControl.GetValue(instance));
                uTimeControl.startTime = 0f;
                uTimeControl.stopTime = clip.length;
                uTimeControl.currentTime = 0f;
            }
            uAvatarPreviewSelection = new UAvatarPreviewSelection(asmUnityEditor);

            pi_OnAvatarChangeFunc.SetValue(instance, Delegate.CreateDelegate(pi_OnAvatarChangeFunc.PropertyType, this, GetType().GetMethod("OnAvatarChangeFunc", BindingFlags.NonPublic | BindingFlags.Instance)), null);
            dg_set_fps(instance, (int)clip.frameRate);
            dg_SetPreview(gameObject);

            SetTime(uTimeControl.currentTime);

            AnimationUtility.onCurveWasModified += OnCurveWasModified;
        }
        ~UAvatarPreview()
        {
            AnimationUtility.onCurveWasModified -= OnCurveWasModified;
#if UNITY_2019_1_OR_NEWER
            Assert.IsFalse(m_PlayableGraph.IsValid());
#else
            Assert.IsNull(m_Controller);
#endif
        }

        public void Release()
        {
            AnimationUtility.onCurveWasModified -= OnCurveWasModified;
            pi_OnAvatarChangeFunc.SetValue(instance, null, null);
            dg_SetPreview(null);
            DestroyController();
            dg_OnDestroy();
        }

        private void OnAvatarChangeFunc()
        {
            if (onAvatarChange != null)
                onAvatarChange.Invoke();
            DestroyController();
            InitController();
        }

        private void InitController()
        {
            gameObject = dg_get_PreviewObject();
            if (gameObject == null) return;
            originalGameObject = uAvatarPreviewSelection.GetPreview(dg_get_animationClipType());

            animator = dg_get_Animator();
            animation = gameObject.GetComponent<Animation>();
            if (originalGameObject != null)
            {
                transformPoseSave = new TransformPoseSave(originalGameObject);
                transformPoseSave.ChangeStartTransform();
                transformPoseSave.ChangeTransformReference(gameObject);
            }
            else
            {
                transformPoseSave = new TransformPoseSave(gameObject);
            }
            blendShapeWeightSave = new BlendShapeWeightSave(gameObject);

            var clip = dg_get_m_SourcePreviewMotion(instance);
            if (clip.legacy || instance == null || !((UnityEngine.Object)animator != (UnityEngine.Object)null))
            {
                if (animation != null)
                    animation.enabled = false;  //If vaw.animation.enabled, it is not updated during execution. bug?
            }
            else
            {
                animator.fireEvents = false;
                animator.applyRootMotion = EditorPrefs.GetBool(EditorPrefsApplyRootMotion, false);

#if UNITY_2019_1_OR_NEWER
                animator.enabled = true;
                UnityEditor.Animations.AnimatorController.SetAnimatorController(animator, null);

                m_PlayableGraph = PlayableGraph.Create("Avatar Preview PlayableGraph");
                m_PlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

                m_AnimationClipPlayable = AnimationClipPlayable.Create(m_PlayableGraph, clip);
                m_AnimationClipPlayable.SetApplyPlayableIK(false);
                m_AnimationClipPlayable.SetApplyFootIK(isIKOnFeet);
                if (m_UAnimationClipPlayable == null)
                    m_UAnimationClipPlayable = new UAnimationClipPlayable();
                m_UAnimationClipPlayable.SetRemoveStartOffset(m_AnimationClipPlayable, true);
                m_UAnimationClipPlayable.SetOverrideLoopTime(m_AnimationClipPlayable, true);
                m_UAnimationClipPlayable.SetLoopTime(m_AnimationClipPlayable, true);

                Playable rootPlayable = m_AnimationClipPlayable;

#if VERYANIMATION_ANIMATIONRIGGING
                m_VARigBuilder = gameObject.GetComponent<VeryAnimationRigBuilder>();
                if (m_VARigBuilder != null)
                {
                    RigBuilder.DestroyImmediate(m_VARigBuilder);
                    m_VARigBuilder = null;
                }
                m_RigBuilder = gameObject.GetComponent<RigBuilder>();
                if (m_RigBuilder != null)
                {
                    RigBuilder.DestroyImmediate(m_RigBuilder);
                    m_RigBuilder = null;
                }
                if (originalGameObject != null)
                {
                    var rigBuilder = originalGameObject.GetComponent<RigBuilder>();
                    if (rigBuilder != null && rigBuilder.isActiveAndEnabled && rigBuilder.enabled)
                    {
                        var layers = new List<GameObject>();
                        foreach (var layer in rigBuilder.layers)
                        {
                            if (layer.rig == null || !layer.active)
                                continue;
                            var originalRig = layer.rig.GetComponent<Rig>();
                            if (originalRig == null)
                                continue;

                            Func<Transform, Transform> GetPreviewTransform = (t) =>
                            {
                                if (t == null) return null;
                                var path = AnimationUtility.CalculateTransformPath(t, originalGameObject.transform);
                                return gameObject.transform.Find(path);
                            };
                            var previewT = GetPreviewTransform(originalRig.transform);
                            if (previewT == null)
                                continue;
                            var newRig = GameObject.Instantiate<GameObject>(originalRig.gameObject, previewT.parent);
                            newRig.name = previewT.name;
                            GameObject.DestroyImmediate(previewT.gameObject);
                            AnimationRigging.ReplaceConstraintTransformReference(gameObject, newRig.GetComponent<Rig>(), originalGameObject, originalRig);
                            layers.Add(newRig);
                        }
                        if (layers.Count > 0)
                        {
                            m_VARigBuilder = gameObject.AddComponent<VeryAnimationRigBuilder>();
                            m_RigBuilder = gameObject.GetComponent<RigBuilder>();
                            foreach (var layer in layers)
                            {
                                var rig = layer.GetComponent<Rig>();
                                #region RemoveEffector
                                {
                                    var effectors = rig.effectors as List<RigEffectorData>;
                                    effectors.Clear();
                                }
                                #endregion

#if UNITY_2020_1_OR_NEWER
                                var rigLayer = new RigLayer(rig);   //version 0.3.2
#else
                                var rigLayer = new RigBuilder.RigLayer(rig);    //version 0.2.5
#endif
                                m_RigBuilder.layers.Add(rigLayer);
                            }
                        }
                    }
                    if (m_VARigBuilder != null && m_RigBuilder != null)
                    {
                        m_VARigBuilder.enabled = m_RigBuilder.enabled = EditorPrefs.GetBool(EditorPrefsARConstraint);
                        if (m_RigBuilder.enabled)
                        {
                            m_VARigBuilder.StartPreview();
                            m_RigBuilder.StartPreview();
                            rootPlayable = m_VARigBuilder.BuildPreviewGraph(m_PlayableGraph, rootPlayable);
                            rootPlayable = m_RigBuilder.BuildPreviewGraph(m_PlayableGraph, rootPlayable);
                        }
                    }
                }
#endif

                if (animator.applyRootMotion)
                {
                    bool hasRootMotionBone = false;
                    if (animator.isHuman)
                        hasRootMotionBone = true;
                    else
                    {
                        UAvatar uAvatar = new UAvatar();
                        var genericRootMotionBonePath = uAvatar.GetGenericRootMotionBonePath(animator.avatar);
                        hasRootMotionBone = !string.IsNullOrEmpty(genericRootMotionBonePath);
                    }
                    if (hasRootMotionBone)
                    {
                        if (m_UAnimationOffsetPlayable == null)
                            m_UAnimationOffsetPlayable = new UAnimationOffsetPlayable();
                        m_AnimationOffsetPlayable = m_UAnimationOffsetPlayable.Create(m_PlayableGraph, transformPoseSave.startLocalPosition, transformPoseSave.startLocalRotation, 1);
                        m_AnimationOffsetPlayable.SetInputWeight(0, 1f);
                        m_PlayableGraph.Connect(rootPlayable, 0, m_AnimationOffsetPlayable, 0);
                        rootPlayable = m_AnimationOffsetPlayable;
                    }
                    {
                        if (m_UAnimationMotionXToDeltaPlayable == null)
                            m_UAnimationMotionXToDeltaPlayable = new UAnimationMotionXToDeltaPlayable();
                        m_AnimationMotionXToDeltaPlayable = m_UAnimationMotionXToDeltaPlayable.Create(m_PlayableGraph);
                        m_UAnimationMotionXToDeltaPlayable.SetAbsoluteMotion(m_AnimationMotionXToDeltaPlayable, true);
                        m_AnimationMotionXToDeltaPlayable.SetInputWeight(0, 1f);
                        m_PlayableGraph.Connect(rootPlayable, 0, m_AnimationMotionXToDeltaPlayable, 0);
                        rootPlayable = m_AnimationMotionXToDeltaPlayable;
                    }
                }

                var playableOutput = AnimationPlayableOutput.Create(m_PlayableGraph, "Animation", animator);
                playableOutput.SetSourcePlayable(rootPlayable);
#else
                if (m_Controller == null)
                {
                    m_Controller = new UnityEditor.Animations.AnimatorController();
                    m_Controller.name = "Avatar Preview AnimatorController";
                    m_Controller.hideFlags |= HideFlags.HideAndDontSave;
                    uAnimatorController.SetPushUndo(m_Controller, false);
                    m_Controller.AddLayer("preview");
                    m_StateMachine = m_Controller.layers[0].stateMachine;
                    uAnimatorStateMachine.SetPushUndo(m_StateMachine, false);
                    m_StateMachine.hideFlags |= HideFlags.HideAndDontSave;
                }
                if (m_State == null)
                {
                    m_State = m_StateMachine.AddState("preview");
                    uAnimatorState.SetPushUndo(m_State, false);
                    m_State.motion = (Motion)clip;
                    m_State.iKOnFeet = isIKOnFeet;
                    m_State.hideFlags |= HideFlags.HideAndDontSave;
                }
                UnityEditor.Animations.AnimatorController.SetAnimatorController(animator, m_Controller);
#endif
            }

            dg_set_ShowIKOnFeetButton(animator != null && animator.isHuman && clip.isHumanMotion);

            ForceUpdate();
        }
        private void DestroyController()
        {
#if UNITY_2019_1_OR_NEWER
#if VERYANIMATION_ANIMATIONRIGGING
            if (m_RigBuilder != null)
            {
                m_RigBuilder.StopPreview();
                m_RigBuilder = null;
            }
            if (m_VARigBuilder != null)
            {
                m_VARigBuilder.StopPreview();
                m_VARigBuilder = null;
            }
#endif
            if (m_PlayableGraph.IsValid())
                m_PlayableGraph.Destroy();
#else
            if (instance != null && animator != null)
                UnityEditor.Animations.AnimatorController.SetAnimatorController(animator, null);
            if (m_Controller != null)
                UnityEditor.Animations.AnimatorController.DestroyImmediate(m_Controller);
            if (m_StateMachine != null)
                AnimatorStateMachine.DestroyImmediate(m_StateMachine);
            if (m_State != null)
                AnimatorState.DestroyImmediate(m_State);
            m_Controller = null;
            m_StateMachine = null;
            m_State = null;
#endif
        }

        private void OnCurveWasModified(AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType deleted)
        {
            if (instance == null) return;
            if (clip != dg_get_m_SourcePreviewMotion(instance)) return;

            Reset();
        }

        public void OnPreviewSettings()
        {
            var clip = dg_get_m_SourcePreviewMotion(instance);

            if (!clip.legacy && animator != null)
            {
                var flag = EditorPrefs.GetBool(EditorPrefsApplyRootMotion, false);
                EditorGUI.BeginChangeCheck();
                flag = GUILayout.Toggle(flag, new GUIContent("Root Motion", "Apply Root Motion"), guiStylePreButton);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool(EditorPrefsApplyRootMotion, flag);
                    Reset();
                    OnAvatarChangeFunc();
                }
            }
#if VERYANIMATION_ANIMATIONRIGGING
            if (m_RigBuilder != null)
            {
                var flag = EditorPrefs.GetBool(EditorPrefsARConstraint, false);
                EditorGUI.BeginChangeCheck();
                flag = GUILayout.Toggle(flag, new GUIContent("Animation Rigging", "Animation Rigging Constraint"), guiStylePreButton);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool(EditorPrefsARConstraint, flag);
                    Reset();
                    OnAvatarChangeFunc();
                }
            }
#endif
            GUILayout.Space(20);
            dg_DoPreviewSettings();
        }

        public void OnGUI(Rect r, GUIStyle background)
        {
            if (Event.current.type == EventType.Repaint)
            {
                #region TimeControl
                {
                    var beforePlaying = uTimeControl.playing;
#if UNITY_2019_1_OR_NEWER
                    var beforeCurrentTime = uTimeControl.currentTime;
                    uTimeControl.Update();
                    if (uTimeControl.playing && uTimeControl.currentTime < beforeCurrentTime)
                    {
                        loopCount++;
                    }
#else
                    uTimeControl.Update();
#endif
                    if (beforePlaying != uTimeControl.playing)
                    {
                        uTimeControl.playing = beforePlaying;
                    }
                }
                #endregion

                {
                    var clip = dg_get_m_SourcePreviewMotion(instance);
                    uTimeControl.loop = true;
                    uTimeControl.startTime = 0f;
                    uTimeControl.stopTime = clip.length;
                    dg_set_fps(instance, (int)clip.frameRate);
                    if (!clip.legacy && animator != null)
                    {
                        dg_set_ShowIKOnFeetButton(animator.isHuman && clip.isHumanMotion);

#if UNITY_2019_1_OR_NEWER
                        if (m_PlayableGraph.IsValid())
                        {
                            if (m_AnimationOffsetPlayable.IsValid())
                            {
                                m_UAnimationOffsetPlayable.SetPosition(m_AnimationOffsetPlayable, transformPoseSave.startPosition);
                                m_UAnimationOffsetPlayable.SetRotation(m_AnimationOffsetPlayable, transformPoseSave.startRotation);
                            }
                            m_AnimationClipPlayable.SetApplyFootIK(isIKOnFeet);
                            m_AnimationClipPlayable.SetTime(loopCount * clip.length + uTimeControl.currentTime);
#if VERYANIMATION_ANIMATIONRIGGING
                            if (m_RigBuilder != null && m_RigBuilder.enabled && m_RigBuilder.layers.Count > 0)
                            {
                                m_RigBuilder.UpdatePreviewGraph(m_PlayableGraph);
                            }
#endif
                            m_PlayableGraph.Evaluate();
                        }
#else
                        if (animator.runtimeAnimatorController != null)
                        {
                            AnimationClipSettings animationClipSettings = AnimationUtility.GetAnimationClipSettings(clip);
                            if (m_State != null)
                                m_State.iKOnFeet = isIKOnFeet;

                            var normalizedTime = animationClipSettings.stopTime - animationClipSettings.startTime == 0.0 ? 0.0f : (float)((uTimeControl.currentTime - animationClipSettings.startTime) / (animationClipSettings.stopTime - animationClipSettings.startTime));
                            animator.Play(0, 0, normalizedTime);
                            animator.Update(uTimeControl.deltaTime);
                        }
#endif
                    }
                    else if (animation != null)
                    {
                        dg_set_ShowIKOnFeetButton(false);
                        clip.SampleAnimation(gameObject, uTimeControl.currentTime);
                    }
                }
            }

            dg_DoAvatarPreview(r, background);

            if (animator.applyRootMotion && transformPoseSave != null)
            {
                var rect = r;
                rect.yMin = rect.yMax - 40f;
                rect.yMax -= 15f;
                var invRot = Quaternion.Inverse(transformPoseSave.originalRotation);
                var pos = invRot * (gameObject.transform.position - transformPoseSave.originalPosition);
                var rot = (invRot * gameObject.transform.rotation).eulerAngles;
                EditorGUI.DropShadowLabel(rect, string.Format("Root Motion Position {0}\nRoot Motion Rotation {1}", pos, rot));
            }
        }

        public void SetTime(float time)
        {
            uTimeControl.currentTime = 0f;
            uTimeControl.nextCurrentTime = time;

            Reset();
        }
        public float GetTime()
        {
            return uTimeControl.currentTime;
        }

        public void Reset()
        {
            {
                var time = uTimeControl.currentTime + (uTimeControl.GetDeltaTimeSet() ? uTimeControl.deltaTime : 0f);
                uTimeControl.currentTime = 0f;
                uTimeControl.nextCurrentTime = time;
            }

            if (transformPoseSave != null)
            {
                transformPoseSave.ResetOriginalTransform();
            }
            if (blendShapeWeightSave != null)
            {
                blendShapeWeightSave.ResetOriginalWeight();
            }

#if UNITY_2019_1_OR_NEWER
            loopCount = 0;
#endif
        }

        public void ForceUpdate()
        {
            var clip = dg_get_m_SourcePreviewMotion(instance);
            if (!clip.legacy && animator != null)
            {
#if UNITY_2019_1_OR_NEWER
                if (m_PlayableGraph.IsValid())
                {
                    m_AnimationClipPlayable.SetTime(uTimeControl.currentTime);
                    m_PlayableGraph.Evaluate();
                }
#else
                if (animator.runtimeAnimatorController != null)
                {
                    animator.Update(0f);
                }
#endif
            }
            else if (animation != null)
            {
                clip.SampleAnimation(gameObject, uTimeControl.currentTime);
            }
        }

        public Vector2 PreviewDir
        {
            get
            {
                return (Vector2)fi_m_PreviewDir.GetValue(instance);
            }
            set
            {
                fi_m_PreviewDir.SetValue(instance, value);
            }
        }

        public float ZoomFactor
        {
            get
            {
                return (float)fi_m_ZoomFactor.GetValue(instance);
            }
            set
            {
                fi_m_ZoomFactor.SetValue(instance, value);
            }
        }

        public bool playing
        {
            get
            {
                return uTimeControl.playing;
            }
            set
            {
                uTimeControl.playing = value;
            }
        }

        private bool isIKOnFeet { get { return dg_get_ShowIKOnFeetButton() && dg_get_IKOnFeet(); } }
    }
}
