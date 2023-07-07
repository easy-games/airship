using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UCurveUtility
    {
        private Action<AnimationCurve, int> dg_SetKeyModeFromContext;

        public UCurveUtility()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var curveUtilityType = asmUnityEditor.GetType("UnityEditor.CurveUtility");
            Assert.IsNotNull(dg_SetKeyModeFromContext = (Action<AnimationCurve, int>)Delegate.CreateDelegate(typeof(Action<AnimationCurve, int>), null, curveUtilityType.GetMethod("SetKeyModeFromContext", BindingFlags.Public | BindingFlags.Static)));
        }

        public void SetKeyModeFromContext(AnimationCurve curve, int keyIndex)
        {
            dg_SetKeyModeFromContext(curve, keyIndex);
        }
    }
}
