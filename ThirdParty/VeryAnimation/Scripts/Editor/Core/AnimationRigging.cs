#if VERYANIMATION_ANIMATIONRIGGING
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Collections.Generic;
using UnityEngine.Animations.Rigging;
using UnityEditor.Animations.Rigging;

namespace VeryAnimation
{
    public class AnimationRigging
    {
        private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
        private VeryAnimation va { get { return VeryAnimation.instance; } }

        public const string AnimationRiggingRigName = "VARig";

        public RigBuilder rigBuilder { get; private set; }
#if UNITY_2020_1_OR_NEWER
        public RigLayer rigLayer { get; private set; }   //version 0.3
#else
        public RigBuilder.RigLayer rigLayer { get; private set; }   //version 0.2
#endif
        public Rig rig { get; private set; }
        public VeryAnimationRigBuilder vaRigBuilder { get; private set; }
        public VeryAnimationRig vaRig { get; private set; }

        public bool isValid { get { return vaRigBuilder != null && vaRig != null && rigBuilder != null && rigLayer != null && rig != null;  } }
        public bool isActive { get { return isValid && rigBuilder.isActiveAndEnabled && rigLayer.active; } }
        public float weight { get { return isActive ? rig.weight : 0f; } }

        public void Initialize()
        {
            Release();

            rigBuilder = vaw.gameObject.GetComponent<RigBuilder>();
            rig = null;
            vaRigBuilder = vaw.gameObject.GetComponent<VeryAnimationRigBuilder>();
            vaRig = GetVeryAnimationRig(vaw.gameObject);
            if (vaRig != null)
                rig = vaRig.GetComponent<Rig>();
            if (rigBuilder != null)
                rigLayer = rigBuilder.layers.Find(x => x != null && x.rig == rig);
        }
        public void Release()
        {
            rigBuilder = null;
            rigLayer = null;
            rig = null;
            vaRigBuilder = null;
            vaRig = null;
        }

        public void Enable()
        {
            Disable();

            va.StopRecording();
            {
                Create(vaw.gameObject);

                rigBuilder = vaw.gameObject.GetComponent<RigBuilder>();
                vaRigBuilder = vaw.gameObject.GetComponent<VeryAnimationRigBuilder>();
                vaRig = GetVeryAnimationRig(vaw.gameObject);
                rig = vaRig != null ? vaRig.GetComponent<Rig>() : null;
                rigLayer = rigBuilder != null ? rigBuilder.layers.Find(x => x != null && x.rig == rig) : null;
            }
            va.OnHierarchyWindowChanged();
        }
        public void Disable()
        {
            va.StopRecording();
            {
                Delete(vaw.gameObject);
            }
            va.OnHierarchyWindowChanged();

            Release();
        }
        public static VeryAnimationRig GetVeryAnimationRig(GameObject gameObject)
        {
            return ArrayUtility.Find(gameObject.GetComponentsInChildren<VeryAnimationRig>(true), x => x.name == AnimationRiggingRigName && x.transform.parent == gameObject.transform);
        }
        public static void Create(GameObject gameObject)
        {
            var rigBuilder = gameObject.GetComponent<RigBuilder>();
            if (rigBuilder == null)
            {
                rigBuilder = Undo.AddComponent<RigBuilder>(gameObject);
            }

            var vaRigBuilder = gameObject.GetComponent<VeryAnimationRigBuilder>();
            if (vaRigBuilder == null)
            {
                vaRigBuilder = Undo.AddComponent<VeryAnimationRigBuilder>(gameObject);
            }

            //Must be in order before RigBuilder
            {
                var components = vaRigBuilder.GetComponents<MonoBehaviour>();
                var indexRigBuilder = ArrayUtility.FindIndex(components, x => x.GetType() == typeof(RigBuilder));
                var indexVARigBuilder = ArrayUtility.FindIndex(components, x => x.GetType() == typeof(VeryAnimationRigBuilder));
                if (indexRigBuilder >= 0 && indexVARigBuilder >= 0)
                {
                    for (int i = 0; i < indexVARigBuilder - indexRigBuilder; i++)
                        ComponentUtility.MoveComponentUp(vaRigBuilder);
                }
            }

            var vaRig = GetVeryAnimationRig(gameObject);
            if (vaRig == null)
            {
                var rigObj = new GameObject(AnimationRiggingRigName);
                rigObj.transform.SetParent(gameObject.transform);
                rigObj.transform.localPosition = Vector3.zero;
                rigObj.transform.localRotation = Quaternion.identity;
                rigObj.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(rigObj, "");
                var rig = Undo.AddComponent<Rig>(rigObj);
                vaRig = Undo.AddComponent<VeryAnimationRig>(rigObj);
                Undo.RecordObject(rigBuilder, "");
#if UNITY_2020_1_OR_NEWER
                var rigLayer = new RigLayer(rig);   //version 0.3
#else
                var rigLayer = new RigBuilder.RigLayer(rig);    //version 0.2
#endif
                rigBuilder.layers.Add(rigLayer);
                Selection.activeGameObject = rigObj;
            }
        }
        public static void Delete(GameObject gameObject)
        {
            var rigBuilder = gameObject.GetComponent<RigBuilder>();
            var vaRigBuilder = gameObject.GetComponent<VeryAnimationRigBuilder>();
            var vaRig = GetVeryAnimationRig(gameObject);
            var rig = vaRig != null ? vaRig.GetComponent<Rig>() : null;

            var index = rigBuilder != null && rig != null ? rigBuilder.layers.FindIndex(x => x.rig == rig) : -1;
            if (rig != null)
            {
                Selection.activeGameObject = rig.gameObject;
                Unsupported.DeleteGameObjectSelection();
                if (rig != null)
                    return;
                rig = null;
            }
            if (vaRigBuilder != null)
            {
                Undo.DestroyObjectImmediate(vaRigBuilder);
                vaRigBuilder = null;
            }
            if (rigBuilder != null)
            {
                if (index >= 0 && index < rigBuilder.layers.Count)
                {
                    Undo.RecordObject(rigBuilder, "");
                    rigBuilder.layers.RemoveAt(index);
                    if (rigBuilder.layers.Count == 0)
                    {
                        Undo.DestroyObjectImmediate(rigBuilder);
                    }
                }
                rigBuilder = null;
            }
        }

        public static void ReplaceConstraintTransformReference(GameObject gameObject, Rig rig,
                                                                GameObject originalGameObject, Rig originalRig)
        {
            Func<Transform, Transform> GetPreviewTransform = (t) =>
            {
                if (t == null) return null;
                var path = AnimationUtility.CalculateTransformPath(t, originalGameObject.transform);
                return gameObject.transform.Find(path);
            };

            var originalRigConstraints = originalGameObject.GetComponentsInChildren<IRigConstraint>();
            foreach (var originalRigConstraint in originalRigConstraints)
            {
                #region BlendConstraint
                if (originalRigConstraint is BlendConstraint)
                {
                    var blendConstraint = originalRigConstraint as BlendConstraint;
                    var constraint = GetPreviewTransform(blendConstraint.transform).GetComponent<BlendConstraint>();
                    if (constraint != null)
                    {
                        constraint.data.constrainedObject = GetPreviewTransform(blendConstraint.data.constrainedObject);
                        constraint.data.sourceObjectA = GetPreviewTransform(blendConstraint.data.sourceObjectA);
                        constraint.data.sourceObjectB = GetPreviewTransform(blendConstraint.data.sourceObjectB);
                    }
                }
                #endregion
                #region ChainIKConstraint
                else if (originalRigConstraint is ChainIKConstraint)
                {
                    var chainIKConstraint = originalRigConstraint as ChainIKConstraint;
                    var constraint = GetPreviewTransform(chainIKConstraint.transform).GetComponent<ChainIKConstraint>();
                    if (constraint != null)
                    {
                        constraint.data.root = GetPreviewTransform(chainIKConstraint.data.root);
                        constraint.data.tip = GetPreviewTransform(chainIKConstraint.data.tip);
                        constraint.data.target = GetPreviewTransform(chainIKConstraint.data.target);
                    }
                }
                #endregion
                #region ChainIKConstraint
                else if (originalRigConstraint is DampedTransform)
                {
                    var dampedTransform = originalRigConstraint as DampedTransform;
                    var constraint = GetPreviewTransform(dampedTransform.transform).GetComponent<DampedTransform>();
                    if (constraint != null)
                    {
                        constraint.data.constrainedObject = GetPreviewTransform(dampedTransform.data.constrainedObject);
                        constraint.data.sourceObject = GetPreviewTransform(dampedTransform.data.sourceObject);
                    }
                }
                #endregion
                #region MultiAimConstraint
                else if (originalRigConstraint is MultiAimConstraint)
                {
                    var multiAimConstraint = originalRigConstraint as MultiAimConstraint;
                    var constraint = GetPreviewTransform(multiAimConstraint.transform).GetComponent<MultiAimConstraint>();
                    if (constraint != null)
                    {
                        constraint.data.constrainedObject = GetPreviewTransform(multiAimConstraint.data.constrainedObject);
                        var sourceObjects = constraint.data.sourceObjects;
                        for (int i = 0; i < multiAimConstraint.data.sourceObjects.Count; i++)
                            sourceObjects.SetTransform(i, GetPreviewTransform(multiAimConstraint.data.sourceObjects.GetTransform(i)));
                        constraint.data.sourceObjects = sourceObjects;
                    }
                }
                #endregion
                #region MultiParentConstraint
                else if (originalRigConstraint is MultiParentConstraint)
                {
                    var multiParentConstraint = originalRigConstraint as MultiParentConstraint;
                    var constraint = GetPreviewTransform(multiParentConstraint.transform).GetComponent<MultiParentConstraint>();
                    if (constraint != null)
                    {
                        constraint.data.constrainedObject = GetPreviewTransform(multiParentConstraint.data.constrainedObject);
                        var sourceObjects = constraint.data.sourceObjects;
                        for (int i = 0; i < multiParentConstraint.data.sourceObjects.Count; i++)
                            sourceObjects.SetTransform(i, GetPreviewTransform(multiParentConstraint.data.sourceObjects.GetTransform(i)));
                        constraint.data.sourceObjects = sourceObjects;
                    }
                }
                #endregion
                #region MultiPositionConstraint
                else if (originalRigConstraint is MultiPositionConstraint)
                {
                    var multiPositionConstraint = originalRigConstraint as MultiPositionConstraint;
                    var constraint = GetPreviewTransform(multiPositionConstraint.transform).GetComponent<MultiPositionConstraint>();
                    if (constraint != null)
                    {
                        constraint.data.constrainedObject = GetPreviewTransform(multiPositionConstraint.data.constrainedObject);
                        var sourceObjects = constraint.data.sourceObjects;
                        for (int i = 0; i < multiPositionConstraint.data.sourceObjects.Count; i++)
                            sourceObjects.SetTransform(i, GetPreviewTransform(multiPositionConstraint.data.sourceObjects.GetTransform(i)));
                        constraint.data.sourceObjects = sourceObjects;
                    }
                }
                #endregion
                #region MultiReferentialConstraint
                else if (originalRigConstraint is MultiReferentialConstraint)
                {
                    var multiReferentialConstraint = originalRigConstraint as MultiReferentialConstraint;
                    var constraint = GetPreviewTransform(multiReferentialConstraint.transform).GetComponent<MultiReferentialConstraint>();
                    if (constraint != null)
                    {
                        var sourceObjects = constraint.data.sourceObjects;
                        for (int i = 0; i < multiReferentialConstraint.data.sourceObjects.Count; i++)
                            sourceObjects[i] = GetPreviewTransform(multiReferentialConstraint.data.sourceObjects[i]);
                        constraint.data.sourceObjects = sourceObjects;
                    }
                }
                #endregion
                #region MultiRotationConstraint
                else if (originalRigConstraint is MultiRotationConstraint)
                {
                    var multiRotationConstraint = originalRigConstraint as MultiRotationConstraint;
                    var constraint = GetPreviewTransform(multiRotationConstraint.transform).GetComponent<MultiRotationConstraint>();
                    if (constraint != null)
                    {
                        constraint.data.constrainedObject = GetPreviewTransform(multiRotationConstraint.data.constrainedObject);
                        var sourceObjects = constraint.data.sourceObjects;
                        for (int i = 0; i < multiRotationConstraint.data.sourceObjects.Count; i++)
                            sourceObjects.SetTransform(i, GetPreviewTransform(multiRotationConstraint.data.sourceObjects.GetTransform(i)));
                        constraint.data.sourceObjects = sourceObjects;
                    }
                }
                #endregion
                #region OverrideTransform
                else if (originalRigConstraint is OverrideTransform)
                {
                    var overrideTransform = originalRigConstraint as OverrideTransform;
                    var constraint = GetPreviewTransform(overrideTransform.transform).GetComponent<OverrideTransform>();
                    if (constraint != null)
                    {
                        constraint.data.constrainedObject = GetPreviewTransform(overrideTransform.data.constrainedObject);
                        constraint.data.sourceObject = GetPreviewTransform(overrideTransform.data.sourceObject);
                    }
                }
                #endregion
                #region TwistCorrection
                else if (originalRigConstraint is TwistCorrection)
                {
                    var twistCorrection = originalRigConstraint as TwistCorrection;
                    var constraint = GetPreviewTransform(twistCorrection.transform).GetComponent<TwistCorrection>();
                    if (constraint != null)
                    {
                        constraint.data.sourceObject = GetPreviewTransform(twistCorrection.data.sourceObject);
                        var twistNodes = constraint.data.twistNodes;
                        for (int i = 0; i < twistCorrection.data.twistNodes.Count; i++)
                            twistNodes.SetTransform(i, GetPreviewTransform(twistCorrection.data.twistNodes.GetTransform(i)));
                        constraint.data.twistNodes = twistNodes;
                    }
                }
                #endregion
                #region TwoBoneIKConstraint
                else if (originalRigConstraint is TwoBoneIKConstraint)
                {
                    var twoBoneIKConstraint = originalRigConstraint as TwoBoneIKConstraint;
                    var constraint = GetPreviewTransform(twoBoneIKConstraint.transform).GetComponent<TwoBoneIKConstraint>();
                    if (constraint != null)
                    {
                        constraint.data.root = GetPreviewTransform(twoBoneIKConstraint.data.root);
                        constraint.data.mid = GetPreviewTransform(twoBoneIKConstraint.data.mid);
                        constraint.data.tip = GetPreviewTransform(twoBoneIKConstraint.data.tip);
                        constraint.data.target = GetPreviewTransform(twoBoneIKConstraint.data.target);
                        constraint.data.hint = GetPreviewTransform(twoBoneIKConstraint.data.hint);
                    }
                }
                #endregion
#if UNITY_2020_1_OR_NEWER
                #region TwistChainConstraint
                else if (originalRigConstraint is TwistChainConstraint) //version 0.3
                {
                    var twistChainConstraint = originalRigConstraint as TwistChainConstraint;
                    var constraint = GetPreviewTransform(twistChainConstraint.transform).GetComponent<TwistChainConstraint>();
                    if (constraint != null)
                    {
                        constraint.data.root = GetPreviewTransform(twistChainConstraint.data.root);
                        constraint.data.tip = GetPreviewTransform(twistChainConstraint.data.tip);
                        constraint.data.rootTarget = GetPreviewTransform(twistChainConstraint.data.rootTarget);
                        constraint.data.tipTarget = GetPreviewTransform(twistChainConstraint.data.tipTarget);
                    }
                }
                #endregion
#endif
                else
                {
                    Debug.LogErrorFormat("<color=blue>[Very Animation]</color>Unknown IRigConstraint. {0}", originalRigConstraint);
                }
            }

            #region VeryAnimationRig
            {
                var vaRig = rig.GetComponent<VeryAnimationRig>();
                var originalVaRig = originalRig.GetComponent<VeryAnimationRig>();
                if (vaRig != null && originalVaRig != null)
                {
                    vaRig.basePoseLeftHand.constraint = GetPreviewTransform(originalVaRig.basePoseLeftHand.constraint);
                    vaRig.basePoseRightHand.constraint = GetPreviewTransform(originalVaRig.basePoseRightHand.constraint);
                    vaRig.basePoseLeftFoot.constraint = GetPreviewTransform(originalVaRig.basePoseLeftFoot.constraint);
                    vaRig.basePoseRightFoot.constraint = GetPreviewTransform(originalVaRig.basePoseRightFoot.constraint);
                }
            }
            #endregion
        }
    }
}
#endif
