using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UAnimator
    {
        public Transform GetAvatarRoot(Animator animator)
        {
#if UNITY_2022_2_OR_NEWER
            return animator.avatarRoot;
#else
            var pi_avatarRoot = animator.GetType().GetProperty("avatarRoot", BindingFlags.NonPublic | BindingFlags.Instance);
            return (Transform)pi_avatarRoot.GetValue(animator, null);
#endif
        }
    }
}
