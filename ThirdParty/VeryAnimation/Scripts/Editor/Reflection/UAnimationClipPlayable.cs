#if UNITY_2018_3_OR_NEWER
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UAnimationClipPlayable
    {
        private MethodInfo m_SetRemoveStartOffset;
#if UNITY_2019_1_OR_NEWER
        private MethodInfo m_SetOverrideLoopTime;
        private MethodInfo m_SetLoopTime;
        private MethodInfo m_SetSampleRate;
#endif

        public UAnimationClipPlayable()
        {
            var animationClipPlayableType = typeof(AnimationClipPlayable);
            Assert.IsNotNull(m_SetRemoveStartOffset = animationClipPlayableType.GetMethod("SetRemoveStartOffset", BindingFlags.Instance | BindingFlags.NonPublic));
#if UNITY_2019_1_OR_NEWER
            Assert.IsNotNull(m_SetOverrideLoopTime = animationClipPlayableType.GetMethod("SetOverrideLoopTime", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(m_SetLoopTime = animationClipPlayableType.GetMethod("SetLoopTime", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(m_SetSampleRate = animationClipPlayableType.GetMethod("SetSampleRate", BindingFlags.Instance | BindingFlags.NonPublic));
#endif
        }

        public void SetRemoveStartOffset(AnimationClipPlayable playable, bool value)
        {
            m_SetRemoveStartOffset.Invoke(playable, new object[] { value });
        }
#if UNITY_2019_1_OR_NEWER
        public void SetOverrideLoopTime(AnimationClipPlayable playable, bool value)
        {
            m_SetOverrideLoopTime.Invoke(playable, new object[] { value });
        }
        public void SetLoopTime(AnimationClipPlayable playable, bool value)
        {
            m_SetLoopTime.Invoke(playable, new object[] { value });
        }
        public void SetSampleRate(AnimationClipPlayable playable, float value)
        {
            m_SetSampleRate.Invoke(playable, new object[] { value });
        }
#endif
    }
}
#endif
