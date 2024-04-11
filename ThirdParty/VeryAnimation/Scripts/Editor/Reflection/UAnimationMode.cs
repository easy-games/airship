using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UAnimationMode
    {
#if UNITY_2019_1_OR_NEWER
        FieldInfo m_onAnimationRecordingStart;
        FieldInfo m_onAnimationRecordingStop;
#endif

        public UAnimationMode()
        {
#if UNITY_2019_1_OR_NEWER
            var type = typeof(AnimationMode);

            Assert.IsNotNull(m_onAnimationRecordingStart = type.GetField("onAnimationRecordingStart", BindingFlags.NonPublic | BindingFlags.Static));
            Assert.IsNotNull(m_onAnimationRecordingStop = type.GetField("onAnimationRecordingStop", BindingFlags.NonPublic | BindingFlags.Static));
#endif
        }

#if UNITY_2019_1_OR_NEWER
        public Action GetOnAnimationRecordingStart()
        {
            return (Action)m_onAnimationRecordingStart.GetValue(null);
        }
        public void SetOnAnimationRecordingStart(Action action)
        {
            m_onAnimationRecordingStart.SetValue(null, action);
        }

        public Action GetOnAnimationRecordingStop()
        {
            return (Action)m_onAnimationRecordingStop.GetValue(null);
        }
        public void SetOnAnimationRecordingStop(Action action)
        {
            m_onAnimationRecordingStop.SetValue(null, action);
        }
#endif
    }
}
