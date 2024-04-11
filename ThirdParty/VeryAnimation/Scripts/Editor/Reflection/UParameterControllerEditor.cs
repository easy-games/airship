using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UParameterControllerEditor
    {
        private Func<object, object> dg_get_tool;
        private Action<object, Animator> dg_set_m_PreviewAnimator;
        private Action<UnityEditor.Animations.AnimatorController> dg_set_animatorController;
        private Action dg_OnInvalidateAnimatorController;

        public UParameterControllerEditor()
        {
            var path = InternalEditorUtility.GetEditorAssemblyPath().Replace("UnityEditor.dll", "UnityEditor.Graphs.dll");
            var asmUnityEditor = Assembly.LoadFrom(path);
            var parameterControllerEditorType = asmUnityEditor.GetType("UnityEditor.Graphs.ParameterControllerEditor");
            Assert.IsNotNull(dg_get_tool = EditorCommon.CreateGetFieldDelegate<object>(parameterControllerEditorType.GetField("tool", BindingFlags.Public | BindingFlags.Static)));
            Assert.IsNotNull(dg_set_m_PreviewAnimator = EditorCommon.CreateSetFieldDelegate<Animator>(parameterControllerEditorType.GetField("m_PreviewAnimator", BindingFlags.NonPublic | BindingFlags.Instance)));
        }

        public void SetAnimatorController(UnityEditor.Animations.AnimatorController ac)
        {
            var instance = dg_get_tool(null);
            if (instance == null) return;

            dg_set_m_PreviewAnimator(instance, null);

            if (dg_set_animatorController == null || dg_set_animatorController.Target != instance)
                dg_set_animatorController = (Action<UnityEditor.Animations.AnimatorController>)Delegate.CreateDelegate(typeof(Action<UnityEditor.Animations.AnimatorController>), instance, instance.GetType().GetProperty("animatorController").GetSetMethod());
            dg_set_animatorController(ac);

            if (dg_OnInvalidateAnimatorController == null || dg_OnInvalidateAnimatorController.Target != instance)
                dg_OnInvalidateAnimatorController = (Action)Delegate.CreateDelegate(typeof(Action), instance, instance.GetType().GetMethod("OnInvalidateAnimatorController"));
            dg_OnInvalidateAnimatorController();            
        }
    }
}
