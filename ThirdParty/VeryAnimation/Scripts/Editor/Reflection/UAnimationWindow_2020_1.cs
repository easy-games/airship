#if UNITY_2020_1_OR_NEWER
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
using System;
using System.Reflection;
using System.Collections;

namespace VeryAnimation
{
    public class UAnimationWindow_2020_1 : UAnimationWindow_2019_2    //2020.1 or later
    {
        protected UEditorWindow_2020_1 uEditorWindow_2020_1;

        public UAnimationWindow_2020_1()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var animationWindowType = asmUnityEditor.GetType("UnityEditor.AnimationWindow");

            //Unity2020.2
            if (mi_OnSelectionChange == null)
                Assert.IsNotNull(mi_OnSelectionChange = animationWindowType.GetMethod("OnSelectionChange", BindingFlags.NonPublic | BindingFlags.Instance));
            if (mi_EditSequencerClip == null)
                Assert.IsNotNull(mi_EditSequencerClip = animationWindowType.GetMethod("EditSequencerClip", BindingFlags.NonPublic | BindingFlags.Instance));

            uEditorWindow = uEditorWindow_2020_1 = new UEditorWindow_2020_1();
        }
    }
}
#endif
