#if !UNITY_2019_1_OR_NEWER
#define VERYANIMATION_TIMELINE
#endif

using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.IMGUI.Controls;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

#if VERYANIMATION_TIMELINE
using UnityEngine.Playables;
using UnityEngine.Timeline;
#endif

namespace VeryAnimation
{
    public class UAnimationWindow //2017.4 or later
    {
        protected VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
        protected VeryAnimation va { get { return VeryAnimation.instance; } }

        protected Func<object, IList> dg_get_s_AnimationWindows;
        protected Func<object, object> dg_get_m_AnimEditor;
        protected Func<object, bool> dg_get_m_Locked;
        protected Action<object, bool> dg_set_m_Locked;
        protected MethodInfo mi_OnSelectionChange;
        protected MethodInfo mi_EditSequencerClip;

        protected class UAnimEditor
        {
            protected PropertyInfo pi_selection;
            protected PropertyInfo pi_selectedItem;
            private PropertyInfo pi_triggerFraming;
            private MethodInfo mi_SwitchBetweenCurvesAndDopesheet;
            private MethodInfo mi_UpdateSelectedKeysToCurveEditor;
            private Func<object, object> dg_get_m_State;
            private Func<object> dg_get_selection;
            private Func<object> dg_get_selectedItem;
            private Func<object> dg_get_curveEditor;

            public UAnimEditor(Assembly asmUnityEditor)
            {
                var animEditorType = asmUnityEditor.GetType("UnityEditor.AnimEditor");
                pi_selection = animEditorType.GetProperty("selection");
                pi_selectedItem = animEditorType.GetProperty("selectedItem");
                Assert.IsNotNull(pi_triggerFraming = animEditorType.GetProperty("triggerFraming", BindingFlags.NonPublic | BindingFlags.Instance));
                Assert.IsNotNull(mi_SwitchBetweenCurvesAndDopesheet = animEditorType.GetMethod("SwitchBetweenCurvesAndDopesheet", BindingFlags.NonPublic | BindingFlags.Instance));
                Assert.IsNotNull(mi_UpdateSelectedKeysToCurveEditor = animEditorType.GetMethod("UpdateSelectedKeysToCurveEditor", BindingFlags.NonPublic | BindingFlags.Instance));
                Assert.IsNotNull(dg_get_m_State = EditorCommon.CreateGetFieldDelegate<object>(animEditorType.GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance)));
            }

            public object GetAnimationWindowState(object instance)
            {
                if (instance == null) return null;
                return dg_get_m_State(instance);
            }

            public object GetSelection(object instance)
            {
                if (instance == null) return null;
                if (dg_get_selection == null || dg_get_selection.Target != instance)
                    dg_get_selection = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), instance, pi_selection.GetGetMethod());
                return dg_get_selection();
            }
            public object GetSelectedItem(object instance)
            {
                if (instance == null) return null;
                if (dg_get_selectedItem == null || dg_get_selectedItem.Target != instance)
                    dg_get_selectedItem = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), instance, pi_selectedItem.GetGetMethod());
                return dg_get_selectedItem();
            }
            public void SetTriggerFraming(object instance)
            {
                if (instance == null) return;
                pi_triggerFraming.SetValue(instance, true, null);
            }

            public void SwitchBetweenCurvesAndDopesheet(object instance)
            {
                if (instance == null) return;
                mi_SwitchBetweenCurvesAndDopesheet.Invoke(instance, null);
            }

            public void UpdateSelectedKeysToCurveEditor(object instance)
            {
                if (instance == null) return;
                mi_UpdateSelectedKeysToCurveEditor.Invoke(instance, null);
            }

            public object GetCurveEditor(object instance)
            {
                if (instance == null) return null;
                if (dg_get_curveEditor == null || dg_get_curveEditor.Target != instance)
                    dg_get_curveEditor = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), instance, instance.GetType().GetProperty("curveEditor", BindingFlags.Instance | BindingFlags.NonPublic).GetGetMethod(true));
                return dg_get_curveEditor();
            }
        }
        protected class UCurveEditor
        {
            private Func<bool> dg_get_hasSelection;
            private Action dg_ClearSelection;

            public UCurveEditor(Assembly asmUnityEditor)
            {
            }

            public bool HasSelection(object instance)
            {
                if (instance == null) return false;
                if (dg_get_hasSelection == null || dg_get_hasSelection.Target != instance)
                    dg_get_hasSelection = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("hasSelection", BindingFlags.Public | BindingFlags.Instance).GetGetMethod());
                return dg_get_hasSelection();
            }
            public void ClearSelection(object instance)
            {
                if (instance == null) return;
                if (dg_ClearSelection == null || dg_ClearSelection.Target != instance)
                    dg_ClearSelection = (Action)Delegate.CreateDelegate(typeof(Action), instance, instance.GetType().GetMethod("ClearSelection", BindingFlags.NonPublic | BindingFlags.Instance));
                dg_ClearSelection();
            }
        }
        protected class UAnimationWindowState
        {
            protected Type animationWindowStateType;
            protected PropertyInfo pi_refresh;
            protected MethodInfo mi_ForceRefresh;
            protected MethodInfo mi_CurveWasModified;
            protected MethodInfo mi_SelectKey;
            protected MethodInfo mi_ClearKeySelections;
            protected MethodInfo mi_ClearHierarchySelection;
            protected MethodInfo mi_SelectHierarchyItem;
            protected MethodInfo mi_UnSelectHierarchyItem;
            protected MethodInfo mi_SnapToFrame;
            protected MethodInfo mi_TimeToFrameRound;
            protected MethodInfo mi_StartRecording;
            protected MethodInfo mi_StopRecording;
            protected MethodInfo mi_StartPlayback;
            protected MethodInfo mi_StopPlayback;
            protected MethodInfo mi_StartPreview;
            protected MethodInfo mi_StopPreview;
            protected Func<object, bool> dg_get_showCurveEditor;
            protected Func<object, TreeViewState> dg_get_hierarchyState;
            protected Func<object, object> dg_get_hierarchyData;
            protected Func<object, bool> dg_get_linkedWithSequencer;
            protected Func<object, IList> dg_get_m_ActiveCurvesCache;
            protected Action<object, IList> dg_set_m_ActiveCurvesCache;
            protected Func<object, IList> dg_get_m_dopelinesCache;
            protected Action<object, IList> dg_set_m_dopelinesCache;
            protected Action<object, EditorCurveBinding?> dg_set_m_lastAddedCurveBinding;
            protected Func<object> dg_get_controlInterface;
            protected Func<GameObject> dg_get_activeRootGameObject;
            protected Func<Component> dg_get_activeAnimationPlayer;
            protected Func<bool> dg_get_playing;
            protected Func<bool> dg_get_recording;
            protected Func<bool> dg_get_previewing;
            protected Func<bool> dg_get_canPreview;
            protected Func<int> dg_get_currentFrame;
            protected Action<int> dg_set_currentFrame;
            protected Func<float> dg_get_currentTime;
            protected Action<float> dg_set_currentTime;
            protected Func<IList> dg_get_allCurves;
            protected Func<IList> dg_get_activeCurves;
            protected Func<IList> dg_get_dopelines;
            protected Func<IEnumerable> dg_get_selectedKeyHashes;
            protected Action<AnimationClip, EditorCurveBinding, AnimationUtility.CurveModifiedType> dg_CurveWasModified;
            protected Func<float, float, float> dg_SnapToFrame;
            protected Func<float, int> dg_TimeToFrameRound;


            public UAnimationWindowState(Assembly asmUnityEditor)
            {
                Assert.IsNotNull(animationWindowStateType = asmUnityEditor.GetType("UnityEditorInternal.AnimationWindowState"));
                Assert.IsNotNull(pi_refresh = animationWindowStateType.GetProperty("refresh"));
                Assert.IsNotNull(mi_ForceRefresh = animationWindowStateType.GetMethod("ForceRefresh"));
                Assert.IsNotNull(mi_CurveWasModified = animationWindowStateType.GetMethod("CurveWasModified", BindingFlags.Instance | BindingFlags.NonPublic));
                Assert.IsNotNull(mi_SelectKey = animationWindowStateType.GetMethod("SelectKey"));
                Assert.IsNotNull(mi_ClearKeySelections = animationWindowStateType.GetMethod("ClearKeySelections"));
                Assert.IsNotNull(mi_ClearHierarchySelection = animationWindowStateType.GetMethod("ClearHierarchySelection"));
                Assert.IsNotNull(mi_SelectHierarchyItem = animationWindowStateType.GetMethod("SelectHierarchyItem", new Type[] { typeof(int), typeof(bool), typeof(bool) }));
                Assert.IsNotNull(mi_UnSelectHierarchyItem = animationWindowStateType.GetMethod("UnSelectHierarchyItem", new Type[] { typeof(int) }));
                Assert.IsNotNull(mi_SnapToFrame = animationWindowStateType.GetMethod("SnapToFrame", new Type[] { typeof(float), typeof(float) }));
                Assert.IsNotNull(mi_TimeToFrameRound = animationWindowStateType.GetMethod("TimeToFrameRound"));
                mi_StartRecording = animationWindowStateType.GetMethod("StartRecording");
                mi_StopRecording = animationWindowStateType.GetMethod("StopRecording");
                mi_StartPlayback = animationWindowStateType.GetMethod("StartPlayback");
                mi_StopPlayback = animationWindowStateType.GetMethod("StopPlayback");
                mi_StartPreview = animationWindowStateType.GetMethod("StartPreview");
                mi_StopPreview = animationWindowStateType.GetMethod("StopPreview");
                Assert.IsNotNull(dg_get_showCurveEditor = EditorCommon.CreateGetFieldDelegate<bool>(animationWindowStateType.GetField("showCurveEditor")));
                Assert.IsNotNull(dg_get_hierarchyState = EditorCommon.CreateGetFieldDelegate<TreeViewState>(animationWindowStateType.GetField("hierarchyState")));
                Assert.IsNotNull(dg_get_hierarchyData = EditorCommon.CreateGetFieldDelegate<object>(animationWindowStateType.GetField("hierarchyData")));
                Assert.IsNotNull(dg_get_linkedWithSequencer = EditorCommon.CreateGetFieldDelegate<bool>(animationWindowStateType.GetField("linkedWithSequencer")));
                Assert.IsNotNull(dg_get_m_ActiveCurvesCache = EditorCommon.CreateGetFieldDelegate<IList>(animationWindowStateType.GetField("m_ActiveCurvesCache", BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.IsNotNull(dg_set_m_ActiveCurvesCache = EditorCommon.CreateSetFieldDelegate<IList>(animationWindowStateType.GetField("m_ActiveCurvesCache", BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.IsNotNull(dg_get_m_dopelinesCache = EditorCommon.CreateGetFieldDelegate<IList>(animationWindowStateType.GetField("m_dopelinesCache", BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.IsNotNull(dg_set_m_dopelinesCache = EditorCommon.CreateSetFieldDelegate<IList>(animationWindowStateType.GetField("m_dopelinesCache", BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.IsNotNull(dg_set_m_lastAddedCurveBinding = EditorCommon.CreateSetFieldDelegate<EditorCurveBinding?>(animationWindowStateType.GetField("m_lastAddedCurveBinding", BindingFlags.NonPublic | BindingFlags.Instance)));
            }

            public enum RefreshType
            {
                None,
                CurvesOnly,
                Everything,
            }

            public TreeViewState GetHierarchyState(object instance)
            {
                if (instance == null) return null;
                return dg_get_hierarchyState(instance);
            }
            public bool GetShowCurveEditor(object instance)
            {
                if (instance == null) return false;
                return dg_get_showCurveEditor(instance);
            }
            public object GetHierarchyData(object instance)
            {
                if (instance == null) return null;
                return dg_get_hierarchyData(instance);
            }
            public bool GetLinkedWithSequencer(object instance)
            {
                if (instance == null) return false;
                return dg_get_linkedWithSequencer(instance);
            }
            public object GetControlInterface(object instance)
            {
                if (instance == null) return null;
                if (dg_get_controlInterface == null || dg_get_controlInterface.Target != instance)
                    dg_get_controlInterface = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), instance, instance.GetType().GetProperty("controlInterface").GetGetMethod());
                return dg_get_controlInterface();
            }
            public GameObject GetActiveRootGameObject(object instance)
            {
                if (instance == null) return null;
                if (dg_get_activeRootGameObject == null || dg_get_activeRootGameObject.Target != instance)
                    dg_get_activeRootGameObject = (Func<GameObject>)Delegate.CreateDelegate(typeof(Func<GameObject>), instance, instance.GetType().GetProperty("activeRootGameObject").GetGetMethod());
                return dg_get_activeRootGameObject();
            }
            public Component GetActiveAnimationPlayer(object instance)
            {
                if (instance == null) return null;
                if (dg_get_activeAnimationPlayer == null || dg_get_activeAnimationPlayer.Target != instance)
                    dg_get_activeAnimationPlayer = (Func<Component>)Delegate.CreateDelegate(typeof(Func<Component>), instance, instance.GetType().GetProperty("activeAnimationPlayer").GetGetMethod());
                return dg_get_activeAnimationPlayer();
            }
            public RefreshType GetRefresh(object instance)
            {
                if (instance == null) return RefreshType.None;
                return (RefreshType)pi_refresh.GetValue(instance, null);
            }
            public bool GetPlaying(object instance)
            {
                if (instance == null) return false;
                if (dg_get_playing == null || dg_get_playing.Target != instance)
                    dg_get_playing = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("playing").GetGetMethod());
                return dg_get_playing();
            }
            public bool GetRecording(object instance)
            {
                if (instance == null) return false;
                if (dg_get_recording == null || dg_get_recording.Target != instance)
                    dg_get_recording = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("recording").GetGetMethod());
                return dg_get_recording();
            }
            public int GetCurrentFrame(object instance)
            {
                if (instance == null) return 0;
                if (dg_get_currentFrame == null || dg_get_currentFrame.Target != instance)
                    dg_get_currentFrame = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), instance, instance.GetType().GetProperty("currentFrame").GetGetMethod());
                return dg_get_currentFrame();
            }
            public void SetCurrentFrame(object instance, int value)
            {
                if (instance == null) return;
                if (dg_set_currentFrame == null || dg_set_currentFrame.Target != instance)
                    dg_set_currentFrame = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), instance, instance.GetType().GetProperty("currentFrame").GetSetMethod());
                dg_set_currentFrame(value);
            }
            public float GetCurrentTime(object instance)
            {
                if (instance == null) return 0f;
                if (dg_get_currentTime == null || dg_get_currentTime.Target != instance)
                    dg_get_currentTime = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), instance, instance.GetType().GetProperty("currentTime").GetGetMethod());
                return dg_get_currentTime();
            }
            public void SetCurrentTime(object instance, float value)
            {
                if (instance == null) return;
                if (dg_set_currentTime == null || dg_set_currentTime.Target != instance)
                    dg_set_currentTime = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), instance, instance.GetType().GetProperty("currentTime").GetSetMethod());
                dg_set_currentTime(value);
            }
            public IList GetAllCurves(object instance)
            {
                if (instance == null) return null;
                if (dg_get_allCurves == null || dg_get_allCurves.Target != instance)
                    dg_get_allCurves = (Func<IList>)Delegate.CreateDelegate(typeof(Func<IList>), instance, instance.GetType().GetProperty("allCurves").GetGetMethod());
                return dg_get_allCurves();
            }
            public IList GetActiveCurves(object instance)
            {
                if (instance == null) return null;
                //Cache Hit
                var list = dg_get_m_ActiveCurvesCache.Invoke(instance);
                if (list != null)
                    return list;
                //Cache Miss
                if (dg_get_activeCurves == null || dg_get_activeCurves.Target != instance)
                    dg_get_activeCurves = (Func<IList>)Delegate.CreateDelegate(typeof(Func<IList>), instance, instance.GetType().GetProperty("activeCurves").GetGetMethod());
                list = dg_get_activeCurves();
                dg_set_m_ActiveCurvesCache(instance, null);  //Cache Clear
                return list;
            }
            public IList GetDopelines(object instance)
            {
                if (instance == null) return null;
                //Cache Hit
                var list = dg_get_m_dopelinesCache(instance);
                if (list != null)
                    return list;
                //Cache Miss
                if (dg_get_dopelines == null || dg_get_dopelines.Target != instance)
                    dg_get_dopelines = (Func<IList>)Delegate.CreateDelegate(typeof(Func<IList>), instance, instance.GetType().GetProperty("dopelines").GetGetMethod());
                list = dg_get_dopelines();
                dg_set_m_dopelinesCache(instance, null);  //Cache Clear
                return list;
            }
            public virtual void ClearCache(object instance)
            {
                if (instance == null) return;
                dg_set_m_ActiveCurvesCache(instance, null);  //Cache Clear
                dg_set_m_dopelinesCache(instance, null);  //Cache Clear
            }
            public void ClearLastAddedCurveBinding(object instance)
            {
                if (instance == null) return;
                dg_set_m_lastAddedCurveBinding(instance, null);
            }

            public IEnumerable GetSelectedKeyHashes(object instance)
            {
                if (instance == null) return null;
                if (dg_get_selectedKeyHashes == null || dg_get_selectedKeyHashes.Target != instance)
                    dg_get_selectedKeyHashes = (Func<IEnumerable>)Delegate.CreateDelegate(typeof(Func<IEnumerable>), instance, instance.GetType().GetProperty("selectedKeyHashes", BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod(true));
                return dg_get_selectedKeyHashes();
            }

            public void ForceRefresh(object instance)
            {
                if (instance == null) return;
                mi_ForceRefresh.Invoke(instance, null);
            }
            public void CurveWasModified(object instance, AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType type)
            {
                if (instance == null) return;
                if (dg_CurveWasModified == null || dg_CurveWasModified.Target != instance)
                    dg_CurveWasModified = (Action<AnimationClip, EditorCurveBinding, AnimationUtility.CurveModifiedType>)Delegate.CreateDelegate(typeof(Action<AnimationClip, EditorCurveBinding, AnimationUtility.CurveModifiedType>), instance, mi_CurveWasModified);
                dg_CurveWasModified(clip, binding, type);
            }
            public void SelectKey(object instance, object keyframe)
            {
                if (instance == null) return;
                mi_SelectKey.Invoke(instance, new object[] { keyframe });
            }
            public void ClearKeySelections(object instance)
            {
                if (instance == null) return;
                mi_ClearKeySelections.Invoke(instance, null);
            }
            public void ClearHierarchySelection(object instance)
            {
                if (instance == null) return;
                mi_ClearHierarchySelection.Invoke(instance, null);
            }
            public void SelectHierarchyItem(object instance, int hierarchyNodeID, bool additive, bool triggerSceneSelectionSync)
            {
                if (instance == null) return;
                mi_SelectHierarchyItem.Invoke(instance, new object[] { hierarchyNodeID, additive, triggerSceneSelectionSync });
            }
            public void UnSelectHierarchyItem(object instance, int hierarchyNodeID)
            {
                if (instance == null) return;
                mi_UnSelectHierarchyItem.Invoke(instance, new object[] { hierarchyNodeID });
            }
            public float SnapToFrame(object instance, float time, float fps)
            {
                if (instance == null) return 0f;
                if (dg_SnapToFrame == null || dg_SnapToFrame.Target != instance)
                    dg_SnapToFrame = (Func<float, float, float>)Delegate.CreateDelegate(typeof(Func<float, float, float>), instance, mi_SnapToFrame);
                return dg_SnapToFrame(time, fps);
            }
            public int TimeToFrameRound(object instance, float time)
            {
                if (instance == null) return 0;
                if (dg_TimeToFrameRound == null || dg_TimeToFrameRound.Target != instance)
                    dg_TimeToFrameRound = (Func<float, int>)Delegate.CreateDelegate(typeof(Func<float, int>), instance, mi_TimeToFrameRound);
                return dg_TimeToFrameRound(time);
            }

            public virtual bool StartRecording(object instance)
            {
                if (instance == null) return false;
                try
                {
                    mi_StartRecording.Invoke(instance, null);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
                return true;
            }
            public virtual bool StopRecording(object instance)
            {
                if (instance == null) return false;
                try
                {
                    mi_StopRecording.Invoke(instance, null);
                    mi_StopPreview.Invoke(instance, null);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
                return true;
            }
            public virtual bool StartPlayback(object instance)
            {
                if (instance == null) return false;
                try
                {
                    mi_StartPlayback.Invoke(instance, null);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
                return true;
            }
            public virtual bool StopPlayback(object instance)
            {
                if (instance == null) return false;
                try
                {
                    mi_StopPlayback.Invoke(instance, null);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
                return true;
            }
            public virtual bool StartPreview(object instance)
            {
                if (instance == null) return false;
                try
                {
                    mi_StartPreview.Invoke(instance, null);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
                return true;
            }
            public virtual bool StopPreview(object instance)
            {
                if (instance == null) return false;
                try
                {
                    mi_StopPreview.Invoke(instance, null);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
                return true;
            }
            public bool GetPreviewing(object instance)
            {
                if (instance == null) return false;
                if (dg_get_previewing == null || dg_get_previewing.Target != instance)
                    dg_get_previewing = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("previewing").GetGetMethod());
                return dg_get_previewing();
            }
            public bool GetCanPreview(object instance)
            {
                if (instance == null) return false;
                if (dg_get_canPreview == null || dg_get_canPreview.Target != instance)
                    dg_get_canPreview = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("canPreview").GetGetMethod());
                return dg_get_canPreview();
            }
        }
        protected class UAnimationWindowControl
        {
            protected MethodInfo mi_GoToNextKeyframe;
            protected MethodInfo mi_GoToPreviousKeyframe;
            protected MethodInfo mi_GoToFirstKeyframe;
            protected MethodInfo mi_GoToLastKeyframe;
            protected Func<bool> dg_get_canRecord;
            protected Action dg_ResampleAnimation;
            protected FieldInfo fi_m_Time;

            public UAnimationWindowControl(Assembly asmUnityEditor)
            {
                var iAnimationWindowControlType = asmUnityEditor.GetType("UnityEditorInternal.IAnimationWindowControl");
                mi_GoToNextKeyframe = iAnimationWindowControlType.GetMethod("GoToNextKeyframe", new Type[] { });
                mi_GoToPreviousKeyframe = iAnimationWindowControlType.GetMethod("GoToPreviousKeyframe", new Type[] { });
                mi_GoToFirstKeyframe = iAnimationWindowControlType.GetMethod("GoToFirstKeyframe", new Type[] { });
                mi_GoToLastKeyframe = iAnimationWindowControlType.GetMethod("GoToLastKeyframe", new Type[] { });

                var animationWindowControlType = asmUnityEditor.GetType("UnityEditorInternal.AnimationWindowControl");
                Assert.IsNotNull(fi_m_Time = animationWindowControlType.GetField("m_Time", BindingFlags.NonPublic | BindingFlags.Instance));
            }

            public virtual bool GetCanRecord(object instance)
            {
                if (instance == null) return false;
                if (dg_get_canRecord == null || dg_get_canRecord.Target != instance)
                    dg_get_canRecord = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("canRecord").GetGetMethod());
                return dg_get_canRecord();
            }
            public virtual void ResampleAnimation(object instance)
            {
                if (instance == null) return;
                if (dg_ResampleAnimation == null || dg_ResampleAnimation.Target != instance)
                    dg_ResampleAnimation = (Action)Delegate.CreateDelegate(typeof(Action), instance, instance.GetType().GetMethod("ResampleAnimation"));
                dg_ResampleAnimation();
            }
            public void GoToNextKeyframe(object instance)
            {
                if (instance == null) return;
                mi_GoToNextKeyframe.Invoke(instance, null);
            }
            public void GoToPreviousKeyframe(object instance)
            {
                if (instance == null) return;
                mi_GoToPreviousKeyframe.Invoke(instance, null);
            }
            public void GoToFirstKeyframe(object instance)
            {
                if (instance == null) return;
                mi_GoToFirstKeyframe.Invoke(instance, null);
            }
            public void GoToLastKeyframe(object instance)
            {
                if (instance == null) return;
                mi_GoToLastKeyframe.Invoke(instance, null);
            }

            public void SetTime(object instance, object time)
            {
                if (instance == null) return;
                fi_m_Time.SetValue(instance, time);
            }
        }
        protected class UAnimationKeyTime
        {
            protected MethodInfo mi_Time;

            public UAnimationKeyTime(Assembly asmUnityEditor)
            {
                var animationKeyTimeType = asmUnityEditor.GetType("UnityEditorInternal.AnimationKeyTime");
                mi_Time = animationKeyTimeType.GetMethod("Time", BindingFlags.Public | BindingFlags.Static);
            }
            public object Time(float time, float frameRate)
            {
                return mi_Time.Invoke(null, new object[] { time, frameRate });
            }
        }
        protected class UAnimationWindowCurve
        {
            public Type animationWindowCurveType { get; private set; }
            private Func<object, EditorCurveBinding> dg_get_m_Binding;
            private PropertyInfo pi_selectionBinding;
            private MethodInfo mi_GetHashCode;
            private MethodInfo mi_CompareTo;
            private MethodInfo mi_FindKeyAtTime;

            public UAnimationWindowCurve(Assembly asmUnityEditor)
            {
                animationWindowCurveType = asmUnityEditor.GetType("UnityEditorInternal.AnimationWindowCurve");

                Assert.IsNotNull(dg_get_m_Binding = EditorCommon.CreateGetFieldDelegate<EditorCurveBinding>(animationWindowCurveType.GetField("m_Binding", BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.IsNotNull(pi_selectionBinding = animationWindowCurveType.GetProperty("selectionBinding"));
                Assert.IsNotNull(mi_GetHashCode = animationWindowCurveType.GetMethod("GetHashCode"));
                Assert.IsNotNull(mi_CompareTo = animationWindowCurveType.GetMethod("CompareTo"));
                Assert.IsNotNull(mi_FindKeyAtTime = animationWindowCurveType.GetMethod("FindKeyAtTime"));
            }

            public EditorCurveBinding GetBinding(object instance)
            {
                if (instance == null) return new EditorCurveBinding();
                return dg_get_m_Binding(instance);
            }
            public int CompareTo(object instance, object other)
            {
                if (instance == null) return 0;
                return (int)mi_CompareTo.Invoke(instance, new object[] { other });
            }
            public void SetSelectionBinding(object instance, object selectionItem)
            {
                if (instance == null) return;
                pi_selectionBinding.SetValue(instance, selectionItem, null);
            }
            public int GetHashCode(object instance)
            {
                if (instance == null) return -1;
                return (int)mi_GetHashCode.Invoke(instance, null);
            }
            public object FindKeyAtTime(object instance, object keyTime)
            {
                if (instance == null) return null;
                return mi_FindKeyAtTime.Invoke(instance, new object[] { keyTime });
            }
        }
        protected class UAnimationWindowSelection
        {
            private MethodInfo mi_UpdateClip;
            private Action dg_ClearCache;

            public UAnimationWindowSelection(Assembly asmUnityEditor)
            {
                var animationWindowSelectionType = asmUnityEditor.GetType("UnityEditorInternal.AnimationWindowSelection");
                if (animationWindowSelectionType == null) return;
                Assert.IsNotNull(mi_UpdateClip = animationWindowSelectionType.GetMethod("UpdateClip"));
            }

            public void UpdateClip(object instance, object itemToUpdate, AnimationClip newClip)
            {
                if (instance == null) return;

                mi_UpdateClip.Invoke(instance, new object[] { itemToUpdate, newClip });
            }
            public void ClearCurvesCache(object instance)
            {
                if (instance == null) return;
                if (dg_ClearCache == null || dg_ClearCache.Target != instance)
                    dg_ClearCache = (Action)Delegate.CreateDelegate(typeof(Action), instance, instance.GetType().GetMethod("ClearCache"));
                dg_ClearCache();
            }
        }
        protected class UAnimationWindowSelectionItem
        {
            private Func<GameObject> dg_get_gameObject;
            private Action<GameObject> dg_set_gameObject;
            private Func<AnimationClip> dg_get_animationClip;
            private Func<IList> dg_get_curves;
            private Action<object, IList> dg_set_m_CurvesCache;
            private Func<object, IList> dg_get_m_CurvesCache;
            private Action dg_ClearCache;
            private Func<EditorCurveBinding, Type> dg_GetEditorCurveValueType;

            public UAnimationWindowSelectionItem(Assembly asmUnityEditor)
            {
                var animationWindowSelectionItemType = asmUnityEditor.GetType("UnityEditorInternal.AnimationWindowSelectionItem");
                dg_set_m_CurvesCache = EditorCommon.CreateSetFieldDelegate<IList>(animationWindowSelectionItemType.GetField("m_CurvesCache", BindingFlags.NonPublic | BindingFlags.Instance));
                dg_get_m_CurvesCache = EditorCommon.CreateGetFieldDelegate<IList>(animationWindowSelectionItemType.GetField("m_CurvesCache", BindingFlags.NonPublic | BindingFlags.Instance));
            }

            public GameObject GetGameObject(object instance)
            {
                if (instance == null) return null;
                if (dg_get_gameObject == null || dg_get_gameObject.Target != instance)
                    dg_get_gameObject = (Func<GameObject>)Delegate.CreateDelegate(typeof(Func<GameObject>), instance, instance.GetType().GetProperty("gameObject").GetGetMethod());
                return dg_get_gameObject();
            }
            public void SetGameObject(object instance, GameObject gameObject)
            {
                if (instance == null) return;
                if (dg_set_gameObject == null || dg_set_gameObject.Target != instance)
                    dg_set_gameObject = (Action<GameObject>)Delegate.CreateDelegate(typeof(Action<GameObject>), instance, instance.GetType().GetProperty("gameObject").GetSetMethod());
                dg_set_gameObject(gameObject);
            }

            public AnimationClip GetAnimationClip(object instance)
            {
                if (instance == null) return null;
                if (dg_get_animationClip == null || dg_get_animationClip.Target != instance)
                    dg_get_animationClip = (Func<AnimationClip>)Delegate.CreateDelegate(typeof(Func<AnimationClip>), instance, instance.GetType().GetProperty("animationClip").GetGetMethod());
                return dg_get_animationClip();
            }

            public virtual IList GetCurves(object instance)
            {
                if (instance == null) return null;
                if (dg_get_curves == null || dg_get_curves.Target != instance)
                    dg_get_curves = (Func<IList>)Delegate.CreateDelegate(typeof(Func<IList>), instance, instance.GetType().GetProperty("curves").GetGetMethod());
                return dg_get_curves();
            }
            public virtual void SetCurvesCache(object instance, IList curves)
            {
                if (instance == null) return;
                dg_set_m_CurvesCache(instance, curves);
            }
            public virtual IList GetCurvesCache(object instance)
            {
                if (instance == null) return null;
                return dg_get_m_CurvesCache(instance);
            }
            public virtual void ClearCurvesCache(object instance)
            {
                if (instance == null) return;
                if (dg_ClearCache == null || dg_ClearCache.Target != instance)
                    dg_ClearCache = (Action)Delegate.CreateDelegate(typeof(Action), instance, instance.GetType().GetMethod("ClearCache"));
                dg_ClearCache();
            }
            public virtual Type GetEditorCurveValueType(object instance, EditorCurveBinding binding)
            {
                if (instance == null) return null;
                if (dg_GetEditorCurveValueType == null || dg_GetEditorCurveValueType.Target != instance)
                    dg_GetEditorCurveValueType = (Func<EditorCurveBinding, Type>)Delegate.CreateDelegate(typeof(Func<EditorCurveBinding, Type>), instance, instance.GetType().GetMethod("GetEditorCurveValueType"));
                return dg_GetEditorCurveValueType(binding);
            }
        }
        protected class UAnimationWindowHierarchyDataSource
        {
            private MethodInfo mi_FindItem;
            private MethodInfo mi_UpdateData;

            public UAnimationWindowHierarchyDataSource(Assembly asmUnityEditor)
            {
                var animationWindowHierarchyDataSourceType = asmUnityEditor.GetType("UnityEditorInternal.AnimationWindowHierarchyDataSource");
                Assert.IsNotNull(mi_FindItem = animationWindowHierarchyDataSourceType.GetMethod("FindItem", BindingFlags.Public | BindingFlags.Instance));
                Assert.IsNotNull(mi_UpdateData = animationWindowHierarchyDataSourceType.GetMethod("UpdateData", BindingFlags.Public | BindingFlags.Instance));
            }

            public object FindItem(object instance, int id)
            {
                if (instance == null) return null;
                return mi_FindItem.Invoke(instance, new object[] { id });
            }

            public void UpdateData(object instance)
            {
                if (instance == null) return;
                mi_UpdateData.Invoke(instance, null);
            }
        }
        protected class UAnimationWindowHierarchyNode
        {
            private Func<object, IList> dg_get_curves;

            public UAnimationWindowHierarchyNode(Assembly asmUnityEditor)
            {
                var animationWindowHierarchyNodeType = asmUnityEditor.GetType("UnityEditorInternal.AnimationWindowHierarchyNode");
                Assert.IsNotNull(dg_get_curves = EditorCommon.CreateGetFieldDelegate<IList>(animationWindowHierarchyNodeType.GetField("curves", BindingFlags.Public | BindingFlags.Instance)));
            }

            public IList GetCurves(object instance)
            {
                if (instance == null) return null;
                return dg_get_curves(instance);
            }
        }
        protected class UDopeLine
        {
            private PropertyInfo pi_curves;
            private PropertyInfo pi_hierarchyNodeID;
            private Func<object, Type> dg_get_objectType;

            public UDopeLine(Assembly asmUnityEditor)
            {
                var dopeLineType = asmUnityEditor.GetType("UnityEditorInternal.DopeLine");
                Assert.IsNotNull(pi_curves = dopeLineType.GetProperty("curves"));
                Assert.IsNotNull(pi_hierarchyNodeID = dopeLineType.GetProperty("hierarchyNodeID"));
                Assert.IsNotNull(dg_get_objectType = EditorCommon.CreateGetFieldDelegate<Type>(dopeLineType.GetField("objectType")));
            }

            public Type GetObjectType(object instance)
            {
                if (instance == null) return null;
                return dg_get_objectType(instance);
            }
            public IList GetCurves(object instance)
            {
                if (instance == null) return null;
                return (IList)pi_curves.GetValue(instance, null);
            }
            public int GetHierarchyNodeID(object instance)
            {
                if (instance == null) return -1;
                return (int)pi_hierarchyNodeID.GetValue(instance, null);
            }
        }

        protected UEditorWindow uEditorWindow;
        protected UAnimationWindowUtility uAnimationWindowUtility;
        protected UAnimEditor uAnimEditor;
        protected UCurveEditor uCurveEditor;
        protected UAnimationWindowState uAnimationWindowState;
        protected UAnimationWindowControl uAnimationWindowControl;
        protected UAnimationKeyTime uAnimationKeyTime;
        protected UAnimationWindowCurve uAnimationWindowCurve;
        protected UAnimationWindowSelection uAnimationWindowSelection;
        protected UAnimationWindowSelectionItem uAnimationWindowSelectionItem;
        protected UAnimationWindowHierarchyDataSource uAnimationWindowHierarchyDataSource;
        protected UAnimationWindowHierarchyNode uAnimationWindowHierarchyNode;
        protected UDopeLine uDopeLine;
        protected UAnimationMode uAnimationMode;
#if VERYANIMATION_TIMELINE
        public UTimelineWindow uTimelineWindow { get; protected set; }
        protected object animationTimeWindowControlInstance
        {
            get
            {
                var awc = animationWindowControlInstance;
                if (awc != null && awc.GetType() == uTimelineWindow.uTimelineWindowTimeControl.timelineWindowTimeControlType)
                    return awc;
                return null;
            }
        }
#endif

        protected object animEditorInstance
        {
            get
            {
                var aw = instance;
                if (aw == null) return null;
                return dg_get_m_AnimEditor(aw);
            }
        }
        protected object animationWindowStateInstance
        {
            get
            {
                return uAnimEditor.GetAnimationWindowState(animEditorInstance);
            }
        }
        protected object animationWindowControlInstance
        {
            get
            {
                return uAnimationWindowState.GetControlInterface(animationWindowStateInstance);
            }
        }

        protected object selection
        {
            get
            {
                var ae = animEditorInstance;
                var si = uAnimEditor.GetSelection(ae);
                if (si == null)
                {
                    if (!HasFocus())
                    {
                        if (instance != null)
                            instance.Focus();
                    }
                    si = uAnimEditor.GetSelection(ae);
                    if (si == null)
                        return null;
                }
                return si;
            }
        }
        protected object selectedItem
        {
            get
            {
                var ae = animEditorInstance;
                var si = uAnimEditor.GetSelectedItem(ae);
                if (si == null)
                {
                    if (!HasFocus())
                    {
                        if (instance != null)
                            instance.Focus();
                    }
                    si = uAnimEditor.GetSelectedItem(ae);
                    if (si == null)
                        return null;
                }
                return si;
            }
        }

        public UAnimationWindow()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var animationWindowType = asmUnityEditor.GetType("UnityEditor.AnimationWindow");

            Assert.IsNotNull(dg_get_s_AnimationWindows = EditorCommon.CreateGetFieldDelegate<IList>(animationWindowType.GetField("s_AnimationWindows", BindingFlags.NonPublic | BindingFlags.Static)));
            Assert.IsNotNull(dg_get_m_AnimEditor = EditorCommon.CreateGetFieldDelegate<object>(animationWindowType.GetField("m_AnimEditor", BindingFlags.NonPublic | BindingFlags.Instance)));
            {
                var fi_m_Locked = animationWindowType.GetField("m_Locked", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi_m_Locked != null)
                {
                    Assert.IsNotNull(dg_get_m_Locked = EditorCommon.CreateGetFieldDelegate<bool>(fi_m_Locked));
                    Assert.IsNotNull(dg_set_m_Locked = EditorCommon.CreateSetFieldDelegate<bool>(fi_m_Locked));
                }
            }
            mi_OnSelectionChange = animationWindowType.GetMethod("OnSelectionChange");
            mi_EditSequencerClip = animationWindowType.GetMethod("EditSequencerClip");

            uEditorWindow = new UEditorWindow();
            uAnimationWindowUtility = new UAnimationWindowUtility();
            uAnimEditor = new UAnimEditor(asmUnityEditor);
            uCurveEditor = new UCurveEditor(asmUnityEditor);
            uAnimationWindowState = new UAnimationWindowState(asmUnityEditor);
            uAnimationWindowControl = new UAnimationWindowControl(asmUnityEditor);
            uAnimationKeyTime = new UAnimationKeyTime(asmUnityEditor);
            uAnimationWindowCurve = new UAnimationWindowCurve(asmUnityEditor);
            uAnimationWindowSelection = new UAnimationWindowSelection(asmUnityEditor);
            uAnimationWindowSelectionItem = new UAnimationWindowSelectionItem(asmUnityEditor);
            uAnimationWindowHierarchyDataSource = new UAnimationWindowHierarchyDataSource(asmUnityEditor);
            uAnimationWindowHierarchyNode = new UAnimationWindowHierarchyNode(asmUnityEditor);
            uDopeLine = new UDopeLine(asmUnityEditor);
            uAnimationMode = new UAnimationMode();
#if VERYANIMATION_TIMELINE
            uTimelineWindow = new UTimelineWindow();
#endif
        }

        public EditorWindow instance
        {
            get
            {
                EditorWindow result = null;
                {
                    var list = dg_get_s_AnimationWindows(null);
                    if (list.Count > 0)
                        result = list[0] as EditorWindow;
                }
                return result;
            }
        }

        public GameObject GetActiveRootGameObject()
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetLinkedWithSequencer(aws))
            {
#if VERYANIMATION_TIMELINE
                var atwc = animationTimeWindowControlInstance;
                if (atwc != null)
                {
                    var bindingObject = uTimelineWindow.uTimelineWindowTimeControl.GetGenericBinding(atwc);
                    if (bindingObject != null)
                    {
                        if (bindingObject is GameObject)
                        {
                            return bindingObject as GameObject;
                        }
                        else if (bindingObject is Animator)
                        {
                            var animator = bindingObject as Animator;
                            return animator.gameObject;
                        }
                    }
                }
#endif
                return null;
            }
            else
            {
                return uAnimationWindowState.GetActiveRootGameObject(aws);
            }
        }
        public Component GetActiveAnimationPlayer()
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetLinkedWithSequencer(aws))
            {
#if VERYANIMATION_TIMELINE
                var atwc = animationTimeWindowControlInstance;
                if (atwc != null)
                {
                    var bindingObject = uTimelineWindow.uTimelineWindowTimeControl.GetGenericBinding(atwc);
                    if (bindingObject != null)
                    {
                        if (bindingObject is GameObject)
                        {
                            var gameObject = bindingObject as GameObject;
                            return gameObject.GetComponent<Animator>();
                        }
                        else if (bindingObject is Animator)
                        {
                            return bindingObject as Animator;
                        }
                    }
                }
#endif
                return null;
            }
            else
            {
                return uAnimationWindowState.GetActiveAnimationPlayer(aws);
            }
        }

        public virtual AnimationClip GetSelectionAnimationClip()
        {
            if (instance == null) return null;
            return uAnimationWindowSelectionItem.GetAnimationClip(selectedItem);
        }
        public virtual void SetSelectionAnimationClip(AnimationClip animationClip)
        {
            if (instance == null) return;
            if (GetSelectionAnimationClip() == animationClip) return;
            var sl = selection;
            if (sl == null) return;
            var si = selectedItem;
            if (si == null) return;

            var aws = animationWindowStateInstance;
            bool playing = uAnimationWindowState.GetPlaying(aws);
            float currentTime = uAnimationWindowState.GetCurrentTime(aws);
            {
                uAnimationWindowSelection.UpdateClip(sl, si, animationClip);
            }
            uAnimationWindowState.SetCurrentTime(aws, currentTime);
            if (playing)
                uAnimationWindowState.StartPlayback(aws);

            ForceRefresh();
        }

        public void CleanAnimationModeEvents()
        {
#if UNITY_2019_1_OR_NEWER
            //Added to infer that there may be an error due to remaining actions for deleted windows.
            {
                var onStart = uAnimationMode.GetOnAnimationRecordingStart();
                if (onStart != null)
                {
                    bool changed = false;
                    {
                        var delegates = onStart.GetInvocationList();
                        foreach (var del in delegates)
                        {
                            if (del.Target == null)
                            {
                                onStart -= (Action)del;
                                changed = true;
                            }
                            else
                            {
                                var type = del.Target.GetType();
#if UNITY_2020_1_OR_NEWER
                                var fi = type.GetField("m_Panel", BindingFlags.Instance | BindingFlags.NonPublic);
#else
                                var fi = type.GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
#endif
                                if (fi != null)
                                {
                                    var panel = fi.GetValue(del.Target);
                                    if (panel == null)
                                    {
                                        onStart -= (Action)del;
                                        changed = true;
                                    }
                                    else if (panel.GetType().GetProperty("visualTree").GetValue(panel) == null)
                                    {
                                        onStart -= (Action)del;
                                        changed = true;
                                    }
                                }
                            }
                        }
                    }
                    if (changed)
                        uAnimationMode.SetOnAnimationRecordingStart(onStart);
                }
            }
            {
                var onStop = uAnimationMode.GetOnAnimationRecordingStop();
                if (onStop != null)
                {
                    bool changed = false;
                    {
                        var delegates = onStop.GetInvocationList();
                        foreach (var del in delegates)
                        {
                            if (del.Target == null)
                            {
                                onStop -= (Action)del;
                                changed = true;
                            }
                            else
                            {
                                var type = del.Target.GetType();
#if UNITY_2020_1_OR_NEWER
                                var fi = type.GetField("m_Panel", BindingFlags.Instance | BindingFlags.NonPublic);
#else
                                var fi = type.GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
#endif
                                if (fi != null)
                                {
                                    var panel = fi.GetValue(del.Target);
                                    if (panel == null)
                                    {
                                        onStop -= (Action)del;
                                        changed = true;
                                    }
                                    else if (panel.GetType().GetProperty("visualTree").GetValue(panel) == null)
                                    {
                                        onStop -= (Action)del;
                                        changed = true;
                                    }
                                }
                            }
                        }
                    }
                    if (changed)
                        uAnimationMode.SetOnAnimationRecordingStop(onStop);
                }
            }
#endif
        }

        public void StopAllRecording()
        {
            var list = dg_get_s_AnimationWindows(null);
            foreach (var aw in list)
            {
                if (aw == null) continue;

                var ae = dg_get_m_AnimEditor(aw);
                if (ae == null) continue;

                var aws = uAnimEditor.GetAnimationWindowState(ae);
                if (aws == null) continue;

                if (uAnimationWindowState.GetRecording(aws))
                {
                    uAnimationWindowState.StopRecording(aws);
                }
                else if (uAnimationWindowState.GetPreviewing(aws))
                {
                    uAnimationWindowState.StopPreview(aws);
                }
            }
        }
        public void StopRecording()
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetRecording(aws))
            {
                uAnimationWindowState.StopRecording(aws);
            }
            else if (uAnimationWindowState.GetPreviewing(aws))
            {
                uAnimationWindowState.StopPreview(aws);
            }
        }
        public bool StartRecording()
        {
            var aws = animationWindowStateInstance;
            if (GetCanRecord())
            {
                if (!uAnimationWindowState.GetRecording(aws))
                {
                    if (!uAnimationWindowState.StartRecording(aws))
                        return false;
                }
            }
            else if (GetCanPreview())
            {
                if (!uAnimationWindowState.GetPreviewing(aws))
                {
                    if (!uAnimationWindowState.StartPreview(aws))
                        return false;
                }
            }
            return true;
        }
        public bool GetCanRecord()
        {
            return uAnimationWindowControl.GetCanRecord(animationWindowControlInstance);
        }
        public bool GetRecording()
        {
            return uAnimationWindowState.GetRecording(animationWindowStateInstance);
        }
        public void StartPreviewing()
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetPreviewing(aws))
                return;
            uAnimationWindowState.StartPreview(aws);
        }
        public void StopPreviewing()
        {
            var aws = animationWindowStateInstance;
            if (!uAnimationWindowState.GetPreviewing(aws))
                return;
            uAnimationWindowState.StopPreview(aws);
        }
        public bool GetCanPreview()
        {
            return uAnimationWindowState.GetCanPreview(animationWindowStateInstance);
        }
        public bool GetPreviewing()
        {
            return uAnimationWindowState.GetPreviewing(animationWindowStateInstance);
        }

        public void PlayingChange()
        {
            var aws = animationWindowStateInstance;
            if (!HasFocus())
                instance.Focus();
            var playing = uAnimationWindowState.GetPlaying(aws);
            playing = !playing;
            if (playing)
                uAnimationWindowState.StartPlayback(aws);
            else
                uAnimationWindowState.StopPlayback(aws);
        }
        public bool GetPlaying()
        {
            return uAnimationWindowState.GetPlaying(animationWindowStateInstance);
        }

        public int GetCurrentFrame()
        {
            return uAnimationWindowState.GetCurrentFrame(animationWindowStateInstance);
        }
        public void SetCurrentFrame(int frame)
        {
            uAnimationWindowState.SetCurrentFrame(animationWindowStateInstance, frame);
            Repaint();
        }
        public void MoveFrame(int add)
        {
            var aws = animationWindowStateInstance;
            var frame = uAnimationWindowState.GetCurrentFrame(aws);
            if (frame + add < 0)
            {
                if (frame == 0)
                    return;
                uAnimationWindowState.SetCurrentFrame(aws, 0);
            }
            else
            {
                uAnimationWindowState.SetCurrentFrame(aws, frame + add);
            }
            Repaint();
        }
        public int GetLastFrame(AnimationClip clip)
        {
            return Mathf.RoundToInt(clip.length * clip.frameRate);
        }
        public float GetFrameTime(int frame, AnimationClip clip)
        {
            return SnapToFrame(frame * (1f / clip.frameRate), clip.frameRate);
        }

        public float GetCurrentTime()
        {
            return uAnimationWindowState.GetCurrentTime(animationWindowStateInstance);
        }
        public void SetCurrentTime(float time)
        {
            time = SnapToFrame(time, GetSelectionAnimationClip().frameRate);
            uAnimationWindowState.SetCurrentTime(animationWindowStateInstance, time);
            Repaint();
        }
        public void SetCurrentTimeOnly(float time)
        {
            var animationKeyTime = uAnimationKeyTime.Time(time, GetSelectionAnimationClip().frameRate);
            uAnimationWindowControl.SetTime(animationWindowControlInstance, animationKeyTime);
        }

        public float SnapToFrame(float time, float fps)
        {
            return uAnimationWindowState.SnapToFrame(animationWindowStateInstance, time, fps);
        }
        public int TimeToFrameRound(float time)
        {
            return uAnimationWindowState.TimeToFrameRound(animationWindowStateInstance, time);
        }

        public virtual void MoveToNextFrame()
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetLinkedWithSequencer(aws))
            {
#if VERYANIMATION_TIMELINE
                var atwc = animationTimeWindowControlInstance;
                if (atwc != null)
                {
                    var state = uTimelineWindow.uTimelineWindowTimeControl.GetTimelineState(atwc);
                    var frame = uTimelineWindow.uTimelineState.GetFrame(state);
                    uTimelineWindow.uTimelineState.SetFrame(state, ++frame);
                    Repaint();
                }
#endif
            }
            else
            {
                MoveFrame(1);
                Repaint();
            }
        }
        public virtual void MoveToPrevFrame()
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetLinkedWithSequencer(aws))
            {
#if VERYANIMATION_TIMELINE
                var atwc = animationTimeWindowControlInstance;
                if (atwc != null)
                {
                    var state = uTimelineWindow.uTimelineWindowTimeControl.GetTimelineState(atwc);
                    var frame = uTimelineWindow.uTimelineState.GetFrame(state);
                    uTimelineWindow.uTimelineState.SetFrame(state, --frame);
                    Repaint();
                }
#endif
            }
            else
            {
                MoveFrame(-1);
                Repaint();
            }
        }
        public virtual void MoveToNextKeyframe()
        {
            uAnimationWindowControl.GoToNextKeyframe(animationWindowControlInstance);
            Repaint();
        }
        public virtual void MoveToPreviousKeyframe()
        {
            uAnimationWindowControl.GoToPreviousKeyframe(animationWindowControlInstance);
            Repaint();
        }
        public virtual void MoveToFirstKeyframe()
        {
            uAnimationWindowControl.GoToFirstKeyframe(animationWindowControlInstance);
            Repaint();
        }
        public virtual void MoveToLastKeyframe()
        {
            uAnimationWindowControl.GoToLastKeyframe(animationWindowControlInstance);
            Repaint();
        }

        public void SwitchBetweenCurvesAndDopesheet()
        {
            var ae = animEditorInstance;
            uAnimEditor.SwitchBetweenCurvesAndDopesheet(ae);
            if (vaw.editorSettings.settingAutorunFrameAll)
            {
                uAnimEditor.SetTriggerFraming(ae);
            }
            Repaint();
        }
        public bool IsShowCurveEditor()
        {
            return uAnimationWindowState.GetShowCurveEditor(animationWindowStateInstance);
        }

        public void ClearKeySelections()
        {
            var ae = animEditorInstance;
            var aws = animationWindowStateInstance;
            if (ae == null || aws == null)
                return;
            if (IsShowCurveEditor())
            {
                var curveEditor = uAnimEditor.GetCurveEditor(ae);
                if (curveEditor != null)
                {
                    if (uCurveEditor.HasSelection(curveEditor))
                    {
                        uCurveEditor.ClearSelection(curveEditor);
                        Repaint();
                    }
                }
            }
            else
            {
                var list = uAnimationWindowState.GetSelectedKeyHashes(aws);
                if (list != null)
                {
                    var e = list.GetEnumerator();
                    if (e.MoveNext())
                    {
                        uAnimationWindowState.ClearKeySelections(aws);
                        Repaint();
                    }
                }
            }
        }

        public TreeViewState GetHierarchyState()
        {
            return uAnimationWindowState.GetHierarchyState(animationWindowStateInstance);
        }

        public virtual void PropertySortOrFilterByBindings(List<EditorCurveBinding> bindings)
        {
            var aws = animationWindowStateInstance;
            var sl = selection;
            var si = selectedItem;
            if (aws == null || sl == null || si == null)
                return;
            var hierarchyData = uAnimationWindowState.GetHierarchyData(aws);
            if (hierarchyData == null)
                return;

            uAnimationWindowState.ClearCache(aws);
            if (bindings != null && bindings.Count > 0)
            {
                var selectionItemCurves = uAnimationWindowSelectionItem.GetCurves(si);
                IList curves = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(uAnimationWindowCurve.animationWindowCurveType));
                {
                    foreach (var curve in selectionItemCurves)
                    {
                        var binding = uAnimationWindowCurve.GetBinding(curve);
                        if (!bindings.Contains(binding))
                            continue;
                        curves.Add(curve);
                    }
                }
                uAnimationWindowSelectionItem.SetCurvesCache(si, curves);
                {
                    if (sl != si)   //Unity 2017.4 or earlier
                        uAnimationWindowSelection.ClearCurvesCache(sl);
                    uAnimationWindowHierarchyDataSource.UpdateData(hierarchyData);
                }
                uAnimationWindowSelectionItem.SetCurvesCache(si, selectionItemCurves);
            }
            else
            {
                uAnimationWindowHierarchyDataSource.UpdateData(hierarchyData);
            }

            Repaint();
        }

        public List<EditorCurveBinding> GetSelectedItemCurves()
        {
            var list = new List<EditorCurveBinding>();

            var aws = animationWindowStateInstance;
            var si = selectedItem;
            if (aws == null || si == null)
                return list;

            var curves = uAnimationWindowSelectionItem.GetCurves(si);
            foreach (var curve in curves)
            {
                var cbinding = uAnimationWindowCurve.GetBinding(curve);
                list.Add(cbinding);
            }
            return list;
        }

        public void SynchroCurveSelection(List<EditorCurveBinding> bindings)
        {
            var ae = animEditorInstance;
            var aws = animationWindowStateInstance;
            if (ae == null || aws == null)
                return;

            uAnimationWindowState.ClearKeySelections(aws);
            uAnimationWindowState.ClearHierarchySelection(aws);
            uAnimationWindowState.ClearLastAddedCurveBinding(aws);

            if (bindings.Count > 0)
            {
                var animationKeyTime = uAnimationKeyTime.Time(GetCurrentTime(), GetSelectionAnimationClip().frameRate);
                foreach (object dopeline in uAnimationWindowState.GetDopelines(aws))
                {
                    foreach (var curve in uDopeLine.GetCurves(dopeline))
                    {
                        var cbinding = uAnimationWindowCurve.GetBinding(curve);
                        foreach (var binding in bindings)
                        {
                            if (binding == cbinding)
                            {
                                uAnimationWindowState.SelectHierarchyItem(aws, uDopeLine.GetHierarchyNodeID(dopeline), true, false);
                                var keyframe = uAnimationWindowCurve.FindKeyAtTime(curve, animationKeyTime);
                                if (keyframe != null)
                                    uAnimationWindowState.SelectKey(aws, keyframe);
                                break;
                            }
                        }
                    }
                }
                if (IsShowCurveEditor())
                {
                    uAnimEditor.UpdateSelectedKeysToCurveEditor(ae);
                }
            }

            if (vaw.editorSettings.settingAutorunFrameAll)
            {
                uAnimEditor.SetTriggerFraming(ae);
            }

            Repaint();
        }

        public List<EditorCurveBinding> GetCurveSelection()
        {
            var list = new List<EditorCurveBinding>();
            var activeCurves = uAnimationWindowState.GetActiveCurves(animationWindowStateInstance);
            if (activeCurves == null)
                return list;
            foreach (var curve in activeCurves)
            {
                var cbinding = uAnimationWindowCurve.GetBinding(curve);
                list.Add(cbinding);
            }
            return list;
        }

        public List<EditorCurveBinding> GetMissingCurveBindings()
        {
            var aws = animationWindowStateInstance;
            List<EditorCurveBinding> list = new List<EditorCurveBinding>();
            var hierarchyData = uAnimationWindowState.GetHierarchyData(aws);
            foreach (object dopeline in uAnimationWindowState.GetDopelines(aws))
            {
                var hierarchyNodeID = uDopeLine.GetHierarchyNodeID(dopeline);
                var windowHierarchyNode = uAnimationWindowHierarchyDataSource.FindItem(hierarchyData, hierarchyNodeID);
                if (windowHierarchyNode == null) continue;
                if (uAnimationWindowUtility.IsNodeLeftOverCurve(windowHierarchyNode))
                {
                    var curves = uAnimationWindowHierarchyNode.GetCurves(windowHierarchyNode);
                    if (curves == null) continue;
                    foreach (var curve in curves)
                    {
                        if (curve == null) continue;
                        var binding = uAnimationWindowCurve.GetBinding(curve);
                        list.Add(binding);
                    }
                }
            }
            return list;
        }

        public void GetNearKeyframeTimes(float[] nextTimes, float[] prevTimes)
        {
            var aws = animationWindowStateInstance;
            Array curves = null;
            {
                var list = uAnimationWindowState.GetAllCurves(aws);
                curves = Array.CreateInstance(uAnimationWindowCurve.animationWindowCurveType, list.Count);
                list.CopyTo(curves, 0);
            }
            var frameRate = GetSelectionAnimationClip().frameRate;
            if (nextTimes != null)
            {
                var time = GetCurrentTime();
                for (int i = 0; i < nextTimes.Length; i++)
                {
                    nextTimes[i] = uAnimationWindowUtility.GetNextKeyframeTime(curves, time, frameRate);
                    if (time != nextTimes[i])
                        time = nextTimes[i];
                    else
                        nextTimes[i] = -1f;
                }
            }
            if (prevTimes != null)
            {
                var time = GetCurrentTime();
                for (int i = 0; i < prevTimes.Length; i++)
                {
                    prevTimes[i] = uAnimationWindowUtility.GetPreviousKeyframeTime(curves, time, frameRate);
                    if (time != prevTimes[i])
                        time = prevTimes[i];
                    else
                        prevTimes[i] = -1f;
                }
            }
        }

        public bool IsDoneRefresh()
        {
            var refresh = uAnimationWindowState.GetRefresh(animationWindowStateInstance);
            return refresh == UAnimationWindowState.RefreshType.None;
        }
        public void ForceRefresh()
        {
            uAnimationWindowState.ForceRefresh(animationWindowStateInstance);
            Repaint();
        }

        public void CurveWasModified(AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType type)
        {
            var aws = animationWindowStateInstance;
            uAnimationWindowState.CurveWasModified(aws, clip, binding, type);
            if (type == AnimationUtility.CurveModifiedType.CurveDeleted)
            {
                Repaint();
            }
        }

        public void ResampleAnimation()
        {
            uAnimationWindowControl.ResampleAnimation(animationWindowControlInstance);
        }

        public void Repaint()
        {
            if (!HasFocus())
                return;

            var list = dg_get_s_AnimationWindows(null);
            if (list.Count > 0)
            {
                (list[0] as EditorWindow).Repaint();
                #region OtherAnimationWindows
                if (list.Count > 1)
                {
                    var clip = GetSelectionAnimationClip();
                    for (int i = 1; i < list.Count; i++)
                    {
                        var ew = list[i] as EditorWindow;
                        if (uEditorWindow.HasFocus(ew))
                        {
                            var ae = dg_get_m_AnimEditor(ew);
                            var si = uAnimEditor.GetSelectedItem(ae);
                            if (si != null)
                            {
                                var sclip = uAnimationWindowSelectionItem.GetAnimationClip(si);
                                if (clip == sclip)
                                {
                                    var aws = uAnimEditor.GetAnimationWindowState(ae);
                                    uAnimationWindowState.ForceRefresh(aws);
                                    ew.Repaint();
                                }
                            }
                        }
                    }
                }
                #endregion
            }
        }

        public bool HasFocus()
        {
            return uEditorWindow.HasFocus(instance);
        }

        public void Close()
        {
            if (instance != null)
                instance.Close();
        }

        public virtual bool GetLock(EditorWindow aw)
        {
            return dg_get_m_Locked(aw);
        }
        public virtual void SetLock(EditorWindow aw, bool flag)
        {
            dg_set_m_Locked(aw, flag);
        }

        public virtual bool GetFilterBySelection()
        {
            return false;
        }
        public virtual void SetFilterBySelection(bool enable)
        {
        }

        public void OnSelectionChange()
        {
            if (instance == null) return;
            mi_OnSelectionChange.Invoke(instance, null);
        }

        public bool GetRemoveStartOffset()
        {
#if VERYANIMATION_TIMELINE
            if (GetLinkedWithTimeline())
                return GetTimelineAnimationRemoveStartOffset();
#endif
            return false;
        }

        public bool GetLinkedWithTimeline()
        {
#if VERYANIMATION_TIMELINE
            return uAnimationWindowState.GetLinkedWithSequencer(animationWindowStateInstance);
#else
            return false;
#endif
        }
#if VERYANIMATION_TIMELINE
        public bool GetTimelineTrackAssetEditable()
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetLinkedWithSequencer(aws))
            {
                var atwc = animationTimeWindowControlInstance;
                if (atwc != null)
                {
                    var trackAsset = uTimelineWindow.uTimelineWindowTimeControl.GetTrackAsset(atwc);
                    if (trackAsset != null && !trackAsset.muted)
                    {
                        var locked = uTimelineWindow.uTrackAsset.GetLocked(trackAsset);
                        if (!locked)
                            return true;
                    }
                }
            }
            return false;
        }
        public bool GetTimelineHasFocus()
        {
            return uEditorWindow.HasFocus(uTimelineWindow.instance);
        }

        public bool GetTimelineRecording()
        {
            return uTimelineWindow.GetRecording();
        }
        public void SetTimelineRecording(bool enable)
        {
            uTimelineWindow.SetRecording(enable);
        }

        public bool GetTimelinePreviewMode()
        {
            return uTimelineWindow.GetPreviewMode();
        }
        public void SetTimelinePreviewMode(bool enable)
        {
            uTimelineWindow.SetPreviewMode(enable);
        }

        public AnimationClip GetTimelineAnimationClip()
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetLinkedWithSequencer(aws))
            {
                var atwc = animationTimeWindowControlInstance;
                if (atwc != null)
                {
                    return uTimelineWindow.uTimelineWindowTimeControl.GetAnimationClip(atwc);
                }
            }
            return null;
        }
        public void SetTimelineAnimationClip(AnimationClip clip, string undoName = null)
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetLinkedWithSequencer(aws))
            {
                var atwc = animationTimeWindowControlInstance;
                if (atwc != null)
                {
                    uTimelineWindow.uTimelineWindowTimeControl.SetAnimationClip(atwc, clip, undoName);
                }
            }
        }

        public virtual void GetTimelineAnimationTrackInfo(out bool animatesRootTransform, out bool requiresMotionXPlayable, out bool usesAbsoluteMotion)
        {
            animatesRootTransform = false;
            requiresMotionXPlayable = false;
            usesAbsoluteMotion = false;

            var animator = GetActiveAnimationPlayer() as Animator;
            if (animator != null)
            {
                animatesRootTransform = animator.applyRootMotion;
                requiresMotionXPlayable = animatesRootTransform;
                usesAbsoluteMotion = animatesRootTransform;
            }
        }
        public virtual bool GetTimelineRootMotionOffsets(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

#if !UNITY_2018_3_OR_NEWER
            var animtionTrack = GetTimelineAnimationTrack(true);
            if (animtionTrack == null)
                return false;

            //Track Offsets
            if (animtionTrack.applyOffsets)
            {
                position = animtionTrack.position;
                rotation = animtionTrack.rotation;
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

        public float GetTimelineFrameRate()
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetLinkedWithSequencer(aws))
            {
                var atwc = animationTimeWindowControlInstance;
                if (atwc != null)
                {
                    var state = uTimelineWindow.uTimelineWindowTimeControl.GetTimelineState(atwc);
                    return uTimelineWindow.uTimelineState.GetFrameRate(state);
                }
            }
            return 0f;
        }

        public bool IsTimelineArmedForRecord()
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetLinkedWithSequencer(aws))
            {
                var awc = animationWindowControlInstance;
                if (awc != null && awc.GetType() == uTimelineWindow.uTimelineWindowTimeControl.timelineWindowTimeControlType)
                {
                    return uTimelineWindow.uTimelineWindowTimeControl.IsArmedForRecord(awc);
                }
            }
            return false;
        }

        public bool EditSequencerClip(TimelineClip timelineClip)
        {
            var sourceObject = GetActiveRootGameObject();
            object controlInterface = uTimelineWindow.uTimelineAnimationUtilities.CreateTimeController(uTimelineWindow.state, timelineClip);
            return (bool)mi_EditSequencerClip.Invoke(instance, new object[] { timelineClip.animationClip != null ? timelineClip.animationClip : timelineClip.curves, sourceObject, controlInterface });
        }

        public PlayableDirector GetTimelineCurrentDirector()
        {
            return uTimelineWindow.GetCurrentDirector();
        }

        public AnimationTrack GetTimelineAnimationTrack(bool top = false)
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetLinkedWithSequencer(aws))
            {
                var atwc = animationTimeWindowControlInstance;
                if (atwc != null)
                {
                    var animtionTrack = uTimelineWindow.uTimelineWindowTimeControl.GetTrackAsset(atwc) as AnimationTrack;
                    if (animtionTrack != null && top)
                    {
                        while (animtionTrack.parent is AnimationTrack)
                        {
                            var track = animtionTrack.parent as AnimationTrack;
                            if (track == null)
                                break;
                            animtionTrack = track;
                        }
                    }
                    return animtionTrack;
                }
            }
            return null;
        }
        public TimelineClip GetTimelineClip()
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetLinkedWithSequencer(aws))
            {
                var atwc = animationTimeWindowControlInstance;
                if (atwc != null)
                {
                    return uTimelineWindow.uTimelineWindowTimeControl.GetTimelineClip(atwc);
                }
            }
            return null;
        }
        public AnimationPlayableAsset GetTimelineAnimationPlayableAsset()
        {
            var aws = animationWindowStateInstance;
            if (uAnimationWindowState.GetLinkedWithSequencer(aws))
            {
                var atwc = animationTimeWindowControlInstance;
                if (atwc != null)
                {
                    return uTimelineWindow.uTimelineWindowTimeControl.GetPlayableAsset(atwc) as AnimationPlayableAsset;
                }
            }
            return null;
        }
        public bool GetTimelineAnimationPlayableAssetHasRootTransforms()
        {
            var animationPlayableAsset = GetTimelineAnimationPlayableAsset();
            if (animationPlayableAsset == null)
                return false;
            return uTimelineWindow.uAnimationPlayableAsset.GetHasRootTransforms(animationPlayableAsset);
        }
        public virtual bool GetTimelineAnimationRemoveStartOffset()
        {
            return false;
        }
        public virtual bool GetTimelineAnimationApplyFootIK()
        {
            return true;
        }
#endif
    }
}
