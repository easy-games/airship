using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
#if VERYANIMATION_ANIMATIONRIGGING
using UnityEngine.Animations.Rigging;
using UnityEditor.Animations.Rigging;
#endif

namespace VeryAnimation
{
    [Serializable]
    public class AnimatorIKCore
    {
        private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
        private VeryAnimation va { get { return VeryAnimation.instance; } }

        public enum IKTarget
        {
            None = -1,
            Head,
            LeftHand,
            RightHand,
            LeftFoot,
            RightFoot,
            Total,
        }
        public static readonly string[] IKTargetStrings =
        {
            "Head",
            "Left Hand",
            "Right Hand",
            "Left Foot",
            "Right Foot",
        };
        private static readonly IKTarget[] IKTargetMirror =
        {
            IKTarget.None,
            IKTarget.RightHand,
            IKTarget.LeftHand,
            IKTarget.RightFoot,
            IKTarget.LeftFoot,
        };
        private readonly Quaternion[] IKTargetSyncRotation =
        {
            Quaternion.identity,
            Quaternion.Euler(0, 90, 180),
            Quaternion.Euler(0, 90, 0),
            Quaternion.Euler(90, 0, 90),
            Quaternion.Euler(90, 0, 90),
        };

        public static readonly IKTarget[] HumanBonesUpdateAnimatorIK =
        {
            IKTarget.Total, //Hips = 0,
            IKTarget.LeftFoot, //LeftUpperLeg = 1,
            IKTarget.RightFoot, //RightUpperLeg = 2,
            IKTarget.LeftFoot, //LeftLowerLeg = 3,
            IKTarget.RightFoot, //RightLowerLeg = 4,
            IKTarget.LeftFoot, //LeftFoot = 5,
            IKTarget.RightFoot, //RightFoot = 6,
            IKTarget.Total, //Spine = 7,
            IKTarget.Total, //Chest = 8,
            IKTarget.Head, //Neck = 9,
            IKTarget.Head, //Head = 10,
            IKTarget.LeftHand, //LeftShoulder = 11,
            IKTarget.RightHand, //RightShoulder = 12,
            IKTarget.LeftHand, //LeftUpperArm = 13,
            IKTarget.RightHand, //RightUpperArm = 14,
            IKTarget.LeftHand, //LeftLowerArm = 15,
            IKTarget.RightHand, //RightLowerArm = 16,
            IKTarget.LeftHand, //LeftHand = 17,
            IKTarget.RightHand, //RightHand = 18,
            IKTarget.None, //LeftToes = 19,
            IKTarget.None, //RightToes = 20,
            IKTarget.Head, //LeftEye = 21,
            IKTarget.Head, //RightEye = 22,
            IKTarget.None, //Jaw = 23,
            IKTarget.None, //LeftThumbProximal = 24,
            IKTarget.None, //LeftThumbIntermediate = 25,
            IKTarget.None, //LeftThumbDistal = 26,
            IKTarget.None, //LeftIndexProximal = 27,
            IKTarget.None, //LeftIndexIntermediate = 28,
            IKTarget.None, //LeftIndexDistal = 29,
            IKTarget.None, //LeftMiddleProximal = 30,
            IKTarget.None, //LeftMiddleIntermediate = 31,
            IKTarget.None, //LeftMiddleDistal = 32,
            IKTarget.None, //LeftRingProximal = 33,
            IKTarget.None, //LeftRingIntermediate = 34,
            IKTarget.None, //LeftRingDistal = 35,
            IKTarget.None, //LeftLittleProximal = 36,
            IKTarget.None, //LeftLittleIntermediate = 37,
            IKTarget.None, //LeftLittleDistal = 38,
            IKTarget.None, //RightThumbProximal = 39,
            IKTarget.None, //RightThumbIntermediate = 40,
            IKTarget.None, //RightThumbDistal = 41,
            IKTarget.None, //RightIndexProximal = 42,
            IKTarget.None, //RightIndexIntermediate = 43,
            IKTarget.None, //RightIndexDistal = 44,
            IKTarget.None, //RightMiddleProximal = 45,
            IKTarget.None, //RightMiddleIntermediate = 46,
            IKTarget.None, //RightMiddleDistal = 47,
            IKTarget.None, //RightRingProximal = 48,
            IKTarget.None, //RightRingIntermediate = 49,
            IKTarget.None, //RightRingDistal = 50,
            IKTarget.None, //RightLittleProximal = 51,
            IKTarget.None, //RightLittleIntermediate = 52,
            IKTarget.None, //RightLittleDistal = 53,
            IKTarget.Total, //UpperChest = 54,
        };

        public GUIContent[] IKSpaceTypeStrings = new GUIContent[(int)AnimatorIKData.SpaceType.Total];

        [Serializable]
        public class AnimatorIKData
        {
            private VeryAnimation va { get { return VeryAnimation.instance; } }

            public enum SpaceType
            {
                Global,
                Local,
                Parent,
                Total
            }

            public bool enable;
            public bool autoRotation;
            public SpaceType spaceType;
            public GameObject parent;
            public Vector3 position;
            public Quaternion rotation;
            //Head
            public float headWeight = 1f;
            public float eyesWeight = 0f;
            //Swivel
            public float swivelRotation;

            public Vector3 worldPosition
            {
                get
                {
                    var getpos = position;
                    switch (spaceType)
                    {
                    case SpaceType.Global:
                        break;
                    case SpaceType.Local:
                        if (rootBoneIndex >= 0 && va.bones[rootBoneIndex] != null && va.bones[rootBoneIndex].transform.parent != null)
                            getpos = va.bones[rootBoneIndex].transform.parent.localToWorldMatrix.MultiplyPoint3x4(getpos);
                        break;
                    case SpaceType.Parent:
                        if (parent != null)
                            getpos = parent.transform.localToWorldMatrix.MultiplyPoint3x4(getpos);
                        break;
                    default:
                        Assert.IsTrue(false);
                        break;
                    }
                    return getpos;
                }
                set
                {
                    var setpos = value;
                    switch (spaceType)
                    {
                    case SpaceType.Global:
                        break;
                    case SpaceType.Local:
                        if (rootBoneIndex >= 0 && va.bones[rootBoneIndex] != null && va.bones[rootBoneIndex].transform.parent != null)
                            setpos = va.bones[rootBoneIndex].transform.parent.worldToLocalMatrix.MultiplyPoint3x4(setpos);
                        break;
                    case SpaceType.Parent:
                        if (parent != null)
                            setpos = parent.transform.worldToLocalMatrix.MultiplyPoint3x4(setpos);
                        break;
                    default:
                        Assert.IsTrue(false);
                        break;
                    }
                    position = setpos;
                }
            }
            public Quaternion worldRotation
            {
                get
                {
                    var getrot = rotation;
                    switch (spaceType)
                    {
                    case SpaceType.Global: 
                        break;
                    case SpaceType.Local:
                        if (rootBoneIndex >= 0 && va.bones[rootBoneIndex] != null && va.bones[rootBoneIndex].transform.parent != null)
                            getrot = va.bones[rootBoneIndex].transform.parent.rotation * getrot; 
                        break;
                    case SpaceType.Parent:
                        if (parent != null)
                            getrot = parent.transform.rotation * getrot;
                        break;
                    default: 
                        Assert.IsTrue(false); 
                        break;
                    }
                    return getrot;
                }
                set
                {
                    var setrot = value;
                    {   //Handles error -> Quaternion To Matrix conversion failed because input Quaternion is invalid
                        float angle;
                        Vector3 axis;
                        setrot.ToAngleAxis(out angle, out axis);
                        setrot = Quaternion.AngleAxis(angle, axis);
                    }
                    switch (spaceType)
                    {
                    case SpaceType.Global: 
                        break;
                    case SpaceType.Local:
                        if (rootBoneIndex >= 0 && va.bones[rootBoneIndex] != null && va.bones[rootBoneIndex].transform.parent != null)
                            setrot = Quaternion.Inverse(va.bones[rootBoneIndex].transform.parent.rotation) * setrot; 
                        break;
                    case SpaceType.Parent:
                        if (parent != null)
                            setrot = Quaternion.Inverse(parent.transform.rotation) * setrot; 
                        break;
                    default: 
                        Assert.IsTrue(false); 
                        break;
                    }
                    rotation = setrot;
                }
            }

            public bool isUpdate { get { return enable && updateIKtarget && !synchroIKtarget; } }

            [NonSerialized]
            public int rootBoneIndex;
            [NonSerialized]
            public int parentBoneIndex;
            [NonSerialized]
            public bool updateIKtarget;
            [NonSerialized]
            public bool synchroIKtarget;
            [NonSerialized]
            public Vector3 swivelPosition;
            [NonSerialized]
            public Vector3 optionPosition;
            [NonSerialized]
            public Quaternion optionRotation;

#if VERYANIMATION_ANIMATIONRIGGING
            [NonSerialized]
            public MonoBehaviour rigConstraint;
            [NonSerialized]
            public GUIContent rigConstraintGUIContent;
            [NonSerialized]
            public EditorCurveBinding rigConstraintWeight;
#endif
        }
        public AnimatorIKData[] ikData;

        public IKTarget[] ikTargetSelect;
        public IKTarget ikActiveTarget { get { return ikTargetSelect != null && ikTargetSelect.Length > 0 ? ikTargetSelect[0] : IKTarget.None; } }

        private AnimationCurve[] rootTCurves;
        private AnimationCurve[] rootQCurves;
        private AnimationCurve[] muscleCurves;
        private List<int> muscleCurvesUpdated;

        private int[] neckMuscleIndexes;
        private int[] headMuscleIndexes;

        private UDisc uDisc;
        private USnapSettings uSnapSettings;

        private ReorderableList ikReorderableList;
        private bool advancedFoldout;

#if !UNITY_2018_3_OR_NEWER
        private Vector3 ikSaveBodyPosition;
        private Quaternion ikSaveBodyRotation;
#endif

        public Avatar avatarClone { get; private set; }

        public void Initialize()
        {
            Release();

            ikData = new AnimatorIKData[(int)IKTarget.Total];
            for (int i = 0; i < ikData.Length; i++)
            {
                ikData[i] = new AnimatorIKData();
#if VERYANIMATION_ANIMATIONRIGGING
                UpdateAnimationRiggingConstraint((IKTarget)i);
#endif
            }
            ikTargetSelect = null;

            rootTCurves = new AnimationCurve[3];
            rootQCurves = new AnimationCurve[4];
            muscleCurves = new AnimationCurve[HumanTrait.MuscleCount];
            muscleCurvesUpdated = new List<int>();

            neckMuscleIndexes = new int[3];
            for (int i = 0; i < neckMuscleIndexes.Length; i++)
                neckMuscleIndexes[i] = HumanTrait.MuscleFromBone((int)HumanBodyBones.Neck, i);
            headMuscleIndexes = new int[3];
            for (int i = 0; i < headMuscleIndexes.Length; i++)
                headMuscleIndexes[i] = HumanTrait.MuscleFromBone((int)HumanBodyBones.Head, i);

            uDisc = new UDisc();
            uSnapSettings = new USnapSettings();

            UpdateReorderableList();

            UpdateGUIContentStrings();
            Language.OnLanguageChanged += UpdateGUIContentStrings;
        }
        public void Release()
        {
            Language.OnLanguageChanged -= UpdateGUIContentStrings;

            if (avatarClone != null)
            {
                Avatar.DestroyImmediate(avatarClone);
                avatarClone = null;
            }

            ikData = null;
            ikTargetSelect = null;
            rootTCurves = null;
            rootQCurves = null;
            muscleCurves = null;
            muscleCurvesUpdated = null;
            neckMuscleIndexes = null;
            headMuscleIndexes = null;
            uDisc = null;
            uSnapSettings = null;
            ikReorderableList = null;
        }

        public void LoadIKSaveSettings(VeryAnimationSaveSettings.AnimatorIKData[] saveIkData)
        {
            if (va.isHuman)
            {
                if (saveIkData != null && saveIkData.Length == ikData.Length)
                {
                    for (int i = 0; i < saveIkData.Length; i++)
                    {
                        var src = saveIkData[i];
                        var dst = ikData[i];
                        dst.enable = src.enable;
                        dst.autoRotation = src.autoRotation;
                        dst.spaceType = (AnimatorIKData.SpaceType)src.spaceType;
                        dst.parent = src.parent;
                        dst.position = src.position;
                        dst.rotation = src.rotation;
                        dst.headWeight = src.headWeight;
                        dst.eyesWeight = src.eyesWeight;
                        dst.swivelRotation = src.swivelRotation;

                        //Path
                        if (!string.IsNullOrEmpty(src.parentPath))
                        {
                            var t = vaw.gameObject.transform.Find(src.parentPath);
                            if (t != null)
                                dst.parent = t.gameObject;
                        }
                    }
                }
            }
        }
        public VeryAnimationSaveSettings.AnimatorIKData[] SaveIKSaveSettings()
        {
            if (va.isHuman)
            {
                List<VeryAnimationSaveSettings.AnimatorIKData> saveIkData = new List<VeryAnimationSaveSettings.AnimatorIKData>();
                if (ikData != null)
                {
                    foreach (var d in ikData)
                    {
                        saveIkData.Add(new VeryAnimationSaveSettings.AnimatorIKData()
                        {
                            enable = d.enable,
                            autoRotation = d.autoRotation,
                            spaceType = (int)d.spaceType,
                            parent = d.parent,
                            position = d.position,
                            rotation = d.rotation,
                            headWeight = d.headWeight,
                            eyesWeight = d.eyesWeight,
                            swivelRotation = d.swivelRotation,
                            //Path
                            parentPath = d.parent != null ? AnimationUtility.CalculateTransformPath(d.parent.transform, vaw.gameObject.transform) : "",
                        });
                    }
                }
                return saveIkData.ToArray();
            }
            else
            {
                return null;
            }
        }

        private void UpdateReorderableList()
        {
            ikReorderableList = null;
            if (ikData == null) return;
            ikReorderableList = new ReorderableList(ikData, typeof(AnimatorIKData), false, false, false, false);
            ikReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (index >= ikData.Length)
                    return;

                float x = rect.x;
                {
                    var r = rect;
                    r.x = x;
                    r.y += 2;
                    r.height -= 4;
                    r.width = 16;
                    rect.xMin += r.width;
                    x = rect.x;
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.Toggle(r, ikData[index].enable);
                    if (EditorGUI.EndChangeCheck())
                    {
                        ChangeTargetIK((IKTarget)index);
                    }
                }

                EditorGUI.BeginDisabledGroup(!ikData[index].enable);
                {
                    const float Rate = 1f;
                    var r = rect;
                    r.x = x + 2;
                    r.y += 2;
                    r.height -= 4;
                    r.width = rect.width * Rate;
                    x += r.width;
                    r.width -= 4;
                    GUI.Label(r, IKTargetStrings[index]);
                }
                EditorGUI.EndDisabledGroup();

                if (!IsValid((IKTarget)index))
                {
                    var tex = vaw.uEditorGUIUtility.GetHelpIcon(MessageType.Warning);
                    var r = rect;
                    r.width = tex.width;
                    r.x = 20;
                    GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
                }

#if VERYANIMATION_ANIMATIONRIGGING
                if (ikData[index].rigConstraint != null)
                {
                    var r = rect;
                    r.width = 140f;
                    r.x = rect.xMax - r.width - 80f;
                    if (GUI.Button(r, ikData[index].rigConstraintGUIContent))
                    {
                        va.SelectGameObject(ikData[index].rigConstraint.gameObject);
                        {
                            var list = new List<EditorCurveBinding>();
                            {
                                list.AddRange(GetAnimationRiggingConstraintBindings((IKTarget)index));
                                list.Add(ikData[index].rigConstraintWeight);
                            }
                            va.SetAnimationWindowSynchroSelection(list.ToArray());
                        }
                    }
                }
#endif

                {
                    var r = rect;
                    r.width = 60f;
                    r.x = rect.xMax - r.width - 14;
                    EditorGUI.LabelField(r, IKSpaceTypeStrings[(int)ikData[index].spaceType], vaw.guiStyleMiddleRightGreyMiniLabel);
                }

#if VERYANIMATION_ANIMATIONRIGGING
                if (ikReorderableList.index == index)
#else
                if (ikReorderableList.index == index && (IKTarget)index == IKTarget.Head)
#endif
                {
                    var r = rect;
                    r.y += 2;
                    r.height -= 2;
                    r.width = 24;
                    r.x = rect.xMax - 12;
                    advancedFoldout = EditorGUI.Foldout(r, advancedFoldout, new GUIContent(" ", "Advanced"), true);
                }
            };
            ikReorderableList.onChangedCallback = (ReorderableList list) =>
            {
                Undo.RecordObject(vaw, "Change Animator IK Data");
                ikTargetSelect = null;
                vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.All);
            };
            ikReorderableList.onSelectCallback = (ReorderableList list) =>
            {
                if (list.index >= 0 && list.index < ikData.Length)
                {
                    if (ikData[list.index].enable)
                        va.SelectAnimatorIKTargetPlusKey((IKTarget)list.index);
                    else
                    {
                        var index = list.index;
                        var humanoidIndex = GetEndHumanoidIndex((IKTarget)list.index);
                        va.SelectGameObject(va.humanoidBones[(int)humanoidIndex]);
                        list.index = index;
                    }
                }
            };
        }

        private void UpdateGUIContentStrings()
        {
            for (int i = 0; i < (int)AnimatorIKData.SpaceType.Total; i++)
            {
                IKSpaceTypeStrings[i] = new GUIContent(Language.GetContent(Language.Help.SelectionAnimatorIKSpaceTypeGlobal + i));
            }
        }

        public void UpdateBones()
        {
            if (!va.isHuman)
                return;

            #region Non-Stretch Avatar
            if (avatarClone != null)
            {
                Avatar.DestroyImmediate(avatarClone);
                avatarClone = null;
            }
            if (va.animatorAvatar != null)
            {
                avatarClone = Avatar.Instantiate<Avatar>(va.animatorAvatar);
                avatarClone.hideFlags |= HideFlags.HideAndDontSave;
                va.uAvatar.SetArmStretch(avatarClone, 0.0001f);  //Since it is occasionally wrong value when it is 0
                va.uAvatar.SetLegStretch(avatarClone, 0.0001f);
                va.calcObject.animator.avatar = avatarClone;
            }
            #endregion

            va.calcObject.vaEdit.onAnimatorIK -= AnimatorOnAnimatorIK;
            va.calcObject.vaEdit.onAnimatorIK += AnimatorOnAnimatorIK;
        }

        public void OnSelectionChange()
        {
            if (ikReorderableList != null)
            {
                if (ikActiveTarget != IKTarget.None)
                {
                    ikReorderableList.index = (int)ikActiveTarget;
                }
                else
                {
                    ikReorderableList.index = -1;
                }
            }

            if(ikTargetSelect != null)
            {
                foreach (var target in ikTargetSelect)
                {
                    SetSynchroIKtargetAnimatorIK(target);
                }
            }
        }

        public void UpdateSynchroIKSet()
        {
            for (int i = 0; i < ikData.Length; i++)
            {
                if (ikData[i].enable && ikData[i].synchroIKtarget)
                {
                    SynchroSet((IKTarget)i);
                }
                ikData[i].synchroIKtarget = false;
            }
        }
        [Flags]
        public enum SynchroSetFlags : UInt32
        {
            None = 0,
            AnimationRigging = (1 << 0),
            HandIK = (1 << 1),
            FootIK = (1 << 2),
            Default = 0xFFFFFFFF,
        }
        public void SynchroSet(IKTarget target, SynchroSetFlags syncFlags = SynchroSetFlags.Default)
        {
            if (!va.isHuman) return;

            if (syncFlags == SynchroSetFlags.Default)
            {
                syncFlags = SynchroSetFlags.None;
                if (va.IsEnableUpdateHumanoidFootIK()) syncFlags |= AnimatorIKCore.SynchroSetFlags.FootIK;
#if VERYANIMATION_ANIMATIONRIGGING
                if (va.animationRigging.isValid) syncFlags |= AnimatorIKCore.SynchroSetFlags.AnimationRigging;
#endif
            }

            var data = ikData[(int)target];

            data.rootBoneIndex = va.humanoidIndex2boneIndex[(int)GetStartHumanoidIndex(target)];
            data.parentBoneIndex = va.BonesIndexOf(data.parent);

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            float swivelRotation = 0f;
            Vector3? swivelPosition = null;
            bool done = false;
            switch (target)
            {
            case IKTarget.Head:
                if (!done)
                {
#if VERYANIMATION_ANIMATIONRIGGING
                    #region AnimationRigging
                    if (!done && (syncFlags & SynchroSetFlags.AnimationRigging) != 0)
                    {
                        var constraint = data.rigConstraint as MultiAimConstraint;
                        if (constraint != null && constraint.data.sourceObjects.Count > 0)
                        {
                            var targetBoneIndex = va.BonesIndexOf(constraint.data.sourceObjects[0].transform.gameObject);
                            if (targetBoneIndex >= 0 && va.IsHaveAnimationCurveTransformPosition(targetBoneIndex))
                            {
                                var parent = va.editBones[targetBoneIndex].transform.parent;
                                position = parent.localToWorldMatrix.MultiplyPoint3x4(va.GetAnimationValueTransformPosition(targetBoneIndex));
                                rotation = Quaternion.identity;
                                done = true;
                            }
                        }
                    }
                    #endregion
#endif
                    if (!done)
                    {
                        Func<HumanBodyBones, Vector3, Vector3> CommonizationVector = (humanoidIndex, vec) =>
                        {
                            var t = va.editHumanoidBones[(int)humanoidIndex].transform;
                            return (t.rotation * va.GetAvatarPostRotation(humanoidIndex)) * vec;
                        };

                        if (va.editHumanoidBones[(int)HumanBodyBones.LeftEye] != null && va.editHumanoidBones[(int)HumanBodyBones.RightEye])
                        {
                            var tL = va.editHumanoidBones[(int)HumanBodyBones.LeftEye].transform;
                            var tR = va.editHumanoidBones[(int)HumanBodyBones.RightEye].transform;
                            position = Vector3.Lerp(tL.position, tR.position, 0.5f) + CommonizationVector(HumanBodyBones.LeftEye, Vector3.right);
                        }
                        else if (va.editHumanoidBones[(int)HumanBodyBones.Head] != null)
                        {
                            var t = va.editHumanoidBones[(int)HumanBodyBones.Head].transform;
                            position = t.position + CommonizationVector(HumanBodyBones.Head, Vector3.down);
                        }
                        rotation = Quaternion.identity;
                        done = true;
                    }

                    {
                        var hp = new HumanPose();
                        va.GetEditGameObjectHumanPose(ref hp);

                        float angleNeck, angleHead;
                        {
                            var muscle = hp.muscles[neckMuscleIndexes[1]];
                            float angle = (va.humanoidMuscleLimit[(int)HumanBodyBones.Neck].max.y - va.humanoidMuscleLimit[(int)HumanBodyBones.Neck].min.y) / 2f;
                            angleNeck = (-angle * muscle);
                        }
                        {
                            var muscle = hp.muscles[headMuscleIndexes[1]];
                            float angle = (va.humanoidMuscleLimit[(int)HumanBodyBones.Head].max.y - va.humanoidMuscleLimit[(int)HumanBodyBones.Head].min.y) / 2f;
                            angleHead = (-angle * muscle);
                        }
                        swivelRotation = angleNeck + angleHead;
                    }
                }
                break;
            case IKTarget.LeftHand:
            case IKTarget.RightHand:
            case IKTarget.LeftFoot:
            case IKTarget.RightFoot:
                var hiA = (int)GetStartHumanoidIndex(target);
                var hiB = (int)GetEndHumanoidIndex(target);
                var hiC = (int)GetCenterHumanoidIndex(target);
                var tA = va.editHumanoidBones[hiA].transform;
                var tB = va.editHumanoidBones[hiB].transform;
                var tC = va.editHumanoidBones[hiC].transform;
#if VERYANIMATION_ANIMATIONRIGGING
                #region AnimationRigging
                if (!done && (syncFlags & SynchroSetFlags.AnimationRigging) != 0)
                {
                    var constraint = data.rigConstraint as TwoBoneIKConstraint;
                    if (constraint != null && constraint.data.target != null && constraint.data.hint != null)
                    {
                        var targetBoneIndex = va.BonesIndexOf(constraint.data.target.gameObject);
                        var hintBoneIndex = va.BonesIndexOf(constraint.data.hint.gameObject);
                        if (targetBoneIndex >= 0 && va.IsHaveAnimationCurveTransformPosition(targetBoneIndex) &&
                            va.IsHaveAnimationCurveTransformRotation(targetBoneIndex) != URotationCurveInterpolation.Mode.Undefined &&
                            hintBoneIndex >= 0 && va.IsHaveAnimationCurveTransformPosition(hintBoneIndex))
                        {
                            {
                                var parent = va.editBones[targetBoneIndex].transform.parent;
                                position = parent.localToWorldMatrix.MultiplyPoint3x4(va.GetAnimationValueTransformPosition(targetBoneIndex));
                                rotation = parent.rotation * va.GetAnimationValueTransformRotation(targetBoneIndex);
                                #region FeetBottomHeight
                                switch (target)
                                {
                                case IKTarget.LeftFoot: position += new Vector3(0f, va.editAnimator.leftFeetBottomHeight, 0f); break;
                                case IKTarget.RightFoot: position += new Vector3(0f, va.editAnimator.rightFeetBottomHeight, 0f); break;
                                }
                                #endregion
                            }
                            {
                                var parent = va.editBones[hintBoneIndex].transform.parent;
                                swivelPosition = parent.localToWorldMatrix.MultiplyPoint3x4(va.GetAnimationValueTransformPosition(hintBoneIndex));
                                if (!GetSwivelRotation(tA.position, tB.position, swivelPosition.Value, out swivelRotation))
                                {
                                    switch (target)
                                    {
                                    case IKTarget.LeftHand: swivelRotation = 90f; break;
                                    case IKTarget.RightHand: swivelRotation = -90f; break;
                                    default: swivelRotation = 0f; break;
                                    }
                                }
                            }
                            done = true;
                        }
                    }
                }
                #endregion
#endif
                #region HumanoidIK
                if (!done &&
                    ((target >= IKTarget.LeftHand && target <= IKTarget.RightHand && (syncFlags & SynchroSetFlags.HandIK) != 0) ||
                    (target >= IKTarget.LeftFoot && target <= IKTarget.RightFoot && (syncFlags & SynchroSetFlags.FootIK) != 0)))
                {
                    var ikIndex = VeryAnimation.AnimatorIKIndex.LeftHand + (int)(target - IKTarget.LeftHand);
                    if (va.IsHaveAnimationCurveAnimatorIkT(ikIndex) &&
                        va.IsHaveAnimationCurveAnimatorIkQ(ikIndex))
                    {
                        var rootT = va.GetAnimationValueAnimatorRootT();
                        var rootQ = va.GetAnimationValueAnimatorRootQ();

                        rotation = (rootQ * va.GetAnimationValueAnimatorIkQ(ikIndex)) * IKTargetSyncRotation[(int)target];

                        position = va.GetAnimationValueAnimatorIkT(ikIndex);
                        position = rootT + rootQ * position;
                        position *= va.editAnimator.humanScale;

                        rotation = va.transformPoseSave.startRotation * rotation;
                        position = va.transformPoseSave.startMatrix.MultiplyPoint3x4(position);

                        switch (ikIndex)
                        {
                        case VeryAnimation.AnimatorIKIndex.LeftFoot:
                            position += rotation * new Vector3(0f, va.editAnimator.leftFeetBottomHeight, 0f);
                            break;
                        case VeryAnimation.AnimatorIKIndex.RightFoot:
                            position += rotation * new Vector3(0f, va.editAnimator.rightFeetBottomHeight, 0f);
                            break;
                        }

                        if (!GetSwivelRotation(tA.position, tB.position, tC.position, out swivelRotation))
                        {
                            switch (target)
                            {
                            case IKTarget.LeftHand: swivelRotation = 90f; break;
                            case IKTarget.RightHand: swivelRotation = -90f; break;
                            default: swivelRotation = 0f; break;
                            }
                        }
                        done = true;
                    }
                }
                #endregion
                if (!done)
                {
                    position = tB.position;
                    rotation = tB.rotation * va.GetAvatarPostRotation((HumanBodyBones)hiB) * IKTargetSyncRotation[(int)target];
                    if (!GetSwivelRotation(tA.position, tB.position, tC.position, out swivelRotation))
                    {
                        switch (target)
                        {
                        case IKTarget.LeftHand: swivelRotation = 90f; break;
                        case IKTarget.RightHand: swivelRotation = -90f; break;
                        default: swivelRotation = 0f; break;
                        }
                    }
                    done = true;
                }
                break;
            }
            if (!done)
                return;

            switch (data.spaceType)
            {
            case AnimatorIKData.SpaceType.Global:
            case AnimatorIKData.SpaceType.Local:
                data.worldPosition = position;
                data.worldRotation = rotation;
                data.swivelRotation = Mathf.Repeat(swivelRotation + 180f, 360f) - 180f;
                break;
            case AnimatorIKData.SpaceType.Parent:
                //not update
                if (target == IKTarget.Head)
                    data.rotation = Quaternion.identity;
                break;
            }

            UpdateOptionPosition(target);
            if (!swivelPosition.HasValue)
                UpdateSwivelPosition(target);
            else
                data.swivelPosition = swivelPosition.Value;

#if VERYANIMATION_ANIMATIONRIGGING
            UpdateAnimationRiggingConstraint(target);
#endif
        }
        public void UpdateOptionPosition(IKTarget target)
        {
            if (!va.isHuman) return;

            var data = ikData[(int)target];
            var humanoidBones = va.editHumanoidBones;

            #region Heel
            switch (target)
            {
            case IKTarget.LeftFoot:
                if (humanoidBones[(int)HumanBodyBones.LeftToes] != null)
                {
                    var tB = humanoidBones[(int)HumanBodyBones.LeftFoot].transform;
                    var tD = humanoidBones[(int)HumanBodyBones.LeftToes].transform;
                    data.optionRotation = data.worldRotation;
                    data.optionPosition = data.worldPosition + (data.optionRotation * Vector3.back) * Vector3.Distance(tD.position, tB.position) * 6f;
                }
                break;
            case IKTarget.RightFoot:
                if (humanoidBones[(int)HumanBodyBones.RightToes] != null)
                {
                    var tB = humanoidBones[(int)HumanBodyBones.RightFoot].transform;
                    var tD = humanoidBones[(int)HumanBodyBones.RightToes].transform;
                    data.optionRotation = data.worldRotation;
                    data.optionPosition = data.worldPosition + (data.optionRotation * Vector3.back) * Vector3.Distance(tD.position, tB.position) * 6f;
                }
                break;
            }
            #endregion
        }
        private bool GetSwivelRotation(Vector3 posA, Vector3 posB, Vector3 posC, out float swivelRotation)
        {
            const float DotThreshold = 0.99f;
            
            var worldToLocalMatrix = va.transformPoseSave.startMatrix.inverse;
            posA = worldToLocalMatrix.MultiplyPoint3x4(posA);
            posB = worldToLocalMatrix.MultiplyPoint3x4(posB);
            posC = worldToLocalMatrix.MultiplyPoint3x4(posC);

            swivelRotation = 0f;
            var axis = posB - posA;
            var dot = Vector3.Dot((posC - posA).normalized, (posB - posC).normalized);
            axis.Normalize();
            if (axis.sqrMagnitude > 0f && Mathf.Abs(dot) < DotThreshold)
            {
                var posP = posA + axis * Vector3.Dot((posC - posA), axis);
                var vec = Quaternion.Inverse(Quaternion.FromToRotation(Vector3.forward, axis)) * (posC - posP).normalized;
                var rot = Quaternion.FromToRotation(Vector3.up, vec);
                swivelRotation = rot.eulerAngles.z;
                return true;
            }
            else
            {
                return false;
            }
        }
        public void UpdateSwivelPosition(IKTarget target)
        {
            if (!va.isHuman) return;
            if (target < IKTarget.LeftHand || target > IKTarget.RightFoot)
                return;

            var data = ikData[(int)target];
            var humanoidBones = va.editHumanoidBones;

            #region Swivel
            {
                var hiA = GetStartHumanoidIndex(target);
                var hiB = GetEndHumanoidIndex(target);
                var hiC = GetCenterHumanoidIndex(target);
                var posA = humanoidBones[(int)hiA].transform.position;
                var posB = humanoidBones[(int)hiB].transform.position;
                var posC = humanoidBones[(int)hiC].transform.position;

                var worldToLocalMatrix = va.transformPoseSave.startMatrix.inverse;
                posA = worldToLocalMatrix.MultiplyPoint3x4(posA);
                posB = worldToLocalMatrix.MultiplyPoint3x4(posB);
                posC = worldToLocalMatrix.MultiplyPoint3x4(posC);

                var axis = posB - posA;
                if (axis.sqrMagnitude > 0f)
                {
                    axis.Normalize();
                    var posP = posA + axis * Vector3.Dot((posC - posA), axis);
                    float length = Vector3.Distance(posC, posA) + Vector3.Distance(posC, posB);
                    var vec = Quaternion.AngleAxis(data.swivelRotation, axis) * (Quaternion.FromToRotation(Vector3.forward, axis) * Vector3.up);
                    var posL = posP + vec * length;
                    data.swivelPosition = va.transformPoseSave.startMatrix.MultiplyPoint3x4(posL);
                }
            }
            #endregion
        }

        public bool IsValid(IKTarget target)
        {
            if (!va.isHuman || va.boneDefaultPose == null)
                return false;

            var boneIndex = va.humanoidIndex2boneIndex[(int)GetEndHumanoidIndex(target)];
            boneIndex = va.parentBoneIndexes[boneIndex];
            while (boneIndex >= 0)
            {
                if (va.boneDefaultPose[boneIndex] == null)
                    return false;
                if (Mathf.Abs(va.boneDefaultPose[boneIndex].scale.x - va.bones[boneIndex].transform.localScale.x) > 0.0001f ||
                    Mathf.Abs(va.boneDefaultPose[boneIndex].scale.y - va.bones[boneIndex].transform.localScale.y) > 0.0001f ||
                    Mathf.Abs(va.boneDefaultPose[boneIndex].scale.z - va.bones[boneIndex].transform.localScale.z) > 0.0001f)
                    return false;
                boneIndex = va.parentBoneIndexes[boneIndex];
            }

            return true;
        }

        public void UpdateIK(bool rootUpdated, bool writeAnimationRigging = true)
        {
            if (!va.isHuman) return;
            if (!GetUpdateIKtargetAll()) return;
            
            {
                for (int i = 0; i < 3; i++)
                    rootTCurves[i] = null;
                for (int i = 0; i < 4; i++)
                    rootQCurves[i] = null;
                for (int i = 0; i < muscleCurves.Length; i++)
                    muscleCurves[i] = null;
            }

            #region Reset Head LeftRight
            if (ikData[(int)IKTarget.Head].isUpdate)
            {
                for (int i = 0; i < neckMuscleIndexes.Length; i++)
                {
                    var muscleIndex = neckMuscleIndexes[i];
                    muscleCurves[muscleIndex] = va.GetAnimationCurveAnimatorMuscle(muscleIndex);
                }
                for (int i = 0; i < headMuscleIndexes.Length; i++)
                {
                    var muscleIndex = headMuscleIndexes[i];
                    muscleCurves[muscleIndex] = va.GetAnimationCurveAnimatorMuscle(muscleIndex);
                }

                va.SetKeyframe(muscleCurves[neckMuscleIndexes[0]], va.currentTime, 0f);
                va.SetKeyframe(muscleCurves[headMuscleIndexes[0]], va.currentTime, 0f);
                {
                    float angle = (va.humanoidMuscleLimit[(int)HumanBodyBones.Neck].max.y - va.humanoidMuscleLimit[(int)HumanBodyBones.Neck].min.y) / 2f;
                    var rate = (-ikData[(int)IKTarget.Head].swivelRotation / angle) / 2f;
                    va.SetKeyframe(muscleCurves[neckMuscleIndexes[1]], va.currentTime, rate);
                }
                {
                    float angle = (va.humanoidMuscleLimit[(int)HumanBodyBones.Head].max.y - va.humanoidMuscleLimit[(int)HumanBodyBones.Head].min.y) / 2f;
                    var rate = (-ikData[(int)IKTarget.Head].swivelRotation / angle) / 2f;
                    va.SetKeyframe(muscleCurves[headMuscleIndexes[1]], va.currentTime, rate);
                }
                {
                    var rate = muscleCurves[neckMuscleIndexes[2]].Evaluate(va.currentTime);
                    rate = Mathf.Clamp(rate, -1f, 1f);
                    va.SetKeyframe(muscleCurves[neckMuscleIndexes[2]], va.currentTime, rate);
                }
                {
                    var rate = muscleCurves[headMuscleIndexes[2]].Evaluate(va.currentTime);
                    rate = Mathf.Clamp(rate, -1f, 1f);
                    va.SetKeyframe(muscleCurves[headMuscleIndexes[2]], va.currentTime, rate);
                }

                for (int i = 0; i < neckMuscleIndexes.Length; i++)
                {
                    var muscleIndex = neckMuscleIndexes[i];
                    va.SetAnimationCurveAnimatorMuscle(muscleIndex, muscleCurves[muscleIndex]);
                }
                for (int i = 0; i < headMuscleIndexes.Length; i++)
                {
                    var muscleIndex = headMuscleIndexes[i];
                    va.SetAnimationCurveAnimatorMuscle(muscleIndex, muscleCurves[muscleIndex]);
                }
            }
            #endregion

            va.calcObject.SetApplyIK(false);

#if !UNITY_2018_3_OR_NEWER
            ikSaveBodyPosition = va.GetAnimationValueAnimatorRootT(va.currentTime) * va.calcObject.animator.humanScale;
            ikSaveBodyRotation = va.GetAnimationValueAnimatorRootQ(va.currentTime);
            if (va.calcObject.animator.applyRootMotion)
                va.calcObject.animator.applyRootMotion = false;
#endif

            var humanoidBones = va.calcObject.humanoidBones;
            var hp = new HumanPose();

            #region Loop
            int loopCount = 1;
            {
                if (va.rootCorrectionMode == VeryAnimation.RootCorrectionMode.Disable)
                {
                    loopCount = Math.Max(loopCount, 4);
                }
                foreach (var data in ikData)
                {
                    if (data.isUpdate &&
                        data.spaceType == AnimatorIKData.SpaceType.Parent &&
                        data.parentBoneIndex >= 0)
                    {
                        loopCount = Math.Max(loopCount, 2);
                        break;
                    }
                }
            }

            va.calcObject.SetApplyIK(true);
            va.calcObject.SetTransformOrigin();
            for (int loop = 0; loop < loopCount; loop++)
            {
                muscleCurvesUpdated.Clear();

                #region Update
                {
                    va.calcObject.SampleAnimation(va.currentClip, va.currentTime);
                }
                #endregion

                #region Options
                {
                    va.calcObject.humanPoseHandler.GetHumanPose(ref hp);
                    #region Virtual Neck
                    if (ikData[(int)IKTarget.Head].isUpdate && humanoidBones[(int)HumanBodyBones.Neck] == null)
                    {
                        for (int dof = 0; dof < 3; dof++)
                        {
                            var muscleNeck = neckMuscleIndexes[dof];
                            var muscleHead = headMuscleIndexes[dof];
                            if (muscleNeck >= 0 && muscleHead >= 0)
                            {
                                hp.muscles[muscleNeck] = hp.muscles[muscleHead] / 2f;
                                hp.muscles[muscleHead] = hp.muscles[muscleHead] / 2f;
                            }
                        }
                    }
                    #endregion
                    for (int i = 0; i < hp.muscles.Length; i++)
                    {
                        var humanoidIndex = (HumanBodyBones)HumanTrait.BoneFromMuscle(i);
                        var target = IsIKBone(humanoidIndex);
                        if (target == IKTarget.None)
                            continue;
                        var data = ikData[(int)target];
                        if (!data.isUpdate)
                            continue;
                        if (data.autoRotation)
                        {
                            if (humanoidIndex == GetEndHumanoidIndex(target))
                                hp.muscles[i] = 0f;
                            else
                            {   //Twist
                                switch (target)
                                {
                                case IKTarget.LeftHand:
                                    if (humanoidIndex == HumanBodyBones.LeftLowerArm && HumanTrait.MuscleFromBone((int)HumanBodyBones.LeftLowerArm, 0) == i)
                                        hp.muscles[i] = 0f;
                                    break;
                                case IKTarget.RightHand:
                                    if (humanoidIndex == HumanBodyBones.RightLowerArm && HumanTrait.MuscleFromBone((int)HumanBodyBones.RightLowerArm, 0) == i)
                                        hp.muscles[i] = 0f;
                                    break;
                                case IKTarget.LeftFoot:
                                    if (humanoidIndex == HumanBodyBones.LeftLowerLeg && HumanTrait.MuscleFromBone((int)HumanBodyBones.LeftLowerLeg, 0) == i)
                                        hp.muscles[i] = 0f;
                                    break;
                                case IKTarget.RightFoot:
                                    if (humanoidIndex == HumanBodyBones.RightLowerLeg && HumanTrait.MuscleFromBone((int)HumanBodyBones.RightLowerLeg, 0) == i)
                                        hp.muscles[i] = 0f;
                                    break;
                                }
                            }
                        }
                        if (va.clampMuscle)
                        {
                            hp.muscles[i] = Mathf.Clamp(hp.muscles[i], -1f, 1f);
                        }
                    }
                    va.calcObject.ResetTranformRoot();
                    va.calcObject.humanPoseHandler.SetHumanPose(ref hp);
                }
                #endregion

                #region SetKeyframe
                {
                    va.calcObject.humanPoseHandler.GetHumanPose(ref hp);
                    #region Virtual Neck
                    if (ikData[(int)IKTarget.Head].isUpdate && humanoidBones[(int)HumanBodyBones.Neck] == null)
                    {
                        for (int dof = 0; dof < 3; dof++)
                        {
                            var muscleNeck = neckMuscleIndexes[dof];
                            var muscleHead = headMuscleIndexes[dof];
                            if (muscleNeck >= 0 && muscleHead >= 0)
                            {
                                hp.muscles[muscleNeck] = hp.muscles[muscleHead] / 2f;
                                hp.muscles[muscleHead] = hp.muscles[muscleHead] / 2f;
                            }
                        }
                    }
                    #endregion
                    for (int i = 0; i < hp.muscles.Length; i++)
                    {
                        var humanoidIndex = (HumanBodyBones)HumanTrait.BoneFromMuscle(i);
                        var target = IsIKBone(humanoidIndex);
                        if (target == IKTarget.None || !ikData[(int)target].isUpdate)
                            continue;
                        if (muscleCurves[i] == null)
                            muscleCurves[i] = va.GetAnimationCurveAnimatorMuscle(i);
                        va.SetKeyframe(muscleCurves[i], va.currentTime, hp.muscles[i]);
                        muscleCurvesUpdated.Add(i);
                    }
                    if (rootUpdated)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            if (rootTCurves[i] == null)
                                rootTCurves[i] = va.GetAnimationCurveAnimatorRootT(i);
                            va.SetKeyframe(rootTCurves[i], va.currentTime, hp.bodyPosition[i]);
                        }
                        for (int i = 0; i < 4; i++)
                        {
                            if (rootQCurves[i] == null)
                                rootQCurves[i] = va.GetAnimationCurveAnimatorRootQ(i);
                            va.SetKeyframe(rootQCurves[i], va.currentTime, hp.bodyRotation[i]);
                        }
                    }
                }
                #endregion

                #region Write
                {
                    foreach (var i in muscleCurvesUpdated)
                    {
                        va.SetAnimationCurveAnimatorMuscle(i, muscleCurves[i]);
                    }
                    if (rootUpdated)
                    {
                        for (int i = 0; i < 3; i++)
                            va.SetAnimationCurveAnimatorRootT(i, rootTCurves[i]);
                        for (int i = 0; i < 4; i++)
                            va.SetAnimationCurveAnimatorRootQ(i, rootQCurves[i]);
                    }
                }
                #endregion
            }
            #endregion

#if !UNITY_2018_3_OR_NEWER
            if (va.calcObject.animator.applyRootMotion != vaw.animator.applyRootMotion)
                va.calcObject.animator.applyRootMotion = vaw.animator.applyRootMotion;
#endif

            va.calcObject.SetApplyIK(false);
            va.calcObject.SetTransformStart();

#if VERYANIMATION_ANIMATIONRIGGING
            if (writeAnimationRigging)
            {
                va.calcObject.SampleAnimation(va.currentClip, va.currentTime);
                for (int target = 0; target < ikData.Length; target++)
                {
                    if (ikData[target].isUpdate)
                        WriteAnimationRiggingConstraint((IKTarget)target, va.currentTime);
                }
            }
#endif
        }

        public void HandleGUI()
        {
            if (!va.isHuman) return;

            if (ikActiveTarget != IKTarget.None && ikData[(int)ikActiveTarget].enable)
            {
                var activeData = ikData[(int)ikActiveTarget];
                var worldPosition = activeData.worldPosition;
                var worldRotation = activeData.worldRotation;
                var hiA = GetStartHumanoidIndex(ikActiveTarget);
                {
                    if (ikActiveTarget == IKTarget.Head)
                    {
                        #region IKSwivel
                        var posA = va.editHumanoidBones[(int)hiA].transform.position;
                        var posB = worldPosition;
                        var axis = posB - posA;
                        axis.Normalize();
                        if (axis.sqrMagnitude > 0f)
                        {
                            var posP = Vector3.Lerp(posA, posB, 0.5f);
                            Vector3 posPC;
                            {
                                var tpos = posP;
                                {
                                    var post = va.GetAvatarPostRotation(HumanBodyBones.Head);
                                    var up = (va.editHumanoidBones[(int)HumanBodyBones.Head].transform.rotation * post) * Vector3.right;
                                    tpos += up;
                                }
                                Vector3 vec;
                                vec = tpos - posP;
                                var length = Vector3.Dot(vec, axis);
                                posPC = tpos - axis * length;
                            }
                            {
                                Handles.color = new Color(Handles.centerColor.r, Handles.centerColor.g, Handles.centerColor.b, Handles.centerColor.a * 0.5f);
                                Handles.DrawWireDisc(posP, axis, HandleUtility.GetHandleSize(posP));
                                {
                                    Handles.color = Handles.centerColor;
                                    Handles.DrawLine(posP, posP + (posPC - posP).normalized * HandleUtility.GetHandleSize(posP));
                                }
                            }
                            {
                                EditorGUI.BeginChangeCheck();
                                Handles.color = Handles.yAxisColor;
                                var rotDofDistSave = uDisc.GetRotationDist();
                                Handles.Disc(Quaternion.identity, posP, axis, HandleUtility.GetHandleSize(posP), true, uSnapSettings.rotation);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(vaw, "Rotate IK Swivel");
                                    var rotDist = uDisc.GetRotationDist() - rotDofDistSave;
                                    foreach (var target in ikTargetSelect)
                                    {
                                        var data = ikData[(int)target];
                                        data.swivelRotation = Mathf.Repeat(data.swivelRotation - rotDist + 180f, 360f) - 180f;
                                        UpdateSwivelPosition(target);
                                        va.SetUpdateIKtargetAnimatorIK(target);
                                    }
                                }
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        #region IKSwivel
                        var posA = va.editHumanoidBones[(int)hiA].transform.position;
                        var posB = worldPosition;
                        var axis = posB - posA;
                        axis.Normalize();
                        if (axis.sqrMagnitude > 0f)
                        {
                            var posP = Vector3.Lerp(posA, posB, 0.5f);
                            {
                                Handles.color = new Color(Handles.centerColor.r, Handles.centerColor.g, Handles.centerColor.b, Handles.centerColor.a * 0.5f);
                                Handles.DrawWireDisc(posP, axis, HandleUtility.GetHandleSize(posP));

                                var posPC = posA + axis * Vector3.Dot((activeData.swivelPosition - posA), axis);
                                Handles.color = Handles.centerColor;
                                Handles.DrawLine(posP, posP + (activeData.swivelPosition - posPC).normalized * HandleUtility.GetHandleSize(posP));

                                //DebugSwivel
                                //Handles.color = Color.red;
                                //Handles.DrawLine(posP, activeData.swivelPosition);
                            }
                            {
                                EditorGUI.BeginChangeCheck();
                                Handles.color = Handles.zAxisColor;
                                var rotDofDistSave = uDisc.GetRotationDist();
                                Handles.Disc(Quaternion.identity, posP, axis, HandleUtility.GetHandleSize(posP), true, uSnapSettings.rotation);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(vaw, "Rotate IK Swivel");
                                    var rotDist = uDisc.GetRotationDist() - rotDofDistSave;
                                    foreach (var target in ikTargetSelect)
                                    {
                                        var data = ikData[(int)target];
                                        data.swivelRotation = Mathf.Repeat(data.swivelRotation - rotDist + 180f, 360f) - 180f;
                                        UpdateSwivelPosition(target);
                                        va.SetUpdateIKtargetAnimatorIK(target);
                                    }
                                }
                            }
                        }
                        #endregion
                    }
                    if (ikActiveTarget != IKTarget.Head &&
                        !activeData.autoRotation && va.lastTool != Tool.Move)
                    {
                        #region Rotate
                        EditorGUI.BeginChangeCheck();
                        var rotation = Handles.RotationHandle(Tools.pivotRotation == PivotRotation.Local ? worldRotation : Tools.handleRotation, worldPosition);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Rotate IK Target");
                            Action<Quaternion> RotationAction = (move) =>
                            {
                                foreach (var target in ikTargetSelect)
                                {
                                    var data = ikData[(int)target];
                                    data.worldRotation = data.worldRotation * move;
                                    UpdateOptionPosition(target);
                                    UpdateSwivelPosition(target);
                                    va.SetUpdateIKtargetAnimatorIK(target);
                                }
                            };
                            if (Tools.pivotRotation == PivotRotation.Local)
                            {
                                var move = Quaternion.Inverse(worldRotation) * rotation;
                                RotationAction(move);
                            }
                            else
                            {
                                float angle;
                                Vector3 axis;
                                (Quaternion.Inverse(Tools.handleRotation) * rotation).ToAngleAxis(out angle, out axis);
                                var move = Quaternion.Inverse(worldRotation) * Quaternion.AngleAxis(angle, Tools.handleRotation * axis) * worldRotation;
                                RotationAction(move);
                                Tools.handleRotation = rotation;
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        #region Move
                        Handles.color = Color.white;
                        EditorGUI.BeginChangeCheck();
                        var position = Handles.PositionHandle(worldPosition, Tools.pivotRotation == PivotRotation.Local ? worldRotation : Quaternion.identity);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Move IK Target");
                            var move = position - worldPosition;
                            foreach (var target in ikTargetSelect)
                            {
                                ikData[(int)target].worldPosition = ikData[(int)target].worldPosition + move;
                                UpdateOptionPosition(target);
                                UpdateSwivelPosition(target);
                                va.SetUpdateIKtargetAnimatorIK(target);
                            }
                        }
                        #endregion
                    }
                    if (!activeData.autoRotation &&
                        ((ikActiveTarget == IKTarget.LeftFoot && va.editHumanoidBones[(int)HumanBodyBones.LeftToes] != null) ||
                       (ikActiveTarget == IKTarget.RightFoot && va.editHumanoidBones[(int)HumanBodyBones.RightToes] != null)))
                    {
                        {
                            Handles.color = Handles.centerColor;
                            Handles.DrawLine(worldPosition, activeData.optionPosition);
                        }
                        {
                            Handles.color = Color.white;
                            EditorGUI.BeginChangeCheck();
                            var handlePosition = Handles.PositionHandle(activeData.optionPosition, Tools.pivotRotation == PivotRotation.Local ? activeData.optionRotation : Quaternion.identity);
                            if (EditorGUI.EndChangeCheck())
                            {
                                var toesTransform = ikActiveTarget == IKTarget.LeftFoot ? va.editHumanoidBones[(int)HumanBodyBones.LeftToes].transform : va.editHumanoidBones[(int)HumanBodyBones.RightToes].transform;
                                var toesPos = toesTransform.position;
                                var beforeVec = activeData.optionPosition - toesPos;
                                var afterVec = handlePosition - toesPos;
                                beforeVec.Normalize();
                                afterVec.Normalize();
                                if (beforeVec.sqrMagnitude > 0f && afterVec.sqrMagnitude > 0f)
                                {
                                    Quaternion rotationY = Quaternion.identity;
                                    {
                                        var normal = activeData.worldRotation * Vector3.up;
                                        var beforeP = activeData.optionPosition - normal * Vector3.Dot(activeData.optionPosition - worldPosition, normal);
                                        var afterP = handlePosition - normal * Vector3.Dot(handlePosition - worldPosition, normal);
                                        rotationY = Quaternion.AngleAxis(Vector3.SignedAngle((beforeP - toesPos).normalized, (afterP - toesPos).normalized, normal), normal);
                                    }
                                    Quaternion rotationX = Quaternion.identity;
                                    {
                                        var normal = activeData.worldRotation * Vector3.right;
                                        var beforeP = activeData.optionPosition - normal * Vector3.Dot(activeData.optionPosition - worldPosition, normal);
                                        var afterP = handlePosition - normal * Vector3.Dot(handlePosition - worldPosition, normal);
                                        rotationX = Quaternion.AngleAxis(Vector3.SignedAngle((beforeP - toesPos).normalized, (afterP - toesPos).normalized, normal), normal);
                                    }
                                    var rotation = rotationX * rotationY;
                                    var afterPosition = toesPos + rotation * (worldPosition - toesPos);
                                    var movePosition = afterPosition - worldPosition;
                                    var moveRotation = rotation;
                                    foreach (var target in ikTargetSelect)
                                    {
                                        ikData[(int)target].worldPosition = ikData[(int)target].worldPosition + movePosition;
                                        ikData[(int)target].worldRotation = moveRotation * ikData[(int)target].worldRotation;
                                        va.SetUpdateIKtargetAnimatorIK(target);
                                        if (target == IKTarget.LeftFoot || target == IKTarget.RightFoot)
                                        {
                                            toesTransform.rotation = Quaternion.Inverse(moveRotation) * toesTransform.rotation;
                                            HumanPose hpAfter = new HumanPose();
                                            va.GetEditGameObjectHumanPose(ref hpAfter);
                                            var muscleIndex = target == IKTarget.LeftFoot ? HumanTrait.MuscleFromBone((int)HumanBodyBones.LeftToes, 2) : HumanTrait.MuscleFromBone((int)HumanBodyBones.RightToes, 2);
                                            var muscle = hpAfter.muscles[muscleIndex];
                                            if (va.clampMuscle)
                                                muscle = Mathf.Clamp(muscle, -1f, 1f);
                                            va.SetAnimationValueAnimatorMuscle(muscleIndex, muscle);
                                        }
                                    }
                                }
                                activeData.optionPosition = handlePosition;
                                UpdateSwivelPosition(ikActiveTarget);
                            }
                        }
                    }
                }
            }
        }
        public void TargetGUI()
        {
            if (!va.isHuman) return;

            var e = Event.current;

            for (int target = 0; target < (int)IKTarget.Total; target++)
            {
                if (!ikData[target].enable) continue;

                var worldPosition = ikData[target].worldPosition;
                var worldRotation = ikData[target].worldRotation;
                var ikTarget = (IKTarget)target;
                var hiA = GetStartHumanoidIndex(ikTarget);
                if (ikTargetSelect != null &&
                    EditorCommon.ArrayContains(ikTargetSelect, ikTarget))
                {
                    #region Active
                    {
                        if (ikTarget == ikActiveTarget)
                        {
                            Handles.color = Color.white;
                            var hiA2 = hiA;
                            if (target == (int)IKTarget.Head)
                            {
                                if (va.editHumanoidBones[(int)HumanBodyBones.Neck] != null)
                                    hiA2 = HumanBodyBones.Neck;
                                else
                                    hiA2 = HumanBodyBones.Head;
                            }
                            Vector3 worldPosition2 = va.editHumanoidBones[(int)hiA2].transform.position;
                            Handles.DrawLine(worldPosition, worldPosition2);
                        }
                        Handles.color = vaw.editorSettings.settingIKTargetActiveColor;
                        if (ikTarget == IKTarget.Head)
                            Handles.SphereHandleCap(0, worldPosition, worldRotation, HandleUtility.GetHandleSize(worldPosition) * vaw.editorSettings.settingIKTargetSize, EventType.Repaint);
                        else
                            Handles.ConeHandleCap(0, worldPosition, worldRotation, HandleUtility.GetHandleSize(worldPosition) * vaw.editorSettings.settingIKTargetSize, EventType.Repaint);
                    }
                    #endregion
                }
                else
                {
                    #region NonActive
                    var freeMoveHandleControlID = -1;
#if UNITY_2022_1_OR_NEWER
                    Handles.FreeMoveHandle(worldPosition, HandleUtility.GetHandleSize(worldPosition) * vaw.editorSettings.settingIKTargetSize, uSnapSettings.move, (id, pos, rot, size, eventType) =>
#else
                    Handles.FreeMoveHandle(worldPosition, worldRotation, HandleUtility.GetHandleSize(worldPosition) * vaw.editorSettings.settingIKTargetSize, uSnapSettings.move, (id, pos, rot, size, eventType) =>
#endif
                    {
                        freeMoveHandleControlID = id;
                        Handles.color = vaw.editorSettings.settingIKTargetNormalColor;
                        if (ikTarget == IKTarget.Head)
                            Handles.SphereHandleCap(id, worldPosition, worldRotation, HandleUtility.GetHandleSize(worldPosition) * vaw.editorSettings.settingIKTargetSize, eventType);
                        else
                            Handles.ConeHandleCap(id, worldPosition, worldRotation, HandleUtility.GetHandleSize(worldPosition) * vaw.editorSettings.settingIKTargetSize, eventType);
                    });
                    if (GUIUtility.hotControl == freeMoveHandleControlID)
                    {
                        if (e.type == EventType.Layout)
                        {
                            GUIUtility.hotControl = -1;
                            {
                                var ikTargetTmp = ikTarget;
                                EditorApplication.delayCall += () =>
                                {
                                    va.SelectAnimatorIKTargetPlusKey(ikTargetTmp);
                                };
                            }
                        }
                    }
                    #endregion
                }
            }
        }
        public void SelectionGUI()
        {
            if (!va.isHuman) return;
            if (ikActiveTarget == IKTarget.None) return;
            var activeData = ikData[(int)ikActiveTarget];
            #region Warning
            if (!IsValid(ikActiveTarget))
            {
                EditorGUILayout.HelpBox(Language.GetText(Language.Help.AnimatorIKScaleChangedErrorWarning), MessageType.Warning);
            }
            #endregion
            #region IK
            {
                EditorGUILayout.BeginHorizontal();
                #region Mirror
                {
                    var mirrorTarget = IKTargetMirror[(int)ikActiveTarget];
                    if (GUILayout.Button(Language.GetContentFormat(Language.Help.SelectionMirror, (mirrorTarget != IKTarget.None ? string.Format("From 'IK: {0}'", IKTargetStrings[(int)mirrorTarget]) : "From self"))))
                    {
                        va.SetSelectionMirror();
                    }
                }
                #endregion
                EditorGUILayout.Space();
                #region Update
                if (GUILayout.Button(Language.GetContent(Language.Help.SelectionUpdateIK)))
                {
                    Undo.RecordObject(vaw, "Update IK");
                    foreach (var target in ikTargetSelect)
                    {
                        va.SetUpdateIKtargetAnimatorIK(target);
                    }
                }
                #endregion
                EditorGUILayout.Space();
                #region Sync
                {
                    EditorGUI.BeginDisabledGroup(activeData.spaceType == AnimatorIKData.SpaceType.Parent);
                    if (GUILayout.Button(Language.GetContent(Language.Help.SelectionSyncIK), vaw.guiStyleDropDown))
                    {
                        GenericMenu menu = new GenericMenu();
                        {
                            menu.AddItem(new GUIContent("Default"), false, () =>
                            {
                                Undo.RecordObject(vaw, "Sync IK");
                                foreach (var target in ikTargetSelect)
                                    SynchroSet(target);
                                SceneView.RepaintAll();
                            });
                            menu.AddSeparator(string.Empty);
#if VERYANIMATION_ANIMATIONRIGGING
                            if (va.animationRigging.isValid)
                            {
                                menu.AddItem(new GUIContent("Animation Rigging (Constraint target curves)"), false, () =>
                                {
                                    Undo.RecordObject(vaw, "Sync IK");
                                    foreach (var target in ikTargetSelect)
                                        SynchroSet(target, SynchroSetFlags.AnimationRigging);
                                    SceneView.RepaintAll();
                                });
                            }
#endif
                            if (va.isHuman && ikActiveTarget >= IKTarget.LeftHand && ikActiveTarget <= IKTarget.RightFoot)
                            {
                                menu.AddItem(new GUIContent("Humanoid IK (Foot IK or Hand IK curves)"), false, () =>
                                {
                                    Undo.RecordObject(vaw, "Sync IK");
                                    foreach (var target in ikTargetSelect)
                                        SynchroSet(target, SynchroSetFlags.HandIK | SynchroSetFlags.FootIK);
                                    SceneView.RepaintAll();
                                });
                            }
                            menu.AddItem(new GUIContent("Animation"), false, () =>
                            {
                                Undo.RecordObject(vaw, "Sync IK");
                                foreach (var target in ikTargetSelect)
                                    SynchroSet(target, SynchroSetFlags.None);
                                SceneView.RepaintAll();
                            });
                        }
                        menu.ShowAsContext();
                    }
                    EditorGUI.EndDisabledGroup();
                }
                #endregion
                EditorGUILayout.Space();
                #region Reset
                if (GUILayout.Button(Language.GetContent(Language.Help.SelectionResetIK)))
                {
                    Undo.RecordObject(vaw, "Reset IK");
                    foreach (var target in ikTargetSelect)
                    {
                        Reset(target);
                    }
                }
                #endregion
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space();
            int RowCount = 0;
            #region SpaceType
            {
                EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                EditorGUILayout.LabelField("Space", GUILayout.Width(60));
                EditorGUI.BeginChangeCheck();
                var spaceType = (AnimatorIKData.SpaceType)GUILayout.Toolbar((int)activeData.spaceType, IKSpaceTypeStrings, EditorStyles.miniButton);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(vaw, "Change IK Position");
                    foreach (var target in ikTargetSelect)
                    {
                        ChangeSpaceType(target, spaceType);
                    }
                    VeryAnimationControlWindow.instance.Repaint();
                }
                EditorGUILayout.EndHorizontal();
            }
            #endregion
            #region Parent
            if (activeData.spaceType == AnimatorIKData.SpaceType.Local || activeData.spaceType == AnimatorIKData.SpaceType.Parent)
            {
                EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                EditorGUILayout.LabelField("Parent", GUILayout.Width(60));
                EditorGUI.BeginChangeCheck();
                if (activeData.spaceType == AnimatorIKData.SpaceType.Local)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    var parent = va.bones[va.humanoidIndex2boneIndex[(int)GetStartHumanoidIndex(ikActiveTarget)]].transform.parent;
                    EditorGUILayout.ObjectField(parent != null ? parent.gameObject : null, typeof(GameObject), true);
                    EditorGUI.EndDisabledGroup();
                }
                else if (activeData.spaceType == AnimatorIKData.SpaceType.Parent)
                {
                    var parent = EditorGUILayout.ObjectField(activeData.parent, typeof(GameObject), true) as GameObject;
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change IK Position");
                        foreach (var target in ikTargetSelect)
                        {
                            var data = ikData[(int)target];
                            var worldPosition = data.worldPosition;
                            var worldRotation = data.worldRotation;
                            data.parent = parent;
                            data.worldPosition = worldPosition;
                            data.worldRotation = worldRotation;
                            va.SetSynchroIKtargetAnimatorIK(target);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            #endregion
            #region Position
            {
                EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                EditorGUILayout.LabelField("Position", GUILayout.Width(60));
                EditorGUI.BeginChangeCheck();
                var position = EditorGUILayout.Vector3Field("", activeData.position);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(vaw, "Change IK Position");
                    var move = position - activeData.position;
                    foreach (var target in ikTargetSelect)
                    {
                        ikData[(int)target].position += move;
                        va.SetUpdateIKtargetAnimatorIK(target);
                    }
                }
                if (activeData.spaceType == AnimatorIKData.SpaceType.Parent)
                {
                    if (GUILayout.Button("Reset", GUILayout.Width(44)))
                    {
                        Undo.RecordObject(vaw, "Change IK Position");
                        foreach (var target in ikTargetSelect)
                        {
                            ikData[(int)target].position = Vector3.zero;
                            va.SetUpdateIKtargetAnimatorIK(target);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            #endregion
            if (ikActiveTarget > IKTarget.Head)
            {
                #region Rotation
                {
                    EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                    {
                        EditorGUI.BeginChangeCheck();
                        var autoRotation = !GUILayout.Toggle(!activeData.autoRotation, "Rotation", EditorStyles.toolbarButton, GUILayout.Width(63));
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change IK Rotation");
                            foreach (var target in ikTargetSelect)
                            {
                                ikData[(int)target].autoRotation = autoRotation;
                                SynchroSet(target);
                                va.SetUpdateIKtargetAnimatorIK(target);
                            }
                        }
                    }
                    if (!activeData.autoRotation)
                    {
                        EditorGUI.BeginChangeCheck();
                        var eulerAngles = EditorGUILayout.Vector3Field("", activeData.rotation.eulerAngles);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change IK Rotation");
                            var move = eulerAngles - activeData.rotation.eulerAngles;
                            foreach (var target in ikTargetSelect)
                            {
                                if (target >= IKTarget.LeftHand && target <= IKTarget.RightFoot)
                                {
                                    ikData[(int)target].rotation.eulerAngles += move;
                                    va.SetUpdateIKtargetAnimatorIK(target);
                                }
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Auto", EditorStyles.centeredGreyMiniLabel);
                    }
                    if (activeData.spaceType == AnimatorIKData.SpaceType.Parent)
                    {
                        if (GUILayout.Button("Reset", GUILayout.Width(44)))
                        {
                            Undo.RecordObject(vaw, "Change IK Rotation");
                            foreach (var target in ikTargetSelect)
                            {
                                ikData[(int)target].rotation = Quaternion.identity;
                                va.SetUpdateIKtargetAnimatorIK(target);
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            #region Swivel
            {
                EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                EditorGUILayout.LabelField("Swivel", GUILayout.Width(60));
                EditorGUI.BeginChangeCheck();
                var swivelRotation = EditorGUILayout.Slider(activeData.swivelRotation, -180f, 180f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(vaw, "Change IK Swivel");
                    var move = swivelRotation - activeData.swivelRotation;
                    foreach (var target in ikTargetSelect)
                    {
                        var data = ikData[(int)target];
                        data.swivelRotation = Mathf.Repeat(data.swivelRotation + move + 180f, 360f) - 180f;
                        UpdateSwivelPosition(target);
                        va.SetUpdateIKtargetAnimatorIK(target);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            #endregion
#if VERYANIMATION_ANIMATIONRIGGING
            #region AnimationRiggingConstraint
            if (activeData.rigConstraint != null)
            {
                EditorGUILayout.BeginVertical(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space();
                if (GUILayout.Button(activeData.rigConstraintGUIContent))
                {
                    var list = new List<EditorCurveBinding>();
                    foreach (var target in ikTargetSelect)
                    {
                        list.AddRange(GetAnimationRiggingConstraintBindings(target));
                        list.Add(ikData[(int)target].rigConstraintWeight);
                    }
                    va.SetAnimationWindowSynchroSelection(list.ToArray());
                }
                EditorGUILayout.Space();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("Source", "Source Objects")))
                {
                    var list = new List<EditorCurveBinding>();
                    foreach (var target in ikTargetSelect)
                    {
                        list.AddRange(GetAnimationRiggingConstraintBindings(target));
                    }
                    va.SetAnimationWindowSynchroSelection(list.ToArray());
                }
                GUILayout.Space(8);
                if (GUILayout.Button(new GUIContent("Weight", "IRigConstraint.weight")))
                {
                    var list = new List<EditorCurveBinding>();
                    foreach (var target in ikTargetSelect)
                    {
                        list.Add(ikData[(int)target].rigConstraintWeight);
                    }
                    va.SetAnimationWindowSynchroSelection(list.ToArray());
                }
                {
                    var rigConstraint = activeData.rigConstraint as IRigConstraint;
                    EditorGUI.BeginChangeCheck();
                    var weight = EditorGUILayout.Slider(va.GetAnimationValueCustomProperty(activeData.rigConstraintWeight), 0f, 1f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        foreach (var target in ikTargetSelect)
                        {
                            if (ikData[(int)target].rigConstraint != null)
                                va.SetAnimationValueCustomProperty(ikData[(int)target].rigConstraintWeight, weight);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            #endregion
#endif
            #endregion
        }
        public void ControlGUI()
        {
            if (!va.isHuman) return;

            EditorGUILayout.BeginVertical(vaw.guiStyleSkinBox);
            if (ikReorderableList != null)
            {
                ikReorderableList.DoLayoutList();
                GUILayout.Space(-14);
                if (advancedFoldout && ikReorderableList.index >= 0 && ikReorderableList.index < ikData.Length)
                {
                    var target = ikReorderableList.index;
                    EditorGUI.BeginDisabledGroup(!ikData[target].enable);
#if !VERYANIMATION_ANIMATIONRIGGING
                    if ((IKTarget)target == IKTarget.Head)
#endif
                    {
                        advancedFoldout = EditorGUILayout.Foldout(advancedFoldout, "Advanced", true);
                        #region Head
                        if ((IKTarget)target == IKTarget.Head)
                        {
                            EditorGUILayout.BeginVertical(vaw.guiStyleSkinBox);
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("Look At Weight");
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("Reset"))
                                {
                                    Undo.RecordObject(vaw, "Change Animator IK Data");
                                    ikData[target].headWeight = 1f;
                                    ikData[target].eyesWeight = 0f;
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                            EditorGUI.indentLevel++;
                            {
                                EditorGUI.BeginChangeCheck();
                                var weight = EditorGUILayout.Slider("Head Weight", ikData[target].headWeight, 0f, 1f);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(vaw, "Change Animator IK Data");
                                    ikData[target].headWeight = weight;
                                    ikData[target].eyesWeight = 1f - ikData[target].headWeight;
                                }
                            }
                            {
                                EditorGUI.BeginChangeCheck();
                                var weight = EditorGUILayout.Slider("Eyes Weight", ikData[target].eyesWeight, 0f, 1f);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(vaw, "Change Animator IK Data");
                                    ikData[target].eyesWeight = weight;
                                    ikData[target].headWeight = 1f - ikData[target].eyesWeight;
                                }
                            }
                            EditorGUI.indentLevel--;
                            EditorGUILayout.EndVertical();
                        }
                        #endregion
#if VERYANIMATION_ANIMATIONRIGGING
                        #region AnimationRigging
                        {
                            EditorGUILayout.BeginHorizontal(vaw.guiStyleSkinBox);
                            EditorGUILayout.LabelField("Animation Rigging Constraint");
                            GUILayout.FlexibleSpace();
                            {
                                EditorGUI.BeginDisabledGroup(ikData[target].rigConstraint != null);
                                if (GUILayout.Button("Create"))
                                {
                                    EditorApplication.delayCall += () =>
                                    {
                                        Undo.RecordObject(vaw, "Change Animation Rigging Constraint");
                                        CreateAnimationRiggingConstraint((IKTarget)target);
                                    };
                                }
                                EditorGUI.EndDisabledGroup();
                            }
                            EditorGUILayout.Space();
                            {
                                EditorGUI.BeginDisabledGroup(ikData[target].rigConstraint == null);
                                if (GUILayout.Button("Delete"))
                                {
                                    EditorApplication.delayCall += () =>
                                    {
                                        Undo.RecordObject(vaw, "Change Animation Rigging Constraint");
                                        DeleteAnimationRiggingConstraint((IKTarget)target);
                                    };
                                }
                                EditorGUI.EndDisabledGroup();
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        #endregion
#endif
                    }
                    EditorGUI.EndDisabledGroup();
                }
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(Language.GetContent(Language.Help.AnimatorIKChangeAll)))
                    {
                        Undo.RecordObject(vaw, "Change Animator IK Data");
                        bool flag = !ikData.Any(v => !v.enable);
                        for (int target = 0; target < ikData.Length; target++)
                        {
                            ikData[target].enable = !flag;
                            if (ikData[target].enable)
                            {
                                SynchroSet((IKTarget)target);
                            }
                        }
                    }
                    EditorGUILayout.Space();
                    if (GUILayout.Button(Language.GetContent(Language.Help.AnimatorIKSelectAll)))
                    {
                        var list = new List<IKTarget>();
                        for (int target = 0; target < ikData.Length; target++)
                        {
                            if (ikData[target].enable)
                                list.Add((IKTarget)target);
                        }
                        va.SelectIKTargets(list.ToArray(), null);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void AnimatorOnAnimatorIK(int layerIndex)
        {
            var animator = va.calcObject.animator;

#if !UNITY_2018_3_OR_NEWER
            {
                animator.rootPosition = Vector3.zero;
                animator.rootRotation = Quaternion.identity;
                animator.bodyPosition = ikSaveBodyPosition;
                animator.bodyRotation = ikSaveBodyRotation;
            }
#endif

            {
                var data = ikData[(int)IKTarget.Head];
                if (data.isUpdate)
                {
                    Vector3 position, hintPosition;
                    Quaternion rotation;
                    GetCalcWorldTransform(IKTarget.Head, out position, out rotation, out hintPosition);

                    animator.SetLookAtPosition(position);
                    animator.SetLookAtWeight(1f, 0f, data.headWeight, data.eyesWeight, 0f);
                }
                else
                {
                    animator.SetLookAtWeight(0f);
                }
            }
            {
                var data = ikData[(int)IKTarget.LeftHand];
                if (data.isUpdate)
                {
                    Vector3 position, hintPosition;
                    Quaternion rotation;
                    GetCalcWorldTransform(IKTarget.LeftHand, out position, out rotation, out hintPosition);

                    animator.SetIKPosition(AvatarIKGoal.LeftHand, position);
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
                    animator.SetIKRotation(AvatarIKGoal.LeftHand, rotation);
                    animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1f);
                    animator.SetIKHintPosition(AvatarIKHint.LeftElbow, hintPosition);
                    animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 1f);
                }
                else
                {
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                    animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                    animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
                }
            }
            {
                var data = ikData[(int)IKTarget.RightHand];
                if (data.isUpdate)
                {
                    Vector3 position, hintPosition;
                    Quaternion rotation;
                    GetCalcWorldTransform(IKTarget.RightHand, out position, out rotation, out hintPosition);

                    animator.SetIKPosition(AvatarIKGoal.RightHand, position);
                    animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
                    animator.SetIKRotation(AvatarIKGoal.RightHand, rotation);
                    animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1f);
                    animator.SetIKHintPosition(AvatarIKHint.RightElbow, hintPosition);
                    animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 1f);
                }
                else
                {
                    animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                    animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                    animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
                }
            }
            {
                var data = ikData[(int)IKTarget.LeftFoot];
                if (data.isUpdate)
                {
                    Vector3 position, hintPosition;
                    Quaternion rotation;
                    GetCalcWorldTransform(IKTarget.LeftFoot, out position, out rotation, out hintPosition);

                    animator.SetIKPosition(AvatarIKGoal.LeftFoot, position);
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1f);
                    animator.SetIKRotation(AvatarIKGoal.LeftFoot, rotation);
                    animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 1f);
                    animator.SetIKHintPosition(AvatarIKHint.LeftKnee, hintPosition);
                    animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, 1f);
                }
                else
                {
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
                    animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
                    animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, 0f);
                }
            }
            {
                var data = ikData[(int)IKTarget.RightFoot];
                if (data.isUpdate)
                {
                    Vector3 position, hintPosition;
                    Quaternion rotation;
                    GetCalcWorldTransform(IKTarget.RightFoot, out position, out rotation, out hintPosition);

                    animator.SetIKPosition(AvatarIKGoal.RightFoot, position);
                    animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1f);
                    animator.SetIKRotation(AvatarIKGoal.RightFoot, rotation);
                    animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 1f);
                    animator.SetIKHintPosition(AvatarIKHint.RightKnee, hintPosition);
                    animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, 1f);
                }
                else
                {
                    animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                    animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
                    animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, 0f);
                }
            }
        }
        private void GetCalcWorldTransform(IKTarget target, out Vector3 position, out Quaternion rotation, out Vector3 hintPosition)
        {
            var data = ikData[(int)target];

            var worldToLocalRotation = Quaternion.Inverse(va.transformPoseSave.startRotation);
            var worldToLocalMatrix = va.transformPoseSave.startMatrix.inverse;

            if (data.spaceType == AnimatorIKData.SpaceType.Parent && data.parentBoneIndex >= 0)
            {
                var parent = va.calcObject.bones[data.parentBoneIndex].transform;
                position = parent.localToWorldMatrix.MultiplyPoint3x4(data.position);
                rotation = parent.rotation * data.rotation;

                var rootEdit = va.editGameObject.transform;
                var rootCalc = va.calcObject.gameObjectTransform;
                var rootOffsetMatrix = (worldToLocalMatrix * rootEdit.localToWorldMatrix) * rootCalc.worldToLocalMatrix;
                position = rootOffsetMatrix.MultiplyPoint3x4(position);
                var rootOffsetRotation = (worldToLocalRotation * rootEdit.rotation) * Quaternion.Inverse(rootCalc.rotation);
                rotation = rootOffsetRotation * rotation;
            }
            else
            {
                position = worldToLocalMatrix.MultiplyPoint3x4(data.worldPosition);
                rotation = worldToLocalRotation * data.worldRotation;
            }

            if (data.autoRotation)
            {
                var hiB = (int)GetEndHumanoidIndex(target);
                var tB = va.editHumanoidBones[hiB].transform;
                rotation = tB.rotation * va.GetAvatarPostRotation((HumanBodyBones)hiB) * IKTargetSyncRotation[(int)target];
                rotation = worldToLocalRotation * rotation;
            }

            hintPosition = worldToLocalMatrix.MultiplyPoint3x4(data.swivelPosition);
        }

        private void Reset(IKTarget target)
        {
            var data = ikData[(int)target];
            switch (target)
            {
            case IKTarget.Head:
                {
                    var t = va.editGameObject.transform;
                    Vector3 vec = data.worldPosition - t.position;
                    var normal = t.rotation * Vector3.right;
                    var dot = Vector3.Dot(vec, normal);
                    data.worldPosition -= normal * dot;
                }
                break;
            case IKTarget.LeftHand:
                {
                    {
                        var posA = va.editHumanoidBones[(int)HumanBodyBones.LeftUpperArm].transform.position;
                        var posB = va.editHumanoidBones[(int)HumanBodyBones.LeftHand].transform.position;
                        var posC = va.editHumanoidBones[(int)HumanBodyBones.LeftLowerArm].transform.position;
                        var up = data.worldPosition - Vector3.Lerp(posA, posB, 0.5f);
                        data.worldRotation = Quaternion.LookRotation(posB - posC, up);
                    }
                }
                break;
            case IKTarget.RightHand:
                {
                    {
                        var posA = va.editHumanoidBones[(int)HumanBodyBones.RightUpperArm].transform.position;
                        var posB = va.editHumanoidBones[(int)HumanBodyBones.RightHand].transform.position;
                        var posC = va.editHumanoidBones[(int)HumanBodyBones.RightLowerArm].transform.position;
                        var up = data.worldPosition - Vector3.Lerp(posA, posB, 0.5f);
                        data.worldRotation = Quaternion.LookRotation(posB - posC, up);
                    }
                }
                break;
            case IKTarget.LeftFoot:
                {
                    var rot = va.editHumanoidBones[(int)HumanBodyBones.Hips].transform.rotation * va.GetAvatarPostRotation(HumanBodyBones.Hips) * Quaternion.Euler(90f, 90f, 0);
                    {
                        var vec = rot * Vector3.forward;
                        rot = Quaternion.LookRotation((new Vector3(vec.x, 0f, vec.z)).normalized, Vector3.up);
                    }
                    data.worldRotation = rot;
                }
                break;
            case IKTarget.RightFoot:
                {
                    var rot = va.editHumanoidBones[(int)HumanBodyBones.Hips].transform.rotation * va.GetAvatarPostRotation(HumanBodyBones.Hips) * Quaternion.Euler(90f, 90f, 0);
                    {
                        var vec = rot * Vector3.forward;
                        rot = Quaternion.LookRotation((new Vector3(vec.x, 0f, vec.z)).normalized, Vector3.up);
                    }
                    data.worldRotation = rot;
                }
                break;
            }
            UpdateOptionPosition(target);
            UpdateSwivelPosition(target);
            va.SetUpdateIKtargetAnimatorIK(target);
        }
        private void ChangeSpaceType(IKTarget target, AnimatorIKData.SpaceType spaceType)
        {
            if (target < 0 || target >= IKTarget.Total) return;
            var data = ikData[(int)target];
            if (data.spaceType == spaceType) return;
            var position = data.worldPosition;
            var rotation = data.worldRotation;
            data.spaceType = spaceType;
            data.worldPosition = position;
            data.worldRotation = rotation;
        }

        public IKTarget IsIKBone(HumanBodyBones hi)
        {
            if (ikData[(int)IKTarget.Head].enable)
            {
                if (ikData[(int)IKTarget.Head].headWeight > 0f)
                    if (hi == HumanBodyBones.Head || hi == HumanBodyBones.Neck)
                        return IKTarget.Head;
                if (ikData[(int)IKTarget.Head].eyesWeight > 0f)
                    if (hi == HumanBodyBones.LeftEye || hi == HumanBodyBones.RightEye)
                        return IKTarget.Head;
            }
            if (ikData[(int)IKTarget.LeftHand].enable)
            {
                if (hi == HumanBodyBones.LeftHand || hi == HumanBodyBones.LeftLowerArm || hi == HumanBodyBones.LeftUpperArm ||
                    (va.humanoidBones[(int)HumanBodyBones.LeftShoulder] == null && hi == HumanBodyBones.LeftShoulder))
                    return IKTarget.LeftHand;
            }
            if (ikData[(int)IKTarget.RightHand].enable)
            {
                if (hi == HumanBodyBones.RightHand || hi == HumanBodyBones.RightLowerArm || hi == HumanBodyBones.RightUpperArm ||
                    (va.humanoidBones[(int)HumanBodyBones.RightShoulder] == null && hi == HumanBodyBones.RightShoulder))
                    return IKTarget.RightHand;
            }
            if (ikData[(int)IKTarget.LeftFoot].enable)
            {
                if (hi == HumanBodyBones.LeftFoot || hi == HumanBodyBones.LeftLowerLeg || hi == HumanBodyBones.LeftUpperLeg)
                    return IKTarget.LeftFoot;
            }
            if (ikData[(int)IKTarget.RightFoot].enable)
            {
                if (hi == HumanBodyBones.RightFoot || hi == HumanBodyBones.RightLowerLeg || hi == HumanBodyBones.RightUpperLeg)
                    return IKTarget.RightFoot;
            }
            return IKTarget.None;
        }

        public void ChangeTargetIK(IKTarget target)
        {
            Undo.RecordObject(vaw, "Change IK");
            if (ikData[(int)target].enable)
            {
                List<GameObject> selectGameObjects = new List<GameObject>();
                switch (target)
                {
                case IKTarget.Head:
                    ikData[(int)target].enable = false;
                    selectGameObjects.Add(va.humanoidBones[(int)HumanBodyBones.Head]);
                    break;
                case IKTarget.LeftHand:
                    ikData[(int)target].enable = false;
                    selectGameObjects.Add(va.humanoidBones[(int)HumanBodyBones.LeftHand]);
                    break;
                case IKTarget.RightHand:
                    ikData[(int)target].enable = false;
                    selectGameObjects.Add(va.humanoidBones[(int)HumanBodyBones.RightHand]);
                    break;
                case IKTarget.LeftFoot:
                    ikData[(int)target].enable = false;
                    selectGameObjects.Add(va.humanoidBones[(int)HumanBodyBones.LeftFoot]);
                    break;
                case IKTarget.RightFoot:
                    ikData[(int)target].enable = false;
                    selectGameObjects.Add(va.humanoidBones[(int)HumanBodyBones.RightFoot]);
                    break;
                }
                va.SelectGameObjects(selectGameObjects.ToArray());
            }
            else
            {
                ikData[(int)target].enable = true;
                SynchroSet(target);
                va.SelectAnimatorIKTargetPlusKey(target);
            }
        }
        public bool ChangeSelectionIK()
        {
            Undo.RecordObject(vaw, "Change IK");
            bool changed = false;
            if (ikTargetSelect != null && ikTargetSelect.Length > 0)
            {
                List<GameObject> selectGameObjects = new List<GameObject>();
                foreach (var target in ikTargetSelect)
                {
                    switch (target)
                    {
                    case IKTarget.Head:
                        ikData[(int)target].enable = false;
                        selectGameObjects.Add(va.humanoidBones[(int)HumanBodyBones.Head]);
                        changed = true;
                        break;
                    case IKTarget.LeftHand:
                        ikData[(int)target].enable = false;
                        selectGameObjects.Add(va.humanoidBones[(int)HumanBodyBones.LeftHand]);
                        changed = true;
                        break;
                    case IKTarget.RightHand:
                        ikData[(int)target].enable = false;
                        selectGameObjects.Add(va.humanoidBones[(int)HumanBodyBones.RightHand]);
                        changed = true;
                        break;
                    case IKTarget.LeftFoot:
                        ikData[(int)target].enable = false;
                        selectGameObjects.Add(va.humanoidBones[(int)HumanBodyBones.LeftFoot]);
                        changed = true;
                        break;
                    case IKTarget.RightFoot:
                        ikData[(int)target].enable = false;
                        selectGameObjects.Add(va.humanoidBones[(int)HumanBodyBones.RightFoot]);
                        changed = true;
                        break;
                    }
                }
                if (changed)
                    va.SelectGameObjects(selectGameObjects.ToArray());
            }
            else
            {
                HashSet<IKTarget> selectIkTargets = new HashSet<IKTarget>();
                foreach (var humanoidIndex in va.SelectionGameObjectsHumanoidIndex())
                {
                    var target = HumanBonesUpdateAnimatorIK[(int)humanoidIndex];
                    if (target < 0 || target >= IKTarget.Total)
                        continue;
                    selectIkTargets.Add(target);
                    changed = true;
                }
                if (changed)
                {
                    foreach (var target in selectIkTargets)
                    {
                        ikData[(int)target].enable = true;
                        SynchroSet(target);
                    }
                    va.SelectIKTargets(selectIkTargets.ToArray(), null);
                }
            }
            return changed;
        }

        public void SetUpdateIKtargetBone(int boneIndex)
        {
            if (boneIndex < 0)
                return;
            {
                var humanoidIndex = va.boneIndex2humanoidIndex[boneIndex];
                if (humanoidIndex >= 0)
                    SetUpdateIKtargetAnimatorIK(HumanBonesUpdateAnimatorIK[(int)humanoidIndex]);
            }
            SetUpdateLinkedIKTarget(boneIndex);
        }
        public void SetUpdateIKtargetAnimatorIK(IKTarget target)
        {
            if (target <= IKTarget.None || ikData == null) return;
            if (target == IKTarget.Total || va.rootCorrectionMode == VeryAnimation.RootCorrectionMode.Disable)
            {
                va.SetUpdateIKtargetAll();
            }
            else
            {
                ikData[(int)target].updateIKtarget = true;

                SetUpdateLinkedIKTarget(ikData[(int)target].rootBoneIndex);
            }
        }
        private void SetUpdateLinkedIKTarget(int boneIndex)
        {
            if (boneIndex < 0)
                return;
            foreach (var data in ikData)
            {
                if (data.updateIKtarget)
                    continue;
                if (data.spaceType == AnimatorIKData.SpaceType.Parent &&
                    data.parentBoneIndex >= 0)
                {
                    var index = data.parentBoneIndex;
                    while (index >= 0)
                    {
                        if (boneIndex == index)
                        {
                            data.updateIKtarget = true;
                            break;
                        }
                        index = va.parentBoneIndexes[index];
                    }
                }
            }
        }
        public void ResetUpdateIKtargetAll()
        {
            if (ikData == null) return;
            foreach (var data in ikData)
            {
                data.updateIKtarget = false;
            }
        }
        public void SetUpdateIKtargetAll()
        {
            if (ikData == null) return;
            foreach (var data in ikData)
            {
                data.updateIKtarget = true;
            }
        }
        public bool GetUpdateIKtargetAll()
        {
            if (ikData == null) return false;
            foreach (var data in ikData)
            {
                if (data.isUpdate)
                    return true;
            }
            return false;
        }

        public void SetSynchroIKtargetBone(int boneIndex)
        {
            if (boneIndex < 0) return;
            var humanoidIndex = va.boneIndex2humanoidIndex[boneIndex];
            if (humanoidIndex < 0) return;
            SetSynchroIKtargetAnimatorIK(HumanBonesUpdateAnimatorIK[(int)humanoidIndex]);
        }
        public void SetSynchroIKtargetAnimatorIK(IKTarget target)
        {
            if (target <= IKTarget.None || ikData == null) return;
            if (target == IKTarget.Total)
            {
                SetSynchroIKtargetAll();
                return;
            }
            if (!ikData[(int)target].updateIKtarget)
                ikData[(int)target].synchroIKtarget = true;
        }
        public void ResetSynchroIKtargetAll()
        {
            if (ikData == null) return;
            foreach (var data in ikData)
            {
                data.synchroIKtarget = false;
            }
        }
        public void SetSynchroIKtargetAll()
        {
            if (ikData == null) return;
            foreach (var data in ikData)
            {
                if (!data.updateIKtarget)
                    data.synchroIKtarget = true;
            }
        }
        public bool GetSynchroIKtargetAll()
        {
            if (ikData == null) return false;
            foreach (var data in ikData)
            {
                if (data.enable && data.synchroIKtarget)
                    return true;
            }
            return false;
        }

        private HumanBodyBones GetStartHumanoidIndex(IKTarget target)
        {
            var humanoidIndex = HumanBodyBones.Hips;
            switch ((IKTarget)target)
            {
            case IKTarget.Head: humanoidIndex = va.editHumanoidBones[(int)HumanBodyBones.Neck] != null ? HumanBodyBones.Neck : HumanBodyBones.Head; break;
            case IKTarget.LeftHand: humanoidIndex = HumanBodyBones.LeftUpperArm; break;
            case IKTarget.RightHand: humanoidIndex = HumanBodyBones.RightUpperArm; break;
            case IKTarget.LeftFoot: humanoidIndex = HumanBodyBones.LeftUpperLeg; break;
            case IKTarget.RightFoot: humanoidIndex = HumanBodyBones.RightUpperLeg; break;
            }
            return humanoidIndex;
        }
        private HumanBodyBones GetCenterHumanoidIndex(IKTarget target)
        {
            var humanoidIndex = HumanBodyBones.Hips;
            switch ((IKTarget)target)
            {
            case IKTarget.Head: break;
            case IKTarget.LeftHand: humanoidIndex = HumanBodyBones.LeftLowerArm; break;
            case IKTarget.RightHand: humanoidIndex = HumanBodyBones.RightLowerArm; break;
            case IKTarget.LeftFoot: humanoidIndex = HumanBodyBones.LeftLowerLeg; break;
            case IKTarget.RightFoot: humanoidIndex = HumanBodyBones.RightLowerLeg; break;
            }
            return humanoidIndex;
        }
        private HumanBodyBones GetEndHumanoidIndex(IKTarget target)
        {
            var humanoidIndex = HumanBodyBones.Hips;
            switch ((IKTarget)target)
            {
            case IKTarget.Head: humanoidIndex = HumanBodyBones.Head; break;
            case IKTarget.LeftHand: humanoidIndex = HumanBodyBones.LeftHand; break;
            case IKTarget.RightHand: humanoidIndex = HumanBodyBones.RightHand; break;
            case IKTarget.LeftFoot: humanoidIndex = HumanBodyBones.LeftFoot; break;
            case IKTarget.RightFoot: humanoidIndex = HumanBodyBones.RightFoot; break;
            }
            return humanoidIndex;
        }

        public List<HumanBodyBones> SelectionAnimatorIKTargetsHumanoidIndexes()
        {
            List<HumanBodyBones> list = new List<HumanBodyBones>();
            if (ikTargetSelect != null)
            {
                foreach (var ikTarget in ikTargetSelect)
                {
                    for (int i = 0; i < HumanBonesUpdateAnimatorIK.Length; i++)
                    {
                        if (ikTarget == HumanBonesUpdateAnimatorIK[i])
                            list.Add((HumanBodyBones)i);
                    }
                }
            }
            return list;
        }

#if VERYANIMATION_ANIMATIONRIGGING
        #region AnimationRigging
        private void UpdateAnimationRiggingConstraint(IKTarget target)
        {
            ikData[(int)target].rigConstraint = null;
            if (va.animationRigging.isValid)
            {
                for (int i = 0; i < va.animationRigging.rig.transform.childCount; i++)
                {
                    var rigConstraints = va.animationRigging.rig.transform.GetChild(i).GetComponents<IRigConstraint>();
                    foreach (var rigConstraint in rigConstraints)
                    {
                        MonoBehaviour mono = null;
                        switch (target)
                        {
                        case IKTarget.Head:
                            mono = rigConstraint as MultiAimConstraint;
                            break;
                        case IKTarget.LeftHand:
                        case IKTarget.RightHand:
                        case IKTarget.LeftFoot:
                        case IKTarget.RightFoot:
                            mono = rigConstraint as TwoBoneIKConstraint;
                            break;
                        }
                        if (mono == null || mono.name != GetAnimationRiggingConstraintName(target))
                            continue;
                        ikData[(int)target].rigConstraint = mono;
                        var text = mono.GetType().ToString();
                        {
                            var lastIndex = text.LastIndexOf('.');
                            if (lastIndex >= 0)
                                text = text.Remove(0, lastIndex + 1);
                        }
                        ikData[(int)target].rigConstraintGUIContent = new GUIContent(text, mono.GetType().ToString());

                        var path = AnimationUtility.CalculateTransformPath(mono.transform, vaw.gameObject.transform);
                        switch (target)
                        {
                        case IKTarget.Head:
                            ikData[(int)target].rigConstraintWeight = EditorCurveBinding.FloatCurve(path, typeof(MultiAimConstraint), "m_Weight");
                            break;
                        case IKTarget.LeftHand:
                        case IKTarget.RightHand:
                        case IKTarget.LeftFoot:
                        case IKTarget.RightFoot:
                            ikData[(int)target].rigConstraintWeight = EditorCurveBinding.FloatCurve(path, typeof(TwoBoneIKConstraint), "m_Weight");
                            break;
                        }
                    }
                }
            }
        }
        private bool CreateAnimationRiggingConstraint(IKTarget target)
        {
            DeleteAnimationRiggingConstraint(target);

            va.StopRecording();

            if (!va.animationRigging.isValid)
            {
                va.animationRigging.Enable();
            }
            if (!va.animationRigging.isValid)
                return false;

            var listIndex = ikReorderableList.index;
            {
                var go = AddAnimationRiggingConstraint(vaw.gameObject, target);
                UpdateAnimationRiggingConstraint(target);
                va.OnHierarchyWindowChanged();
                Selection.activeGameObject = go;
                va.SetUpdateSampleAnimation();
                va.SetSynchroIKtargetAll();
            }
            ikReorderableList.index = listIndex;

            return true;
        }
        private void DeleteAnimationRiggingConstraint(IKTarget target)
        {
            if (ikData[(int)target].rigConstraint == null)
                return;

            va.StopRecording();

            var listIndex = ikReorderableList.index;
            {
                Selection.activeGameObject= ikData[(int)target].rigConstraint.gameObject;
                Unsupported.DeleteGameObjectSelection();
                if (ikData[(int)target].rigConstraint != null)
                    return;
                ikData[(int)target].rigConstraint = null;
                switch (target)
                {
                case IKTarget.Head:
                    break;
                case IKTarget.LeftHand:
                    va.animationRigging.vaRig.basePoseLeftHand.Reset();
                    break;
                case IKTarget.RightHand:
                    va.animationRigging.vaRig.basePoseRightHand.Reset();
                    break;
                case IKTarget.LeftFoot:
                    va.animationRigging.vaRig.basePoseLeftFoot.Reset();
                    break;
                case IKTarget.RightFoot:
                    va.animationRigging.vaRig.basePoseRightFoot.Reset();
                    break;
                }
                va.OnHierarchyWindowChanged();
            }
            ikReorderableList.index = listIndex;
        }
        private void WriteAnimationRiggingConstraint(IKTarget target, float time)
        {
            if (ikData[(int)target].rigConstraint == null || !ikData[(int)target].enable)
                return;

            var worldToLocalRotation = Quaternion.Inverse(va.transformPoseSave.startRotation);
            var worldToLocalMatrix = va.transformPoseSave.startMatrix.inverse;

            Vector3 position, hintPosition;
            Quaternion rotation;
            GetCalcWorldTransform(target, out position, out rotation, out hintPosition);
            {
                var hiHint = GetCenterHumanoidIndex(target);
                if (hiHint != (HumanBodyBones)0)
                {
                    var limitSign = va.GetLimitSign(hiHint);
                    var length = va.GetAvatarAxisLength(hiHint);
                    var vec = (va.editHumanoidBones[(int)hiHint].transform.rotation * va.GetAvatarPostRotation(hiHint)) * Vector3.up * limitSign.z;
                    var hiEnd = GetEndHumanoidIndex(target);
                    hintPosition = worldToLocalMatrix.MultiplyPoint3x4(va.editHumanoidBones[(int)hiEnd].transform.position + vec * length);
                }
            }

            #region FeetBottomHeight
            switch (target)
            {
            case IKTarget.LeftFoot: position += new Vector3(0f, -va.editAnimator.leftFeetBottomHeight, 0f); break;
            case IKTarget.RightFoot: position += new Vector3(0f, -va.editAnimator.rightFeetBottomHeight, 0f); break;
            }
            #endregion

            switch (target)
            {
            case IKTarget.Head:
                {
                    var constraint = ikData[(int)target].rigConstraint as MultiAimConstraint;
                    if (constraint != null && constraint.data.sourceObjects.Count > 0 && constraint.data.sourceObjects[0].transform != null)
                    {
                        var boneIndex = va.BonesIndexOf(constraint.gameObject);
                        var parentMatrix = (worldToLocalMatrix * va.editBones[boneIndex].transform.localToWorldMatrix).inverse;
                        var targetBoneIndex = va.BonesIndexOf(constraint.data.sourceObjects[0].transform.gameObject);
                        var localPosition = parentMatrix.MultiplyPoint3x4(position);
                        va.SetAnimationValueTransformPosition(targetBoneIndex, localPosition, time);
                    }
                }
                break;
            case IKTarget.LeftHand:
            case IKTarget.RightHand:
            case IKTarget.LeftFoot:
            case IKTarget.RightFoot:
                {
                    var constraint = ikData[(int)target].rigConstraint as TwoBoneIKConstraint;
                    if (constraint != null)
                    {
                        var boneIndex = va.BonesIndexOf(constraint.gameObject);
                        var parentRotation = Quaternion.Inverse(worldToLocalRotation * va.editBones[boneIndex].transform.rotation);
                        var parentMatrix = (worldToLocalMatrix * va.editBones[boneIndex].transform.localToWorldMatrix).inverse;
                        if (constraint.data.target != null)
                        {
                            var targetBoneIndex = va.BonesIndexOf(constraint.data.target.gameObject);
                            var localPosition = parentMatrix.MultiplyPoint3x4(position);
                            var localRotation = parentRotation * rotation;
                            va.SetAnimationValueTransformPosition(targetBoneIndex, localPosition, time);
                            va.SetAnimationValueTransformRotation(targetBoneIndex, localRotation, time);
                        }
                        if (constraint.data.hint != null)
                        {
                            var hintBoneIndex = va.BonesIndexOf(constraint.data.hint.gameObject);
                            var localPosition = parentMatrix.MultiplyPoint3x4(hintPosition);
                            va.SetAnimationValueTransformPosition(hintBoneIndex, localPosition, time);
                        }
                    }
                }
                break;
            }
        }

        public void WriteCurveAnimationRiggingConstraint(IKTarget target, int beginFrame, int endFrame, SynchroSetFlags syncFlags)
        {
            if (beginFrame < 0)
                beginFrame = 0;
            if (endFrame < 0) 
                endFrame = va.uAw.GetLastFrame(va.currentClip);

            List<float> keyTimes;
            {
                var checkCurves = new List<AnimationCurve>();
                var humanoidIndex = GetEndHumanoidIndex(target);
                keyTimes = va.GetHumanoidKeyframeTimeList(va.currentClip, humanoidIndex);
                var beginTime = va.uAw.SnapToFrame(beginFrame >= 0 ? beginFrame / va.currentClip.frameRate : 0f, va.currentClip.frameRate);
                var endTime = va.uAw.SnapToFrame(endFrame >= 0 ? endFrame / va.currentClip.frameRate : va.currentClip.length, va.currentClip.frameRate);
                if (!keyTimes.Contains(beginTime))
                    keyTimes.Add(beginTime);
                if (!keyTimes.Contains(endTime))
                    keyTimes.Add(endTime);
                keyTimes.RemoveAll(s => s < beginTime || s > endTime);
                keyTimes.Sort();
            }
            if (keyTimes.Count > 0)
            {
                var saveTime = va.currentTime;
                try
                {
                    for (int i = 0; i < keyTimes.Count; i++)
                    {
                        EditorUtility.DisplayProgressBar("Genarate IK Curves", target.ToString(), i / (float)keyTimes.Count);

                        var time = keyTimes[i];
                        va.SetCurrentTime(time);
                        va.SampleAnimation(time);
                        SynchroSet(target, syncFlags);

                        WriteAnimationRiggingConstraint(target, time);
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                    va.SetCurrentTime(saveTime);
                }
            }
        }
        public void SetSyncroAndWriteAllAnimationRiggingConstraint()
        {
            for (int target = 0; target < ikData.Length; target++)
            {
                if (!ikData[target].enable)
                    continue;
                SynchroSet((IKTarget)target, SynchroSetFlags.None);
                WriteAnimationRiggingConstraint((IKTarget)target, va.currentTime);
            }
        }
        public EditorCurveBinding[] GetAnimationRiggingConstraintBindings(IKTarget target)
        {
            List<EditorCurveBinding> bindings = new List<EditorCurveBinding>();
            switch (target)
            {
            case IKTarget.Head:
                {
                    var constraint = ikData[(int)target].rigConstraint as MultiAimConstraint;
                    if (constraint != null && constraint.data.sourceObjects.Count > 0 && constraint.data.sourceObjects[0].transform != null)
                    {
                        var boneIndex = va.BonesIndexOf(constraint.data.sourceObjects[0].transform.gameObject);
                        for (int dof = 0; dof < 3; dof++)
                            bindings.Add(va.AnimationCurveBindingTransformPosition(boneIndex, dof));
                    }
                }
                break;
            case IKTarget.LeftHand:
            case IKTarget.RightHand:
            case IKTarget.LeftFoot:
            case IKTarget.RightFoot:
                {
                    var constraint = ikData[(int)target].rigConstraint as TwoBoneIKConstraint;
                    if (constraint != null)
                    {
                        if (constraint.data.target != null)
                        {
                            var boneIndex = va.BonesIndexOf(constraint.data.target.gameObject);
                            for (int dof = 0; dof < 3; dof++)
                                bindings.Add(va.AnimationCurveBindingTransformPosition(boneIndex, dof));
                            for (int dof = 0; dof < 3; dof++)
                                bindings.Add(va.AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.RawEuler));
                            for (int dof = 0; dof < 4; dof++)
                                bindings.Add(va.AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.RawQuaternions));
                        }
                        if (constraint.data.hint != null)
                        {
                            var boneIndex = va.BonesIndexOf(constraint.data.hint.gameObject);
                            for (int dof = 0; dof < 3; dof++)
                                bindings.Add(va.AnimationCurveBindingTransformPosition(boneIndex, dof));
                        }
                    }
                }
                break;
            }
            return bindings.ToArray();
        }

        public static string GetAnimationRiggingConstraintName(IKTarget target)
        {
            return string.Format("{0}_{1}", AnimationRigging.AnimationRiggingRigName, target);
        }
        public static GameObject GetAnimationRiggingConstraint(GameObject gameObject, IKTarget target)
        {
            var vaRig = AnimationRigging.GetVeryAnimationRig(gameObject);
            if (vaRig == null)
                return null;
            var rig = vaRig.GetComponent<Rig>();
            if (rig == null)
                return null;
            var animator = gameObject.GetComponent<Animator>();
            if (animator == null || animator.avatar == null)
                return null;
            var child = vaRig.transform.Find(GetAnimationRiggingConstraintName(target));
            if (child == null)
                return null;
            if (target == IKTarget.Head)
            {
                if (child.GetComponent<MultiAimConstraint>() == null)
                    return null;
            }
            else
            {
                if (child.GetComponent<TwoBoneIKConstraint>() == null)
                    return null;
            }
            return child.gameObject;
        }
        public static GameObject AddAnimationRiggingConstraint(GameObject gameObject, IKTarget target)
        {
            var vaRig = AnimationRigging.GetVeryAnimationRig(gameObject);
            if (vaRig == null)
                return null;
            var rig = vaRig.GetComponent<Rig>();
            if (rig == null)
                return null;
            var animator = gameObject.GetComponent<Animator>();
            if (animator == null || animator.avatar == null)
                return null;

            if (!animator.isInitialized)
                animator.Rebind();

            var uAvatar = new UAvatar();

            var go = new GameObject(GetAnimationRiggingConstraintName(target));
            go.transform.SetParent(rig.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(go, "");
            var targetObj = new GameObject(go.name + "_Target");
            targetObj.transform.SetParent(go.transform);
            targetObj.transform.localPosition = Vector3.zero;
            targetObj.transform.localRotation = Quaternion.identity;
            targetObj.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(targetObj, "");

            var transformPoseSave = new TransformPoseSave(gameObject);
            transformPoseSave.CreateExtraTransforms();
            var saveRoot = transformPoseSave.GetHumanDescriptionTransforms(gameObject.transform);
            if (saveRoot == null)
                return null;

            var rootRotationInv = Quaternion.Inverse(saveRoot.rotation);
            if (target == IKTarget.Head)
            {
                var tHead = animator.GetBoneTransform(HumanBodyBones.Head);
                if (tHead != null)
                {
                    var constraint = Undo.AddComponent<MultiAimConstraint>(go);
                    Undo.RecordObject(constraint, "");

                    constraint.weight = 0f;
                    constraint.data.constrainedObject = tHead;
                    {
                        var list = constraint.data.sourceObjects;
                        list.Add(new WeightedTransform(targetObj.transform, 1f));
                        constraint.data.sourceObjects = list;
                    }
                    {
                        var forward = uAvatar.GetPostRotation(animator.avatar, (int)HumanBodyBones.Head) * Vector3.down;
                        var dotX = Vector3.Dot(forward, Vector3.right);
                        var dotY = Vector3.Dot(forward, Vector3.up);
                        var dotZ = Vector3.Dot(forward, Vector3.forward);
                        var axis = forward;
                        if (Mathf.Abs(dotX) > Mathf.Abs(dotY) && Mathf.Abs(dotX) > Mathf.Abs(dotZ))
                        {
                            if (dotX > 0f)
                            {
                                constraint.data.aimAxis = MultiAimConstraintData.Axis.X;
                                axis = Vector3.right;
                            }
                            else
                            {
                                constraint.data.aimAxis = MultiAimConstraintData.Axis.X_NEG;
                                axis = Vector3.left;
                            }
                            constraint.data.constrainedXAxis = false;
                        }
                        else if (Mathf.Abs(dotY) > Mathf.Abs(dotX) && Mathf.Abs(dotY) > Mathf.Abs(dotZ))
                        {
                            if (dotY > 0f)
                            {
                                constraint.data.aimAxis = MultiAimConstraintData.Axis.Y;
                                axis = Vector3.up;
                            }
                            else
                            {
                                constraint.data.aimAxis = MultiAimConstraintData.Axis.Y_NEG;
                                axis = Vector3.down;
                            }
                            constraint.data.constrainedYAxis = false;
                        }
                        else
                        {
                            if (dotZ > 0f)
                            {
                                constraint.data.aimAxis = MultiAimConstraintData.Axis.Z;
                                axis = Vector3.forward;
                            }
                            else
                            {
                                constraint.data.aimAxis = MultiAimConstraintData.Axis.Z_NEG;
                                axis = Vector3.back;
                            }
                            constraint.data.constrainedZAxis = false;
                        }
                        constraint.data.offset = Quaternion.FromToRotation(forward, axis).eulerAngles;
                    }
                    {
                        var limit = uAvatar.GetMuscleLimitNonError(animator.avatar, HumanBodyBones.Head);
                        var min = Mathf.Min(limit.min[0], limit.min[2]);
                        var max = Mathf.Min(limit.max[0], limit.max[2]);
                        constraint.data.limits = new Vector2(min, max);
                    }
                    {
                        constraint.data.maintainOffset = false;
                    }
                }
                else
                {
                    Debug.LogErrorFormat("<color=blue>[Very Animation]</color>Unknown avatar file error. {0}", animator.avatar);
                }
            }
            else
            {
                Transform tRoot, tMid, tTip;
                switch (target)
                {
                    case IKTarget.LeftHand:
                        tRoot = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                        tMid = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                        tTip = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                        break;
                    case IKTarget.RightHand:
                        tRoot = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                        tMid = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                        tTip = animator.GetBoneTransform(HumanBodyBones.RightHand);
                        break;
                    case IKTarget.LeftFoot:
                        tRoot = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                        tMid = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                        tTip = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                        break;
                    case IKTarget.RightFoot:
                        tRoot = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                        tMid = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                        tTip = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                        break;
                    default:
                        tRoot = tMid = tTip = null;
                        break;
                }
                if (tRoot != null && tMid != null && tTip != null)
                {
                    var constraint = Undo.AddComponent<TwoBoneIKConstraint>(go);
                    var hintObj = new GameObject(go.name + "_Hint");
                    hintObj.transform.SetParent(go.transform);
                    hintObj.transform.localPosition = Vector3.zero;
                    hintObj.transform.localRotation = Quaternion.identity;
                    hintObj.transform.localScale = Vector3.one;
                    Undo.RegisterCreatedObjectUndo(hintObj, "");
                    Undo.RecordObject(constraint, "");
                    Undo.RecordObject(vaRig, "");
                    constraint.weight = 0f;
                    switch (target)
                    {
                        case IKTarget.LeftHand:
                            constraint.data.root = tRoot;
                            constraint.data.mid = tMid;
                            constraint.data.tip = tTip;
                            constraint.data.maintainTargetRotationOffset = true;
                            {
                                var save = transformPoseSave.GetHumanDescriptionTransforms(constraint.data.tip);
                                if (save != null)
                                {
                                    var rotation = save.rotation;
                                    #region Corrects the difference between the finger direction not straight to the arm direction at the Avatar Configuration-Pose stage.
                                    var finger = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
                                    if (finger != null)
                                    {
                                        var saveLower = transformPoseSave.GetHumanDescriptionTransforms(constraint.data.mid);
                                        var saveFinger = transformPoseSave.GetHumanDescriptionTransforms(finger);
                                        if (saveLower != null && saveFinger != null)
                                        {
                                            var vecArm = save.position - saveLower.position;
                                            var vecIndex = saveFinger.position - save.position;
                                            vecIndex.z = vecArm.z = 0f;
                                            var offset = Quaternion.FromToRotation(vecIndex.normalized, vecArm.normalized);
                                            rotation = offset * rotation;
                                        }
                                    }
                                    #endregion
                                    rotation = Quaternion.Euler(0f, 90f, 0f) * (rootRotationInv * rotation);
                                    vaRig.basePoseLeftHand = new VeryAnimationRig.BasePoseTransformOffset(go.transform, rotation);
                                }
                            }
                            break;
                        case IKTarget.RightHand:
                            constraint.data.root = tRoot;
                            constraint.data.mid = tMid;
                            constraint.data.tip = tTip;
                            constraint.data.maintainTargetRotationOffset = true;
                            {
                                var save = transformPoseSave.GetHumanDescriptionTransforms(constraint.data.tip);
                                if (save != null)
                                {
                                    var rotation = save.rotation;
                                    #region Corrects the difference between the finger direction not straight to the arm direction at the Avatar Configuration-Pose stage.
                                    var finger = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                                    if (finger != null)
                                    {
                                        var saveLower = transformPoseSave.GetHumanDescriptionTransforms(constraint.data.mid);
                                        var saveFinger = transformPoseSave.GetHumanDescriptionTransforms(finger);
                                        if (saveLower != null && saveFinger != null)
                                        {
                                            var vecArm = save.position - saveLower.position;
                                            var vecIndex = saveFinger.position - save.position;
                                            vecIndex.z = vecArm.z = 0f;
                                            var offset = Quaternion.FromToRotation(vecIndex.normalized, vecArm.normalized);
                                            rotation = offset * rotation;
                                        }
                                    }
                                    #endregion
                                    rotation = Quaternion.Euler(0f, -90f, 0f) * (rootRotationInv * rotation);
                                    vaRig.basePoseRightHand = new VeryAnimationRig.BasePoseTransformOffset(go.transform, rotation);
                                }
                            }
                            break;
                        case IKTarget.LeftFoot:
                            constraint.data.root = tRoot;
                            constraint.data.mid = tMid;
                            constraint.data.tip = tTip;
                            constraint.data.maintainTargetPositionOffset = true;
                            constraint.data.maintainTargetRotationOffset = true;
                            {
                                var save = transformPoseSave.GetHumanDescriptionTransforms(constraint.data.tip);
                                if (save != null)
                                {
                                    var rotation = save.rotation;
                                    vaRig.basePoseLeftFoot = new VeryAnimationRig.BasePoseTransformOffset(go.transform, new Vector3(0f, animator.leftFeetBottomHeight, 0f), rootRotationInv * rotation);
                                }
                            }
                            break;
                        case IKTarget.RightFoot:
                            constraint.data.root = tRoot;
                            constraint.data.mid = tMid;
                            constraint.data.tip = tTip;
                            constraint.data.maintainTargetPositionOffset = true;
                            constraint.data.maintainTargetRotationOffset = true;
                            {
                                var save = transformPoseSave.GetHumanDescriptionTransforms(constraint.data.tip);
                                if (save != null)
                                {
                                    var rotation = save.rotation;
                                    vaRig.basePoseRightFoot = new VeryAnimationRig.BasePoseTransformOffset(go.transform, new Vector3(0f, animator.rightFeetBottomHeight, 0f), rootRotationInv * rotation);
                                }
                            }
                            break;
                    }
                    constraint.data.target = targetObj.transform;
                    constraint.data.hint = hintObj.transform;
                }
                else
                {
                    Debug.LogErrorFormat("<color=blue>[Very Animation]</color>Unknown avatar file error. {0}", animator.avatar);
                }
            }

            return go;
        }
        #endregion
#endif
    }
}
