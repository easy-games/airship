using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UHandleUtility
    {
        private Action dg_ApplyWireMaterial;

        public UHandleUtility()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var handleUtilityType = asmUnityEditor.GetType("UnityEditor.HandleUtility");

            Assert.IsNotNull(dg_ApplyWireMaterial = (Action)Delegate.CreateDelegate(typeof(Action), null, handleUtilityType.GetMethod("ApplyWireMaterial", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { }, null)));
        }

        public void ApplyWireMaterial()
        {
            dg_ApplyWireMaterial();
        }
    }
}
