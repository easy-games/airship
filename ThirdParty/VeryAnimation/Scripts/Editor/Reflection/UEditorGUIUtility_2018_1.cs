using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UEditorGUIUtility_2018_1 : UEditorGUIUtility
    {
        public class UEditorLockTracker
        {
            private PropertyInfo pi_isLocked;

            public UEditorLockTracker(Assembly asmUnityEditor)
            {
                var editorLockTrackerType = asmUnityEditor.GetType("UnityEditor.EditorGUIUtility+EditorLockTracker");
                Assert.IsNotNull(pi_isLocked = editorLockTrackerType.GetProperty("isLocked", BindingFlags.NonPublic | BindingFlags.Instance));
            }

            public bool GetLock(object instance)
            {
                if (instance == null) return false;
                return (bool)pi_isLocked.GetValue(instance, null);
            }
            public void SetLock(object instance, bool flag)
            {
                if (instance == null) return;
                pi_isLocked.SetValue(instance, flag, null);
            }
        }

        public UEditorLockTracker uEditorLockTracker;

        public UEditorGUIUtility_2018_1()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            uEditorLockTracker = new UEditorLockTracker(asmUnityEditor);
        }
    }
}
