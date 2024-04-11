#if UNITY_2019_1_OR_NEWER
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
using System;
using System.Reflection;
using System.Collections;

#if VERYANIMATION_TIMELINE
using UnityEngine.Timeline;
#endif

namespace VeryAnimation
{
    public class UAnimationWindow_2019_1 : UAnimationWindow_2018_1    //2019.1 or later
    {
        protected class UAnimationWindowControl_2019_1 : UAnimationWindowControl
        {
            protected Func<object, PlayableGraph> dg_get_m_Graph;
            protected Func<object, AnimationClipPlayable> dg_get_m_ClipPlayable;
            protected Func<object, AnimationClipPlayable> dg_get_m_CandidateClipPlayable;
            protected Action<int> dg_ResampleAnimationHasFlag;
            protected Action dg_DestroyGraph;

            public UAnimationWindowControl_2019_1(Assembly asmUnityEditor) : base(asmUnityEditor)
            {
                var animationWindowControlType = asmUnityEditor.GetType("UnityEditorInternal.AnimationWindowControl");
                Assert.IsNotNull(dg_get_m_Graph = EditorCommon.CreateGetFieldDelegate<PlayableGraph>(animationWindowControlType.GetField("m_Graph", BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.IsNotNull(dg_get_m_ClipPlayable = EditorCommon.CreateGetFieldDelegate<AnimationClipPlayable>(animationWindowControlType.GetField("m_ClipPlayable", BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.IsNotNull(dg_get_m_CandidateClipPlayable = EditorCommon.CreateGetFieldDelegate<AnimationClipPlayable>(animationWindowControlType.GetField("m_CandidateClipPlayable", BindingFlags.NonPublic | BindingFlags.Instance)));
            }

            public PlayableGraph GetGraph(object instance)
            {
                if (instance == null) return new PlayableGraph();
                return dg_get_m_Graph(instance);
            }
            public void DestroyGraph(object instance)
            {
                if (instance == null) return;
                if (dg_DestroyGraph == null || dg_DestroyGraph.Target != instance)
                    dg_DestroyGraph = (Action)Delegate.CreateDelegate(typeof(Action), instance, instance.GetType().GetMethod("DestroyGraph", BindingFlags.NonPublic | BindingFlags.Instance));
                dg_DestroyGraph();
            }
            public AnimationClipPlayable GetClipPlayable(object instance)
            {
                if (instance == null) return new AnimationClipPlayable();
                return dg_get_m_ClipPlayable(instance);
            }
            public AnimationClipPlayable GetCandidateClipPlayable(object instance)
            {
                if (instance == null) return new AnimationClipPlayable();
                return dg_get_m_CandidateClipPlayable(instance);
            }
            public override void ResampleAnimation(object instance)
            {
                if (instance == null) return;
                if (dg_ResampleAnimationHasFlag == null || dg_ResampleAnimationHasFlag.Target != instance)
                    dg_ResampleAnimationHasFlag = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), instance, instance.GetType().GetMethod("ResampleAnimation", BindingFlags.NonPublic | BindingFlags.Instance));
                dg_ResampleAnimationHasFlag(0);
            }
        }

        protected UAnimationWindowControl_2019_1 uAnimationWindowControl_2019_1;

        public UAnimationWindow_2019_1()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var animationWindowType = asmUnityEditor.GetType("UnityEditor.AnimationWindow");
            uAnimationWindowControl = uAnimationWindowControl_2019_1 = new UAnimationWindowControl_2019_1(asmUnityEditor);
        }

        public PlayableGraph GetPlayableGraph()
        {
            return uAnimationWindowControl_2019_1.GetGraph(animationWindowControlInstance);
        }
        public void DestroyPlayableGraph()
        {
            uAnimationWindowControl_2019_1.DestroyGraph(animationWindowControlInstance);
        }
        public AnimationClipPlayable GetClipPlayable()
        {
            return uAnimationWindowControl_2019_1.GetClipPlayable(animationWindowControlInstance);
        }
        public AnimationClipPlayable GetCandidateClipPlayable()
        {
            return uAnimationWindowControl_2019_1.GetCandidateClipPlayable(animationWindowControlInstance);
        }

#if VERYANIMATION_TIMELINE
        public override bool GetTimelineRootMotionOffsets(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

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
                    position += rotation * animtionTrack.infiniteClipOffsetPosition;
                    rotation *= animtionTrack.infiniteClipOffsetRotation;
                }
            }

            return true;
        }
#endif
    }
}
#endif
