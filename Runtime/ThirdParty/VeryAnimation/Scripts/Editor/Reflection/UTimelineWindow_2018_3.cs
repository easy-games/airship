#if !UNITY_2019_1_OR_NEWER
#define VERYANIMATION_TIMELINE
#endif

using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

#if VERYANIMATION_TIMELINE
using UnityEngine.Playables;
using UnityEngine.Timeline;
#endif

namespace VeryAnimation
{
#if VERYANIMATION_TIMELINE
#if UNITY_2018_2_OR_NEWER
    public class UTimelineWindow_2018_3 : UTimelineWindow_2018_2
    {
        public UAnimationTrack_2018_3 uAnimationTrack_2018_3 { get; private set; }
        public UAnimationPlayableAsset_2018_3 uAnimationPlayableAsset_2018_3 { get; private set; }

        public UTimelineWindow_2018_3()
        {
            Assembly asmTimelineEditor, asmTimelineEngine;
            GetTimelineAssembly(out asmTimelineEditor, out asmTimelineEngine);

            uAnimationTrack = uAnimationTrack_2018_3 = new UAnimationTrack_2018_3();
            uAnimationPlayableAsset = uAnimationPlayableAsset_2018_3 = new UAnimationPlayableAsset_2018_3();
        }
        public class UAnimationTrack_2018_3 : UAnimationTrack
        {
            protected FieldInfo fi_m_ClipOffset;
            protected Func<Vector3> dg_GetSceneOffsetPosition;
            protected Func<Vector3> dg_GetSceneOffsetRotation;
            protected MethodInfo mi_RequiresMotionXPlayable;
            protected MethodInfo mi_UsesAbsoluteMotion;
            protected MethodInfo mi_GetOffsetMode;
            protected Func<bool> dg_AnimatesRootTransform;

            public IPlayable GetClipOffset(object instance)
            {
                if (fi_m_ClipOffset == null)
                    fi_m_ClipOffset = instance.GetType().GetField("m_ClipOffset", BindingFlags.NonPublic | BindingFlags.Instance);
                return (IPlayable)fi_m_ClipOffset.GetValue(instance);
            }
            public Vector3 GetSceneOffsetPosition(object instance)
            {
                if (instance == null) return Vector3.zero;
                if (dg_GetSceneOffsetPosition == null || dg_GetSceneOffsetPosition.Target != instance)
                    dg_GetSceneOffsetPosition = (Func<Vector3>)Delegate.CreateDelegate(typeof(Func<Vector3>), instance, instance.GetType().GetProperty("sceneOffsetPosition", BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod(true));
                return dg_GetSceneOffsetPosition();
            }
            public Quaternion GetSceneOffsetRotation(object instance)
            {
                if (instance == null) return Quaternion.identity;
                if (dg_GetSceneOffsetRotation == null || dg_GetSceneOffsetRotation.Target != instance)
                    dg_GetSceneOffsetRotation = (Func<Vector3>)Delegate.CreateDelegate(typeof(Func<Vector3>), instance, instance.GetType().GetProperty("sceneOffsetRotation", BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod(true));
                return Quaternion.Euler(dg_GetSceneOffsetRotation());
            }

            public bool RequiresMotionXPlayable(object instance, int mode, GameObject gameObject)
            {
                if (instance == null) return false;
                if (mi_RequiresMotionXPlayable == null)
                    mi_RequiresMotionXPlayable = instance.GetType().GetMethod("RequiresMotionXPlayable", BindingFlags.NonPublic | BindingFlags.Instance);
                return (bool)mi_RequiresMotionXPlayable.Invoke(instance, new object[] { mode, gameObject });
            }
            public bool UsesAbsoluteMotion(object instance, int mode)
            {
                if (instance == null) return false;
                if (mi_UsesAbsoluteMotion == null)
                    mi_UsesAbsoluteMotion = instance.GetType().GetMethod("UsesAbsoluteMotion", BindingFlags.NonPublic | BindingFlags.Static);
                return (bool)mi_UsesAbsoluteMotion.Invoke(null, new object[] { mode });
            }
            public int GetOffsetMode(object instance, GameObject go, bool animatesRootTransform)
            {
                if (instance == null) return -1;
                if (mi_GetOffsetMode == null)
                    mi_GetOffsetMode = instance.GetType().GetMethod("GetOffsetMode", BindingFlags.NonPublic | BindingFlags.Instance);
                return (int)mi_GetOffsetMode.Invoke(instance, new object[] { go, animatesRootTransform });
            }
            public bool AnimatesRootTransform(object instance)
            {
                if (instance == null) return false;
                if (dg_AnimatesRootTransform == null || dg_AnimatesRootTransform.Target != instance)
                    dg_AnimatesRootTransform = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetMethod("AnimatesRootTransform", BindingFlags.NonPublic | BindingFlags.Instance));
                return dg_AnimatesRootTransform();
            }
        }
        public class UAnimationPlayableAsset_2018_3 : UAnimationPlayableAsset
        {
            private Func<bool> dg_get_hasRootTransforms;

            public UAnimationPlayableAsset_2018_3()
            {
            }

            public override bool GetHasRootTransforms(AnimationPlayableAsset instance)
            {
                if (instance == null) return false;
                if (dg_get_hasRootTransforms == null || dg_get_hasRootTransforms.Target != (object)instance)
                    dg_get_hasRootTransforms = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("hasRootTransforms", BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod(true));
                return dg_get_hasRootTransforms();
            }
        }
    }
#endif
#endif
}
