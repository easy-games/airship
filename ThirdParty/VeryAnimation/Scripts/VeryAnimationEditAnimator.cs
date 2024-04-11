using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Assertions;
using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace VeryAnimation
{
#if UNITY_2018_3_OR_NEWER
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Animator))]
#else
    [ExecuteInEditMode, DisallowMultipleComponent, RequireComponent(typeof(Animator))]
#endif
    public class VeryAnimationEditAnimator : MonoBehaviour
    {
#if !UNITY_EDITOR
        private void Awake()
        {
            Destroy(this);
        }
#else
        public Action<int> onAnimatorIK;

        private void OnAnimatorIK(int layerIndex)
        {
            if (onAnimatorIK != null)
                onAnimatorIK.Invoke(layerIndex);
        }
#endif
    }
}
