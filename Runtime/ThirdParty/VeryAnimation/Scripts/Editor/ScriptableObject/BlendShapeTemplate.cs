using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

namespace VeryAnimation
{
    public class BlendShapeTemplate : ScriptableObject
    {
        [Serializable]
        public class BlendShapeData
        {
            public string name;
            public string[] blendShapePaths;
            public PoseTemplate.BlendShapeData[] blendShapeValues;

            public PoseTemplate GetPoseTemplate()
            {
                var poseTemplate = ScriptableObject.CreateInstance<PoseTemplate>();
                poseTemplate.name = name;
                {
                    poseTemplate.blendShapePaths = new string[blendShapePaths.Length];
                    blendShapePaths.CopyTo(poseTemplate.blendShapePaths, 0);
                    poseTemplate.blendShapeValues = new PoseTemplate.BlendShapeData[blendShapeValues.Length];
                    for (int i = 0; i < blendShapeValues.Length; i++)
                    {
                        poseTemplate.blendShapeValues[i].names = new string[blendShapeValues[i].names.Length];
                        blendShapeValues[i].names.CopyTo(poseTemplate.blendShapeValues[i].names, 0);
                        poseTemplate.blendShapeValues[i].weights = new float[blendShapeValues[i].weights.Length];
                        blendShapeValues[i].weights.CopyTo(poseTemplate.blendShapeValues[i].weights, 0);
                    }
                }
                return poseTemplate;
            }
        }

        public List<BlendShapeData> list;

        public void Add(PoseTemplate srcPoseTemplate)
        {
            var blendShape = new BlendShapeData();
            blendShape.name = srcPoseTemplate.name;
            {
                blendShape.blendShapePaths = new string[srcPoseTemplate.blendShapePaths.Length];
                srcPoseTemplate.blendShapePaths.CopyTo(blendShape.blendShapePaths, 0);
                blendShape.blendShapeValues = new PoseTemplate.BlendShapeData[srcPoseTemplate.blendShapeValues.Length];
                for (int i = 0; i < srcPoseTemplate.blendShapeValues.Length; i++)
                {
                    blendShape.blendShapeValues[i].names = new string[srcPoseTemplate.blendShapeValues[i].names.Length];
                    srcPoseTemplate.blendShapeValues[i].names.CopyTo(blendShape.blendShapeValues[i].names, 0);
                    blendShape.blendShapeValues[i].weights = new float[srcPoseTemplate.blendShapeValues[i].weights.Length];
                    srcPoseTemplate.blendShapeValues[i].weights.CopyTo(blendShape.blendShapeValues[i].weights, 0);
                }
            }
            if (list == null)
                list = new List<BlendShapeData>();
            list.Add(blendShape);
        }
    }
}
