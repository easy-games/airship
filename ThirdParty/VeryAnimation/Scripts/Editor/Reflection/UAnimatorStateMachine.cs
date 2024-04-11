using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.Animations;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UAnimatorStateMachine
    {
        protected Action<bool> dg_set_pushUndo;

        public UAnimatorStateMachine()
        {
        }

        public void SetPushUndo(UnityEditor.Animations.AnimatorStateMachine asm, bool flag)
        {
            if (asm == null) return;
            if (dg_set_pushUndo == null || dg_set_pushUndo.Target != (object)asm)
                dg_set_pushUndo = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), asm, asm.GetType().GetProperty("pushUndo", BindingFlags.NonPublic | BindingFlags.Instance).GetSetMethod(true));
            dg_set_pushUndo(flag);
        }
    }
}
