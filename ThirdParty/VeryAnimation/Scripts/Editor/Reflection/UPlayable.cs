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
    public class UPlayable
    {
        private FieldInfo m_m_Handle;

        public UPlayable()
        {
            Assert.IsNotNull(m_m_Handle = typeof(Playable).GetField("m_Handle", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        public Playable Create(PlayableHandle handle)
        {
            object obj = new Playable();
            m_m_Handle.SetValue(obj, handle);
            return (Playable)obj;
        }
    }
}
#endif