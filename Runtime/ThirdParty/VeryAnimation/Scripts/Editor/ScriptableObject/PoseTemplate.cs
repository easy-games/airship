using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

namespace VeryAnimation
{
    public class PoseTemplate : ScriptableObject
    {
        public bool isHuman;
        #region HumanPose
        public bool haveRootT;
        public Vector3 rootT;
        public bool haveRootQ;
        public Quaternion rootQ;
        public string[] musclePropertyNames;
        public float[] muscleValues;
        public VeryAnimation.AnimatorTDOFIndex[] tdofIndices;
        public Vector3[] tdofValues;
        public VeryAnimation.AnimatorIKIndex[] ikIndices;
        [Serializable]
        public struct IKData
        {
            public Vector3 position;
            public Quaternion rotation;
        }
        public IKData[] ikValues;
        #endregion
        #region GenerincPose
        public string[] transformPaths;
        [Serializable]
        public struct TransformData
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }
        public TransformData[] transformValues;
        #endregion
        #region BlendShape
        public string[] blendShapePaths;
        [Serializable]
        public struct BlendShapeData
        {
            public string[] names;
            public float[] weights;
        }
        public BlendShapeData[] blendShapeValues;
        #endregion

        public void Reset()
        {
            isHuman = false;
            #region HumanPose
            haveRootT = false;
            rootT = Vector3.zero;
            haveRootQ = false;
            rootQ = Quaternion.identity;
            musclePropertyNames = null;
            muscleValues = null;
            tdofIndices = null;
            tdofValues = null;
            ikIndices = null;
            ikValues = null;
            #endregion
            #region GenerincPose
            transformPaths = null;
            transformValues = null;
            #endregion
            #region BlendShape
            blendShapePaths = null;
            blendShapeValues = null;
            #endregion
        }
    }
}
