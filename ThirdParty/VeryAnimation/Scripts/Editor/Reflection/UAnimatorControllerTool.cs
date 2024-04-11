using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UAnimatorControllerTool
    {
        private Func<object, object> dg_get_tool;
        private Action<UnityEditor.Animations.AnimatorController> dg_set_animatorController;
        private Func<bool> dg_get_isLocked;
        private Action dg_OnInvalidateAnimatorController;

        public UAnimatorControllerTool()
        {
            var path = InternalEditorUtility.GetEditorAssemblyPath().Replace("UnityEditor.dll", "UnityEditor.Graphs.dll");
            var asmUnityEditor = Assembly.LoadFrom(path);
            var animatorControllerToolType = asmUnityEditor.GetType("UnityEditor.Graphs.AnimatorControllerTool");
            {
                var fi_tool = animatorControllerToolType.GetField("tool", BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(dg_get_tool = EditorCommon.CreateGetFieldDelegate<object>(fi_tool));
            }
        }

        public void SetAnimatorController(UnityEditor.Animations.AnimatorController ac)
        {
            var instance = dg_get_tool(null);
            if (instance == null) return;

            if (dg_get_isLocked == null || dg_get_isLocked.Target != instance)
                dg_get_isLocked = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, instance.GetType().GetProperty("isLocked").GetGetMethod());
            if (dg_get_isLocked()) return;

            if (dg_OnInvalidateAnimatorController == null || dg_OnInvalidateAnimatorController.Target != instance)
                dg_OnInvalidateAnimatorController = (Action)Delegate.CreateDelegate(typeof(Action), instance, instance.GetType().GetMethod("OnInvalidateAnimatorController"));
            dg_OnInvalidateAnimatorController();

            if (dg_set_animatorController == null || dg_set_animatorController.Target != instance)
                dg_set_animatorController = (Action<UnityEditor.Animations.AnimatorController>)Delegate.CreateDelegate(typeof(Action<UnityEditor.Animations.AnimatorController>), instance, instance.GetType().GetProperty("animatorController").GetSetMethod());
            dg_set_animatorController(ac);
        }
    }
}
