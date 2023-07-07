#if VERYANIMATION_ANIMATIONRIGGING
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VeryAnimation
{
    [CustomEditor(typeof(VeryAnimationRig))]
    public class VeryAnimationRigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
        }
    }
}
#endif
