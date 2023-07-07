using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections;
#if VERYANIMATION_ANIMATIONRIGGING
using UnityEngine.Animations.Rigging;
using UnityEngine.Experimental.Animations;
#endif

namespace VeryAnimation
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1)] //before RigBuilder
#if VERYANIMATION_ANIMATIONRIGGING
    [RequireComponent(typeof(Rig))]
#endif
    public class VeryAnimationRig : MonoBehaviour
    {
        [Serializable]
        public struct BasePoseTransformOffset
        {
            public BasePoseTransformOffset(Transform constraint, Quaternion offsetRotation)
            {
                this.constraint = constraint;
                enable = Enable.Rotation;
                this.offsetPosition = Vector3.zero;
                this.offsetRotation = offsetRotation;
            }
            public BasePoseTransformOffset(Transform constraint, Vector3 offsetPosition, Quaternion offsetRotation)
            {
                this.constraint = constraint;
                enable = Enable.Position | Enable.Rotation;
                this.offsetPosition = offsetPosition;
                this.offsetRotation = offsetRotation;
            }
            public void Reset()
            {
                this.constraint = null;
                enable = Enable.None;
                this.offsetPosition = Vector3.zero;
                this.offsetRotation = Quaternion.identity;
            }

            [Flags]
            public enum Enable
            {
                None = 0,
                Position = (1 << 0),
                Rotation = (1 << 1),
            }
            public Transform constraint;
            public Enable enable;
            public Vector3 offsetPosition;
            public Quaternion offsetRotation;
        }

        public BasePoseTransformOffset basePoseLeftHand;
        public BasePoseTransformOffset basePoseRightHand;
        public BasePoseTransformOffset basePoseLeftFoot;
        public BasePoseTransformOffset basePoseRightFoot;

#if VERYANIMATION_ANIMATIONRIGGING
        private RigBuilder m_RigBuilder;

        private void OnEnable()
        {
            m_RigBuilder = GetComponentInParent<RigBuilder>();
            RigBuilder.onAddRigBuilder += OnAddRigBuilderCallback;
        }
        private void OnDisable()
        {
            RigBuilder.onAddRigBuilder -= OnAddRigBuilderCallback;
        }
        private void OnAddRigBuilderCallback(RigBuilder rigBuilder)
        {
            if (m_RigBuilder != rigBuilder)
                return;

            if (m_RigBuilder.graph.IsValid())
            {
                m_RigBuilder.Clear();
                SetBaseTransform(); //before RigBuilder.Build
                m_RigBuilder.Build();
            }
        }

        internal void SetBaseTransform()
        {
            var rig = GetComponent<Rig>();
            if (rig == null)
                return;
            var animator = rig.GetComponentInParent<Animator>();
            if (animator == null)
                return;

            if ((basePoseLeftHand.enable & BasePoseTransformOffset.Enable.Rotation) != 0 &&
                basePoseLeftHand.constraint != null)
            {
                var constraint = basePoseLeftHand.constraint.GetComponent<TwoBoneIKConstraint>();
                if (constraint != null && constraint.data.tip != null)
                {
                    var t = constraint.data.tip;
                    if (constraint.data.target != null)
                        constraint.data.target.rotation = t.rotation * Quaternion.Inverse(basePoseLeftHand.offsetRotation);
                }
            }
            if ((basePoseRightHand.enable & BasePoseTransformOffset.Enable.Rotation) != 0 &&
                basePoseRightHand.constraint != null)
            {
                var constraint = basePoseRightHand.constraint.GetComponent<TwoBoneIKConstraint>();
                if (constraint != null && constraint.data.tip != null)
                {
                    var t = constraint.data.tip;
                    if (constraint.data.target != null)
                        constraint.data.target.rotation = t.rotation * Quaternion.Inverse(basePoseRightHand.offsetRotation);
                }
            }

            if (basePoseLeftFoot.enable != 0 &&
                basePoseLeftFoot.constraint != null)
            {
                var constraint = basePoseLeftFoot.constraint.GetComponent<TwoBoneIKConstraint>();
                if (constraint != null && constraint.data.tip != null)
                {
                    var t = constraint.data.tip;
                    if ((basePoseLeftFoot.enable & BasePoseTransformOffset.Enable.Position) != 0)
                    {
                        if (constraint.data.target != null)
                            constraint.data.target.position = t.position - animator.transform.rotation * basePoseLeftFoot.offsetPosition;
                    }
                    if ((basePoseLeftFoot.enable & BasePoseTransformOffset.Enable.Rotation) != 0)
                    {
                        if (constraint.data.target != null)
                            constraint.data.target.rotation = t.rotation * Quaternion.Inverse(basePoseLeftFoot.offsetRotation);
                    }
                }
            }
            if (basePoseRightFoot.enable != 0 &&
                basePoseRightFoot.constraint != null)
            {
                var constraint = basePoseRightFoot.constraint.GetComponent<TwoBoneIKConstraint>();
                if (constraint != null && constraint.data.tip != null)
                {
                    var t = constraint.data.tip;
                    if ((basePoseRightFoot.enable & BasePoseTransformOffset.Enable.Position) != 0)
                    {
                        if (constraint.data.target != null)
                            constraint.data.target.position = t.position - animator.transform.rotation * basePoseRightFoot.offsetPosition;
                    }
                    if ((basePoseRightFoot.enable & BasePoseTransformOffset.Enable.Rotation) != 0)
                    {
                        if (constraint.data.target != null)
                            constraint.data.target.rotation = t.rotation * Quaternion.Inverse(basePoseRightFoot.offsetRotation);
                    }
                }
            }
        }
#else
        public void SetBaseTransform() { } //Dummy
#endif
    }
}
