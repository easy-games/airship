using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.Animations;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UAnimatorController
    {
        protected Action<bool> dg_set_pushUndo;

        public UAnimatorController()
        {
        }

        public void SetPushUndo(UnityEditor.Animations.AnimatorController ac, bool flag)
        {
            if (ac == null) return;
            if (dg_set_pushUndo == null || dg_set_pushUndo.Target != (object)ac)
                dg_set_pushUndo = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), ac, ac.GetType().GetProperty("pushUndo", BindingFlags.NonPublic | BindingFlags.Instance).GetSetMethod(true));
            dg_set_pushUndo(flag);
        }
    }
}
