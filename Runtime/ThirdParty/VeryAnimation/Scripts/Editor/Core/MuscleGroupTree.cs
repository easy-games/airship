using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace VeryAnimation
{
    [Serializable]
    public class MuscleGroupTree
    {
        private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
        private VeryAnimation va { get { return VeryAnimation.instance; } }
        private VeryAnimationEditorWindow vae { get { return VeryAnimationEditorWindow.instance; } }

        private enum MuscleGroupMode
        {
            Category,
            Part,
            Total,
        }
        private static readonly string[] MuscleGroupModeString =
        {
            MuscleGroupMode.Category.ToString(),
            MuscleGroupMode.Part.ToString(),
        };

        private MuscleGroupMode muscleGroupMode;

        private class MuscleInfo
        {
            public HumanBodyBones hi;
            public int dof;
            public float scale = 1f;
        }
        private class MuscleGroupNode
        {
            public string name;
            public string mirrorName;
            public bool foldout;
            public int dof = -1;
            public MuscleInfo[] infoList;
            public MuscleGroupNode[] children;
        }
        private MuscleGroupNode[] muscleGroupNode;
        private Dictionary<MuscleGroupNode, int> muscleGroupTreeTable;

        [SerializeField]
        private float[] muscleGroupValues;

        public MuscleGroupTree()
        {
            #region MuscleGroupNode
            {
                muscleGroupNode = new MuscleGroupNode[]
                {
#region Category
                    new MuscleGroupNode() { name = MuscleGroupMode.Category.ToString(),
                        children = new MuscleGroupNode[]
                        {
#region Open Close
                            new MuscleGroupNode() { name = "Open Close", dof = 2,
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.Spine, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.Chest, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.UpperChest, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.Neck, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.Head, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftShoulder, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftUpperArm, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLowerArm, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftHand, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightShoulder, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightUpperArm, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLowerArm, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightHand, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftUpperLeg, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLowerLeg, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftFoot, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightUpperLeg, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLowerLeg, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightFoot, dof = 2 },
                                },
                                children = new MuscleGroupNode[]
                                {
                                    new MuscleGroupNode() { name = "Head", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.Head, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.Neck, dof = 2 },
                                        },
                                    },
                                    new MuscleGroupNode() { name = "Body", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.UpperChest, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.Chest, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.Spine, dof = 2 },
                                        },
                                    },
                                    new MuscleGroupNode() { name = "Left Arm", mirrorName = "Open Close/Right Arm", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftShoulder, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftUpperArm, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLowerArm, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftHand, dof = 2 },
                                        },
                                    },
                                    new MuscleGroupNode() { name = "Right Arm", mirrorName = "Open Close/Left Arm", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightShoulder, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightUpperArm, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLowerArm, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightHand, dof = 2 },
                                        },
                                    },
                                    new MuscleGroupNode() { name = "Left Leg", mirrorName = "Open Close/Right Leg", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftUpperLeg, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLowerLeg, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftFoot, dof = 2 },
                                        },
                                    },
                                    new MuscleGroupNode() { name = "Right Leg", mirrorName = "Open Close/Left Leg", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightUpperLeg, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLowerLeg, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightFoot, dof = 2 },
                                        },
                                    },
                                },
                            },
#endregion
#region Left Right
                            new MuscleGroupNode() { name = "Left Right", dof = 1,
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.Head, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.Neck, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.UpperChest, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.Chest, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.Spine, dof = 1 },
                                },
                            },
#endregion
#region Roll Left Right
                            new MuscleGroupNode() { name = "Roll Left Right", dof = 0,
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.Head, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.Neck, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.UpperChest, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.Chest, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.Spine, dof = 0 },
                                },
                            },
#endregion
#region In Out
                            new MuscleGroupNode() { name = "In Out", dof = 1,
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.LeftShoulder, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftUpperArm, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftHand, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightShoulder, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightUpperArm, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightHand, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftUpperLeg, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftFoot, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightUpperLeg, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightFoot, dof = 1 },
                                },
                                children = new MuscleGroupNode[]
                                {
                                    new MuscleGroupNode() { name = "Left Arm", mirrorName = "In Out/Right Arm", dof = 1,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftShoulder, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftUpperArm, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftHand, dof = 1 },
                                        },
                                    },
                                    new MuscleGroupNode() { name = "Right Arm", mirrorName = "In Out/Left Arm", dof = 1,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightShoulder, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightUpperArm, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightHand, dof = 1 },
                                        },
                                    },
                                    new MuscleGroupNode() { name = "Left Leg", mirrorName = "In Out/Right Leg", dof = 1,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftUpperLeg, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftFoot, dof = 1 },
                                        },
                                    },
                                    new MuscleGroupNode() { name = "Right Leg", mirrorName = "In Out/Left Leg", dof = 1,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightUpperLeg, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightFoot, dof = 1 },
                                        },
                                    },
                                },
                            },
#endregion
#region Roll In Out
                            new MuscleGroupNode() { name = "Roll In Out", dof = 0,
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.LeftUpperArm, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLowerArm, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightUpperArm, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLowerArm, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftUpperLeg, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLowerLeg, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightUpperLeg, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLowerLeg, dof = 0 },
                                },
                                children = new MuscleGroupNode[]
                                {
                                    new MuscleGroupNode() { name = "Left Arm", mirrorName = "Roll In Out/Right Arm", dof = 0,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftUpperArm, dof = 0 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLowerArm, dof = 0 },
                                        },
                                    },
                                    new MuscleGroupNode() { name = "Right Arm", mirrorName = "Roll In Out/Left Arm", dof = 0,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightUpperArm, dof = 0 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLowerArm, dof = 0 },
                                        },
                                    },
                                    new MuscleGroupNode() { name = "Left Leg", mirrorName = "Roll In Out/Right Leg", dof = 0,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftUpperLeg, dof = 0 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLowerLeg, dof = 0 },
                                        },
                                    },
                                    new MuscleGroupNode() { name = "Right Leg", mirrorName = "Roll In Out/Left Leg", dof = 0,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightUpperLeg, dof = 0 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLowerLeg, dof = 0 },
                                        },
                                    },
                                },
                            },
#endregion
#region Finger Open Close
                            new MuscleGroupNode() { name = "Finger Open Close", dof = 2,
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.LeftThumbProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftThumbIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftThumbDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftIndexProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftIndexIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftIndexDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftMiddleProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftMiddleIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftMiddleDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftRingProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftRingIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftRingDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLittleProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLittleIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLittleDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightThumbProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightThumbIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightThumbDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightIndexProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightIndexIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightIndexDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightMiddleProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightMiddleIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightMiddleDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightRingProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightRingIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightRingDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLittleProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLittleIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLittleDistal, dof = 2 },
                                },
                                children = new MuscleGroupNode[]
                                {
                                    new MuscleGroupNode() { name = "Left Finger", mirrorName = "Finger Open Close/Right Finger", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftThumbProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftThumbIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftThumbDistal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftIndexProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftIndexIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftIndexDistal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftMiddleProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftMiddleIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftMiddleDistal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftRingProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftRingIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftRingDistal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLittleProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLittleIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLittleDistal, dof = 2 },
                                        },
                                        children = new MuscleGroupNode[]
                                        {
                                            new MuscleGroupNode() { name = "Left Thumb", mirrorName = "Finger Open Close/Right Finger/Right Thumb", dof = 2,
                                                infoList = new MuscleInfo[]
                                                {
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftThumbProximal, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftThumbIntermediate, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftThumbDistal, dof = 2 },
                                                },
                                            },
                                            new MuscleGroupNode() { name = "Left Index", mirrorName = "Finger Open Close/Right Finger/Right Index", dof = 2,
                                                infoList = new MuscleInfo[]
                                                {
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftIndexProximal, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftIndexIntermediate, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftIndexDistal, dof = 2 },
                                                },
                                            },
                                            new MuscleGroupNode() { name = "Left Middle", mirrorName = "Finger Open Close/Right Finger/Right Middle", dof = 2,
                                                infoList = new MuscleInfo[]
                                                {
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftMiddleProximal, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftMiddleIntermediate, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftMiddleDistal, dof = 2 },
                                                },
                                            },
                                            new MuscleGroupNode() { name = "Left Ring", mirrorName = "Finger Open Close/Right Finger/Right Ring", dof = 2,
                                                infoList = new MuscleInfo[]
                                                {
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftRingProximal, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftRingIntermediate, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftRingDistal, dof = 2 },
                                                },
                                            },
                                            new MuscleGroupNode() { name = "Left Little", mirrorName = "Finger Open Close/Right Finger/Right Little", dof = 2,
                                                infoList = new MuscleInfo[]
                                                {
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftLittleProximal, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftLittleIntermediate, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.LeftLittleDistal, dof = 2 },
                                                },
                                            },
                                        },
                                    },
                                    new MuscleGroupNode() { name = "Right Finger", mirrorName = "Finger Open Close/Left Finger", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightThumbProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightThumbIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightThumbDistal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightIndexProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightIndexIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightIndexDistal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightMiddleProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightMiddleIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightMiddleDistal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightRingProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightRingIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightRingDistal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLittleProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLittleIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLittleDistal, dof = 2 },
                                        },
                                        children = new MuscleGroupNode[]
                                        {
                                            new MuscleGroupNode() { name = "Right Thumb", mirrorName = "Finger Open Close/Left Finger/Left Thumb", dof = 2,
                                                infoList = new MuscleInfo[]
                                                {
                                                    new MuscleInfo() { hi = HumanBodyBones.RightThumbProximal, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.RightThumbIntermediate, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.RightThumbDistal, dof = 2 },
                                                },
                                            },
                                            new MuscleGroupNode() { name = "Right Index", mirrorName = "Finger Open Close/Left Finger/Left Index", dof = 2,
                                                infoList = new MuscleInfo[]
                                                {
                                                    new MuscleInfo() { hi = HumanBodyBones.RightIndexProximal, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.RightIndexIntermediate, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.RightIndexDistal, dof = 2 },
                                                },
                                            },
                                            new MuscleGroupNode() { name = "Right Middle", mirrorName = "Finger Open Close/Left Finger/Left Middle", dof = 2,
                                                infoList = new MuscleInfo[]
                                                {
                                                    new MuscleInfo() { hi = HumanBodyBones.RightMiddleProximal, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.RightMiddleIntermediate, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.RightMiddleDistal, dof = 2 },
                                                },
                                            },
                                            new MuscleGroupNode() { name = "Right Ring", mirrorName = "Finger Open Close/Left Finger/Left Ring", dof = 2,
                                                infoList = new MuscleInfo[]
                                                {
                                                    new MuscleInfo() { hi = HumanBodyBones.RightRingProximal, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.RightRingIntermediate, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.RightRingDistal, dof = 2 },
                                                },
                                            },
                                            new MuscleGroupNode() { name = "Right Little", mirrorName = "Finger Open Close/Left Finger/Left Little", dof = 2,
                                                infoList = new MuscleInfo[]
                                                {
                                                    new MuscleInfo() { hi = HumanBodyBones.RightLittleProximal, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.RightLittleIntermediate, dof = 2 },
                                                    new MuscleInfo() { hi = HumanBodyBones.RightLittleDistal, dof = 2 },
                                                },
                                            },
                                        },
                                    },
                                },
                            },
#endregion
#region Finger In Out
                            new MuscleGroupNode() { name = "Finger In Out", dof = 1,
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.LeftThumbProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftIndexProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftMiddleProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftRingProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLittleProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightThumbProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightIndexProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightMiddleProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightRingProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLittleProximal, dof = 1 },
                                },
                                children = new MuscleGroupNode[]
                                {
                                    new MuscleGroupNode() { name = "Left Finger", mirrorName = "Finger In Out/Right Finger", dof = 1,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftThumbProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftIndexProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftMiddleProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftRingProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLittleProximal, dof = 1 },
                                        },
                                    },
                                    new MuscleGroupNode() { name = "Right Finger", mirrorName = "Finger In Out/Left Finger", dof = 1,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightThumbProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightIndexProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightMiddleProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightRingProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLittleProximal, dof = 1 },
                                        },
                                    },
                                },
                            },
#endregion
                        },
                    },
#endregion
#region Part
                    new MuscleGroupNode() { name = MuscleGroupMode.Category.ToString(),
                        children = new MuscleGroupNode[]
                        {
#region Face
                            new MuscleGroupNode() { name = "Face",
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.LeftEye, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftEye, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightEye, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightEye, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.Jaw, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.Jaw, dof = 1 },
                                },
                                children = new MuscleGroupNode[]
                                {
#region Eyes Down Up
                                    new MuscleGroupNode() { name = "Eyes Down Up",
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftEye, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightEye, dof = 2 },
                                        },
                                    },
#endregion
#region Eyes Left Right
                                    new MuscleGroupNode() { name = "Eyes Left Right",
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftEye, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightEye, dof = 1, scale = -1f },
                                        },
                                    },
#endregion
#region Jaw
                                    new MuscleGroupNode() { name = "Jaw",
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.Jaw, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.Jaw, dof = 1 },
                                        },
                                    },
#endregion
                                },
                            },
#endregion
#region Head
                            new MuscleGroupNode() { name = "Head",
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.Neck, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.Neck, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.Neck, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.Head, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.Head, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.Head, dof = 0 },
                                },
                                children = new MuscleGroupNode[]
                                {
#region Open Close
                                    new MuscleGroupNode() { name = "Open Close", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.Head, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.Neck, dof = 2 },
                                        },
                                    },
#endregion
#region Left Right
                                    new MuscleGroupNode() { name = "Left Right", dof = 1,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.Head, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.Neck, dof = 1 },
                                        },
                                    },
#endregion
#region Roll Left Right
                                    new MuscleGroupNode() { name = "Roll Left Right", dof = 0,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.Head, dof = 0 },
                                            new MuscleInfo() { hi = HumanBodyBones.Neck, dof = 0 },
                                        },
                                    },
#endregion
                                },
                            },
#endregion
#region Body
                            new MuscleGroupNode() { name = "Body",
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.Spine, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.Spine, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.Spine, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.Chest, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.Chest, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.Chest, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.UpperChest, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.UpperChest, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.UpperChest, dof = 0 },
                                },
                                children = new MuscleGroupNode[]
                                {
#region Open Close
                                    new MuscleGroupNode() { name = "Open Close", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.UpperChest, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.Chest, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.Spine, dof = 2 },
                                        },
                                    },
#endregion
#region Left Right
                                    new MuscleGroupNode() { name = "Left Right", dof = 1,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.UpperChest, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.Chest, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.Spine, dof = 1 },
                                        },
                                    },
#endregion
#region Roll Left Right
                                    new MuscleGroupNode() { name = "Roll Left Right", dof = 0,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.UpperChest, dof = 0 },
                                            new MuscleInfo() { hi = HumanBodyBones.Chest, dof = 0 },
                                            new MuscleInfo() { hi = HumanBodyBones.Spine, dof = 0 },
                                        },
                                    },
#endregion
                                },
                            },
#endregion
#region Left Arm
                            new MuscleGroupNode() { name = "Left Arm", mirrorName = "Right Arm",
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.LeftShoulder, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftShoulder, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftUpperArm, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftUpperArm, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftUpperArm, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLowerArm, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLowerArm, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftHand, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftHand, dof = 1 },
                                },
                                children = new MuscleGroupNode[]
                                {
#region Open Close
                                    new MuscleGroupNode() { name = "Open Close", mirrorName = "Right Arm/Open Close", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftShoulder, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftUpperArm, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLowerArm, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftHand, dof = 2 },
                                        },
                                    },
#endregion
#region In Out
                                    new MuscleGroupNode() { name = "In Out", mirrorName = "Right Arm/In Out", dof = 1,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftShoulder, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftUpperArm, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftHand, dof = 1 },
                                        },
                                    },
#endregion
#region Roll In Out
                                    new MuscleGroupNode() { name = "Roll In Out", mirrorName = "Right Arm/Roll In Out", dof = 0,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftUpperArm, dof = 0 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLowerArm, dof = 0 },
                                        },
                                    },
#endregion
                                },
                            },
#endregion
#region Right Arm
                            new MuscleGroupNode() { name = "Right Arm", mirrorName = "Left Arm",
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.RightShoulder, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightShoulder, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightUpperArm, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightUpperArm, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightUpperArm, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLowerArm, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLowerArm, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightHand, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightHand, dof = 1 },
                                },
                                children = new MuscleGroupNode[]
                                {
#region Open Close
                                    new MuscleGroupNode() { name = "Open Close", mirrorName = "Left Arm/Open Close", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightShoulder, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightUpperArm, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLowerArm, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightHand, dof = 2 },
                                        },
                                    },
#endregion
#region In Out
                                    new MuscleGroupNode() { name = "In Out", mirrorName = "Left Arm/In Out", dof = 1,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightShoulder, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightUpperArm, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightHand, dof = 1 },
                                        },
                                    },
#endregion
#region Roll In Out
                                    new MuscleGroupNode() { name = "Roll In Out", mirrorName = "Left Arm/Roll In Out", dof = 0,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightUpperArm, dof = 0 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLowerArm, dof = 0 },
                                        },
                                    },
#endregion
                                },
                            },
#endregion
#region Left Leg
                            new MuscleGroupNode() { name = "Left Leg", mirrorName = "Right Leg",
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.LeftUpperLeg, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftUpperLeg, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftUpperLeg, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLowerLeg, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLowerLeg, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftFoot, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftFoot, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftToes, dof = 2 },
                                },
                                children = new MuscleGroupNode[]
                                {
#region Open Close
                                    new MuscleGroupNode() { name = "Open Close", mirrorName = "Right Leg/Open Close", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftUpperLeg, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLowerLeg, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftFoot, dof = 2 },
                                        },
                                    },
#endregion
#region In Out
                                    new MuscleGroupNode() { name = "In Out", mirrorName = "Right Leg/In Out", dof = 1,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftUpperLeg, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftFoot, dof = 1 },
                                        },
                                    },
#endregion
#region Roll In Out
                                    new MuscleGroupNode() { name = "Roll In Out", mirrorName = "Right Leg/Roll In Out", dof = 0,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftUpperLeg, dof = 0 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLowerLeg, dof = 0 },
                                        },
                                    },
#endregion
#region Toes
                                    new MuscleGroupNode() { name = "Toes", mirrorName = "Right Leg/Toes", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftToes, dof = 2 },
                                        },
                                    },
#endregion
                                },
                            },
#endregion
#region Right Leg
                            new MuscleGroupNode() { name = "Right Leg", mirrorName = "Left Leg",
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.RightUpperLeg, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightUpperLeg, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightUpperLeg, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLowerLeg, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLowerLeg, dof = 0 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightFoot, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightFoot, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightToes, dof = 2 },
                                },
                                children = new MuscleGroupNode[]
                                {
#region Open Close
                                    new MuscleGroupNode() { name = "Open Close", mirrorName = "Left Leg/Open Close", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightUpperLeg, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLowerLeg, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightFoot, dof = 2 },
                                        },
                                    },
#endregion
#region In Out
                                    new MuscleGroupNode() { name = "In Out", mirrorName = "Left Leg/In Out", dof = 1,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightUpperLeg, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightFoot, dof = 1 },
                                        },
                                    },
#endregion
#region Roll In Out
                                    new MuscleGroupNode() { name = "Roll In Out", mirrorName = "Left Leg/Roll In Out", dof = 0,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightUpperLeg, dof = 0 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLowerLeg, dof = 0 },
                                        },
                                    },
#endregion
#region Toes
                                    new MuscleGroupNode() { name = "Toes", mirrorName = "Left Leg/Toes", dof = 2,
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightToes, dof = 2 },
                                        },
                                    },
#endregion
                                },
                            },
#endregion
#region Left Finger
                            new MuscleGroupNode() { name = "Left Finger", mirrorName = "Right Finger",
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.LeftThumbProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftThumbProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftThumbIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftThumbDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftIndexProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftIndexProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftIndexIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftIndexDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftMiddleProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftMiddleProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftMiddleIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftMiddleDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftRingProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftRingProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftRingIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftRingDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLittleProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLittleProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLittleIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.LeftLittleDistal, dof = 2 },
                                },
                                children = new MuscleGroupNode[]
                                {
#region Left Thumb
                                    new MuscleGroupNode() { name = "Left Thumb", mirrorName = "Right Finger/Right Thumb",
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftThumbProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftThumbProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftThumbIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftThumbDistal, dof = 2 },
                                        },
                                    },
#endregion
#region Left Index
                                    new MuscleGroupNode() { name = "Left Index", mirrorName = "Right Finger/Right Index",
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftIndexProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftIndexProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftIndexIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftIndexDistal, dof = 2 },
                                        },
                                    },
#endregion
#region Left Middle
                                    new MuscleGroupNode() { name = "Left Middle", mirrorName = "Right Finger/Right Middle",
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftMiddleProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftMiddleProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftMiddleIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftMiddleDistal, dof = 2 },
                                        },
                                    },
#endregion
#region Left Ring
                                    new MuscleGroupNode() { name = "Left Ring", mirrorName = "Right Finger/Right Ring",
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftRingProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftRingProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftRingIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftRingDistal, dof = 2 },
                                        },
                                    },
#endregion
#region Left Little
                                    new MuscleGroupNode() { name = "Left Little", mirrorName = "Right Finger/Right Little",
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLittleProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLittleProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLittleIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.LeftLittleDistal, dof = 2 },
                                        },
                                    },
#endregion
                                },
                            },
#endregion
#region Right Finger
                            new MuscleGroupNode() { name = "Right Finger", mirrorName = "Left Finger",
                                infoList = new MuscleInfo[]
                                {
                                    new MuscleInfo() { hi = HumanBodyBones.RightThumbProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightThumbProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightThumbIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightThumbDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightIndexProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightIndexProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightIndexIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightIndexDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightMiddleProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightMiddleProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightMiddleIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightMiddleDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightRingProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightRingProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightRingIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightRingDistal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLittleProximal, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLittleProximal, dof = 1 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLittleIntermediate, dof = 2 },
                                    new MuscleInfo() { hi = HumanBodyBones.RightLittleDistal, dof = 2 },
                                },
                                children = new MuscleGroupNode[]
                                {
#region Right Thumb
                                    new MuscleGroupNode() { name = "Right Thumb", mirrorName = "Left Finger/Left Thumb",
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightThumbProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightThumbProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightThumbIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightThumbDistal, dof = 2 },
                                        },
                                    },
#endregion
#region Right Index
                                    new MuscleGroupNode() { name = "Right Index", mirrorName = "Left Finger/Left Index",
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightIndexProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightIndexProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightIndexIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightIndexDistal, dof = 2 },
                                        },
                                    },
#endregion
#region Right Middle
                                    new MuscleGroupNode() { name = "Right Middle", mirrorName = "Left Finger/Left Middle",
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightMiddleProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightMiddleProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightMiddleIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightMiddleDistal, dof = 2 },
                                        },
                                    },
#endregion
#region Right Ring
                                    new MuscleGroupNode() { name = "Right Ring", mirrorName = "Left Finger/Left Ring",
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightRingProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightRingProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightRingIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightRingDistal, dof = 2 },
                                        },
                                    },
#endregion
#region Right Little
                                    new MuscleGroupNode() { name = "Right Little", mirrorName = "Left Finger/Left Little",
                                        infoList = new MuscleInfo[]
                                        {
                                            new MuscleInfo() { hi = HumanBodyBones.RightLittleProximal, dof = 1 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLittleProximal, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLittleIntermediate, dof = 2 },
                                            new MuscleInfo() { hi = HumanBodyBones.RightLittleDistal, dof = 2 },
                                        },
                                    },
#endregion
                                },
                            },
#endregion
                        },
                    },
#endregion
                };

                {
                    muscleGroupTreeTable = new Dictionary<MuscleGroupNode, int>();
                    int counter = 0;
                    Action<MuscleGroupNode> AddTable = null;
                    AddTable = (mg) =>
                    {
                        muscleGroupTreeTable.Add(mg, counter++);
                        if (mg.children != null)
                        {
                            foreach (var child in mg.children)
                            {
                                AddTable(child);
                            }
                        }
                    };
                    foreach (var node in muscleGroupNode)
                    {
                        AddTable(node);
                    }

                    muscleGroupValues = new float[muscleGroupTreeTable.Count];
                }
            }
            #endregion
        }

        public void LoadEditorPref()
        {
            muscleGroupMode = (MuscleGroupMode)EditorPrefs.GetInt("VeryAnimation_MuscleGroupMode", 0);
        }
        public void SaveEditorPref()
        {
            EditorPrefs.SetInt("VeryAnimation_MuscleGroupMode", (int)muscleGroupMode);
        }

        public void MuscleGroupToolbarGUI()
        {
            EditorGUI.BeginChangeCheck();
            var m = (MuscleGroupMode)GUILayout.Toolbar((int)muscleGroupMode, MuscleGroupModeString, EditorStyles.miniButton);
            if (EditorGUI.EndChangeCheck())
            {
                muscleGroupMode = m;
            }
        }

        private struct MuscleValue
        {
            public int muscleIndex;
            public float value;
        }
        public void MuscleGroupTreeGUI()
        {
            RowCount = 0;

            var e = Event.current;

            EditorGUILayout.BeginVertical(vaw.guiStyleSkinBox);
            {
                var mgRoot = muscleGroupNode[(int)muscleGroupMode];

                #region Top
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Select All", GUILayout.Width(100)))
                    {
                        if (Shortcuts.IsKeyControl(e) || e.shift)
                        {
                            var combineGoList = new HashSet<GameObject>(va.selectionGameObjects);
                            var combineVirtualList = new HashSet<HumanBodyBones>();
                            if (va.selectionHumanVirtualBones != null)
                                combineVirtualList.UnionWith(va.selectionHumanVirtualBones);
                            combineGoList.Add(vaw.gameObject);
                            for (int hi = 0; hi < (int)HumanBodyBones.LastBone; hi++)
                            {
                                if (va.humanoidBones[hi] != null)
                                    combineGoList.Add(va.humanoidBones[hi]);
                                else if (VeryAnimation.HumanVirtualBones[hi] != null)
                                    combineVirtualList.Add((HumanBodyBones)hi);
                            }
                            va.SelectGameObjects(combineGoList.ToArray(), combineVirtualList.ToArray());
                        }
                        else
                        {
                            var combineGoList = new List<GameObject>();
                            var combineVirtualList = new List<HumanBodyBones>();
                            combineGoList.Add(vaw.gameObject);
                            for (int hi = 0; hi < (int)HumanBodyBones.LastBone; hi++)
                            {
                                if (va.humanoidBones[hi] != null)
                                    combineGoList.Add(va.humanoidBones[hi]);
                                else if (VeryAnimation.HumanVirtualBones[hi] != null)
                                    combineVirtualList.Add((HumanBodyBones)hi);
                            }
                            Selection.activeGameObject = vaw.gameObject;
                            va.SelectGameObjects(combineGoList.ToArray(), combineVirtualList.ToArray());
                        }
                    }
                    GUILayout.Space(10);
                    if (GUILayout.Button("Root", GUILayout.Width(100)))
                    {
                        if (Shortcuts.IsKeyControl(e) || e.shift)
                        {
                            var combineGoList = new List<GameObject>(va.selectionGameObjects);
                            combineGoList.Add(vaw.gameObject);
                            va.SelectGameObjects(combineGoList.ToArray(), va.selectionHumanVirtualBones != null ? va.selectionHumanVirtualBones.ToArray() : null);
                        }
                        else
                        {
                            va.SelectGameObject(vaw.gameObject);
                        }
                    }
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Reset All", GUILayout.Width(100)))
                    {
                        Undo.RecordObject(vae, "Reset All Muscle Group");
                        foreach (var root in mgRoot.children)
                        {
                            List<MuscleValue> muscles = new List<MuscleValue>();
                            SetMuscleGroupValue(root, 0f, muscles);
                            SetAnimationCurveMuscleValues(muscles);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                #endregion

                EditorGUILayout.Space();

                #region Muscle
                {
                    int maxLevel = 0;
                    foreach (var root in mgRoot.children)
                    {
                        maxLevel = Math.Max(GetTreeLevel(root, 0), maxLevel);
                    }
                    foreach (var root in mgRoot.children)
                    {
                        MuscleGroupTreeNodeGUI(root, 0, maxLevel);
                    }
                }
                #endregion
            }
            EditorGUILayout.EndVertical();
        }
        #region MuscleGroupTreeGUI
        private int RowCount = 0;
        private const int IndentWidth = 15;
        private int GetTreeLevel(MuscleGroupNode mg, int level)
        {
            if (mg.foldout)
            {
                if (mg.children != null && mg.children.Length > 0)
                {
                    int tmp = level;
                    foreach (var child in mg.children)
                    {
                        tmp = Math.Max(tmp, GetTreeLevel(child, level + 1));
                    }
                    level = tmp;
                }
                else if (mg.infoList != null && mg.infoList.Length > 0)
                {
                    level++;
                }
            }
            return level;
        }
        private MuscleGroupNode GetMirrorNode(MuscleGroupNode mg)
        {
            if (string.IsNullOrEmpty(mg.mirrorName))
                return null;
            var splits = mg.mirrorName.Split('/');
            MuscleGroupNode mirrorNode = muscleGroupNode[(int)muscleGroupMode];
            for (int i = 0; i < splits.Length; i++)
            {
                var index = ArrayUtility.FindIndex(mirrorNode.children, (node) => node.name == splits[i]);
                mirrorNode = mirrorNode.children[index];
            }
            Assert.IsTrue(mirrorNode.name == Path.GetFileName(mg.mirrorName));
            return mirrorNode;
        }
        private void SetMuscleGroupFoldout(MuscleGroupNode mg, bool foldout)
        {
            mg.foldout = foldout;
            if (mg.children != null)
            {
                foreach (var child in mg.children)
                {
                    SetMuscleGroupFoldout(child, foldout);
                }
            }
        }
        private bool ContainsMuscleGroup(MuscleGroupNode mg)
        {
            if (mg.infoList != null)
            {
                foreach (var info in mg.infoList)
                {
                    var muscleIndex = HumanTrait.MuscleFromBone((int)info.hi, info.dof);
                    if (va.humanoidMuscleContains[muscleIndex]) return true;
                }
            }
            if (mg.children != null && mg.children.Length > 0)
            {
                foreach (var child in mg.children)
                {
                    if (ContainsMuscleGroup(child)) return true;
                }
            }
            return false;
        }
        private void SetMuscleGroupValue(MuscleGroupNode mg, float value, List<MuscleValue> muscles)
        {
            muscleGroupValues[muscleGroupTreeTable[mg]] = value;
            if (mg.infoList != null)
            {
                foreach (var info in mg.infoList)
                {
                    var muscleIndex = HumanTrait.MuscleFromBone((int)info.hi, info.dof);
                    muscles.Add(new MuscleValue() { muscleIndex = muscleIndex, value = value * info.scale });
                }
            }
            if (mg.children != null && mg.children.Length > 0)
            {
                foreach (var child in mg.children)
                {
                    SetMuscleGroupValue(child, value, muscles);
                }
            }
        }
        private void SetAnimationCurveMuscleValues(List<MuscleValue> muscles)
        {
            bool[] doneFlags = null;
            for (int i = 0; i < muscles.Count; i++)
            {
                if (va.mirrorEnable)
                {
                    if (doneFlags == null) doneFlags = new bool[HumanTrait.MuscleCount];
                    var mmuscleIndex = va.GetMirrorMuscleIndex(muscles[i].muscleIndex);
                    if (mmuscleIndex >= 0 && doneFlags[mmuscleIndex])
                        continue;
                    doneFlags[muscles[i].muscleIndex] = true;
                }
                va.SetAnimationValueAnimatorMuscleIfNotOriginal(muscles[i].muscleIndex, muscles[i].value);
            }
        }
        private void MuscleGroupTreeNodeGUI(MuscleGroupNode mg, int level, int brotherMaxLevel)
        {
            const int FoldoutWidth = 22;
            const int FoldoutSpace = 17;
            const int FloatFieldWidth = 44;
            var indentSpace = IndentWidth * level;
            var e = Event.current;
            var mgContains = ContainsMuscleGroup(mg);
            EditorGUI.BeginDisabledGroup(!mgContains);
            EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
            {
                {
                    EditorGUI.indentLevel = level;
                    var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(indentSpace + FoldoutWidth));
                    EditorGUI.BeginChangeCheck();
                    mg.foldout = EditorGUI.Foldout(rect, mg.foldout, "", true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (Event.current.alt)
                            SetMuscleGroupFoldout(mg, mg.foldout);
                    }
                    EditorGUI.indentLevel = 0;
                }
                {
                    Action<MuscleGroupNode> SelectNodeAll = (node) =>
                    {
                        var humanoidIndexes = new HashSet<HumanBodyBones>();
                        var bindings = new HashSet<EditorCurveBinding>();
                        if (node.infoList != null && node.infoList.Length > 0)
                        {
                            foreach (var info in node.infoList)
                            {
                                humanoidIndexes.Add(info.hi);
                                var muscleIndex = HumanTrait.MuscleFromBone((int)info.hi, info.dof);
                                bindings.Add(va.AnimationCurveBindingAnimatorMuscle(muscleIndex));
                            }
                        }
                        if (Shortcuts.IsKeyControl(e) || e.shift)
                        {
                            var combineGoList = new HashSet<GameObject>(va.selectionGameObjects);
                            var combineVirtualList = new HashSet<HumanBodyBones>();
                            if (va.selectionHumanVirtualBones != null)
                                combineVirtualList.UnionWith(va.selectionHumanVirtualBones);
                            foreach (var hi in humanoidIndexes)
                            {
                                if (va.humanoidBones[(int)hi] != null)
                                    combineGoList.Add(va.humanoidBones[(int)hi]);
                                else if (VeryAnimation.HumanVirtualBones[(int)hi] != null)
                                    combineVirtualList.Add(hi);
                            }
                            va.SelectGameObjects(combineGoList.ToArray(), combineVirtualList.ToArray());
                            bindings.UnionWith(va.uAw.GetCurveSelection());
                            va.SetAnimationWindowSynchroSelection(bindings.ToArray());
                        }
                        else
                        {
                            if (humanoidIndexes.Count > 0)
                            {
                                foreach (var hi in humanoidIndexes)
                                {
                                    if (va.humanoidBones[(int)hi] != null)
                                    {
                                        Selection.activeGameObject = va.humanoidBones[(int)hi];
                                        break;
                                    }
                                }
                            }
                            va.SelectHumanoidBones(humanoidIndexes.ToArray());
                            va.SetAnimationWindowSynchroSelection(bindings.ToArray());
                        }
                    };
                    if (GUILayout.Button(new GUIContent(mg.name, mg.name), GUILayout.Width(vaw.editorSettings.settingEditorNameFieldWidth)))
                    {
                        SelectNodeAll(mg);
                    }
                    if (!string.IsNullOrEmpty(mg.mirrorName))
                    {
                        if (GUILayout.Button(new GUIContent("", string.Format("Mirror: '{0}'", Path.GetFileName(mg.mirrorName))), vaw.guiStyleMirrorButton, GUILayout.Width(vaw.mirrorTex.width), GUILayout.Height(vaw.mirrorTex.height)))
                        {
                            SelectNodeAll(GetMirrorNode(mg));
                        }
                    }
                    else
                    {
                        GUILayout.Space(FoldoutSpace);
                    }
                }
                {
                    var saveBackgroundColor = GUI.backgroundColor;
                    switch (mg.dof)
                    {
                    case 0: GUI.backgroundColor = Handles.xAxisColor; break;
                    case 1: GUI.backgroundColor = Handles.yAxisColor; break;
                    case 2: GUI.backgroundColor = Handles.zAxisColor; break;
                    }
                    EditorGUI.BeginChangeCheck();
                    var value = GUILayout.HorizontalSlider(muscleGroupValues[muscleGroupTreeTable[mg]], -1f, 1f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vae, "Change Muscle Group");
                        List<MuscleValue> muscles = new List<MuscleValue>();
                        SetMuscleGroupValue(mg, value, muscles);
                        SetAnimationCurveMuscleValues(muscles);
                        if (va.mirrorEnable)
                        {
                            var mirrorNode = GetMirrorNode(mg);
                            if (mirrorNode != null)
                                SetMuscleGroupValue(mirrorNode, value, muscles);
                        }
                    }
                    GUI.backgroundColor = saveBackgroundColor;
                }
                {
                    var width = FloatFieldWidth + IndentWidth * Math.Max(GetTreeLevel(mg, 0), brotherMaxLevel);
                    EditorGUI.BeginChangeCheck();
                    var value = EditorGUILayout.FloatField(muscleGroupValues[muscleGroupTreeTable[mg]], GUILayout.Width(width));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vae, "Change Muscle Group");
                        List<MuscleValue> muscles = new List<MuscleValue>();
                        SetMuscleGroupValue(mg, value, muscles);
                        SetAnimationCurveMuscleValues(muscles);
                        if (va.mirrorEnable)
                        {
                            var mirrorNode = GetMirrorNode(mg);
                            if (mirrorNode != null)
                                SetMuscleGroupValue(mirrorNode, value, muscles);
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
            if (mg.foldout)
            {
                if (mg.children != null && mg.children.Length > 0)
                {
                    int maxLevel = 0;
                    foreach (var child in mg.children)
                    {
                        maxLevel = Math.Max(GetTreeLevel(child, 0), maxLevel);
                    }
                    foreach (var child in mg.children)
                    {
                        MuscleGroupTreeNodeGUI(child, level + 1, maxLevel);
                    }
                }
                else if (mg.infoList != null && mg.infoList.Length > 0)
                {
                    #region Muscle
                    foreach (var info in mg.infoList)
                    {
                        var muscleIndex = HumanTrait.MuscleFromBone((int)info.hi, info.dof);
                        var humanoidIndex = (HumanBodyBones)HumanTrait.BoneFromMuscle(muscleIndex);
                        var muscleValue = va.GetAnimationValueAnimatorMuscle(muscleIndex);
                        EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                        {
                            EditorGUILayout.GetControlRect(false, GUILayout.Width(indentSpace + FoldoutWidth));
                            GUILayout.Space(IndentWidth);
                        }
                        {
                            var contains = va.humanoidBones[(int)humanoidIndex] != null || VeryAnimation.HumanVirtualBones[(int)humanoidIndex] != null;
                            EditorGUI.BeginDisabledGroup(!contains);
                            if (GUILayout.Button(new GUIContent(va.musclePropertyName.Names[muscleIndex], va.musclePropertyName.Names[muscleIndex]), GUILayout.Width(vaw.editorSettings.settingEditorNameFieldWidth)))
                            {
                                var humanoidIndexes = new HashSet<HumanBodyBones>();
                                var bindings = new HashSet<EditorCurveBinding>();
                                {
                                    humanoidIndexes.Add(info.hi);
                                    bindings.Add(va.AnimationCurveBindingAnimatorMuscle(muscleIndex));
                                }
                                if (Shortcuts.IsKeyControl(e) || e.shift)
                                {
                                    var combineGoList = new HashSet<GameObject>(va.selectionGameObjects);
                                    var combineVirtualList = new HashSet<HumanBodyBones>();
                                    if (va.selectionHumanVirtualBones != null)
                                        combineVirtualList.UnionWith(va.selectionHumanVirtualBones);
                                    foreach (var hi in humanoidIndexes)
                                    {
                                        if (va.humanoidBones[(int)hi] != null)
                                            combineGoList.Add(va.humanoidBones[(int)hi]);
                                        else if (VeryAnimation.HumanVirtualBones[(int)hi] != null)
                                            combineVirtualList.Add(hi);
                                    }
                                    va.SelectGameObjects(combineGoList.ToArray(), combineVirtualList.ToArray());
                                    bindings.UnionWith(va.uAw.GetCurveSelection());
                                    va.SetAnimationWindowSynchroSelection(bindings.ToArray());
                                }
                                else
                                {
                                    if (humanoidIndexes.Count > 0)
                                    {
                                        foreach (var hi in humanoidIndexes)
                                        {
                                            if (va.humanoidBones[(int)hi] != null)
                                            {
                                                Selection.activeGameObject = va.humanoidBones[(int)hi];
                                                break;
                                            }
                                        }
                                    }
                                    va.SelectHumanoidBones(humanoidIndexes.ToArray());
                                    va.SetAnimationWindowSynchroSelection(bindings.ToArray());
                                }
                            }
                            EditorGUI.EndDisabledGroup();
                        }
                        {
                            var mmuscleIndex = va.GetMirrorMuscleIndex(muscleIndex);
                            if (mmuscleIndex >= 0)
                            {
                                if (GUILayout.Button(new GUIContent("", string.Format("Mirror: '{0}'", va.musclePropertyName.Names[mmuscleIndex])), vaw.guiStyleMirrorButton, GUILayout.Width(vaw.mirrorTex.width), GUILayout.Height(vaw.mirrorTex.height)))
                                {
                                    var mhumanoidIndex = (HumanBodyBones)HumanTrait.BoneFromMuscle(mmuscleIndex);
                                    va.SelectHumanoidBones(new HumanBodyBones[] { mhumanoidIndex });
                                    va.SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { va.AnimationCurveBindingAnimatorMuscle(mmuscleIndex) });
                                }
                            }
                            else
                            {
                                GUILayout.Space(FoldoutSpace);
                            }
                        }
                        {
                            EditorGUI.BeginDisabledGroup(!va.humanoidMuscleContains[muscleIndex]);
                            var saveBackgroundColor = GUI.backgroundColor;
                            switch (info.dof)
                            {
                            case 0: GUI.backgroundColor = Handles.xAxisColor; break;
                            case 1: GUI.backgroundColor = Handles.yAxisColor; break;
                            case 2: GUI.backgroundColor = Handles.zAxisColor; break;
                            }
                            EditorGUI.BeginChangeCheck();
                            var value2 = GUILayout.HorizontalSlider(muscleValue, -1f, 1f);
                            if (EditorGUI.EndChangeCheck())
                            {
                                va.SetAnimationValueAnimatorMuscle(muscleIndex, value2);
                            }
                            GUI.backgroundColor = saveBackgroundColor;
                        }
                        {
                            EditorGUI.BeginChangeCheck();
                            var value2 = EditorGUILayout.FloatField(muscleValue, GUILayout.Width(FloatFieldWidth));
                            if (EditorGUI.EndChangeCheck())
                            {
                                va.SetAnimationValueAnimatorMuscle(muscleIndex, value2);
                            }
                        }
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.EndHorizontal();
                    }
                    #endregion
                }
            }
        }
        #endregion
    }
}
