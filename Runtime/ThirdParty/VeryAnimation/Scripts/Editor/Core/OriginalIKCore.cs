#if !UNITY_2019_1_OR_NEWER
#define VERYANIMATION_TIMELINE
#endif

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

namespace VeryAnimation
{
    [Serializable]
    public class OriginalIKCore
    {
        private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
        private VeryAnimationControlWindow vac { get { return VeryAnimationControlWindow.instance; } }
        private VeryAnimationEditorWindow vae { get { return VeryAnimationEditorWindow.instance; } }
        private VeryAnimation va { get { return VeryAnimation.instance; } }

        public enum SolverType
        {
            CcdIK,
            LimbIK,
            LookAt,
            Total,
        }
        public GUIContent[] SolverTypeStrings = new GUIContent[(int)SolverType.LimbIK + 1];

        private const int SolverLevelMin = 2;
        private const int SolverLevelMax = 16;
        private readonly GUIContent[] SolverLevelStrings = new GUIContent[]
        {
            new GUIContent("2", "IK Level"),
            new GUIContent("3", "IK Level"),
            new GUIContent("4", "IK Level"),
            new GUIContent("5", "IK Level"),
            new GUIContent("6", "IK Level"),
            new GUIContent("7", "IK Level"),
            new GUIContent("8", "IK Level"),
            new GUIContent("9", "IK Level"),
            new GUIContent("10", "IK Level"),
            new GUIContent("11", "IK Level"),
            new GUIContent("12", "IK Level"),
            new GUIContent("13", "IK Level"),
            new GUIContent("14", "IK Level"),
            new GUIContent("15", "IK Level"),
            new GUIContent("16", "IK Level"),
        };

        public GUIContent[] IKSpaceTypeStrings = new GUIContent[(int)OriginalIKData.SpaceType.Total];

        [Serializable]
        public class OriginalIKData
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
            public float swivel;

            public string name;
            public SolverType solverType;
            public bool resetRotations;  //CCD
            public int level;           //CCD
            public float limbDirection;   //Limb
            [Serializable]
            public class JointData
            {
                public GameObject bone;
                public float weight;

                [NonSerialized]
                public int boneIndex;
                [NonSerialized]
                public bool foldout;
            }
            public List<JointData> joints;

            public bool isValid { get { return enable && solver != null && solver.isValid; } }
            public bool isUpdate { get { return isValid && updateIKtarget && !synchroIKtarget; } }
            public GameObject tip { get { return joints != null && joints.Count > 0 && joints[0].boneIndex >= 0 ? va.editBones[joints[0].boneIndex] : null; } }
            public GameObject root { get { return joints != null && joints.Count > 0 && joints[level - 1].boneIndex >= 0 ? va.editBones[joints[level - 1].boneIndex] : null; } }

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
                        if (root != null && root.transform.parent != null)
                            getpos = root.transform.parent.localToWorldMatrix.MultiplyPoint3x4(getpos);
                        break;
                    case SpaceType.Parent:
                        if (parent != null)
                            getpos = parent.transform.localToWorldMatrix.MultiplyPoint3x4(getpos);
                        break;
                    default:
                        Assert.IsTrue(false); getpos = position;
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
                        if (root != null && root.transform.parent != null)
                            setpos = root.transform.parent.worldToLocalMatrix.MultiplyPoint3x4(setpos); 
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
                        if (root != null && root.transform.parent != null)
                            getrot = root.transform.parent.rotation * getrot; 
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
                        if (root != null && root.transform.parent != null)
                            setrot = Quaternion.Inverse(root.transform.parent.rotation) * setrot; 
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

            [NonSerialized]
            public int rootBoneIndex;
            [NonSerialized]
            public int parentBoneIndex;
            [NonSerialized]
            public bool updateIKtarget;
            [NonSerialized]
            public bool synchroIKtarget;
            [NonSerialized]
            public SolverBase solver;
        }
        public List<OriginalIKData> ikData;

        public int[] ikTargetSelect;
        public int ikActiveTarget { get { return ikTargetSelect != null && ikTargetSelect.Length > 0 ? ikTargetSelect[0] : -1; } }

        private struct UpdateData
        {
            public float time;
            public Quaternion rotation;
        }
        private List<UpdateData>[] updateRotations;

        private class TmpCurves
        {
            public AnimationCurve[] curves = new AnimationCurve[4];

            public void Clear()
            {
                for (int i = 0; i < 4; i++)
                {
                    curves[i] = null;
                }
            }
        }
        private TmpCurves tmpCurves;

        private UDisc uDisc;
        private USnapSettings uSnapSettings;

        private ReorderableList ikReorderableList;
        private bool advancedFoldout;

        #region Solver
        public abstract class SolverBase
        {
            protected VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
            protected VeryAnimation va { get { return VeryAnimation.instance; } }

            public bool isValid { get; protected set; }
            public Transform[] boneTransforms { get; protected set; }
            public int[] boneIndexes { get; protected set; }

            public Transform tip { get { return boneTransforms[0]; } }
            public Transform root { get { return boneTransforms[boneTransforms.Length - 1]; } }

            public virtual bool Initialize(GameObject[] bones, GameObject[] joints)
            {
                isValid = false;
                boneTransforms = null;
                boneIndexes = null;

                if (joints.Length < 2) return false;

                #region Check
                {
                    for (int i = 0; i < joints.Length; i++)
                    {
                        if (joints[i] == null)
                            return false;
                        if (EditorCommon.ArrayIndexOf(bones, joints[i]) < 0)
                            return false;
                        {
                            int count = 0;
                            var t = joints[i].transform.parent;
                            while (t != null)
                            {
                                for (int j = i + 1; j < joints.Length; j++)
                                {
                                    if (joints[j] != null && t == joints[j].transform)
                                        count++;
                                }
                                t = t.parent;
                            }
                            if (count < joints.Length - (i + 1))
                                return false;
                        }
                    }
                }
                #endregion

                #region Set
                {
                    boneTransforms = new Transform[joints.Length];
                    boneIndexes = new int[joints.Length];
                    for (int i = 0; i < joints.Length; i++)
                    {
                        boneTransforms[i] = joints[i].transform;
                        boneIndexes[i] = EditorCommon.ArrayIndexOf(bones, joints[i]);
                    }
                }
                #endregion

                isValid = true;

                return true;
            }

            public virtual Vector3 GetBasicDir()
            {
                const float ToleranceSq = 0.0001f;
                var posA = root.position;
                var posB = tip.position;
                var axis = posB - posA;
                axis.Normalize();
                if (axis.sqrMagnitude <= ToleranceSq)
                    return (boneTransforms[boneTransforms.Length - 2].position - root.position).normalized;
                if (boneTransforms.Length <= 2)
                {
                    Vector3 cross;
                    if (Mathf.Abs(Vector3.Dot(axis, root.up)) < 0.5f)
                    {
                        cross = Vector3.Cross(axis, root.up);
                        cross.Normalize();
                    }
                    else
                    {
                        cross = Vector3.Cross(axis, root.right);
                        cross.Normalize();
                    }
                    return cross;
                }
                else
                {
                    var posC = boneTransforms[boneTransforms.Length - 2].position;
                    var vecCP = posC - (posA + axis * Vector3.Dot((posC - posA), axis));
                    vecCP.Normalize();
                    if (vecCP.sqrMagnitude <= ToleranceSq) return root.up;
                    return vecCP;
                }
            }

            protected void FixReverseRotation()
            {
                for (int i = 0; i < boneTransforms.Length; i++)
                {
                    var save = va.boneSaveTransforms[boneIndexes[i]];
                    var rot = boneTransforms[i].localRotation * Quaternion.Inverse(save.localRotation);
                    if (rot.w < 0f)
                    {
                        var rotation = boneTransforms[i].localRotation;
                        for (int dof = 0; dof < 4; dof++)
                            rotation[dof] = -rotation[dof];
                        boneTransforms[i].localRotation = rotation;
                    }
                }
            }

            public abstract void Update(Vector3 targetPos, Quaternion? targetRotation);

            public virtual float GetSwivel() { return 0f; }

            public Quaternion GetTipAutoRotation()
            {
                return tip.parent.rotation * va.boneSaveTransforms[boneIndexes[0]].localRotation;
            }
        }
        #region Cyclic-Coordinate-Descent (CCD)
        public class SolverCCD : SolverBase
        {
            private const int Iteration = 16;

            public bool resetRotations;
            public float swivel;
            public float[] weights;

            public override bool Initialize(GameObject[] bones, GameObject[] joints)
            {
                if (!base.Initialize(bones, joints))
                    return false;

                swivel = 0f;
                {
                    weights = new float[joints.Length];
                    for (int i = 0; i < joints.Length; i++)
                        weights[i] = 1f;
                }

                return true;
            }

            public override void Update(Vector3 targetPos, Quaternion? targetRotation)
            {
                if (!isValid) return;

                #region Reset
                if (resetRotations)
                {
                    for (int i = 0; i < boneTransforms.Length; i++)
                        boneTransforms[i].localRotation = va.boneSaveTransforms[boneIndexes[i]].localRotation;
                }
                #endregion 

                #region StraightAvoidance
                {
                    var vecTarget = targetPos - root.position;
                    var lengthTarget = vecTarget.magnitude;
                    vecTarget.Normalize();
                    if (vecTarget.sqrMagnitude > 0f)
                    {
                        int count = 0;
                        float lengthTotal = 0f;
                        for (int i = boneTransforms.Length - 1; i > 0; i--)
                        {
                            var vec = boneTransforms[i - 1].position - boneTransforms[i].position;
                            var lengthVec = vec.magnitude;
                            vec.Normalize();
                            if (vec.sqrMagnitude > 0f)
                            {
                                lengthTotal += lengthVec;
                                var dot = Vector3.Dot(vecTarget, vec);
                                if (Mathf.Approximately(Mathf.Abs(dot), 1f))
                                    count++;
                            }
                        }
                        if (lengthTarget < lengthTotal && count == boneTransforms.Length - 1)
                        {
                            root.rotation *= Quaternion.AngleAxis(1f, GetBasicDir());
                        }
                    }
                }
                #endregion

                const float ToleranceSq = 0.0001f;
                for (int i = 0; i < Iteration; i++)
                {
                    for (int j = 1; j < boneTransforms.Length; j++)
                    {
                        Vector3 localTargetPos;
                        Vector3 localEffectorPos;
                        {
                            var invRot = Quaternion.Inverse(boneTransforms[j].rotation);
                            var position = boneTransforms[j].position;
                            localEffectorPos = invRot * (tip.position - position);
                            localTargetPos = invRot * (targetPos - position);
                        }
                        localEffectorPos.Normalize();
                        localTargetPos.Normalize();
                        if (localEffectorPos.sqrMagnitude <= 0f || localTargetPos.sqrMagnitude <= 0f)
                            continue;
                        {
                            var rotationAdd = Quaternion.FromToRotation(localEffectorPos, localTargetPos);
                            if (weights[j] != 1f)
                                rotationAdd = Quaternion.Slerp(Quaternion.identity, rotationAdd, weights[j]);
                            boneTransforms[j].localRotation *= rotationAdd;
                        }
                    }
                    if ((tip.position - targetPos).sqrMagnitude < ToleranceSq)
                        break;
                }

                #region Swivel
                if (resetRotations && swivel != 0f)
                {
                    var axis = (tip.position - root.position).normalized;
                    if (axis.sqrMagnitude > 0f)
                    {
                        axis = root.transform.worldToLocalMatrix.MultiplyVector(axis);
                        root.localRotation *= Quaternion.AngleAxis(swivel, axis);
                    }
                }
                #endregion

                FixReverseRotation();

                if (targetRotation.HasValue)
                {
                    tip.rotation = targetRotation.Value;
                }
                else
                {
                    tip.rotation = GetTipAutoRotation();
                }
            }
            public override float GetSwivel()
            {
                if (!isValid || !resetRotations) return 0f;

                float result = 0f;

                #region Save
                var saveSwivel = swivel;
                TransformPoseSave.SaveData[] save = new TransformPoseSave.SaveData[boneTransforms.Length];
                for (int i = 0; i < boneTransforms.Length; i++)
                    save[i] = new TransformPoseSave.SaveData(boneTransforms[i]);
                #endregion

                try
                {
                    var vecAfter = GetBasicDir();
                    swivel = 0f;
                    Update(tip.position, tip.rotation);
                    var vecBefore = GetBasicDir();
                    result = Vector3.SignedAngle(vecBefore, vecAfter, (tip.position - root.position).normalized);
                }
                finally
                {
                    swivel = saveSwivel;

                    #region Load
                    for (int i = 0; i < boneTransforms.Length; i++)
                        save[i].LoadLocal(boneTransforms[i]);
                    #endregion
                }

                return result;
            }
        }
        #endregion
        #region Limb
        public class SolverLimb : SolverBase
        {
            public Transform lower { get { return boneTransforms[1]; } }
            public Transform upper { get { return boneTransforms[2]; } }

            public float swivel;
            public float direction;

            private Vector3 lowerAxis;
            private Vector3 lowerDirection;
            private Quaternion linearizationForward;
            private Quaternion linearizationLower;

            public override bool Initialize(GameObject[] bones, GameObject[] joints)
            {
                if (joints.Length != 3)
                    return false;
                if (!base.Initialize(bones, joints))
                    return false;

                direction = 0f;
                swivel = GetSwivel();

                return true;
            }

            public override Vector3 GetBasicDir()
            {
                var rotation = (upper.rotation * Quaternion.Inverse(va.boneSaveTransforms[boneIndexes[2]].rotation)) * Quaternion.Inverse(linearizationForward);
                return rotation * GetLowerAxis();
            }
            public override void Update(Vector3 targetPos, Quaternion? targetRotation)
            {
                if (!isValid) return;

                var upperLength = Vector3.Distance(lower.position, upper.position);
                var lowerLength = Vector3.Distance(tip.position, lower.position);
                if (upperLength <= 0f || lowerLength <= 0f)
                    return;

                #region Reset
                for (int i = boneTransforms.Length - 1; i >= 0; i--)
                    boneTransforms[i].rotation = va.boneSaveTransforms[boneIndexes[i]].rotation;
                {
                    var vLower = (lower.position - upper.position).normalized;
                    var lookRot = Quaternion.LookRotation(vLower);
                    {
                        linearizationForward = Quaternion.Inverse(lookRot);
                    }
                    {
                        var invRot = Quaternion.Inverse(lower.rotation);
                        linearizationLower = Quaternion.FromToRotation(invRot * (tip.position - lower.position).normalized, invRot * vLower);
                    }
                    {
                        lowerAxis = lookRot * Vector3.up;
                        lowerAxis = linearizationForward * lowerAxis;
                        lowerDirection = Vector3.Cross(Vector3.forward, lowerAxis).normalized;
                    }
                }
                #endregion

                upper.rotation = linearizationForward * upper.rotation;
                lower.localRotation *= linearizationLower;

                var vGoal = linearizationForward * (targetPos - upper.position);

                Quaternion upperRot = Quaternion.identity;
                Quaternion lowerRot = Quaternion.identity;
                {
                    const float Tolerance = 0.000001f;
                    var vAxis = GetLowerAxis();
                    var vDirection = GetLowerDirection();
                    var distGoal = vGoal.magnitude;
                    //Far
                    if (distGoal >= upperLength + lowerLength)
                    {
                        distGoal = upperLength + lowerLength - Tolerance;
                        vGoal = vGoal.normalized * distGoal;
                    }
                    //Near
                    if (distGoal < Tolerance)
                    {
                        lowerRot = Quaternion.AngleAxis(180f, vAxis);
                        upperRot = Quaternion.identity;
                    }
                    else if (upperLength >= lowerLength && distGoal < upperLength - lowerLength + Tolerance)
                    {
                        lowerRot = Quaternion.AngleAxis(180f, vAxis);
                        upperRot = Quaternion.FromToRotation(Vector3.forward, vGoal.normalized);
                    }
                    else if (upperLength < lowerLength && distGoal < lowerLength - upperLength + Tolerance)
                    {
                        lowerRot = Quaternion.AngleAxis(180f, vAxis);
                        upperRot = Quaternion.FromToRotation(Vector3.forward, -vGoal.normalized);
                    }
                    else
                    {
                        //Ry
                        {
                            float rY = Mathf.Acos(Mathf.Clamp((distGoal * distGoal - upperLength * upperLength - lowerLength * lowerLength) / (2f * upperLength * lowerLength), -1f, 1f));
                            Assert.IsFalse(rY < 0);
                            lowerRot = Quaternion.AngleAxis(rY * Mathf.Rad2Deg, vAxis);
                        }
                        Vector3 lowerPos;
                        {
                            var vGoalN = vGoal.normalized;
                            Vector3 posCenter;
                            float circleRadius;
                            {
                                var cosAlpha = Mathf.Min((distGoal * distGoal + upperLength * upperLength - lowerLength * lowerLength) / (2f * distGoal * upperLength), 1f);
                                posCenter = cosAlpha * upperLength * vGoalN;
                                circleRadius = Mathf.Sqrt(1f - cosAlpha * cosAlpha) * upperLength;
                            }
                            var vU = (vDirection - Vector3.Dot(vDirection, vGoalN) * vGoalN).normalized;
                            var radSwivel = swivel * Mathf.Deg2Rad;
                            lowerPos = posCenter + circleRadius * (Mathf.Cos(radSwivel) * vU + Mathf.Sin(radSwivel) * Vector3.Cross(vGoalN, vU));
                        }
                        //R1
                        {
                            var vR1Z = lowerPos.normalized;
                            var vR1X = (vGoal - Vector3.Dot(vGoal, vR1Z) * vR1Z).normalized;
                            var vR1Y = Vector3.Cross(vR1Z, vR1X);
                            {
                                var forward = new Vector3(vR1X.z, vR1Y.z, vR1Z.z);
                                var upwards = new Vector3(vR1X.y, vR1Y.y, vR1Z.y);
                                if (forward.sqrMagnitude > 0f && upwards.sqrMagnitude > 0f)
                                {
                                    upperRot = Quaternion.LookRotation(forward, upwards);
                                    upperRot = Quaternion.Inverse(upperRot);
                                }
                            }
                            upperRot *= Quaternion.AngleAxis(direction, Vector3.forward);
                        }
                    }
                }
                lower.rotation = lowerRot * lower.rotation;
                upper.rotation = upperRot * upper.rotation;

                upper.rotation = Quaternion.Inverse(linearizationForward) * upper.rotation;

                FixReverseRotation();

                if (targetRotation.HasValue)
                {
                    tip.rotation = targetRotation.Value;
                }
                else
                {
                    tip.rotation = GetTipAutoRotation();
                }
            }
            public override float GetSwivel()
            {
                if (!isValid) return 0f;

                float result = 0f;

                #region Save
                var saveSwivel = swivel;
                TransformPoseSave.SaveData[] save = new TransformPoseSave.SaveData[boneTransforms.Length];
                for (int i = 0; i < boneTransforms.Length; i++)
                    save[i] = new TransformPoseSave.SaveData(boneTransforms[i]);
                #endregion

                try
                {
                    var vecAfter = GetBasicDir();
                    swivel = 0f;
                    Update(tip.position, tip.rotation);
                    var vecBefore = GetBasicDir();
                    result = Vector3.SignedAngle(vecBefore, vecAfter, (tip.position - root.position).normalized);
                }
                finally
                {
                    swivel = saveSwivel;

                    #region Load
                    for (int i = 0; i < boneTransforms.Length; i++)
                        save[i].LoadLocal(boneTransforms[i]);
                    #endregion
                }

                return result;
            }

            public float GetDirectionFromTransform()
            {
                if (!isValid) return 0f;

                var vUpper = (lower.position - upper.position).normalized;
                var vLower = (tip.position - lower.position).normalized;
                if (Mathf.Abs(Vector3.Dot(vUpper, vLower)) > 1f - 0.0001f)
                    return 0f;

                var vFw = (tip.position - upper.position).normalized;

                Vector3 direction;
                {
                    var posP = upper.position + vFw * Vector3.Dot((lower.position - upper.position), vFw);
                    direction = (lower.position - posP).normalized;
                }
                Vector3 basicDirection;
                {
                    var rotation = (upper.rotation * Quaternion.Inverse(va.boneSaveTransforms[boneIndexes[2]].rotation)) * Quaternion.Inverse(linearizationForward);
                    basicDirection = Vector3.Cross(vFw, rotation * lowerAxis);
                }

                float result = 0f;
                {
                    var offsetRot = Quaternion.FromToRotation(direction, basicDirection);
                    Vector3 tmpAxis;
                    offsetRot.ToAngleAxis(out result, out tmpAxis);
                    if (Vector3.Dot(vFw, tmpAxis) < 0f)
                        result = -result;
                    result = Mathf.Repeat(result + 180f, 360f) - 180f;
                }
                return result;
            }

            private Vector3 GetLowerAxis()
            {
                return Quaternion.AngleAxis(-direction, Vector3.forward) * lowerAxis;
            }
            private Vector3 GetLowerDirection()
            {
                return Quaternion.AngleAxis(-direction, Vector3.forward) * lowerDirection;
            }
        }
        #endregion
        #endregion

        public void Initialize()
        {
            Release();

            ikData = new List<OriginalIKData>();
            ikTargetSelect = null;

            updateRotations = new List<UpdateData>[va.bones.Length];
            tmpCurves = new TmpCurves();

            uDisc = new UDisc();
            uSnapSettings = new USnapSettings();

            UpdateReorderableList();

            UpdateGUIContentStrings();
            Language.OnLanguageChanged += UpdateGUIContentStrings;

            Undo.undoRedoPerformed += UndoRedoPerformed;
        }
        public void Release()
        {
            Language.OnLanguageChanged -= UpdateGUIContentStrings;
            Undo.undoRedoPerformed -= UndoRedoPerformed;

            ikData = null;
            ikTargetSelect = null;
            updateRotations = null;
            tmpCurves = null;
            uDisc = null;
            uSnapSettings = null;
            ikReorderableList = null;
        }

        public void LoadIKSaveSettings(VeryAnimationSaveSettings.OriginalIKData[] saveIkData)
        {
            if (saveIkData != null)
            {
                ikData.Clear();
                if (ikReorderableList != null)
                    ikReorderableList.index = -1;
                ikTargetSelect = null;
                foreach (var d in saveIkData)
                {
                    if (d.level < SolverLevelMin || d.joints.Count < SolverLevelMin)
                        continue;

                    var data = new OriginalIKData()
                    {
                        enable = d.enable,
                        autoRotation = d.autoRotation,
                        spaceType = (OriginalIKData.SpaceType)d.spaceType,
                        parent = d.parent,
                        position = d.position,
                        rotation = d.rotation,
                        swivel = d.swivel,
                        name = d.name,
                        solverType = (SolverType)d.solverType,
                        resetRotations = d.resetRotations,
                        level = d.level,
                        limbDirection = d.limbDirection,
                        joints = new List<OriginalIKData.JointData>(),
                    };
                    //Path
                    if (!string.IsNullOrEmpty(d.parentPath))
                    {
                        var t = vaw.gameObject.transform.Find(d.parentPath);
                        if (t != null)
                            data.parent = t.gameObject;
                    }
                    foreach (var joint in d.joints)
                    {
                        var jdata = new OriginalIKData.JointData()
                        {
                            bone = joint.bone,
                            weight = joint.weight,
                        };
                        //Path
                        if (!string.IsNullOrEmpty(joint.bonePath))
                        {
                            var t = vaw.gameObject.transform.Find(joint.bonePath);
                            if (t != null)
                                jdata.bone = t.gameObject;
                        }
                        data.joints.Add(jdata);
                    }
                    ikData.Add(data);
                }
                for (int target = 0; target < ikData.Count; target++)
                {
                    UpdateSolver(target);
                }
            }
        }
        public VeryAnimationSaveSettings.OriginalIKData[] SaveIKSaveSettings()
        {
            if (va.originalIK == null || ikData == null)
                return null;
            List<VeryAnimationSaveSettings.OriginalIKData> saveIkData = new List<VeryAnimationSaveSettings.OriginalIKData>();
            foreach (var d in ikData)
            {
                var data = new VeryAnimationSaveSettings.OriginalIKData()
                {
                    enable = d.enable,
                    autoRotation = d.autoRotation,
                    spaceType = (int)d.spaceType,
                    parent = d.parent,
                    position = d.position,
                    rotation = d.rotation,
                    swivel = d.swivel,
                    name = d.name,
                    solverType = (int)d.solverType,
                    resetRotations = d.resetRotations,
                    level = d.level,
                    limbDirection = d.limbDirection,
                    joints = new List<VeryAnimationSaveSettings.OriginalIKData.JointData>(),
                    //Path
                    parentPath = d.parent != null ? AnimationUtility.CalculateTransformPath(d.parent.transform, vaw.gameObject.transform) : "",
                };
                for (int i = 0; i < d.joints.Count; i++)
                {
                    data.joints.Add(new VeryAnimationSaveSettings.OriginalIKData.JointData()
                    {
                        bone = d.joints[i].bone,
                        weight = d.joints[i].weight,
                        //Path
                        bonePath = d.joints[i].bone != null ? AnimationUtility.CalculateTransformPath(d.joints[i].bone.transform, vaw.gameObject.transform) : "",
                    });
                }
                saveIkData.Add(data);
            }
            return saveIkData.ToArray();
        }

        private int CreateIKData(GameObject jointTip)
        {
            {
                var boneIndex = va.BonesIndexOf(jointTip);
                if (boneIndex < 0) return -1;
                if (va.isHuman && va.humanoidConflict[boneIndex]) return -1;
            }

            var data = new OriginalIKData()
            {
                enable = true,
                name = jointTip.name,
                solverType = SolverType.CcdIK,
                resetRotations = true,
                joints = new List<OriginalIKData.JointData>(),
            };
            {
                var t = jointTip.transform;
                for (int i = 0; i < 3; i++)
                {
                    if (!IsValidAddBone(t.gameObject))
                        break;
                    data.joints.Add(new OriginalIKData.JointData()
                    {
                        bone = t.gameObject,
                        weight = 1f,
                    });
                    t = t.parent;
                    if (t == null) break;
                }
                if (data.joints.Count < 2)
                    return -1;
                data.level = data.joints.Count;
            }
            ikData.Add(data);
            UpdateSolver(ikData.Count - 1);
            SynchroSet(ikData.Count - 1);
            return ikData.Count - 1;
        }
        private bool ChangeSolverType(int target, SolverType solverType)
        {
            if (target < 0 || target >= ikData.Count)
                return false;
            ikData[target].solverType = solverType;
            switch (ikData[target].solverType)
            {
            case SolverType.LimbIK:
                ikData[target].level = 3;
                while (ikData[target].joints.Count < ikData[target].level)
                {
                    ikData[target].joints.Add(new OriginalIKData.JointData()
                    {
                        bone = null,
                        weight = 1f,
                    });
                }
                break;
            }

            UpdateSolver(target);

            if (ikData[target].solver.isValid)
            {
                switch (ikData[target].solverType)
                {
                case SolverType.LimbIK:
                    #region Auto set limbDirection
                    {
                        var solverLimb = ikData[target].solver as SolverLimb;
                        ikData[target].limbDirection = solverLimb.GetDirectionFromTransform();
                        solverLimb.direction = ikData[target].limbDirection;
                    }
                    #endregion
                    break;
                }
                SynchroSet(target);
            }

            SetUpdateIKtargetOriginalIK(target);

            return true;
        }
        public bool ChangeTypeSetting(int target, float add)
        {
            if (target < 0 || target >= ikData.Count)
                return false;
            switch (ikData[target].solverType)
            {
            case SolverType.CcdIK:
            case SolverType.LookAt:
                if (add > 0)
                {
                    Transform t = null;
                    if (ikData[target].joints.Count > 0)
                    {
                        var root = ikData[target].joints[ikData[target].level - 1].bone;
                        if (root != null)
                            t = root.transform.parent;
                    }
                    for (int i = 0; i < add; i++)
                    {
                        if (t == null) break;
                        if (!IsValidAddBone(t.gameObject))
                            break;
                        if (ikData[target].level + 1 > SolverLevelMax)
                            break;
                        if (ikData[target].joints.Count < ++ikData[target].level)
                        {
                            ikData[target].joints.Add(new OriginalIKData.JointData()
                            {
                                bone = t.gameObject,
                                weight = 1f,
                            });
                        }
                        t = t.parent;
                    }
                }
                else if (add < 0)
                {
                    for (int i = 0; i < Math.Abs(add); i++)
                    {
                        if (ikData[target].level - 1 < SolverLevelMin)
                            break;
                        ikData[target].level--;
                    }
                }
                break;
            case SolverType.LimbIK:
                {
                    ikData[target].limbDirection = Mathf.Repeat(ikData[target].limbDirection + add + 180f, 360f) - 180f;
                }
                break;
            default:
                return false;
            }
            UpdateSolver(target);
            va.SetUpdateIKtargetOriginalIK(target);
            return true;
        }

        private void UpdateGUIContentStrings()
        {
            for (int i = 0; i <= (int)SolverType.LimbIK; i++)
            {
                SolverTypeStrings[i] = new GUIContent(Language.GetContent(Language.Help.OriginalIKTypeCcdIK + i));
            }
            for (int i = 0; i < (int)OriginalIKData.SpaceType.Total; i++)
            {
                IKSpaceTypeStrings[i] = new GUIContent(Language.GetContent(Language.Help.SelectionOriginalIKSpaceTypeGlobal + i));
            }
        }

        private void UpdateReorderableList()
        {
            ikReorderableList = null;
            if (ikData == null) return;
            ikReorderableList = new ReorderableList(ikData, typeof(OriginalIKData), true, true, true, true);
            ikReorderableList.drawHeaderCallback = (Rect rect) =>
            {
                float x = rect.x;
                {
                    const float ButtonWidth = 100f;
                    #region Add
                    {
                        var r = rect;
                        r.width = ButtonWidth;
                        if (GUI.Button(r, Language.GetContent(Language.Help.OriginalIKTemplate), EditorStyles.toolbarDropDown))
                        {
                            var originalIKTemplates = new Dictionary<string, string>();
                            {
                                var guids = AssetDatabase.FindAssets("t:originaliktemplate");
                                for (int i = 0; i < guids.Length; i++)
                                {
                                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                                    var name = path.Remove(0, "Assets/".Length);
                                    originalIKTemplates.Add(name, path);
                                }
                            }

                            var menu = new GenericMenu();
                            {
                                var enu = originalIKTemplates.GetEnumerator();
                                while (enu.MoveNext())
                                {
                                    var value = enu.Current.Value;
                                    menu.AddItem(new GUIContent(enu.Current.Key), false, () =>
                                    {
                                        var originalIKTemplate = AssetDatabase.LoadAssetAtPath<OriginalIKTemplate>(value);
                                        if (originalIKTemplate != null)
                                        {
                                            Undo.RecordObject(vaw, "Template OriginalIK");
                                            LoadIKSaveSettings(originalIKTemplate.originalIkData);
                                        }
                                    });
                                }
                            }
                            menu.ShowAsContext();
                        }
                    }
                    #endregion
                    #region Clear
                    {
                        var r = rect;
                        r.xMin += ButtonWidth;
                        r.width = ButtonWidth;
                        if (GUI.Button(r, "Clear", EditorStyles.toolbarButton))
                        {
                            Undo.RecordObject(vaw, "Clear Original IK Data");
                            ikData.Clear();
                            ikReorderableList.index = -1;
                            ikTargetSelect = null;
                            vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.All);
                        }
                    }
                    #endregion
                    #region Save as
                    {
                        var r = rect;
                        r.width = ButtonWidth;
                        r.x = rect.xMax - r.width;
                        if (GUI.Button(r, Language.GetContent(Language.Help.OriginalIKSaveAs), EditorStyles.toolbarButton))
                        {
                            string path = EditorUtility.SaveFilePanel("Save as OriginalIK Template", vae.templateSaveDefaultDirectory, string.Format("{0}_OriginalIK.asset", vaw.gameObject.name), "asset");
                            if (!string.IsNullOrEmpty(path))
                            {
                                if (!path.StartsWith(Application.dataPath))
                                {
                                    EditorCommon.SaveInsideAssetsFolderDisplayDialog();
                                }
                                else
                                {
                                    vae.templateSaveDefaultDirectory = Path.GetDirectoryName(path);
                                    path = FileUtil.GetProjectRelativePath(path);
                                    var originalIKTemplate = ScriptableObject.CreateInstance<OriginalIKTemplate>();
                                    {
                                        originalIKTemplate.originalIkData = SaveIKSaveSettings();
                                    }
                                    try
                                    {
                                        VeryAnimationWindow.CustomAssetModificationProcessor.Pause();
                                        AssetDatabase.CreateAsset(originalIKTemplate, path);
                                    }
                                    finally
                                    {
                                        VeryAnimationWindow.CustomAssetModificationProcessor.Resume();
                                    }
                                    vac.Focus();
                                }
                            }
                        }
                    }
                    #endregion
                }
            };
            ikReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (index >= ikData.Count)
                    return;
                var isValid = ikData[index].solver != null && ikData[index].solver.isValid;

                var saveColor = GUI.backgroundColor;

                float x = rect.x;
                {
                    var r = rect;
                    r.x = x;
                    r.y += 2;
                    r.height -= 2;
                    r.width = 16;
                    rect.xMin += r.width;
                    x = rect.x;
                    if (!isValid)
                    {
                        ikData[index].enable = false;
                        advancedFoldout = true;
                        GUI.backgroundColor = Color.red;
                    }
                    {
                        EditorGUI.BeginChangeCheck();
                        EditorGUI.Toggle(r, ikData[index].enable);
                        if (EditorGUI.EndChangeCheck())
                        {
                            ChangeTargetIK(index);
                        }
                    }
                    GUI.backgroundColor = saveColor;
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
                    EditorGUI.LabelField(r, ikData[index].name);
                }

                {
                    var r = rect;
                    r.width = 100f;
                    r.x = rect.xMax - r.width - 14;
                    EditorGUI.LabelField(r, string.Format("{0} : {1}", SolverTypeStrings[(int)ikData[index].solverType].text, IKSpaceTypeStrings[(int)ikData[index].spaceType].text), vaw.guiStyleMiddleRightGreyMiniLabel);
                }

                EditorGUI.EndDisabledGroup();

                if (ikReorderableList.index == index)
                {
                    var r = rect;
                    r.y += 2;
                    r.height -= 2;
                    r.width = 12;
                    r.x = rect.xMax - r.width;
                    advancedFoldout = EditorGUI.Foldout(r, advancedFoldout, new GUIContent("", "Advanced"), true);
                }
            };
            ikReorderableList.onCanAddCallback = (ReorderableList list) =>
            {
                if (va.selectionActiveBone < 0) return false;
                if (va.isHuman && va.humanoidConflict[va.selectionActiveBone]) return false;
                return ikData.FindIndex((data) => data.tip == va.editBones[va.selectionActiveBone]) < 0;
            };
            ikReorderableList.onAddCallback = (ReorderableList list) =>
            {
                va.originalIK.ChangeSelectionIK();
                vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.All);
            };
            ikReorderableList.onCanRemoveCallback = (ReorderableList list) =>
            {
                return list.index >= 0 && list.index < ikData.Count;
            };
            ikReorderableList.onRemoveCallback = (ReorderableList list) =>
            {
                Undo.RecordObject(vaw, "Change Original IK Data");
                ikData.RemoveAt(list.index);
                ikTargetSelect = null;
                vaw.SetRepaintGUI(VeryAnimationWindow.RepaintGUI.All);
            };
            ikReorderableList.onSelectCallback = (ReorderableList list) =>
            {
                if (list.index >= 0 && list.index < ikData.Count)
                {
                    if (ikData[list.index].enable)
                        va.SelectOriginalIKTargetPlusKey(list.index);
                    else
                    {
                        var index = list.index;
                        va.SelectGameObject(ikData[list.index].tip);
                        list.index = index;
                    }
                }
            };
        }
        private void UpdateSolver(int target)
        {
            ikData[target].solver = null;
            if (target < 0 || target >= ikData.Count || ikData[target].joints == null) return;
            var joints = new GameObject[ikData[target].level];
            for (int i = 0; i < ikData[target].level; i++)
            {
                var boneIndex = va.BonesIndexOf(ikData[target].joints[i].bone);
                ikData[target].joints[i].boneIndex = boneIndex;
                joints[i] = boneIndex >= 0 ? va.editBones[boneIndex] : null;
            }
            switch (ikData[target].solverType)
            {
            case SolverType.CcdIK:
                {
                    var solverCcd = new SolverCCD();
                    solverCcd.Initialize(va.editBones, joints);
                    ikData[target].solver = solverCcd;
                }
                break;
            case SolverType.LimbIK:
                {
                    var solverLimb = new SolverLimb();
                    solverLimb.Initialize(va.editBones, joints);
                    ikData[target].solver = solverLimb;
                }
                break;
            }
            SetSolverParam(target);
        }

        private bool IsErrorJoint(int target, int index)
        {
            var data = ikData[target];
            if (target < 0 || target >= ikData.Count || data.joints == null) 
                return true;
            if (index < 0 || index >= data.level || data.joints[index].bone == null)
                return true;
            {
                var indexBone = data.joints[index].bone.transform;
                var t = data.joints[0].bone.transform;
                int level = -1;
                while (t != null)
                {
                    level = data.joints.FindIndex(x => x.bone == t.gameObject);
                    if (t == indexBone)
                        break;
                    if (level > index)
                        break;
                    t = t.parent;
                }
                if (level != index)
                    return true;
            }
            return false;
        }
        private bool IsValidAddBone(GameObject gameObject)
        {
            var boneIndex = va.BonesIndexOf(gameObject);
            if (boneIndex < 0) return false;
            if (va.isHuman && va.humanoidConflict[boneIndex]) return false;
            if (ikData.FindIndex((d) =>
            {
                if (d.joints == null) return false;
                for (int i = 0; i < d.level; i++)
                {
                    if (d.joints[i].bone == va.bones[boneIndex])
                        return true;
                }
                return false;
            }) >= 0) return false;
            return true;
        }
        private int GetMirrorTarget(int target)
        {
            if (target >= 0 && target < ikData.Count)
            {
                var boneIndex = va.EditBonesIndexOf(ikData[target].tip);
                if (boneIndex >= 0 && va.mirrorBoneIndexes[boneIndex] >= 0)
                {
                    for (int i = 0; i < ikData.Count; i++)
                    {
                        if (ikData[i].tip == va.editBones[va.mirrorBoneIndexes[boneIndex]])
                            return i;
                    }
                }
            }
            return -1;
        }

        public void OnSelectionChange()
        {
            if (ikReorderableList != null)
            {
                if (ikActiveTarget >= 0 && ikActiveTarget < ikReorderableList.count)
                {
                    ikReorderableList.index = ikActiveTarget;
                }
                else
                {
                    ikReorderableList.index = -1;
                }
            }

            if (ikTargetSelect != null)
            {
                foreach (var target in ikTargetSelect)
                {
                    SetSynchroIKtargetOriginalIK(target);
                }
            }
        }

        public void UpdateSynchroIKSet()
        {
            for (int i = 0; i < ikData.Count; i++)
            {
                if (ikData[i].enable && ikData[i].synchroIKtarget)
                {
                    SynchroSet(i);
                }
                ikData[i].synchroIKtarget = false;
            }
        }
        public void SynchroSet(int target)
        {
            if (target < 0 || target >= ikData.Count) return;
            var data = ikData[target];
            if (data.solver == null || !data.solver.isValid) return;

            data.rootBoneIndex = data.joints.Count > 0 ? data.joints[data.level - 1].boneIndex : -1;
            data.parentBoneIndex = va.BonesIndexOf(data.parent);
            foreach (var joint in data.joints)
            {
                joint.boneIndex = va.BonesIndexOf(joint.bone);
            }

            var t = va.editBones[data.solver.boneIndexes[0]].transform;
            switch (data.spaceType)
            {
            case OriginalIKData.SpaceType.Global:
            case OriginalIKData.SpaceType.Local:
                data.worldPosition = t.position;
                data.worldRotation = t.rotation;
                data.swivel = data.solver.GetSwivel();
                break;
            case OriginalIKData.SpaceType.Parent:
                //not update
                break;
            }
        }

        private void SetSolverParam(int target)
        {
            if (target < 0 || target >= ikData.Count) return;
            var data = ikData[target];
            if (!data.isValid) return;
            switch (data.solverType)
            {
            case SolverType.CcdIK:
                {
                    var solverCcd = data.solver as SolverCCD;
                    solverCcd.resetRotations = data.resetRotations;
                    solverCcd.swivel = data.swivel;
                    for (int i = 0; i < data.level; i++)
                    {
                        solverCcd.weights[i] = data.joints[i].weight;
                    }
                }
                break;
            case SolverType.LimbIK:
                {
                    var solverLimb = data.solver as SolverLimb;
                    solverLimb.swivel = data.swivel;
                    solverLimb.direction = data.limbDirection;
                }
                break;
            case SolverType.LookAt:
                break;
            }
        }
        public void UpdateIK()
        {
            if (!GetUpdateIKtargetAll()) return;

            for (int boneIndex = 0; boneIndex < updateRotations.Length; boneIndex++)
            {
                if (updateRotations[boneIndex] == null) continue;
                updateRotations[boneIndex].Clear();
            }
            tmpCurves.Clear();

            #region Loop
            int loopCount = 1;
            {
                foreach (var data in ikData)
                {
                    if (data.isUpdate &&
                        data.spaceType == OriginalIKData.SpaceType.Parent &&
                        data.parentBoneIndex >= 0)
                    {
                        loopCount = Math.Max(loopCount, 2);
                    }
                }
            }
            for (int loop = 0; loop < loopCount; loop++)
            {
                va.SampleAnimation(va.currentTime, VeryAnimation.EditObjectFlag.Edit);

                #region Update
                {
                    #region OriginalIK
                    for (int i = 0; i < ikData.Count; i++)
                    {
                        var data = ikData[i];
                        if (!data.isUpdate)
                            continue;
                        SetSolverParam(i);
                        {
                            Quaternion? worldRotation = null;
                            if (!data.autoRotation)
                                worldRotation = data.worldRotation;
                            data.solver.Update(data.worldPosition, worldRotation);
                        }
                    }
                    #endregion
                }
                #endregion

                #region SetValue
                {
                    for (int target = 0; target < ikData.Count; target++)
                    {
                        var data = ikData[target];
                        if (!data.isUpdate)
                            continue;
                        for (int i = 0; i < data.solver.boneIndexes.Length; i++)
                        {
                            var boneIndex = data.solver.boneIndexes[i];
                            if (boneIndex < 0) continue;
                            if (updateRotations[boneIndex] == null)
                            {
                                updateRotations[boneIndex] = new List<UpdateData>();
                            }
                            updateRotations[boneIndex].Add(new UpdateData() { time = va.currentTime, rotation = data.solver.boneTransforms[i].localRotation });
                        }
                    }
                }
                #endregion

                #region Write
                for (int boneIndex = 0; boneIndex < updateRotations.Length; boneIndex++)
                {
                    if (updateRotations[boneIndex] == null || updateRotations[boneIndex].Count == 0) continue;
                    var mode = va.IsHaveAnimationCurveTransformRotation(boneIndex);
                    if (mode == URotationCurveInterpolation.Mode.Undefined)
                        mode = URotationCurveInterpolation.Mode.RawQuaternions;
                    if (mode == URotationCurveInterpolation.Mode.RawQuaternions)
                    {
                        #region RawQuaternions
                        for (int dof = 0; dof < 4; dof++)
                        {
                            tmpCurves.curves[dof] = va.GetAnimationCurveTransformRotation(boneIndex, dof, mode);
                        }
                        for (int i = 0; i < updateRotations[boneIndex].Count; i++)
                        {
                            var rotation = va.FixReverseRotationQuaternion(tmpCurves.curves, updateRotations[boneIndex][i].time, updateRotations[boneIndex][i].rotation);
                            for (int dof = 0; dof < 4; dof++)
                            {
                                va.SetKeyframe(tmpCurves.curves[dof], updateRotations[boneIndex][i].time, rotation[dof]);
                            }
                        }
                        for (int dof = 0; dof < 4; dof++)
                        {
                            va.SetAnimationCurveTransformRotation(boneIndex, dof, mode, tmpCurves.curves[dof]);
                        }
                        #endregion
                    }
                    else
                    {
                        #region RawEuler
                        for (int dof = 0; dof < 3; dof++)
                        {
                            tmpCurves.curves[dof] = va.GetAnimationCurveTransformRotation(boneIndex, dof, mode);
                        }
                        for (int i = 0; i < updateRotations[boneIndex].Count; i++)
                        {
                            var eulerAngles = va.FixReverseRotationEuler(tmpCurves.curves, updateRotations[boneIndex][i].time, updateRotations[boneIndex][i].rotation.eulerAngles);
                            for (int dof = 0; dof < 3; dof++)
                            {
                                va.SetKeyframe(tmpCurves.curves[dof], updateRotations[boneIndex][i].time, eulerAngles[dof]);
                            }
                        }
                        for (int dof = 0; dof < 3; dof++)
                        {
                            va.SetAnimationCurveTransformRotation(boneIndex, dof, mode, tmpCurves.curves[dof]);
                        }
                        #endregion
                    }
                    updateRotations[boneIndex].Clear();
                }
                tmpCurves.Clear();
                #endregion
            }
            #endregion
        }

        public void HandleGUI()
        {
            if (ikTargetSelect == null || ikTargetSelect.Length <= 0) return;
            if (ikActiveTarget < 0) return;
            var activeData = ikData[ikActiveTarget];
            if (!activeData.isValid) return;

            var worldPosition = activeData.worldPosition;
            var worldRotation = activeData.worldRotation;

            {
                if ((activeData.solverType == SolverType.CcdIK && activeData.resetRotations) ||
                    activeData.solverType == SolverType.LimbIK)
                {
                    #region IKSwivel
                    var posA = va.editBones[activeData.solver.boneIndexes[activeData.solver.boneIndexes.Length - 1]].transform.position;
                    var posB = worldPosition;
                    var axis = posB - posA;
                    axis.Normalize();
                    if (axis.sqrMagnitude > 0f)
                    {
                        var posP = Vector3.Lerp(posA, posB, 0.5f);
                        {
                            Handles.color = new Color(Handles.centerColor.r, Handles.centerColor.g, Handles.centerColor.b, Handles.centerColor.a * 0.5f);
                            Handles.DrawWireDisc(posP, axis, HandleUtility.GetHandleSize(posP));
                            Handles.color = Handles.centerColor;
                            Handles.DrawLine(posP, posP + activeData.solver.GetBasicDir() * HandleUtility.GetHandleSize(posP));
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
                                foreach (var ikTarget in ikTargetSelect)
                                {
                                    int target = (int)ikTarget;
                                    ikData[target].swivel = Mathf.Repeat(ikData[target].swivel - rotDist  + 180f, 360f) - 180f;
                                    SetUpdateIKtargetOriginalIK(target);
                                }
                            }
                        }
                    }
                    #endregion

                    #region Diection
                    if (activeData.solverType == SolverType.LimbIK && activeData.solver.isValid)
                    {
                        var solverLimb = activeData.solver as SolverLimb;
                        var vUpper = (solverLimb.upper.position - solverLimb.lower.position).normalized;
                        var vTip = (solverLimb.tip.position - solverLimb.lower.position).normalized;
                        float angle = 0f;
                        {
                            var rot = Quaternion.FromToRotation(vUpper, vTip);
                            Vector3 tmp;
                            rot.ToAngleAxis(out angle, out tmp);
                            if (angle > 180f) angle = 360f - angle;
                        }
                        var length = Mathf.Min(Vector3.Distance(solverLimb.lower.position, solverLimb.upper.position), Vector3.Distance(solverLimb.tip.position, solverLimb.lower.position)) / 4f;
                        Handles.color = vaw.editorSettings.settingIKTargetActiveColor;
                        Handles.DrawSolidArc(solverLimb.lower.position, solverLimb.GetBasicDir(), vTip, angle, length);
                    }
                    #endregion
                }
                if (!activeData.autoRotation && va.lastTool != Tool.Move)
                {
                    #region Rotate
                    EditorGUI.BeginChangeCheck();
                    var rotation = Handles.RotationHandle(Tools.pivotRotation == PivotRotation.Local ? worldRotation : Tools.handleRotation, worldPosition);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Rotate IK Target");
                        if (Tools.pivotRotation == PivotRotation.Local)
                        {
                            var move = Quaternion.Inverse(worldRotation) * rotation;
                            foreach (var target in ikTargetSelect)
                            {
                                ikData[target].worldRotation = ikData[target].worldRotation * move;
                                {   //Handles.ConeCap -> Quaternion To Matrix conversion failed because input Quaternion is invalid
                                    float angle;
                                    Vector3 axis;
                                    ikData[target].worldRotation.ToAngleAxis(out angle, out axis);
                                    ikData[target].worldRotation = Quaternion.AngleAxis(angle, axis);
                                }
                                va.SetUpdateIKtargetOriginalIK(target);
                            }
                        }
                        else
                        {
                            float angle;
                            Vector3 axis;
                            (Quaternion.Inverse(Tools.handleRotation) * rotation).ToAngleAxis(out angle, out axis);
                            var move = Quaternion.Inverse(worldRotation) * Quaternion.AngleAxis(angle, Tools.handleRotation * axis) * worldRotation;
                            foreach (var target in ikTargetSelect)
                            {
                                ikData[target].worldRotation = ikData[target].worldRotation * move;
                                {   //Handles.ConeCap -> Quaternion To Matrix conversion failed because input Quaternion is invalid
                                    ikData[target].worldRotation.ToAngleAxis(out angle, out axis);
                                    ikData[target].worldRotation = Quaternion.AngleAxis(angle, axis);
                                }
                                va.SetUpdateIKtargetOriginalIK(target);
                                Tools.handleRotation = rotation;
                            }
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
                            ikData[target].worldPosition = ikData[target].worldPosition + move;
                            va.SetUpdateIKtargetOriginalIK(target);
                        }
                    }
                    #endregion
                }
            }
        }
        public void TargetGUI()
        {
            var e = Event.current;

            for (int target = 0; target < ikData.Count; target++)
            {
                if (!ikData[target].isValid) continue;

                var worldPosition = ikData[target].worldPosition;
                var worldRotation = ikData[target].worldRotation;

                if (ikTargetSelect != null &&
                    EditorCommon.ArrayContains(ikTargetSelect, target))
                {
                    #region Active
                    {
                        if (target == ikActiveTarget)
                        {
                            Handles.color = Color.white;
                            var boneIndex = ikData[target].solver.boneIndexes[ikData[target].solver.boneIndexes.Length - 1];
                            var worldPosition2 = va.editBones[boneIndex].transform.position;
                            Handles.DrawLine(worldPosition, worldPosition2);
                        }
                        Handles.color = vaw.editorSettings.settingIKTargetActiveColor;
                        if (ikData[target].solverType == SolverType.LookAt)
                            Handles.SphereHandleCap(0, worldPosition, worldRotation, HandleUtility.GetHandleSize(worldPosition) * vaw.editorSettings.settingIKTargetSize, EventType.Repaint);
                        else
                            Handles.CubeHandleCap(0, worldPosition, worldRotation, HandleUtility.GetHandleSize(worldPosition) * vaw.editorSettings.settingIKTargetSize, EventType.Repaint);
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
                        if (ikData[target].solverType == SolverType.LookAt)
                            Handles.SphereHandleCap(id, worldPosition, worldRotation, HandleUtility.GetHandleSize(worldPosition) * vaw.editorSettings.settingIKTargetSize, eventType);
                        else
                            Handles.CubeHandleCap(id, worldPosition, worldRotation, HandleUtility.GetHandleSize(worldPosition) * vaw.editorSettings.settingIKTargetSize, eventType);
                    });
                    if (GUIUtility.hotControl == freeMoveHandleControlID)
                    {
                        if (e.type == EventType.Layout)
                        {
                            GUIUtility.hotControl = -1;
                            {
                                var ikTarget = target;
                                EditorApplication.delayCall += () =>
                                {
                                    va.SelectOriginalIKTargetPlusKey(ikTarget);
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
            if (ikActiveTarget < 0) return;
            var activeData = ikData[ikActiveTarget];
            if (!activeData.isValid) return;
            #region IK
            {
                EditorGUILayout.BeginHorizontal();
                #region Mirror
                {
                    var mirrorTarget = GetMirrorTarget(ikActiveTarget);
                    if (GUILayout.Button(Language.GetContentFormat(Language.Help.SelectionMirror, (mirrorTarget >= 0 ? string.Format("From 'IK: {0}'", ikData[mirrorTarget].name) : "From self"))))
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
                        va.SetUpdateIKtargetOriginalIK(target);
                    }
                }
                #endregion
                EditorGUILayout.Space();
                #region Sync
                EditorGUI.BeginDisabledGroup(activeData.spaceType == OriginalIKData.SpaceType.Parent);
                if (GUILayout.Button(Language.GetContent(Language.Help.SelectionSyncIK)))
                {
                    Undo.RecordObject(vaw, "Sync IK");
                    foreach (var target in ikTargetSelect)
                    {
                        va.SetSynchroIKtargetOriginalIK(target);
                    }
                }
                EditorGUI.EndDisabledGroup();
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
                var spaceType = (OriginalIKData.SpaceType)GUILayout.Toolbar((int)activeData.spaceType, IKSpaceTypeStrings, EditorStyles.miniButton);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(vaw, "Change IK Position");
                    foreach (var target in ikTargetSelect)
                    {
                        ChangeSpaceType(target, spaceType);
                    }
                    vac.Repaint();
                }
                EditorGUILayout.EndHorizontal();
            }
            #endregion
            #region Parent
            if (activeData.spaceType == OriginalIKData.SpaceType.Local || activeData.spaceType == OriginalIKData.SpaceType.Parent)
            {
                EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                EditorGUILayout.LabelField("Parent", GUILayout.Width(60));
                EditorGUI.BeginChangeCheck();
                if (activeData.spaceType == OriginalIKData.SpaceType.Local)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(activeData.root != null && activeData.root.transform.parent != null ? activeData.root.transform.parent.gameObject : null, typeof(GameObject), true);
                    EditorGUI.EndDisabledGroup();
                }
                else if (activeData.spaceType == OriginalIKData.SpaceType.Parent)
                {
                    var parent = EditorGUILayout.ObjectField(activeData.parent, typeof(GameObject), true) as GameObject;
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change IK Position");
                        foreach (var target in ikTargetSelect)
                        {
                            var data = ikData[target];
                            var worldPosition = data.worldPosition;
                            var worldRotation = data.worldRotation;
                            data.parent = parent;
                            data.worldPosition = worldPosition;
                            data.worldRotation = worldRotation;
                            va.SetSynchroIKtargetOriginalIK(target);
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
                        ikData[target].position += move;
                        va.SetUpdateIKtargetOriginalIK(target);
                    }
                }
                if (activeData.spaceType == OriginalIKData.SpaceType.Parent)
                {
                    if (GUILayout.Button("Reset", GUILayout.Width(44)))
                    {
                        Undo.RecordObject(vaw, "Change IK Position");
                        foreach (var target in ikTargetSelect)
                        {
                            ikData[target].position = Vector3.zero;
                            va.SetUpdateIKtargetOriginalIK(target);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            #endregion
            if (activeData.solverType == SolverType.CcdIK || activeData.solverType == SolverType.LimbIK)
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
                                ikData[target].autoRotation = autoRotation;
                                SynchroSet(target);
                                va.SetUpdateIKtargetOriginalIK(target);
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
                                ikData[target].rotation.eulerAngles += move;
                                va.SetUpdateIKtargetOriginalIK(target);
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Auto", EditorStyles.centeredGreyMiniLabel);
                    }
                    if (activeData.spaceType == OriginalIKData.SpaceType.Parent)
                    {
                        if (GUILayout.Button("Reset", GUILayout.Width(44)))
                        {
                            Undo.RecordObject(vaw, "Change IK Rotation");
                            foreach (var target in ikTargetSelect)
                            {
                                ikData[target].rotation = Quaternion.identity;
                                va.SetUpdateIKtargetOriginalIK(target);
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            if ((activeData.solverType == SolverType.CcdIK && activeData.resetRotations) ||
                activeData.solverType == SolverType.LimbIK)
            {
                #region Swivel
                {
                    EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                    EditorGUILayout.LabelField("Swivel", GUILayout.Width(60));
                    EditorGUI.BeginChangeCheck();
                    var swivel = EditorGUILayout.Slider(activeData.swivel, -180f, 180f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(vaw, "Change IK Swivel");
                        var move = swivel - activeData.swivel;
                        foreach (var target in ikTargetSelect)
                        {
                            ikData[target].swivel = Mathf.Repeat(ikData[target].swivel + move + 180f, 360f) - 180f;
                            SetUpdateIKtargetOriginalIK(target);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                #endregion
            }
            #endregion
        }
        public void ControlGUI()
        {
            var saveColor = GUI.backgroundColor;

            EditorGUILayout.BeginVertical(vaw.guiStyleSkinBox);
            if (ikReorderableList != null)
            {
                ikReorderableList.DoLayoutList();
                if (advancedFoldout && ikReorderableList.index >= 0 && ikReorderableList.index < ikData.Count)
                {
                    advancedFoldout = EditorGUILayout.Foldout(advancedFoldout, "Advanced", true);
                    EditorGUILayout.BeginVertical(vaw.guiStyleSkinBox);
                    var target = ikReorderableList.index;
                    {
                        EditorGUI.BeginChangeCheck();
                        var name = EditorGUILayout.TextField("Name", ikData[target].name);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(vaw, "Change Original IK Data");
                            ikData[target].name = name;
                        }
                    }
                    {
                        {
                            EditorGUI.BeginChangeCheck();
                            var type = (SolverType)EditorGUILayout.Popup(new GUIContent("Type"), (int)ikData[target].solverType, SolverTypeStrings);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vaw, "Change Original IK Data");
                                ChangeSolverType(target, type);
                            }
                        }
                        EditorGUI.indentLevel++;
                        {
                            if (ikData[target].solverType == SolverType.CcdIK || ikData[target].solverType == SolverType.LookAt)
                            {
                                #region CcdIK || LookAt
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var resetRotations = EditorGUILayout.Toggle(Language.GetContent(Language.Help.OriginalIKResetRotations), ikData[target].resetRotations);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        Undo.RecordObject(vaw, "Change Original IK Data");
                                        ikData[target].resetRotations = resetRotations;
                                        va.SetUpdateIKtargetOriginalIK(target);
                                    }
                                }
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var level = EditorGUILayout.Popup(new GUIContent("Level"), ikData[target].level - SolverLevelMin, SolverLevelStrings) + SolverLevelMin;
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        Undo.RecordObject(vaw, "Change Original IK Data");
                                        ChangeTypeSetting(target, level - ikData[target].level);
                                    }
                                }
                                #endregion
                            }
                            else if (ikData[target].solverType == SolverType.LimbIK)
                            {
                                #region LimbIK
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var limbDirection = EditorGUILayout.Slider(Language.GetContent(Language.Help.OriginalIKDirection), ikData[target].limbDirection, -180f, 180f);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        Undo.RecordObject(vaw, "Change Original IK Data");
                                        ChangeTypeSetting(target, limbDirection - ikData[target].limbDirection);
                                    }
                                }
                                #endregion
                            }
                            EditorGUI.indentLevel++;
                            if (ikData[target].joints != null)
                            {
                                {
                                    EditorGUI.BeginDisabledGroup(true);
                                    EditorGUILayout.ObjectField("Target", ikData[target].joints[0].bone, typeof(GameObject), true);
                                    EditorGUI.EndDisabledGroup();
                                }
                                bool haveError = false;
                                if (ikData[target].solverType == SolverType.CcdIK)
                                {
                                    #region CcdIK
                                    for (int i = 1; i < ikData[target].level; i++)
                                    {
                                        var isError = IsErrorJoint(target, i);
                                        if (isError) { GUI.backgroundColor = Color.red; haveError = true; }
                                        else GUI.backgroundColor = saveColor;

                                        var data = ikData[target];

                                        EditorGUILayout.BeginHorizontal();
                                        {
                                            {
                                                EditorGUI.BeginChangeCheck();
                                                var foldout = EditorGUILayout.Foldout(data.joints[i].foldout, i.ToString(), true);
                                                if (EditorGUI.EndChangeCheck())
                                                {
                                                    Undo.RecordObject(vaw, "Change Original IK Data");
                                                    var joint = data.joints[i];
                                                    joint.foldout = foldout;
                                                    data.joints[i] = joint;
                                                }
                                            }
                                            {
                                                EditorGUI.BeginChangeCheck();
                                                var bone = EditorGUILayout.ObjectField(data.joints[i].bone, typeof(GameObject), true) as GameObject;
                                                if (EditorGUI.EndChangeCheck())
                                                {
                                                    Undo.RecordObject(vaw, "Change Original IK Data");
                                                    var joint = data.joints[i];
                                                    joint.bone = bone;
                                                    data.joints[i] = joint;
                                                    UpdateSolver(target);
                                                }
                                            }
                                        }
                                        EditorGUILayout.EndHorizontal();

                                        if (data.joints[i].foldout)
                                        {
                                            EditorGUI.indentLevel++;
                                            {
                                                if (ikData[target].solverType == SolverType.CcdIK || ikData[target].solverType == SolverType.LookAt)
                                                {
                                                    EditorGUI.BeginChangeCheck();
                                                    var weight = EditorGUILayout.Slider("Weight", data.joints[i].weight, 0f, 1f);
                                                    if (EditorGUI.EndChangeCheck())
                                                    {
                                                        Undo.RecordObject(vaw, "Change Original IK Data");
                                                        var joint = data.joints[i];
                                                        joint.weight = weight;
                                                        data.joints[i] = joint;
                                                        UpdateSolver(target);
                                                        SetUpdateIKtargetOriginalIK(target);
                                                    }
                                                }
                                            }
                                            EditorGUI.indentLevel--;
                                        }

                                        GUI.backgroundColor = saveColor;
                                    }
                                    #endregion
                                }
                                else if (ikData[target].solverType == SolverType.LimbIK)
                                {
                                    #region LimbIK
                                    for (int i = 1; i < ikData[target].level; i++)
                                    {
                                        var isError = IsErrorJoint(target, i);
                                        if (isError) { GUI.backgroundColor = Color.red; haveError = true; }
                                        else GUI.backgroundColor = saveColor;

                                        var data = ikData[target];

                                        {
                                            EditorGUI.BeginChangeCheck();
                                            var bone = EditorGUILayout.ObjectField(i == 1 ? "Lower" : "Upper", data.joints[i].bone, typeof(GameObject), true) as GameObject;
                                            if (EditorGUI.EndChangeCheck())
                                            {
                                                Undo.RecordObject(vaw, "Change Original IK Data");
                                                var joint = data.joints[i];
                                                joint.bone = bone;
                                                data.joints[i] = joint;
                                                UpdateSolver(target);
                                            }
                                        }

                                        GUI.backgroundColor = saveColor;
                                    }
                                    #endregion
                                }
                                if (haveError)
                                {
                                    EditorGUILayout.HelpBox(Language.GetText(Language.Help.OriginalIKPleasespecifyGameObject), MessageType.Error);
                                }
                            }
                            EditorGUI.indentLevel--;
                        }
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(Language.GetContent(Language.Help.OriginalIKChangeAll)))
                    {
                        Undo.RecordObject(vaw, "Change Original IK Data");
                        bool flag = !ikData.Any(v => !v.enable);
                        for (int target = 0; target < ikData.Count; target++)
                        {
                            ikData[target].enable = !flag;
                            if (ikData[target].enable)
                            {
                                UpdateSolver(target);
                                SynchroSet(target);
                            }
                        }
                    }
                    EditorGUILayout.Space();
                    if (GUILayout.Button(Language.GetContent(Language.Help.OriginalIKSelectAll)))
                    {
                        var list = new List<int>();
                        for (int target = 0; target < ikData.Count; target++)
                        {
                            if (ikData[target].enable)
                                list.Add(target);
                        }
                        va.SelectIKTargets(null, list.ToArray());
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();

            GUI.backgroundColor = saveColor;
        }

        private void UndoRedoPerformed()
        {
            if (ikData == null) return;

            for (int target = 0; target < ikData.Count; target++)
            {
                UpdateSolver(target);
            }
        }

        private void Reset(int target)
        {
            var data = ikData[target];
            switch (data.solverType)
            {
            case SolverType.LookAt:
                {
                    var t = va.editGameObject.transform;
                    Vector3 vec = data.worldPosition - t.position;
                    var normal = t.rotation * Vector3.right;
                    var dot = Vector3.Dot(vec, normal);
                    data.worldPosition -= normal * dot;
                }
                break;
            default:
                {
                    var boneIndex = va.EditBonesIndexOf(data.tip);
                    data.worldRotation = data.tip.transform.parent.rotation * va.boneSaveTransforms[boneIndex].localRotation;
                }
                break;
            }
            va.SetUpdateIKtargetOriginalIK(target);
        }

        private void ChangeSpaceType(int target, OriginalIKData.SpaceType spaceType)
        {
            if (target < 0 || target >= ikData.Count) return;
            var data = ikData[target];
            if (data.spaceType == spaceType) return;
            var position = data.worldPosition;
            var rotation = data.worldRotation;
            data.spaceType = spaceType;
            data.worldPosition = position;
            data.worldRotation = rotation;
        }

        public int IsIKBone(HumanBodyBones humanoidIndex)
        {
            var boneIndex = va.humanoidIndex2boneIndex[(int)humanoidIndex];
            if (boneIndex < 0) return -1;
            return IsIKBone(boneIndex);
        }
        public int IsIKBone(int boneIndex)
        {
            for (int i = 0; i < ikData.Count; i++)
            {
                if (!ikData[i].enable || ikData[i].joints == null) continue;
                for (int j = 0; j < ikData[i].level; j++)
                {
                    if (ikData[i].joints[j].bone == va.bones[boneIndex])
                        return i;
                }
            }
            return -1;
        }

        public void ChangeTargetIK(int target)
        {
            Undo.RecordObject(vaw, "Change IK");
            if (ikData[target].enable)
            {
                List<GameObject> selectGameObjects = new List<GameObject>();
                ikData[target].enable = false;
                if (ikData[target].tip != null)
                    selectGameObjects.Add(ikData[target].tip);
                va.SelectGameObjects(selectGameObjects.ToArray());
            }
            else
            {
                ikData[target].enable = true;
                UpdateSolver(target);
                SynchroSet(target);
                va.SelectOriginalIKTargetPlusKey(target);
            }
        }
        public bool ChangeSelectionIK()
        {
            Undo.RecordObject(vaw, "Change IK");
            if (ikTargetSelect != null && ikTargetSelect.Length > 0)
            {
                List<GameObject> selectGameObjects = new List<GameObject>();
                foreach (var target in ikTargetSelect)
                {
                    if (target < 0 || target >= ikData.Count) continue;
                    ikData[target].enable = false;
                    if (ikData[target].tip != null)
                    {
                        var boneIndex = va.EditBonesIndexOf(ikData[target].tip);
                        if (boneIndex >= 0)
                            selectGameObjects.Add(va.bones[boneIndex]);
                    }
                }
                if (selectGameObjects.Count > 0)
                {
                    va.SelectGameObjects(selectGameObjects.ToArray());
                    return true;
                }
            }
            else
            {
                HashSet<int> selectIkTargets = new HashSet<int>();
                foreach (var boneIndex in va.selectionBones)
                {
                    var target = ikData.FindIndex((data) =>
                    {
                        if (data.joints == null) return false;
                        return data.joints.FindIndex((joint) => joint.bone == va.bones[boneIndex]) >= 0;
                    });
                    if (target < 0)
                    {
                        target = CreateIKData(va.bones[boneIndex]);
                        if (target >= 0)
                        {
                            selectIkTargets.Add(target);
                        }
                    }
                    else
                    {
                        selectIkTargets.Add(target);
                    }
                }
                if (selectIkTargets.Count > 0)
                {
                    foreach (var target in selectIkTargets)
                    {
                        ikData[target].enable = true;
                        UpdateSolver(target);
                        SynchroSet(target);
                    }
                    va.SelectIKTargets(null, selectIkTargets.ToArray());
                    return true;
                }
            }
            return false;
        }

        public void SetUpdateIKtargetBone(int boneIndex)
        {
            if (boneIndex < 0 || ikData == null)
                return;
            for (int target = 0; target < ikData.Count; target++)
            {
                var data = ikData[target];
                if (!data.enable || data.updateIKtarget)
                    continue;
                for (int i = 0; i < data.level; i++)
                {
                    if (data.joints[i].bone == null)
                        continue;
                    var t = data.joints[i].bone.transform;
                    while (t != null && vaw.gameObject.transform.parent != t)
                    {
                        if (va.bones[boneIndex] == t.gameObject)
                        {
                            SetUpdateIKtargetOriginalIK(target);
                            break;
                        }
                        t = t.parent;
                    }
                }
            }
            SetUpdateLinkedIKTarget(boneIndex);
        }
        public void SetUpdateIKtargetOriginalIK(int target)
        {
            if (target < 0 || ikData == null || target >= ikData.Count)
                return;

            ikData[target].updateIKtarget = true;

            SetUpdateLinkedIKTarget(va.EditBonesIndexOf(ikData[(int)target].root));
        }
        private void SetUpdateLinkedIKTarget(int boneIndex)
        {
            if (boneIndex < 0)
                return;
            foreach (var data in ikData)
            {
                if (data.updateIKtarget)
                    continue;
                if (data.spaceType == OriginalIKData.SpaceType.Parent &&
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
            if (boneIndex < 0 || ikData == null) return;
            for (int target = 0; target < ikData.Count; target++)
            {
                var data = ikData[target];
                if (!data.enable || data.synchroIKtarget) continue;
                for (int i = 0; i < data.level; i++)
                {
                    if (data.joints[i].bone == null) continue;
                    var t = data.joints[i].bone.transform;
                    while (t != null && vaw.gameObject.transform.parent != t)
                    {
                        if (va.bones[boneIndex] == t.gameObject)
                        {
                            SetSynchroIKtargetOriginalIK(target);
                            break;
                        }
                        t = t.parent;
                    }
                }
            }
        }
        public void SetSynchroIKtargetOriginalIK(int target)
        {
            if (target < 0 || ikData == null || target >= ikData.Count)
                return;

            if (!ikData[target].updateIKtarget)
                ikData[target].synchroIKtarget = true;
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
                if (data.isValid && data.synchroIKtarget)
                    return true;
            }
            return false;
        }

        public List<int> SelectionOriginalIKTargetsBoneIndexes()
        {
            List<int> list = new List<int>();
            if (ikTargetSelect != null && ikData != null)
            {
                foreach (var ikTarget in ikTargetSelect)
                {
                    for (int i = 0; i < ikData[ikTarget].level; i++)
                    {
                        var boneIndex = va.BonesIndexOf(ikData[ikTarget].joints[i].bone);
                        if (boneIndex < 0) continue;
                        list.Add(boneIndex);
                    }
                }
            }
            return list;
        }
    }
}
