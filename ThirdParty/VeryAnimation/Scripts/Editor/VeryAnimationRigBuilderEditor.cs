#if VERYANIMATION_ANIMATIONRIGGING
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEditor;
using UnityEditorInternal;

namespace VeryAnimation
{
    [CustomEditor(typeof(VeryAnimationRigBuilder))]
    public class VeryAnimationRigBuilderEditor : Editor
    {
        void OnEnable()
        {
            //Must be in order before RigBuilder
            var vaRigBuilder = target as VeryAnimationRigBuilder;
            if (vaRigBuilder == null) return;
            var components = vaRigBuilder.GetComponents<MonoBehaviour>();
            var indexRigBuilder = ArrayUtility.FindIndex(components, x => x.GetType() == typeof(RigBuilder));
            var indexVARigBuilder = ArrayUtility.FindIndex(components, x => x.GetType() == typeof(VeryAnimationRigBuilder));
            if (indexRigBuilder >= 0 && indexVARigBuilder >= 0)
            {
                for (int i = 0; i < indexVARigBuilder - indexRigBuilder; i++)
                    ComponentUtility.MoveComponentUp(vaRigBuilder);
            }
        }

        public override void OnInspectorGUI()
        {
        }
    }
}
#endif
