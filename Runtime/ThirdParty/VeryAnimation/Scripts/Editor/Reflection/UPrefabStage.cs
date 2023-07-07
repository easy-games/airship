#if UNITY_2018_3_OR_NEWER

using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using System;
using System.Reflection;
#if !UNITY_2021_2_OR_NEWER
using UnityEditor.Experimental.SceneManagement;
#endif

namespace VeryAnimation
{
    public class UPrefabStage
    {
        private Func<bool> dg_get_autoSave;

        public UPrefabStage()
        {
        }

        public bool GetAutoSave(PrefabStage instance)
        {
            if (instance == null) return false;
            if (dg_get_autoSave == null || dg_get_autoSave.Target != (object)instance)
                dg_get_autoSave = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("autoSave", BindingFlags.Instance | BindingFlags.NonPublic).GetGetMethod(true));
            return dg_get_autoSave();
        }
    }
}

#endif
