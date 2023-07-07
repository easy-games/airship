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
    public class UTimelineWindow
    {
        private Func<EditorWindow> dg_get_instance;
        private Func<object> dg_get_state;
        private Func<object> dg_get_treeView;
        protected Action<AnimationClip, EditorCurveBinding, AnimationUtility.CurveModifiedType> dg_OnCurveModified;

        public UTimelineWindowTimeControl uTimelineWindowTimeControl { get; protected set; }
        public UTimelineState uTimelineState { get; protected set; }
        public UTrackAsset uTrackAsset { get; protected set; }
        public UAnimationTrack uAnimationTrack { get; protected set; }
        public UAnimationPlayableAsset uAnimationPlayableAsset { get; protected set; }
        public UTimelineTreeViewGUI uTimelineTreeViewGUI { get; protected set; }
        public UTimelineAnimationUtilities uTimelineAnimationUtilities { get; protected set; }

        public UTimelineWindow()
        {
            Assembly asmTimelineEditor, asmTimelineEngine;
            GetTimelineAssembly(out asmTimelineEditor, out asmTimelineEngine);

            var timelineWindowType = asmTimelineEditor.GetType("UnityEditor.Timeline.TimelineWindow");
            Assert.IsNotNull(dg_get_instance = (Func<EditorWindow>)Delegate.CreateDelegate(typeof(Func<EditorWindow>), null, timelineWindowType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetGetMethod()));

            uTimelineWindowTimeControl = new UTimelineWindowTimeControl(asmTimelineEditor, asmTimelineEngine);
            uTimelineState = new UTimelineState();
            uTrackAsset = new UTrackAsset();
            uAnimationTrack = new UAnimationTrack();
            uAnimationPlayableAsset = new UAnimationPlayableAsset();
            uTimelineTreeViewGUI = new UTimelineTreeViewGUI();
            uTimelineAnimationUtilities = new UTimelineAnimationUtilities(asmTimelineEditor, asmTimelineEngine);
        }

        public class UTimelineState //UWindowState
        {
            protected Func<PlayableDirector> dg_get_currentDirector;
            protected Func<bool> dg_get_recording;
            protected Action<bool> dg_set_recording;
            protected Func<bool> dg_get_previewMode;
            protected Action<bool> dg_set_previewMode;
            protected Action<bool> dg_set_playing;
            protected Func<int> dg_get_frame;
            protected Action<int> dg_set_frame;
            protected Func<float> dg_get_frameRate;
            protected Action<bool> dg_set_rebuildGraph;
            protected Func<TrackAsset, bool> dg_get_IsArmedForRecord;

            public virtual PlayableDirector GetCurrentDirector(object instance)
            {
                if (instance == null) return null;
                if (dg_get_currentDirector == null || dg_get_currentDirector.Target != instance)
                    dg_get_currentDirector = (Func<PlayableDirector>)Delegate.CreateDelegate(typeof(Func<PlayableDirector>), instance, instance.GetType().GetProperty("currentDirector").GetGetMethod());
                return dg_get_currentDirector();
            }

            public bool GetRecording(object instance)
            {
                if (instance == null) return false;
                if (dg_get_recording == null || dg_get_recording.Target != instance)
                    dg_get_recording = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("recording").GetGetMethod());
                return dg_get_recording();
            }
            public void SetRecording(object instance, bool enable)
            {
                if (instance == null) return;
                if (dg_set_recording == null || dg_set_recording.Target != instance)
                    dg_set_recording = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), instance, instance.GetType().GetProperty("recording").GetSetMethod());
                try
                {
                    dg_set_recording(enable);
                }
                catch
                {
                }
            }

            public bool GetPreviewMode(object instance)
            {
                if (instance == null) return false;
                if (dg_get_previewMode == null || dg_get_previewMode.Target != instance)
                    dg_get_previewMode = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("previewMode").GetGetMethod());
                return dg_get_previewMode();
            }
            public virtual void SetPreviewMode(object instance, bool enable)
            {
                if (instance == null) return;
                if (dg_set_previewMode == null || dg_set_previewMode.Target != instance)
                    dg_set_previewMode = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), instance, instance.GetType().GetProperty("previewMode").GetSetMethod());
                dg_set_previewMode(enable);
                if (!enable)
                {
                    if (dg_set_playing == null || dg_set_playing.Target != instance)
                        dg_set_playing = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), instance, instance.GetType().GetProperty("playing").GetSetMethod());
                    dg_set_playing(false);
                }
                else
                {
                    if (dg_set_rebuildGraph == null || dg_set_rebuildGraph.Target != instance)
                        dg_set_rebuildGraph = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), instance, instance.GetType().GetProperty("rebuildGraph").GetSetMethod());
                    dg_set_rebuildGraph(true);
                }
            }

            public virtual int GetFrame(object instance)
            {
                if (instance == null) return 0;
                if (dg_get_frame == null || dg_get_frame.Target != instance)
                    dg_get_frame = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), instance, instance.GetType().GetProperty("frame").GetGetMethod());
                return dg_get_frame();
            }
            public virtual void SetFrame(object instance, int frame)
            {
                if (instance == null) return;
                if (dg_set_frame == null || dg_set_frame.Target != instance)
                    dg_set_frame = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), instance, instance.GetType().GetProperty("frame").GetSetMethod());
                dg_set_frame(frame);
            }

            public virtual float GetFrameRate(object instance)
            {
                if (instance == null) return 0f;
                if (dg_get_frameRate == null || dg_get_frameRate.Target != instance)
                    dg_get_frameRate = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), instance, instance.GetType().GetProperty("frameRate").GetGetMethod());
                return dg_get_frameRate();
            }

            public bool IsArmedForRecord(object instance, TrackAsset track)
            {
                if (instance == null) return false;
                if (dg_get_IsArmedForRecord == null || dg_get_IsArmedForRecord.Target != instance)
                    dg_get_IsArmedForRecord = (Func<TrackAsset, bool>)Delegate.CreateDelegate(typeof(Func<TrackAsset, bool>), instance, instance.GetType().GetMethod("IsArmedForRecord"));
                return dg_get_IsArmedForRecord(track);
            }
        }
        public class UTimelineWindowTimeControl
        {
            public Type timelineWindowTimeControlType { get; protected set; }

            protected Func<object, TimelineClip> dg_get_m_Clip;
            protected Func<TrackAsset> dg_get_track;
            protected Func<object> dg_get_state;

            protected UTimelineState uTimelineState;

            public class SetGenericBindingMemory
            {
                public PlayableDirector playableDirector;
                public UnityEngine.Object key;
                public UnityEngine.Object value;
                public UnityEngine.Object original;
            }

            public UTimelineWindowTimeControl(Assembly asmTimelineEditor, Assembly asmTimelineEngine)
            {
                timelineWindowTimeControlType = asmTimelineEditor.GetType("UnityEditor.Timeline.TimelineWindowTimeControl");
                Assert.IsNotNull(dg_get_m_Clip = EditorCommon.CreateGetFieldDelegate<TimelineClip>(timelineWindowTimeControlType.GetField("m_Clip", BindingFlags.NonPublic | BindingFlags.Instance)));

                uTimelineState = new UTimelineState();
            }

            public virtual TrackAsset GetTrackAsset(object instance)
            {
                if (instance == null) return null;
                if (dg_get_track == null || dg_get_track.Target != instance)
                    dg_get_track = (Func<TrackAsset>)Delegate.CreateDelegate(typeof(Func<TrackAsset>), instance, instance.GetType().GetProperty("track").GetGetMethod());
                return dg_get_track();
            }
            public virtual object GetTimelineState(object instance)
            {
                if (instance == null) return null;
                if (dg_get_state == null || dg_get_state.Target != instance)
                    dg_get_state = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), instance, instance.GetType().GetProperty("state").GetGetMethod());
                return dg_get_state();
            }

            public TimelineClip GetTimelineClip(object instance)
            {
                if (instance == null) return null;
                return dg_get_m_Clip(instance);
            }
            public PlayableAsset GetPlayableAsset(object instance)
            {
                if (instance == null) return null;
                var clip = dg_get_m_Clip(instance);
                if (clip == null) return null;
                return clip.asset as PlayableAsset;
            }
            public UnityEngine.Object GetGenericBinding(object instance)
            {
                var currentDirector = uTimelineState.GetCurrentDirector(GetTimelineState(instance));
                if (currentDirector == null) return null;
                var trackAsset = GetTrackAsset(instance);
                while (trackAsset != null)
                {
                    foreach (var playableBinding in trackAsset.outputs)
                    {
                        var o = currentDirector.GetGenericBinding(trackAsset) as UnityEngine.Object;
                        if (o != null) return o;
                    }
                    trackAsset = trackAsset.parent as TrackAsset;
                }
                return null;
            }
            public AnimationClip GetAnimationClip(object instance)
            {
                if (instance == null) return null;
                var clip = dg_get_m_Clip(instance);
                if (clip == null) return null;
                return clip.animationClip;
            }
            public void SetAnimationClip(object instance, AnimationClip animClip, string undoName = null)
            {
                if (instance == null) return;
                var clip = dg_get_m_Clip(instance);
                if (clip == null) return;
                var animationPlayableAsset = clip.asset as AnimationPlayableAsset;
                if (animationPlayableAsset == null) return;
                if (undoName != null)
                    Undo.RecordObject(animationPlayableAsset, undoName);
                animationPlayableAsset.clip = animClip;
            }

            public bool IsArmedForRecord(object instance)
            {
                var currentDirector = uTimelineState.GetCurrentDirector(GetTimelineState(instance));
                if (currentDirector == null) return false;
                var state = GetTimelineState(instance);
                if (state == null) return false;
                var trackAsset = GetTrackAsset(instance);
                if (trackAsset == null) return false;
                return uTimelineState.IsArmedForRecord(state, trackAsset);
            }
        }
        public class UTrackAsset
        {
            protected Func<bool> dg_get_locked;

            public virtual bool GetLocked(object instance)
            {
                if (instance == null) return false;
                if (dg_get_locked == null || dg_get_locked.Target != instance)
                    dg_get_locked = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("locked", BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod(true));
                return dg_get_locked();
            }
        }
        public class UAnimationTrack
        {

        }
        public class UAnimationPlayableAsset
        {
            private Func<bool> dg_get_hasRootTransforms;

            private UAnimationUtility uAnimationUtility;

            public UAnimationPlayableAsset()
            {
                uAnimationUtility = new UAnimationUtility();
            }

            public virtual bool GetHasRootTransforms(AnimationPlayableAsset instance)
            {
                if (instance == null) return false;
                return uAnimationUtility.HasRootCurves(instance.clip);
            }
        }
        public class UTimelineTreeViewGUI
        {
            public Func<List<TrackAsset>> dg_get_selection;

            public List<TrackAsset> GetSelection(object instance)
            {
                if (instance == null) return null;
                if (dg_get_selection == null || dg_get_selection.Target != instance)
                    dg_get_selection = (Func<List<TrackAsset>>)Delegate.CreateDelegate(typeof(Func<List<TrackAsset>>), instance, instance.GetType().GetProperty("selection").GetGetMethod());
                return dg_get_selection();
            }
        }
        public class UTimelineAnimationUtilities
        {
            private MethodInfo mi_CreateTimeController;

            public UTimelineAnimationUtilities(Assembly asmTimelineEditor, Assembly asmTimelineEngine)
            {
                var timelineAnimationUtilitiesType = asmTimelineEditor.GetType("UnityEditor.Timeline.TimelineAnimationUtilities");
                var methods = timelineAnimationUtilitiesType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var mi in methods)
                {
                    if (mi.Name != "CreateTimeController") continue;
                    var parameters = mi.GetParameters();
                    if (parameters.Length != 2) continue;
                    if (parameters[0].Name == "state" &&
                        parameters[1].Name == "clip")
                    {
                        mi_CreateTimeController = mi;
                        break;
                    }
                }
                if (mi_CreateTimeController == null)
                {   //Timeline 1.4.0
                    foreach (var mi in methods)
                    {
                        if (mi.Name != "CreateTimeController") continue;
                        var parameters = mi.GetParameters();
                        if (parameters.Length != 1) continue;
                        if (parameters[0].Name == "clip")
                        {
                            mi_CreateTimeController = mi;
                            break;
                        }
                    }
                }
                Assert.IsNotNull(mi_CreateTimeController);
            }

            public object CreateTimeController(object timelineState, TimelineClip clip)
            {
                if (mi_CreateTimeController.GetParameters().Length == 2)
                    return mi_CreateTimeController.Invoke(null, new object[] { timelineState, clip });
                else
                    return mi_CreateTimeController.Invoke(null, new object[] { clip });    //Timeline 1.4.0
            }
        }

        public EditorWindow instance
        {
            get { return dg_get_instance(); }
        }

        public object state
        {
            get
            {
                if (instance == null) return null;
                if (dg_get_state == null || dg_get_state.Target != (object)instance)
                    dg_get_state = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), instance, instance.GetType().GetProperty("state").GetGetMethod());
                return dg_get_state();
            }
        }

        public PlayableDirector GetCurrentDirector()
        {
            return uTimelineState.GetCurrentDirector(state);
        }

        public bool GetRecording()
        {
            return uTimelineState.GetRecording(state);
        }
        public void SetRecording(bool enable)
        {
            uTimelineState.SetRecording(state, enable);
        }

        public bool GetPreviewMode()
        {
            return uTimelineState.GetPreviewMode(state);
        }
        public void SetPreviewMode(bool enable)
        {
            uTimelineState.SetPreviewMode(state, enable);
        }

        public void Close()
        {
            if (instance != null)
                instance.Close();
        }

        public TrackAsset GetSelectionTrack()
        {
            if (instance == null) return null;
            if (dg_get_treeView == null || dg_get_treeView.Target != (object)instance)
                dg_get_treeView = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), instance, instance.GetType().GetProperty("treeView").GetGetMethod());
            var treeView = dg_get_treeView();
            if (treeView == null) return null;
            var selection = uTimelineTreeViewGUI.GetSelection(treeView);
            if (selection == null || selection.Count <= 0) return null;
            return selection[0];
        }

        public void OnCurveModified(AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType type)
        {
            if (instance == null) return;
            if (dg_OnCurveModified == null || dg_OnCurveModified.Target != (object)instance)
            {
                var mi_OnCurveModified = instance.GetType().GetMethod("OnCurveModified", BindingFlags.Instance | BindingFlags.NonPublic);
                dg_OnCurveModified = (Action<AnimationClip, EditorCurveBinding, AnimationUtility.CurveModifiedType>)Delegate.CreateDelegate(typeof(Action<AnimationClip, EditorCurveBinding, AnimationUtility.CurveModifiedType>), instance, mi_OnCurveModified);
            }
            dg_OnCurveModified(clip, binding, type);
        }

        protected void GetTimelineAssembly(out Assembly asmTimelineEditor, out Assembly asmTimelineEngine)
        {
#if UNITY_2019_1_OR_NEWER
            asmTimelineEditor = typeof(UnityEditor.Timeline.TimelineEditor).Assembly;
            asmTimelineEngine = typeof(TrackAsset).Assembly;
#else
            {
                var pathEditor = Path.GetDirectoryName(InternalEditorUtility.GetEditorAssemblyPath());
                pathEditor = pathEditor.Replace("Managed", "UnityExtensions/Unity/Timeline/Editor/UnityEditor.Timeline.dll");
                asmTimelineEditor = Assembly.LoadFrom(pathEditor);
            }
            {
                var pathEditor = Path.GetDirectoryName(InternalEditorUtility.GetEditorAssemblyPath());
                pathEditor = pathEditor.Replace("Managed", "UnityExtensions/Unity/Timeline/RuntimeEditor/UnityEngine.Timeline.dll");
                asmTimelineEngine = Assembly.LoadFrom(pathEditor);
            }
#endif
        }
    }
#endif
}
