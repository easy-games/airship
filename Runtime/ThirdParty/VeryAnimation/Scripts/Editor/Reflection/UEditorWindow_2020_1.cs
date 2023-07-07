#if UNITY_2020_1_OR_NEWER
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UEditorWindow_2020_1 : UEditorWindow
    {
        public UEditorWindow_2020_1()
        {
        }

        public override bool HasFocus(EditorWindow w)
        {
            if (w == null) return false;
            if (dg_get_hasFocus == null || dg_get_hasFocus.Target != (object)w)
                dg_get_hasFocus = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), w, w.GetType().GetProperty("hasFocus", BindingFlags.Public | BindingFlags.Instance).GetGetMethod());
            return dg_get_hasFocus();
        }
    }
}
#endif