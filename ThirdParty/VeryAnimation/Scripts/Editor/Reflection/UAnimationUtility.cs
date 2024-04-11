using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UAnimationUtility
    {
        private Action<AnimationCurve> dg_UpdateTangentsFromMode;
        protected Func<AnimationClip, bool> dg_GetGenerateMotionCurves;
        protected Action<AnimationClip, bool> dg_SetGenerateMotionCurves;
        private Func<AnimationClip, bool> dg_HasMotionCurves;
        private Func<AnimationClip, bool> dg_HasRootCurves;
        private Action<AnimationCurve, int> dg_UpdateTangentsFromModeSurrounding;
        private Action<AnimationClip, EditorCurveBinding, AnimationCurve, bool> dg_Internal_SetEditorCurve;
        protected Action<AnimationClip> dg_Internal_SyncEditorCurves;

        public UAnimationUtility()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var animationUtilityType = asmUnityEditor.GetType("UnityEditor.AnimationUtility");
            Assert.IsNotNull(dg_UpdateTangentsFromMode = (Action<AnimationCurve>)Delegate.CreateDelegate(typeof(Action<AnimationCurve>), null, animationUtilityType.GetMethod("UpdateTangentsFromMode", BindingFlags.NonPublic | BindingFlags.Static)));
            {
                var mi = animationUtilityType.GetMethod("GetGenerateMotionCurves", BindingFlags.NonPublic | BindingFlags.Static);
                if (mi != null)
                    dg_GetGenerateMotionCurves = (Func<AnimationClip, bool>)Delegate.CreateDelegate(typeof(Func<AnimationClip, bool>), null, mi);
            }
            {
                var mi = animationUtilityType.GetMethod("SetGenerateMotionCurves", BindingFlags.NonPublic | BindingFlags.Static);
                if (mi != null)
                    dg_SetGenerateMotionCurves = (Action<AnimationClip, bool>)Delegate.CreateDelegate(typeof(Action<AnimationClip, bool>), null, mi);
            }
            Assert.IsNotNull(dg_HasMotionCurves = (Func<AnimationClip, bool>)Delegate.CreateDelegate(typeof(Func<AnimationClip, bool>), null, animationUtilityType.GetMethod("HasMotionCurves", BindingFlags.NonPublic | BindingFlags.Static)));
            Assert.IsNotNull(dg_HasRootCurves = (Func<AnimationClip, bool>)Delegate.CreateDelegate(typeof(Func<AnimationClip, bool>), null, animationUtilityType.GetMethod("HasRootCurves", BindingFlags.NonPublic | BindingFlags.Static)));
            Assert.IsNotNull(dg_UpdateTangentsFromModeSurrounding = (Action<AnimationCurve, int>)Delegate.CreateDelegate(typeof(Action<AnimationCurve, int>), null, animationUtilityType.GetMethod("UpdateTangentsFromModeSurrounding", BindingFlags.NonPublic | BindingFlags.Static)));
            Assert.IsNotNull(dg_Internal_SetEditorCurve = (Action<AnimationClip, EditorCurveBinding, AnimationCurve, bool>)Delegate.CreateDelegate(typeof(Action<AnimationClip, EditorCurveBinding, AnimationCurve, bool>), null, animationUtilityType.GetMethod("Internal_SetEditorCurve", BindingFlags.NonPublic | BindingFlags.Static)));
            {
                var mi = animationUtilityType.GetMethod("Internal_SyncEditorCurves", BindingFlags.NonPublic | BindingFlags.Static);
                if (mi != null)
                    dg_Internal_SyncEditorCurves = (Action<AnimationClip>)Delegate.CreateDelegate(typeof(Action<AnimationClip>), null, mi);
            }
        }

        public void UpdateTangentsFromMode(AnimationCurve curve)
        {
            dg_UpdateTangentsFromMode(curve);
        }

        public bool GetGenerateMotionCurves(AnimationClip clip)
        {
            return dg_GetGenerateMotionCurves(clip);
        }

        public void SetGenerateMotionCurves(AnimationClip clip, bool value)
        {
            dg_SetGenerateMotionCurves(clip, value);
        }

        public bool HasMotionCurves(AnimationClip clip)
        {
            return dg_HasMotionCurves(clip);
        }

        public bool HasRootCurves(AnimationClip clip)
        {
            return dg_HasRootCurves(clip);
        }

        public void UpdateTangentsFromModeSurrounding(AnimationCurve curve, int index)
        {
            dg_UpdateTangentsFromModeSurrounding(curve, index);
        }

        public void Internal_SetEditorCurve(AnimationClip clip, EditorCurveBinding binding, AnimationCurve curve, bool syncEditorCurve)
        {
            dg_Internal_SetEditorCurve(clip, binding, curve, syncEditorCurve);
        }
        public void Internal_SyncEditorCurves(AnimationClip clip)
        {
            dg_Internal_SyncEditorCurves(clip);
        }
    }
}
