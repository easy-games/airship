using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VeryAnimation
{
    public class HandPoseTemplate : ScriptableObject
    {
        [Serializable]
        public class HandPoseData
        {
            public string name;
            public string[] musclePropertyNames;
            public float[] muscleValues;

            public PoseTemplate GetPoseTemplate(MusclePropertyName musclePropertyName)
            {
                var poseTemplate = ScriptableObject.CreateInstance<PoseTemplate>();
                poseTemplate.name = name;
                {
                    poseTemplate.musclePropertyNames = new string[musclePropertyNames.Length];
                    musclePropertyNames.CopyTo(poseTemplate.musclePropertyNames, 0);
                    poseTemplate.muscleValues = new float[muscleValues.Length];
                    muscleValues.CopyTo(poseTemplate.muscleValues, 0);
                }
                poseTemplate.isHuman = true;
                return poseTemplate;
            }
        }

        public List<HandPoseData> list;

        public void Add(MusclePropertyName musclePropertyName, PoseTemplate srcPoseTemplate)
        {
            var handPose = new HandPoseData();
            handPose.name = srcPoseTemplate.name;
            {
                var beginMuscle = HumanTrait.MuscleFromBone((int)HumanBodyBones.LeftThumbProximal, 2);
                var endMuscle = HumanTrait.MuscleFromBone((int)HumanBodyBones.LeftLittleDistal, 2);
                var muscleDic = new Dictionary<string, float>();
                for (int muscle = beginMuscle; muscle <= endMuscle; muscle++)
                {
                    var index = ArrayUtility.IndexOf(srcPoseTemplate.musclePropertyNames, musclePropertyName.PropertyNames[muscle]);
                    if (index < 0) continue;
                    muscleDic.Add(srcPoseTemplate.musclePropertyNames[index], srcPoseTemplate.muscleValues[index]);
                }
                handPose.musclePropertyNames = muscleDic.Keys.ToArray();
                handPose.muscleValues = muscleDic.Values.ToArray();
            }
            if (list == null)
                list = new List<HandPoseData>();
            list.Add(handPose);
        }
    }
}
