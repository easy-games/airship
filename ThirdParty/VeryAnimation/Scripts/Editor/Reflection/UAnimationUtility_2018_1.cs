using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UAnimationUtility_2018_1 : UAnimationUtility
    {
        public UAnimationUtility_2018_1()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var animationUtilityType = asmUnityEditor.GetType("UnityEditor.AnimationUtility");
            Assert.IsNotNull(dg_GetGenerateMotionCurves = (Func<AnimationClip, bool>)Delegate.CreateDelegate(typeof(Func<AnimationClip, bool>), null, animationUtilityType.GetMethod("GetGenerateMotionCurves", BindingFlags.Public | BindingFlags.Static)));
            Assert.IsNotNull(dg_SetGenerateMotionCurves = (Action<AnimationClip, bool>)Delegate.CreateDelegate(typeof(Action<AnimationClip, bool>), null, animationUtilityType.GetMethod("SetGenerateMotionCurves", BindingFlags.Public | BindingFlags.Static)));
#if UNITY_2018_3_OR_NEWER
            Assert.IsNotNull(dg_Internal_SyncEditorCurves = (Action<AnimationClip>)Delegate.CreateDelegate(typeof(Action<AnimationClip>), null, animationUtilityType.GetMethod("SyncEditorCurves", BindingFlags.NonPublic | BindingFlags.Static)));
#endif
        }
    }
}
