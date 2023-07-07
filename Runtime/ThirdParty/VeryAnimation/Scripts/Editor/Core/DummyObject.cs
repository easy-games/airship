#if !UNITY_2019_1_OR_NEWER
#define VERYANIMATION_TIMELINE
#endif

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;

namespace VeryAnimation
{
    public class DummyObject
    {
        private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
        private VeryAnimation va { get { return VeryAnimation.instance; } }

        public GameObject gameObject { get; private set; }
        public Transform gameObjectTransform { get; private set; }
        public GameObject sourceObject { get; private set; }
        public Animator animator { get; private set; }
        public Animation animation { get; private set; }
        public GameObject[] bones { get; private set; }
        public Dictionary<GameObject, int> boneDic { get; private set; }
        public GameObject[] humanoidBones { get; private set; }
        public Transform humanoidHipsTransform { get; private set; }
        public HumanPoseHandler humanPoseHandler { get; private set; }
        public VeryAnimationEditAnimator vaEdit { get; private set; }
        public Dictionary<Renderer, Renderer> rendererDictionary { get; private set; }

        private List<Material> createdMaterials;
        private MaterialPropertyBlock materialPropertyBlock;
        private UnityEditor.Animations.AnimatorController tmpAnimatorController;
        private AnimatorControllerLayer tmpAnimatorControllerLayer;
        private AnimatorState tmpAnimationState;

        private TransformPoseSave.SaveData m_SetTransformRootSave;
        private Vector3 m_OffsetPosition = Vector3.zero;
        private Quaternion m_OffsetRotation = Quaternion.identity;
        private bool m_RemoveStartOffset;
        private bool m_ApplyIK;

#if UNITY_2018_3_OR_NEWER
        private PlayableGraph m_PlayableGraph;
        private AnimationClipPlayable m_AnimationClipPlayable;
        private Playable m_AnimationMotionXToDeltaPlayable;
        private Playable m_AnimationOffsetPlayable;
        private UAnimationOffsetPlayable m_UAnimationOffsetPlayable;
        private UAnimationMotionXToDeltaPlayable m_UAnimationMotionXToDeltaPlayable;
        private UAnimationClipPlayable m_UAnimationClipPlayable;
        private bool m_AnimatesRootTransform;
        private bool m_RequiresOffsetPlayable;
        private bool m_RequiresMotionXPlayable;
        private bool m_UsesAbsoluteMotion;
#endif

        private static readonly int ShaderID_Color = Shader.PropertyToID("_Color");
        private static readonly int ShaderID_FaceColor = Shader.PropertyToID("_FaceColor");

        ~DummyObject()
        {
            if (vaEdit != null || gameObject != null)
            {
                EditorApplication.delayCall += () =>
                {
                    if (vaEdit != null)
                        Component.DestroyImmediate(vaEdit);
                    if (gameObject != null)
                        GameObject.DestroyImmediate(gameObject);
                };
            }
        }

        public void Initialize(GameObject sourceObject)
        {
            Release();

            this.sourceObject = sourceObject;
            gameObject = sourceObject != null ? GameObject.Instantiate<GameObject>(sourceObject) : new GameObject();
            gameObject.hideFlags |= HideFlags.HideAndDontSave | HideFlags.HideInInspector;
            gameObject.name = sourceObject.name;
            EditorCommon.DisableOtherBehaviors(gameObject);
            gameObjectTransform = gameObject.transform;

            animator = gameObject.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = true;
                animator.fireEvents = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                UnityEditor.Animations.AnimatorController.SetAnimatorController(animator, null);
            }
            animation = gameObject.GetComponent<Animation>();
            if (animation != null)
            {
                animation.enabled = true;
            }

            UpdateBones();

            #region rendererDictionary
            {
                rendererDictionary = new Dictionary<Renderer, Renderer>();
                var sourceRenderers = sourceObject.GetComponentsInChildren<Renderer>(true);
                var objectRenderers = gameObject.GetComponentsInChildren<Renderer>(true);
                foreach (var sr in sourceRenderers)
                {
                    if (sr == null)
                        continue;
                    var spath = AnimationUtility.CalculateTransformPath(sr.transform, sourceObject.transform);
                    var index = ArrayUtility.FindIndex(objectRenderers, (x) => AnimationUtility.CalculateTransformPath(x.transform, gameObject.transform) == spath);
                    Assert.IsTrue(index >= 0);
                    if (index >= 0 && !rendererDictionary.ContainsKey(objectRenderers[index]))
                        rendererDictionary.Add(objectRenderers[index], sr);
                }
            }
            #endregion

            UpdateState();

            SetTransformOutside();
        }
        public void Release()
        {
#if UNITY_2018_3_OR_NEWER
            if (m_PlayableGraph.IsValid())
                m_PlayableGraph.Destroy();
#endif
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                UnityEditor.Animations.AnimatorController.SetAnimatorController(animator, null);
            }
            if (tmpAnimatorController != null)
            {
                {
                    var layerCount = tmpAnimatorController.layers.Length;
                    for (int i = 0; i < layerCount; i++)
                        tmpAnimatorController.RemoveLayer(0);
                }
                UnityEditor.Animations.AnimatorController.DestroyImmediate(tmpAnimatorController);
                tmpAnimatorController = null;
            }
            tmpAnimatorControllerLayer = null;
            tmpAnimationState = null;

            RevertTransparent();

            animator = null;
            animation = null;
            if (vaEdit != null)
            {
                Component.DestroyImmediate(vaEdit);
                vaEdit = null;
            }
            if (gameObject != null)
            {
                GameObject.DestroyImmediate(gameObject);
                gameObject = null;
            }
            sourceObject = null;
        }

        public void SetTransformOrigin()
        {
            gameObjectTransform.SetParent(null);
            ResetOriginal();

            gameObjectTransform.localPosition = Vector3.zero;
            gameObjectTransform.localRotation = Quaternion.identity;
            gameObjectTransform.localScale = Vector3.one;
            m_SetTransformRootSave = new TransformPoseSave.SaveData(gameObjectTransform);

            SetOffset(Vector3.zero, Quaternion.identity);
        }
        public void SetTransformStart()
        {
            gameObjectTransform.SetParent(sourceObject.transform.parent);
            ResetOriginal();

            gameObjectTransform.SetPositionAndRotation(va.transformPoseSave.startPosition, va.transformPoseSave.startRotation);
            gameObjectTransform.localScale = va.transformPoseSave.startLocalScale;
            m_SetTransformRootSave = new TransformPoseSave.SaveData(gameObjectTransform);

            SetOffset(va.transformPoseSave.startLocalPosition, va.transformPoseSave.startLocalRotation);
        }
        public void SetTransformOutside()
        {
            gameObjectTransform.SetParent(null);
            //ResetOriginal();  Waste

            gameObjectTransform.SetPositionAndRotation(new Vector3(10000f, 10000f, 10000f), Quaternion.identity);
            gameObjectTransform.localScale = Vector3.one;
            m_SetTransformRootSave = new TransformPoseSave.SaveData(gameObjectTransform);

            SetOffset(Vector3.zero, Quaternion.identity);
        }
        public void ResetTranformRoot()
        {
            if (m_SetTransformRootSave != null)
                m_SetTransformRootSave.LoadLocal(gameObjectTransform);
        }
        private void ResetOriginal()
        {
            for (int i = 0; i < va.bones.Length; i++)
            {
                if (va.bones[i] == null)
                    continue;
                var save = va.transformPoseSave.GetOriginalTransform(va.bones[i].transform);
                if (save == null) continue;
                save.LoadLocal(bones[i].transform);
            }
            foreach (var pair in rendererDictionary)
            {
                var renderer = pair.Key as SkinnedMeshRenderer;
                if (renderer == null || renderer.sharedMesh == null)
                    continue;
                var sourceRenderer = pair.Value as SkinnedMeshRenderer;
                if (sourceRenderer == null || sourceRenderer.sharedMesh == null)
                    continue;
                for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    var name = renderer.sharedMesh.GetBlendShapeName(i);
                    if (!va.blendShapeWeightSave.IsHaveOriginalWeight(sourceRenderer, name))
                        continue;
                    var weight = va.blendShapeWeightSave.GetOriginalWeight(sourceRenderer, name);
                    if (renderer.GetBlendShapeWeight(i) != weight)
                    {
                        renderer.SetBlendShapeWeight(i, weight);
                    }
                }
            }
        }

        public void ChangeTransparent()
        {
            RevertTransparent();

            createdMaterials = new List<Material>();
            foreach (var pair in rendererDictionary)
            {
                bool changeShader = pair.Key is SkinnedMeshRenderer;
                if (!changeShader && pair.Key is MeshRenderer)
                {
                    changeShader = true;
                    foreach (var comp in pair.Key.GetComponents<Component>())
                    {
                        if (comp.GetType().Name.StartsWith("TextMesh"))
                        {
                            changeShader = false;
                            break;
                        }
                    }
                }
                if (changeShader)
                {
                    Shader shader;
#if UNITY_2018_1_OR_NEWER
                    if (GraphicsSettings.renderPipelineAsset != null)
                    {
                        shader = Shader.Find("Very Animation/OnionSkin-1pass");
                    }
                    else
#endif
                    {
                        shader = Shader.Find("Very Animation/OnionSkin-2pass");
                    }
                    var materials = new Material[pair.Key.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var keyMat = pair.Key.sharedMaterials[i];
                        if (keyMat == null) continue;
                        Material mat;
                        {
                            mat = new Material(shader);
                            mat.hideFlags |= HideFlags.HideAndDontSave;
                            #region SetTexture
                            {
                                Action<string> SetTexture = (name) =>
                                {
                                    if (mat.mainTexture == null && keyMat.HasProperty(name))
                                        mat.mainTexture = keyMat.GetTexture(name);
                                };
                                SetTexture("_MainTex");
                                SetTexture("_BaseColorMap");    //HDRP
                                SetTexture("_BaseMap");         //LWRP
#if UNITY_2018_2_OR_NEWER
                                if (mat.mainTexture == null)
                                {
                                    foreach (var name in keyMat.GetTexturePropertyNames())
                                    {
                                        SetTexture(name);
                                    }
                                }
#endif
                            }
                            #endregion
                            createdMaterials.Add(mat);
                        }
                        materials[i] = mat;
                    }
                    pair.Key.sharedMaterials = materials;
                }
                else
                {
                    var materials = new Material[pair.Key.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var keyMat = pair.Key.sharedMaterials[i];
                        if (keyMat == null) continue;
                        Material mat;
                        {
                            mat = Material.Instantiate<Material>(keyMat);
                            mat.hideFlags |= HideFlags.HideAndDontSave;
                            createdMaterials.Add(mat);
                        }
                        materials[i] = mat;
                    }
                    pair.Key.sharedMaterials = materials;
                }
            }
        }
        public void RevertTransparent()
        {
            if (createdMaterials != null)
            {
                foreach (var mat in createdMaterials)
                {
                    if (mat != null)
                        Material.DestroyImmediate(mat);
                }
                createdMaterials = null;

                foreach (var pair in rendererDictionary)
                {
                    if (pair.Key == null || pair.Value == null)
                        continue;
                    if (pair.Key.sharedMaterials != pair.Value.sharedMaterials)
                        pair.Key.sharedMaterials = pair.Value.sharedMaterials;
                }
            }
        }
        public void SetTransparentRenderQueue(int renderQueue)
        {
            if (createdMaterials == null)
                return;
            foreach (var mat in createdMaterials)
            {
                mat.renderQueue = renderQueue;
            }
        }

        public void SetColor(Color color)
        {
            materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetColor(ShaderID_Color, color);
            materialPropertyBlock.SetColor(ShaderID_FaceColor, color);
            foreach (var pair in rendererDictionary)
            {
                var renderer = pair.Key;
                if (renderer == null)
                    continue;
                renderer.SetPropertyBlock(materialPropertyBlock);
            }
        }
        public void ResetColor()
        {
            materialPropertyBlock = null;
            foreach (var pair in rendererDictionary)
            {
                var renderer = pair.Key;
                if (renderer == null)
                    continue;
                renderer.SetPropertyBlock(materialPropertyBlock);
            }
        }

        public void AddEditComponent()
        {
            Assert.IsNull(vaEdit);
            vaEdit = gameObject.AddComponent<VeryAnimationEditAnimator>();
            vaEdit.hideFlags |= HideFlags.HideAndDontSave;
        }
        public void RemoveEditComponent()
        {
            if (vaEdit != null)
            {
                Component.DestroyImmediate(vaEdit);
                vaEdit = null;
            }
        }

        public void SetRendererEnabled(bool enable)
        {
            foreach (var pair in rendererDictionary)
            {
                if (pair.Key == null || pair.Value == null) continue;
                pair.Key.enabled = enable;
            }
        }
        public void RendererForceUpdate()
        {
            //It is necessary to avoid situations where only display is not updated.
            foreach (var pair in rendererDictionary)
            {
                if (pair.Key == null || pair.Value == null) continue;
                pair.Key.enabled = !pair.Key.enabled;
                pair.Key.enabled = !pair.Key.enabled;
            }
        }

        private void SetOffset(Vector3 position, Quaternion rotation)
        {
            if (m_OffsetPosition == position && m_OffsetRotation == rotation)
                return;
            m_OffsetPosition = position;
            m_OffsetRotation = rotation;
#if UNITY_2018_3_OR_NEWER
            if (m_AnimationOffsetPlayable.IsValid())
            {
                m_UAnimationOffsetPlayable.SetPosition(m_AnimationOffsetPlayable, m_OffsetPosition);
                m_UAnimationOffsetPlayable.SetRotation(m_AnimationOffsetPlayable, m_OffsetRotation);
            }
#endif
        }
        public void SetRemoveStartOffset(bool enable)
        {
            if (m_RemoveStartOffset == enable)
                return;
            m_RemoveStartOffset = enable;
#if UNITY_2018_3_OR_NEWER
            if (m_AnimationClipPlayable.IsValid())
            {
                m_UAnimationClipPlayable.SetRemoveStartOffset(m_AnimationClipPlayable, m_RemoveStartOffset);
            }
#endif
        }
        public void SetApplyIK(bool enable)
        {
            if (m_ApplyIK == enable)
                return;
            m_ApplyIK = enable;
#if UNITY_2018_3_OR_NEWER
            if (m_AnimationClipPlayable.IsValid())
            {
                m_AnimationClipPlayable.SetApplyPlayableIK(m_ApplyIK);
            }
#endif
        }

        public void UpdateState()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null || va.bones[i] == null) continue;
                if (bones[i].activeSelf != va.bones[i].activeSelf)
                    bones[i].SetActive(va.bones[i].activeSelf);
            }
            foreach (var pair in rendererDictionary)
            {
                if (pair.Key == null || pair.Value == null) continue;
                if (pair.Key.enabled != pair.Value.enabled)
                    pair.Key.enabled = pair.Value.enabled;
            }
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        public void SampleAnimation(AnimationClip clip, float time)
        {
#if UNITY_2018_3_OR_NEWER
            ResetTranformRoot();

            va.UpdateSyncEditorCurveClip();

            if (animator != null)
            {
                PlayableGraphReady(clip);

#if UNITY_2019_1_OR_NEWER
                m_AnimationClipPlayable.SetTime(time);
#else
                //Loop cannot be disabled because there is no SetOverrideLoopTime
                var subTime = time;
                if (subTime > clip.length)
                    subTime = clip.length - 0.0000001f;
                m_AnimationClipPlayable.SetTime(subTime);
#endif
                m_PlayableGraph.Evaluate();
            }
            else if (animation != null)
            {
                SampleAnimationLegacy(clip, time);
            }
#else
            SampleAnimationLegacy(clip, time);
#endif
        }
        public void SampleAnimationLegacy(AnimationClip clip, float time)
        {
            ResetTranformRoot();

#if UNITY_2018_3_OR_NEWER
            PlayableGraphReady(clip);
            #region Offset
            if (m_AnimationOffsetPlayable.IsValid())
            {
                gameObjectTransform.localPosition = Vector3.zero;
                gameObjectTransform.localRotation = Quaternion.identity;
            }
            #endregion
#endif

            va.UpdateSyncEditorCurveClip();

            if (animator != null)
            {
                #region Initialize
                if (tmpAnimatorController == null)
                {
                    tmpAnimatorController = new UnityEditor.Animations.AnimatorController();
                    tmpAnimatorController.name = "Very Animation Temporary Controller";
                    tmpAnimatorController.hideFlags |= HideFlags.HideAndDontSave;
                    {
                        tmpAnimatorControllerLayer = new AnimatorControllerLayer();
                        tmpAnimatorControllerLayer.name = "Very Animation Layer";
                        {
                            tmpAnimatorControllerLayer.stateMachine = new AnimatorStateMachine();
                            tmpAnimatorControllerLayer.stateMachine.name = tmpAnimatorControllerLayer.name;
                            tmpAnimatorControllerLayer.stateMachine.hideFlags |= HideFlags.HideAndDontSave;
                            {
                                tmpAnimationState = new AnimatorState();
                                tmpAnimationState.hideFlags |= HideFlags.HideAndDontSave;
                                tmpAnimationState.name = "Animation";
                                tmpAnimatorControllerLayer.stateMachine.states = new ChildAnimatorState[]
                                {
                                    new ChildAnimatorState()
                                    {
                                        state = tmpAnimationState,
                                    },
                                };
                            }
                        }
                        tmpAnimatorController.layers = new AnimatorControllerLayer[] { tmpAnimatorControllerLayer };
                    }
                }
                if (animator.runtimeAnimatorController != tmpAnimatorController)
                    UnityEditor.Animations.AnimatorController.SetAnimatorController(animator, tmpAnimatorController);
                #endregion

                #region Settings
                if (tmpAnimatorControllerLayer.iKPass != m_ApplyIK)
                {
                    tmpAnimatorControllerLayer.iKPass = m_ApplyIK;
                    tmpAnimatorController.layers = new AnimatorControllerLayer[] { tmpAnimatorControllerLayer };
                }
                if (tmpAnimationState.motion != clip)
                    tmpAnimationState.motion = clip;
                #endregion

                if (!animator.isInitialized)
                    animator.Rebind();

                if (m_ApplyIK)
                {
                    float normalizedTime;
                    {
                        AnimationClipSettings animationClipSettings = AnimationUtility.GetAnimationClipSettings(clip);
                        var totalTime = animationClipSettings.stopTime - animationClipSettings.startTime;
                        var ttime = time;
                        if (ttime > 0f && ttime >= totalTime)
                            ttime = totalTime - 0.0001f;
                        normalizedTime = totalTime == 0.0 ? 0.0f : (float)((ttime - animationClipSettings.startTime) / (totalTime));
                    }
                    animator.Play(tmpAnimationState.nameHash, 0, normalizedTime);
                    animator.Update(0f);
                }
                else
                {
#if UNITY_2018_3_OR_NEWER
                    if (animator.applyRootMotion)
                        animator.applyRootMotion = false;
#endif

                    clip.SampleAnimation(gameObject, time);

#if UNITY_2018_3_OR_NEWER
                    if (animator.applyRootMotion != vaw.animator.applyRootMotion)
                        animator.applyRootMotion = vaw.animator.applyRootMotion;
#endif
                }
            }
            else if (animation != null)
            {
                WrapMode? beforeWrapMode = null;
                try
                {
                    if (clip.wrapMode != WrapMode.Default)
                    {
                        beforeWrapMode = clip.wrapMode;
                        clip.wrapMode = WrapMode.Default;
                    }

                    clip.SampleAnimation(gameObject, time);
                }
                finally
                {
                    if (beforeWrapMode.HasValue)
                    {
                        clip.wrapMode = beforeWrapMode.Value;
                    }
                }
            }

#if UNITY_2018_3_OR_NEWER
            #region Offset
            if (m_AnimationOffsetPlayable.IsValid())
            {
                gameObjectTransform.localPosition = (m_OffsetRotation * gameObjectTransform.localPosition) + m_OffsetPosition;
                gameObjectTransform.localRotation = m_OffsetRotation * gameObjectTransform.localRotation;
            }
            #endregion
#endif
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

        private void UpdateBones()
        {
            #region Humanoid
            if (va.isHuman && animator != null)
            {
                if (!animator.isInitialized)
                    animator.Rebind();

                humanoidBones = new GameObject[HumanTrait.BoneCount];
                for (int bone = 0; bone < HumanTrait.BoneCount; bone++)
                {
                    var t = animator.GetBoneTransform((HumanBodyBones)bone);
                    if (t != null)
                    {
                        humanoidBones[bone] = t.gameObject;
                    }
                }
                humanoidHipsTransform = humanoidBones[(int)HumanBodyBones.Hips].transform;
                humanPoseHandler = new HumanPoseHandler(animator.avatar, va.uAnimator.GetAvatarRoot(animator));
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
            }
            else
            {
                humanoidBones = null;
                humanoidHipsTransform = null;
                humanPoseHandler = null;
            }
            #endregion
            #region bones
            bones = EditorCommon.GetHierarchyGameObject(gameObject).ToArray();
            boneDic = new Dictionary<GameObject, int>(bones.Length);
            for (int i = 0; i < bones.Length; i++)
            {
                boneDic.Add(bones[i], i);
            }
            #endregion

            #region EqualCheck
            {/*
                Assert.IsTrue(bones.Length == va.bones.Length);
                for (int i = 0; i < bones.Length; i++)
                {
                    Assert.IsTrue(bones[i].name == va.bones[i].name);
                }
                if (va.isHuman)
                {
                    Assert.IsTrue(humanoidBones.Length == va.humanoidBones.Length);
                    for (int i = 0; i < humanoidBones.Length; i++)
                    {
                        Assert.IsTrue(humanoidBones[i].name == va.humanoidBones[i].name);
                    }
                }*/
            }
            #endregion
        }

#if UNITY_2018_3_OR_NEWER
        private void PlayableGraphReady(AnimationClip clip)
        {
            if (animator == null)
                return;

            UnityEditor.Animations.AnimatorController.SetAnimatorController(animator, null);

            bool animatesRootTransform = animator.applyRootMotion;
            bool requiresOffsetPlayable = va.rootMotionBoneIndex >= 0;
            bool requiresMotionXPlayable = animatesRootTransform;
            bool usesAbsoluteMotion = true;
            if (va.uAw.GetLinkedWithTimeline())
            {
#if VERYANIMATION_TIMELINE
                va.uAw.GetTimelineAnimationTrackInfo(out animatesRootTransform, out requiresMotionXPlayable, out usesAbsoluteMotion);
                requiresOffsetPlayable = requiresMotionXPlayable;
#else
                Assert.IsTrue(false);
#endif
            }

            if (m_PlayableGraph.IsValid())
            {
                if (m_AnimationClipPlayable.IsValid())
                {
                    if (m_AnimationClipPlayable.GetAnimationClip() == clip &&
                        m_AnimatesRootTransform == animatesRootTransform &&
                        m_RequiresOffsetPlayable == requiresOffsetPlayable &&
                        m_RequiresMotionXPlayable == requiresMotionXPlayable &&
                        m_UsesAbsoluteMotion == usesAbsoluteMotion)
                    {
                        return;
                    }
                }
                m_PlayableGraph.Destroy();
            }
            m_AnimatesRootTransform = animatesRootTransform;
            m_RequiresOffsetPlayable = requiresOffsetPlayable;
            m_RequiresMotionXPlayable = requiresMotionXPlayable;
            m_UsesAbsoluteMotion = usesAbsoluteMotion;

            m_PlayableGraph = PlayableGraph.Create(gameObject.name + "_DummyObject");
            m_PlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            m_AnimationClipPlayable = AnimationClipPlayable.Create(m_PlayableGraph, clip);
            m_AnimationClipPlayable.SetApplyPlayableIK(m_ApplyIK);
            m_AnimationClipPlayable.SetApplyFootIK(false);
            if (m_UAnimationClipPlayable == null)
                m_UAnimationClipPlayable = new UAnimationClipPlayable();
            m_UAnimationClipPlayable.SetRemoveStartOffset(m_AnimationClipPlayable, m_RemoveStartOffset);
#if UNITY_2019_1_OR_NEWER
            m_UAnimationClipPlayable.SetOverrideLoopTime(m_AnimationClipPlayable, true);
            m_UAnimationClipPlayable.SetLoopTime(m_AnimationClipPlayable, false);
#endif

            Playable rootPlayable = m_AnimationClipPlayable;

            if (m_AnimatesRootTransform)
            {
                if (m_RequiresOffsetPlayable)
                {
                    if (m_UAnimationOffsetPlayable == null)
                        m_UAnimationOffsetPlayable = new UAnimationOffsetPlayable();
                    m_AnimationOffsetPlayable = m_UAnimationOffsetPlayable.Create(m_PlayableGraph, m_OffsetPosition, m_OffsetRotation, 1);
                    m_AnimationOffsetPlayable.SetInputWeight(0, 1f);
                    m_PlayableGraph.Connect(rootPlayable, 0, m_AnimationOffsetPlayable, 0);
                    rootPlayable = m_AnimationOffsetPlayable;
                }
                if (m_RequiresMotionXPlayable)
                {
                    if (m_UAnimationMotionXToDeltaPlayable == null)
                        m_UAnimationMotionXToDeltaPlayable = new UAnimationMotionXToDeltaPlayable();
                    m_AnimationMotionXToDeltaPlayable = m_UAnimationMotionXToDeltaPlayable.Create(m_PlayableGraph);
                    m_UAnimationMotionXToDeltaPlayable.SetAbsoluteMotion(m_AnimationMotionXToDeltaPlayable, m_UsesAbsoluteMotion);
                    m_AnimationMotionXToDeltaPlayable.SetInputWeight(0, 1f);
                    m_PlayableGraph.Connect(rootPlayable, 0, m_AnimationMotionXToDeltaPlayable, 0);
                    rootPlayable = m_AnimationMotionXToDeltaPlayable;
                }
            }

            var playableOutput = AnimationPlayableOutput.Create(m_PlayableGraph, "Animation", animator);
            playableOutput.SetSourcePlayable(rootPlayable);
        }
#endif
    }
}
