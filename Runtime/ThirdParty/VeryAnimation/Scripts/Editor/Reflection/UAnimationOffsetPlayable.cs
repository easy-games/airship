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
    public class UAnimationOffsetPlayable
    {
        public Type type { get; private set; }

        private FieldInfo m_m_Handle;
        private MethodInfo m_Create;
        private MethodInfo m_SetPosition;
        private MethodInfo m_SetRotation;
        private MethodInfo m_GetPosition;
        private MethodInfo m_GetRotation;

        private object instance;
        private UPlayable uPlayable;

        public UAnimationOffsetPlayable()
        {
            var asmUnityEngine = typeof(UnityEngine.Animations.AnimationClipPlayable).Assembly;
            Assert.IsNotNull(type = asmUnityEngine.GetType("UnityEngine.Animations.AnimationOffsetPlayable"));
            Assert.IsNotNull(m_m_Handle = type.GetField("m_Handle", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(m_Create = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(m_SetPosition = type.GetMethod("SetPosition", BindingFlags.Public | BindingFlags.Instance));
            Assert.IsNotNull(m_SetRotation = type.GetMethod("SetRotation", BindingFlags.Public | BindingFlags.Instance));
            Assert.IsNotNull(m_GetPosition = type.GetMethod("GetPosition", BindingFlags.Public | BindingFlags.Instance));
            Assert.IsNotNull(m_GetRotation = type.GetMethod("GetRotation", BindingFlags.Public | BindingFlags.Instance));
            uPlayable = new UPlayable();
            instance = Activator.CreateInstance(type);
        }

        public Playable Create(PlayableGraph graph, Vector3 position, Quaternion rotation, int inputCount)
        {
            var obj = m_Create.Invoke(null, new object[] { graph, position, rotation, inputCount });
            var hanble = (PlayableHandle)m_m_Handle.GetValue(obj);
            return uPlayable.Create(hanble);
        }

        public void SetPosition(IPlayable playable, Vector3 value)
        {
            m_m_Handle.SetValue(instance, playable.GetHandle());
            m_SetPosition.Invoke(instance, new object[] { value });
        }
        public void SetRotation(IPlayable playable, Quaternion value)
        {
            m_m_Handle.SetValue(instance, playable.GetHandle());
            m_SetRotation.Invoke(instance, new object[] { value });
        }
        public Vector3 GetPosition(IPlayable playable)
        {
            m_m_Handle.SetValue(instance, playable.GetHandle());
            return (Vector3)m_GetPosition.Invoke(instance, null);
        }
        public Quaternion GetRotation(IPlayable playable)
        {
            m_m_Handle.SetValue(instance, playable.GetHandle());
            return (Quaternion)m_GetRotation.Invoke(instance, null);
        }
    }
}
#endif