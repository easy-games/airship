using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Animations;
using UnityEngine.Playables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if VERYANIMATION_ANIMATIONRIGGING
using UnityEngine.Animations.Rigging;
using UnityEngine.Experimental.Animations;
#endif
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace VeryAnimation
{
    [DisallowMultipleComponent]
#if VERYANIMATION_ANIMATIONRIGGING
    [RequireComponent(typeof(RigBuilder))]
    public class VeryAnimationRigBuilder : MonoBehaviour, IAnimationWindowPreview
    {
        public Playable BuildPreviewGraph(PlayableGraph graph, Playable inputPlayable)
        {
            if (!enabled)
                return inputPlayable;

            SetBaseTransform();

            return inputPlayable;
        }

        public void StartPreview()
        {
            if (!enabled)
                return;

            SetBaseTransform();
        }
        public void StopPreview()
        {
            if (!enabled)
                return;
        }

        public void UpdatePreviewGraph(PlayableGraph graph)
        {
            if (!enabled)
                return;
        }

        private void SetBaseTransform()
        {
            var vaRig = GetComponentInChildren<VeryAnimationRig>();
            if (vaRig == null)
                return;
            vaRig.SetBaseTransform();
        }
    }
#else
    public class VeryAnimationRigBuilder : MonoBehaviour
    {

    }
#endif
}
