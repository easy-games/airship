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
    public class UTimelineWindow_2018_2 : UTimelineWindow
    {
        public UTimelineWindowTimeControl_2018_2 uTimelineWindowTimeControl_2018_2 { get; private set; }
        public UTimelineState_2018_2 uTimelineState_2018_2 { get; private set; }
        public UTrackAsset_2018_2 uTrackAsset_2018_2 { get; private set; }

        public UTimelineWindow_2018_2()
        {
            Assembly asmTimelineEditor, asmTimelineEngine;
            GetTimelineAssembly(out asmTimelineEditor, out asmTimelineEngine);

            uTimelineWindowTimeControl = uTimelineWindowTimeControl_2018_2 = new UTimelineWindowTimeControl_2018_2(asmTimelineEditor, asmTimelineEngine);
            uTimelineState = uTimelineState_2018_2 = new UTimelineState_2018_2();
            uTrackAsset = uTrackAsset_2018_2 = new UTrackAsset_2018_2();
        }

        public class UISequenceState
        {
            private Func<PlayableDirector> dg_get_director;
            private Func<int> dg_get_frame;
            private Action<int> dg_set_frame;
            private Func<float> dg_get_frameRate;
            private Func<double> dg_get_frameRateDouble;

            public PlayableDirector GetDirector(object instance)
            {
                if (instance == null) return null;
                if (dg_get_director == null || dg_get_director.Target != instance)
                    dg_get_director = (Func<PlayableDirector>)Delegate.CreateDelegate(typeof(Func<PlayableDirector>), instance, instance.GetType().GetProperty("director").GetGetMethod());
                return dg_get_director();
            }

            public int GetFrame(object instance)
            {
                if (instance == null) return 0;
                if (dg_get_frame == null || dg_get_frame.Target != instance)
                    dg_get_frame = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), instance, instance.GetType().GetProperty("frame").GetGetMethod());
                return dg_get_frame();
            }
            public void SetFrame(object instance, int frame)
            {
                if (instance == null) return;
                if (dg_set_frame == null || dg_set_frame.Target != instance)
                    dg_set_frame = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), instance, instance.GetType().GetProperty("frame").GetSetMethod());
                dg_set_frame(frame);
            }

            public float GetFrameRate(object instance)
            {
                if (instance == null) return 0f;
                if (!(dg_get_frameRate != null && dg_get_frameRate.Target == instance) &&
                    !(dg_get_frameRateDouble != null && dg_get_frameRateDouble.Target == instance))
                {
                    var mi = instance.GetType().GetProperty("frameRate").GetGetMethod();
                    dg_get_frameRate = null;
                    dg_get_frameRateDouble = null;
                    if (mi.ReturnType == typeof(double))
                        dg_get_frameRateDouble = (Func<double>)Delegate.CreateDelegate(typeof(Func<double>), instance, mi);
                    else
                        dg_get_frameRate = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), instance, mi);
                }
                if (dg_get_frameRate != null && dg_get_frameRate.Target == instance)
                    return dg_get_frameRate();
                if (dg_get_frameRateDouble != null && dg_get_frameRateDouble.Target == instance)
                    return (float)dg_get_frameRateDouble();
                return 0f;
            }
        }

        public class UTimelineState_2018_2 : UTimelineState //UWindowState
        {
            public UISequenceState uISequenceState { get; private set; }

            private Func<object> dg_get_editSequence;

            public UTimelineState_2018_2()
            {
                uISequenceState = new UISequenceState();
            }

            public override PlayableDirector GetCurrentDirector(object instance)
            {
                if (instance == null) return null;
                return uISequenceState.GetDirector(GetEditSequence(instance));
            }

            public override void SetPreviewMode(object instance, bool enable)
            {
                if (instance == null) return;
                if (dg_set_previewMode == null || dg_set_previewMode.Target != instance)
                    dg_set_previewMode = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), instance, instance.GetType().GetProperty("previewMode").GetSetMethod());
                dg_set_previewMode(enable);
                if (!enable)
                {
                    var mi = instance.GetType().GetMethod("SetPlaying");
                    mi.Invoke(instance, new object[] { false });
                }
                else
                {
                    if (dg_set_rebuildGraph == null || dg_set_rebuildGraph.Target != instance)
                        dg_set_rebuildGraph = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), instance, instance.GetType().GetProperty("rebuildGraph").GetSetMethod());
                    dg_set_rebuildGraph(true);
                }
            }

            public override int GetFrame(object instance)
            {
                if (instance == null) return 0;
                return uISequenceState.GetFrame(GetEditSequence(instance));
            }
            public override void SetFrame(object instance, int frame)
            {
                if (instance == null) return;
                uISequenceState.SetFrame(GetEditSequence(instance), frame);
            }

            public override float GetFrameRate(object instance)
            {
                if (instance == null) return 0f;
                return uISequenceState.GetFrameRate(GetEditSequence(instance));
            }

            public object GetEditSequence(object instance)
            {
                if (instance == null) return null;
                if (dg_get_editSequence == null || dg_get_editSequence.Target != instance)
                    dg_get_editSequence = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), instance, instance.GetType().GetProperty("editSequence").GetGetMethod());
                return dg_get_editSequence();
            }
        }
        public class UTimelineWindowTimeControl_2018_2 : UTimelineWindowTimeControl
        {
            public UTimelineWindowTimeControl_2018_2(Assembly asmTimelineEditor, Assembly asmTimelineEngine) : base(asmTimelineEditor, asmTimelineEngine)
            {
                uTimelineState = new UTimelineState_2018_2();
            }

            public override TrackAsset GetTrackAsset(object instance)
            {
                if (instance == null) return null;
                if (dg_get_track == null || dg_get_track.Target != instance)
                    dg_get_track = (Func<TrackAsset>)Delegate.CreateDelegate(typeof(Func<TrackAsset>), instance, instance.GetType().GetProperty("track", BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod(true));
                return dg_get_track();
            }
            public override object GetTimelineState(object instance)
            {
                if (instance == null) return null;
                if (dg_get_state == null || dg_get_state.Target != instance)
                    dg_get_state = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), null, instance.GetType().GetProperty("state", BindingFlags.NonPublic | BindingFlags.Static).GetGetMethod(true));
                return dg_get_state();
            }
        }
        public class UTrackAsset_2018_2 : UTrackAsset
        {
            public override bool GetLocked(object instance)
            {
                if (instance == null) return false;
                if (dg_get_locked == null || dg_get_locked.Target != instance)
                    dg_get_locked = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("locked").GetGetMethod());
                return dg_get_locked();
            }
        }
    }
#endif
#endif
}
