using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UMuscleClipEditorUtilities_2018_1 : UMuscleClipEditorUtilities
    {
        public UMuscleClipEditorUtilities_2018_1()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());

            var muscleClipEditorUtilitiesType = asmUnityEditor.GetType("UnityEditor.MuscleClipUtility");
            if (muscleClipEditorUtilitiesType != null)
            {
                mi_GetMuscleClipQualityInfo = muscleClipEditorUtilitiesType.GetMethod("GetMuscleClipQualityInfo", BindingFlags.NonPublic | BindingFlags.Static);
                if (mi_GetMuscleClipQualityInfo != null)
                    Assert.IsNotNull(dg_GetMuscleClipQualityInfo = (Func<AnimationClip, float, float, object>)Delegate.CreateDelegate(typeof(Func<AnimationClip, float, float, object>), null, mi_GetMuscleClipQualityInfo));
            }
        }
    }
}
