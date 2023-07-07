using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class URotationCurveInterpolation
    {
        private MethodInfo mi_GetModeFromCurveData;
        private MethodInfo mi_SetInterpolation;

        public enum Mode
        {
            Baked,
            NonBaked,
            RawQuaternions,
            RawEuler,
            Undefined,
            Total,
        }
        public static readonly string[] PrefixForInterpolation =
        {
            "localEulerAnglesBaked.",
            "localEulerAngles.",
            "m_LocalRotation.",
            "localEulerAnglesRaw.",
            null,
        };

        public URotationCurveInterpolation()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var rotationCurveInterpolationType = asmUnityEditor.GetType("UnityEditor.RotationCurveInterpolation");
            Assert.IsNotNull(mi_GetModeFromCurveData = rotationCurveInterpolationType.GetMethod("GetModeFromCurveData", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(mi_SetInterpolation = rotationCurveInterpolationType.GetMethod("SetInterpolation", BindingFlags.NonPublic | BindingFlags.Static));
        }

        public Mode GetModeFromCurveData(EditorCurveBinding data)
        {
            return (Mode)mi_GetModeFromCurveData.Invoke(null, new object[] { data });
        }
        
        public void SetInterpolation(AnimationClip clip, EditorCurveBinding[] curveBindings, Mode newInterpolationMode)
        {
            mi_SetInterpolation.Invoke(null, new object[] { clip, curveBindings, (int)newInterpolationMode });
        }
    }
}
