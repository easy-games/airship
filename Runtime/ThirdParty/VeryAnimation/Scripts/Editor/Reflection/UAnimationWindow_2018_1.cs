#if !UNITY_2019_1_OR_NEWER
#define VERYANIMATION_TIMELINE
#endif

using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

#if VERYANIMATION_TIMELINE
using UnityEngine.Playables;
using UnityEngine.Timeline;
#endif

namespace VeryAnimation
{
#if UNITY_2018_1_OR_NEWER
    public class UAnimationWindow_2018_1 : UAnimationWindow    //2018.1 or later
    {
        protected class UAnimEditor_2018_1 : UAnimEditor
        {
            public UAnimEditor_2018_1(Assembly asmUnityEditor) : base(asmUnityEditor)
            {
                var animEditorType = asmUnityEditor.GetType("UnityEditor.AnimEditor");
                Assert.IsNotNull(pi_selectedItem = animEditorType.GetProperty("selection"));
            }
        }

        protected class UAnimationWindowState_2018_1 : UAnimationWindowState
        {
            protected Func<AnimationClip> dg_get_activeAnimationClip;
            protected Action<AnimationClip> dg_set_activeAnimationClip;

            public UAnimationWindowState_2018_1(Assembly asmUnityEditor) : base(asmUnityEditor)
            {
            }

            public AnimationClip GetActiveAnimationClip(object instance)
            {
                if (instance == null) return null;
                if (dg_get_activeAnimationClip == null || dg_get_activeAnimationClip.Target != instance)
                    dg_get_activeAnimationClip = (Func<AnimationClip>)Delegate.CreateDelegate(typeof(Func<AnimationClip>), instance, instance.GetType().GetProperty("activeAnimationClip").GetGetMethod());
                return dg_get_activeAnimationClip();
            }
            public void SetActiveAnimationClip(object instance, AnimationClip clip)
            {
                if (instance == null) return;
                if (dg_set_activeAnimationClip == null || dg_set_activeAnimationClip.Target != instance)
                    dg_set_activeAnimationClip = (Action<AnimationClip>)Delegate.CreateDelegate(typeof(Action<AnimationClip>), instance, instance.GetType().GetProperty("activeAnimationClip").GetSetMethod());
                dg_set_activeAnimationClip(clip);
            }
        }

        protected UAnimEditor_2018_1 uAnimEditor_2018_1;
        protected UAnimationWindowState_2018_1 uAnimationWindowState_2018_1;
        protected UEditorGUIUtility_2018_1 uEditorGUIUtility_2018_1;
#if VERYANIMATION_TIMELINE
#if UNITY_2018_2_OR_NEWER
        protected UTimelineWindow_2018_2 uTimelineWindow_2018_2;
#endif
#if UNITY_2018_3_OR_NEWER
        protected UTimelineWindow_2018_3 uTimelineWindow_2018_3;
#endif
#endif

        protected Func<object, object> dg_get_m_LockTracker;

        public UAnimationWindow_2018_1()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var animationWindowType = asmUnityEditor.GetType("UnityEditor.AnimationWindow");
            uAnimEditor = uAnimEditor_2018_1 = new UAnimEditor_2018_1(asmUnityEditor);
            uAnimationWindowState = uAnimationWindowState_2018_1 = new UAnimationWindowState_2018_1(asmUnityEditor);
#if VERYANIMATION_TIMELINE
#if UNITY_2018_3_OR_NEWER
            uTimelineWindow = uTimelineWindow_2018_2 = uTimelineWindow_2018_3 = new UTimelineWindow_2018_3();
#elif UNITY_2018_2_OR_NEWER
            uTimelineWindow = uTimelineWindow_2018_2 = new UTimelineWindow_2018_2();
#endif
#endif
            uEditorGUIUtility_2018_1 = new UEditorGUIUtility_2018_1();
            Assert.IsNotNull(dg_get_m_LockTracker = EditorCommon.CreateGetFieldDelegate<object>(animationWindowType.GetField("m_LockTracker", BindingFlags.NonPublic | BindingFlags.Instance)));
        }

        public override AnimationClip GetSelectionAnimationClip()
        {
            if (instance == null) return null;
            return uAnimationWindowState_2018_1.GetActiveAnimationClip(animationWindowStateInstance);
        }
        public override void SetSelectionAnimationClip(AnimationClip animationClip)
        {
            if (instance == null) return;
            if (GetSelectionAnimationClip() == animationClip) return;

            var aws = animationWindowStateInstance;
            bool playing = uAnimationWindowState.GetPlaying(aws);
            float currentTime = uAnimationWindowState.GetCurrentTime(aws);
            {
                uAnimationWindowState_2018_1.SetActiveAnimationClip(aws, animationClip);
            }
            uAnimationWindowState.SetCurrentTime(aws, currentTime);
            if (playing)
                uAnimationWindowState.StartPlayback(aws);

            ForceRefresh();
        }

        public override bool GetLock(EditorWindow aw)
        {
            if (aw == null) return false;
            return uEditorGUIUtility_2018_1.uEditorLockTracker.GetLock(dg_get_m_LockTracker(aw));
        }
        public override void SetLock(EditorWindow aw, bool flag)
        {
            if (aw == null) return;
            uEditorGUIUtility_2018_1.uEditorLockTracker.SetLock(dg_get_m_LockTracker(aw), flag);
        }

#if VERYANIMATION_TIMELINE
#if UNITY_2018_3_OR_NEWER
        public override void GetTimelineAnimationTrackInfo(out bool animatesRootTransform, out bool requiresMotionXPlayable, out bool usesAbsoluteMotion)
        {
            animatesRootTransform = false;
            requiresMotionXPlayable = false;
            usesAbsoluteMotion = false;

            var animtionTrack = GetTimelineAnimationTrack(true);
            if (animtionTrack == null)
                return;
            var go = GetActiveRootGameObject();

            animatesRootTransform = uTimelineWindow_2018_3.uAnimationTrack_2018_3.AnimatesRootTransform(animtionTrack);
            var mode = uTimelineWindow_2018_3.uAnimationTrack_2018_3.GetOffsetMode(animtionTrack, go, animatesRootTransform);
            requiresMotionXPlayable = uTimelineWindow_2018_3.uAnimationTrack_2018_3.RequiresMotionXPlayable(animtionTrack, mode, go);
            usesAbsoluteMotion = uTimelineWindow_2018_3.uAnimationTrack_2018_3.UsesAbsoluteMotion(animtionTrack, mode);
        }
        public override bool GetTimelineRootMotionOffsets(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

#if !UNITY_2019_1_OR_NEWER
            var animtionTrack = GetTimelineAnimationTrack(true);

            //Track Offsets
            {
                if (animtionTrack == null)
                    return false;

                var hasRootTransforms = uTimelineWindow_2018_3.uAnimationTrack_2018_3.AnimatesRootTransform(animtionTrack);
                if (!hasRootTransforms)
                    return false;

                if (animtionTrack.trackOffset == TrackOffset.Auto || animtionTrack.trackOffset == TrackOffset.ApplyTransformOffsets)
                {
                    position = animtionTrack.position;
                    rotation = animtionTrack.rotation;
                }
                else if (animtionTrack.trackOffset == TrackOffset.ApplySceneOffsets)
                {
                    position = uTimelineWindow_2018_3.uAnimationTrack_2018_3.GetSceneOffsetPosition(animtionTrack);
                    rotation = uTimelineWindow_2018_3.uAnimationTrack_2018_3.GetSceneOffsetRotation(animtionTrack);
                }
            }
            //Clip Offsets
            {
                var animationPlayableAsset = GetTimelineAnimationPlayableAsset();
                if (animationPlayableAsset != null)
                {
                    position += rotation * animationPlayableAsset.position;
                    rotation *= animationPlayableAsset.rotation;
                }
                else
                {
                    position += rotation * animtionTrack.openClipOffsetPosition;
                    rotation *= animtionTrack.openClipOffsetRotation;
                }
            }
#endif
            return true;
        }
        public override bool GetTimelineAnimationRemoveStartOffset()
        {
            var animationPlayableAsset = GetTimelineAnimationPlayableAsset();
            if (animationPlayableAsset != null)
                return animationPlayableAsset.removeStartOffset;
            else
                return false;
        }
        public override bool GetTimelineAnimationApplyFootIK()
        {
            var animationPlayableAsset = GetTimelineAnimationPlayableAsset();
            if (animationPlayableAsset != null)
                return animationPlayableAsset.applyFootIK;
            else
                return true;
        }
#endif
#endif
    }
#endif
}
