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
    public class UAnimationMotionXToDeltaPlayable
    {
        public Type type { get; private set; }

        private FieldInfo m_m_Handle;
        private MethodInfo m_Create;
        private MethodInfo m_SetAbsoluteMotion;

        private UPlayable uPlayable;

        public UAnimationMotionXToDeltaPlayable()
        {
            var asmUnityEngine = typeof(UnityEngine.Animations.AnimationClipPlayable).Assembly;
            Assert.IsNotNull(type = asmUnityEngine.GetType("UnityEngine.Animations.AnimationMotionXToDeltaPlayable"));
            Assert.IsNotNull(m_m_Handle = type.GetField("m_Handle", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(m_Create = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(m_SetAbsoluteMotion = type.GetMethod("SetAbsoluteMotion"));
            uPlayable = new UPlayable();
        }

        public Playable Create(PlayableGraph graph)
        {
            var obj = m_Create.Invoke(null, new object[] { graph });
            var hanble = (PlayableHandle)m_m_Handle.GetValue(obj);
            return uPlayable.Create(hanble);
        }

        public void SetAbsoluteMotion(Playable playable, bool value)
        {
            var tmp = Activator.CreateInstance(type);
            m_m_Handle.SetValue(tmp, playable.GetHandle());
            m_SetAbsoluteMotion.Invoke(tmp, new object[] { value });
        }
    }
}
#endif
