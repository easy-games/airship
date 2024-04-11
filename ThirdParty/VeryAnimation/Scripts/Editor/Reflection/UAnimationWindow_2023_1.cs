#if UNITY_2023_1_OR_NEWER
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
using System.Collections.Generic;

namespace VeryAnimation
{
    public class UAnimationWindow_2023_1 : UAnimationWindow_2020_1    //2023.1 or later
    {
        protected class UAnimationWindowState_2023_1 : UAnimationWindowState_2019_2
        {
            protected PropertyInfo pi_recording;
            protected PropertyInfo pi_playing;
            protected PropertyInfo pi_previewing;
            protected MethodInfo mi_GoToPreviousFrame;
            protected MethodInfo mi_GoToNextFrame;
            protected MethodInfo mi_GoToPreviousKeyframe;
            protected MethodInfo mi_GoToNextKeyframe;
            protected MethodInfo mi_GoToFirstKeyframe;
            protected MethodInfo mi_GoToLastKeyframe;
            protected Action<object, bool> dg_set_m_AllCurvesCacheDirty;
            protected Action<object, bool> dg_set_m_FilteredCurvesCacheDirty;
            protected Action<object, bool> dg_set_m_ActiveCurvesCacheDirty;
            protected Action<object, IList> dg_set_m_FilteredCurvesCache;
            protected Func<IList> dg_get_filteredCurves;

            public UAnimationWindowState_2023_1(Assembly asmUnityEditor) : base(asmUnityEditor)
            {
                Assert.IsNotNull(pi_recording = animationWindowStateType.GetProperty("recording"));
                Assert.IsNotNull(pi_playing = animationWindowStateType.GetProperty("playing"));
                Assert.IsNotNull(pi_previewing = animationWindowStateType.GetProperty("previewing"));
                Assert.IsNotNull(mi_GoToPreviousFrame = animationWindowStateType.GetMethod("GoToPreviousFrame"));
                Assert.IsNotNull(mi_GoToNextFrame = animationWindowStateType.GetMethod("GoToNextFrame"));
                Assert.IsNotNull(mi_GoToPreviousKeyframe = animationWindowStateType.GetMethod("GoToPreviousKeyframe"));
                Assert.IsNotNull(mi_GoToNextKeyframe = animationWindowStateType.GetMethod("GoToNextKeyframe"));
                Assert.IsNotNull(mi_GoToFirstKeyframe = animationWindowStateType.GetMethod("GoToFirstKeyframe"));
                Assert.IsNotNull(mi_GoToLastKeyframe = animationWindowStateType.GetMethod("GoToLastKeyframe"));
                Assert.IsNotNull(dg_set_m_AllCurvesCacheDirty = EditorCommon.CreateSetFieldDelegate<bool>(animationWindowStateType.GetField("m_AllCurvesCacheDirty", BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.IsNotNull(dg_set_m_FilteredCurvesCacheDirty = EditorCommon.CreateSetFieldDelegate<bool>(animationWindowStateType.GetField("m_FilteredCurvesCacheDirty", BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.IsNotNull(dg_set_m_ActiveCurvesCacheDirty = EditorCommon.CreateSetFieldDelegate<bool>(animationWindowStateType.GetField("m_ActiveCurvesCacheDirty", BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.IsNotNull(dg_set_m_FilteredCurvesCache = EditorCommon.CreateSetFieldDelegate<IList>(animationWindowStateType.GetField("m_FilteredCurvesCache", BindingFlags.NonPublic | BindingFlags.Instance)));
            }

            public override void ClearCache(object instance)
            {
                if (instance == null) return;
                dg_set_m_dopelinesCache(instance, null);  //Cache Clear
                dg_set_m_AllCurvesCacheDirty(instance, true);
                dg_set_m_FilteredCurvesCacheDirty(instance, true);
                dg_set_m_ActiveCurvesCacheDirty(instance, true);
            }
            public override bool StartRecording(object instance)
            {
                if (instance == null) return false;
                try
                {
                    pi_recording.SetValue(instance, true);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
                return true;
            }
            public override bool StopRecording(object instance)
            {
                if (instance == null) return false;
                try
                {
                    pi_recording.SetValue(instance, false);
                    pi_previewing.SetValue(instance, false);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
                return true;
            }
            public override bool StartPlayback(object instance)
            {
                if (instance == null) return false;
                try
                {
                    pi_playing.SetValue(instance, true);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
                return true;
            }
            public override bool StopPlayback(object instance)
            {
                if (instance == null) return false;
                try
                {
                    pi_playing.SetValue(instance, false);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
                return true;
            }
            public override bool StartPreview(object instance)
            {
                if (instance == null) return false;
                try
                {
                    pi_previewing.SetValue(instance, true);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
                return true;
            }
            public override bool StopPreview(object instance)
            {
                if (instance == null) return false;
                try
                {
                    pi_previewing.SetValue(instance, false);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    return false;
                }
                return true;
            }
            public void GoToPreviousFrame(object instance)
            {
                if (instance == null) return;
                mi_GoToPreviousFrame.Invoke(instance, null);
            }
            public void GoToNextFrame(object instance)
            {
                if (instance == null) return;
                mi_GoToNextFrame.Invoke(instance, null);
            }
            public void GoToPreviousKeyframe(object instance)
            {
                if (instance == null) return;
                mi_GoToPreviousKeyframe.Invoke(instance, null);
            }
            public void GoToNextKeyframe(object instance)
            {
                if (instance == null) return;
                mi_GoToNextKeyframe.Invoke(instance, null);
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

            public IList GetFilteredCurves(object instance)
            {
                if (instance == null) return null;
                if (dg_get_filteredCurves == null || dg_get_filteredCurves.Target != instance)
                    dg_get_filteredCurves = (Func<IList>)Delegate.CreateDelegate(typeof(Func<IList>), instance, instance.GetType().GetProperty("filteredCurves").GetGetMethod());
                return dg_get_filteredCurves();
            }
            public void SetFilteredCurves(object instance, IList curves)
            {
                if (instance == null) return;
                dg_set_m_FilteredCurvesCacheDirty(instance, false);
                dg_set_m_FilteredCurvesCache(instance, curves);
            }
        }
        protected class UAnimationWindowSelectionItem_2023_1 : UAnimationWindowSelectionItem
        {
            public UAnimationWindowSelectionItem_2023_1(Assembly asmUnityEditor) : base(asmUnityEditor)
            {
                var animationWindowSelectionItemType = asmUnityEditor.GetType("UnityEditorInternal.AnimationWindowSelectionItem");
            }

            public override IList GetCurves(object instance)
            {
                Assert.IsTrue(false);
                return null;
            }
            public override void SetCurvesCache(object instance, IList curves)
            {
                if (instance == null) return;
                Assert.IsTrue(false);
            }
            public override IList GetCurvesCache(object instance)
            {
                if (instance == null) return null;
                Assert.IsTrue(false);
                return null;
            }
            public override void ClearCurvesCache(object instance)
            {
                Assert.IsTrue(false);
            }
            public override Type GetEditorCurveValueType(object instance, EditorCurveBinding binding)
            {
                Assert.IsTrue(false);
                return null;
            }
        }

        protected UAnimationWindowState_2023_1 uAnimationWindowState_2023_1;
        protected UAnimationWindowSelectionItem_2023_1 uAnimationWindowSelectionItem_2023_1;

        public UAnimationWindow_2023_1()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var animationWindowType = asmUnityEditor.GetType("UnityEditor.AnimationWindow");
            uAnimationWindowState = uAnimationWindowState_2018_1 = uAnimationWindowState_2019_2 = uAnimationWindowState_2023_1 = new UAnimationWindowState_2023_1(asmUnityEditor);
            uAnimationWindowSelectionItem = uAnimationWindowSelectionItem_2023_1 = new UAnimationWindowSelectionItem_2023_1(asmUnityEditor);
        }

        public override void MoveToNextFrame()
        {
            uAnimationWindowState_2023_1.GoToNextFrame(animationWindowStateInstance);
            Repaint();
        }
        public override void MoveToPrevFrame()
        {
            uAnimationWindowState_2023_1.GoToPreviousFrame(animationWindowStateInstance);
            Repaint();
        }
        public override void MoveToNextKeyframe()
        {
            uAnimationWindowState_2023_1.GoToNextKeyframe(animationWindowStateInstance);
            Repaint();
        }
        public override void MoveToPreviousKeyframe()
        {
            uAnimationWindowState_2023_1.GoToPreviousKeyframe(animationWindowStateInstance);
            Repaint();
        }
        public override void MoveToFirstKeyframe()
        {
            uAnimationWindowState_2023_1.GoToFirstKeyframe(animationWindowStateInstance);
            Repaint();
        }
        public override void MoveToLastKeyframe()
        {
            uAnimationWindowState_2023_1.GoToLastKeyframe(animationWindowStateInstance);
            Repaint();
        }

        public override void PropertySortOrFilterByBindings(List<EditorCurveBinding> bindings)
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
                var allCurves = uAnimationWindowState.GetAllCurves(aws);
                IList curves = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(uAnimationWindowCurve.animationWindowCurveType));
                {
                    foreach (var curve in allCurves)
                    {
                        var binding = uAnimationWindowCurve.GetBinding(curve);
                        if (!bindings.Contains(binding))
                            continue;
                        curves.Add(curve);
                    }
                }
                uAnimationWindowState_2023_1.SetFilteredCurves(aws, curves);
                uAnimationWindowHierarchyDataSource.UpdateData(hierarchyData);
            }
            else
            {
                uAnimationWindowHierarchyDataSource.UpdateData(hierarchyData);
            }

            Repaint();
        }

    }
}
#endif
