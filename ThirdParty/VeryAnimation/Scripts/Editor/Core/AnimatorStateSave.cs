using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;

namespace VeryAnimation
{
    public class AnimatorStateSave
    {
        #region Static
        public static void SaveAllState()
        {
            animatorStateSaves = new Dictionary<GameObject, AnimatorStateSave>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                foreach (var go in scene.GetRootGameObjects())
                {
                    foreach (var animator in go.GetComponentsInChildren<Animator>(true))
                    {
                        if (animator == null || !animator.gameObject.activeInHierarchy || !animator.enabled || animator.runtimeAnimatorController == null)
                            continue;
                        animatorStateSaves.Add(animator.gameObject, new AnimatorStateSave(animator));
                    }
                } 
            }
        }
        public static void LoadAllState()
        {
            if (animatorStateSaves == null) return;
            foreach (var pair in animatorStateSaves)
            {
                if (pair.Key == null) continue;
                var animator = pair.Key.GetComponent<Animator>();
                if (animator == null) continue;
                pair.Value.Load(animator);
            }
            animatorStateSaves = null;
        }

        private static Dictionary<GameObject, AnimatorStateSave> animatorStateSaves;
        #endregion

        private TransformPoseSave.SaveData gameObjectTransform;

        #region Animator
        private AnimatorStateInfo[] currentAnimatorStateInfo;
        private AnimatorStateInfo[] nextAnimatorStateInfo;
        private class SaveParamater
        {
            public int nameHash;
            public UnityEngine.AnimatorControllerParameterType type;
            public object value;
        }
        private SaveParamater[] animatorParamaters;
        #endregion
        
        public AnimatorStateSave()
        {
        }
        public AnimatorStateSave(Animator animator)
        {
            Save(animator);
        }

        public void Save(Animator animator)
        {
            gameObjectTransform = new TransformPoseSave.SaveData(animator.gameObject.transform);

            currentAnimatorStateInfo = new AnimatorStateInfo[animator.layerCount];
            nextAnimatorStateInfo = new AnimatorStateInfo[animator.layerCount];
            for (int i = 0; i < animator.layerCount; i++)
            {
                currentAnimatorStateInfo[i] = animator.GetCurrentAnimatorStateInfo(i);
                nextAnimatorStateInfo[i] = animator.GetNextAnimatorStateInfo(i);
            }
            animatorParamaters = new SaveParamater[animator.parameterCount];
            for (int i = 0; i < animator.parameterCount; i++)
            {
                var param = animator.parameters[i];
                animatorParamaters[i] = new SaveParamater()
                {
                    nameHash = param.nameHash,
                    type = param.type,
                };
                switch (param.type)
                {
                case AnimatorControllerParameterType.Float: animatorParamaters[i].value = animator.GetFloat(param.nameHash); break;
                case AnimatorControllerParameterType.Int: animatorParamaters[i].value = animator.GetInteger(param.nameHash); break;
                case AnimatorControllerParameterType.Bool: animatorParamaters[i].value = animator.GetBool(param.nameHash); break;
                case AnimatorControllerParameterType.Trigger: break;
                default: Assert.IsTrue(false); break;
                }
            }
        }

        public void Load(Animator animator)
        {
            if (!animator.isInitialized)
                animator.Rebind();
            if (currentAnimatorStateInfo != null && currentAnimatorStateInfo.Length == animator.layerCount &&
                nextAnimatorStateInfo != null && nextAnimatorStateInfo.Length == animator.layerCount)
            {
                bool changed = false;
                for (int i = 0; i < animator.layerCount; i++)
                {
                    var info = animator.GetCurrentAnimatorStateInfo(i);
                    if (info.fullPathHash != currentAnimatorStateInfo[i].fullPathHash ||
                        info.shortNameHash != currentAnimatorStateInfo[i].shortNameHash ||
                        info.normalizedTime != currentAnimatorStateInfo[i].normalizedTime ||
                        info.length != currentAnimatorStateInfo[i].length)
                    {
                        changed = true;
                        break;
                    }
                    info = animator.GetNextAnimatorStateInfo(i);
                    if (info.fullPathHash != nextAnimatorStateInfo[i].fullPathHash ||
                        info.shortNameHash != nextAnimatorStateInfo[i].shortNameHash ||
                        info.normalizedTime != nextAnimatorStateInfo[i].normalizedTime ||
                        info.length != nextAnimatorStateInfo[i].length)
                    {
                        changed = true;
                        break;
                    }
                }
                if (changed)
                {
                    for (int i = 0; i < animator.layerCount; i++)
                    {
                        animator.Play(currentAnimatorStateInfo[i].fullPathHash, i, currentAnimatorStateInfo[i].normalizedTime);
                    }
                    animator.Update(0f);
                    for (int i = 0; i < animator.layerCount; i++)
                    {
                        if (nextAnimatorStateInfo[i].fullPathHash != 0)
                            animator.CrossFade(nextAnimatorStateInfo[i].fullPathHash, 1f - currentAnimatorStateInfo[i].normalizedTime, i, nextAnimatorStateInfo[i].normalizedTime);
                    }
                }
                animator.Update(0f);
                gameObjectTransform.LoadLocal(animator.gameObject.transform);
                #region RendererForceUpdate
                if (animator.gameObject != null) //Is there a bug that will not be updated while pausing? Therefore, it forcibly updates it.
                {
                    foreach (var renderer in animator.gameObject.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                            continue;
                        renderer.enabled = !renderer.enabled;
                        renderer.enabled = !renderer.enabled;
                    }
                }
                #endregion
            }
        }
    }
}
