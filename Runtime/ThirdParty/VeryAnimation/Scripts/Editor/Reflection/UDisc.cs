using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UDisc
    {
        private Func<object, float> dg_get_s_RotationDist;

        public UDisc()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var discType = asmUnityEditor.GetType("UnityEditorInternal.Disc");
            Assert.IsNotNull(dg_get_s_RotationDist = EditorCommon.CreateGetFieldDelegate<float>(discType.GetField("s_RotationDist", BindingFlags.NonPublic | BindingFlags.Static)));
        }
        
        public float GetRotationDist()
        {
            return dg_get_s_RotationDist(null);
        }
    }
}
