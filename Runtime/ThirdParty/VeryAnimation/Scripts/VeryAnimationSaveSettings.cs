using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VeryAnimation
{
    [DisallowMultipleComponent]
    public class VeryAnimationSaveSettings : MonoBehaviour
    {
#if !UNITY_EDITOR
        private void Awake()
        {
            Destroy(this);
        }
#else
        private void Awake()
        {
            enabled = false;
        }

        #region Bones
        [HideInInspector]
        public string[] bonePaths;
        [HideInInspector]
        public int[] showBones;
        [HideInInspector]
        public int[] foldoutBones;
        [HideInInspector]
        public int[] mirrorBones;
        [Serializable]
        public class MirrorBlendShape
        {
            public SkinnedMeshRenderer renderer;
            public string[] names;
            public string[] mirrorNames;
        }
        [HideInInspector]
        public MirrorBlendShape[] mirrorBlendShape;
        #endregion

        #region AnimatorIK
        [Serializable]
        public class AnimatorIKData
        {
            public bool enable;
            public bool _fixed;
            public bool autoRotation;
            public int spaceType;
            public GameObject parent;
            public Vector3 position;
            public Quaternion rotation;
            //Head
            public float headWeight = 1f;
            public float eyesWeight = 0f;
            //Swivel
            public float swivelRotation;
            public Vector3 swivelPosition;

            //Path
            public string parentPath;
        }
        [HideInInspector]
        public AnimatorIKData[] animatorIkData;
        #endregion

        #region OriginalIK
        [Serializable]
        public class OriginalIKData
        {
            public bool enable;
            public bool _fixed;
            public bool autoRotation;
            public int spaceType;
            public GameObject parent;
            public Vector3 position;
            public Quaternion rotation;
            public float swivel;

            public string name;
            public int solverType;
            public bool resetRotations;  //CCD
            public int level;           //CCD
            public float limbDirection;   //Limb
            [Serializable]
            public class JointData
            {
                public GameObject bone;
                public float weight;

                //Path
                public string bonePath;
            }
            public List<JointData> joints;

            //Path
            public string parentPath;
        }
        [HideInInspector]
        public OriginalIKData[] originalIkData;
        #endregion

        #region Selection
        [Serializable]
        public class SelectionData
        {
            public string name;
            public GameObject[] bones;
            public HumanBodyBones[] virtualBones;

            public int count { get { return (bones != null ? bones.Length : 0) + (virtualBones != null ? virtualBones.Length : 0); } }

            //Path
            public string[] bonePaths;
        }
        [HideInInspector]
        public SelectionData[] selectionData;
        #endregion

        #region Animation
        [HideInInspector]
        public AnimationClip lastSelectAnimationClip;
        #endregion

        #region HandPose
        [Serializable]
        public class HandPoseSet
        {
            public string name;
            public string[] musclePropertyNames;
            public float[] muscleValues;
        }
        [HideInInspector]
        public HandPoseSet[] handPoseList;
        #endregion

        #region BlendShape
        [Serializable]
        public class BlendShapeSet
        {
            [Serializable]
            public class BlendShapeData
            {
                public string[] names;
                public float[] weights;
            }

            public string name;
            public string[] blendShapePaths;
            public BlendShapeData[] blendShapeValues;
        }
        [HideInInspector]
        public BlendShapeSet[] blendShapeList;
        #endregion
#endif
    }
}
