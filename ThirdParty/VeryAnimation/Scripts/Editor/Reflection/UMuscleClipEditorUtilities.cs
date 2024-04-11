using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UMuscleClipEditorUtilities
    {
        private Func<object, float> dg_get_loop;
        private Func<object, float> dg_get_loopOrientation;
        private Func<object, float> dg_get_loopPositionY;
        private Func<object, float> dg_get_loopPositionXZ;
        protected MethodInfo mi_GetMuscleClipQualityInfo;
        protected Func<AnimationClip, float, float, object> dg_GetMuscleClipQualityInfo;

        public class UMuscleClipQualityInfo
        {
            public float loop = 0.0f;
            public float loopOrientation = 0.0f;
            public float loopPositionY = 0.0f;
            public float loopPositionXZ = 0.0f;
        }

        public UMuscleClipEditorUtilities()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var muscleClipQualityInfoType = asmUnityEditor.GetType("UnityEditor.MuscleClipQualityInfo");
            Assert.IsNotNull(dg_get_loop = EditorCommon.CreateGetFieldDelegate<float>(muscleClipQualityInfoType.GetField("loop", BindingFlags.Public | BindingFlags.Instance)));
            Assert.IsNotNull(dg_get_loopOrientation = EditorCommon.CreateGetFieldDelegate<float>(muscleClipQualityInfoType.GetField("loopOrientation", BindingFlags.Public | BindingFlags.Instance)));
            Assert.IsNotNull(dg_get_loopPositionY = EditorCommon.CreateGetFieldDelegate<float>(muscleClipQualityInfoType.GetField("loopPositionY", BindingFlags.Public | BindingFlags.Instance)));
            Assert.IsNotNull(dg_get_loopPositionXZ = EditorCommon.CreateGetFieldDelegate<float>(muscleClipQualityInfoType.GetField("loopPositionXZ", BindingFlags.Public | BindingFlags.Instance)));

            var muscleClipEditorUtilitiesType = asmUnityEditor.GetType("UnityEditor.MuscleClipEditorUtilities");
            if (muscleClipEditorUtilitiesType != null)
            {
                mi_GetMuscleClipQualityInfo = muscleClipEditorUtilitiesType.GetMethod("GetMuscleClipQualityInfo", BindingFlags.Public | BindingFlags.Static);
                if (mi_GetMuscleClipQualityInfo != null)
                    Assert.IsNotNull(dg_GetMuscleClipQualityInfo = (Func<AnimationClip, float, float, object>)Delegate.CreateDelegate(typeof(Func<AnimationClip, float, float, object>), null, mi_GetMuscleClipQualityInfo));
            }
        }

        public UMuscleClipQualityInfo GetMuscleClipQualityInfo(AnimationClip clip, float startTime, float stopTime)
        {
            var info = dg_GetMuscleClipQualityInfo(clip, startTime, stopTime);
            return new UMuscleClipQualityInfo()
            {
                loop = dg_get_loop(info),
                loopOrientation = dg_get_loopOrientation(info),
                loopPositionY = dg_get_loopPositionY(info),
                loopPositionXZ = dg_get_loopPositionXZ(info),
            };
        }
    }
}
