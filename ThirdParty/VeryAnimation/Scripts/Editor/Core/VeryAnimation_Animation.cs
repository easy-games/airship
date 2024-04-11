#if !UNITY_2019_1_OR_NEWER
#define VERYANIMATION_TIMELINE
#endif
#if NET_4_6
#define ENABLE_PARALLEL
#endif

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
#if ENABLE_PARALLEL
using System.Threading.Tasks;
#endif
#if UNITY_2018_3_OR_NEWER && !UNITY_2021_2_OR_NEWER
using UnityEditor.Experimental.SceneManagement;
#endif

namespace VeryAnimation
{
    public partial class VeryAnimation
    {
        private static readonly string[] DofIndex2String =
        {
            ".x", ".y", ".z", ".w"
        };
        private static int[] QuaternionXMirrorSwapDof = new int[] { 2, 3, 0, 1 };
        public enum AnimatorIKIndex
        {
            None = -1,
            LeftHand,
            RightHand,
            LeftFoot,
            RightFoot,
            Total
        }
        public static readonly string[] AnimatorIKTIndexStrings =
        {
            AnimatorIKIndex.LeftHand.ToString() + "T.",
            AnimatorIKIndex.RightHand.ToString() + "T.",
            AnimatorIKIndex.LeftFoot.ToString() + "T.",
            AnimatorIKIndex.RightFoot.ToString() + "T.",
        };
        public static readonly string[] AnimatorIKQIndexStrings =
        {
            AnimatorIKIndex.LeftHand.ToString() + "Q.",
            AnimatorIKIndex.RightHand.ToString() + "Q.",
            AnimatorIKIndex.LeftFoot.ToString() + "Q.",
            AnimatorIKIndex.RightFoot.ToString() + "Q.",
        };
        public static readonly AnimatorIKIndex[] AnimatorIKMirrorIndexes =
        {
            AnimatorIKIndex.RightHand,
            AnimatorIKIndex.LeftHand,
            AnimatorIKIndex.RightFoot,
            AnimatorIKIndex.LeftFoot,
        };
        public static readonly HumanBodyBones[] AnimatorIKIndex2HumanBodyBones =
        {
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
        };
        public enum AnimatorTDOFIndex
        {
            None = -1,
            LeftUpperLeg,
            RightUpperLeg,
            Spine,
            Chest,
            Neck,
            LeftShoulder,
            RightShoulder,
            UpperChest,
            LeftLowerLeg,
            RightLowerLeg,
            LeftFoot,
            RightFoot,
            Head,
            LeftUpperArm,
            RightUpperArm,
            LeftLowerArm,
            RightLowerArm,
            LeftHand,
            RightHand,
            LeftToes,
            RightToes,
            Total
        }
        public static readonly string[] AnimatorTDOFIndexStrings =
        {
            AnimatorTDOFIndex.LeftUpperLeg.ToString(),
            AnimatorTDOFIndex.RightUpperLeg.ToString(),
            AnimatorTDOFIndex.Spine.ToString(),
            AnimatorTDOFIndex.Chest.ToString(),
            AnimatorTDOFIndex.Neck.ToString(),
            AnimatorTDOFIndex.LeftShoulder.ToString(),
            AnimatorTDOFIndex.RightShoulder.ToString(),
            AnimatorTDOFIndex.UpperChest.ToString(),
            AnimatorTDOFIndex.LeftLowerLeg.ToString(),
            AnimatorTDOFIndex.RightLowerLeg.ToString(),
            AnimatorTDOFIndex.LeftFoot.ToString(),
            AnimatorTDOFIndex.RightFoot.ToString(),
            AnimatorTDOFIndex.Head.ToString(),
            AnimatorTDOFIndex.LeftUpperArm.ToString(),
            AnimatorTDOFIndex.RightUpperArm.ToString(),
            AnimatorTDOFIndex.LeftLowerArm.ToString(),
            AnimatorTDOFIndex.RightLowerArm.ToString(),
            AnimatorTDOFIndex.LeftHand.ToString(),
            AnimatorTDOFIndex.RightHand.ToString(),
            AnimatorTDOFIndex.LeftToes.ToString(),
            AnimatorTDOFIndex.RightToes.ToString(),
        };
        public static readonly AnimatorTDOFIndex[] AnimatorTDOFMirrorIndexes =
        {
            AnimatorTDOFIndex.RightUpperLeg,
            AnimatorTDOFIndex.LeftUpperLeg,
            AnimatorTDOFIndex.None,
            AnimatorTDOFIndex.None,
            AnimatorTDOFIndex.None,
            AnimatorTDOFIndex.RightShoulder,
            AnimatorTDOFIndex.LeftShoulder,
            AnimatorTDOFIndex.None,
            AnimatorTDOFIndex.RightLowerLeg,
            AnimatorTDOFIndex.LeftLowerLeg,
            AnimatorTDOFIndex.RightFoot,
            AnimatorTDOFIndex.LeftFoot,
            AnimatorTDOFIndex.None,
            AnimatorTDOFIndex.RightUpperArm,
            AnimatorTDOFIndex.LeftUpperArm,
            AnimatorTDOFIndex.RightLowerArm,
            AnimatorTDOFIndex.LeftLowerArm,
            AnimatorTDOFIndex.RightHand,
            AnimatorTDOFIndex.LeftHand,
            AnimatorTDOFIndex.RightToes,
            AnimatorTDOFIndex.LeftToes,
        };
        public static readonly HumanBodyBones[] AnimatorTDOFIndex2HumanBodyBones =
        {
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.Neck,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.UpperChest,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightToes,
        };

        public static readonly HumanBodyBones[] HumanBodyMirrorBones =
        {
            (HumanBodyBones)(-1),           //Hips = 0,
            HumanBodyBones.RightUpperLeg,   //LeftUpperLeg = 1,
            HumanBodyBones.LeftUpperLeg,    //RightUpperLeg = 2,
            HumanBodyBones.RightLowerLeg,   //LeftLowerLeg = 3,
            HumanBodyBones.LeftLowerLeg,    //RightLowerLeg = 4,
            HumanBodyBones.RightFoot,       //LeftFoot = 5,
            HumanBodyBones.LeftFoot,        //RightFoot = 6,
            (HumanBodyBones)(-1),           //Spine = 7,
            (HumanBodyBones)(-1),           //Chest = 8,
            (HumanBodyBones)(-1),           //Neck = 9,
            (HumanBodyBones)(-1),           //Head = 10,
            HumanBodyBones.RightShoulder,   //LeftShoulder = 11,
            HumanBodyBones.LeftShoulder,    //RightShoulder = 12,
            HumanBodyBones.RightUpperArm,   //LeftUpperArm = 13,
            HumanBodyBones.LeftUpperArm,    //RightUpperArm = 14,
            HumanBodyBones.RightLowerArm,   //LeftLowerArm = 15,
            HumanBodyBones.LeftLowerArm,    //RightLowerArm = 16,
            HumanBodyBones.RightHand,       //LeftHand = 17,
            HumanBodyBones.LeftHand,        //RightHand = 18,
            HumanBodyBones.RightToes,       //LeftToes = 19,
            HumanBodyBones.LeftToes,        //RightToes = 20,
            HumanBodyBones.RightEye,        //LeftEye = 21,
            HumanBodyBones.LeftEye,         //RightEye = 22,
            (HumanBodyBones)(-1),           //Jaw = 23,
            HumanBodyBones.RightThumbProximal,      //LeftThumbProximal = 24,
            HumanBodyBones.RightThumbIntermediate,  //LeftThumbIntermediate = 25,
            HumanBodyBones.RightThumbDistal,        //LeftThumbDistal = 26,
            HumanBodyBones.RightIndexProximal,      //LeftIndexProximal = 27,
            HumanBodyBones.RightIndexIntermediate,  //LeftIndexIntermediate = 28,
            HumanBodyBones.RightIndexDistal,        //LeftIndexDistal = 29,
            HumanBodyBones.RightMiddleProximal,     //LeftMiddleProximal = 30,
            HumanBodyBones.RightMiddleIntermediate, //LeftMiddleIntermediate = 31,
            HumanBodyBones.RightMiddleDistal,       //LeftMiddleDistal = 32,
            HumanBodyBones.RightRingProximal,       //LeftRingProximal = 33,
            HumanBodyBones.RightRingIntermediate,   //LeftRingIntermediate = 34,
            HumanBodyBones.RightRingDistal,         //LeftRingDistal = 35,
            HumanBodyBones.RightLittleProximal,     //LeftLittleProximal = 36,
            HumanBodyBones.RightLittleIntermediate, //LeftLittleIntermediate = 37,
            HumanBodyBones.RightLittleDistal,       //LeftLittleDistal = 38,
            HumanBodyBones.LeftThumbProximal,       //RightThumbProximal = 39,
            HumanBodyBones.LeftThumbIntermediate,   //RightThumbIntermediate = 40,
            HumanBodyBones.LeftThumbDistal,         //RightThumbDistal = 41,
            HumanBodyBones.LeftIndexProximal,       //RightIndexProximal = 42,
            HumanBodyBones.LeftIndexIntermediate,   //RightIndexIntermediate = 43,
            HumanBodyBones.LeftIndexDistal,         //RightIndexDistal = 44,
            HumanBodyBones.LeftMiddleProximal,      //RightMiddleProximal = 45,
            HumanBodyBones.LeftMiddleIntermediate,  //RightMiddleIntermediate = 46,
            HumanBodyBones.LeftMiddleDistal,        //RightMiddleDistal = 47,
            HumanBodyBones.LeftRingProximal,        //RightRingProximal = 48,
            HumanBodyBones.LeftRingIntermediate,    //RightRingIntermediate = 49,
            HumanBodyBones.LeftRingDistal,          //RightRingDistal = 50,
            HumanBodyBones.LeftLittleProximal,      //RightLittleProximal = 51,
            HumanBodyBones.LeftLittleIntermediate,  //RightLittleIntermediate = 52,
            HumanBodyBones.LeftLittleDistal,        //RightLittleDistal = 53,
            (HumanBodyBones)(-1),                   //UpperChest = 54,
        };

        public class HumanVirtualBone
        {
            public HumanBodyBones boneA;
            public HumanBodyBones boneB;
            public float leap;
            public Quaternion addRotation = Quaternion.identity;
            public Vector3 limitSign = Vector3.one;
        }
        public static readonly HumanVirtualBone[][] HumanVirtualBones =
        {
            null, //Hips = 0,
            null, //LeftUpperLeg = 1,
            null, //RightUpperLeg = 2,
            null, //LeftLowerLeg = 3,
            null, //RightLowerLeg = 4,
            null, //LeftFoot = 5,
            null, //RightFoot = 6,
            null, //Spine = 7,
            new HumanVirtualBone[] { new HumanVirtualBone() { boneA = HumanBodyBones.Spine, boneB = HumanBodyBones.Head, leap = 0.15f } }, //Chest = 8,
            new HumanVirtualBone[] { new HumanVirtualBone() { boneA = HumanBodyBones.UpperChest, boneB = HumanBodyBones.Head, leap = 0.8f },
                                        new HumanVirtualBone() { boneA = HumanBodyBones.Chest, boneB = HumanBodyBones.Head, leap = 0.8f },
                                        new HumanVirtualBone() { boneA = HumanBodyBones.Spine, boneB = HumanBodyBones.Head, leap = 0.85f } }, //Neck = 9,
            null, //Head = 10,
            new HumanVirtualBone[] { new HumanVirtualBone() { boneA = HumanBodyBones.LeftUpperArm, boneB = HumanBodyBones.RightUpperArm, leap = 0.2f, limitSign = new Vector3(1f, 1f, -1f) } }, //LeftShoulder = 11,
            new HumanVirtualBone[] { new HumanVirtualBone() { boneA = HumanBodyBones.RightUpperArm, boneB = HumanBodyBones.LeftUpperArm, leap = 0.2f } }, //RightShoulder = 12,
            null, //LeftUpperArm = 13,
            null, //RightUpperArm = 14,
            null, //LeftLowerArm = 15,
            null, //RightLowerArm = 16,
            null, //LeftHand = 17,
            null, //RightHand = 18,
            null, //LeftToes = 19,
            null, //RightToes = 20,
            null, //LeftEye = 21,
            null, //RightEye = 22,
            null, //Jaw = 23,
            null, //LeftThumbProximal = 24,
            null, //LeftThumbIntermediate = 25,
            null, //LeftThumbDistal = 26,
            null, //LeftIndexProximal = 27,
            null, //LeftIndexIntermediate = 28,
            null, //LeftIndexDistal = 29,
            null, //LeftMiddleProximal = 30,
            null, //LeftMiddleIntermediate = 31,
            null, //LeftMiddleDistal = 32,
            null, //LeftRingProximal = 33,
            null, //LeftRingIntermediate = 34,
            null, //LeftRingDistal = 35,
            null, //LeftLittleProximal = 36,
            null, //LeftLittleIntermediate = 37,
            null, //LeftLittleDistal = 38,
            null, //RightThumbProximal = 39,
            null, //RightThumbIntermediate = 40,
            null, //RightThumbDistal = 41,
            null, //RightIndexProximal = 42,
            null, //RightIndexIntermediate = 43,
            null, //RightIndexDistal = 44,
            null, //RightMiddleProximal = 45,
            null, //RightMiddleIntermediate = 46,
            null, //RightMiddleDistal = 47,
            null, //RightRingProximal = 48,
            null, //RightRingIntermediate = 49,
            null, //RightRingDistal = 50,
            null, //RightLittleProximal = 51,
            null, //RightLittleIntermediate = 52,
            null, //RightLittleDistal = 53,
            new HumanVirtualBone[] { new HumanVirtualBone() { boneA = HumanBodyBones.Chest, boneB = HumanBodyBones.Head, leap = 0.2f },
                                        new HumanVirtualBone() { boneA = HumanBodyBones.Spine, boneB = HumanBodyBones.Head, leap = 0.3f } }, //UpperChest = 54,
        };

        public class AnimatorTDOF
        {
            public AnimatorTDOFIndex index;
            public HumanBodyBones parent;
            public Vector3 mirror = new Vector3(1f, 1f, -1f);
        }
        public static readonly AnimatorTDOF[] HumanBonesAnimatorTDOFIndex =
        {
            null, //Hips = 0,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.LeftUpperLeg, parent = HumanBodyBones.Hips }, //LeftUpperLeg = 1,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.RightUpperLeg, parent = HumanBodyBones.Hips }, //RightUpperLeg = 2,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.LeftLowerLeg, parent = HumanBodyBones.LeftUpperLeg }, //LeftLowerLeg = 3,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.RightLowerLeg, parent = HumanBodyBones.RightUpperLeg }, //RightLowerLeg = 4,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.LeftFoot, parent = HumanBodyBones.LeftLowerLeg }, //LeftFoot = 5,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.RightFoot, parent = HumanBodyBones.RightLowerLeg }, //RightFoot = 6,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.Spine, parent = HumanBodyBones.Hips }, //Spine = 7,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.Chest, parent = HumanBodyBones.Spine }, //Chest = 8,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.Neck, parent = HumanBodyBones.UpperChest }, //Neck = 9,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.Head, parent = HumanBodyBones.Neck }, //Head = 10,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.LeftShoulder, parent = HumanBodyBones.UpperChest }, //LeftShoulder = 11,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.RightShoulder, parent = HumanBodyBones.UpperChest }, //RightShoulder = 12,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.LeftUpperArm, parent = HumanBodyBones.LeftShoulder, mirror = new Vector3(1f, -1f, 1f) }, //LeftUpperArm = 13,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.RightUpperArm, parent = HumanBodyBones.RightShoulder, mirror = new Vector3(1f, -1f, 1f) }, //RightUpperArm = 14,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.LeftLowerArm, parent = HumanBodyBones.LeftUpperArm, mirror = new Vector3(1f, -1f, 1f)  }, //LeftLowerArm = 15,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.RightLowerArm, parent = HumanBodyBones.RightUpperArm, mirror = new Vector3(1f, -1f, 1f)  }, //RightLowerArm = 16,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.LeftHand, parent = HumanBodyBones.LeftLowerArm, mirror = new Vector3(1f, -1f, 1f)  }, //LeftHand = 17,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.RightHand, parent = HumanBodyBones.RightLowerArm, mirror = new Vector3(1f, -1f, 1f)  }, //RightHand = 18,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.LeftToes, parent = HumanBodyBones.LeftFoot }, //LeftToes = 19,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.RightToes, parent = HumanBodyBones.RightFoot }, //RightToes = 20,
            null, //LeftEye = 21,
            null, //RightEye = 22,
            null, //Jaw = 23,
            null, //LeftThumbProximal = 24,
            null, //LeftThumbIntermediate = 25,
            null, //LeftThumbDistal = 26,
            null, //LeftIndexProximal = 27,
            null, //LeftIndexIntermediate = 28,
            null, //LeftIndexDistal = 29,
            null, //LeftMiddleProximal = 30,
            null, //LeftMiddleIntermediate = 31,
            null, //LeftMiddleDistal = 32,
            null, //LeftRingProximal = 33,
            null, //LeftRingIntermediate = 34,
            null, //LeftRingDistal = 35,
            null, //LeftLittleProximal = 36,
            null, //LeftLittleIntermediate = 37,
            null, //LeftLittleDistal = 38,
            null, //RightThumbProximal = 39,
            null, //RightThumbIntermediate = 40,
            null, //RightThumbDistal = 41,
            null, //RightIndexProximal = 42,
            null, //RightIndexIntermediate = 43,
            null, //RightIndexDistal = 44,
            null, //RightMiddleProximal = 45,
            null, //RightMiddleIntermediate = 46,
            null, //RightMiddleDistal = 47,
            null, //RightRingProximal = 48,
            null, //RightRingIntermediate = 49,
            null, //RightRingDistal = 50,
            null, //RightLittleProximal = 51,
            null, //RightLittleIntermediate = 52,
            null, //RightLittleDistal = 53,
            new AnimatorTDOF() { index = AnimatorTDOFIndex.UpperChest, parent = HumanBodyBones.Chest }, //UpperChest = 54,
        };

        public static readonly HumanBodyBones[] HumanPoseHaveMassBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightToes,
            HumanBodyBones.LeftEye,
            HumanBodyBones.RightEye,
            HumanBodyBones.Jaw,
        };

        public float GetAvatarAxisLength(HumanBodyBones humanoidIndex)
        {
            return uAvatar.GetAxisLength(animatorAvatar, (int)humanoidIndex);
        }
        public Quaternion GetAvatarPreRotation(HumanBodyBones humanoidIndex)
        {
            return uAvatar.GetPreRotation(animatorAvatar, (int)humanoidIndex);
        }
        public Quaternion GetAvatarPostRotation(HumanBodyBones humanoidIndex)
        {
            return uAvatar.GetPostRotation(animatorAvatar, (int)humanoidIndex);
        }
        public Quaternion GetZYPostQ(HumanBodyBones humanoidIndex, Quaternion parentQ, Quaternion q)
        {
            return uAvatar.GetZYPostQ(animatorAvatar, (int)humanoidIndex, parentQ, q);
        }
        public Quaternion GetZYRoll(HumanBodyBones humanoidIndex)
        {
            return uAvatar.GetZYRoll(animatorAvatar, (int)humanoidIndex, Vector3.zero);
        }
        public Vector3 GetLimitSign(HumanBodyBones humanoidIndex)
        {
            return uAvatar.GetLimitSign(animatorAvatar, (int)humanoidIndex);
        }

        #region WeightUpdateFrame
        private class WeightUpdateFrame
        {
            public WeightUpdateFrame()
            {
                frames = new Dictionary<int, float>();
            }
            public void Add(int frame, float weight)
            {
                float outWeight;
                if (frames.TryGetValue(frame, out outWeight))
                {
                    if (Mathf.Abs(outWeight) > Mathf.Abs(weight))
                        frames[frame] = weight;
                }
                else
                {
                    frames.Add(frame, weight);
                }
            }
            public void Clear()
            {
                frames.Clear();
            }
            public bool IsEmpty()
            {
                return frames.Count == 0;
            }

            public Dictionary<int, float> frames { get; private set; }
        }
        #endregion

        #region AnimatorRootCorrection
        private class AnimatorRootCorrection
        {
            public bool update;
            public bool disable;
            public int[] muscleIndexes;
            public AnimationCurve[] rootTCurves = new AnimationCurve[3];
            public AnimationCurve[] rootQCurves = new AnimationCurve[4];
            public AnimationCurve[] muscleCurves;
            //Save
            [Serializable, System.Diagnostics.DebuggerDisplay("Position({position}), Rotation({rotation})")]
            public struct TransformSave
            {
                public Vector3 position;
                public Quaternion rotation;
            }
            public List<TransformSave> hipSaves = new List<TransformSave>();
            public List<TransformSave> rootSaves = new List<TransformSave>();
            public List<float>[] muscleValueSaves;

            public TransformSave[] frameRootSaves;

            public HumanPose humanPose;

            public WeightUpdateFrame updateFrame = new WeightUpdateFrame();

            public Vector3 GetRootT(float time)
            {
                Vector3 result = Vector3.zero;
                for (int i = 0; i < 3; i++)
                {
                    if (rootTCurves[i] != null)
                        result[i] = rootTCurves[i].Evaluate(time);
                }
                return result;
            }
            public Quaternion GetRootQ(float time)
            {
                Vector4 result = new Vector4(0, 0, 0, 1);
                for (int i = 0; i < 4; i++)
                {
                    if (rootQCurves[i] != null)
                        result[i] = rootQCurves[i].Evaluate(time);
                }
                result.Normalize();
                if (result.sqrMagnitude > 0f)
                {
                    return new Quaternion(result.x, result.y, result.z, result.w);
                }
                else
                {
                    return Quaternion.identity;
                }
            }
        }
        private AnimatorRootCorrection updateAnimatorRootCorrection;
        private void InitializeAnimatorRootCorrection()
        {
            if (!isHuman) return;

            updateAnimatorRootCorrection = new AnimatorRootCorrection();

            {
                List<int> muscles = new List<int>();
                for (int i = 0; i < HumanPoseHaveMassBones.Length; i++)
                {
                    for (int dof = 0; dof < 3; dof++)
                    {
                        var muscleIndex = HumanTrait.MuscleFromBone((int)HumanPoseHaveMassBones[i], dof);
                        if (muscleIndex >= 0)
                            muscles.Add(muscleIndex);
                    }
                }
                updateAnimatorRootCorrection.muscleIndexes = muscles.ToArray();
            }
            updateAnimatorRootCorrection.muscleCurves = new AnimationCurve[updateAnimatorRootCorrection.muscleIndexes.Length];
            updateAnimatorRootCorrection.muscleValueSaves = new List<float>[updateAnimatorRootCorrection.muscleIndexes.Length];
            for (int i = 0; i < updateAnimatorRootCorrection.muscleValueSaves.Length; i++)
                updateAnimatorRootCorrection.muscleValueSaves[i] = new List<float>();
            updateAnimatorRootCorrection.humanPose.muscles = new float[HumanTrait.MuscleCount];
        }
        private void ReleaseAnimatorRootCorrection()
        {
            updateAnimatorRootCorrection = null;
        }
        private void EnableAnimatorRootCorrection(AnimationCurve curve, int keyIndex)
        {
            if (!isHuman) return;
            if (rootCorrectionMode == RootCorrectionMode.Disable) return;
            if (keyIndex < 0 || keyIndex >= curve.length) return;

            var currentTime = curve[keyIndex].time;
            var beforeTime = 0f;
            var afterTime = currentClip.length;
            if (rootCorrectionMode == RootCorrectionMode.Full)
            {
                if (keyIndex > 0)
                    beforeTime = curve[keyIndex - 1].time;
                if (keyIndex + 1 < curve.length)
                    afterTime = curve[keyIndex + 1].time;
            }
            EnableAnimatorRootCorrection(currentTime, beforeTime, afterTime);
        }
        private void EnableAnimatorRootCorrection(float currentTime, float beforeTime, float afterTime)
        {
            if (!isHuman) return;
            if (rootCorrectionMode == RootCorrectionMode.Disable) return;

            updateAnimatorRootCorrection.update = true;

            var currentFrame = uAw.GetCurrentFrame();
            updateAnimatorRootCorrection.updateFrame.Add(currentFrame, 0f);

            if (rootCorrectionMode == RootCorrectionMode.Full)
            {
                var beforeFrame = uAw.TimeToFrameRound(beforeTime);
                var afterFrame = uAw.TimeToFrameRound(afterTime);
                updateAnimatorRootCorrection.updateFrame.Add(beforeFrame, 1f);
                updateAnimatorRootCorrection.updateFrame.Add(afterFrame, -1f);
                for (int frame = currentFrame - 1; frame > beforeFrame; frame--)
                {
                    updateAnimatorRootCorrection.updateFrame.Add(frame, 0f);
                }
                for (int frame = currentFrame + 1; frame < afterFrame; frame++)
                {
                    updateAnimatorRootCorrection.updateFrame.Add(frame, 0f);
                }
            }
        }
        private void DisableAnimatorRootCorrection()
        {
            if (!isHuman) return;
            updateAnimatorRootCorrection.disable = true;
        }
        private void ResetAnimatorRootCorrection()
        {
            if (!isHuman) return;
            updateAnimatorRootCorrection.update = false;
            updateAnimatorRootCorrection.disable = false;
            updateAnimatorRootCorrection.updateFrame.Clear();
        }
        private void SaveAnimatorRootCorrection(bool forceUpdate)
        {
            if (!isHuman) return;

            var lastFrame = GetLastFrame();

            #region NotUpdateCheck
            if (!forceUpdate)
            {
                if (!humanoidHasTDoF)
                {
                    if (updateAnimatorRootCorrection.rootSaves.Count == lastFrame + 1)
                        return;
                }
                else
                {
                    if (updateAnimatorRootCorrection.hipSaves.Count == lastFrame + 1)
                        return;
                }
            }
            #endregion

            ResetAnimatorRootCorrection();

            #region Clear
            {
                updateAnimatorRootCorrection.hipSaves.Clear();
                updateAnimatorRootCorrection.rootSaves.Clear();
                foreach (var saves in updateAnimatorRootCorrection.muscleValueSaves)
                    saves.Clear();
            }
            #endregion

            if (!humanoidHasTDoF)
            {
                #region Not TDoF
                for (int i = 0; i < 3; i++)
                    updateAnimatorRootCorrection.rootTCurves[i] = GetEditorCurveCache(AnimationCurveBindingAnimatorRootT[i]);
                for (int i = 0; i < 4; i++)
                    updateAnimatorRootCorrection.rootQCurves[i] = GetEditorCurveCache(AnimationCurveBindingAnimatorRootQ[i]);
                for (int i = 0; i < updateAnimatorRootCorrection.muscleIndexes.Length; i++)
                    updateAnimatorRootCorrection.muscleCurves[i] = GetEditorCurveCache(AnimationCurveBindingAnimatorMuscle(updateAnimatorRootCorrection.muscleIndexes[i]));
                for (int frame = 0; frame <= lastFrame; frame++)
                {
                    var time = GetFrameTime(frame);
                    var rootT = new Vector3(updateAnimatorRootCorrection.rootTCurves[0] != null ? updateAnimatorRootCorrection.rootTCurves[0].Evaluate(time) : 0f,
                                            updateAnimatorRootCorrection.rootTCurves[1] != null ? updateAnimatorRootCorrection.rootTCurves[1].Evaluate(time) : 0f,
                                            updateAnimatorRootCorrection.rootTCurves[2] != null ? updateAnimatorRootCorrection.rootTCurves[2].Evaluate(time) : 0f);
                    Quaternion rootQ = Quaternion.identity;
                    {
                        Vector4 result = new Vector4(0, 0, 0, 1);
                        for (int i = 0; i < 4; i++)
                        {
                            if (updateAnimatorRootCorrection.rootQCurves[i] != null)
                                result[i] = updateAnimatorRootCorrection.rootQCurves[i].Evaluate(time);
                        }
                        result.Normalize();
                        if (result.sqrMagnitude > 0f)
                            rootQ = new Quaternion(result.x, result.y, result.z, result.w);
                    }
                    updateAnimatorRootCorrection.rootSaves.Add(new AnimatorRootCorrection.TransformSave()
                    {
                        position = rootT,
                        rotation = rootQ,
                    });
                    for (int i = 0; i < updateAnimatorRootCorrection.muscleIndexes.Length; i++)
                    {
                        var curve = updateAnimatorRootCorrection.muscleCurves[i];
                        updateAnimatorRootCorrection.muscleValueSaves[i].Add(curve != null ? curve.Evaluate(time) : 0f);
                    }
                }
                #endregion
            }
            else
            {
                #region Has TDoF
                calcObject.SetApplyIK(false);
                calcObject.SetTransformOrigin();
                var tHip = calcObject.humanoidHipsTransform;
                for (int frame = 0; frame <= lastFrame; frame++)
                {
                    var time = GetFrameTime(frame);
                    calcObject.SampleAnimationLegacy(currentClip, time);
                    updateAnimatorRootCorrection.hipSaves.Add(new AnimatorRootCorrection.TransformSave()
                    {
                        position = tHip.position,
                        rotation = (tHip.rotation * humanoidPostHipRotation) * humanoidPreHipRotationInverse,
                    });
                }
                #endregion
            }
        }
        private void UpdateAnimatorRootCorrection()
        {
            if (isHuman &&
                rootCorrectionMode != RootCorrectionMode.Disable &&
                !updatePoseFixAnimation &&
                updateAnimatorRootCorrection.update &&
                !updateAnimatorRootCorrection.disable &&
                !updateAnimatorRootCorrection.updateFrame.IsEmpty())
            {
                Undo.RegisterCompleteObjectUndo(currentClip, "Change Root");

                var lastFrame = GetLastFrame();

                #region Chache
                {
                    for (int i = 0; i < 3; i++)
                    {
                        updateAnimatorRootCorrection.rootTCurves[i] = GetAnimationCurveAnimatorRootT(i);
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        updateAnimatorRootCorrection.rootQCurves[i] = GetAnimationCurveAnimatorRootQ(i);
                    }
                    #region FrameRootSaves
                    {
                        if (updateAnimatorRootCorrection.frameRootSaves == null || updateAnimatorRootCorrection.frameRootSaves.Length < lastFrame + 1)
                        {
                            updateAnimatorRootCorrection.frameRootSaves = new AnimatorRootCorrection.TransformSave[(lastFrame + 1) * 2];
                        }
                        foreach (var pair in updateAnimatorRootCorrection.updateFrame.frames)
                        {
                            var frame = pair.Key;
                            if (frame > lastFrame)
                                frame = lastFrame;
                            var time = GetFrameTime(frame);

                            updateAnimatorRootCorrection.frameRootSaves[frame].position = new Vector3(updateAnimatorRootCorrection.rootTCurves[0].Evaluate(time),
                                                                                                        updateAnimatorRootCorrection.rootTCurves[1].Evaluate(time),
                                                                                                        updateAnimatorRootCorrection.rootTCurves[2].Evaluate(time));
                            {
                                Vector4 result = new Vector4(0, 0, 0, 1);
                                for (int i = 0; i < 4; i++)
                                    result[i] = updateAnimatorRootCorrection.rootQCurves[i].Evaluate(time);
                                result.Normalize();
                                if (result.sqrMagnitude > 0f)
                                    updateAnimatorRootCorrection.frameRootSaves[frame].rotation = new Quaternion(result.x, result.y, result.z, result.w);
                                else
                                    updateAnimatorRootCorrection.frameRootSaves[frame].rotation = Quaternion.identity;
                            }
                        }
                    }
                    #endregion
                }
                #endregion

                if (!humanoidHasTDoF)
                {
                    #region Not TDoF
                    calcObject.SetApplyIK(false);
                    calcObject.SetTransformOrigin();
                    #region Chache
                    {
                        for (int i = 0; i < updateAnimatorRootCorrection.muscleIndexes.Length; i++)
                        {
                            updateAnimatorRootCorrection.muscleCurves[i] = GetEditorCurveCache(AnimationCurveBindingAnimatorMuscle(updateAnimatorRootCorrection.muscleIndexes[i]));
                        }
                    }
                    #endregion
                    foreach (var pair in updateAnimatorRootCorrection.updateFrame.frames)
                    {
                        var frame = pair.Key;
                        if (frame > lastFrame)
                            continue;
                        var tHip = calcObject.humanoidHipsTransform;
                        var time = GetFrameTime(frame);
                        #region Before
                        {
                            var tframe = frame;
                            if (tframe >= updateAnimatorRootCorrection.rootSaves.Count)
                                tframe = updateAnimatorRootCorrection.rootSaves.Count - 1;
                            updateAnimatorRootCorrection.humanPose.bodyPosition = updateAnimatorRootCorrection.rootSaves[tframe].position;
                            updateAnimatorRootCorrection.humanPose.bodyRotation = updateAnimatorRootCorrection.rootSaves[tframe].rotation;
                            for (int i = 0; i < updateAnimatorRootCorrection.muscleIndexes.Length; i++)
                            {
                                var muscleIndex = updateAnimatorRootCorrection.muscleIndexes[i];
                                updateAnimatorRootCorrection.humanPose.muscles[muscleIndex] = updateAnimatorRootCorrection.muscleValueSaves[i][tframe];
                            }
                            calcObject.humanPoseHandler.SetHumanPose(ref updateAnimatorRootCorrection.humanPose);
                        }
                        var hipBeforeRot = (tHip.rotation * humanoidPostHipRotation) * humanoidPreHipRotationInverse;
                        var hipBeforePos = tHip.position;
                        #endregion
                        #region RootQ
                        Quaternion rootQ;
                        {
                            updateAnimatorRootCorrection.humanPose.bodyPosition = updateAnimatorRootCorrection.frameRootSaves[frame].position;
                            updateAnimatorRootCorrection.humanPose.bodyRotation = updateAnimatorRootCorrection.frameRootSaves[frame].rotation;
                            for (int i = 0; i < updateAnimatorRootCorrection.muscleIndexes.Length; i++)
                            {
                                if (updateAnimatorRootCorrection.muscleCurves[i] == null) continue;
                                var muscleIndex = updateAnimatorRootCorrection.muscleIndexes[i];
                                updateAnimatorRootCorrection.humanPose.muscles[muscleIndex] = updateAnimatorRootCorrection.muscleCurves[i].Evaluate(time);
                            }
                            calcObject.humanPoseHandler.SetHumanPose(ref updateAnimatorRootCorrection.humanPose);
                            {
                                var hipNowRot = (tHip.rotation * humanoidPostHipRotation) * humanoidPreHipRotationInverse;
                                var offset = hipBeforeRot * Quaternion.Inverse(hipNowRot);
                                rootQ = offset * updateAnimatorRootCorrection.humanPose.bodyRotation;
                                #region FixReverseRotation
                                {
                                    var rot = rootQ * Quaternion.Inverse(updateAnimatorRootCorrection.GetRootQ(time));
                                    if (rot.w < 0f)
                                    {
                                        for (int i = 0; i < 4; i++)
                                            rootQ[i] = -rootQ[i];
                                    }
                                }
                                #endregion
                            }
                        }
                        for (int i = 0; i < 4; i++)
                        {
                            var curve = updateAnimatorRootCorrection.rootQCurves[i];
                            float value = rootQ[i];
                            SetKeyframe(curve, time, value, false);
                        }
                        updateAnimatorRootCorrection.humanPose.bodyRotation = rootQ;
                        calcObject.humanPoseHandler.SetHumanPose(ref updateAnimatorRootCorrection.humanPose);
                        #endregion
                        #region RootT
                        Vector3 rootT;
                        {
                            var hipNowPos = tHip.position;
                            var offset = ((hipNowPos - hipBeforePos)) * (1f / calcObject.animator.humanScale);
                            rootT = updateAnimatorRootCorrection.humanPose.bodyPosition - offset;
                        }
                        for (int i = 0; i < 3; i++)
                        {
                            var curve = updateAnimatorRootCorrection.rootTCurves[i];
                            float value = rootT[i];
                            SetKeyframe(curve, time, value, false);
                        }
                        #endregion
                    }
                    calcObject.SetTransformStart();
                    #endregion
                }
                else
                {
                    #region Has TDoF
                    calcObject.SetApplyIK(false);
                    calcObject.SetTransformOrigin();
                    foreach (var pair in updateAnimatorRootCorrection.updateFrame.frames)
                    {
                        var frame = pair.Key;
                        if (frame > lastFrame)
                            continue;
                        var tHip = calcObject.humanoidHipsTransform;
                        var time = GetFrameTime(frame);
                        calcObject.SampleAnimationLegacy(currentClip, time);
                        {
                            #region Before
                            Vector3 hipBeforePos;
                            Quaternion hipBeforeRot;
                            {
                                var tframe = frame;
                                if (tframe >= updateAnimatorRootCorrection.hipSaves.Count)
                                    tframe = updateAnimatorRootCorrection.hipSaves.Count - 1;
                                hipBeforePos = updateAnimatorRootCorrection.hipSaves[tframe].position;
                                hipBeforeRot = updateAnimatorRootCorrection.hipSaves[tframe].rotation;
                            }
                            #endregion
                            var hipNowPos = tHip.position;
                            var hipNowRot = (tHip.rotation * humanoidPostHipRotation) * humanoidPreHipRotationInverse;
                            #region RootQ
                            Quaternion rootQ;
                            Quaternion rotationOffset;
                            {
                                rotationOffset = hipBeforeRot * Quaternion.Inverse(hipNowRot);
                                updateAnimatorRootCorrection.humanPose.bodyRotation = updateAnimatorRootCorrection.frameRootSaves[frame].rotation;
                                rootQ = rotationOffset * updateAnimatorRootCorrection.humanPose.bodyRotation;
                                #region FixReverseRotation
                                {
                                    var rot = rootQ * Quaternion.Inverse(updateAnimatorRootCorrection.GetRootQ(time));
                                    if (rot.w < 0f)
                                    {
                                        for (int i = 0; i < 4; i++)
                                            rootQ[i] = -rootQ[i];
                                    }
                                }
                                #endregion
                            }
                            for (int i = 0; i < 4; i++)
                            {
                                var curve = updateAnimatorRootCorrection.rootQCurves[i];
                                float value = rootQ[i];
                                SetKeyframe(curve, time, value, false);
                            }
                            #endregion
                            #region RootT
                            Vector3 rootT;
                            {
                                updateAnimatorRootCorrection.humanPose.bodyPosition = updateAnimatorRootCorrection.frameRootSaves[frame].position;
                                var bodyPosition = updateAnimatorRootCorrection.humanPose.bodyPosition * calcObject.animator.humanScale;
                                var worldRootPosition = calcObject.gameObjectTransform.localToWorldMatrix.MultiplyPoint3x4(bodyPosition);
                                hipNowPos = worldRootPosition + rotationOffset * (hipNowPos - worldRootPosition);
                                var offset = ((hipNowPos - hipBeforePos)) * (1f / calcObject.animator.humanScale);
                                rootT = updateAnimatorRootCorrection.humanPose.bodyPosition - offset;
                            }
                            for (int i = 0; i < 3; i++)
                            {
                                var curve = updateAnimatorRootCorrection.rootTCurves[i];
                                float value = rootT[i];
                                SetKeyframe(curve, time, value, false);
                            }
                            #endregion
                        }
                    }
                    calcObject.SetTransformStart();
                    #endregion
                }

                #region SmoothTangent
                {
                    foreach (var pair in updateAnimatorRootCorrection.updateFrame.frames)
                    {
                        var frame = pair.Key;
                        var weight = pair.Value;
                        var time = GetFrameTime(frame);
                        for (int i = 0; i < 4; i++)
                        {
                            var keyIndex = FindKeyframeAtTime(updateAnimatorRootCorrection.rootQCurves[i], time);
                            if (keyIndex >= 0)
                                updateAnimatorRootCorrection.rootQCurves[i].SmoothTangents(keyIndex, weight);
                        }
                        for (int i = 0; i < 3; i++)
                        {
                            var keyIndex = FindKeyframeAtTime(updateAnimatorRootCorrection.rootTCurves[i], time);
                            if (keyIndex >= 0)
                                updateAnimatorRootCorrection.rootTCurves[i].SmoothTangents(keyIndex, weight);
                        }
                        AddHumanoidFootIK(time, weight);
                    }
                }
                #endregion

                #region Write
                {
                    for (int i = 0; i < 4; i++)
                    {
                        SetAnimationCurveAnimatorRootQ(i, updateAnimatorRootCorrection.rootQCurves[i]);
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        SetAnimationCurveAnimatorRootT(i, updateAnimatorRootCorrection.rootTCurves[i]);
                    }
                }
                #endregion
            }
        }
        private bool IsAnimatorRootCorrectionBone(HumanBodyBones humanoidIndex)
        {
            return ((humanoidIndex >= HumanBodyBones.Hips && humanoidIndex <= HumanBodyBones.Jaw) ||
                    humanoidIndex == HumanBodyBones.UpperChest);
        }
        #endregion

        #region FootIK
        private class HumanoidFootIK
        {
            public class IkCurves
            {
                public AnimationCurve[] ikT = new AnimationCurve[3];
                public AnimationCurve[] ikQ = new AnimationCurve[4];
            }
            public IkCurves[] ikCurves;

            public WeightUpdateFrame updateFrame = new WeightUpdateFrame();

            public HumanoidFootIK()
            {
                ikCurves = new IkCurves[AnimatorIKIndex.RightFoot - AnimatorIKIndex.LeftFoot + 1];
                for (int i = 0; i < ikCurves.Length; i++)
                {
                    ikCurves[i] = new IkCurves();
                }
            }

            public void Clear()
            {
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < ikCurves.Length; j++)
                        ikCurves[j].ikT[i] = null;
                }
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < ikCurves.Length; j++)
                        ikCurves[j].ikQ[i] = null;
                }
                updateFrame.Clear();
            }
        }
        private HumanoidFootIK humanoidFootIK;

        private void InitializeHumanoidFootIK()
        {
            if (!isHuman) return;
            humanoidFootIK = new HumanoidFootIK();
        }
        private void ReleaseHumanoidFootIK()
        {
            humanoidFootIK = null;
        }
        public bool IsEnableUpdateHumanoidFootIK()
        {
            if (!isHuman)
                return false;

#if VERYANIMATION_TIMELINE
            if (uAw.GetLinkedWithTimeline())
                return uAw.GetTimelineAnimationApplyFootIK();
#endif
            return (autoFootIK);
        }
        private void AddHumanoidFootIK(float time, float weight = 0f)
        {
            if (!isHuman) return;
            if (time < 0f || time > currentClip.length) return;

            var frame = uAw.TimeToFrameRound(time);
            humanoidFootIK.updateFrame.Add(frame, weight);
        }
        private bool UpdateHumanoidFootIK()
        {
            if (!isHuman)
                return false;

            bool update = false;
            if (IsEnableUpdateHumanoidFootIK() &&
                !humanoidFootIK.updateFrame.IsEmpty())
            {
                var lastFrame = GetLastFrame();
                #region Tmp
                for (var ikIndex = AnimatorIKIndex.LeftFoot; ikIndex <= AnimatorIKIndex.RightFoot; ikIndex++)
                {
                    int index = ikIndex - AnimatorIKIndex.LeftFoot;
                    for (int i = 0; i < 3; i++)
                    {
                        humanoidFootIK.ikCurves[index].ikT[i] = GetAnimationCurveAnimatorIkT(ikIndex, i);
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        humanoidFootIK.ikCurves[index].ikQ[i] = GetAnimationCurveAnimatorIkQ(ikIndex, i);
                    }
                }
                #endregion
                #region Set
                {
                    calcObject.SetApplyIK(false);
                    calcObject.SetTransformStart();
                    var localToWorldRotation = transformPoseSave.startRotation;
                    var worldToLocalMatrix = transformPoseSave.startMatrix.inverse;
                    var humanScale = calcObject.animator.humanScale;
                    var leftFeetBottomHeight = calcObject.animator.leftFeetBottomHeight;
                    var rightFeetBottomHeight = calcObject.animator.rightFeetBottomHeight;
                    var postLeftFoot = GetAvatarPostRotation(HumanBodyBones.LeftFoot);
                    var postRightFoot = GetAvatarPostRotation(HumanBodyBones.RightFoot);
                    foreach (var pair in humanoidFootIK.updateFrame.frames)
                    {
                        var frame = pair.Key;
                        if (frame > lastFrame)
                            frame = lastFrame;
                        var time = GetFrameTime(frame);
                        calcObject.SampleAnimation(currentClip, time);
                        var rootT = GetAnimationValueAnimatorRootT(time);
                        var rootQ = GetAnimationValueAnimatorRootQ(time);
                        for (var ikIndex = AnimatorIKIndex.LeftFoot; ikIndex <= AnimatorIKIndex.RightFoot; ikIndex++)
                        {
                            var humanoidIndex = AnimatorIKIndex2HumanBodyBones[(int)ikIndex];
                            var t = calcObject.humanoidBones[(int)humanoidIndex].transform;
                            Vector3 position = t.position;
                            Quaternion rotation = t.rotation;
                            Vector3 ikT = position;
                            Quaternion ikQ = rotation;
                            {
                                Quaternion post = Quaternion.identity;
                                switch ((AnimatorIKIndex)ikIndex)
                                {
                                case AnimatorIKIndex.LeftFoot: post = postLeftFoot; break;
                                case AnimatorIKIndex.RightFoot: post = postRightFoot; break;
                                }
                                #region IkT
                                if (ikIndex == AnimatorIKIndex.LeftFoot || ikIndex == AnimatorIKIndex.RightFoot)
                                {
                                    Vector3 add = Vector3.zero;
                                    switch ((AnimatorIKIndex)ikIndex)
                                    {
                                    case AnimatorIKIndex.LeftFoot: add.x += leftFeetBottomHeight; break;
                                    case AnimatorIKIndex.RightFoot: add.x += rightFeetBottomHeight; break;
                                    }
                                    ikT += (rotation * post) * add;
                                }
                                ikT = worldToLocalMatrix.MultiplyPoint3x4(ikT) - (rootT * humanScale);
                                ikT = Quaternion.Inverse(rootQ) * ikT;
                                ikT *= 1f / humanScale;
                                #endregion
                                #region IkQ
                                ikQ = Quaternion.Inverse(localToWorldRotation * rootQ) * (rotation * post);
                                #endregion
                            }
                            int index = ikIndex - AnimatorIKIndex.LeftFoot;
                            for (int i = 0; i < 3; i++)
                            {
                                SetKeyframe(humanoidFootIK.ikCurves[index].ikT[i], time, ikT[i], false);
                            }
                            for (int i = 0; i < 4; i++)
                            {
                                SetKeyframe(humanoidFootIK.ikCurves[index].ikQ[i], time, ikQ[i], false);
                            }
                        }
                    }
                    calcObject.SetTransformStart();
                }
                #endregion
                #region SmoothTangent
                {
                    foreach (var pair in humanoidFootIK.updateFrame.frames)
                    {
                        var frame = pair.Key;
                        if (frame > lastFrame) continue;
                        var weight = pair.Value;
                        var time = GetFrameTime(frame);
                        for (var ikIndex = AnimatorIKIndex.LeftFoot; ikIndex <= AnimatorIKIndex.RightFoot; ikIndex++)
                        {
                            int index = ikIndex - AnimatorIKIndex.LeftFoot;
                            for (int i = 0; i < 3; i++)
                            {
                                var keyIndex = FindKeyframeAtTime(humanoidFootIK.ikCurves[index].ikT[i], time);
                                if (keyIndex >= 0)
                                    humanoidFootIK.ikCurves[index].ikT[i].SmoothTangents(keyIndex, weight);
                            }
                            for (int i = 0; i < 4; i++)
                            {
                                var keyIndex = FindKeyframeAtTime(humanoidFootIK.ikCurves[index].ikQ[i], time);
                                if (keyIndex >= 0)
                                    humanoidFootIK.ikCurves[index].ikQ[i].SmoothTangents(keyIndex, weight);
                            }
                        }
                    }
                }
                #endregion
                #region Write
                for (var ikIndex = AnimatorIKIndex.LeftFoot; ikIndex <= AnimatorIKIndex.RightFoot; ikIndex++)
                {
                    int index = ikIndex - AnimatorIKIndex.LeftFoot;
                    for (int i = 0; i < 3; i++)
                    {
                        SetAnimationCurveAnimatorIkT(ikIndex, i, humanoidFootIK.ikCurves[index].ikT[i]);
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        SetAnimationCurveAnimatorIkQ(ikIndex, i, humanoidFootIK.ikCurves[index].ikQ[i]);
                    }
                }
                #endregion

                update = true;
            }
            humanoidFootIK.Clear();
            return update;
        }
        #endregion

        #region Collision
        private class Collision
        {
            public int savedObjectDataFrame = -1;

            [System.Diagnostics.DebuggerDisplay("{renderer.name}")]
            public class EditObjectData
            {
                public const int VertexGroupCount = 300;

                public Renderer renderer;
                public UnityEngine.Object nearestPrefabInstanceRoot;
                public MeshFilter meshFilter;
                //Save
                public Matrix4x4 saveLocalToWorldMatrix;
                public Mesh saveMesh;
                public bool createSaveMesh;
                public Mesh updateMesh;
                public bool createUpdateMesh;
                //Calc
                public List<Vector3> saveVertices = new List<Vector3>();
                public List<Vector3> updateVertices = new List<Vector3>();
                public Bounds bounds;
                public bool isMove;
                [System.Diagnostics.DebuggerDisplay("\"({bounds}, VertexCount: {vertices.Count}\")")]
                public class VertexGroup
                {
                    public Bounds bounds;
                    public List<int> vertices = new List<int>();
                    //Calc
                    public List<int> intersectsTriangleGroups = new List<int>();
                    public float rate;
                    public Vector3 hitPosition;
                }
                public VertexGroup[] vertexGroups;

                public void ResetUpdateCalc()
                {
                    saveVertices.Clear();
                    updateVertices.Clear();
                    bounds = new Bounds();
                    isMove = false;
                }
                public void SetUpdateCalc()
                {
                    saveMesh.GetVertices(saveVertices);
                    updateMesh.GetVertices(updateVertices);
                    Matrix4x4 localToWorldMatrix;
                    {
                        if (renderer is SkinnedMeshRenderer)
                            localToWorldMatrix = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
                        else
                            localToWorldMatrix = renderer.localToWorldMatrix;
                    }
                    {
                        var groupNum = Mathf.CeilToInt(updateVertices.Count / (float)VertexGroupCount);
                        if (vertexGroups == null || vertexGroups.Length != groupNum)
                            vertexGroups = new VertexGroup[groupNum];
#if ENABLE_PARALLEL
                        Parallel.For(0, groupNum, i =>
#else
                        for (int i = 0; i < groupNum; i++)
#endif
                        {
                            if (vertexGroups[i] == null)
                                vertexGroups[i] = new VertexGroup();
                            vertexGroups[i].vertices.Clear();
                            vertexGroups[i].bounds = new Bounds();

                            int begin, end;
                            GetVertexGroupRange(i, out begin, out end);
                            for (int v = begin; v < end; v++)
                            {
                                saveVertices[v] = saveLocalToWorldMatrix.MultiplyPoint3x4(saveVertices[v]);
                                updateVertices[v] = localToWorldMatrix.MultiplyPoint3x4(updateVertices[v]);

                                const float IgnoreMin = 0.0001f;
                                if (Mathf.Abs(saveVertices[v].x - updateVertices[v].x) < IgnoreMin &&
                                    Mathf.Abs(saveVertices[v].y - updateVertices[v].y) < IgnoreMin &&
                                    Mathf.Abs(saveVertices[v].z - updateVertices[v].z) < IgnoreMin)
                                    continue;

                                if (vertexGroups[i].vertices.Count == 0)
                                    vertexGroups[i].bounds = new Bounds(saveVertices[v], Vector3.zero);
                                else
                                    vertexGroups[i].bounds.Encapsulate(saveVertices[v]);
                                vertexGroups[i].bounds.Encapsulate(updateVertices[v]);
                                vertexGroups[i].vertices.Add(v);
                            }
#if ENABLE_PARALLEL
                        });
#else
                        }
#endif
                        {
                            bounds = new Bounds();
                            isMove = false;
                            for (int i = 0; i < groupNum; i++)
                            {
                                if (vertexGroups[i].vertices.Count <= 0)
                                    continue;
                                if (!isMove)
                                {
                                    bounds = vertexGroups[i].bounds;
                                    isMove = true;
                                }
                                else
                                {
                                    bounds.Encapsulate(vertexGroups[i].bounds);
                                }
                            }
                        }
                    }
                }
                public void GetVertexGroupRange(int groupIndex, out int begin, out int end)
                {
                    begin = groupIndex * VertexGroupCount;
                    end = begin + VertexGroupCount;
                    if (end > saveVertices.Count)
                        end = saveVertices.Count;
                }
            }
            public Dictionary<Renderer, EditObjectData> editObjectData = new Dictionary<Renderer, EditObjectData>();

            public abstract class CollisionRendererData
            {
                public const int VertexGroupCount = 300;
                public const int TriangleGroupCount = 30;

                public Renderer renderer;
                public UnityEngine.Object nearestPrefabInstanceRoot;

                public float savedTime;
                public List<Vector3> vertices = new List<Vector3>();
                public List<int> triangles = new List<int>();
                public List<int> tmpTriangles = new List<int>();
                public Bounds bounds;
                public Bounds[] triangleBounds;
                public Vector3[] triangleNormals;
                public Bounds[] triangleGroupBounds;

                public bool HasBuffer { get { return vertices.Count > 0; } }

                public virtual void Release() { }

                public virtual void ResetCalc()
                {
                    savedTime = -1f;
                    vertices.Clear();
                    triangles.Clear();
                    tmpTriangles.Clear();
                }
                public void SetCalc(float time)
                {
                    savedTime = time;
                    var mesh = GetBakedMesh();
                    mesh.GetVertices(vertices);
                    {
                        triangles.Clear();
                        for (int i = 0; i < mesh.subMeshCount; i++)
                        {
                            if (mesh.GetTopology(i) == MeshTopology.Triangles)
                            {
                                mesh.GetTriangles(tmpTriangles, i);
                                triangles.AddRange(tmpTriangles);
                            }
                        }
                        tmpTriangles.Clear();
                    }
                    {
                        Matrix4x4 localToWorldMatrix;
                        if (renderer is SkinnedMeshRenderer)
                            localToWorldMatrix = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
                        else
                            localToWorldMatrix = renderer.localToWorldMatrix;
                        var groupNum = Mathf.CeilToInt(vertices.Count / (float)VertexGroupCount);
#if ENABLE_PARALLEL
                        Parallel.For(0, groupNum, i =>
#else
                        for (int i = 0; i < groupNum; i++)
#endif
                        {
                            int begin, end;
                            GetVertexGroupRange(i, out begin, out end);
                            for (int v = begin; v < end; v++)
                            {
                                vertices[v] = localToWorldMatrix.MultiplyPoint3x4(vertices[v]);
                            }
#if ENABLE_PARALLEL
                        });
#else
                        }
#endif
                    }
                    {
                        Assert.IsTrue(TriangleGroupCount % 3 == 0);
                        var groupNum = Mathf.CeilToInt(triangles.Count / (float)TriangleGroupCount);
                        if (triangleBounds == null || triangleBounds.Length != triangles.Count / 3)
                            triangleBounds = new Bounds[triangles.Count / 3];
                        if (triangleNormals == null || triangleNormals.Length != triangles.Count)
                            triangleNormals = new Vector3[triangles.Count];
                        if (triangleGroupBounds == null || triangleGroupBounds.Length != groupNum)
                            triangleGroupBounds = new Bounds[groupNum];
#if ENABLE_PARALLEL
                        Parallel.For(0, groupNum, i =>
#else
                        for (int i = 0; i < groupNum; i++)
#endif
                        {
                            int begin, end;
                            GetTriangleGroupRange(i, out begin, out end);
                            for (int triangleIndex = begin; triangleIndex < end; triangleIndex++)
                            {
                                var vt = triangleIndex * 3;
                                {
                                    triangleBounds[triangleIndex] = new Bounds(vertices[triangles[vt + 0]], Vector3.zero);
                                    triangleBounds[triangleIndex].Encapsulate(vertices[triangles[vt + 1]]);
                                    triangleBounds[triangleIndex].Encapsulate(vertices[triangles[vt + 2]]);
                                }
                                {
                                    triangleNormals[triangleIndex] = Vector3.Cross(vertices[triangles[vt + 0]] - vertices[triangles[vt + 1]],
                                                                                    vertices[triangles[vt + 1]] - vertices[triangles[vt + 2]]).normalized;
                                }
                                if (triangleIndex == begin)
                                    triangleGroupBounds[i] = triangleBounds[triangleIndex];
                                else
                                    triangleGroupBounds[i].Encapsulate(triangleBounds[triangleIndex]);
                            }
#if ENABLE_PARALLEL
                        });
#else
                        }
#endif
                        bounds = triangleGroupBounds[0];
                        for (int i = 1; i < groupNum; i++)
                            bounds.Encapsulate(triangleGroupBounds[i]);
                    }
                }

                public void GetVertexGroupRange(int groupIndex, out int begin, out int end)
                {
                    begin = groupIndex * VertexGroupCount;
                    end = begin + VertexGroupCount;
                    if (end > vertices.Count)
                        end = vertices.Count;
                }
                public void GetTriangleGroupRange(int groupIndex, out int begin, out int end)
                {
                    begin = groupIndex * TriangleGroupCount;
                    end = begin + TriangleGroupCount;
                    if (end > triangles.Count)
                        end = triangles.Count;
                    begin /= 3;
                    end /= 3;
                }

                public abstract Mesh GetCurrentMesh();
                public abstract Mesh GetBakedMesh();
            }
            [System.Diagnostics.DebuggerDisplay("{renderer.name}")]
            public class CollisionMeshRendererData : CollisionRendererData
            {
                public MeshRenderer meshRenderer;
                public MeshFilter meshFilter;

                public override Mesh GetCurrentMesh()
                {
                    if (meshFilter != null)
                        return meshFilter.sharedMesh;
                    return null;
                }
                public override Mesh GetBakedMesh()
                {
                    return GetCurrentMesh();
                }
            }
            [System.Diagnostics.DebuggerDisplay("{renderer.name}")]
            public class CollisionSkinnedMeshRendererData : CollisionRendererData
            {
                public SkinnedMeshRenderer skinnedMeshRenderer;
                public Mesh bakedMesh;

                public override void Release()
                {
                    base.Release();

                    if (bakedMesh != null)
                    {
                        Mesh.DestroyImmediate(bakedMesh);
                        bakedMesh = null;
                    }
                }

                public override void ResetCalc()
                {
                    base.ResetCalc();

                    if (bakedMesh != null)
                    {
                        Mesh.DestroyImmediate(bakedMesh);
                        bakedMesh = null;
                    }
                }

                public override Mesh GetCurrentMesh()
                {
                    if (skinnedMeshRenderer != null)
                        return skinnedMeshRenderer.sharedMesh;
                    return null;
                }
                public override Mesh GetBakedMesh()
                {
                    if (skinnedMeshRenderer != null)
                    {
                        var mesh = skinnedMeshRenderer.sharedMesh;
                        if (mesh != null)
                        {
                            if (bakedMesh == null)
                            {
                                bakedMesh = new Mesh();
                                bakedMesh.hideFlags |= HideFlags.HideAndDontSave;
                                skinnedMeshRenderer.BakeMesh(bakedMesh);
                            }
                            return bakedMesh;
                        }
                    }
                    return null;
                }
            }
            public Dictionary<Renderer, CollisionRendererData> collisionObjectData;

            public List<int> updateBoneIndexes = new List<int>();

            public PointSignal collisionSignal = new PointSignal();

            public void Release()
            {
                if (editObjectData != null)
                {
                    foreach (var pair in editObjectData)
                    {
                        if (pair.Value.createSaveMesh && pair.Value.saveMesh != null)
                            Mesh.DestroyImmediate(pair.Value.saveMesh);
                        if (pair.Value.createUpdateMesh && pair.Value.updateMesh != null)
                            Mesh.DestroyImmediate(pair.Value.updateMesh);
                    }
                    editObjectData = null;
                }
                if (collisionObjectData != null)
                {
                    foreach (var pair in collisionObjectData)
                    {
                        pair.Value.Release();
                    }
                    collisionObjectData = null;
                }
            }
        }
        private Collision collision;
        private void ReleaseCollision()
        {
            if (collision != null)
            {
                collision.Release();
                collision = null;
            }
        }
        private void SaveCollision(bool forceUpdate)
        {
            if (!collisionEnable)
            {
                ReleaseCollision();
                return;
            }
            else if (collision == null)
            {
                collision = new Collision();
            }

            var currentFrame = uAw.GetCurrentFrame();

            #region NotUpdateCheck
            if (!forceUpdate)
            {
                if (collision.savedObjectDataFrame == currentFrame)
                    return;
            }
            #endregion

            collision.savedObjectDataFrame = currentFrame;

            #region Collision.EditObjectData
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                    continue;
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                    continue;

                Collision.EditObjectData data;
                if (!collision.editObjectData.TryGetValue(renderer, out data))
                {
                    data = new Collision.EditObjectData();
                    data.renderer = renderer;
#if UNITY_2018_3_OR_NEWER
                    data.nearestPrefabInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(renderer.gameObject);
#else
                    data.nearestPrefabInstanceRoot = PrefabUtility.FindPrefabRoot(renderer.gameObject);
#endif
                    if (renderer is MeshRenderer)
                    {
                        var meshRenderer = renderer as MeshRenderer;
                        data.meshFilter = meshRenderer.GetComponent<MeshFilter>();
                    }
                    collision.editObjectData.Add(renderer, data);
                }

                if (renderer is MeshRenderer)
                {
                    data.saveLocalToWorldMatrix = renderer.localToWorldMatrix;
                    if (data.meshFilter != null)
                        data.saveMesh = data.meshFilter.sharedMesh;
                }
                else if (renderer is SkinnedMeshRenderer)
                {
                    data.saveLocalToWorldMatrix = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
                    if (data.saveMesh == null)
                    {
                        data.saveMesh = new Mesh();
                        data.saveMesh.hideFlags |= HideFlags.HideAndDontSave;
                        data.createSaveMesh = true;
                    }
                    var skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
                    skinnedMeshRenderer.BakeMesh(data.saveMesh);
                }
            }
            #endregion

            #region collision.collisionObjectData 
            if (collision.collisionObjectData == null)
            {
                collision.collisionObjectData = new Dictionary<Renderer, Collision.CollisionRendererData>();
                Action<GameObject> AddGameObject = (go) =>
                {
                    Func<Transform, bool> CheckHideFlags = null;
                    CheckHideFlags = (t) =>
                    {
                        if ((t.gameObject.hideFlags & (HideFlags.HideAndDontSave | HideFlags.NotEditable)) != 0)
                            return false;

                        if (t.parent != null)
                            return CheckHideFlags(t.parent);
                        else
                            return true;
                    };

                    #region MeshRenderer
                    {
                        var meshRenderers = go.GetComponentsInChildren<MeshRenderer>(true);
                        if (meshRenderers != null && meshRenderers.Length > 0)
                        {
                            foreach (var meshRenderer in meshRenderers)
                            {
                                if (meshRenderer == null || !meshRenderer.enabled || !meshRenderer.gameObject.activeInHierarchy)
                                    continue;
                                if (!CheckHideFlags(meshRenderer.transform))
                                    continue;
#if UNITY_2018_3_OR_NEWER
                                var prefab = PrefabUtility.GetNearestPrefabInstanceRoot(meshRenderer.gameObject);
#else
                                var prefab = PrefabUtility.FindPrefabRoot(meshRenderer.gameObject);
#endif
                                collision.collisionObjectData.Add(meshRenderer, new Collision.CollisionMeshRendererData()
                                {
                                    renderer = meshRenderer,
                                    nearestPrefabInstanceRoot = prefab,
                                    meshRenderer = meshRenderer,
                                    meshFilter = meshRenderer.GetComponent<MeshFilter>(),
                                });
                            }
                        }
                    }
                    #endregion
                    #region SkinnedMeshRenderer
                    {
                        var skinnedMeshRenderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                        if (skinnedMeshRenderers != null && skinnedMeshRenderers.Length > 0)
                        {
                            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
                            {
                                if (skinnedMeshRenderer == null || !skinnedMeshRenderer.enabled || !skinnedMeshRenderer.gameObject.activeInHierarchy)
                                    continue;
                                if (!CheckHideFlags(skinnedMeshRenderer.transform))
                                    continue;
#if UNITY_2018_3_OR_NEWER
                                var prefab = PrefabUtility.GetNearestPrefabInstanceRoot(skinnedMeshRenderer.gameObject);
#else
                                var prefab = PrefabUtility.FindPrefabRoot(skinnedMeshRenderer.gameObject);
#endif
                                collision.collisionObjectData.Add(skinnedMeshRenderer, new Collision.CollisionSkinnedMeshRendererData()
                                {
                                    renderer = skinnedMeshRenderer,
                                    nearestPrefabInstanceRoot = prefab,
                                    skinnedMeshRenderer = skinnedMeshRenderer,
                                });
                            }
                        }
                    }
                    #endregion
                };

#if UNITY_2018_3_OR_NEWER
                if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                {
                    var scene = PrefabStageUtility.GetCurrentPrefabStage().scene;
                    foreach (var go in scene.GetRootGameObjects())
                        AddGameObject(go);
                }
                else
#endif
                {
                    for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                    {
                        var scene = SceneManager.GetSceneAt(sceneIndex);
                        foreach (var go in scene.GetRootGameObjects())
                        {
                            AddGameObject(go);
                        }
                    }
                }
            }
            #endregion
        }
        private void UpdateCollision()
        {
            if (!collisionEnable || updatePoseFixAnimation || curvesWasModified.Count <= 0 ||
                collision == null || collision.editObjectData == null || collision.collisionObjectData == null)
            {
                return;
            }

            #region Ready
            #region DataReady
            {
                foreach (var pair in collision.editObjectData)
                {
                    Collision.CollisionRendererData data;
                    if (collision.collisionObjectData.TryGetValue(pair.Key, out data))
                        data.ResetCalc();
                }
                if (vaw.editorSettings.settingExtraSynchronizeAnimation)
                {
                    foreach (var pair in collision.collisionObjectData)
                    {
                        var data = pair.Value;
                        if (data.savedTime != currentTime)
                            data.ResetCalc();
                    }
                }
            }
            #endregion
            #region updateBoneIndexes
            bool hasHumanRoot = false;
            {
                collision.updateBoneIndexes.Clear();
                foreach (var pair in curvesWasModified)
                {
                    if (pair.Value.deleted != AnimationUtility.CurveModifiedType.CurveModified)
                        continue;
                    var boneIndex = GetBoneIndexFromCurveBinding(pair.Value.binding);
                    if (boneIndex >= 0)
                    {
                        if (!collision.updateBoneIndexes.Contains(boneIndex))
                        {
                            collision.updateBoneIndexes.Add(boneIndex);
                            if (isHuman && boneIndex == 0)
                                hasHumanRoot = true;
                        }
                    }
                }
                collision.updateBoneIndexes.Sort((a, b) => boneHierarchyLevels[a] - boneHierarchyLevels[b]);
            }
            #endregion
            #endregion

            const int MaxIterations = 3;
            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                bool endFlag = true;
                foreach (var updateBoneIndex in collision.updateBoneIndexes)
                {
                    endFlag = true;

                    #region SampleAnimation
                    if (uAw.GetLinkedWithTimeline())
                    {
                        PlayableDirectorEvaluateImmediate();
                    }
                    else
                    {
                        SampleAnimation(currentTime, EditObjectFlag.Base);
                    }
                    #endregion

                    #region Update
                    foreach (var pair in collision.editObjectData)
                    {
                        var renderer = pair.Key;
                        var data = pair.Value;

                        data.ResetUpdateCalc();

                        if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                            continue;

                        if (renderer is MeshRenderer)
                        {
                            if (data.meshFilter == null)
                                continue;

                            data.updateMesh = data.meshFilter.sharedMesh;
                        }
                        else if (renderer is SkinnedMeshRenderer)
                        {
                            if (data.updateMesh == null)
                            {
                                data.updateMesh = new Mesh();
                                data.updateMesh.hideFlags |= HideFlags.HideAndDontSave;
                                data.createUpdateMesh = true;
                            }

                            var skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
                            skinnedMeshRenderer.BakeMesh(data.updateMesh);
                        }
                        if (data.saveMesh.vertexCount != data.updateMesh.vertexCount)
                            continue;

                        data.SetUpdateCalc();
                    }
                    #endregion

                    #region Calc
                    float rate = 1f;
                    Vector3 hitPosition = Vector3.zero;
                    foreach (var pair in collision.editObjectData)
                    {
                        var renderer = pair.Key;
                        var data = pair.Value;

                        #region IgnoreCheck
                        if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                            continue;
                        if (!data.isMove)
                            continue;
                        #endregion

                        foreach (var meshPair in collision.collisionObjectData)
                        {
                            var meshData = meshPair.Value;

                            #region IgnoreCheck
                            if (meshData.renderer == null || !meshData.renderer.gameObject.activeInHierarchy || !meshData.renderer.enabled)
                                continue;
                            if (meshData.GetCurrentMesh() == null)
                                continue;
                            if (renderer == meshData.renderer)
                                continue;
                            if (data.nearestPrefabInstanceRoot != null && data.nearestPrefabInstanceRoot == meshData.nearestPrefabInstanceRoot)
                                continue;
                            {
                                Collision.EditObjectData otherData;
                                if (collision.editObjectData.TryGetValue(meshPair.Key, out otherData))
                                {
                                    if (otherData.isMove)
                                        continue;
                                }
                            }
                            #endregion

                            if (!meshData.HasBuffer)
                            {
                                //If it is uncertain but you do not create information, exclude it here
                                if (meshData.renderer is MeshRenderer)  //ignore SkinnedMeshRenderer
                                {
                                    if (!data.bounds.Intersects(meshData.renderer.bounds))
                                        continue;
                                }
                            }

                            #region DataReady
                            if (!meshData.HasBuffer)   //First time only
                                meshData.SetCalc(currentTime);
                            #endregion

                            //Certain exclusion decision that created the information
                            if (!data.bounds.Intersects(meshData.bounds))
                                continue;

                            #region EditObjectVertex vs CollisionObjectTriangle
#if ENABLE_PARALLEL
                            Parallel.ForEach(data.vertexGroups, group =>
#else
                            foreach (var group in data.vertexGroups)
#endif
                            {
                                group.rate = 1f;
                                if (group.vertices.Count > 0 &&
                                    group.bounds.Intersects(meshData.bounds))
                                {
                                    group.intersectsTriangleGroups.Clear();
                                    for (int tGroup = 0; tGroup < meshData.triangleGroupBounds.Length; tGroup++)
                                    {
                                        if (!meshData.triangleGroupBounds[tGroup].Intersects(group.bounds))
                                            continue;
                                        group.intersectsTriangleGroups.Add(tGroup);
                                    }
                                    if (group.intersectsTriangleGroups.Count > 0)
                                    {
                                        #region EditMeshVertex vs OtherMeshTriangle
                                        float minDistance = float.MaxValue;
                                        foreach (var v in group.vertices)
                                        {
                                            var worldRay = new Ray(data.saveVertices[v], (data.updateVertices[v] - data.saveVertices[v]).normalized);

                                            {
                                                float distance;
                                                if (!meshData.bounds.IntersectRay(worldRay, out distance))
                                                    continue;
                                                if (minDistance < distance)
                                                    continue;
                                                if (Vector3.Dot(data.updateVertices[v] - worldRay.GetPoint(distance), worldRay.direction) < 0f)
                                                    continue;
                                            }

                                            foreach (var tGroup in group.intersectsTriangleGroups)
                                            {
                                                {
                                                    float distance;
                                                    if (!meshData.triangleGroupBounds[tGroup].IntersectRay(worldRay, out distance))
                                                        continue;
                                                    if (minDistance < distance)
                                                        continue;
                                                }

                                                int begin, end;
                                                meshData.GetTriangleGroupRange(tGroup, out begin, out end);
                                                for (int triangleIndex = begin; triangleIndex < end; triangleIndex++)
                                                {
                                                    {
                                                        float distance;
                                                        if (!meshData.triangleBounds[triangleIndex].IntersectRay(worldRay, out distance))
                                                            continue;
                                                        if (minDistance < distance)
                                                            continue;
                                                    }

                                                    if (Vector3.Dot(worldRay.direction, meshData.triangleNormals[triangleIndex]) >= 0f)
                                                        continue;

                                                    var vt = triangleIndex * 3;

                                                    Vector3 posP;
                                                    if (!EditorCommon.Ray_Triangle(worldRay,
                                                                                    meshData.vertices[meshData.triangles[vt + 0]],
                                                                                    meshData.vertices[meshData.triangles[vt + 1]],
                                                                                    meshData.vertices[meshData.triangles[vt + 2]],
                                                                                    out posP))
                                                    {
                                                        continue;
                                                    }

                                                    var vecAP = posP - data.saveVertices[v];
                                                    var vecAB = data.updateVertices[v] - data.saveVertices[v];
                                                    var subRate = vecAP.magnitude / vecAB.magnitude;
                                                    if (subRate < group.rate)
                                                    {
                                                        group.rate = subRate;
                                                        group.hitPosition = posP;
                                                        minDistance = Mathf.Min(minDistance, Vector3.Distance(posP, worldRay.origin));
                                                    }
                                                }
                                            }
                                        }
                                        #endregion
                                    }
                                }
#if ENABLE_PARALLEL
                            });
#else
                            }
#endif
                            foreach (var group in data.vertexGroups)
                            {
                                if (group.rate < rate)
                                {
                                    rate = group.rate;
                                    hitPosition = group.hitPosition;
                                }
                            }
                            #endregion
                        }
                    }
                    if (rate >= 1f)
                        break;
                    if (iteration < MaxIterations - 1)
                        rate = Mathf.Max(rate - 0.01f, 0f);    //Minute extrusion
                    else
                        rate = 0f;
                    #endregion

                    #region Write
                    {
                        bool written = false;
                        foreach (var pair in curvesWasModified)
                        {
                            if (pair.Value.deleted != AnimationUtility.CurveModifiedType.CurveModified)
                                continue;
                            var curve = GetEditorCurveCache(pair.Value.binding);
                            if (curve == null || pair.Value.beforeCurve == null)
                                continue;
                            var beforeValue = pair.Value.beforeCurve.Evaluate(currentTime);
                            var currentValue = curve.Evaluate(currentTime);
                            if (!Mathf.Approximately(beforeValue, currentValue))
                            {
                                var newValue = Mathf.LerpUnclamped(beforeValue, currentValue, rate);
                                if (!Mathf.Approximately(newValue, currentValue))
                                {
                                    if (hasHumanRoot || updateBoneIndex == GetBoneIndexFromCurveBinding(pair.Value.binding))
                                    {
                                        SetKeyframe(curve, currentTime, newValue);
                                        SetEditorCurveCache(pair.Value.binding, curve);
                                        written = true;
                                    }
                                }
                            }
                        }
                        if (!written)
                            break;
                    }
                    #endregion

                    collision.collisionSignal.Fire(hitPosition, 0.5f, Color.red);
                    endFlag = false;
                }
                if (endFlag)
                    break;
            }
        }
        public bool DrawCollision()
        {
            if (collision == null)
                return false;
            return collision.collisionSignal.Draw();
        }
        #endregion

        #region AnimationPlayable
#if UNITY_2019_1_OR_NEWER
        private class AnimationPlayable
        {
            public UAnimationMotionXToDeltaPlayable uAnimationMotionXToDeltaPlayable;
            public UAnimationOffsetPlayable uAnimationOffsetPlayable;
            public UAnimationClipPlayable uAnimationClipPlayable;

            public Playable animationOffsetPlayable;

            public AnimationPlayable()
            {
                uAnimationMotionXToDeltaPlayable = new UAnimationMotionXToDeltaPlayable();
                uAnimationOffsetPlayable = new UAnimationOffsetPlayable();
                uAnimationClipPlayable = new UAnimationClipPlayable();
            }
            public void Release()
            {
                animationOffsetPlayable = Playable.Null;
            }
        }
        private AnimationPlayable animationPlayable;
#endif

        private void InitializeAnimationPlayable()
        {
            ReleaseAnimationPlayable();

#if UNITY_2019_1_OR_NEWER
            animationPlayable = new AnimationPlayable();
#endif
        }
        private void ReleaseAnimationPlayable()
        {
#if UNITY_2019_1_OR_NEWER
            if (animationPlayable != null)
            {
                animationPlayable.Release();
                animationPlayable = null;
            }
#endif
        }
        #endregion

        #region HandPoseSet
        [Serializable]
        public class HandPoseSet
        {
            public PoseTemplate poseTemplate;
            public string[] leftMusclePropertyNames;
            public string[] rightMusclePropertyNames;

            public void SetLeft()
            {
                poseTemplate.musclePropertyNames = leftMusclePropertyNames;
            }
            public void SetRight()
            {
                poseTemplate.musclePropertyNames = rightMusclePropertyNames;
            }

            [NonSerialized]
            public Texture2D iconLeft;
            [NonSerialized]
            public Texture2D iconRight;
        }
        [SerializeField]
        public List<HandPoseSet> handPoseSetList;

        private void InitializeHandPoseSetList()
        {
            ReleaseHandPoseSetList();

            handPoseSetList = new List<HandPoseSet>();
        }
        private void ReleaseHandPoseSetList()
        {
            if (handPoseSetList != null)
            {
                foreach (var item in handPoseSetList)
                {
                    if (item.iconLeft != null)
                    {
                        Texture2D.DestroyImmediate(item.iconLeft);
                        item.iconLeft = null;
                    }
                    if (item.iconRight != null)
                    {
                        Texture2D.DestroyImmediate(item.iconRight);
                        item.iconRight = null;
                    }
                }
                handPoseSetList.Clear();
            }
        }
        #endregion

        #region BlendShapeSet
        [Serializable]
        public class BlendShapeSet
        {
            public PoseTemplate poseTemplate;
            [NonSerialized]
            public Texture2D icon;
        }
        [SerializeField]
        public List<BlendShapeSet> blendShapeSetList;

        private void InitializeBlendShapeSetList()
        {
            ReleaseBlendShapeSetList();

            blendShapeSetList = new List<BlendShapeSet>();
        }
        private void ReleaseBlendShapeSetList()
        {
            if (blendShapeSetList != null)
            {
                foreach (var item in blendShapeSetList)
                {
                    if (item.icon != null)
                    {
                        Texture2D.DestroyImmediate(item.icon);
                        item.icon = null;
                    }
                }
                blendShapeSetList.Clear();
            }
        }
        #endregion

        #region AnimationTool
        public HumanBodyBones GetHumanVirtualBoneParentBone(HumanBodyBones bone)
        {
            if (!isHuman) return (HumanBodyBones)(-1);
            var vbs = HumanVirtualBones[(int)bone];
            if (vbs != null)
            {
                foreach (var vb in vbs)
                {
                    if (humanoidBones[(int)vb.boneA] == null) continue;
                    return vb.boneA;
                }
            }
            return (HumanBodyBones)(-1);
        }
        public Vector3 GetHumanVirtualBoneLimitSign(HumanBodyBones bone)
        {
            if (!isHuman) return Vector3.one;
            var vbs = HumanVirtualBones[(int)bone];
            if (vbs != null)
            {
                foreach (var vb in vbs)
                {
                    if (humanoidBones[(int)vb.boneA] == null) continue;
                    return vb.limitSign;
                }
            }
            return Vector3.one;
        }

        public Vector3 GetHumanVirtualBonePosition(HumanBodyBones bone)
        {
            if (!isHuman) return Vector3.zero;
            var vbs = HumanVirtualBones[(int)bone];
            if (vbs != null)
            {
                foreach (var vb in vbs)
                {
                    if (editHumanoidBones[(int)vb.boneA] == null || editHumanoidBones[(int)vb.boneB] == null) continue;
                    var posA = editHumanoidBones[(int)vb.boneA].transform.position;
                    var posB = editHumanoidBones[(int)vb.boneB].transform.position;
                    return Vector3.Lerp(posA, posB, vb.leap);
                }
            }
            return Vector3.zero;
        }
        public Quaternion GetHumanVirtualBoneRotation(HumanBodyBones bone)
        {
            if (!isHuman) return Quaternion.identity;
            var vbs = HumanVirtualBones[(int)bone];
            if (vbs != null)
            {
                foreach (var vb in vbs)
                {
                    if (editHumanoidBones[(int)vb.boneA] == null) continue;
                    var vRotation = Vector3.zero;
                    for (int i = 0; i < 3; i++)
                    {
                        var mi = HumanTrait.MuscleFromBone((int)bone, i);
                        if (i >= 0)
                        {
                            var muscle = GetAnimationValueAnimatorMuscle(mi);
                            vRotation[i] = Mathf.Lerp(humanoidMuscleLimit[(int)bone].min[i], humanoidMuscleLimit[(int)bone].max[i], (muscle + 1f) / 2f);
                        }
                    }
                    var qRotation = Quaternion.Euler(vRotation);
                    var parentRotation = editHumanoidBones[(int)vb.boneA].transform.rotation * GetAvatarPostRotation(vb.boneA);
                    return parentRotation * qRotation;
                }
            }
            return Quaternion.identity;
        }
        public Quaternion GetHumanVirtualBoneParentRotation(HumanBodyBones bone)
        {
            if (!isHuman) return Quaternion.identity;
            var vbs = HumanVirtualBones[(int)bone];
            if (vbs != null)
            {
                foreach (var vb in vbs)
                {
                    if (editHumanoidBones[(int)vb.boneA] == null) continue;
                    return editHumanoidBones[(int)vb.boneA].transform.rotation * GetAvatarPostRotation(vb.boneA) * vb.addRotation;
                }
            }
            return Quaternion.identity;
        }

        public Vector3 GetHumanWorldRootPosition()
        {
            if (!isHuman) return Vector3.zero;
            var bodyPosition = GetAnimationValueAnimatorRootT() * editAnimator.humanScale;
            return transformPoseSave.startMatrix.MultiplyPoint3x4(bodyPosition);
        }
        public Vector3 GetHumanLocalRootPosition(Vector3 pos)
        {
            if (!isHuman) return Vector3.zero;
            var bodyPosition = transformPoseSave.startMatrix.inverse.MultiplyPoint3x4(pos);
            return bodyPosition / editAnimator.humanScale;
        }
        public Quaternion GetHumanWorldRootRotation()
        {
            if (!isHuman) return Quaternion.identity;
            return transformPoseSave.startRotation * GetAnimationValueAnimatorRootQ();
        }
        public Quaternion GetHumanLocalRootRotation(Quaternion rot)
        {
            if (!isHuman) return Quaternion.identity;
            return Quaternion.Inverse(transformPoseSave.startRotation) * rot;
        }

        public Vector3 GetAnimatorWorldMotionPosition()
        {
            if (editAnimator == null) return Vector3.zero;
            var scale = 1f;
            if (isHuman) scale = editAnimator.humanScale;
            var bodyPosition = GetAnimationValueAnimatorMotionT() * scale;
            return transformPoseSave.startMatrix.MultiplyPoint3x4(bodyPosition);
        }
        public Vector3 GetAnimatorLocalMotionPosition(Vector3 pos)
        {
            if (editAnimator == null) return Vector3.zero;
            var scale = 1f;
            if (isHuman) scale = editAnimator.humanScale;
            var bodyPosition = transformPoseSave.startMatrix.inverse.MultiplyPoint3x4(pos);
            return bodyPosition / scale;
        }
        public Quaternion GetAnimatorWorldMotionRotation()
        {
            if (editAnimator == null) return Quaternion.identity;
            return transformPoseSave.startRotation * GetAnimationValueAnimatorMotionQ();
        }
        public Quaternion GetAnimatorLocalMotionRotation(Quaternion rot)
        {
            if (editAnimator == null) return Quaternion.identity;
            return Quaternion.Inverse(transformPoseSave.startRotation) * rot;
        }

        public bool GetPlayingAnimationInfo(out AnimationClip dstClip, out float dstTime, out float dstLength)
        {
            dstClip = null;
            dstTime = 0f;
            dstLength = 0f;
            if (!EditorApplication.isPlaying) return false;
            if (vaw.animator != null && vaw.animator.runtimeAnimatorController != null && vaw.animator.isInitialized)
            {
                #region animator
                var ac = EditorCommon.GetAnimatorController(vaw.animator);
                var owc = vaw.animator.runtimeAnimatorController as AnimatorOverrideController;
                if (vaw.animator.layerCount > 0)
                {
                    var layerIndex = 0;
                    var currentAnimatorStateInfo = vaw.animator.GetCurrentAnimatorStateInfo(layerIndex);
                    AnimationClip resultClip = null;
                    float resultTime = 0f;
                    float resultLength = 0f;
                    Func<AnimatorStateMachine, bool> FindStateMachine = null;
                    FindStateMachine = (stateMachine) =>
                    {
                        foreach (var state in stateMachine.states)
                        {
                            if (state.state.nameHash != currentAnimatorStateInfo.shortNameHash ||
                                currentAnimatorStateInfo.length <= 0f)
                                continue;

                            Func<Motion, AnimationClip> FindMotion = null;
                            FindMotion = (motion) =>
                            {
                                if (motion != null)
                                {
                                    if (motion is UnityEditor.Animations.BlendTree)
                                    {
                                        #region BlendTree
                                        var blendTree = motion as UnityEditor.Animations.BlendTree;
                                        switch (blendTree.blendType)
                                        {
                                        case BlendTreeType.Simple1D:
                                            #region 1D
                                            {
                                                var param = vaw.animator.GetFloat(blendTree.blendParameter);
                                                float near = float.MaxValue;
                                                int index = -1;
                                                for (int i = 0; i < blendTree.children.Length; i++)
                                                {
                                                    var offset = Mathf.Abs(blendTree.children[i].threshold - param);
                                                    if (offset < near)
                                                    {
                                                        index = i;
                                                        near = offset;
                                                    }
                                                }
                                                if (index >= 0)
                                                {
                                                    return FindMotion(blendTree.children[index].motion);
                                                }
                                            }
                                            #endregion
                                            break;
                                        case BlendTreeType.SimpleDirectional2D:
                                        case BlendTreeType.FreeformDirectional2D:
                                        case BlendTreeType.FreeformCartesian2D:
                                            #region 2D
                                            {
                                                var paramX = vaw.animator.GetFloat(blendTree.blendParameter);
                                                var paramY = vaw.animator.GetFloat(blendTree.blendParameterY);
                                                float near = float.MaxValue;
                                                int index = -1;
                                                for (int i = 0; i < blendTree.children.Length; i++)
                                                {
                                                    var offsetX = Mathf.Abs(blendTree.children[i].position.x - paramX);
                                                    var offsetY = Mathf.Abs(blendTree.children[i].position.y - paramY);
                                                    if (offsetX + offsetY < near)
                                                    {
                                                        index = i;
                                                        near = offsetX + offsetY;
                                                    }
                                                }
                                                if (index >= 0)
                                                {
                                                    return FindMotion(blendTree.children[index].motion);
                                                }
                                            }
                                            #endregion
                                            break;
                                        case BlendTreeType.Direct:
                                            #region Direct
                                            {
                                                float max = float.MinValue;
                                                int index = -1;
                                                for (int i = 0; i < blendTree.children.Length; i++)
                                                {
                                                    var param = vaw.animator.GetFloat(blendTree.children[i].directBlendParameter);
                                                    if (param >= max)
                                                    {
                                                        index = i;
                                                        max = param;
                                                    }
                                                }
                                                if (index >= 0)
                                                {
                                                    return FindMotion(blendTree.children[index].motion);
                                                }
                                            }
                                            #endregion
                                            break;
                                        default:
                                            Assert.IsTrue(false, "not support type");
                                            break;
                                        }
                                        #endregion
                                    }
                                    else if (motion is AnimationClip)
                                    {
                                        return motion as AnimationClip;
                                    }
                                }
                                return null;
                            };
                            var clip = FindMotion(state.state.motion);
                            if (clip == null)
                                continue;
                            if (owc != null)
                                clip = owc[clip];
                            resultClip = clip;
                            if (resultClip.isLooping)
                            {
                                resultTime = currentAnimatorStateInfo.length * (currentAnimatorStateInfo.normalizedTime % 1f);
                            }
                            else
                            {
                                if (currentAnimatorStateInfo.normalizedTime > 1f)
                                    resultTime = currentAnimatorStateInfo.length;
                                else
                                    resultTime = currentAnimatorStateInfo.length * currentAnimatorStateInfo.normalizedTime;
                            }
                            resultLength = currentAnimatorStateInfo.length;
                            return true;
                        }
                        foreach (var cstateMachine in stateMachine.stateMachines)
                        {
                            if (FindStateMachine(cstateMachine.stateMachine))
                                return true;
                        }
                        return false;
                    };
                    if (ac != null && FindStateMachine(ac.layers[layerIndex].stateMachine))
                    {
                        dstClip = resultClip;
                        dstTime = resultTime;
                        dstLength = resultLength;
                        return true;
                    }
                }
                #endregion
            }
            else if (vaw.animation != null)
            {
                #region animation
                foreach (AnimationState state in vaw.animation)
                {
                    if (!state.enabled || state.length <= 0f) continue;
                    dstClip = state.clip;
                    var time = state.time;
                    dstTime = time;
                    dstLength = state.length;
                    switch (state.wrapMode)
                    {
                    case WrapMode.Loop:
                        {
                            var loop = Mathf.FloorToInt(time / state.length);
                            dstTime -= loop * state.length;
                        }
                        break;
                    case WrapMode.PingPong:
                        {
                            var loop = Mathf.FloorToInt(time / state.length);
                            dstTime -= loop * state.length;
                            if (loop % 2 != 0)
                                dstTime = state.length - dstTime;
                        }
                        break;
                    default:
                        dstTime = Mathf.Min(dstTime, state.length);
                        break;
                    }
                    return true;
                }
                #endregion
            }
            return false;
        }

        public float Muscle2EulerAngle(int muscleIndex, float muscleValue)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
                return 0f;

            var humanoidIndex = HumanTrait.BoneFromMuscle(muscleIndex);
            if (humanoidIndex < 0)
                return 0f;

            var dof = -1;
            for (int i = 0; i < 3; i++)
            {
                if (HumanTrait.MuscleFromBone(humanoidIndex, i) == muscleIndex)
                {
                    dof = i;
                    break;
                }
            }
            if (dof < 0)
                return 0f;

            if (muscleValue < 0f)
            {
                return Mathf.LerpUnclamped(0f, humanoidMuscleLimit[humanoidIndex].min[dof], Mathf.Abs(muscleValue));
            }
            else if (muscleValue > 0f)
            {
                return Mathf.LerpUnclamped(0f, humanoidMuscleLimit[humanoidIndex].max[dof], Mathf.Abs(muscleValue));
            }
            else
            {
                return 0f;
            }
        }
        public float EulerAngle2Muscle(int muscleIndex, float degree)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
                return 0f;

            var humanoidIndex = HumanTrait.BoneFromMuscle(muscleIndex);
            if (humanoidIndex < 0)
                return 0f;

            var dof = -1;
            for (int i = 0; i < 3; i++)
            {
                if (HumanTrait.MuscleFromBone(humanoidIndex, i) == muscleIndex)
                {
                    dof = i;
                    break;
                }
            }
            if (dof < 0)
                return 0f;

            if (degree < 0f)
            {
                return -(degree / humanoidMuscleLimit[humanoidIndex].min[dof]);
            }
            else if (degree > 0f)
            {
                return degree / humanoidMuscleLimit[humanoidIndex].max[dof];
            }
            else
            {
                return 0f;
            }
        }

        public int GetMirrorMuscleIndex(int muscleIndex)
        {
            if (muscleIndex < 0) return -1;
            var humanIndex = HumanTrait.BoneFromMuscle(muscleIndex);
            if (humanIndex < 0) return -1;
            if (HumanBodyMirrorBones[humanIndex] < 0) return -1;
            for (int i = 0; i < 3; i++)
            {
                if (muscleIndex == HumanTrait.MuscleFromBone(humanIndex, i))
                    return HumanTrait.MuscleFromBone((int)HumanBodyMirrorBones[humanIndex], i);
            }
            return -1;
        }
        public Vector3 GetMirrorBoneLocalPosition(int boneIndex, Vector3 localPosition)
        {
            var rootInv = Quaternion.Inverse(boneSaveTransforms[0].rotation);
            var local = localPosition - boneSaveTransforms[boneIndex].localPosition;
            var parentRot = (rootInv * boneSaveTransforms[boneIndex].rotation) * Quaternion.Inverse(boneSaveTransforms[boneIndex].localRotation);
            var world = parentRot * local;
            world.x = -world.x;
            if (mirrorBoneIndexes[boneIndex] >= 0)
            {
                var mparentRot = (rootInv * boneSaveTransforms[mirrorBoneIndexes[boneIndex]].rotation) * Quaternion.Inverse(boneSaveTransforms[mirrorBoneIndexes[boneIndex]].localRotation);
                var mlocal = Quaternion.Inverse(mparentRot) * world;
                return boneSaveTransforms[mirrorBoneIndexes[boneIndex]].localPosition + mlocal;
            }
            else
            {
                var mlocal = Quaternion.Inverse(parentRot) * world;
                return boneSaveTransforms[boneIndex].localPosition + mlocal;
            }
        }
        public Quaternion GetMirrorBoneLocalRotation(int boneIndex, Quaternion localRotation)
        {
            var rootInv = Quaternion.Inverse(boneSaveTransforms[0].rotation);
            var parentRot = (rootInv * boneSaveTransforms[boneIndex].rotation) * Quaternion.Inverse(boneSaveTransforms[boneIndex].localRotation);
            var wrot = parentRot * localRotation;
            if (mirrorBoneIndexes[boneIndex] >= 0 && mirrorBoneData[boneIndex].rootBoneIndex >= 0)
            {
                var rootRot = rootInv * boneSaveTransforms[mirrorBoneData[boneIndex].rootBoneIndex].rotation;
                wrot *= Quaternion.Inverse(Quaternion.Inverse(rootRot) * (rootInv * boneSaveTransforms[boneIndex].rotation));
                {
                    wrot *= Quaternion.Inverse(rootRot);
                    wrot = new Quaternion(wrot.x, -wrot.y, -wrot.z, wrot.w);
                    wrot *= rootRot;
                }
                wrot *= Quaternion.Inverse(rootRot) * (rootInv * boneSaveTransforms[mirrorBoneIndexes[boneIndex]].rotation);
                var mparentRot = (rootInv * boneSaveTransforms[mirrorBoneIndexes[boneIndex]].rotation) * Quaternion.Inverse(boneSaveTransforms[mirrorBoneIndexes[boneIndex]].localRotation);
                return Quaternion.Inverse(mparentRot) * wrot;
            }
            else
            {
                var rootRot = rootInv * boneSaveTransforms[boneIndex].rotation;
                wrot *= Quaternion.Inverse(rootRot);
                wrot = new Quaternion(wrot.x, -wrot.y, -wrot.z, wrot.w);
                wrot *= rootRot;
                return Quaternion.Inverse(parentRot) * wrot;
            }
        }
        public Vector3 GetMirrorBoneLocalScale(int boneIndex, Vector3 localScale)
        {
            if (mirrorBoneIndexes[boneIndex] >= 0)
            {
                var rootInv = Quaternion.Inverse(boneSaveTransforms[0].rotation);
                var local = new Vector3(boneSaveTransforms[boneIndex].localScale.x != 0f ? localScale.x / boneSaveTransforms[boneIndex].localScale.x : 0f,
                                        boneSaveTransforms[boneIndex].localScale.y != 0f ? localScale.y / boneSaveTransforms[boneIndex].localScale.y : 0f,
                                        boneSaveTransforms[boneIndex].localScale.z != 0f ? localScale.z / boneSaveTransforms[boneIndex].localScale.z : 0f);
                var parentRot = (rootInv * boneSaveTransforms[boneIndex].rotation);
                var world = parentRot * local;
                world.x = -world.x;
                var mparentRot = (rootInv * boneSaveTransforms[mirrorBoneIndexes[boneIndex]].rotation);
                var mlocal = Quaternion.Inverse(mparentRot) * world;
                {
                    var minus = new Vector3(local.x < 0f ? 1f : 0f, local.y < 0f ? 1f : 0f, local.z < 0f ? 1f : 0f);
                    minus = parentRot * minus;
                    minus = Quaternion.Inverse(mparentRot) * minus;
                    for (int i = 0; i < 3; i++)
                    {
                        mlocal[i] = Mathf.Abs(mlocal[i]);
                        if (Mathf.Abs(minus[i]) > 0.5f)
                            mlocal[i] *= -1f;
                    }
                }
                return Vector3.Scale(boneSaveTransforms[mirrorBoneIndexes[boneIndex]].localScale, mlocal);
            }
            else
            {
                return localScale;
            }
        }
        public string GetMirrorBlendShape(SkinnedMeshRenderer renderer, string name)
        {
            Dictionary<string, string> nameTable;
            if (mirrorBlendShape.TryGetValue(renderer, out nameTable))
            {
                string mirrorName;
                if (nameTable.TryGetValue(name, out mirrorName))
                {
                    return mirrorName;
                }
            }
            return null;
        }

        public int GetLastFrame()
        {
            return uAw.GetLastFrame(currentClip);
        }
        public float GetFrameTime(int frame)
        {
#if false
            return uAw.GetFrameTime(frame, currentClip);    //Slow
#else
            var fps = currentClip.frameRate;
            var time = frame * (1f / fps);
            return GetSnapToFrame(time);
#endif
        }
        private float GetFrameSnapTime(float time = -1f)
        {
            if (time < 0f)
                return GetSnapToFrame(currentTime);
            else
                return GetSnapToFrame(time);
        }
        private float GetSnapToFrame(float time)
        {
            var fps = currentClip.frameRate;
            return Mathf.Round(time * fps) / fps;
        }

        public int FindKeyframeAtTime(AnimationCurve curve, float time)
        {
            if (curve.length > 0)
            {
                int begin = 0, end = curve.length - 1;
                while (end - begin > 1)
                {
                    var index = begin + Mathf.FloorToInt((end - begin) / 2f);
                    if (time < curve[index].time)
                    {
                        if (end == index) break;
                        end = index;
                    }
                    else
                    {
                        if (begin == index) break;
                        begin = index;
                    }
                }
                if (Mathf.Abs(curve[begin].time - time) < 0.0001f)
                    return begin;
                if (Mathf.Abs(curve[end].time - time) < 0.0001f)
                    return end;
            }
            return -1;
        }
        public int FindKeyframeAtTime(Keyframe[] keys, float time)
        {
            if (keys.Length > 0)
            {
                int begin = 0, end = keys.Length - 1;
                while (end - begin > 1)
                {
                    var index = begin + Mathf.FloorToInt((end - begin) / 2f);
                    if (time < keys[index].time)
                    {
                        if (end == index) break;
                        end = index;
                    }
                    else
                    {
                        if (begin == index) break;
                        begin = index;
                    }
                }
                if (Mathf.Abs(keys[begin].time - time) < 0.0001f)
                    return begin;
                if (Mathf.Abs(keys[end].time - time) < 0.0001f)
                    return end;
            }
            return -1;
        }
        public int FindKeyframeAtTime(ObjectReferenceKeyframe[] keys, float time)
        {
            if (keys.Length > 0)
            {
                int begin = 0, end = keys.Length - 1;
                while (end - begin > 1)
                {
                    var index = begin + Mathf.FloorToInt((end - begin) / 2f);
                    if (time < keys[index].time)
                    {
                        if (end == index) break;
                        end = index;
                    }
                    else
                    {
                        if (begin == index) break;
                        begin = index;
                    }
                }
                if (Mathf.Abs(keys[begin].time - time) < 0.0001f)
                    return begin;
                if (Mathf.Abs(keys[end].time - time) < 0.0001f)
                    return end;
            }
            return -1;
        }
        public int FindKeyframeAtTime(List<ObjectReferenceKeyframe> keys, float time)
        {
            if (keys.Count > 0)
            {
                int begin = 0, end = keys.Count - 1;
                while (end - begin > 1)
                {
                    var index = begin + Mathf.FloorToInt((end - begin) / 2f);
                    if (time < keys[index].time)
                    {
                        if (end == index) break;
                        end = index;
                    }
                    else
                    {
                        if (begin == index) break;
                        begin = index;
                    }
                }
                if (Mathf.Abs(keys[begin].time - time) < 0.0001f)
                    return begin;
                if (Mathf.Abs(keys[end].time - time) < 0.0001f)
                    return end;
            }
            return -1;
        }
        public int FindKeyframeAtTime(AnimationEvent[] events, float time)
        {
            if (events.Length > 0)
            {
                int begin = 0, end = events.Length - 1;
                while (end - begin > 1)
                {
                    var index = begin + Mathf.FloorToInt((end - begin) / 2f);
                    if (time < events[index].time)
                    {
                        if (end == index) break;
                        end = index;
                    }
                    else
                    {
                        if (begin == index) break;
                        begin = index;
                    }
                }
                if (Mathf.Abs(events[begin].time - time) < 0.0001f)
                    return begin;
                if (Mathf.Abs(events[end].time - time) < 0.0001f)
                    return end;
            }
            return -1;
        }
        public int FindBeforeNearKeyframeAtTime(AnimationCurve curve, float time)
        {
            time = GetFrameSnapTime(time);

            if (curve.length == 0)
                return -1;
            else if (curve[curve.length - 1].time < time)
                return curve.length - 1;
            else if (curve[0].time >= time)
                return -1;

            var result = -1;
            {
                int begin = 0, end = curve.length - 1;
                while (end - begin > 1)
                {
                    var index = begin + Mathf.FloorToInt((end - begin) / 2f);
                    if (time < curve[index].time)
                    {
                        if (end == index) break;
                        end = index;
                    }
                    else
                    {
                        if (begin == index) break;
                        begin = index;
                    }
                }
                if (Mathf.Abs(curve[begin].time - time) < 0.0001f)
                    result = begin - 1;
                else
                    result = begin;
            }
            //Assert.IsTrue(curve[result].time < time && (result + 1 >= curve.length || curve[result + 1].time >= time));
            return result;
        }
        public int FindBeforeNearKeyframeAtTime(ObjectReferenceKeyframe[] keys, float time)
        {
            time = GetFrameSnapTime(time);

            if (keys.Length == 0)
                return -1;
            else if (keys[keys.Length - 1].time < time)
                return keys.Length - 1;
            else if (keys[0].time >= time)
                return -1;

            var result = -1;
            {
                int begin = 0, end = keys.Length - 1;
                while (end - begin > 1)
                {
                    var index = begin + Mathf.FloorToInt((end - begin) / 2f);
                    if (time < keys[index].time)
                    {
                        if (end == index) break;
                        end = index;
                    }
                    else
                    {
                        if (begin == index) break;
                        begin = index;
                    }
                }
                if (Mathf.Abs(keys[begin].time - time) < 0.0001f)
                    result = begin - 1;
                else
                    result = begin;
            }
            //Assert.IsTrue(keys[result].time < time && (result + 1 >= keys.Length || keys[result + 1].time >= time));
            return result;
        }
        public int FindBeforeNearKeyframeAtTime(AnimationEvent[] events, float time)
        {
            time = GetFrameSnapTime(time);

            if (events.Length == 0)
                return -1;
            else if (events[events.Length - 1].time < time)
                return events.Length - 1;
            else if (events[0].time >= time)
                return -1;

            var result = -1;
            {
                int begin = 0, end = events.Length - 1;
                while (end - begin > 1)
                {
                    var index = begin + Mathf.FloorToInt((end - begin) / 2f);
                    if (time < events[index].time)
                    {
                        if (end == index) break;
                        end = index;
                    }
                    else
                    {
                        if (begin == index) break;
                        begin = index;
                    }
                }
                if (Mathf.Abs(events[begin].time - time) < 0.0001f)
                    result = begin - 1;
                else
                    result = begin;
            }
            //Assert.IsTrue(ev[result].time < time && (result + 1 >= ev.Length || ev[result + 1].time >= time));
            return result;
        }
        public int FindAfterNearKeyframeAtTime(AnimationCurve curve, float time)
        {
            time = GetFrameSnapTime(time);

            if (curve.length == 0)
                return -1;
            else if (curve[0].time > time)
                return 0;
            else if (curve[curve.length - 1].time <= time)
                return -1;

            var result = -1;
            {
                int begin = 0, end = curve.length - 1;
                while (end - begin > 1)
                {
                    var index = begin + Mathf.CeilToInt((end - begin) / 2f);
                    if (time > curve[index].time)
                    {
                        if (begin == index) break;
                        begin = index;
                    }
                    else
                    {
                        if (end == index) break;
                        end = index;
                    }
                }
                if (Mathf.Abs(curve[end].time - time) < 0.0001f)
                    result = end + 1;
                else
                    result = end;
            }
            //Assert.IsTrue(curve[result].time > time && (result + 1 >= curve.length || curve[result - 1].time <= time));
            return result;
        }
        public int FindAfterNearKeyframeAtTime(ObjectReferenceKeyframe[] keys, float time)
        {
            time = GetFrameSnapTime(time);

            if (keys.Length == 0)
                return -1;
            else if (keys[0].time > time)
                return 0;
            else if (keys[keys.Length - 1].time <= time)
                return -1;

            var result = -1;
            {
                int begin = 0, end = keys.Length - 1;
                while (end - begin > 1)
                {
                    var index = begin + Mathf.CeilToInt((end - begin) / 2f);
                    if (time > keys[index].time)
                    {
                        if (begin == index) break;
                        begin = index;
                    }
                    else
                    {
                        if (end == index) break;
                        end = index;
                    }
                }
                if (Mathf.Abs(keys[end].time - time) < 0.0001f)
                    result = end + 1;
                else
                    result = end;
            }
            //Assert.IsTrue(keys[result].time > time && (result + 1 >= keys.Length || keys[result - 1].time <= time));
            return result;
        }
        public int FindAfterNearKeyframeAtTime(AnimationEvent[] events, float time)
        {
            time = GetFrameSnapTime(time);

            if (events.Length == 0)
                return -1;
            else if (events[0].time > time)
                return 0;
            else if (events[events.Length - 1].time <= time)
                return -1;

            var result = -1;
            {
                int begin = 0, end = events.Length - 1;
                while (end - begin > 1)
                {
                    var index = begin + Mathf.CeilToInt((end - begin) / 2f);
                    if (time > events[index].time)
                    {
                        if (begin == index) break;
                        begin = index;
                    }
                    else
                    {
                        if (end == index) break;
                        end = index;
                    }
                }
                if (Mathf.Abs(events[end].time - time) < 0.0001f)
                    result = end + 1;
                else
                    result = end;
            }
            //Assert.IsTrue(events[result].time > time && (result + 1 >= events.Length || events[result - 1].time <= time));
            return result;
        }
        public int FindKeyframeIndex(AnimationCurve curve, AnimationCurve findCurve, int findIndex)
        {
            var index = FindKeyframeAtTime(curve, findCurve[findIndex].time);
            if (index >= 0)
            {
                //if(curve[index].Equals(key))  GC Alloc...
                if (curve[index].time == findCurve[findIndex].time &&
                    curve[index].value == findCurve[findIndex].value &&
                    curve[index].inTangent == findCurve[findIndex].inTangent &&
                    curve[index].outTangent == findCurve[findIndex].outTangent &&
#if !UNITY_2018_1_OR_NEWER
                    curve[index].tangentMode == findCurve[findIndex].tangentMode)
#else
                    curve[index].inWeight == findCurve[findIndex].inWeight &&
                    curve[index].outWeight == findCurve[findIndex].outWeight &&
                    curve[index].weightedMode == findCurve[findIndex].weightedMode &&
                    AnimationUtility.GetKeyLeftTangentMode(curve, index) == AnimationUtility.GetKeyLeftTangentMode(findCurve, findIndex) &&
                    AnimationUtility.GetKeyRightTangentMode(curve, index) == AnimationUtility.GetKeyRightTangentMode(findCurve, findIndex))
#endif
                {
                    return index;
                }
            }
            return -1;
        }

        public int AddKeyframe(AnimationCurve curve, float time, float value, bool updateTangents = true)
        {
            var keyIndex = curve.AddKey(new Keyframe(time, value));
            if (keyIndex < 0) return -1;
            uCurveUtility.SetKeyModeFromContext(curve, keyIndex);
            if (updateTangents)
                uAnimationUtility.UpdateTangentsFromModeSurrounding(curve, keyIndex);
            return keyIndex;
        }
        public int AddKeyframe(AnimationCurve curve, Keyframe keyframe, bool updateTangents = true)
        {
            var keyIndex = curve.AddKey(keyframe);
            if (keyIndex < 0) return -1;
            uCurveUtility.SetKeyModeFromContext(curve, keyIndex);
            if (updateTangents)
                uAnimationUtility.UpdateTangentsFromModeSurrounding(curve, keyIndex);
            return keyIndex;
        }
        public int SetKeyframe(AnimationCurve curve, float time, float value, bool updateTangents = true)
        {
            var keyIndex = FindKeyframeAtTime(curve, time);
            if (keyIndex < 0)
            {
                keyIndex = AddKeyframe(curve, time, value, updateTangents);
                SetAnimationWindowRefresh(AnimationWindowStateRefreshType.CurvesOnly);
            }
            else
            {
                var keyframe = curve[keyIndex];
                keyframe.value = value;
                curve.MoveKey(keyIndex, keyframe);
            }
            return keyIndex;
        }
        public int SetKeyframe(AnimationCurve curve, Keyframe keyframe, bool updateTangents = true)
        {
            var keyIndex = FindKeyframeAtTime(curve, keyframe.time);
            if (keyIndex < 0)
            {
                keyIndex = AddKeyframe(curve, keyframe, updateTangents);
                SetAnimationWindowRefresh(AnimationWindowStateRefreshType.CurvesOnly);
            }
            else
            {
                curve.MoveKey(keyIndex, keyframe);
            }
            return keyIndex;
        }

        public void SetKeyframeTangentModeLinear(AnimationCurve curve, int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= curve.length) return;
            AnimationUtility.SetKeyRightTangentMode(curve, keyIndex, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyLeftTangentMode(curve, keyIndex, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyBroken(curve, keyIndex, false);
        }
        public void SetKeyframeTangentModeClampedAuto(AnimationCurve curve, int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= curve.length) return;
            AnimationUtility.SetKeyLeftTangentMode(curve, keyIndex, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, keyIndex, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyBroken(curve, keyIndex, false);
        }

        public Quaternion FixReverseRotationQuaternion(EditorCurveBinding binding, float time, Quaternion rotation)
        {
            Assert.IsNull(tmpCurves.curves[0]);
            LoadTmpCurvesFullDof(binding, 4);
            rotation = FixReverseRotationQuaternion(tmpCurves.curves, time, rotation);
            tmpCurves.Clear();
            return rotation;
        }
        public Quaternion FixReverseRotationQuaternion(AnimationCurve[] curves, float time, Quaternion rotation)
        {
            Quaternion beforeRotation;
            {
                var beforeTime = 0f;
                for (int i = 0; i < 4; i++)
                {
                    if (curves[i] == null) continue;
                    var index = FindBeforeNearKeyframeAtTime(curves[i], time);
                    if (index >= 0)
                        beforeTime = Mathf.Max(beforeTime, curves[i][index].time);
                }
                {
                    Vector4 result = Vector4.zero;
                    for (int i = 0; i < 4; i++)
                    {
                        if (curves[i] == null) continue;
                        result[i] = curves[i].Evaluate(beforeTime);
                    }
                    beforeRotation = Quaternion.identity;
                    result.Normalize();
                    if (result.sqrMagnitude > 0f)
                    {
                        for (int i = 0; i < 4; i++)
                            beforeRotation[i] = result[i];
                    }
                }
            }
            return FixReverseRotationQuaternion(rotation, beforeRotation);
        }
        public Quaternion FixReverseRotationQuaternion(Quaternion rotation, Quaternion beforeRotation)
        {
            var rot = rotation * Quaternion.Inverse(beforeRotation);
            if (rot.w < 0f)
            {
                for (int i = 0; i < 4; i++)
                    rotation[i] = -rotation[i];
            }
            return rotation;
        }
        public Vector3 FixReverseRotationEuler(EditorCurveBinding binding, float time, Vector3 eulerAngles)
        {
            Assert.IsNull(tmpCurves.curves[0]);
            LoadTmpCurvesFullDof(binding, 3);
            eulerAngles = FixReverseRotationEuler(tmpCurves.curves, time, eulerAngles);
            tmpCurves.Clear();
            return eulerAngles;
        }
        public Vector3 FixReverseRotationEuler(AnimationCurve[] curves, float time, Vector3 eulerAngles)
        {
            Vector3 beforeEulerAngles = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                if (curves[i] != null)
                {
                    var beforeTime = 0f;
                    {
                        var index = FindBeforeNearKeyframeAtTime(curves[i], time);
                        if (index >= 0)
                            beforeTime = Mathf.Max(beforeTime, curves[i][index].time);
                    }
                    beforeEulerAngles[i] = curves[i].Evaluate(beforeTime);
                }
            }
            return FixReverseRotationEuler(eulerAngles, beforeEulerAngles);
        }
        public Vector3 FixReverseRotationEuler(Vector3 eulerAngles, Vector3 beforeEulerAngles)
        {
            for (int i = 0; i < 3; i++)
            {
                while (Mathf.Abs(eulerAngles[i] - beforeEulerAngles[i]) > 180f)
                {
                    if (beforeEulerAngles[i] < eulerAngles[i])
                        eulerAngles[i] -= 360f;
                    else
                        eulerAngles[i] += 360f;
                }
            }
            return eulerAngles;
        }
        public bool FixReverseRotationEuler(AnimationCurve curve)
        {
            bool updated = false;
            for (int i = 1; i < curve.length; i++)
            {
                var keyframe = curve[i];
                if (Mathf.Abs(keyframe.value - curve[i - 1].value) <= 180f)
                    continue;
                while (Mathf.Abs(keyframe.value - curve[i - 1].value) > 180f)
                {
                    if (keyframe.value < curve[i - 1].value)
                        keyframe.value += 360f;
                    else
                        keyframe.value -= 360f;
                }
                curve.MoveKey(i, keyframe);
                updated = true;
            }
            return updated;
        }

        private void ActionAllAnimatorState(AnimationClip clip, Action<UnityEditor.Animations.AnimatorState> action)
        {
            var ac = EditorCommon.GetAnimatorController(vaw.animator);
            if (ac == null) return;

            foreach (UnityEditor.Animations.AnimatorControllerLayer layer in ac.layers)
            {
                Action<AnimatorStateMachine> ActionStateMachine = null;
                ActionStateMachine = (stateMachine) =>
                {
                    foreach (var state in stateMachine.states)
                    {
                        if (state.state.motion is UnityEditor.Animations.BlendTree)
                        {
                            Action<UnityEditor.Animations.BlendTree> ActionBlendTree = null;
                            ActionBlendTree = (blendTree) =>
                            {
                                if (blendTree.children == null) return;
                                var children = blendTree.children;
                                for (int j = 0; j < children.Length; j++)
                                {
                                    if (children[j].motion is UnityEditor.Animations.BlendTree)
                                    {
                                        ActionBlendTree(children[j].motion as UnityEditor.Animations.BlendTree);
                                    }
                                    else
                                    {
                                        if (children[j].motion == clip)
                                        {
                                            action(state.state);
                                        }
                                    }
                                }
                            };
                            ActionBlendTree(state.state.motion as UnityEditor.Animations.BlendTree);
                        }
                        else
                        {
                            if (state.state.motion == clip)
                            {
                                action(state.state);
                            }
                        }
                    }
                    foreach (var childStateMachine in stateMachine.stateMachines)
                    {
                        ActionStateMachine(childStateMachine.stateMachine);
                    }
                };
                ActionStateMachine(layer.stateMachine);
            }
        }

        private Transform GetTransformFromPath(string path)
        {
            var root = editGameObject.transform;
            if (!string.IsNullOrEmpty(path))
            {
                var splits = path.Split('/');
                for (int i = 0; i < splits.Length; i++)
                {
                    bool contains = false;
                    for (int j = 0; j < root.childCount; j++)
                    {
                        if (root.GetChild(j).name == splits[i])
                        {
                            root = root.GetChild(j);
                            contains = true;
                            break;
                        }
                    }
                    if (!contains) return null;
                }
            }
            return root;
        }
        #endregion

        private class OnCurveWasModifiedData
        {
            public EditorCurveBinding binding;
            public AnimationUtility.CurveModifiedType deleted;
            public AnimationCurve beforeCurve;
        }
        private Dictionary<int, OnCurveWasModifiedData> curvesWasModified = new Dictionary<int, OnCurveWasModifiedData>();
        private Dictionary<int, OnCurveWasModifiedData> curvesWasModifiedStopped = new Dictionary<int, OnCurveWasModifiedData>();
        private bool OnCurveWasModifiedStop = false;
        private void OnCurveWasModified(AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType deleted)
        {
            if (isEditError) return;

            if (currentClip != clip || !IsCheckChangeClipClearEditorCurveCache(clip))
                return;

            if (deleted == AnimationUtility.CurveModifiedType.ClipModified)
            {
                ClearEditorCurveCache();
                return;
            }

            AnimationCurve beforeCurve = null;
            if (IsContainsEditorCurveCache(binding))
            {
                beforeCurve = GetEditorCurveCache(binding);
            }
            if (deleted == AnimationUtility.CurveModifiedType.CurveModified ||
                deleted == AnimationUtility.CurveModifiedType.CurveDeleted)
            {
                if (editorCurveCacheDic != null)
                {
                    if (editorCurveCacheDic.ContainsKey(GetEditorCurveBindingHashCode(binding)))
                    {
                        RemoveEditorCurveCache(binding);
                    }
                }
                if (binding.type == typeof(Transform) &&
                    binding.propertyName.StartsWith(URotationCurveInterpolation.PrefixForInterpolation[(int)URotationCurveInterpolation.Mode.NonBaked]))
                {
                    var bindingSub = binding;
                    foreach (var propertyName in EditorCurveBindingTransformRotationPropertyNames[(int)URotationCurveInterpolation.Mode.RawQuaternions])
                    {
                        bindingSub.propertyName = propertyName;
                        OnCurveWasModified(clip, bindingSub, deleted);
                    }
                    return;
                }
            }

            #region Ignore undo
            {
                var stackTrace = new System.Diagnostics.StackTrace();
                if (stackTrace.FrameCount >= 2 && stackTrace.GetFrame(1).GetMethod().Name == "Internal_CallAnimationClipAwake")
                    return;
            }
            #endregion

            AddOnCurveWasModified(binding, deleted, beforeCurve);
        }
        private void AddOnCurveWasModified(EditorCurveBinding binding, AnimationUtility.CurveModifiedType deleted, AnimationCurve beforeCurve)
        {
            var hash = GetEditorCurveBindingHashCode(binding);
            Dictionary<int, OnCurveWasModifiedData> dic = !OnCurveWasModifiedStop ? curvesWasModified : curvesWasModifiedStopped;
            OnCurveWasModifiedData data;
            if (dic.TryGetValue(hash, out data))
            {
                if (data.deleted == AnimationUtility.CurveModifiedType.CurveModified &&
                    data.deleted != deleted)
                {
                    data.deleted = deleted;
                }
                if (data.beforeCurve == null && beforeCurve != null)
                {
                    data.beforeCurve = beforeCurve;
                }
            }
            else
            {
                dic.Add(hash, new OnCurveWasModifiedData() { binding = binding, deleted = deleted, beforeCurve = beforeCurve });
            }
        }
        private void SetOnCurveWasModifiedStop(bool flag)
        {
            OnCurveWasModifiedStop = flag;
            if (!flag)
            {
                foreach (var pair in curvesWasModifiedStopped)
                {
                    AddOnCurveWasModified(pair.Value.binding, pair.Value.deleted, pair.Value.beforeCurve);
                }
            }
            curvesWasModifiedStopped.Clear();
        }
        private void ResetOnCurveWasModifiedStop()
        {
            OnCurveWasModifiedStop = false;
            curvesWasModifiedStopped.Clear();
        }
        private void ActionCurrentChangedKeyframes(OnCurveWasModifiedData data, Action<AnimationCurve, int> action)
        {
            var curve = GetEditorCurveCache(data.binding);
            if (curve != null)
            {
                if (data.beforeCurve != null)
                {
                    for (int i = 0; i < curve.length; i++)
                    {
                        if (FindKeyframeIndex(data.beforeCurve, curve, i) < 0)
                            action(curve, i);
                    }
                }
                else
                {
                    //Debug.LogWarningFormat("<color=blue>[Very Animation]</color>Lost before cache '{0}'", data.binding.path, data.binding.propertyName);
                }
            }
        }
        private void ActionBeforeChangedKeyframes(OnCurveWasModifiedData data, Action<AnimationCurve, int> action)
        {
            var curve = GetEditorCurveCache(data.binding);
            if (curve != null)
            {
                if (data.beforeCurve != null)
                {
                    for (int i = 0; i < data.beforeCurve.length; i++)
                    {
                        if (FindKeyframeAtTime(curve, data.beforeCurve[i].time) < 0)
                            action(data.beforeCurve, i);
                    }
                }
                else
                {
                    //Debug.LogWarningFormat("<color=blue>[Very Animation]</color>Lost before cache '{0}'", data.binding.path, data.binding.propertyName);
                }
            }
        }
        private void ActionAllInfluencedTime(OnCurveWasModifiedData data, Action<float, float> action)
        {
            var curve = GetEditorCurveCache(data.binding);
            if (curve != null)
            {
                if (data.beforeCurve != null)
                {
                    for (int i = 0; i < curve.length; i++)
                    {
                        if (FindKeyframeIndex(data.beforeCurve, curve, i) < 0)
                        {
                            var beforeTime = i > 0 ? curve[i - 1].time : 0f;
                            var afterTime = i + 1 < curve.length ? curve[i + 1].time : currentClip.length;
                            action(beforeTime, afterTime);
                        }
                    }
                    for (int i = 0; i < data.beforeCurve.length; i++)
                    {
                        if (FindKeyframeAtTime(curve, data.beforeCurve[i].time) < 0)
                        {
                            var beforeTime = i > 0 ? data.beforeCurve[i - 1].time : 0f;
                            var afterTime = i + 1 < data.beforeCurve.length ? data.beforeCurve[i + 1].time : currentClip.length;
                            action(beforeTime, afterTime);
                        }
                    }
                }
                else
                {
                    //Debug.LogWarningFormat("<color=blue>[Very Animation]</color>Lost before cache '{0} - {1}'", data.binding.path, data.binding.propertyName);
                }
            }
        }

        #region EditorCurveBinding
        private void CreateEditorCurveBindingPropertyNames()
        {
            {
                EditorCurveBindingAnimatorIkTPropertyNames = new string[(int)AnimatorIKIndex.Total][];
                EditorCurveBindingAnimatorIkQPropertyNames = new string[(int)AnimatorIKIndex.Total][];
                for (int i = 0; i < (int)AnimatorIKIndex.Total; i++)
                {
                    EditorCurveBindingAnimatorIkTPropertyNames[i] = new string[3];
                    for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                        EditorCurveBindingAnimatorIkTPropertyNames[i][dofIndex] = string.Format("{0}T{1}", (AnimatorIKIndex)i, DofIndex2String[dofIndex]);
                    EditorCurveBindingAnimatorIkQPropertyNames[i] = new string[4];
                    for (int dofIndex = 0; dofIndex < 4; dofIndex++)
                        EditorCurveBindingAnimatorIkQPropertyNames[i][dofIndex] = string.Format("{0}Q{1}", (AnimatorIKIndex)i, DofIndex2String[dofIndex]);
                }
            }
            {
                EditorCurveBindingAnimatorTDOFPropertyNames = new string[(int)AnimatorTDOFIndex.Total][];
                for (int i = 0; i < (int)AnimatorTDOFIndex.Total; i++)
                {
                    EditorCurveBindingAnimatorTDOFPropertyNames[i] = new string[3];
                    for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                        EditorCurveBindingAnimatorTDOFPropertyNames[i][dofIndex] = string.Format("{0}TDOF{1}", AnimatorTDOFIndex2HumanBodyBones[i], DofIndex2String[dofIndex]);
                }
            }
            {
                EditorCurveBindingTransformRotationPropertyNames = new string[(int)URotationCurveInterpolation.Mode.Total][];
                for (int i = 0; i < (int)URotationCurveInterpolation.Mode.Total; i++)
                {
                    int dofCount;
                    switch ((URotationCurveInterpolation.Mode)i)
                    {
                    case URotationCurveInterpolation.Mode.RawQuaternions: dofCount = 4; break;
                    default: dofCount = 3; break;
                    }
                    EditorCurveBindingTransformRotationPropertyNames[i] = new string[dofCount];
                    for (int dofIndex = 0; dofIndex < dofCount; dofIndex++)
                    {
                        if (URotationCurveInterpolation.PrefixForInterpolation[i] == null) continue;
                        EditorCurveBindingTransformRotationPropertyNames[i][dofIndex] = URotationCurveInterpolation.PrefixForInterpolation[i] + DofIndex2String[dofIndex].Remove(0, 1);
                    }
                }
            }
        }
        public readonly EditorCurveBinding[] AnimationCurveBindingAnimatorRootT =
        {
            EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.x"),
            EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y"),
            EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.z"),
        };
        public readonly EditorCurveBinding[] AnimationCurveBindingAnimatorRootQ =
        {
            EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.x"),
            EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.y"),
            EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.z"),
            EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.w"),
        };
        public readonly EditorCurveBinding[] AnimationCurveBindingAnimatorMotionT =
        {
            EditorCurveBinding.FloatCurve("", typeof(Animator), "MotionT.x"),
            EditorCurveBinding.FloatCurve("", typeof(Animator), "MotionT.y"),
            EditorCurveBinding.FloatCurve("", typeof(Animator), "MotionT.z"),
        };
        public readonly EditorCurveBinding[] AnimationCurveBindingAnimatorMotionQ =
        {
            EditorCurveBinding.FloatCurve("", typeof(Animator), "MotionQ.x"),
            EditorCurveBinding.FloatCurve("", typeof(Animator), "MotionQ.y"),
            EditorCurveBinding.FloatCurve("", typeof(Animator), "MotionQ.z"),
            EditorCurveBinding.FloatCurve("", typeof(Animator), "MotionQ.w"),
        };
        private string[][] EditorCurveBindingAnimatorIkTPropertyNames;
        public EditorCurveBinding AnimationCurveBindingAnimatorIkT(AnimatorIKIndex ikIndex, int dofIndex)
        {
            return EditorCurveBinding.FloatCurve("", typeof(Animator), EditorCurveBindingAnimatorIkTPropertyNames[(int)ikIndex][dofIndex]);
        }
        private string[][] EditorCurveBindingAnimatorIkQPropertyNames;
        public EditorCurveBinding AnimationCurveBindingAnimatorIkQ(AnimatorIKIndex ikIndex, int dofIndex)
        {
            return EditorCurveBinding.FloatCurve("", typeof(Animator), EditorCurveBindingAnimatorIkQPropertyNames[(int)ikIndex][dofIndex]);
        }
        private string[][] EditorCurveBindingAnimatorTDOFPropertyNames;
        public EditorCurveBinding AnimationCurveBindingAnimatorTDOF(AnimatorTDOFIndex tdofIndex, int dofIndex)
        {
            return EditorCurveBinding.FloatCurve("", typeof(Animator), EditorCurveBindingAnimatorTDOFPropertyNames[(int)tdofIndex][dofIndex]);
        }
        public EditorCurveBinding AnimationCurveBindingAnimatorMuscle(int muscleIndex)
        {
            return EditorCurveBinding.FloatCurve("", typeof(Animator), musclePropertyName.PropertyNames[muscleIndex]);
        }
        public EditorCurveBinding AnimationCurveBindingAnimatorCustom(string propertyName)
        {
            return EditorCurveBinding.FloatCurve("", typeof(Animator), propertyName);
        }
        private static readonly string[] EditorCurveBindingTransformPositionPropertyNames =
        {
            "m_LocalPosition.x",
            "m_LocalPosition.y",
            "m_LocalPosition.z",
        };
        public EditorCurveBinding AnimationCurveBindingTransformPosition(int boneIndex, int dofIndex)
        {
            return EditorCurveBinding.FloatCurve(bonePaths[boneIndex], typeof(Transform), EditorCurveBindingTransformPositionPropertyNames[dofIndex]);
        }
        private string[][] EditorCurveBindingTransformRotationPropertyNames;
        public EditorCurveBinding AnimationCurveBindingTransformRotation(int boneIndex, int dofIndex, URotationCurveInterpolation.Mode mode)
        {
            return EditorCurveBinding.FloatCurve(bonePaths[boneIndex], typeof(Transform), EditorCurveBindingTransformRotationPropertyNames[(int)mode][dofIndex]);
        }
        private static readonly string[] EditorCurveBindingTransformScalePropertyNames =
        {
            "m_LocalScale.x",
            "m_LocalScale.y",
            "m_LocalScale.z",
        };
        public EditorCurveBinding AnimationCurveBindingTransformScale(int boneIndex, int dofIndex)
        {
            return EditorCurveBinding.FloatCurve(bonePaths[boneIndex], typeof(Transform), EditorCurveBindingTransformScalePropertyNames[dofIndex]);
        }
        public EditorCurveBinding AnimationCurveBindingBlendShape(SkinnedMeshRenderer renderer, string name)
        {
            return EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(renderer.transform, vaw.gameObject.transform), typeof(SkinnedMeshRenderer), string.Format("blendShape.{0}", name));
        }
        public EditorCurveBinding AnimationCurveBindingCustomProperty(int boneIndex, Type type, string propertyName)
        {
            return EditorCurveBinding.FloatCurve(bonePaths[boneIndex], type, propertyName);
        }

        public int GetBoneIndexFromCurveBinding(EditorCurveBinding binding)
        {
            if (binding.type == typeof(Animator))
            {
                AnimatorTDOFIndex tdofIndex;
                int muscleIndex;
                if (IsAnimatorRootCurveBinding(binding))
                {
                    return 0;
                }
                else if ((tdofIndex = GetTDOFIndexFromCurveBinding(binding)) >= 0)
                {
                    var humanoidIndex = (int)AnimatorTDOFIndex2HumanBodyBones[(int)tdofIndex];
                    if (humanoidIndex >= 0)
                        return humanoidIndex2boneIndex[humanoidIndex];
                }
                else if ((muscleIndex = GetMuscleIndexFromCurveBinding(binding)) >= 0)
                {
                    var humanoidIndex = HumanTrait.BoneFromMuscle(muscleIndex);
                    if (humanoidIndex >= 0)
                        return humanoidIndex2boneIndex[humanoidIndex];
                }
                return -1;
            }
            else
            {
                return GetBoneIndexFromPath(binding.path);
            }
        }
        public int GetBoneIndexFromPath(string path)
        {
            int boneIndex;
            if (bonePathDic.TryGetValue(path, out boneIndex))
            {
                return boneIndex;
            }
            return -1;
        }
        public int GetRootTDofIndexFromCurveBinding(EditorCurveBinding binding)
        {
            if (binding.type != typeof(Animator)) return -1;
            for (int dofIndex = 0; dofIndex < 3; dofIndex++)
            {
                if (binding == AnimationCurveBindingAnimatorRootT[dofIndex])
                    return dofIndex;
            }
            return -1;
        }
        public int GetRootQDofIndexFromCurveBinding(EditorCurveBinding binding)
        {
            if (binding.type != typeof(Animator)) return -1;
            for (int dofIndex = 0; dofIndex < 4; dofIndex++)
            {
                if (binding == AnimationCurveBindingAnimatorRootQ[dofIndex])
                    return dofIndex;
            }
            return -1;
        }
        public int GetMuscleIndexFromCurveBinding(EditorCurveBinding binding)
        {
            if (binding.type != typeof(Animator)) return -1;
            return GetMuscleIndexFromPropertyName(binding.propertyName);
        }
        public int GetMuscleIndexFromPropertyName(string propertyName)
        {
            int muscleIndex;
            if (musclePropertyName.PropertyNameDic.TryGetValue(propertyName, out muscleIndex))
            {
                return muscleIndex;
            }
            return -1;
        }
        public AnimatorIKIndex GetIkTIndexFromCurveBinding(EditorCurveBinding binding)
        {
            if (binding.type != typeof(Animator)) return AnimatorIKIndex.None;
            for (var ikIndex = 0; ikIndex < AnimatorIKTIndexStrings.Length; ikIndex++)
            {
                if (binding.propertyName.StartsWith(AnimatorIKTIndexStrings[ikIndex]))
                    return (AnimatorIKIndex)ikIndex;
            }
            return AnimatorIKIndex.None;
        }
        public AnimatorIKIndex GetIkQIndexFromCurveBinding(EditorCurveBinding binding)
        {
            if (binding.type != typeof(Animator)) return AnimatorIKIndex.None;
            for (var ikIndex = 0; ikIndex < AnimatorIKQIndexStrings.Length; ikIndex++)
            {
                if (binding.propertyName.StartsWith(AnimatorIKQIndexStrings[ikIndex]))
                    return (AnimatorIKIndex)ikIndex;
            }
            return AnimatorIKIndex.None;
        }
        public AnimatorTDOFIndex GetTDOFIndexFromCurveBinding(EditorCurveBinding binding)
        {
            if (binding.type != typeof(Animator)) return AnimatorTDOFIndex.None;
            var indexOf = binding.propertyName.IndexOf("TDOF.");
            if (indexOf < 0) return AnimatorTDOFIndex.None;
            var name = binding.propertyName.Remove(indexOf);
            for (int tdofIndex = 0; tdofIndex < (int)AnimatorTDOFIndex.Total; tdofIndex++)
            {
                if (name == AnimatorTDOFIndexStrings[tdofIndex])
                    return (AnimatorTDOFIndex)tdofIndex;
            }
            return AnimatorTDOFIndex.None;
        }
        public int GetDOFIndexFromCurveBinding(EditorCurveBinding binding)
        {
            for (int i = 0; i < DofIndex2String.Length; i++)
            {
                if (binding.propertyName.EndsWith(DofIndex2String[i]))
                    return i;
            }
            return -1;
        }
        public EditorCurveBinding GetDOFIndexChangeCurveBinding(EditorCurveBinding binding, int dofIndex)
        {
            binding.propertyName = binding.propertyName.Remove(binding.propertyName.Length - DofIndex2String[dofIndex].Length);
            binding.propertyName += DofIndex2String[dofIndex];
            return binding;
        }
        public EditorCurveBinding? GetMirrorAnimationCurveBinding(EditorCurveBinding binding)
        {
            if (binding.type == typeof(Animator))
            {
                int muscleIndex;
                AnimatorIKIndex ikTIndex, ikQIndex;
                AnimatorTDOFIndex tdofIndex;
                if (IsAnimatorRootCurveBinding(binding))
                {
                    return null;
                }
                else if ((muscleIndex = GetMuscleIndexFromCurveBinding(binding)) >= 0)
                {
                    var mmuscleIndex = GetMirrorMuscleIndex(muscleIndex);
                    if (mmuscleIndex < 0) return null;
                    return AnimationCurveBindingAnimatorMuscle(mmuscleIndex);
                }
                else if ((ikTIndex = GetIkTIndexFromCurveBinding(binding)) != AnimatorIKIndex.None)
                {
                    var mikTIndex = AnimatorIKMirrorIndexes[(int)ikTIndex];
                    if (mikTIndex < 0) return null;
                    var dofIndex = GetDOFIndexFromCurveBinding(binding);
                    if (dofIndex < 0) return null;
                    return AnimationCurveBindingAnimatorIkT(mikTIndex, dofIndex);
                }
                else if ((ikQIndex = GetIkQIndexFromCurveBinding(binding)) != AnimatorIKIndex.None)
                {
                    var mikQIndex = AnimatorIKMirrorIndexes[(int)ikQIndex];
                    if (mikQIndex < 0) return null;
                    var dofIndex = GetDOFIndexFromCurveBinding(binding);
                    if (dofIndex < 0) return null;
                    return AnimationCurveBindingAnimatorIkQ(mikQIndex, dofIndex);
                }
                else if ((tdofIndex = GetTDOFIndexFromCurveBinding(binding)) != AnimatorTDOFIndex.None)
                {
                    var mtdofIndex = AnimatorTDOFMirrorIndexes[(int)tdofIndex];
                    if (mtdofIndex < 0) return null;
                    var dofIndex = GetDOFIndexFromCurveBinding(binding);
                    if (dofIndex < 0) return null;
                    return AnimationCurveBindingAnimatorTDOF(mtdofIndex, dofIndex);
                }
                else
                {
                    return null;
                }
            }
            else if (binding.type == typeof(Transform))
            {
                var boneIndex = GetBoneIndexFromCurveBinding(binding);
                if (boneIndex < 0) return null;
                if (mirrorBoneIndexes[boneIndex] < 0) return null;
                binding.path = bonePaths[mirrorBoneIndexes[boneIndex]];
                if (!vaw.editorSettings.settingGenericMirrorScale)
                {
                    if (IsTransformScaleCurveBinding(binding))
                        return null;
                }
                return binding;
            }
            else if (IsSkinnedMeshRendererBlendShapeCurveBinding(binding))
            {
                var boneIndex = GetBoneIndexFromCurveBinding(binding);
                if (boneIndex < 0) return null;
                var renderer = bones[boneIndex].GetComponent<SkinnedMeshRenderer>();
                if (renderer == null) return null;
                Dictionary<string, string> nameTable;
                if (!mirrorBlendShape.TryGetValue(renderer, out nameTable)) return null;
                string mirrorName;
                if (!nameTable.TryGetValue(PropertyName2BlendShapeName(binding.propertyName), out mirrorName)) return null;
                binding.propertyName = BlendShapeName2PropertyName(mirrorName);
                return binding;
            }
            else
            {
                return null;
            }
        }

        public bool IsAnimatorRootCurveBinding(EditorCurveBinding binding)
        {
            return (GetRootTDofIndexFromCurveBinding(binding) >= 0 ||
                    GetRootQDofIndexFromCurveBinding(binding) >= 0);
        }
        public bool IsAnimatorReservedPropertyName(string propertyName)
        {
            for (int dof = 0; dof < 3; dof++)
            {
                if (propertyName == AnimationCurveBindingAnimatorRootT[dof].propertyName)
                    return true;
            }
            for (int dof = 0; dof < 4; dof++)
            {
                if (propertyName == AnimationCurveBindingAnimatorRootQ[dof].propertyName)
                    return true;
            }
            for (int dof = 0; dof < 3; dof++)
            {
                if (propertyName == AnimationCurveBindingAnimatorMotionT[dof].propertyName)
                    return true;
            }
            for (int dof = 0; dof < 4; dof++)
            {
                if (propertyName == AnimationCurveBindingAnimatorMotionQ[dof].propertyName)
                    return true;
            }
            for (var i = 0; i < (int)AnimatorIKIndex.Total; i++)
            {
                for (int dof = 0; dof < 3; dof++)
                {
                    if (propertyName == EditorCurveBindingAnimatorIkTPropertyNames[i][dof])
                        return true;
                }
                for (int dof = 0; dof < 4; dof++)
                {
                    if (propertyName == EditorCurveBindingAnimatorIkQPropertyNames[i][dof])
                        return true;
                }
            }
            for (var i = 0; i < (int)AnimatorTDOFIndex.Total; i++)
            {
                for (int dof = 0; dof < 3; dof++)
                {
                    if (propertyName == EditorCurveBindingAnimatorTDOFPropertyNames[i][dof])
                        return true;
                }
            }
            if (GetMuscleIndexFromPropertyName(propertyName) >= 0)
                return true;

            return false;
        }
        public bool IsTransformPositionCurveBinding(EditorCurveBinding binding)
        {
            if (binding.type != typeof(Transform)) return false;
            return binding.propertyName.StartsWith("m_LocalPosition.");
        }
        public bool IsTransformRotationCurveBinding(EditorCurveBinding binding)
        {
            if (binding.type != typeof(Transform)) return false;
            for (int i = 0; i < URotationCurveInterpolation.PrefixForInterpolation.Length; i++)
            {
                if (URotationCurveInterpolation.PrefixForInterpolation[i] == null) continue;
                if (binding.propertyName.StartsWith(URotationCurveInterpolation.PrefixForInterpolation[i]))
                    return true;
            }
            return false;
        }
        public bool IsTransformScaleCurveBinding(EditorCurveBinding binding)
        {
            if (binding.type != typeof(Transform)) return false;
            return binding.propertyName.StartsWith("m_LocalScale.");
        }
        public bool IsSkinnedMeshRendererBlendShapeCurveBinding(EditorCurveBinding binding)
        {
            return binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape.");
        }

        public bool IsBlendShapePropertyName(string name)
        {
            return name.StartsWith("blendShape.");
        }
        public string BlendShapeName2PropertyName(string name)
        {
            return "blendShape." + name;
        }
        public string PropertyName2BlendShapeName(string name)
        {
            return name.Remove(0, "blendShape.".Length);
        }
        #endregion

        #region HumanPose

        public void GetHumanPoseCurve(ref HumanPose humanPose, float time = -1f)
        {
            humanPose.bodyPosition = GetAnimationValueAnimatorRootT(time);
            humanPose.bodyRotation = GetAnimationValueAnimatorRootQ(time);
            humanPose.muscles = new float[HumanTrait.MuscleCount];
            for (int i = 0; i < humanPose.muscles.Length; i++)
            {
                humanPose.muscles[i] = GetAnimationValueAnimatorMuscle(i, time);
            }
        }
        #endregion

        #region EditorCurveCache
        #region EditorCurveBindingCache
        private Dictionary<string, Dictionary<Type, Dictionary<string, int>>> editorCurveBindingHashCacheDic;
        private void ClearEditorCurveBindingHashCode()
        {
            if (editorCurveBindingHashCacheDic == null)
                editorCurveBindingHashCacheDic = new Dictionary<string, Dictionary<Type, Dictionary<string, int>>>();
            else
                editorCurveBindingHashCacheDic.Clear();
        }
        public int GetEditorCurveBindingHashCode(EditorCurveBinding binding)
        {
            if (binding.path == null || binding.propertyName == null)
                return binding.GetHashCode();

            if (editorCurveBindingHashCacheDic == null)
                editorCurveBindingHashCacheDic = new Dictionary<string, Dictionary<Type, Dictionary<string, int>>>();

            int hashCode;
            Dictionary<Type, Dictionary<string, int>> typeNameDic;
            if (editorCurveBindingHashCacheDic.TryGetValue(binding.path, out typeNameDic))
            {
                Dictionary<string, int> propertyNameDic;
                if (typeNameDic.TryGetValue(binding.type, out propertyNameDic))
                {
                    if (propertyNameDic.TryGetValue(binding.propertyName, out hashCode))
                    {
                        return hashCode;
                    }
                    else
                    {
                        hashCode = binding.GetHashCode();
                        propertyNameDic.Add(binding.propertyName, hashCode);
                    }
                }
                else
                {
                    propertyNameDic = new Dictionary<string, int>();
                    hashCode = binding.GetHashCode();
                    propertyNameDic.Add(binding.propertyName, hashCode);
                    typeNameDic.Add(binding.type, propertyNameDic);
                }
            }
            else
            {
                typeNameDic = new Dictionary<Type, Dictionary<string, int>>();
                hashCode = binding.GetHashCode();
                {
                    var propertyNameDic = new Dictionary<string, int>();
                    propertyNameDic.Add(binding.propertyName, hashCode);
                    typeNameDic.Add(binding.type, propertyNameDic);
                }
                editorCurveBindingHashCacheDic.Add(binding.path, typeNameDic);
            }

            return hashCode;
        }
        #endregion

        private AnimationClip editorCurveCacheClip;
        private bool editorCurveCacheDirty;
        private class EditorCurveCacheDicData
        {
            public EditorCurveCacheDicData(AnimationCurve curve)
            {
                this.curve = curve;
                beforeKeys = new List<Keyframe>();
            }

            public AnimationCurve curve;
            public List<Keyframe> beforeKeys;
        }
        private Dictionary<int, EditorCurveCacheDicData> editorCurveCacheDic;

        private Dictionary<int, EditorCurveBinding> editorCurveDelayWriteDic;

        private struct EditorCurveWasModifiedDicData
        {
            public EditorCurveBinding binding;
            public AnimationUtility.CurveModifiedType type;
        }
        private Dictionary<int, EditorCurveWasModifiedDicData> editorCurveWasModifiedDic;

        private void ClearEditorCurveCache()
        {
            ClearEditorCurveBindingHashCode();

            editorCurveCacheClip = null;
            if (editorCurveCacheDic == null)
                editorCurveCacheDic = new Dictionary<int, EditorCurveCacheDicData>();
            else
                editorCurveCacheDic.Clear();

            editorCurveCacheDirty = true;
        }
        private void RemoveEditorCurveCache(EditorCurveBinding binding)
        {
            CheckChangeClipClearEditorCurveCache();
            if (editorCurveCacheDic == null) return;
            editorCurveCacheDic.Remove(GetEditorCurveBindingHashCode(binding));
        }
        private bool IsCheckChangeClipClearEditorCurveCache(AnimationClip clip)
        {
            return clip == editorCurveCacheClip;
        }
        private void CheckChangeClipClearEditorCurveCache()
        {
            if (!IsCheckChangeClipClearEditorCurveCache(currentClip))
            {
                ClearEditorCurveCache();
                editorCurveCacheClip = currentClip;
            }
        }
        private bool IsContainsEditorCurveCache(EditorCurveBinding binding)
        {
            CheckChangeClipClearEditorCurveCache();
            var hash = GetEditorCurveBindingHashCode(binding);
            return editorCurveCacheDic.ContainsKey(hash);
        }
        private AnimationCurve GetEditorCurveCache(EditorCurveBinding binding)
        {
            CheckChangeClipClearEditorCurveCache();
            if (editorCurveCacheDic == null)
                return null;
            var hash = GetEditorCurveBindingHashCode(binding);
            EditorCurveCacheDicData data = null;
            if (!editorCurveCacheDic.TryGetValue(hash, out data))
            {
                var curve = AnimationUtility.GetEditorCurve(currentClip, binding);     //If an error occurs on this line, execute Tools/Fix Errors.
                data = new EditorCurveCacheDicData(curve);
                if (curve != null)
                {
                    if (data.beforeKeys.Capacity < curve.length)
                        data.beforeKeys.Capacity = curve.length;
                    for (int i = 0; i < curve.length; i++)
                        data.beforeKeys.Add(curve[i]);
                }
                editorCurveCacheDic.Add(hash, data);
            }
            return data.curve;
        }
        private void SetEditorCurveCache(EditorCurveBinding binding, AnimationCurve curve)
        {
            CheckChangeClipClearEditorCurveCache();
            if (editorCurveCacheDic == null)
                editorCurveCacheDic = new Dictionary<int, EditorCurveCacheDicData>();
            if (editorCurveDelayWriteDic == null)
                editorCurveDelayWriteDic = new Dictionary<int, EditorCurveBinding>();
            if (editorCurveWasModifiedDic == null)
                editorCurveWasModifiedDic = new Dictionary<int, EditorCurveWasModifiedDicData>();
            var hash = GetEditorCurveBindingHashCode(binding);
            editorCurveDelayWriteDic[hash] = binding;
            EditorCurveCacheDicData data = null;
            if (!editorCurveCacheDic.TryGetValue(hash, out data))
                data = new EditorCurveCacheDicData(curve);
            else
                data.curve = curve;
            {
                var type = curve != null ? AnimationUtility.CurveModifiedType.CurveModified : AnimationUtility.CurveModifiedType.CurveDeleted;
                if (binding.type == typeof(Transform))
                {
                    if (binding.propertyName.StartsWith(URotationCurveInterpolation.PrefixForInterpolation[(int)URotationCurveInterpolation.Mode.RawQuaternions]))
                    {
                        var bindingSub = binding;
                        for (int dof = 0; dof < 3; dof++)
                        {
                            bindingSub.propertyName = EditorCurveBindingTransformRotationPropertyNames[(int)URotationCurveInterpolation.Mode.NonBaked][dof];
                            RemoveEditorCurveCache(bindingSub);
                            editorCurveWasModifiedDic[GetEditorCurveBindingHashCode(bindingSub)] = new EditorCurveWasModifiedDicData() { binding = bindingSub, type = type };
                        }
                    }
                    else if (binding.propertyName.StartsWith(URotationCurveInterpolation.PrefixForInterpolation[(int)URotationCurveInterpolation.Mode.NonBaked]))
                    {
                        var bindingSub = binding;
                        for (int dof = 0; dof < 4; dof++)
                        {
                            bindingSub.propertyName = EditorCurveBindingTransformRotationPropertyNames[(int)URotationCurveInterpolation.Mode.RawQuaternions][dof];
                            RemoveEditorCurveCache(bindingSub);
                        }
                        editorCurveWasModifiedDic[hash] = new EditorCurveWasModifiedDicData() { binding = binding, type = type };
                    }
                    else
                    {
                        editorCurveWasModifiedDic[hash] = new EditorCurveWasModifiedDicData() { binding = binding, type = type };
                    }
                }
                else
                {
                    editorCurveWasModifiedDic[hash] = new EditorCurveWasModifiedDicData() { binding = binding, type = type };
                }
                {
                    //new AnimationCurve(data.beforeKeys.ToArray())   Many gc alloc  
                    var beforeCurve = new AnimationCurve();
                    foreach (var key in data.beforeKeys)
                    {
                        beforeCurve.AddKey(key);
                    }
                    AddOnCurveWasModified(binding, type, beforeCurve);
                }
            }
            data.beforeKeys.Clear();
            if (curve != null)
            {
                if (data.beforeKeys.Capacity < curve.length)
                    data.beforeKeys.Capacity = curve.length;
                for (int i = 0; i < curve.length; i++)
                    data.beforeKeys.Add(curve[i]);
            }
            editorCurveCacheDic[hash] = data;
        }
        public void UpdateSyncEditorCurveClip()
        {
            if (editorCurveDelayWriteDic != null && editorCurveDelayWriteDic.Count > 0)
            {
                bool updated = false;
                foreach (var pair in editorCurveDelayWriteDic)
                {
                    EditorCurveCacheDicData data = null;
                    if (!editorCurveCacheDic.TryGetValue(pair.Key, out data))
                        continue;
                    uAnimationUtility.Internal_SetEditorCurve(editorCurveCacheClip, pair.Value, data.curve, false);
                    updated = true;
                }
                if (updated)
                {
                    uAnimationUtility.Internal_SyncEditorCurves(editorCurveCacheClip);
                }
                editorCurveDelayWriteDic.Clear();
            }

            if (editorCurveWasModifiedDic != null && editorCurveWasModifiedDic.Count > 0)
            {
                bool firstAddCurve = true;
                foreach (var pair in editorCurveWasModifiedDic)
                {
                    uAw.CurveWasModified(editorCurveCacheClip, pair.Value.binding, pair.Value.type);

                    #region PropertyFilterByBindings
                    if (vaw.editorSettings.settingPropertyStyle == EditorSettings.PropertyStyle.Filter)
                    {
                        if (pair.Value.type == AnimationUtility.CurveModifiedType.CurveModified &&
                            animationWindowFilterBindings != null &&
                            !animationWindowFilterBindings.Contains(pair.Value.binding))
                        {
                            animationWindowFilterBindings.Add(pair.Value.binding);
                            if (firstAddCurve)
                            {
                                SetAnimationWindowSynchroSelection(uAw.GetCurveSelection().ToArray());
                                firstAddCurve = false;
                            }
                            SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { pair.Value.binding });
                        }
                    }
                    #endregion
                }
                editorCurveWasModifiedDic.Clear();
            }
        }
        #endregion

        #region PoseTemplate
        [Flags]
        public enum PoseFlags : uint
        {
            Humanoid = (1 << 0),
            Generic = (1 << 1),
            BlendShape = (1 << 2),
            All = uint.MaxValue,
        }
        public void SavePoseTemplate(PoseTemplate poseTemplate, PoseFlags flags = PoseFlags.All)
        {
            poseTemplate.Reset();
            poseTemplate.isHuman = isHuman;
            #region Humanoid
            if (isHuman && (flags & PoseFlags.Humanoid) != 0)
            {
                poseTemplate.haveRootT = true;
                poseTemplate.rootT = GetAnimationValueAnimatorRootT();
                poseTemplate.haveRootQ = true;
                poseTemplate.rootQ = GetAnimationValueAnimatorRootQ();
                {
                    Dictionary<string, float> muscleList = new Dictionary<string, float>();
                    for (int muscleIndex = 0; muscleIndex < musclePropertyName.PropertyNames.Length; muscleIndex++)
                        muscleList.Add(musclePropertyName.PropertyNames[muscleIndex], GetAnimationValueAnimatorMuscle(muscleIndex));
                    poseTemplate.musclePropertyNames = muscleList.Keys.ToArray();
                    poseTemplate.muscleValues = muscleList.Values.ToArray();
                }
                {
                    Dictionary<AnimatorTDOFIndex, Vector3> tdofIndices = new Dictionary<AnimatorTDOFIndex, Vector3>();
                    for (AnimatorTDOFIndex tdofIndex = 0; tdofIndex < AnimatorTDOFIndex.Total; tdofIndex++)
                        tdofIndices.Add(tdofIndex, GetAnimationValueAnimatorTDOF(tdofIndex));
                    poseTemplate.tdofIndices = tdofIndices.Keys.ToArray();
                    poseTemplate.tdofValues = tdofIndices.Values.ToArray();
                }
                {
                    Dictionary<AnimatorIKIndex, PoseTemplate.IKData> ikIndices = new Dictionary<AnimatorIKIndex, PoseTemplate.IKData>();
                    for (AnimatorIKIndex ikIndex = 0; ikIndex < AnimatorIKIndex.Total; ikIndex++)
                    {
                        ikIndices.Add(ikIndex, new PoseTemplate.IKData()
                        {
                            position = GetAnimationValueAnimatorIkT(ikIndex),
                            rotation = GetAnimationValueAnimatorIkQ(ikIndex),
                        });
                    }
                    poseTemplate.ikIndices = ikIndices.Keys.ToArray();
                    poseTemplate.ikValues = ikIndices.Values.ToArray();
                }
            }
            #endregion
            #region Generic
            if ((flags & PoseFlags.Generic) != 0)
            {
                Dictionary<string, PoseTemplate.TransformData> transformList = new Dictionary<string, PoseTemplate.TransformData>();
                for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                {
                    if (transformList.ContainsKey(bonePaths[boneIndex]))
                        continue;
                    if (isHuman && humanoidConflict[boneIndex])
                    {
                        var t = editBones[boneIndex].transform;
                        transformList.Add(bonePaths[boneIndex], new PoseTemplate.TransformData()
                        {
                            position = t.localPosition,
                            rotation = t.localRotation,
                            scale = t.localScale,
                        });
                    }
                    else
                    {
                        transformList.Add(bonePaths[boneIndex], new PoseTemplate.TransformData()
                        {
                            position = GetAnimationValueTransformPosition(boneIndex),
                            rotation = GetAnimationValueTransformRotation(boneIndex),
                            scale = GetAnimationValueTransformScale(boneIndex),
                        });
                    }
                }
                poseTemplate.transformPaths = transformList.Keys.ToArray();
                poseTemplate.transformValues = transformList.Values.ToArray();
            }
            #endregion
            #region BlendShape
            if ((flags & PoseFlags.BlendShape) != 0)
            {
                Dictionary<string, PoseTemplate.BlendShapeData> blendShapeList = new Dictionary<string, PoseTemplate.BlendShapeData>();
                foreach (var renderer in vaw.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount <= 0) continue;
                    var path = AnimationUtility.CalculateTransformPath(renderer.transform, vaw.gameObject.transform);
                    var data = new PoseTemplate.BlendShapeData()
                    {
                        names = new string[renderer.sharedMesh.blendShapeCount],
                        weights = new float[renderer.sharedMesh.blendShapeCount],
                    };
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    {
                        data.names[i] = renderer.sharedMesh.GetBlendShapeName(i);
                        data.weights[i] = GetAnimationValueBlendShape(renderer, data.names[i]);
                    }
                    blendShapeList.Add(path, data);
                }
                poseTemplate.blendShapePaths = blendShapeList.Keys.ToArray();
                poseTemplate.blendShapeValues = blendShapeList.Values.ToArray();
            }
            #endregion
        }
        public void SaveSelectionPoseTemplate(PoseTemplate poseTemplate, PoseFlags flags = PoseFlags.All)
        {
            bool selectRoot = SelectionGameObjectsIndexOf(vaw.gameObject) >= 0;
            var selectHumanoidIndexes = SelectionGameObjectsHumanoidIndex();
            var selectMuscleIndexes = SelectionGameObjectsMuscleIndex();
            var selectAnimatorIKTargetsHumanoidIndexes = animatorIK.SelectionAnimatorIKTargetsHumanoidIndexes();
            var selectOriginalIKTargetsBoneIndexes = originalIK.SelectionOriginalIKTargetsBoneIndexes();
            //
            poseTemplate.Reset();
            poseTemplate.isHuman = isHuman;
            #region Humanoid
            if (isHuman && (flags & PoseFlags.Humanoid) != 0)
            {
                if (selectRoot)
                {
                    poseTemplate.haveRootT = true;
                    poseTemplate.rootT = GetAnimationValueAnimatorRootT();
                    poseTemplate.haveRootQ = true;
                    poseTemplate.rootQ = GetAnimationValueAnimatorRootQ();
                }
                {
                    Dictionary<string, float> muscleList = new Dictionary<string, float>();
                    for (int muscleIndex = 0; muscleIndex < musclePropertyName.PropertyNames.Length; muscleIndex++)
                    {
                        if (selectMuscleIndexes.Contains(muscleIndex) ||
                            selectAnimatorIKTargetsHumanoidIndexes.Contains((HumanBodyBones)HumanTrait.BoneFromMuscle(muscleIndex)))
                        {
                            muscleList.Add(musclePropertyName.PropertyNames[muscleIndex], GetAnimationValueAnimatorMuscle(muscleIndex));
                        }
                    }
                    poseTemplate.musclePropertyNames = muscleList.Keys.ToArray();
                    poseTemplate.muscleValues = muscleList.Values.ToArray();
                }
                {
                    Dictionary<AnimatorTDOFIndex, Vector3> tdofIndices = new Dictionary<AnimatorTDOFIndex, Vector3>();
                    for (AnimatorTDOFIndex tdofIndex = 0; tdofIndex < AnimatorTDOFIndex.Total; tdofIndex++)
                    {
                        if (selectHumanoidIndexes.Contains(AnimatorTDOFIndex2HumanBodyBones[(int)tdofIndex]) ||
                            selectAnimatorIKTargetsHumanoidIndexes.Contains(AnimatorTDOFIndex2HumanBodyBones[(int)tdofIndex]))
                        {
                            tdofIndices.Add(tdofIndex, GetAnimationValueAnimatorTDOF(tdofIndex));
                        }
                    }
                    poseTemplate.tdofIndices = tdofIndices.Keys.ToArray();
                    poseTemplate.tdofValues = tdofIndices.Values.ToArray();
                }
                {
                    Dictionary<AnimatorIKIndex, PoseTemplate.IKData> ikIndices = new Dictionary<AnimatorIKIndex, PoseTemplate.IKData>();
                    for (AnimatorIKIndex ikIndex = 0; ikIndex < AnimatorIKIndex.Total; ikIndex++)
                    {
                        if (selectHumanoidIndexes.Contains(AnimatorIKIndex2HumanBodyBones[(int)ikIndex]) ||
                            selectAnimatorIKTargetsHumanoidIndexes.Contains(AnimatorIKIndex2HumanBodyBones[(int)ikIndex]))
                        {
                            ikIndices.Add(ikIndex, new PoseTemplate.IKData()
                            {
                                position = GetAnimationValueAnimatorIkT(ikIndex),
                                rotation = GetAnimationValueAnimatorIkQ(ikIndex),
                            });
                        }
                    }
                    poseTemplate.ikIndices = ikIndices.Keys.ToArray();
                    poseTemplate.ikValues = ikIndices.Values.ToArray();
                }
            }
            #endregion
            #region Generic
            if ((flags & PoseFlags.Generic) != 0)
            {
                Dictionary<string, PoseTemplate.TransformData> transformList = new Dictionary<string, PoseTemplate.TransformData>();
                for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                {
                    if (selectionBones.Contains(boneIndex) ||
                        selectOriginalIKTargetsBoneIndexes.Contains(boneIndex))
                    {
                        if (transformList.ContainsKey(bonePaths[boneIndex]))
                            continue;
                        if (isHuman && humanoidConflict[boneIndex])
                        {
                            var t = editBones[boneIndex].transform;
                            transformList.Add(bonePaths[boneIndex], new PoseTemplate.TransformData()
                            {
                                position = t.localPosition,
                                rotation = t.localRotation,
                                scale = t.localScale,
                            });
                        }
                        else
                        {
                            transformList.Add(bonePaths[boneIndex], new PoseTemplate.TransformData()
                            {
                                position = GetAnimationValueTransformPosition(boneIndex),
                                rotation = GetAnimationValueTransformRotation(boneIndex),
                                scale = GetAnimationValueTransformScale(boneIndex),
                            });
                        }
                    }
                }
                poseTemplate.transformPaths = transformList.Keys.ToArray();
                poseTemplate.transformValues = transformList.Values.ToArray();
            }
            #endregion
            #region BlendShape
            if ((flags & PoseFlags.BlendShape) != 0)
            {
                Dictionary<string, PoseTemplate.BlendShapeData> blendShapeList = new Dictionary<string, PoseTemplate.BlendShapeData>();
                foreach (var boneIndex in selectionBones)
                {
                    var renderer = bones[boneIndex].GetComponent<SkinnedMeshRenderer>();
                    if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount <= 0) continue;
                    var path = AnimationUtility.CalculateTransformPath(renderer.transform, vaw.gameObject.transform);
                    var data = new PoseTemplate.BlendShapeData()
                    {
                        names = new string[renderer.sharedMesh.blendShapeCount],
                        weights = new float[renderer.sharedMesh.blendShapeCount],
                    };
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    {
                        data.names[i] = renderer.sharedMesh.GetBlendShapeName(i);
                        data.weights[i] = GetAnimationValueBlendShape(renderer, data.names[i]);
                    }
                    blendShapeList.Add(path, data);
                }
                poseTemplate.blendShapePaths = blendShapeList.Keys.ToArray();
                poseTemplate.blendShapeValues = blendShapeList.Values.ToArray();
            }
            #endregion
        }
        public void LoadPoseTemplate(PoseTemplate poseTemplate, bool calcIK = false, PoseFlags flags = PoseFlags.All)
        {
            #region Humanoid
            if ((flags & PoseFlags.Humanoid) != 0)
            {
                if (isHuman && poseTemplate.isHuman)
                {
                    if (poseTemplate.haveRootT)
                    {
                        SetAnimationValueAnimatorRootTIfNotOriginal(poseTemplate.rootT);
                    }
                    if (poseTemplate.haveRootQ)
                    {
                        SetAnimationValueAnimatorRootQIfNotOriginal(poseTemplate.rootQ);
                    }
                    if (poseTemplate.musclePropertyNames != null && poseTemplate.muscleValues != null)
                    {
                        Assert.IsTrue(poseTemplate.musclePropertyNames.Length == poseTemplate.muscleValues.Length);
                        for (int i = 0; i < poseTemplate.musclePropertyNames.Length; i++)
                        {
                            var muscleIndex = GetMuscleIndexFromPropertyName(poseTemplate.musclePropertyNames[i]);
                            if (muscleIndex < 0) continue;
                            SetAnimationValueAnimatorMuscleIfNotOriginal(muscleIndex, poseTemplate.muscleValues[i]);
                        }
                    }
                    if (poseTemplate.tdofIndices != null && poseTemplate.tdofValues != null)
                    {
                        Assert.IsTrue(poseTemplate.tdofIndices.Length == poseTemplate.tdofValues.Length);
                        for (int i = 0; i < poseTemplate.tdofIndices.Length; i++)
                        {
                            var tdofIndex = poseTemplate.tdofIndices[i];
                            var value = poseTemplate.tdofValues[i];
                            SetAnimationValueAnimatorTDOFIfNotOriginal(tdofIndex, value);
                        }
                    }
                    if (poseTemplate.ikIndices != null && poseTemplate.ikValues != null)
                    {
                        Assert.IsTrue(poseTemplate.ikIndices.Length == poseTemplate.ikValues.Length);
                        for (int i = 0; i < poseTemplate.ikIndices.Length; i++)
                        {
                            var ikIndex = poseTemplate.ikIndices[i];
                            var value = poseTemplate.ikValues[i];
                            SetAnimationValueAnimatorIkTIfNotOriginal(ikIndex, value.position);
                            SetAnimationValueAnimatorIkQIfNotOriginal(ikIndex, value.rotation);
                        }
                    }
                }
            }
            #endregion
            #region Generic
            if ((flags & PoseFlags.Generic) != 0)
            {
                if (poseTemplate.transformPaths != null && poseTemplate.transformValues != null)
                {
                    Assert.IsTrue(poseTemplate.transformPaths.Length == poseTemplate.transformValues.Length);
                    for (int i = 0; i < poseTemplate.transformPaths.Length; i++)
                    {
                        var boneIndex = GetBoneIndexFromPath(poseTemplate.transformPaths[i]);
                        if (boneIndex < 0 || (isHuman && humanoidConflict[boneIndex])) continue;
                        var position = poseTemplate.transformValues[i].position;
                        var rotation = poseTemplate.transformValues[i].rotation;
                        var scale = poseTemplate.transformValues[i].scale;
                        SetAnimationValueTransformPositionIfNotOriginal(boneIndex, position);
                        SetAnimationValueTransformRotationIfNotOriginal(boneIndex, rotation);
                        SetAnimationValueTransformScaleIfNotOriginal(boneIndex, scale);
                    }
                }
            }
            #endregion
            #region BlendShape
            if ((flags & PoseFlags.BlendShape) != 0)
            {
                if (poseTemplate.blendShapePaths != null && poseTemplate.blendShapeValues != null)
                {
                    foreach (var renderer in vaw.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount <= 0) continue;
                        var path = AnimationUtility.CalculateTransformPath(renderer.transform, vaw.gameObject.transform);
                        var index = EditorCommon.ArrayIndexOf(poseTemplate.blendShapePaths, path);
                        if (index < 0) continue;
                        for (int i = 0; i < poseTemplate.blendShapeValues[index].names.Length; i++)
                        {
                            SetAnimationValueBlendShapeIfNotOriginal(renderer, poseTemplate.blendShapeValues[index].names[i], poseTemplate.blendShapeValues[index].weights[i]);
                        }
                    }
                }
            }
            #endregion
            SetPoseAfter(calcIK);
        }
        public void LoadSelectionPoseTemplate(PoseTemplate poseTemplate, bool calcIK = false, PoseFlags flags = PoseFlags.All)
        {
            bool selectRoot = SelectionGameObjectsIndexOf(vaw.gameObject) >= 0;
            var selectHumanoidIndexes = SelectionGameObjectsHumanoidIndex();
            var selectMuscleIndexes = SelectionGameObjectsMuscleIndex();
            var selectAnimatorIKTargetsHumanoidIndexes = animatorIK.SelectionAnimatorIKTargetsHumanoidIndexes();
            var selectOriginalIKTargetsBoneIndexes = originalIK.SelectionOriginalIKTargetsBoneIndexes();
            //
            #region Humanoid
            if ((flags & PoseFlags.Humanoid) != 0)
            {
                if (isHuman && poseTemplate.isHuman)
                {
                    if (selectRoot)
                    {
                        if (poseTemplate.haveRootT)
                        {
                            SetAnimationValueAnimatorRootTIfNotOriginal(poseTemplate.rootT);
                        }
                        if (poseTemplate.haveRootQ)
                        {
                            SetAnimationValueAnimatorRootQIfNotOriginal(poseTemplate.rootQ);
                        }
                    }
                    if (poseTemplate.musclePropertyNames != null && poseTemplate.muscleValues != null)
                    {
                        Assert.IsTrue(poseTemplate.musclePropertyNames.Length == poseTemplate.muscleValues.Length);
                        for (int i = 0; i < poseTemplate.musclePropertyNames.Length; i++)
                        {
                            var muscleIndex = GetMuscleIndexFromPropertyName(poseTemplate.musclePropertyNames[i]);
                            if (muscleIndex < 0) continue;
                            if (selectMuscleIndexes.Contains(muscleIndex) ||
                                selectAnimatorIKTargetsHumanoidIndexes.Contains((HumanBodyBones)HumanTrait.BoneFromMuscle(muscleIndex)))
                            {
                                SetAnimationValueAnimatorMuscleIfNotOriginal(muscleIndex, poseTemplate.muscleValues[i]);
                            }
                        }
                    }
                    if (poseTemplate.tdofIndices != null && poseTemplate.tdofValues != null)
                    {
                        Assert.IsTrue(poseTemplate.tdofIndices.Length == poseTemplate.tdofValues.Length);
                        for (int i = 0; i < poseTemplate.tdofIndices.Length; i++)
                        {
                            var tdofIndex = poseTemplate.tdofIndices[i];
                            var value = poseTemplate.tdofValues[i];
                            if (selectHumanoidIndexes.Contains(AnimatorTDOFIndex2HumanBodyBones[(int)tdofIndex]) ||
                                selectAnimatorIKTargetsHumanoidIndexes.Contains(AnimatorTDOFIndex2HumanBodyBones[(int)tdofIndex]))
                            {
                                SetAnimationValueAnimatorTDOFIfNotOriginal(tdofIndex, value);
                            }
                        }
                    }
                    if (poseTemplate.ikIndices != null && poseTemplate.ikValues != null)
                    {
                        Assert.IsTrue(poseTemplate.ikIndices.Length == poseTemplate.ikValues.Length);
                        for (int i = 0; i < poseTemplate.ikIndices.Length; i++)
                        {
                            var ikIndex = poseTemplate.ikIndices[i];
                            var value = poseTemplate.ikValues[i];
                            if (selectHumanoidIndexes.Contains(AnimatorIKIndex2HumanBodyBones[(int)ikIndex]) ||
                                selectAnimatorIKTargetsHumanoidIndexes.Contains(AnimatorIKIndex2HumanBodyBones[(int)ikIndex]))
                            {
                                SetAnimationValueAnimatorIkTIfNotOriginal(ikIndex, value.position);
                                SetAnimationValueAnimatorIkQIfNotOriginal(ikIndex, value.rotation);
                            }
                        }
                    }
                }
            }
            #endregion
            #region Generic
            if ((flags & PoseFlags.Generic) != 0)
            {
                if (poseTemplate.transformPaths != null && poseTemplate.transformValues != null)
                {
                    Assert.IsTrue(poseTemplate.transformPaths.Length == poseTemplate.transformValues.Length);
                    for (int i = 0; i < poseTemplate.transformPaths.Length; i++)
                    {
                        var boneIndex = GetBoneIndexFromPath(poseTemplate.transformPaths[i]);
                        if (boneIndex < 0 || (isHuman && humanoidConflict[boneIndex])) continue;
                        if (selectionBones.Contains(boneIndex) ||
                            selectOriginalIKTargetsBoneIndexes.Contains(boneIndex))
                        {
                            var position = poseTemplate.transformValues[i].position;
                            var rotation = poseTemplate.transformValues[i].rotation;
                            var scale = poseTemplate.transformValues[i].scale;
                            SetAnimationValueTransformPositionIfNotOriginal(boneIndex, position);
                            SetAnimationValueTransformRotationIfNotOriginal(boneIndex, rotation);
                            SetAnimationValueTransformScaleIfNotOriginal(boneIndex, scale);
                        }
                    }
                }
            }
            #endregion
            #region BlendShape
            if ((flags & PoseFlags.BlendShape) != 0)
            {
                if (poseTemplate.blendShapePaths != null && poseTemplate.blendShapeValues != null)
                {
                    foreach (var renderer in vaw.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount <= 0) continue;
                        var path = AnimationUtility.CalculateTransformPath(renderer.transform, vaw.gameObject.transform);
                        var index = EditorCommon.ArrayIndexOf(poseTemplate.blendShapePaths, path);
                        if (index < 0) continue;
                        var boneIndex = GetBoneIndexFromPath(path);
                        if (boneIndex < 0) continue;
                        if (selectionBones.Contains(boneIndex))
                        {
                            for (int i = 0; i < poseTemplate.blendShapeValues[index].names.Length; i++)
                            {
                                SetAnimationValueBlendShapeIfNotOriginal(renderer, poseTemplate.blendShapeValues[index].names[i], poseTemplate.blendShapeValues[index].weights[i]);
                            }
                        }
                    }
                }
            }
            #endregion
            SetPoseAfter(calcIK);
        }
        #endregion

        #region IK
        public void IKHandleGUI()
        {
            animatorIK.HandleGUI();
            originalIK.HandleGUI();
        }
        public void IKTargetGUI()
        {
            animatorIK.TargetGUI();
            originalIK.TargetGUI();
        }

        private void IKUpdateBones()
        {
            animatorIK.UpdateBones();
        }
        private void IKChangeSelection()
        {
            if (animatorIK.ChangeSelectionIK()) return;
            if (originalIK.ChangeSelectionIK()) return;
        }

        public void ClearIkTargetSelect()
        {
            animatorIK.ikTargetSelect = null;
            animatorIK.OnSelectionChange();
            originalIK.ikTargetSelect = null;
            originalIK.OnSelectionChange();
        }

        public bool IsIKBone(HumanBodyBones humanoidIndex)
        {
            return animatorIK.IsIKBone(humanoidIndex) != AnimatorIKCore.IKTarget.None ||
                    originalIK.IsIKBone(humanoidIndex) >= 0;
        }
        public bool IsIKBone(int boneIndex)
        {
            return animatorIK.IsIKBone(boneIndex2humanoidIndex[boneIndex]) != AnimatorIKCore.IKTarget.None ||
                    originalIK.IsIKBone(boneIndex) >= 0;
        }

        public void SetUpdateIKtargetBone(int boneIndex)
        {
            if (boneIndex < 0) return;
            if (isHuman)
            {
                animatorIK.SetUpdateIKtargetBone(boneIndex);
            }
            originalIK.SetUpdateIKtargetBone(boneIndex);
        }
        public void SetUpdateIKtargetMuscle(int muscleIndex)
        {
            if (muscleIndex < 0) return;
            if (isHuman)
            {
                SetUpdateIKtargetHumanoidIndex((HumanBodyBones)HumanTrait.BoneFromMuscle(muscleIndex));
            }
        }
        public void SetUpdateIKtargetHumanoidIndex(HumanBodyBones humanoidIndex)
        {
            if (humanoidIndex < 0) return;
            if (isHuman)
            {
                var boneIndex = humanoidIndex2boneIndex[(int)humanoidIndex];
                if (boneIndex < 0)
                {
                    var virtualIndex = GetHumanVirtualBoneParentBone(humanoidIndex);
                    if (virtualIndex >= 0)
                        boneIndex = humanoidIndex2boneIndex[(int)virtualIndex];
                }
                SetUpdateIKtargetBone(boneIndex);
            }
        }
        public void SetUpdateIKtargetTdofIndex(AnimatorTDOFIndex tdofIndex)
        {
            if (tdofIndex < 0) return;
            if (isHuman)
            {
                var humanoidIndex = VeryAnimation.AnimatorTDOFIndex2HumanBodyBones[(int)tdofIndex];
                if (humanoidIndex < 0) return;
                SetUpdateIKtargetHumanoidIndex(humanoidIndex);
            }
        }
        public void SetUpdateIKtargetAnimatorIK(AnimatorIKCore.IKTarget ikTarget)
        {
            if (ikTarget < 0) return;
            if (isHuman)
            {
                animatorIK.SetUpdateIKtargetAnimatorIK(ikTarget);
                for (int humanoidIndex = 0; humanoidIndex < AnimatorIKCore.HumanBonesUpdateAnimatorIK.Length; humanoidIndex++)
                {
                    if (AnimatorIKCore.HumanBonesUpdateAnimatorIK[humanoidIndex] == ikTarget)
                    {
                        originalIK.SetUpdateIKtargetBone(humanoidIndex2boneIndex[humanoidIndex]);
                    }
                }
            }
        }
        public void SetUpdateIKtargetOriginalIK(int ikTarget)
        {
            if (ikTarget < 0) return;
            if (isHuman)
            {
                for (int i = 0; i < originalIK.ikData[ikTarget].level; i++)
                {
                    if (originalIK.ikData[ikTarget].joints[i] == null) continue;
                    animatorIK.SetUpdateIKtargetBone(BonesIndexOf(originalIK.ikData[ikTarget].joints[i].bone));
                }
            }
            originalIK.SetUpdateIKtargetOriginalIK(ikTarget);
        }
        public void ResetUpdateIKtargetAll()
        {
            animatorIK.ResetUpdateIKtargetAll();
            originalIK.ResetUpdateIKtargetAll();
        }
        public void SetUpdateIKtargetAll()
        {
            animatorIK.SetUpdateIKtargetAll();
            originalIK.SetUpdateIKtargetAll();
        }
        public bool GetUpdateIKtargetAll()
        {
            return animatorIK.GetUpdateIKtargetAll() || originalIK.GetUpdateIKtargetAll();
        }

        public void SetSynchroIKtargetBone(int boneIndex)
        {
            if (boneIndex < 0) return;
            if (isHuman)
            {
                animatorIK.SetSynchroIKtargetBone(boneIndex);
            }
            originalIK.SetSynchroIKtargetBone(boneIndex);
        }
        public void SetSynchroIKtargetMuscle(int muscleIndex)
        {
            if (muscleIndex < 0) return;
            if (isHuman)
            {
                SetSynchroIKtargetHumanoidIndex((HumanBodyBones)HumanTrait.BoneFromMuscle(muscleIndex));
            }
        }
        public void SetSynchroIKtargetHumanoidIndex(HumanBodyBones humanoidIndex)
        {
            if (humanoidIndex < 0) return;
            if (isHuman)
            {
                var boneIndex = humanoidIndex2boneIndex[(int)humanoidIndex];
                if (boneIndex < 0)
                {
                    var virtualIndex = GetHumanVirtualBoneParentBone(humanoidIndex);
                    if (virtualIndex >= 0)
                        boneIndex = humanoidIndex2boneIndex[(int)virtualIndex];
                }
                SetSynchroIKtargetBone(boneIndex);
            }
        }
        public void SetSynchroIKtargetTdofIndex(AnimatorTDOFIndex tdofIndex)
        {
            if (tdofIndex < 0) return;
            if (isHuman)
            {
                var humanoidIndex = VeryAnimation.AnimatorTDOFIndex2HumanBodyBones[(int)tdofIndex];
                if (humanoidIndex < 0) return;
                SetSynchroIKtargetHumanoidIndex(humanoidIndex);
            }
        }
        public void SetSynchroIKtargetAnimatorIK(AnimatorIKCore.IKTarget ikTarget)
        {
            if (ikTarget < 0) return;
            if (isHuman)
            {
                animatorIK.SetSynchroIKtargetAnimatorIK(ikTarget);
                for (int humanoidIndex = 0; humanoidIndex < AnimatorIKCore.HumanBonesUpdateAnimatorIK.Length; humanoidIndex++)
                {
                    if (AnimatorIKCore.HumanBonesUpdateAnimatorIK[humanoidIndex] == ikTarget)
                    {
                        originalIK.SetSynchroIKtargetBone(humanoidIndex2boneIndex[humanoidIndex]);
                    }
                }
            }
        }
        public void SetSynchroIKtargetOriginalIK(int ikTarget)
        {
            if (ikTarget < 0) return;
            if (isHuman)
            {
                for (int i = 0; i < originalIK.ikData[ikTarget].level; i++)
                {
                    if (originalIK.ikData[ikTarget].joints[i] == null) continue;
                    animatorIK.SetSynchroIKtargetBone(BonesIndexOf(originalIK.ikData[ikTarget].joints[i].bone));
                }
            }
            originalIK.SetSynchroIKtargetOriginalIK(ikTarget);
        }
        public void ResetSynchroIKtargetAll()
        {
            animatorIK.ResetSynchroIKtargetAll();
            originalIK.ResetSynchroIKtargetAll();
        }
        public void SetSynchroIKtargetAll()
        {
            animatorIK.SetSynchroIKtargetAll();
            originalIK.SetSynchroIKtargetAll();
        }
        public bool GetSynchroIKtargetAll()
        {
            return animatorIK.GetSynchroIKtargetAll() || originalIK.GetSynchroIKtargetAll();
        }
        public void UpdateSynchroIKSet()
        {
            animatorIK.UpdateSynchroIKSet();
            originalIK.UpdateSynchroIKSet();
        }
        #endregion

        #region AnimationCurve
        private class TmpCurves
        {
            public EditorCurveBinding[] bindings = new EditorCurveBinding[4];
            public AnimationCurve[] curves = new AnimationCurve[4];

            public EditorCurveBinding[] subBindings = new EditorCurveBinding[4];
            public AnimationCurve[] subCurves = new AnimationCurve[4];

            public void Clear()
            {
                for (int i = 0; i < 4; i++)
                {
                    curves[i] = subCurves[i] = null;
                }
            }
        }
        private TmpCurves tmpCurves = new TmpCurves();

        private void LoadTmpCurvesFullDof(EditorCurveBinding binding, int count)
        {
            tmpCurves.Clear();
            for (int i = 0; i < count; i++)
            {
                tmpCurves.bindings[i] = binding;
                tmpCurves.bindings[i].propertyName = tmpCurves.bindings[i].propertyName.Remove(tmpCurves.bindings[i].propertyName.Length - DofIndex2String[i].Length) + DofIndex2String[i];
                tmpCurves.curves[i] = GetEditorCurveCache(tmpCurves.bindings[i]);
            }
        }
        private void LoadTmpSubCurvesFullDof(EditorCurveBinding binding, int count)
        {
            for (int i = 0; i < count; i++)
            {
                tmpCurves.subBindings[i] = binding;
                tmpCurves.subBindings[i].propertyName = tmpCurves.subBindings[i].propertyName.Remove(tmpCurves.subBindings[i].propertyName.Length - DofIndex2String[i].Length) + DofIndex2String[i];
                tmpCurves.subCurves[i] = GetEditorCurveCache(tmpCurves.subBindings[i]);
            }
        }

        private bool beginChangeAnimationCurve;
        private bool BeginChangeAnimationCurve(AnimationClip clip, string undoName)
        {
            SetUpdateSampleAnimation();
            if (!beginChangeAnimationCurve)
            {
                if (clip == null) return false;
                if ((clip.hideFlags & HideFlags.NotEditable) != HideFlags.None)
                {
                    EditorCommon.ShowNotification("Read-Only");
                    Debug.LogErrorFormat(Language.GetText(Language.Help.LogAnimationClipReadOnlyError), clip.name);
                    return false;
                }

                Undo.RegisterCompleteObjectUndo(clip, undoName);
                uAnimatorControllerTool.SetAnimatorController(null);
                uParameterControllerEditor.SetAnimatorController(null);
                beginChangeAnimationCurve = true;
            }
            return true;
        }
        private void EndChangeAnimationCurve()
        {
            if (!beginChangeAnimationCurve) return;
            var ac = EditorCommon.GetAnimatorController(vaw.animator);
            if (ac != null)
            {
                uAnimatorControllerTool.SetAnimatorController(ac);
                uParameterControllerEditor.SetAnimatorController(ac);
            }
            beginChangeAnimationCurve = false;
        }

        public void SetPoseHumanoidDefault()
        {
            ResetAllHaveAnimationCurve(PoseFlags.Humanoid);
            SetPoseAfter();
        }
        public void SetPoseHumanoidAvatarConfiguration()
        {
            ResetAllHaveAnimationCurve();
            transformPoseSave.ResetHumanDescriptionTransforms();
            blendShapeWeightSave.ResetDefaultWeight();
            SetAllChangedAnimationCurve();
            SetPoseAfter();
        }
        public void SetPoseHumanoidTPose()
        {
            ResetAllHaveAnimationCurve(PoseFlags.Humanoid);
            transformPoseSave.ResetTPoseTransform();
            blendShapeWeightSave.ResetDefaultWeight();
            SetAllChangedAnimationCurve(PoseFlags.Humanoid);
            SetPoseAfter();
        }
        public void SetPoseEditStart()
        {
            ResetAllHaveAnimationCurve();
            SetAllChangedAnimationCurve();
            SetPoseAfter();
        }
        public void SetPoseBind()
        {
            ResetAllHaveAnimationCurve();
            transformPoseSave.ResetBindTransform();
            blendShapeWeightSave.ResetDefaultWeight();
            SetAllChangedAnimationCurve();
            SetPoseAfter();
        }
        public void SetPosePrefab()
        {
#if UNITY_2018_2_OR_NEWER
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(vaw.gameObject) as GameObject;
#else
            var prefab = PrefabUtility.GetPrefabParent(vaw.gameObject) as GameObject;
#endif
            if (prefab == null) return;

            ResetAllHaveAnimationCurve();
            transformPoseSave.ResetPrefabTransform();
            blendShapeWeightSave.ResetPrefabWeight();
            SetAllChangedAnimationCurve();
            SetPoseAfter();
        }

        public void SetPoseMirror()
        {
            #region Humanoid
            if (isHuman)
            {
                {
                    var rootT = GetAnimationValueAnimatorRootT();
                    SetAnimationValueAnimatorRootTIfNotOriginal(new Vector3(-rootT.x, rootT.y, rootT.z));
                    var rootQ = GetAnimationValueAnimatorRootQ();
                    SetAnimationValueAnimatorRootQIfNotOriginal(new Quaternion(rootQ.x, -rootQ.y, -rootQ.z, rootQ.w));
                }
                {
                    var values = new float[HumanTrait.MuscleCount];
                    for (int i = 0; i < values.Length; i++)
                    {
                        var mmi = GetMirrorMuscleIndex(i);
                        if (mmi < 0)
                            values[i] = float.MaxValue;
                        else
                            values[i] = GetAnimationValueAnimatorMuscle(mmi);
                    }
                    for (int i = 0; i < values.Length; i++)
                    {
                        if (values[i] == float.MaxValue)
                        {
                            var hi = HumanTrait.BoneFromMuscle(i);
                            if (i == HumanTrait.MuscleFromBone(hi, 0) || i == HumanTrait.MuscleFromBone(hi, 1))
                            {
                                SetAnimationValueAnimatorMuscleIfNotOriginal(i, -GetAnimationValueAnimatorMuscle(i));
                            }
                        }
                        else
                        {
                            SetAnimationValueAnimatorMuscleIfNotOriginal(i, values[i]);
                        }
                    }
                }
                {
                    Vector3[] saves = new Vector3[(int)AnimatorTDOFIndex.Total];
                    for (var tdof = (AnimatorTDOFIndex)0; tdof < AnimatorTDOFIndex.Total; tdof++)
                    {
                        saves[(int)tdof] = GetAnimationValueAnimatorTDOF(tdof);
                    }
                    for (var tdof = (AnimatorTDOFIndex)0; tdof < AnimatorTDOFIndex.Total; tdof++)
                    {
                        var mmi = AnimatorTDOFMirrorIndexes[(int)tdof];
                        var vec = Vector3.zero;
                        if (mmi != AnimatorTDOFIndex.None)
                        {
                            vec = Vector3.Scale(saves[(int)mmi], HumanBonesAnimatorTDOFIndex[(int)AnimatorTDOFIndex2HumanBodyBones[(int)mmi]].mirror);
                        }
                        else
                        {
                            vec = saves[(int)tdof];
                            vec.z = -vec.z;
                        }
                        SetAnimationValueAnimatorTDOFIfNotOriginal(tdof, vec);
                    }
                }
            }
            #endregion
            var bindings = AnimationUtility.GetCurveBindings(currentClip);
            #region Generic
            {
                var values = new Dictionary<int, TransformPoseSave.SaveData>();
                for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                {
                    if (values.ContainsKey(boneIndex)) continue;
                    values.Add(boneIndex, new TransformPoseSave.SaveData());
                    var mbi = mirrorBoneIndexes[boneIndex];
                    if (mbi >= 0)
                    {
                        values[boneIndex].localPosition = GetMirrorBoneLocalPosition(mbi, GetAnimationValueTransformPosition(mbi));
                        values[boneIndex].localRotation = GetMirrorBoneLocalRotation(mbi, GetAnimationValueTransformRotation(mbi));
                        values[boneIndex].localScale = GetMirrorBoneLocalScale(mbi, GetAnimationValueTransformScale(mbi));
                        if (!values.ContainsKey(mbi))
                        {
                            values.Add(mbi, new TransformPoseSave.SaveData());
                            values[mbi].localPosition = GetMirrorBoneLocalPosition(boneIndex, GetAnimationValueTransformPosition(boneIndex));
                            values[mbi].localRotation = GetMirrorBoneLocalRotation(boneIndex, GetAnimationValueTransformRotation(boneIndex));
                            values[mbi].localScale = GetMirrorBoneLocalScale(boneIndex, GetAnimationValueTransformScale(boneIndex));
                        }
                    }
                    else
                    {
                        values[boneIndex].localPosition = GetMirrorBoneLocalPosition(boneIndex, GetAnimationValueTransformPosition(boneIndex));
                        values[boneIndex].localRotation = GetMirrorBoneLocalRotation(boneIndex, GetAnimationValueTransformRotation(boneIndex));
                        values[boneIndex].localScale = GetMirrorBoneLocalScale(boneIndex, GetAnimationValueTransformScale(boneIndex));
                    }
                }
                foreach (var pair in values)
                {
                    var bi = pair.Key;
                    if (isHuman && humanoidConflict[bi]) continue;
                    SetAnimationValueTransformPositionIfNotOriginal(bi, pair.Value.localPosition);
                    SetAnimationValueTransformRotationIfNotOriginal(bi, pair.Value.localRotation);
                    if (vaw.editorSettings.settingGenericMirrorScale)
                        SetAnimationValueTransformScaleIfNotOriginal(bi, pair.Value.localScale);
                }
            }
            #endregion
            #region BlendShape
            {
                var values = new Dictionary<SkinnedMeshRenderer, Dictionary<string, float>>();
                foreach (var binding in bindings)
                {
                    if (!IsSkinnedMeshRendererBlendShapeCurveBinding(binding)) continue;
                    var boneIndex = GetBoneIndexFromPath(binding.path);
                    if (boneIndex < 0) continue;
                    var renderer = bones[boneIndex].GetComponent<SkinnedMeshRenderer>();
                    if (renderer == null) continue;
                    var name = PropertyName2BlendShapeName(binding.propertyName);
                    Dictionary<string, string> nameTable;
                    if (mirrorBlendShape.TryGetValue(renderer, out nameTable))
                    {
                        string mirrorName;
                        if (nameTable.TryGetValue(name, out mirrorName))
                        {
                            if (!values.ContainsKey(renderer))
                                values.Add(renderer, new Dictionary<string, float>());
                            values[renderer].Add(mirrorName, GetAnimationValueBlendShape(renderer, name));
                            #region NotHaveMirrorCurve
                            {
                                var mbinding = AnimationCurveBindingBlendShape(renderer, mirrorName);
                                if (!EditorCommon.ArrayContains(bindings, mbinding))
                                {
                                    values[renderer].Add(name, blendShapeWeightSave.GetOriginalWeight(renderer, mirrorName));
                                }
                            }
                            #endregion
                        }
                    }
                }
                foreach (var list in values)
                {
                    foreach (var pair in list.Value)
                    {
                        SetAnimationValueBlendShapeIfNotOriginal(list.Key, pair.Key, pair.Value);
                    }
                }
            }
            #endregion
            SetPoseAfter();
        }

        public void SetSelectionHumanoidDefault(bool position, bool rotation, bool scale)
        {
            var selectHumanoidIndexes = SelectionGameObjectsHumanoidIndex();
            var selectMuscleIndexes = SelectionGameObjectsMuscleIndex();
            {
                if (rotation)
                {
                    foreach (var muscle in selectMuscleIndexes)
                        SetAnimationValueAnimatorMuscleIfNotOriginal(muscle, 0f);
                }
                if (position)
                {
                    foreach (int hi in selectHumanoidIndexes)
                    {
                        if (HumanBonesAnimatorTDOFIndex[hi] == null) continue;
                        SetAnimationValueAnimatorTDOFIfNotOriginal(HumanBonesAnimatorTDOFIndex[hi].index, Vector3.zero);
                    }
                }
            }
            if (SelectionGameObjectsIndexOf(vaw.gameObject) >= 0)
            {
                if (position)
                    SetAnimationValueAnimatorRootTIfNotOriginal(new Vector3(0, 1, 0));
                if (rotation)
                    SetAnimationValueAnimatorRootQIfNotOriginal(Quaternion.identity);
            }

            SetSelectionCommonOriginal();

            SetUpdateSampleAnimation(true);
        }
        public void SetSelectionHumanoidAvatarConfiguration(bool position, bool rotation, bool scale)
        {
            transformPoseSave.ResetHumanDescriptionTransforms();
            blendShapeWeightSave.ResetDefaultWeight();

            var selectHumanoidIndexes = SelectionGameObjectsHumanoidIndex();
            var selectMuscleIndexes = SelectionGameObjectsMuscleIndex();
            if (position)
            {
                foreach (int hi in selectHumanoidIndexes)
                {
                    if (HumanBonesAnimatorTDOFIndex[hi] != null)
                        SetAnimationValueAnimatorTDOFIfNotOriginal(HumanBonesAnimatorTDOFIndex[hi].index, Vector3.zero);
                }
            }

            {
                HumanPose hp = new HumanPose();
                GetEditGameObjectHumanPose(ref hp, EditObjectFlag.Base);
                if (SelectionGameObjectsIndexOf(vaw.gameObject) >= 0)
                {
                    if (position)
                        SetAnimationValueAnimatorRootTIfNotOriginal(hp.bodyPosition);
                    if (rotation)
                        SetAnimationValueAnimatorRootQIfNotOriginal(hp.bodyRotation);
                }
                if (rotation)
                {
                    foreach (var muscle in selectMuscleIndexes)
                        SetAnimationValueAnimatorMuscleIfNotOriginal(muscle, hp.muscles[muscle]);
                }
            }

            SetSelectionCommonOriginal();

            SetUpdateSampleAnimation(true);
        }
        public void SetSelectionHumanoidTPose(bool position, bool rotation, bool scale)
        {
            transformPoseSave.ResetTPoseTransform();
            blendShapeWeightSave.ResetDefaultWeight();

            var selectHumanoidIndexes = SelectionGameObjectsHumanoidIndex();
            var selectMuscleIndexes = SelectionGameObjectsMuscleIndex();
            if (position)
            {
                foreach (int hi in selectHumanoidIndexes)
                {
                    if (HumanBonesAnimatorTDOFIndex[hi] != null)
                        SetAnimationValueAnimatorTDOFIfNotOriginal(HumanBonesAnimatorTDOFIndex[hi].index, Vector3.zero);
                }
            }

            {
                HumanPose hp = new HumanPose();
                GetEditGameObjectHumanPose(ref hp, EditObjectFlag.Base);
                if (SelectionGameObjectsIndexOf(vaw.gameObject) >= 0)
                {
                    if (position)
                        SetAnimationValueAnimatorRootTIfNotOriginal(hp.bodyPosition);
                    if (rotation)
                        SetAnimationValueAnimatorRootQIfNotOriginal(hp.bodyRotation);
                }
                if (rotation)
                {
                    foreach (var muscle in selectMuscleIndexes)
                        SetAnimationValueAnimatorMuscleIfNotOriginal(muscle, hp.muscles[muscle]);
                }
            }

            SetSelectionCommonOriginal();

            SetUpdateSampleAnimation(true);
        }
        public void SetSelectionBindPose(bool position, bool rotation, bool scale)
        {
            transformPoseSave.ResetBindTransform();
            blendShapeWeightSave.ResetDefaultWeight();

            #region Humanoid
            {
                var selectHumanoidIndexes = SelectionGameObjectsHumanoidIndex();
                var selectMuscleIndexes = SelectionGameObjectsMuscleIndex();
                if (position)
                {
                    foreach (int hi in selectHumanoidIndexes)
                    {
                        if (HumanBonesAnimatorTDOFIndex[hi] != null)
                            SetAnimationValueAnimatorTDOFIfNotOriginal(HumanBonesAnimatorTDOFIndex[hi].index, Vector3.zero);
                    }
                }

                {
                    HumanPose hp = new HumanPose();
                    GetEditGameObjectHumanPose(ref hp, EditObjectFlag.Base);
                    if (SelectionGameObjectsIndexOf(vaw.gameObject) >= 0)
                    {
                        if (position)
                            SetAnimationValueAnimatorRootTIfNotOriginal(hp.bodyPosition);
                        if (rotation)
                            SetAnimationValueAnimatorRootQIfNotOriginal(hp.bodyRotation);
                    }
                    if (rotation)
                    {
                        foreach (var muscle in selectMuscleIndexes)
                            SetAnimationValueAnimatorMuscleIfNotOriginal(muscle, hp.muscles[muscle]);
                    }
                }
            }
            #endregion

            #region Generic
            {
                var selectOriginalIKTargetsBoneIndexes = originalIK.SelectionOriginalIKTargetsBoneIndexes();
                var boneIndexes = new List<int>(selectionBones);
                boneIndexes.AddRange(selectOriginalIKTargetsBoneIndexes);
                foreach (var bi in boneIndexes)
                {
                    if (isHuman && humanoidConflict[bi]) continue;
                    if (position)
                        SetAnimationValueTransformPositionIfNotOriginal(bi, bones[bi].transform.localPosition);
                    if (rotation)
                        SetAnimationValueTransformRotationIfNotOriginal(bi, bones[bi].transform.localRotation);
                    if (scale)
                        SetAnimationValueTransformScaleIfNotOriginal(bi, bones[bi].transform.localScale);
                }
            }
            #endregion

            SetSelectionCommonOriginal();

            SetUpdateSampleAnimation(true);
        }
        public void SetSelectionPrefabPose(bool position, bool rotation, bool scale)
        {
            transformPoseSave.ResetPrefabTransform();
            blendShapeWeightSave.ResetPrefabWeight();

            #region Humanoid
            {
                var selectHumanoidIndexes = SelectionGameObjectsHumanoidIndex();
                var selectMuscleIndexes = SelectionGameObjectsMuscleIndex();
                if (position)
                {
                    foreach (int hi in selectHumanoidIndexes)
                    {
                        if (HumanBonesAnimatorTDOFIndex[hi] != null)
                            SetAnimationValueAnimatorTDOFIfNotOriginal(HumanBonesAnimatorTDOFIndex[hi].index, Vector3.zero);
                    }
                }

                {
                    HumanPose hp = new HumanPose();
                    GetEditGameObjectHumanPose(ref hp, EditObjectFlag.Base);
                    if (SelectionGameObjectsIndexOf(vaw.gameObject) >= 0)
                    {
                        if (position)
                            SetAnimationValueAnimatorRootTIfNotOriginal(hp.bodyPosition);
                        if (rotation)
                            SetAnimationValueAnimatorRootQIfNotOriginal(hp.bodyRotation);
                    }
                    if (rotation)
                    {
                        foreach (var muscle in selectMuscleIndexes)
                            SetAnimationValueAnimatorMuscleIfNotOriginal(muscle, hp.muscles[muscle]);
                    }
                }
            }
            #endregion

            #region Generic
            {
                var selectOriginalIKTargetsBoneIndexes = originalIK.SelectionOriginalIKTargetsBoneIndexes();
                var boneIndexes = new List<int>(selectionBones);
                boneIndexes.AddRange(selectOriginalIKTargetsBoneIndexes);
                foreach (var bi in boneIndexes)
                {
                    if (isHuman && humanoidConflict[bi]) continue;
                    if (position)
                        SetAnimationValueTransformPositionIfNotOriginal(bi, bones[bi].transform.localPosition);
                    if (rotation)
                        SetAnimationValueTransformRotationIfNotOriginal(bi, bones[bi].transform.localRotation);
                    if (scale)
                        SetAnimationValueTransformScaleIfNotOriginal(bi, bones[bi].transform.localScale);
                }
            }
            #endregion

            #region Motion
            if (selectionMotionTool)
            {
                SetAnimationValueAnimatorMotionTIfNotOriginal(Vector3.zero);
                SetAnimationValueAnimatorMotionQIfNotOriginal(Quaternion.identity);
            }
            #endregion

            #region BlendShape
            {
                foreach (var boneIndex in selectionBones)
                {
                    var renderer = bones[boneIndex].GetComponent<SkinnedMeshRenderer>();
                    if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount == 0) continue;
                    blendShapeWeightSave.ActionOriginalWeights(renderer, (name, value) =>
                    {
                        if (blendShapeWeightSave.IsHavePrefabWeight(renderer, name))
                            SetAnimationValueBlendShape(renderer, name, blendShapeWeightSave.GetPrefabWeight(renderer, name));
                        else
                            SetAnimationValueBlendShapeIfNotOriginal(renderer, name, blendShapeWeightSave.GetDefaultWeight(renderer, name));
                    });
                }
            }
            #endregion

            SetUpdateSampleAnimation(true);
        }
        public void SetSelectionEditStart(bool position, bool rotation, bool scale)
        {
            transformPoseSave.ResetOriginalTransform();
            blendShapeWeightSave.ResetOriginalWeight();

            #region Humanoid
            {
                var selectHumanoidIndexes = SelectionGameObjectsHumanoidIndex();
                var selectMuscleIndexes = SelectionGameObjectsMuscleIndex();
                if (position)
                {
                    foreach (int hi in selectHumanoidIndexes)
                    {
                        if (HumanBonesAnimatorTDOFIndex[hi] != null)
                            SetAnimationValueAnimatorTDOFIfNotOriginal(HumanBonesAnimatorTDOFIndex[hi].index, Vector3.zero);
                    }
                }

                {
                    HumanPose hp = new HumanPose();
                    GetEditGameObjectHumanPose(ref hp, EditObjectFlag.Base);
                    if (SelectionGameObjectsIndexOf(vaw.gameObject) >= 0)
                    {
                        if (position)
                            SetAnimationValueAnimatorRootTIfNotOriginal(hp.bodyPosition);
                        if (rotation)
                            SetAnimationValueAnimatorRootQIfNotOriginal(hp.bodyRotation);
                    }
                    if (rotation)
                    {
                        foreach (var muscle in selectMuscleIndexes)
                            SetAnimationValueAnimatorMuscleIfNotOriginal(muscle, hp.muscles[muscle]);
                    }
                }
            }
            #endregion

            #region Generic
            {
                var selectOriginalIKTargetsBoneIndexes = originalIK.SelectionOriginalIKTargetsBoneIndexes();
                var boneIndexes = new List<int>(selectionBones);
                boneIndexes.AddRange(selectOriginalIKTargetsBoneIndexes);
                foreach (var bi in boneIndexes)
                {
                    if (isHuman && humanoidConflict[bi]) continue;
                    if (position)
                        SetAnimationValueTransformPositionIfNotOriginal(bi, bones[bi].transform.localPosition);
                    if (rotation)
                        SetAnimationValueTransformRotationIfNotOriginal(bi, bones[bi].transform.localRotation);
                    if (scale)
                        SetAnimationValueTransformScaleIfNotOriginal(bi, bones[bi].transform.localScale);
                }
            }
            #endregion

            SetSelectionCommonOriginal();

            SetUpdateSampleAnimation(true);
        }
        private void SetSelectionCommonOriginal()
        {
            #region Motion
            if (selectionMotionTool)
            {
                SetAnimationValueAnimatorMotionTIfNotOriginal(Vector3.zero);
                SetAnimationValueAnimatorMotionQIfNotOriginal(Quaternion.identity);
            }
            #endregion

            #region BlendShape
            {
                foreach (var boneIndex in selectionBones)
                {
                    var renderer = bones[boneIndex].GetComponent<SkinnedMeshRenderer>();
                    if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount == 0) continue;
                    blendShapeWeightSave.ActionOriginalWeights(renderer, (name, value) =>
                    {
                        SetAnimationValueBlendShapeIfNotOriginal(renderer, name, blendShapeWeightSave.GetDefaultWeight(renderer, name));
                    });
                }
            }
            #endregion
        }
        public void SetSelectionMirror()
        {
            #region Humanoid
            if (isHuman)
            {
                var selectAnimatorIKTargetsHumanoidIndexes = animatorIK.SelectionAnimatorIKTargetsHumanoidIndexes();
                var selectMuscleIndexes = SelectionGameObjectsMuscleIndex();
                foreach (var humanoidIndex in selectAnimatorIKTargetsHumanoidIndexes)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        var muscleIndex = HumanTrait.MuscleFromBone((int)humanoidIndex, j);
                        if (muscleIndex < 0) continue;
                        selectMuscleIndexes.Add(muscleIndex);
                    }
                }
                int[] mirrorMuscles = new int[selectMuscleIndexes.Count];
                var values = new float[selectMuscleIndexes.Count];
                for (int i = 0; i < selectMuscleIndexes.Count; i++)
                {
                    mirrorMuscles[i] = GetMirrorMuscleIndex(selectMuscleIndexes[i]);
                    if (mirrorMuscles[i] >= 0)
                        values[i] = GetAnimationValueAnimatorMuscle(mirrorMuscles[i]);
                }
                for (int i = 0; i < selectMuscleIndexes.Count; i++)
                {
                    if (mirrorMuscles[i] < 0)
                    {
                        var hi = HumanTrait.BoneFromMuscle(selectMuscleIndexes[i]);
                        if (selectMuscleIndexes[i] == HumanTrait.MuscleFromBone(hi, 0) || selectMuscleIndexes[i] == HumanTrait.MuscleFromBone(hi, 1))
                        {
                            var value = -GetAnimationValueAnimatorMuscle(selectMuscleIndexes[i]);
                            SetAnimationValueAnimatorMuscleIfNotOriginal(selectMuscleIndexes[i], value);
                        }
                    }
                    else
                    {
                        SetAnimationValueAnimatorMuscleIfNotOriginal(selectMuscleIndexes[i], values[i]);
                    }
                }
                if (humanoidHasTDoF)
                {
                    var his = SelectionGameObjectsHumanoidIndex();
                    his.AddRange(selectAnimatorIKTargetsHumanoidIndexes);
                    Vector3[] saves = new Vector3[(int)AnimatorTDOFIndex.Total];
                    foreach (var hi in his)
                    {
                        if (HumanBonesAnimatorTDOFIndex[(int)hi] == null) continue;
                        var tdof = HumanBonesAnimatorTDOFIndex[(int)hi].index;
                        var mmi = AnimatorTDOFMirrorIndexes[(int)tdof];
                        if (mmi != AnimatorTDOFIndex.None)
                            saves[(int)mmi] = GetAnimationValueAnimatorTDOF(mmi);
                        else
                            saves[(int)tdof] = GetAnimationValueAnimatorTDOF(tdof);
                    }
                    foreach (var hi in his)
                    {
                        if (HumanBonesAnimatorTDOFIndex[(int)hi] == null) continue;
                        var tdof = HumanBonesAnimatorTDOFIndex[(int)hi].index;
                        var mmi = AnimatorTDOFMirrorIndexes[(int)tdof];
                        var vec = Vector3.zero;
                        if (mmi != AnimatorTDOFIndex.None)
                        {
                            vec = Vector3.Scale(saves[(int)mmi], HumanBonesAnimatorTDOFIndex[(int)AnimatorTDOFIndex2HumanBodyBones[(int)mmi]].mirror);
                        }
                        else
                        {
                            vec = saves[(int)tdof];
                            vec.z = -vec.z;
                        }
                        SetAnimationValueAnimatorTDOFIfNotOriginal(tdof, vec);
                    }
                }

                if (selectionBones.Contains(rootMotionBoneIndex))
                {
                    var rootT = GetAnimationValueAnimatorRootT();
                    SetAnimationValueAnimatorRootTIfNotOriginal(new Vector3(-rootT.x, rootT.y, rootT.z));
                    var rootQ = GetAnimationValueAnimatorRootQ();
                    SetAnimationValueAnimatorRootQIfNotOriginal(new Quaternion(rootQ.x, -rootQ.y, -rootQ.z, rootQ.w));
                }

                if (animatorIK.ikTargetSelect != null)
                {
                    foreach (var ikTarget in animatorIK.ikTargetSelect)
                    {
                        SetSynchroIKtargetAnimatorIK(ikTarget);
                    }
                }
            }
            #endregion

            #region Generic
            {
                var selectOriginalIKTargetsBoneIndexes = originalIK.SelectionOriginalIKTargetsBoneIndexes();
                var bones = new List<int>(selectionBones);
                bones.AddRange(selectOriginalIKTargetsBoneIndexes);
                var values = new TransformPoseSave.SaveData[bones.Count];
                for (int i = 0; i < bones.Count; i++)
                {
                    var mbi = mirrorBoneIndexes[bones[i]];
                    if (mbi >= 0)
                    {
                        var mt = editBones[mbi].transform;
                        values[i] = new TransformPoseSave.SaveData()
                        {
                            localPosition = GetMirrorBoneLocalPosition(mbi, mt.localPosition),
                            localRotation = GetMirrorBoneLocalRotation(mbi, mt.localRotation),
                            localScale = GetMirrorBoneLocalScale(mbi, mt.localScale),
                        };
                    }
                    else
                    {
                        var bi = bones[i];
                        var t = editBones[bi].transform;
                        values[i] = new TransformPoseSave.SaveData()
                        {
                            localPosition = GetMirrorBoneLocalPosition(bi, t.localPosition),
                            localRotation = GetMirrorBoneLocalRotation(bi, t.localRotation),
                            localScale = GetMirrorBoneLocalScale(bi, t.localScale),
                        };
                    }
                }
                for (int i = 0; i < bones.Count; i++)
                {
                    var bi = bones[i];
                    if (isHuman && humanoidConflict[bi]) continue;
                    SetAnimationValueTransformPositionIfNotOriginal(bi, values[i].localPosition);
                    SetAnimationValueTransformRotationIfNotOriginal(bi, values[i].localRotation);
                    if (vaw.editorSettings.settingGenericMirrorScale)
                        SetAnimationValueTransformScaleIfNotOriginal(bi, values[i].localScale);
                }

                if (originalIK.ikTargetSelect != null)
                {
                    foreach (var ikTarget in originalIK.ikTargetSelect)
                    {
                        SetSynchroIKtargetOriginalIK(ikTarget);
                    }
                }
            }
            #endregion

            #region Motion
            if (selectionMotionTool)
            {
                var motionT = GetAnimationValueAnimatorMotionT();
                SetAnimationValueAnimatorMotionTIfNotOriginal(new Vector3(-motionT.x, motionT.y, motionT.z));
                var motionQ = GetAnimationValueAnimatorMotionQ();
                SetAnimationValueAnimatorMotionQIfNotOriginal(new Quaternion(motionQ.x, -motionQ.y, -motionQ.z, motionQ.w));
            }
            #endregion

            #region BlendShape
            {
                var values = new Dictionary<SkinnedMeshRenderer, Dictionary<string, float>>();
                foreach (var boneIndex in selectionBones)
                {
                    var renderer = bones[boneIndex].GetComponent<SkinnedMeshRenderer>();
                    if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount == 0) continue;
                    blendShapeWeightSave.ActionOriginalWeights(renderer, (name, value) =>
                    {
                        Dictionary<string, string> nameTable;
                        if (mirrorBlendShape.TryGetValue(renderer, out nameTable))
                        {
                            string mirrorName;
                            if (nameTable.TryGetValue(name, out mirrorName))
                            {
                                if (!values.ContainsKey(renderer))
                                    values.Add(renderer, new Dictionary<string, float>());
                                values[renderer].Add(mirrorName, GetAnimationValueBlendShape(renderer, name));
                            }
                        }
                    });
                }
                foreach (var list in values)
                {
                    foreach (var pair in list.Value)
                    {
                        SetAnimationValueBlendShapeIfNotOriginal(list.Key, pair.Key, pair.Value);
                    }
                }
            }
            #endregion
        }
        public void SetPoseAfter(bool calcIK = false)
        {
            SetUpdateSampleAnimation(true);
            if (!calcIK)
            {
                SetSynchroIKtargetAll();
                updatePoseFixAnimation = true;
            }
            else
            {
                ResetSynchroIKtargetAll();
                updatePoseFixAnimation = false;
            }
        }

        private void ResetAllHaveAnimationCurve(PoseFlags flags = PoseFlags.All)
        {
            transformPoseSave.ResetOriginalTransform();
            blendShapeWeightSave.ResetOriginalWeight();

            #region Humanoid
            if (isHuman && (flags & PoseFlags.Humanoid) != 0)
            {
                SetAnimationValueAnimatorRootT(new Vector3(0, 1, 0));   //Always create
                SetAnimationValueAnimatorRootQ(Quaternion.identity);    //Always create
                for (int mi = 0; mi < HumanTrait.MuscleCount; mi++)
                {
                    SetAnimationValueAnimatorMuscleIfNotOriginal(mi, 0f);
                }
                for (var tdof = (AnimatorTDOFIndex)0; tdof < AnimatorTDOFIndex.Total; tdof++)
                {
                    SetAnimationValueAnimatorTDOFIfNotOriginal(tdof, Vector3.zero);
                }
            }
            #endregion

            #region Generic
            if ((flags & PoseFlags.Generic) != 0)
            {
                for (int bi = 0; bi < editBones.Length; bi++)
                {
                    if (isHuman && humanoidConflict[bi]) continue;
                    SetAnimationValueTransformPositionIfNotOriginal(bi, boneSaveTransforms[bi].localPosition);
                    SetAnimationValueTransformRotationIfNotOriginal(bi, boneSaveTransforms[bi].localRotation);
                    SetAnimationValueTransformScaleIfNotOriginal(bi, boneSaveTransforms[bi].localScale);
                }
            }
            #endregion

            #region BlendShape
            if ((flags & PoseFlags.BlendShape) != 0)
            {
                foreach (var renderer in vaw.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount <= 0) continue;
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    {
                        var name = renderer.sharedMesh.GetBlendShapeName(i);
                        SetAnimationValueBlendShapeIfNotOriginal(renderer, name, blendShapeWeightSave.GetDefaultWeight(renderer, name));
                    }
                }
            }
            #endregion
        }
        private void SetAllChangedAnimationCurve(PoseFlags flags = PoseFlags.All)
        {
            #region Humanoid
            if (isHuman && (flags & PoseFlags.Humanoid) != 0)
            {
                HumanPose hp = new HumanPose();
                GetEditGameObjectHumanPose(ref hp, EditObjectFlag.Base);
                SetAnimationValueAnimatorRootT(hp.bodyPosition);    //Always create
                SetAnimationValueAnimatorRootQIfNotOriginal(hp.bodyRotation);
                for (int i = 0; i < hp.muscles.Length; i++)
                {
                    SetAnimationValueAnimatorMuscleIfNotOriginal(i, hp.muscles[i]);
                }
            }
            #endregion

            #region Generic
            if ((flags & PoseFlags.Generic) != 0)
            {
                for (int i = 0; i < bones.Length; i++)
                {
                    if (isHuman && humanoidConflict[i]) continue;
                    var t = bones[i].transform;
                    SetAnimationValueTransformPositionIfNotOriginal(i, t.localPosition);
                    SetAnimationValueTransformRotationIfNotOriginal(i, t.localRotation);
                    SetAnimationValueTransformScaleIfNotOriginal(i, t.localScale);
                }
            }
            #endregion

            #region BlendShape
            if ((flags & PoseFlags.BlendShape) != 0)
            {
                foreach (var r in renderers)
                {
                    var renderer = r as SkinnedMeshRenderer;
                    if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount <= 0) continue;
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    {
                        var name = renderer.sharedMesh.GetBlendShapeName(i);
                        var weight = renderer.GetBlendShapeWeight(i);
                        SetAnimationValueBlendShapeIfNotOriginal(renderer, name, weight);
                    }
                }
            }
            #endregion
        }

        private bool AddRemoveInbetween(int plusFrame)
        {
            var bindings = AnimationUtility.GetCurveBindings(currentClip);
            var rbindings = AnimationUtility.GetObjectReferenceCurveBindings(currentClip);
            var events = AnimationUtility.GetAnimationEvents(currentClip);

            #region HasCheck
            if (plusFrame < 0)
            {
                bool hasCurrent = false;
                {
                    foreach (var binding in bindings)
                    {
                        var curve = GetEditorCurveCache(binding);
                        if (curve == null)
                            continue;
                        if (FindKeyframeAtTime(curve, currentTime) >= 0)
                        {
                            hasCurrent |= true;
                            break;
                        }
                    }
                    foreach (var rbinding in rbindings)
                    {
                        var keys = AnimationUtility.GetObjectReferenceCurve(currentClip, rbinding);
                        if (keys == null)
                            continue;
                        if (FindKeyframeAtTime(keys, currentTime) >= 0)
                        {
                            hasCurrent |= true;
                            break;
                        }
                    }
                    {
                        if (FindKeyframeAtTime(events, currentTime) >= 0)
                        {
                            hasCurrent |= true;
                        }
                    }
                }
                if (hasCurrent)
                {
                    var nextTime = uAw.SnapToFrame(currentTime + GetFrameTime(1), currentClip.frameRate);
                    foreach (var binding in bindings)
                    {
                        var curve = GetEditorCurveCache(binding);
                        if (curve == null)
                            continue;
                        if (FindKeyframeAtTime(curve, nextTime) >= 0)
                            return false;
                    }
                    foreach (var rbinding in rbindings)
                    {
                        var keys = AnimationUtility.GetObjectReferenceCurve(currentClip, rbinding);
                        if (keys == null)
                            continue;
                        if (FindKeyframeAtTime(keys, nextTime) >= 0)
                            return false;
                    }
                    if (events.Length > 0)
                    {
                        if (FindKeyframeAtTime(events, nextTime) >= 0)
                        {
                            return false;
                        }
                    }
                }
            }
            #endregion

            if (!BeginChangeAnimationCurve(currentClip, plusFrame > 0 ? "Add In between" : "Remove In between"))
                return false;

            #region MoveKeys
            var plusTime = GetFrameTime(plusFrame);
            {
                foreach (var binding in bindings)
                {
                    var curve = GetEditorCurveCache(binding);
                    if (curve == null)
                        continue;
                    var index = FindAfterNearKeyframeAtTime(curve, currentTime);
                    if (index < 0)
                        continue;
                    if (plusTime > 0f)
                    {
                        for (int i = curve.length - 1; i >= index; i--)
                        {
                            var key = curve[i];
                            key.time = uAw.SnapToFrame(key.time + plusTime, currentClip.frameRate);
                            curve.MoveKey(i, key);
                        }
                    }
                    else
                    {
                        for (int i = index; i < curve.length; i++)
                        {
                            var key = curve[i];
                            key.time = uAw.SnapToFrame(key.time + plusTime, currentClip.frameRate);
                            curve.MoveKey(i, key);
                        }
                    }
                    SetEditorCurveCache(binding, curve);
                }
                UpdateSyncEditorCurveClip();
            }
            {
                foreach (var rbinding in rbindings)
                {
                    var keys = AnimationUtility.GetObjectReferenceCurve(currentClip, rbinding);
                    if (keys == null)
                        continue;
                    var index = FindAfterNearKeyframeAtTime(keys, currentTime);
                    if (index < 0)
                        continue;
                    if (plusTime > 0f)
                    {
                        for (int i = keys.Length - 1; i >= index; i--)
                        {
                            var key = keys[i];
                            key.time = uAw.SnapToFrame(key.time + plusTime, currentClip.frameRate);
                            keys[i] = key;
                        }
                    }
                    else
                    {
                        for (int i = index; i < keys.Length; i++)
                        {
                            var key = keys[i];
                            key.time = uAw.SnapToFrame(key.time + plusTime, currentClip.frameRate);
                            keys[i] = key;
                        }
                    }
                    AnimationUtility.SetObjectReferenceCurve(currentClip, rbinding, keys);
                }
            }
            if (events.Length > 0)
            {
                var index = FindAfterNearKeyframeAtTime(events, currentTime);
                if (index >= 0)
                {
                    if (plusTime > 0f)
                    {
                        for (int i = events.Length - 1; i >= index; i--)
                        {
                            var ev = events[i];
                            ev.time = uAw.SnapToFrame(ev.time + plusTime, currentClip.frameRate);
                            events[i] = ev;
                        }
                    }
                    else
                    {
                        for (int i = index; i < events.Length; i++)
                        {
                            var ev = events[i];
                            ev.time = uAw.SnapToFrame(ev.time + plusTime, currentClip.frameRate);
                            events[i] = ev;
                        }
                    }
                    AnimationUtility.SetAnimationEvents(currentClip, events);
                }
            }
            #endregion

            SetUpdateSampleAnimation();
            uAw.ClearKeySelections();
            uAw.ForceRefresh();

            return true;
        }

        public List<float> GetHumanoidKeyframeTimeList(AnimationClip clip, HumanBodyBones humanoidIndex)
        {
            HashSet<float> keyTimes = new HashSet<float>();
            #region KeyTimes
            {
                Action<HumanBodyBones> AddKeyTimes = null;
                AddKeyTimes = (hi) =>
                {
                    for (int dofIndex = 0; dofIndex < 3; dofIndex++)
                    {
                        var mi = HumanTrait.MuscleFromBone((int)hi, dofIndex);
                        if (mi < 0) continue;
                        var curve = AnimationUtility.GetEditorCurve(clip, AnimationCurveBindingAnimatorMuscle(mi));
                        if (curve == null) continue;
                        for (int i = 0; i < curve.length; i++)
                            keyTimes.Add(curve[i].time);
                    }
                    if (humanoidHasTDoF && HumanBonesAnimatorTDOFIndex[(int)hi] != null)
                    {
                        var tdofIndex = HumanBonesAnimatorTDOFIndex[(int)hi].index;
                        for (int dof = 0; dof < 3; dof++)
                        {
                            var curve = AnimationUtility.GetEditorCurve(clip, AnimationCurveBindingAnimatorTDOF(tdofIndex, dof));
                            if (curve == null)
                                continue;
                            for (int i = 0; i < curve.length; i++)
                                keyTimes.Add(curve[i].time);
                        }
                    }
                    var phi = (HumanBodyBones)HumanTrait.GetParentBone((int)hi);
                    if (phi >= 0)
                    {
                        AddKeyTimes(phi);
                    }
                };
                keyTimes.Add(0f);
                keyTimes.Add(clip.length);
                AddKeyTimes(humanoidIndex);
            }
            #endregion
            var list = keyTimes.ToList();
            list.Sort();
            return list;
        }

        public bool IsHaveAnimationCurveAnimatorRootT()
        {
            if (currentClip == null)
                return false;
            for (int i = 0; i < 3; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorRootT[i]);
                if (curve != null)
                    return true;
            }
            return false;
        }
        public Vector3 GetAnimationValueAnimatorRootT(float time = -1f)
        {
            if (currentClip == null)
                return Vector3.zero;
            time = GetFrameSnapTime(time);
            Vector3 result = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorRootT[i]);
                if (curve != null)
                {
                    result[i] = curve.Evaluate(time);
                }
            }
            return result;
        }
        public void SetAnimationValueAnimatorRootTIfNotOriginal(Vector3 value3, float time = -1f)
        {
            if (IsHaveAnimationCurveAnimatorRootT() ||
                !Mathf.Approximately(value3.x, 0f) ||
                !Mathf.Approximately(value3.y, 0f) ||
                !Mathf.Approximately(value3.z, 0f))
            {
                SetAnimationValueAnimatorRootT(value3, time);
            }
        }
        public void SetAnimationValueAnimatorRootT(Vector3 value3, float time = -1f)
        {
            if (!BeginChangeAnimationCurve(currentClip, "Change RootT"))
                return;
            time = GetFrameSnapTime(time);
            for (int i = 0; i < 3; i++)
            {
                var curve = GetAnimationCurveAnimatorRootT(i);
                SetKeyframe(curve, time, value3[i]);
                SetAnimationCurveAnimatorRootT(i, curve);
            }
        }
        public AnimationCurve GetAnimationCurveAnimatorRootT(int dof, bool notNull = true)
        {
            var binding = AnimationCurveBindingAnimatorRootT[dof];
            var curve = GetEditorCurveCache(binding);
            if (curve == null && notNull)
            {
                var defaultValue = isHuman ? new Vector3(0f, 1f, 0f) : Vector3.zero;
                curve = new AnimationCurve();
                AddKeyframe(curve, 0f, defaultValue[dof]);
                AddKeyframe(curve, currentClip.length, defaultValue[dof]);
                //Created
                uAw.ForceRefresh();
                SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { binding });
            }
            return curve;
        }
        public void SetAnimationCurveAnimatorRootT(int dof, AnimationCurve curve)
        {
            if (BeginChangeAnimationCurve(currentClip, "Change RootT"))
            {
                SetEditorCurveCache(AnimationCurveBindingAnimatorRootT[dof], curve);
            }
        }

        public bool IsHaveAnimationCurveAnimatorRootQ()
        {
            if (currentClip == null)
                return false;
            for (int i = 0; i < 4; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorRootQ[i]);
                if (curve != null)
                    return true;
            }
            return false;
        }
        public Quaternion GetAnimationValueAnimatorRootQ(float time = -1f)
        {
            if (currentClip == null)
                return Quaternion.identity;
            time = GetFrameSnapTime(time);
            Vector4 result = new Vector4(0, 0, 0, 1);
            for (int i = 0; i < 4; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorRootQ[i]);
                if (curve != null)
                {
                    result[i] = curve.Evaluate(time);
                }
            }
            result.Normalize();
            if (result.sqrMagnitude > 0f)
            {
                return new Quaternion(result.x, result.y, result.z, result.w);
            }
            else
            {
                return Quaternion.identity;
            }
        }
        public void SetAnimationValueAnimatorRootQIfNotOriginal(Quaternion rotation, float time = -1f)
        {
            if (IsHaveAnimationCurveAnimatorRootQ() ||
                !Mathf.Approximately(rotation.x, 0f) ||
                !Mathf.Approximately(rotation.y, 0f) ||
                !Mathf.Approximately(rotation.z, 0f) ||
                !Mathf.Approximately(rotation.w, 1f))
            {
                SetAnimationValueAnimatorRootQ(rotation, time);
            }
        }
        public void SetAnimationValueAnimatorRootQ(Quaternion rotation, float time = -1f)
        {
            if (!BeginChangeAnimationCurve(currentClip, "Change RootQ"))
                return;
            time = GetFrameSnapTime(time);
            tmpCurves.Clear();
            for (int i = 0; i < 4; i++)
            {
                tmpCurves.curves[i] = GetAnimationCurveAnimatorRootQ(i);
            }
            rotation = FixReverseRotationQuaternion(tmpCurves.curves, time, rotation);
            for (int i = 0; i < 4; i++)
            {
                SetKeyframe(tmpCurves.curves[i], time, rotation[i]);
                SetAnimationCurveAnimatorRootQ(i, tmpCurves.curves[i]);
            }
            tmpCurves.Clear();
        }
        public AnimationCurve GetAnimationCurveAnimatorRootQ(int dof, bool notNull = true)
        {
            var binding = AnimationCurveBindingAnimatorRootQ[dof];
            var curve = GetEditorCurveCache(binding);
            if (curve == null && notNull)
            {
                var defaultValue = Quaternion.identity;
                curve = new AnimationCurve();
                AddKeyframe(curve, 0f, defaultValue[dof]);
                AddKeyframe(curve, currentClip.length, defaultValue[dof]);
                //Created
                uAw.ForceRefresh();
                SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { binding });
            }
            return curve;
        }
        public void SetAnimationCurveAnimatorRootQ(int dof, AnimationCurve curve)
        {
            if (BeginChangeAnimationCurve(currentClip, "Change RootQ"))
            {
                SetEditorCurveCache(AnimationCurveBindingAnimatorRootQ[dof], curve);
            }
        }

        public bool IsHaveAnimationCurveAnimatorMotionT()
        {
            if (currentClip == null)
                return false;
            for (int i = 0; i < 3; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorMotionT[i]);
                if (curve != null)
                    return true;
            }
            return false;
        }
        public Vector3 GetAnimationValueAnimatorMotionT(float time = -1f)
        {
            if (currentClip == null)
                return Vector3.zero;
            time = GetFrameSnapTime(time);
            Vector3 result = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorMotionT[i]);
                if (curve != null)
                {
                    result[i] = curve.Evaluate(time);
                }
            }
            return result;
        }
        public void SetAnimationValueAnimatorMotionTIfNotOriginal(Vector3 value3, float time = -1f)
        {
            if (IsHaveAnimationCurveAnimatorMotionT() ||
                !Mathf.Approximately(value3.x, 0f) ||
                !Mathf.Approximately(value3.y, 0f) ||
                !Mathf.Approximately(value3.z, 0f))
            {
                SetAnimationValueAnimatorMotionT(value3, time);
            }
        }
        public void SetAnimationValueAnimatorMotionT(Vector3 value3, float time = -1f)
        {
            if (!BeginChangeAnimationCurve(currentClip, "Change MotionT"))
                return;
            time = GetFrameSnapTime(time);
            for (int i = 0; i < 3; i++)
            {
                var curve = GetAnimationCurveAnimatorMotionT(i);
                SetKeyframe(curve, time, value3[i]);
                SetAnimationCurveAnimatorMotionT(i, curve);
            }
        }
        public AnimationCurve GetAnimationCurveAnimatorMotionT(int dof, bool notNull = true)
        {
            var binding = AnimationCurveBindingAnimatorMotionT[dof];
            var curve = GetEditorCurveCache(binding);
            if (curve == null && notNull)
            {
                var defaultValue = Vector3.zero;
                curve = new AnimationCurve();
                AddKeyframe(curve, 0f, defaultValue[dof]);
                AddKeyframe(curve, currentClip.length, defaultValue[dof]);
                //Created
                uAw.ForceRefresh();
                SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { binding });
            }
            return curve;
        }
        public void SetAnimationCurveAnimatorMotionT(int dof, AnimationCurve curve)
        {
            if (BeginChangeAnimationCurve(currentClip, "Change MotionT"))
            {
                SetEditorCurveCache(AnimationCurveBindingAnimatorMotionT[dof], curve);
            }
        }

        public bool IsHaveAnimationCurveAnimatorMotionQ()
        {
            if (currentClip == null)
                return false;
            for (int i = 0; i < 4; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorMotionQ[i]);
                if (curve != null)
                    return true;
            }
            return false;
        }
        public Quaternion GetAnimationValueAnimatorMotionQ(float time = -1f)
        {
            if (currentClip == null)
                return Quaternion.identity;
            time = GetFrameSnapTime(time);
            Vector4 result = new Vector4(0, 0, 0, 1);
            for (int i = 0; i < 4; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorMotionQ[i]);
                if (curve != null)
                {
                    result[i] = curve.Evaluate(time);
                }
            }
            result.Normalize();
            if (result.sqrMagnitude > 0f)
            {
                return new Quaternion(result.x, result.y, result.z, result.w);
            }
            else
            {
                return Quaternion.identity;
            }
        }
        public void SetAnimationValueAnimatorMotionQIfNotOriginal(Quaternion rotation, float time = -1f)
        {
            if (IsHaveAnimationCurveAnimatorMotionQ() ||
                !Mathf.Approximately(rotation.x, 0f) ||
                !Mathf.Approximately(rotation.y, 0f) ||
                !Mathf.Approximately(rotation.z, 0f) ||
                !Mathf.Approximately(rotation.w, 1f))
            {
                SetAnimationValueAnimatorMotionQ(rotation, time);
            }
        }
        public void SetAnimationValueAnimatorMotionQ(Quaternion rotation, float time = -1f)
        {
            if (!BeginChangeAnimationCurve(currentClip, "Change MotionQ"))
                return;
            time = GetFrameSnapTime(time);
            tmpCurves.Clear();
            for (int i = 0; i < 4; i++)
            {
                tmpCurves.curves[i] = GetAnimationCurveAnimatorMotionQ(i);
            }
            rotation = FixReverseRotationQuaternion(tmpCurves.curves, time, rotation);
            for (int i = 0; i < 4; i++)
            {
                SetKeyframe(tmpCurves.curves[i], time, rotation[i]);
                SetAnimationCurveAnimatorMotionQ(i, tmpCurves.curves[i]);
            }
            tmpCurves.Clear();
        }
        public AnimationCurve GetAnimationCurveAnimatorMotionQ(int dof, bool notNull = true)
        {
            var binding = AnimationCurveBindingAnimatorMotionQ[dof];
            var curve = GetEditorCurveCache(binding);
            if (curve == null && notNull)
            {
                var defaultValue = Quaternion.identity;
                curve = new AnimationCurve();
                AddKeyframe(curve, 0f, defaultValue[dof]);
                AddKeyframe(curve, currentClip.length, defaultValue[dof]);
                //Created
                uAw.ForceRefresh();
                SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { binding });
            }
            return curve;
        }
        public void SetAnimationCurveAnimatorMotionQ(int dof, AnimationCurve curve)
        {
            if (BeginChangeAnimationCurve(currentClip, "Change MotionQ"))
            {
                SetEditorCurveCache(AnimationCurveBindingAnimatorMotionQ[dof], curve);
            }
        }

        public bool IsHaveAnimationCurveAnimatorIkT(AnimatorIKIndex ikIndex)
        {
            if (currentClip == null)
                return false;
            for (int i = 0; i < 3; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorIkT(ikIndex, i));
                if (curve != null)
                    return true;
            }
            return false;
        }
        public Vector3 GetAnimationValueAnimatorIkT(AnimatorIKIndex ikIndex, float time = -1f)
        {
            if (currentClip == null)
                return Vector3.zero;
            time = GetFrameSnapTime(time);
            Vector3 result = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorIkT(ikIndex, i));
                if (curve != null)
                {
                    result[i] = curve.Evaluate(time);
                }
            }
            return result;
        }
        public void SetAnimationValueAnimatorIkTIfNotOriginal(AnimatorIKIndex ikIndex, Vector3 value3, float time = -1f)
        {
            if (IsHaveAnimationCurveAnimatorIkT(ikIndex) ||
                !Mathf.Approximately(value3.x, 0f) ||
                !Mathf.Approximately(value3.y, 0f) ||
                !Mathf.Approximately(value3.z, 0f))
            {
                SetAnimationValueAnimatorIkT(ikIndex, value3, time);
            }
        }
        public void SetAnimationValueAnimatorIkT(AnimatorIKIndex ikIndex, Vector3 value3, float time = -1f)
        {
            if (!BeginChangeAnimationCurve(currentClip, "Change IK T"))
                return;
            time = GetFrameSnapTime(time);
            for (int i = 0; i < 3; i++)
            {
                var curve = GetAnimationCurveAnimatorIkT(ikIndex, i);
                SetKeyframe(curve, time, value3[i]);
                SetAnimationCurveAnimatorIkT(ikIndex, i, curve);
            }
        }
        public AnimationCurve GetAnimationCurveAnimatorIkT(AnimatorIKIndex ikIndex, int dof, bool notNull = true)
        {
            var binding = AnimationCurveBindingAnimatorIkT(ikIndex, dof);
            var curve = GetEditorCurveCache(binding);
            if (curve == null && notNull)
            {
                var defaultValue = Vector3.zero;
                curve = new AnimationCurve();
                AddKeyframe(curve, 0f, defaultValue[dof]);
                AddKeyframe(curve, currentClip.length, defaultValue[dof]);
            }
            return curve;
        }
        public void SetAnimationCurveAnimatorIkT(AnimatorIKIndex ikIndex, int dof, AnimationCurve curve)
        {
            if (BeginChangeAnimationCurve(currentClip, "Change IK T"))
            {
                SetEditorCurveCache(AnimationCurveBindingAnimatorIkT(ikIndex, dof), curve);
            }
        }

        public bool IsHaveAnimationCurveAnimatorIkQ(AnimatorIKIndex ikIndex)
        {
            if (currentClip == null)
                return false;
            for (int i = 0; i < 4; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorIkQ(ikIndex, i));
                if (curve != null)
                    return true;
            }
            return false;
        }
        public Quaternion GetAnimationValueAnimatorIkQ(AnimatorIKIndex ikIndex, float time = -1f)
        {
            if (currentClip == null)
                return Quaternion.identity;
            time = GetFrameSnapTime(time);
            Vector4 result = new Vector4(0, 0, 0, 1);
            for (int i = 0; i < 4; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorIkQ(ikIndex, i));
                if (curve != null)
                {
                    result[i] = curve.Evaluate(time);
                }
            }
            result.Normalize();
            if (result.sqrMagnitude > 0f)
            {
                return new Quaternion(result.x, result.y, result.z, result.w);
            }
            else
            {
                return Quaternion.identity;
            }
        }
        public void SetAnimationValueAnimatorIkQIfNotOriginal(AnimatorIKIndex ikIndex, Quaternion rotation, float time = -1f)
        {
            if (IsHaveAnimationCurveAnimatorIkQ(ikIndex) ||
                !Mathf.Approximately(rotation.x, 0f) ||
                !Mathf.Approximately(rotation.y, 0f) ||
                !Mathf.Approximately(rotation.z, 0f) ||
                !Mathf.Approximately(rotation.w, 1f))
            {
                SetAnimationValueAnimatorIkQ(ikIndex, rotation, time);
            }
        }
        public void SetAnimationValueAnimatorIkQ(AnimatorIKIndex ikIndex, Quaternion rotation, float time = -1f)
        {
            if (!BeginChangeAnimationCurve(currentClip, "Change IK Q"))
                return;
            time = GetFrameSnapTime(time);
            tmpCurves.Clear();
            for (int i = 0; i < 4; i++)
            {
                tmpCurves.curves[i] = GetAnimationCurveAnimatorIkQ(ikIndex, i);
            }
            rotation = FixReverseRotationQuaternion(tmpCurves.curves, time, rotation);
            for (int i = 0; i < 4; i++)
            {
                SetKeyframe(tmpCurves.curves[i], time, rotation[i]);
                SetAnimationCurveAnimatorIkQ(ikIndex, i, tmpCurves.curves[i]);
            }
            tmpCurves.Clear();
        }
        public AnimationCurve GetAnimationCurveAnimatorIkQ(AnimatorIKIndex ikIndex, int dof, bool notNull = true)
        {
            var binding = AnimationCurveBindingAnimatorIkQ(ikIndex, dof);
            var curve = GetEditorCurveCache(binding);
            if (curve == null && notNull)
            {
                var defaultValue = Quaternion.identity;
                curve = new AnimationCurve();
                AddKeyframe(curve, 0f, defaultValue[dof]);
                AddKeyframe(curve, currentClip.length, defaultValue[dof]);
            }
            return curve;
        }
        public void SetAnimationCurveAnimatorIkQ(AnimatorIKIndex ikIndex, int dof, AnimationCurve curve)
        {
            if (BeginChangeAnimationCurve(currentClip, "Change IK Q"))
            {
                SetEditorCurveCache(AnimationCurveBindingAnimatorIkQ(ikIndex, dof), curve);
            }
        }

        public bool IsHaveAnimationCurveAnimatorTDOF(AnimatorTDOFIndex tdofIndex)
        {
            if (currentClip == null)
                return false;
            for (int i = 0; i < 3; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorTDOF(tdofIndex, i));
                if (curve != null)
                    return true;
            }
            return false;
        }
        public Vector3 GetAnimationValueAnimatorTDOF(AnimatorTDOFIndex tdofIndex, float time = -1f)
        {
            if (currentClip == null)
                return Vector3.zero;
            time = GetFrameSnapTime(time);
            Vector3 result = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorTDOF(tdofIndex, i));
                if (curve != null)
                {
                    result[i] = curve.Evaluate(time);
                }
            }
            return result;
        }
        public void SetAnimationValueAnimatorTDOFIfNotOriginal(AnimatorTDOFIndex tdofIndex, Vector3 value3, float time = -1f)
        {
            if (IsHaveAnimationCurveAnimatorTDOF(tdofIndex) ||
                !Mathf.Approximately(value3.x, 0f) ||
                !Mathf.Approximately(value3.y, 0f) ||
                !Mathf.Approximately(value3.z, 0f))
            {
                SetAnimationValueAnimatorTDOF(tdofIndex, value3, time);
            }
        }
        public void SetAnimationValueAnimatorTDOF(AnimatorTDOFIndex tdofIndex, Vector3 value3, float time = -1f)
        {
            if (!BeginChangeAnimationCurve(currentClip, "Change TDOF"))
                return;
            time = GetFrameSnapTime(time);
            for (int dof = 0; dof < 3; dof++)
            {
                var curve = GetAnimationCurveAnimatorTDOF(tdofIndex, dof);
                SetKeyframe(curve, time, value3[dof]);
                SetAnimationCurveAnimatorTDOF(tdofIndex, dof, curve);
            }
        }
        public AnimationCurve GetAnimationCurveAnimatorTDOF(AnimatorTDOFIndex tdofIndex, int dof, bool notNull = true)
        {
            var binding = AnimationCurveBindingAnimatorTDOF(tdofIndex, dof);
            var curve = GetEditorCurveCache(binding);
            if (curve == null && notNull)
            {
                var defaultValue = Vector3.zero;
                curve = new AnimationCurve();
                AddKeyframe(curve, 0f, defaultValue[dof]);
                AddKeyframe(curve, currentClip.length, defaultValue[dof]);
                //Created
                uAw.ForceRefresh();
                SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { binding });
            }
            return curve;
        }
        public void SetAnimationCurveAnimatorTDOF(AnimatorTDOFIndex tdofIndex, int dof, AnimationCurve curve)
        {
            if (BeginChangeAnimationCurve(currentClip, "Change TDOF"))
            {
                SetEditorCurveCache(AnimationCurveBindingAnimatorTDOF(tdofIndex, dof), curve);
            }
        }

        public bool IsHaveAnimationCurveAnimatorMuscle(int muscleIndex)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
                return false;
            if (currentClip == null)
                return false;
            return GetEditorCurveCache(AnimationCurveBindingAnimatorMuscle(muscleIndex)) != null;
        }
        public float GetAnimationValueAnimatorMuscle(int muscleIndex, float time = -1f)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
                return 0f;
            if (currentClip == null)
                return 0f;
            time = GetFrameSnapTime(time);
            var curve = GetEditorCurveCache(AnimationCurveBindingAnimatorMuscle(muscleIndex));
            if (curve == null) return 0f;
            return curve.Evaluate(time);
        }
        public void SetAnimationValueAnimatorMuscleIfNotOriginal(int muscleIndex, float value, float time = -1f)
        {
            if (IsHaveAnimationCurveAnimatorMuscle(muscleIndex) ||
                !Mathf.Approximately(value, 0f))
            {
                SetAnimationValueAnimatorMuscle(muscleIndex, value, time);
            }
        }
        public void SetAnimationValueAnimatorMuscle(int muscleIndex, float value, float time = -1f)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
                return;
            if (!BeginChangeAnimationCurve(currentClip, "Change Muscle"))
                return;
            time = GetFrameSnapTime(time);
            {
                var curve = GetAnimationCurveAnimatorMuscle(muscleIndex);
                SetKeyframe(curve, time, value);
                SetAnimationCurveAnimatorMuscle(muscleIndex, curve);
            }
        }
        public AnimationCurve GetAnimationCurveAnimatorMuscle(int muscleIndex, bool notNull = true)
        {
            var binding = AnimationCurveBindingAnimatorMuscle(muscleIndex);
            var curve = GetEditorCurveCache(binding);
            if (curve == null && notNull)
            {
                curve = new AnimationCurve();
                AddKeyframe(curve, 0f, 0f);
                AddKeyframe(curve, currentClip.length, 0f);
                //Created
                uAw.ForceRefresh();
                SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { binding });
            }
            return curve;
        }
        public void SetAnimationCurveAnimatorMuscle(int muscleIndex, AnimationCurve curve)
        {
            if (BeginChangeAnimationCurve(currentClip, "Change Muscle"))
            {
                SetEditorCurveCache(AnimationCurveBindingAnimatorMuscle(muscleIndex), curve);
            }
        }

        private const float TransformPositionApproximatelyThreshold = 0.001f;
        public bool IsHaveAnimationCurveTransformPosition(int boneIndex)
        {
            if (currentClip == null || boneIndex < 0 || boneIndex >= editBones.Length)
                return false;
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingTransformPosition(boneIndex, 0));
                if (curve != null)
                    return true;
            }
            return false;
        }
        public Vector3 GetAnimationValueTransformPosition(int boneIndex, float time = -1f)
        {
            if (currentClip == null || boneIndex < 0 || boneIndex >= editBones.Length)
                return boneSaveOriginalTransforms[boneIndex].localPosition;
            time = GetFrameSnapTime(time);
            Vector3 result = boneSaveOriginalTransforms[boneIndex].localPosition;
            for (int i = 0; i < 3; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingTransformPosition(boneIndex, i));
                if (curve != null)
                {
                    result[i] = curve.Evaluate(time);
                }
            }
            return result;
        }
        public void SetAnimationValueTransformPositionIfNotOriginal(int boneIndex, Vector3 position, float time = -1f)
        {
            if (IsHaveAnimationCurveTransformPosition(boneIndex) ||
                Mathf.Abs(position.x - boneSaveOriginalTransforms[boneIndex].localPosition.x) >= TransformPositionApproximatelyThreshold ||
                Mathf.Abs(position.y - boneSaveOriginalTransforms[boneIndex].localPosition.y) >= TransformPositionApproximatelyThreshold ||
                Mathf.Abs(position.z - boneSaveOriginalTransforms[boneIndex].localPosition.z) >= TransformPositionApproximatelyThreshold)
            {
                SetAnimationValueTransformPosition(boneIndex, position, time);
            }
        }
        public void SetAnimationValueTransformPosition(int boneIndex, Vector3 position, float time = -1f)
        {
            if (boneIndex < 0 || boneIndex >= editBones.Length)
                return;
            if (!BeginChangeAnimationCurve(currentClip, "Change Transform Position"))
                return;
            time = GetFrameSnapTime(time);
            bool removeCurve = false;
            if (isHuman && humanoidConflict[boneIndex])
            {
                EditorCommon.ShowNotification("Conflict");
                Debug.LogErrorFormat(Language.GetText(Language.Help.LogGenericCurveHumanoidConflictError), editBones[boneIndex].name);
                removeCurve = true;
            }
            else if (rootMotionBoneIndex >= 0 && boneIndex == 0)
            {
                EditorCommon.ShowNotification("Conflict");
                Debug.LogErrorFormat(Language.GetText(Language.Help.LogGenericCurveRootConflictError), editBones[boneIndex].name);
                removeCurve = true;
            }
            if (removeCurve)
            {
                for (int i = 0; i < 3; i++)
                {
                    SetEditorCurveCache(AnimationCurveBindingTransformPosition(boneIndex, i), null);
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    var curve = GetAnimationCurveTransformPosition(boneIndex, i);
                    SetKeyframe(curve, time, position[i]);
                    SetAnimationCurveTransformPosition(boneIndex, i, curve);
                }
            }
        }
        public AnimationCurve GetAnimationCurveTransformPosition(int boneIndex, int dof, bool notNull = true)
        {
            var binding = AnimationCurveBindingTransformPosition(boneIndex, dof);
            var curve = GetEditorCurveCache(binding);
            if (curve == null && notNull)
            {
                var defaultValue = boneSaveOriginalTransforms[boneIndex].localPosition;
                curve = new AnimationCurve();
                AddKeyframe(curve, 0f, defaultValue[dof]);
                AddKeyframe(curve, currentClip.length, defaultValue[dof]);
                //Created
                uAw.ForceRefresh();
                SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { binding });
            }
            return curve;
        }
        public void SetAnimationCurveTransformPosition(int boneIndex, int dof, AnimationCurve curve)
        {
            if (BeginChangeAnimationCurve(currentClip, "Change Transform Position"))
            {
                SetEditorCurveCache(AnimationCurveBindingTransformPosition(boneIndex, dof), curve);
            }
        }

        private const float TransformRotationApproximatelyThreshold = 0.01f;
        public URotationCurveInterpolation.Mode IsHaveAnimationCurveTransformRotation(int boneIndex)
        {
            if (currentClip == null || boneIndex < 0 || boneIndex >= editBones.Length)
                return URotationCurveInterpolation.Mode.Undefined;

            if (GetEditorCurveCache(AnimationCurveBindingTransformRotation(boneIndex, 0, URotationCurveInterpolation.Mode.RawQuaternions)) != null)
                return URotationCurveInterpolation.Mode.RawQuaternions;

            if (GetEditorCurveCache(AnimationCurveBindingTransformRotation(boneIndex, 0, URotationCurveInterpolation.Mode.RawEuler)) != null)
                return URotationCurveInterpolation.Mode.RawEuler;

            return URotationCurveInterpolation.Mode.Undefined;
        }
        public Quaternion GetAnimationValueTransformRotation(int boneIndex, float time = -1f)
        {
            if (currentClip == null || boneIndex < 0 || boneIndex >= editBones.Length)
                return boneSaveOriginalTransforms[boneIndex].localRotation;
            time = GetFrameSnapTime(time);
            var binding = AnimationCurveBindingTransformRotation(boneIndex, 0, URotationCurveInterpolation.Mode.RawQuaternions);
            var curve = GetEditorCurveCache(binding);
            if (curve != null)
            {
                #region RawQuaternions
                Vector4 result = Vector4.zero;
                result[0] = curve.Evaluate(time);
                for (int i = 1; i < 4; i++)
                {
                    binding = AnimationCurveBindingTransformRotation(boneIndex, i, URotationCurveInterpolation.Mode.RawQuaternions);
                    curve = GetEditorCurveCache(binding);
                    if (curve != null)
                        result[i] = curve.Evaluate(time);
                }
                result.Normalize();
                if (result.sqrMagnitude <= 0f)
                    return boneSaveOriginalTransforms[boneIndex].localRotation;
                Quaternion resultQ = Quaternion.identity;
                for (int i = 0; i < 4; i++)
                    resultQ[i] = result[i];
                return resultQ;
                #endregion
            }
            else
            {
                binding = AnimationCurveBindingTransformRotation(boneIndex, 0, URotationCurveInterpolation.Mode.RawEuler);
                curve = GetEditorCurveCache(binding);
                if (curve != null)
                {
                    #region RawEuler
                    Vector3 result = Vector3.zero;
                    for (int i = 0; i < 3; i++)
                    {
                        binding = AnimationCurveBindingTransformRotation(boneIndex, i, URotationCurveInterpolation.Mode.RawEuler);
                        curve = GetEditorCurveCache(binding);
                        if (curve != null)
                            result[i] = curve.Evaluate(time);
                    }
                    return Quaternion.Euler(result);
                }
                #endregion
            }
            return boneSaveOriginalTransforms[boneIndex].localRotation;
        }
        public void SetAnimationValueTransformRotationIfNotOriginal(int boneIndex, Quaternion rotation, float time = -1f)
        {
            var eulerAngles = rotation.eulerAngles;
            var originalEulerAngles = boneSaveOriginalTransforms[boneIndex].localRotation.eulerAngles;
            if (IsHaveAnimationCurveTransformRotation(boneIndex) != URotationCurveInterpolation.Mode.Undefined ||
                Mathf.Abs(eulerAngles.x - originalEulerAngles.x) >= TransformRotationApproximatelyThreshold ||
                Mathf.Abs(eulerAngles.y - originalEulerAngles.y) >= TransformRotationApproximatelyThreshold ||
                Mathf.Abs(eulerAngles.z - originalEulerAngles.z) >= TransformRotationApproximatelyThreshold)
            {
                SetAnimationValueTransformRotation(boneIndex, rotation, time);
            }
        }
        public void SetAnimationValueTransformRotation(int boneIndex, Quaternion rotation, float time = -1f)
        {
            if (boneIndex < 0 || boneIndex >= editBones.Length)
                return;
            if (!BeginChangeAnimationCurve(currentClip, "Change Transform Rotation"))
                return;
            time = GetFrameSnapTime(time);
            bool removeCurve = false;
            if (isHuman && humanoidConflict[boneIndex])
            {
                EditorCommon.ShowNotification("Conflict");
                Debug.LogErrorFormat(Language.GetText(Language.Help.LogGenericCurveHumanoidConflictError), editBones[boneIndex].name);
                removeCurve = true;
            }
            else if (rootMotionBoneIndex >= 0 && boneIndex == 0)
            {
                EditorCommon.ShowNotification("Conflict");
                Debug.LogErrorFormat(Language.GetText(Language.Help.LogGenericCurveRootConflictError), editBones[boneIndex].name);
                removeCurve = true;
            }
            var mode = IsHaveAnimationCurveTransformRotation(boneIndex);
            if (removeCurve)
            {
                if (mode == URotationCurveInterpolation.Mode.RawQuaternions)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        SetEditorCurveCache(AnimationCurveBindingTransformRotation(boneIndex, i, URotationCurveInterpolation.Mode.RawQuaternions), null);
                    }
                }
                else
                {
                    for (int i = 0; i < 3; i++)
                    {
                        SetEditorCurveCache(AnimationCurveBindingTransformRotation(boneIndex, i, URotationCurveInterpolation.Mode.RawEuler), null);
                    }
                }
            }
            else
            {
                if (mode == URotationCurveInterpolation.Mode.Undefined)
                    mode = URotationCurveInterpolation.Mode.RawQuaternions;
                tmpCurves.Clear();
                if (mode == URotationCurveInterpolation.Mode.RawQuaternions)
                {
                    #region RawQuaternions
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            tmpCurves.curves[i] = GetAnimationCurveTransformRotation(boneIndex, i, mode);
                        }
                        rotation = FixReverseRotationQuaternion(tmpCurves.curves, time, rotation);
                        for (int i = 0; i < 4; i++)
                        {
                            float value = rotation[i];
                            SetKeyframe(tmpCurves.curves[i], time, value);
                            #region ErrorAvoidance  
                            {
                                //There must be at least two keyframes. If not, an error will occur.[AnimationUtility.GetEditorCurve]
                                while (tmpCurves.curves[i].length < 2)
                                {
                                    var addTime = 0f;
                                    if (time != 0f) addTime = 0f;
                                    else if (currentClip.length != 0f) addTime = currentClip.length;
                                    else addTime = 1f;
                                    AddKeyframe(tmpCurves.curves[i], addTime, tmpCurves.curves[i].Evaluate(addTime));
                                }
                            }
                            #endregion
                            SetAnimationCurveTransformRotation(boneIndex, i, mode, tmpCurves.curves[i]);
                        }
                    }
                    #endregion
                }
                else
                {
                    #region RawEuler
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            tmpCurves.curves[i] = GetAnimationCurveTransformRotation(boneIndex, i, mode);
                        }
                        var eulerAngles = FixReverseRotationEuler(tmpCurves.curves, time, rotation.eulerAngles);
                        for (int i = 0; i < 3; i++)
                        {
                            var value = eulerAngles[i];
                            SetKeyframe(tmpCurves.curves[i], time, value);
                            SetAnimationCurveTransformRotation(boneIndex, i, mode, tmpCurves.curves[i]);
                        }
                    }
                    #endregion
                }
            }
            tmpCurves.Clear();
        }
        public AnimationCurve GetAnimationCurveTransformRotation(int boneIndex, int dof, URotationCurveInterpolation.Mode mode, bool notNull = true)
        {
            var binding = AnimationCurveBindingTransformRotation(boneIndex, dof, mode);
            var curve = GetEditorCurveCache(binding);
            if (curve == null && notNull)
            {
                curve = new AnimationCurve();
                if (mode == URotationCurveInterpolation.Mode.RawQuaternions)
                {
                    var defaultValue = boneSaveOriginalTransforms[boneIndex].localRotation;
                    AddKeyframe(curve, 0f, defaultValue[dof]);
                    AddKeyframe(curve, currentClip.length, defaultValue[dof]);
                }
                else
                {
                    var defaultValue = boneSaveOriginalTransforms[boneIndex].localRotation.eulerAngles;
                    AddKeyframe(curve, 0f, defaultValue[dof]);
                    AddKeyframe(curve, currentClip.length, defaultValue[dof]);
                }
                //Created
                uAw.ForceRefresh();
                SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { binding });
            }
            return curve;
        }
        public void SetAnimationCurveTransformRotation(int boneIndex, int dof, URotationCurveInterpolation.Mode mode, AnimationCurve curve)
        {
            if (BeginChangeAnimationCurve(currentClip, "Change Transform Rotation"))
            {
                SetEditorCurveCache(AnimationCurveBindingTransformRotation(boneIndex, dof, mode), curve);
            }
        }

        private const float TransformScaleApproximatelyThreshold = 0.1f;   //There is an error only on the scale, so roughly check it.
        public bool IsHaveAnimationCurveTransformScale(int boneIndex)
        {
            if (currentClip == null || boneIndex < 0 || boneIndex >= editBones.Length)
                return false;
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingTransformScale(boneIndex, 0));
                if (curve != null)
                    return true;
            }
            return false;
        }
        public Vector3 GetAnimationValueTransformScale(int boneIndex, float time = -1f)
        {
            if (currentClip == null || boneIndex < 0 || boneIndex >= editBones.Length)
                return boneSaveOriginalTransforms[boneIndex].localScale;
            time = GetFrameSnapTime(time);
            Vector3 result = boneSaveOriginalTransforms[boneIndex].localScale;
            for (int i = 0; i < 3; i++)
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingTransformScale(boneIndex, i));
                if (curve != null)
                {
                    result[i] = curve.Evaluate(time);
                }
            }
            return result;
        }
        public void SetAnimationValueTransformScaleIfNotOriginal(int boneIndex, Vector3 scale, float time = -1f)
        {
            if (IsHaveAnimationCurveTransformScale(boneIndex) ||
                Mathf.Abs(scale.x - boneSaveOriginalTransforms[boneIndex].localScale.x) >= TransformScaleApproximatelyThreshold ||
                Mathf.Abs(scale.y - boneSaveOriginalTransforms[boneIndex].localScale.y) >= TransformScaleApproximatelyThreshold ||
                Mathf.Abs(scale.z - boneSaveOriginalTransforms[boneIndex].localScale.z) >= TransformScaleApproximatelyThreshold)
            {
                SetAnimationValueTransformScale(boneIndex, scale, time);
            }
        }
        public void SetAnimationValueTransformScale(int boneIndex, Vector3 scale, float time = -1f)
        {
            if (boneIndex < 0 || boneIndex >= editBones.Length)
                return;
            if (!BeginChangeAnimationCurve(currentClip, "Change Transform Scale"))
                return;
            time = GetFrameSnapTime(time);
            bool removeCurve = false;
            if (isHuman && humanoidConflict[boneIndex])
            {
                EditorCommon.ShowNotification("Conflict");
                Debug.LogErrorFormat(Language.GetText(Language.Help.LogGenericCurveHumanoidConflictError), editBones[boneIndex].name);
                removeCurve = true;
            }
            if (removeCurve)
            {
                for (int i = 0; i < 3; i++)
                {
                    SetEditorCurveCache(AnimationCurveBindingTransformScale(boneIndex, i), null);
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    var curve = GetAnimationCurveTransformScale(boneIndex, i);
                    SetKeyframe(curve, time, scale[i]);
                    SetAnimationCurveTransformScale(boneIndex, i, curve);
                }
            }
        }
        public AnimationCurve GetAnimationCurveTransformScale(int boneIndex, int dof, bool notNull = true)
        {
            var binding = AnimationCurveBindingTransformScale(boneIndex, dof);
            var curve = GetEditorCurveCache(binding);
            if (curve == null && notNull)
            {
                var defaultValue = boneSaveOriginalTransforms[boneIndex].localScale;
                curve = new AnimationCurve();
                AddKeyframe(curve, 0f, defaultValue[dof]);
                AddKeyframe(curve, currentClip.length, defaultValue[dof]);
                //Created
                uAw.ForceRefresh();
                SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { binding });
            }
            return curve;
        }
        public void SetAnimationCurveTransformScale(int boneIndex, int dof, AnimationCurve curve)
        {
            if (BeginChangeAnimationCurve(currentClip, "Change Transform Scale"))
            {
                SetEditorCurveCache(AnimationCurveBindingTransformScale(boneIndex, dof), curve);
            }
        }

        public bool IsHaveAnimationCurveBlendShape(SkinnedMeshRenderer renderer, string name)
        {
            if (currentClip == null || renderer == null || renderer.sharedMesh == null)
                return false;
            {
                var curve = GetEditorCurveCache(AnimationCurveBindingBlendShape(renderer, name));
                if (curve != null)
                    return true;
            }
            return false;
        }
        public float GetAnimationValueBlendShape(SkinnedMeshRenderer renderer, string name, float time = -1f)
        {
            if (currentClip == null || renderer == null || renderer.sharedMesh == null)
                return 0f;
            time = GetFrameSnapTime(time);
            var curve = GetEditorCurveCache(AnimationCurveBindingBlendShape(renderer, name));
            if (curve != null)
            {
                return curve.Evaluate(time);
            }
            else
            {
                return blendShapeWeightSave.GetOriginalWeight(renderer, name);
            }
        }
        public void SetAnimationValueBlendShapeIfNotOriginal(SkinnedMeshRenderer renderer, string name, float value, float time = -1f)
        {
            if (IsHaveAnimationCurveBlendShape(renderer, name) ||
                !Mathf.Approximately(value, blendShapeWeightSave.GetOriginalWeight(renderer, name)))
            {
                SetAnimationValueBlendShape(renderer, name, value, time);
            }
        }
        public void SetAnimationValueBlendShape(SkinnedMeshRenderer renderer, string name, float value, float time = -1f)
        {
            if (renderer == null || renderer.sharedMesh == null)
                return;
            if (!BeginChangeAnimationCurve(currentClip, "Change BlendShape"))
                return;
            time = GetFrameSnapTime(time);
            {
                var curve = GetAnimationCurveBlendShape(renderer, name);
                SetKeyframe(curve, time, value);
                SetAnimationCurveBlendShape(renderer, name, curve);
            }
        }
        public AnimationCurve GetAnimationCurveBlendShape(SkinnedMeshRenderer renderer, string name, bool notNull = true)
        {
            var binding = AnimationCurveBindingBlendShape(renderer, name);
            var curve = GetEditorCurveCache(binding);
            if (curve == null && notNull)
            {
                var defaultValue = blendShapeWeightSave.GetOriginalWeight(renderer, name);
                curve = new AnimationCurve();
                AddKeyframe(curve, 0f, defaultValue);
                AddKeyframe(curve, currentClip.length, defaultValue);
                //Created
                uAw.ForceRefresh();
                SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { binding });
            }
            return curve;
        }
        public void SetAnimationCurveBlendShape(SkinnedMeshRenderer renderer, string name, AnimationCurve curve)
        {
            if (BeginChangeAnimationCurve(currentClip, "Change BlendShape"))
            {
                SetEditorCurveCache(AnimationCurveBindingBlendShape(renderer, name), curve);
            }
        }

        public bool IsHaveAnimationCurveCustomProperty(EditorCurveBinding binding)
        {
            if (currentClip == null)
                return false;
            return GetEditorCurveCache(binding) != null;
        }
        public float GetAnimationValueCustomProperty(EditorCurveBinding binding, float time = -1f)
        {
            if (currentClip == null)
                return 0f;
            time = GetFrameSnapTime(time);
            var curve = GetEditorCurveCache(binding);
            if (curve == null)
            {
                float value;
                AnimationUtility.GetFloatValue(vaw.gameObject, binding, out value);
                return value;
            }
            return curve.Evaluate(time);
        }
        public void SetAnimationValueCustomPropertyIfNotOriginal(EditorCurveBinding binding, float value, float time = -1f)
        {
            if (IsHaveAnimationCurveCustomProperty(binding) ||
                !Mathf.Approximately(value, 0f))
            {
                SetAnimationValueCustomProperty(binding, value, time);
            }
        }
        public void SetAnimationValueCustomProperty(EditorCurveBinding binding, float value, float time = -1f)
        {
            if (!BeginChangeAnimationCurve(currentClip, "Change Property"))
                return;
            time = GetFrameSnapTime(time);
            {
                var curve = GetAnimationCurveCustomProperty(binding);
                SetKeyframe(curve, time, value);
                SetAnimationCurveCustomProperty(binding, curve);
            }
        }
        public AnimationCurve GetAnimationCurveCustomProperty(EditorCurveBinding binding, bool notNull = true)
        {
            var curve = GetEditorCurveCache(binding);
            if (curve == null && notNull)
            {
                curve = new AnimationCurve();
                float value;
                AnimationUtility.GetFloatValue(vaw.gameObject, binding, out value);
                AddKeyframe(curve, 0f, value);
                AddKeyframe(curve, currentClip.length, value);
                //Created
                uAw.ForceRefresh();
                SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { binding });
            }
            return curve;
        }
        public void SetAnimationCurveCustomProperty(EditorCurveBinding binding, AnimationCurve curve)
        {
            if (BeginChangeAnimationCurve(currentClip, "Change Property"))
            {
                SetEditorCurveCache(binding, curve);
            }
        }
        #endregion
    }
}
