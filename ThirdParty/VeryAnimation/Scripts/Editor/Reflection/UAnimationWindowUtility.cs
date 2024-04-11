using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace VeryAnimation
{
    public class UAnimationWindowUtility
    {
        private Func<EditorCurveBinding, bool> dg_ShouldShowAnimationWindowCurve;
        private MethodInfo mi_IsNodeLeftOverCurve;
        private MethodInfo mi_CreateNewClipAtPath;
        private MethodInfo mi_GetNextKeyframeTime;
        private MethodInfo mi_GetPreviousKeyframeTime;

        public UAnimationWindowUtility()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var animationWindowUtilityType = asmUnityEditor.GetType("UnityEditorInternal.AnimationWindowUtility");
            Assert.IsNotNull(dg_ShouldShowAnimationWindowCurve = (Func<EditorCurveBinding, bool>)Delegate.CreateDelegate(typeof(Func<EditorCurveBinding, bool>), null, animationWindowUtilityType.GetMethod("ShouldShowAnimationWindowCurve", BindingFlags.Public | BindingFlags.Static)));
            Assert.IsNotNull(mi_IsNodeLeftOverCurve = animationWindowUtilityType.GetMethod("IsNodeLeftOverCurve", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(mi_CreateNewClipAtPath = animationWindowUtilityType.GetMethod("CreateNewClipAtPath", BindingFlags.NonPublic | BindingFlags.Static));
            Assert.IsNotNull(mi_GetNextKeyframeTime = animationWindowUtilityType.GetMethod("GetNextKeyframeTime", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(mi_GetPreviousKeyframeTime = animationWindowUtilityType.GetMethod("GetPreviousKeyframeTime", BindingFlags.Public | BindingFlags.Static));
        }

        public bool ShouldShowAnimationWindowCurve(EditorCurveBinding binding)
        {
            return dg_ShouldShowAnimationWindowCurve(binding);
        }

        public bool IsNodeLeftOverCurve(object node)
        {
            return (bool)mi_IsNodeLeftOverCurve.Invoke(null, new object[] { node });
        }

        public AnimationClip CreateNewClipAtPath(string clipPath)
        {
            return mi_CreateNewClipAtPath.Invoke(null, new object[] { clipPath }) as AnimationClip;
        }

        public float GetNextKeyframeTime(Array curves, float currentTime, float frameRate)
        {
            return (float)mi_GetNextKeyframeTime.Invoke(null, new object[] { curves, currentTime, frameRate });
        }
        public float GetPreviousKeyframeTime(Array curves, float currentTime, float frameRate)
        {
            return (float)mi_GetPreviousKeyframeTime.Invoke(null, new object[] { curves, currentTime, frameRate });
        }
    }
}
