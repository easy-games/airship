using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.Animations;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UAnimatorState
    {
        protected Action<bool> dg_set_pushUndo;

        public UAnimatorState()
        {
        }

        public void SetPushUndo(UnityEditor.Animations.AnimatorState animatorState, bool flag)
        {
            if (animatorState == null) return;
            if (dg_set_pushUndo == null || dg_set_pushUndo.Target != (object)animatorState)
                dg_set_pushUndo = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), animatorState, animatorState.GetType().GetProperty("pushUndo", BindingFlags.NonPublic | BindingFlags.Instance).GetSetMethod(true));
            dg_set_pushUndo(flag);
        }
    }
}
