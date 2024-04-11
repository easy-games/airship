#if UNITY_2019_2_OR_NEWER
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

namespace VeryAnimation
{
    public class UAnimationWindow_2019_2 : UAnimationWindow_2019_1    //2019.2 or later
    {
        protected class UAnimationWindowState_2019_2 : UAnimationWindowState_2018_1
        {
            protected Action<object, IList> dg_set_m_AllCurvesCache;
            protected Func<bool> dg_get_filterBySelection;
            protected Action<bool> dg_set_filterBySelection;

            public UAnimationWindowState_2019_2(Assembly asmUnityEditor) : base(asmUnityEditor)
            {
                Assert.IsNotNull(dg_set_m_AllCurvesCache = EditorCommon.CreateSetFieldDelegate<IList>(animationWindowStateType.GetField("m_AllCurvesCache", BindingFlags.NonPublic | BindingFlags.Instance)));
            }

            public override void ClearCache(object instance)
            {
                if (instance == null) return;
                base.ClearCache(instance);
                dg_set_m_AllCurvesCache(instance, null);  //Cache Clear
            }

            public bool GetFilterBySelection(object instance)
            {
                if (instance == null) return false;
                if (dg_get_filterBySelection == null || dg_get_filterBySelection.Target != instance)
                    dg_get_filterBySelection = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("filterBySelection").GetGetMethod());
                return dg_get_filterBySelection();
            }
            public void SetFilterBySelection(object instance, bool enable)
            {
                if (instance == null) return;
                if (dg_set_filterBySelection == null || dg_set_filterBySelection.Target != instance)
                    dg_set_filterBySelection = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), instance, instance.GetType().GetProperty("filterBySelection").GetSetMethod());
                dg_set_filterBySelection(enable);
            }
        }

        protected class UAnimationWindowControl_2019_2 : UAnimationWindowControl_2019_1
        {
            protected Func<object, AnimationClipPlayable> dg_get_m_DefaultPosePlayable;

            public UAnimationWindowControl_2019_2(Assembly asmUnityEditor) : base(asmUnityEditor)
            {
                var animationWindowControlType = asmUnityEditor.GetType("UnityEditorInternal.AnimationWindowControl");
                Assert.IsNotNull(dg_get_m_DefaultPosePlayable = EditorCommon.CreateGetFieldDelegate<AnimationClipPlayable>(animationWindowControlType.GetField("m_DefaultPosePlayable", BindingFlags.NonPublic | BindingFlags.Instance)));
            }

            public AnimationClipPlayable GetDefaultPosePlayable(object instance)
            {
                if (instance == null) return new AnimationClipPlayable();
                return dg_get_m_DefaultPosePlayable(instance);
            }
        }

        protected UAnimationWindowState_2019_2 uAnimationWindowState_2019_2;
        protected UAnimationWindowControl_2019_2 uAnimationWindowControl_2019_2;

        public UAnimationWindow_2019_2()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var animationWindowType = asmUnityEditor.GetType("UnityEditor.AnimationWindow");
            uAnimationWindowState = uAnimationWindowState_2018_1 = uAnimationWindowState_2019_2 = new UAnimationWindowState_2019_2(asmUnityEditor);
            uAnimationWindowControl = uAnimationWindowControl_2019_1 = uAnimationWindowControl_2019_2 = new UAnimationWindowControl_2019_2(asmUnityEditor);
        }

        public override bool GetFilterBySelection()
        {
            return uAnimationWindowState_2019_2.GetFilterBySelection(animationWindowStateInstance);
        }
        public override void SetFilterBySelection(bool enable)
        {
            uAnimationWindowState_2019_2.SetFilterBySelection(animationWindowStateInstance, enable);
        }

        public AnimationClipPlayable GetDefaultPosePlayable()
        {
            return uAnimationWindowControl_2019_2.GetDefaultPosePlayable(animationWindowControlInstance);
        }
    }
}
#endif
