using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace VeryAnimation
{
    [Serializable]
    public class BlendPoseTree
    {
        private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
        private VeryAnimation va { get { return VeryAnimation.instance; } }
        private VeryAnimationEditorWindow vae { get { return VeryAnimationEditorWindow.instance; } }

        [SerializeField]
        private PoseTemplate poseL;
        [SerializeField]
        private PoseTemplate poseR;

        private bool IsPoseReady { get { return poseL != null && poseR != null; } }

        private class PoseIndexTable
        {
            public int[] muscleIndexes;
            public int[] transformIndexes;
        }
        private PoseIndexTable poseIndexTableL;
        private PoseIndexTable poseIndexTableR;

        public enum EditMode
        {
            Tree,
            Selection,
            Total
        }
        private EditMode editMode;

        private GUIContent[] EditModeString = new GUIContent[(int)EditMode.Total];

        private class BaseNode
        {
            public string name;
            public bool foldout;
            public BaseNode[] children;
        }
        private BaseNode rootNode;

        #region Humanoid
        private class HumanoidNode : BaseNode
        {
            public HumanBodyBones[] humanoidIndexes;
            public string mirrorName;
        }
        private HumanoidNode humanoidNode;

        [SerializeField]
        private bool humanoidEnablePosition = true;
        [SerializeField]
        private bool humanoidEnableRotation = true;
        #endregion

        #region Generic
        private class GenericNode : BaseNode
        {
            public int boneIndex;
        }
        private GenericNode genericNode;

        [SerializeField]
        private bool genericEnablePosition = true;
        [SerializeField]
        private bool genericEnableRotation = true;
        [SerializeField]
        private bool genericEnableScale = true;
        #endregion

        #region BlendShape
        private class BlendShapeNode : BaseNode
        {
            public SkinnedMeshRenderer renderer;
            public string[] blendShapeNames;
        }
        private BlendShapeNode blendShapeNode;

        [SerializeField]
        private bool blendShapeEnable = true;
        #endregion

        #region Values
        private Dictionary<BaseNode, int> blendPoseTreeTable;
        [SerializeField]
        private float[] blendPoseValues;
        #endregion

        public BlendPoseTree()
        {
            UpdateEditModeString();
            Language.OnLanguageChanged += UpdateEditModeString;

            UpdateNode();
        }

        public void LoadEditorPref()
        {
            editMode = (EditMode)EditorPrefs.GetInt("VeryAnimation_Editor_BlendPose_EditMode", 0);
        }
        public void SaveEditorPref()
        {
            EditorPrefs.SetInt("VeryAnimation_Editor_BlendPose_EditMode", (int)editMode);
        }

        public void BlendPoseTreeToolbarGUI()
        {
            EditorGUI.BeginChangeCheck();
            var mode = (EditMode)GUILayout.Toolbar((int)editMode, EditModeString, EditorStyles.miniButton);
            if (EditorGUI.EndChangeCheck())
            {
                editMode = mode;
            }
        }
        public void BlendPoseTreeGUI()
        {
            const int FoldoutSpace = 17;
            const int FloatFieldWidth = 44;

            RowCount = 0;
            LabelWidth = Mathf.Min(VeryAnimationEditorWindow.instance.position.width / 2f, 400f);

            EditorGUILayout.BeginVertical(vaw.guiStyleSkinBox);
            {
                #region Top
                {
                    Action<int> SetPoseButton = (index) =>
                    {
                        if (GUILayout.Button(new GUIContent("", "Set current pose or load from template file"), vaw.guiStyleDropDown, GUILayout.Width(20f)))
                        {
                            Dictionary<string, PoseTemplate> poseTemplates = new Dictionary<string, PoseTemplate>();
                            {
                                var guids = AssetDatabase.FindAssets("t:posetemplate");
                                for (int i = 0; i < guids.Length; i++)
                                {
                                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                                    var poseTemplate = AssetDatabase.LoadAssetAtPath<PoseTemplate>(path);
                                    if (poseTemplate == null) continue;
                                    var name = path.Remove(0, "Assets/".Length);
                                    poseTemplates.Add(name, poseTemplate);
                                }
                            }

                            Action<PoseTemplate> MenuCallback = (poseTemplate) =>
                            {
                                Undo.RecordObject(vae, "Set Pose");
                                if (index == 0)
                                {
                                    poseL = poseTemplate;
                                }
                                else if (index == 1)
                                {
                                    poseR = poseTemplate;
                                }
                                UpdateNode();
                            };

                            GenericMenu menu = new GenericMenu();
                            {
                                menu.AddItem(new GUIContent("Current Pose"), false, () =>
                                {
                                    Undo.RecordObject(vae, "Set Pose");
                                    if (index == 0)
                                    {
                                        poseL = ScriptableObject.CreateInstance<PoseTemplate>();
                                        poseL.name = "Current";
                                        va.SavePoseTemplate(poseL);
                                    }
                                    else if (index == 1)
                                    {
                                        poseR = ScriptableObject.CreateInstance<PoseTemplate>();
                                        poseR.name = "Current";
                                        va.SavePoseTemplate(poseR);
                                    }
                                    UpdateNode();
                                });
                                menu.AddSeparator(string.Empty);
                            }
                            {
                                var enu = poseTemplates.GetEnumerator();
                                while (enu.MoveNext())
                                {
                                    var value = enu.Current.Value;
                                    menu.AddItem(new GUIContent(enu.Current.Key), false, () => { MenuCallback(value); });
                                }
                            }
                            menu.ShowAsContext();
                        }
                    };

                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.BeginHorizontal(vaw.guiStyleAnimationRowOddStyle);
                        EditorGUILayout.LabelField("L", EditorStyles.boldLabel, GUILayout.Width(14f));
                        {
                            EditorGUI.BeginChangeCheck();
                            var pose = EditorGUILayout.ObjectField(poseL, typeof(PoseTemplate), false, GUILayout.Width(LabelWidth / 3f)) as PoseTemplate;
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vae, "Set Pose");
                                poseL = pose;
                                UpdateNode();
                            }
                        }
                        SetPoseButton(0);
                        EditorGUILayout.EndHorizontal();
                    }
                    GUILayout.Space(FoldoutSpace);
                    EditorGUI.BeginDisabledGroup(poseL == null || poseR == null);
                    {
                        Action<float> ChangeValue = (value) =>
                        {
                            Undo.RecordObject(vae, "Change Slider");
                            if (editMode == EditMode.Tree)
                            {
                                SetChildrenValue(rootNode, value);
                            }
                            else if (editMode == EditMode.Selection)
                            {
                                blendPoseValues[blendPoseTreeTable[rootNode]] = value;
                                SetSelectionHumanoidValue(value);
                                SetSelectionGenericValue(value);
                                SetSelectionBlendShapeValue(value);
                            }
                        };
                        {
                            EditorGUI.BeginChangeCheck();
                            var value = GUILayout.HorizontalSlider(blendPoseValues[0], 0f, 1f);
                            if (EditorGUI.EndChangeCheck())
                            {
                                ChangeValue(value);
                            }
                        }
                        {
                            EditorGUI.BeginChangeCheck();
                            var value = EditorGUILayout.FloatField(blendPoseValues[0], GUILayout.Width(FloatFieldWidth));
                            if (EditorGUI.EndChangeCheck())
                            {
                                ChangeValue(value);
                            }
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                    GUILayout.Space(FoldoutSpace);
                    {
                        EditorGUILayout.BeginHorizontal(vaw.guiStyleAnimationRowOddStyle);
                        {
                            EditorGUI.BeginChangeCheck();
                            var pose = EditorGUILayout.ObjectField(poseR, typeof(PoseTemplate), false, GUILayout.Width(LabelWidth / 3f)) as PoseTemplate;
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vae, "Set Pose");
                                poseR = pose;
                                UpdateNode();
                            }
                        }
                        SetPoseButton(1);
                        EditorGUILayout.LabelField("R", EditorStyles.boldLabel, GUILayout.Width(14f));
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                #endregion

                GUILayout.Space(1);

                Func<int> MaxLevel = () =>
                {
                    int maxLevel = 0;
                    {
                        if (humanoidNode != null)
                            maxLevel = Math.Max(GetTreeLevel(humanoidNode, 0), maxLevel);
                        if (genericNode != null)
                            maxLevel = Math.Max(GetTreeLevel(genericNode, 0), maxLevel);
                        if (blendShapeNode != null)
                            maxLevel = Math.Max(GetTreeLevel(blendShapeNode, 0), maxLevel);
                    }
                    return maxLevel;
                };

                if (!IsPoseReady)
                {
                    EditorGUILayout.HelpBox(Language.GetText(Language.Help.BlendPoseNotPoseReady), MessageType.Info);
                }
                else if (editMode == EditMode.Tree)
                {
                    int maxLevel = MaxLevel();

                    #region Humanoid
                    if (humanoidNode != null)
                    {
                        HumanoidTreeNodeGUI(humanoidNode, 0, maxLevel);
                    }
                    #endregion

                    #region Generic
                    if (genericNode != null)
                    {
                        GenericTreeNodeGUI(genericNode, 0, maxLevel);
                    }
                    #endregion

                    #region BlendShape
                    if (blendShapeNode != null)
                    {
                        BlendShapeTreeNodeGUI(blendShapeNode, 0, maxLevel);
                    }
                    #endregion
                }
                else if (editMode == EditMode.Selection)
                {
                    #region Humanoid
                    if (humanoidNode != null)
                    {
                        EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                        {
                            {
                                var width = vaw.editorSettings.settingEditorNameFieldWidth - PRSWidth;
                                EditorGUILayout.LabelField(new GUIContent(humanoidNode.name, humanoidNode.name), GUILayout.Width(width));
                            }
                            EditorGUILayout.GetControlRect(false, GUILayout.Width(FoldoutWidth));
                            {
                                humanoidEnablePosition = EditorGUILayout.ToggleLeft(new GUIContent("P", "Position"), humanoidEnablePosition, GUILayout.Width(26f));
                                humanoidEnableRotation = EditorGUILayout.ToggleLeft(new GUIContent("R", "Rotation"), humanoidEnableRotation, GUILayout.Width(26f));
                                GUILayout.Space(30f);
                            }
                            {
                                Action<float> SetValue = (value) =>
                                {
                                    Undo.RecordObject(vae, "Change Humanoid");
                                    SetSelectionHumanoidValue(value);
                                };
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var value = GUILayout.HorizontalSlider(blendPoseValues[blendPoseTreeTable[humanoidNode]], 0f, 1f);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        SetValue(value);
                                    }
                                }
                                {
                                    var width = FloatFieldWidth;
                                    EditorGUI.BeginChangeCheck();
                                    var value = EditorGUILayout.FloatField(blendPoseValues[blendPoseTreeTable[humanoidNode]], GUILayout.Width(width));
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        SetValue(value);
                                    }
                                }
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    #endregion

                    #region Generic
                    if (genericNode != null)
                    {
                        EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                        {
                            {
                                var width = vaw.editorSettings.settingEditorNameFieldWidth - PRSWidth;
                                EditorGUILayout.LabelField(new GUIContent(genericNode.name, genericNode.name), GUILayout.Width(width));
                            }
                            EditorGUILayout.GetControlRect(false, GUILayout.Width(FoldoutWidth));
                            {
                                genericEnablePosition = EditorGUILayout.ToggleLeft(new GUIContent("P", "Position"), genericEnablePosition, GUILayout.Width(26f));
                                genericEnableRotation = EditorGUILayout.ToggleLeft(new GUIContent("R", "Rotation"), genericEnableRotation, GUILayout.Width(26f));
                                genericEnableScale = EditorGUILayout.ToggleLeft(new GUIContent("S", "Scale"), genericEnableScale, GUILayout.Width(26f));
                            }
                            {
                                Action<float> SetValue = (value) =>
                                {
                                    Undo.RecordObject(vae, "Change Generic");
                                    SetSelectionGenericValue(value);
                                };
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var value = GUILayout.HorizontalSlider(blendPoseValues[blendPoseTreeTable[genericNode]], 0f, 1f);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        SetValue(value);
                                    }
                                }
                                {
                                    var width = FloatFieldWidth;
                                    EditorGUI.BeginChangeCheck();
                                    var value = EditorGUILayout.FloatField(blendPoseValues[blendPoseTreeTable[genericNode]], GUILayout.Width(width));
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        SetValue(value);
                                    }
                                }
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    #endregion

                    #region BlendShape
                    if (blendShapeNode != null)
                    {
                        EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                        {
                            {
                                var width = vaw.editorSettings.settingEditorNameFieldWidth - PRSWidth;
                                EditorGUILayout.LabelField(new GUIContent(blendShapeNode.name, blendShapeNode.name), GUILayout.Width(width));
                            }
                            EditorGUILayout.GetControlRect(false, GUILayout.Width(FoldoutWidth));
                            {
                                blendShapeEnable = EditorGUILayout.ToggleLeft(new GUIContent("", "BlendShape"), blendShapeEnable, GUILayout.Width(26f));
                                GUILayout.Space(30f * 2);
                            }
                            {
                                Action<float> SetValue = (value) =>
                                {
                                    Undo.RecordObject(vae, "Change BlendShape");
                                    SetSelectionBlendShapeValue(value);
                                };
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var value = GUILayout.HorizontalSlider(blendPoseValues[blendPoseTreeTable[blendShapeNode]], 0f, 1f);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        SetValue(value);
                                    }
                                }
                                {
                                    var width = FloatFieldWidth;
                                    EditorGUI.BeginChangeCheck();
                                    var value = EditorGUILayout.FloatField(blendPoseValues[blendPoseTreeTable[blendShapeNode]], GUILayout.Width(width));
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        SetValue(value);
                                    }
                                }
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    #endregion
                }
            }
            EditorGUILayout.EndVertical();
        }
        #region BlendPoseTreeGUI
        private int RowCount = 0;
        private float LabelWidth = 0;
        private const int FoldoutWidth = 22;
        private const int FoldoutSpace = 17;
        private const int FloatFieldWidth = 44;
        private const int IndentWidth = 15;
        private const int PRSWidth = 73;
        private void HumanoidTreeNodeGUI(HumanoidNode mg, int level, int brotherMaxLevel)
        {
            var indentSpace = IndentWidth * level;
            var e = Event.current;
            brotherMaxLevel = Math.Max(GetTreeLevel(mg, 0), brotherMaxLevel);

            EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
            {
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.indentLevel = level;
                    var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(indentSpace + FoldoutWidth));
                    mg.foldout = EditorGUI.Foldout(rect, mg.foldout, "", true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (Event.current.alt)
                            SetChildrenFoldout(mg, mg.foldout);
                    }
                    EditorGUI.indentLevel = 0;
                }
                {
                    Action<HumanoidNode> SelectNodeAll = (node) =>
                    {
                        Action<HumanoidNode, HashSet<HumanBodyBones>> GetAllNodeChildren = null;
                        GetAllNodeChildren = (n, list) =>
                        {
                            if (n.humanoidIndexes != null && n.humanoidIndexes != null)
                                list.UnionWith(n.humanoidIndexes);
                            if (n.children != null && n.children.Length > 0)
                            {
                                foreach (var nc in n.children)
                                    GetAllNodeChildren(nc as HumanoidNode, list);
                            }
                        };
                        var humanoidIndexes = new HashSet<HumanBodyBones>();
                        GetAllNodeChildren(node, humanoidIndexes);

                        var bindings = new HashSet<EditorCurveBinding>();
                        foreach (var hi in humanoidIndexes)
                        {
                            if (hi < 0)
                            {
                                if (humanoidEnablePosition)
                                {
                                    foreach (var binding in va.AnimationCurveBindingAnimatorRootT)
                                        bindings.Add(binding);
                                }
                                if (humanoidEnableRotation)
                                {
                                    foreach (var binding in va.AnimationCurveBindingAnimatorRootQ)
                                        bindings.Add(binding);
                                }
                            }
                            else
                            {
                                if (humanoidEnablePosition && va.humanoidHasTDoF)
                                {
                                    if (VeryAnimation.HumanBonesAnimatorTDOFIndex[(int)hi] != null)
                                    {
                                        var tdofIndex = VeryAnimation.HumanBonesAnimatorTDOFIndex[(int)hi].index;
                                        if (tdofIndex >= 0)
                                        {
                                            for (int dof = 0; dof < 3; dof++)
                                            {
                                                bindings.Add(va.AnimationCurveBindingAnimatorTDOF(tdofIndex, dof));
                                            }
                                        }
                                    }
                                }
                                if (humanoidEnableRotation)
                                {
                                    for (int dof = 0; dof < 3; dof++)
                                    {
                                        var muscleIndex = HumanTrait.MuscleFromBone((int)hi, dof);
                                        if (muscleIndex < 0) continue;
                                        bindings.Add(va.AnimationCurveBindingAnimatorMuscle(muscleIndex));
                                    }
                                }
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
                                if (hi < 0)
                                    combineGoList.Add(vaw.gameObject);
                                else if (va.humanoidBones[(int)hi] != null)
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
                            va.SelectHumanoidBones(humanoidIndexes.ToArray());
                            va.SetAnimationWindowSynchroSelection(bindings.ToArray());
                        }
                    };
                    {
                        var width = vaw.editorSettings.settingEditorNameFieldWidth;
                        if (mg == humanoidNode)
                            width -= PRSWidth;
                        if (GUILayout.Button(new GUIContent(mg.name, mg.name), GUILayout.Width(width)))
                        {
                            SelectNodeAll(mg);
                        }
                    }
                    if (mg == humanoidNode)
                    {
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft(new GUIContent("P", "Position"), humanoidEnablePosition, GUILayout.Width(26f));
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vae, "Change Enable Flag");
                                humanoidEnablePosition = flag;
                            }
                        }
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft(new GUIContent("R", "Rotation"), humanoidEnableRotation, GUILayout.Width(26f));
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vae, "Change Enable Flag");
                                humanoidEnableRotation = flag;
                            }
                        }
                        GUILayout.Space(30f);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(mg.mirrorName))
                        {
                            if (GUILayout.Button(new GUIContent("", string.Format("Mirror: '{0}'", Path.GetFileName(mg.mirrorName))), vaw.guiStyleMirrorButton, GUILayout.Width(vaw.mirrorTex.width), GUILayout.Height(vaw.mirrorTex.height)))
                            {
                                SelectNodeAll(GetMirrorHumanoidNode(mg));
                            }
                        }
                        else
                        {
                            GUILayout.Space(FoldoutSpace);
                        }
                    }
                }
                {
                    Action<float> SetValue = (value) =>
                    {
                        Undo.RecordObject(vae, "Change Humanoid");
                        SetChildrenValue(mg, value);
                        if (va.mirrorEnable)
                        {
                            var mirrorNode = GetMirrorHumanoidNode(mg);
                            if (mirrorNode != null)
                                SetChildrenValue(mirrorNode, value);
                        }
                    };
                    {
                        EditorGUI.BeginChangeCheck();
                        var value = GUILayout.HorizontalSlider(blendPoseValues[blendPoseTreeTable[mg]], 0f, 1f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetValue(value);
                        }
                    }
                    {
                        var width = FloatFieldWidth + IndentWidth * brotherMaxLevel;
                        EditorGUI.BeginChangeCheck();
                        var value = EditorGUILayout.FloatField(blendPoseValues[blendPoseTreeTable[mg]], GUILayout.Width(width));
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetValue(value);
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            if (mg.foldout)
            {
                if (mg.humanoidIndexes != null && mg.humanoidIndexes.Length > 0)
                {
                    for (int index = 0; index < mg.humanoidIndexes.Length; index++)
                    {
                        var hi = mg.humanoidIndexes[index];
                        var name = hi >= 0 ? hi.ToString() : "Root";
                        var valueIndex = blendPoseTreeTable[mg] + 1 + index;

                        EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                        {
                            {
                                EditorGUILayout.GetControlRect(false, GUILayout.Width(indentSpace + FoldoutWidth));
                                GUILayout.Space(IndentWidth);
                            }

                            Action<HumanBodyBones> SelectHumanoidIndex = (humanoidIndex) =>
                            {
                                var bindings = new HashSet<EditorCurveBinding>();
                                if (humanoidIndex < 0)
                                {
                                    if (humanoidEnablePosition)
                                    {
                                        foreach (var binding in va.AnimationCurveBindingAnimatorRootT)
                                            bindings.Add(binding);
                                    }
                                    if (humanoidEnableRotation)
                                    {
                                        foreach (var binding in va.AnimationCurveBindingAnimatorRootQ)
                                            bindings.Add(binding);
                                    }
                                }
                                else
                                {
                                    if (humanoidEnableRotation)
                                    {
                                        for (int dof = 0; dof < 3; dof++)
                                        {
                                            var muscleIndex = HumanTrait.MuscleFromBone((int)humanoidIndex, dof);
                                            if (muscleIndex < 0) continue;
                                            bindings.Add(va.AnimationCurveBindingAnimatorMuscle(muscleIndex));
                                        }
                                    }
                                    if (humanoidEnablePosition)
                                    {
                                        if (VeryAnimation.HumanBonesAnimatorTDOFIndex[(int)humanoidIndex] != null)
                                        {
                                            var tdofIndex = VeryAnimation.HumanBonesAnimatorTDOFIndex[(int)humanoidIndex].index;
                                            if (tdofIndex >= 0)
                                            {
                                                for (int dof = 0; dof < 3; dof++)
                                                {
                                                    bindings.Add(va.AnimationCurveBindingAnimatorTDOF(tdofIndex, dof));
                                                }
                                            }
                                        }
                                    }
                                }
                                if (Shortcuts.IsKeyControl(e) || e.shift)
                                {
                                    var combineGoList = new HashSet<GameObject>(va.selectionGameObjects);
                                    var combineVirtualList = new HashSet<HumanBodyBones>();
                                    if (va.selectionHumanVirtualBones != null)
                                        combineVirtualList.UnionWith(va.selectionHumanVirtualBones);
                                    {
                                        if (humanoidIndex < 0)
                                            combineGoList.Add(vaw.gameObject);
                                        else if (va.humanoidBones[(int)humanoidIndex] != null)
                                            combineGoList.Add(va.humanoidBones[(int)humanoidIndex]);
                                        else if (VeryAnimation.HumanVirtualBones[(int)humanoidIndex] != null)
                                            combineVirtualList.Add(humanoidIndex);
                                    }
                                    va.SelectGameObjects(combineGoList.ToArray(), combineVirtualList.ToArray());
                                    bindings.UnionWith(va.uAw.GetCurveSelection());
                                    va.SetAnimationWindowSynchroSelection(bindings.ToArray());
                                }
                                else
                                {
                                    va.SelectHumanoidBones(new HumanBodyBones[] { humanoidIndex });
                                    va.SetAnimationWindowSynchroSelection(bindings.ToArray());
                                }
                            };
                            if (GUILayout.Button(new GUIContent(name, name), GUILayout.Width(vaw.editorSettings.settingEditorNameFieldWidth)))
                            {
                                SelectHumanoidIndex(hi);
                            }

                            var mirrorBone = hi >= 0 && va.humanoidIndex2boneIndex[(int)hi] >= 0 ? va.mirrorBoneIndexes[va.humanoidIndex2boneIndex[(int)hi]] : -1;
                            if (mirrorBone >= 0 && va.boneIndex2humanoidIndex[mirrorBone] >= 0)
                            {
                                if (GUILayout.Button(new GUIContent("", string.Format("Mirror: '{0}'", va.boneIndex2humanoidIndex[mirrorBone])), vaw.guiStyleMirrorButton, GUILayout.Width(vaw.mirrorTex.width), GUILayout.Height(vaw.mirrorTex.height)))
                                {
                                    SelectHumanoidIndex(va.boneIndex2humanoidIndex[mirrorBone]);
                                }
                            }
                            else
                            {
                                GUILayout.Space(FoldoutSpace);
                            }
                        }
                        {
                            Action<float> SetValue = (value) =>
                            {
                                Undo.RecordObject(vae, "Change Humanoid");
                                blendPoseValues[valueIndex] = value;
                                SetHumanoidValue(hi, value);
                                if (va.mirrorEnable)
                                {
                                    var mirrorNode = GetMirrorHumanoidNode(mg);
                                    if (mirrorNode != null)
                                    {
                                        var mhi = va.boneIndex2humanoidIndex[va.mirrorBoneIndexes[va.humanoidIndex2boneIndex[(int)hi]]];
                                        var mindex = ArrayUtility.IndexOf(mirrorNode.humanoidIndexes, mhi);
                                        var mvalueIndex = blendPoseTreeTable[mirrorNode] + 1 + mindex;
                                        blendPoseValues[mvalueIndex] = value;
                                        SetHumanoidValue(mhi, value);
                                    }
                                }
                            };
                            {
                                EditorGUI.BeginChangeCheck();
                                var value = GUILayout.HorizontalSlider(blendPoseValues[valueIndex], 0f, 1f);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    SetValue(value);
                                }
                            }
                            {
                                var width = FloatFieldWidth + IndentWidth * (brotherMaxLevel - 1);
                                EditorGUI.BeginChangeCheck();
                                var value = EditorGUILayout.FloatField(blendPoseValues[valueIndex], GUILayout.Width(width));
                                if (EditorGUI.EndChangeCheck())
                                {
                                    SetValue(value);
                                }
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                if (mg.children != null && mg.children.Length > 0)
                {
                    int maxLevel = 0;
                    foreach (var child in mg.children)
                    {
                        maxLevel = Math.Max(GetTreeLevel(child, 0), maxLevel);
                    }
                    foreach (var child in mg.children)
                    {
                        HumanoidTreeNodeGUI(child as HumanoidNode, level + 1, maxLevel);
                    }
                }
            }
        }
        private void GenericTreeNodeGUI(GenericNode mg, int level, int brotherMaxLevel)
        {
            var indentSpace = IndentWidth * level;
            var e = Event.current;
            brotherMaxLevel = Math.Max(GetTreeLevel(mg, 0), brotherMaxLevel);

            EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
            {
                {
                    var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(indentSpace + FoldoutWidth));
                    if (mg.children != null && mg.children.Length > 0)
                    {
                        EditorGUI.indentLevel = level;
                        EditorGUI.BeginChangeCheck();
                        mg.foldout = EditorGUI.Foldout(rect, mg.foldout, "", true);
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (Event.current.alt)
                                SetChildrenFoldout(mg, mg.foldout);
                        }
                        EditorGUI.indentLevel = 0;
                    }
                }
                {
                    Action<GenericNode> SelectNodeAll = (node) =>
                    {
                        Action<GenericNode, HashSet<int>> GetAllNodeChildren = null;
                        GetAllNodeChildren = (n, list) =>
                        {
                            if (n.boneIndex >= 0)
                                list.Add(n.boneIndex);
                            if (n.children != null && n.children.Length > 0)
                            {
                                foreach (var nc in n.children)
                                    GetAllNodeChildren(nc as GenericNode, list);
                            }
                        };
                        var boneIndexes = new HashSet<int>();
                        GetAllNodeChildren(node, boneIndexes);

                        var bindings = new HashSet<EditorCurveBinding>();
                        foreach (var boneIndex in boneIndexes)
                        {
                            if (genericEnablePosition)
                            {
                                for (int dof = 0; dof < 3; dof++)
                                {
                                    bindings.Add(va.AnimationCurveBindingTransformPosition(boneIndex, dof));
                                }
                            }
                            if (genericEnableRotation)
                            {
                                for (int dof = 0; dof < 3; dof++)
                                {
                                    bindings.Add(va.AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.Baked));
                                    bindings.Add(va.AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.NonBaked));
                                    bindings.Add(va.AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.RawEuler));
                                }
                                for (int dof = 0; dof < 4; dof++)
                                    bindings.Add(va.AnimationCurveBindingTransformRotation(boneIndex, dof, URotationCurveInterpolation.Mode.RawQuaternions));
                            }
                            if (genericEnableScale)
                            {
                                for (int dof = 0; dof < 3; dof++)
                                    bindings.Add(va.AnimationCurveBindingTransformScale(boneIndex, dof));
                            }
                        }

                        var combineGoList = new HashSet<GameObject>();
                        foreach (var boneIndex in boneIndexes)
                        {
                            if (va.bones[boneIndex] != null)
                                combineGoList.Add(va.bones[boneIndex]);
                        }
                        if (Shortcuts.IsKeyControl(e) || e.shift)
                        {
                            combineGoList.UnionWith(va.selectionGameObjects);
                            var combineVirtualList = new HashSet<HumanBodyBones>();
                            if (va.selectionHumanVirtualBones != null)
                                combineVirtualList.UnionWith(va.selectionHumanVirtualBones);
                            va.SelectGameObjects(combineGoList.ToArray(), combineVirtualList.ToArray());
                            bindings.UnionWith(va.uAw.GetCurveSelection());
                            va.SetAnimationWindowSynchroSelection(bindings.ToArray());
                        }
                        else
                        {
                            va.SelectGameObjects(combineGoList.ToArray());
                            va.SetAnimationWindowSynchroSelection(bindings.ToArray());
                        }
                    };
                    {
                        var width = vaw.editorSettings.settingEditorNameFieldWidth;
                        if (mg == genericNode)
                            width -= PRSWidth;
                        if (GUILayout.Button(new GUIContent(mg.name, mg.name), GUILayout.Width(width)))
                        {
                            SelectNodeAll(mg);
                        }
                    }
                    if (mg == genericNode)
                    {
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft(new GUIContent("P", "Position"), genericEnablePosition, GUILayout.Width(26f));
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vae, "Change Enable Flag");
                                genericEnablePosition = flag;
                            }
                        }
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft(new GUIContent("R", "Rotation"), genericEnableRotation, GUILayout.Width(26f));
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vae, "Change Enable Flag");
                                genericEnableRotation = flag;
                            }
                        }
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft(new GUIContent("S", "Scale"), genericEnableScale, GUILayout.Width(26f));
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vae, "Change Enable Flag");
                                genericEnableScale = flag;
                            }
                        }
                    }
                    else
                    {
                        if (va.mirrorBoneIndexes[mg.boneIndex] >= 0)
                        {
                            if (GUILayout.Button(new GUIContent("", string.Format("Mirror: '{0}'", va.bones[va.mirrorBoneIndexes[mg.boneIndex]].name)), vaw.guiStyleMirrorButton, GUILayout.Width(vaw.mirrorTex.width), GUILayout.Height(vaw.mirrorTex.height)))
                            {
                                SelectNodeAll(GetMirrorGenericNode(mg));
                            }
                        }
                        else
                        {
                            GUILayout.Space(FoldoutSpace);
                        }
                    }
                }
                {
                    Action<float> SetValue = (value) =>
                    {
                        Undo.RecordObject(vae, "Change Generic");
                        SetChildrenValue(mg, value);
                        if (va.mirrorEnable)
                        {
                            var mirrorNode = GetMirrorGenericNode(mg);
                            if (mirrorNode != null)
                                SetChildrenValue(mirrorNode, value);
                        }
                    };
                    {
                        EditorGUI.BeginChangeCheck();
                        var value = GUILayout.HorizontalSlider(blendPoseValues[blendPoseTreeTable[mg]], 0f, 1f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetValue(value);
                        }
                    }
                    {
                        var width = FloatFieldWidth + IndentWidth * brotherMaxLevel;
                        EditorGUI.BeginChangeCheck();
                        var value = EditorGUILayout.FloatField(blendPoseValues[blendPoseTreeTable[mg]], GUILayout.Width(width));
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetValue(value);
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
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
                        GenericTreeNodeGUI(child as GenericNode, level + 1, maxLevel);
                    }
                }
            }
        }
        private void BlendShapeTreeNodeGUI(BlendShapeNode mg, int level, int brotherMaxLevel)
        {
            var indentSpace = IndentWidth * level;
            var e = Event.current;
            brotherMaxLevel = Math.Max(GetTreeLevel(mg, 0), brotherMaxLevel);

            EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
            {
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.indentLevel = level;
                    var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(indentSpace + FoldoutWidth));
                    mg.foldout = EditorGUI.Foldout(rect, mg.foldout, "", true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (Event.current.alt)
                            SetChildrenFoldout(mg, mg.foldout);
                    }
                    EditorGUI.indentLevel = 0;
                }
                {
                    Action<BlendShapeNode> SelectNodeAll = (node) =>
                    {
                        var bindings = new HashSet<EditorCurveBinding>();
                        Action<BlendShapeNode, HashSet<int>> GetAllNodeChildren = null;
                        GetAllNodeChildren = (n, list) =>
                        {
                            if (n.renderer != null)
                            {
                                var boneIndex = va.BonesIndexOf(n.renderer.gameObject);
                                list.Add(boneIndex);
                                if (n.blendShapeNames != null && n.blendShapeNames.Length > 0)
                                {
                                    foreach (var name in n.blendShapeNames)
                                    {
                                        if (blendShapeEnable)
                                        {
                                            bindings.Add(va.AnimationCurveBindingBlendShape(n.renderer, name));
                                        }
                                    }
                                }
                            }
                            if (n.children != null && n.children.Length > 0)
                            {
                                foreach (var nc in n.children)
                                    GetAllNodeChildren(nc as BlendShapeNode, list);
                            }
                        };
                        var boneIndexes = new HashSet<int>();
                        GetAllNodeChildren(node, boneIndexes);

                        var combineGoList = new HashSet<GameObject>();
                        foreach (var boneIndex in boneIndexes)
                        {
                            if (va.bones[boneIndex] != null)
                                combineGoList.Add(va.bones[boneIndex]);
                        }
                        if (Shortcuts.IsKeyControl(e) || e.shift)
                        {
                            combineGoList.UnionWith(va.selectionGameObjects);
                            var combineVirtualList = new HashSet<HumanBodyBones>();
                            if (va.selectionHumanVirtualBones != null)
                                combineVirtualList.UnionWith(va.selectionHumanVirtualBones);
                            va.SelectGameObjects(combineGoList.ToArray(), combineVirtualList.ToArray());
                            bindings.UnionWith(va.uAw.GetCurveSelection());
                            va.SetAnimationWindowSynchroSelection(bindings.ToArray());
                        }
                        else
                        {
                            va.SelectGameObjects(combineGoList.ToArray());
                            va.SetAnimationWindowSynchroSelection(bindings.ToArray());
                        }
                    };
                    {
                        var width = vaw.editorSettings.settingEditorNameFieldWidth;
                        if (mg == blendShapeNode)
                            width -= PRSWidth;
                        if (GUILayout.Button(new GUIContent(mg.name, mg.name), GUILayout.Width(width)))
                        {
                            SelectNodeAll(mg);
                        }
                    }
                    if (mg == blendShapeNode)
                    {
                        {
                            EditorGUI.BeginChangeCheck();
                            var flag = EditorGUILayout.ToggleLeft(new GUIContent(" ", "BlendShape"), blendShapeEnable, GUILayout.Width(26f));
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vae, "Change Enable Flag");
                                blendShapeEnable = flag;
                            }
                        }
                        GUILayout.Space(30f * 2);
                    }
                    else
                    {
                        GUILayout.Space(FoldoutSpace);
                    }
                }
                {
                    Action<float> SetValue = (value) =>
                    {
                        Undo.RecordObject(vae, "Change BlendShape");
                        SetChildrenValue(mg, value);
                    };
                    {
                        EditorGUI.BeginChangeCheck();
                        var value = GUILayout.HorizontalSlider(blendPoseValues[blendPoseTreeTable[mg]], 0f, 1f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetValue(value);
                        }
                    }
                    {
                        var width = FloatFieldWidth + IndentWidth * brotherMaxLevel;
                        EditorGUI.BeginChangeCheck();
                        var value = EditorGUILayout.FloatField(blendPoseValues[blendPoseTreeTable[mg]], GUILayout.Width(width));
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetValue(value);
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            if (mg.foldout)
            {
                if (mg.blendShapeNames != null && mg.blendShapeNames.Length > 0)
                {
                    for (int index = 0; index < mg.blendShapeNames.Length; index++)
                    {
                        var valueIndex = blendPoseTreeTable[mg] + 1 + index;

                        EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                        {
                            {
                                EditorGUILayout.GetControlRect(false, GUILayout.Width(indentSpace + FoldoutWidth));
                                GUILayout.Space(IndentWidth);
                            }

                            Action<string> SelectBlendShapeName = (name) =>
                            {
                                var bindings = new HashSet<EditorCurveBinding>();
                                var boneIndexes = new List<int>();
                                if (mg.renderer != null)
                                {
                                    var boneIndex = va.BonesIndexOf(mg.renderer.gameObject);
                                    boneIndexes.Add(boneIndex);
                                    if (blendShapeEnable)
                                    {
                                        bindings.Add(va.AnimationCurveBindingBlendShape(mg.renderer, name));
                                    }
                                }

                                var combineGoList = new HashSet<GameObject>();
                                foreach (var boneIndex in boneIndexes)
                                {
                                    if (va.bones[boneIndex] != null)
                                        combineGoList.Add(va.bones[boneIndex]);
                                }
                                if (Shortcuts.IsKeyControl(e) || e.shift)
                                {
                                    combineGoList.UnionWith(va.selectionGameObjects);
                                    var combineVirtualList = new HashSet<HumanBodyBones>();
                                    if (va.selectionHumanVirtualBones != null)
                                        combineVirtualList.UnionWith(va.selectionHumanVirtualBones);
                                    va.SelectGameObjects(combineGoList.ToArray(), combineVirtualList.ToArray());
                                    bindings.UnionWith(va.uAw.GetCurveSelection());
                                    va.SetAnimationWindowSynchroSelection(bindings.ToArray());
                                }
                                else
                                {
                                    va.SelectGameObjects(combineGoList.ToArray());
                                    va.SetAnimationWindowSynchroSelection(bindings.ToArray());
                                }
                            };
                            if (GUILayout.Button(new GUIContent(mg.blendShapeNames[index], mg.blendShapeNames[index]), GUILayout.Width(vaw.editorSettings.settingEditorNameFieldWidth)))
                            {
                                SelectBlendShapeName(mg.blendShapeNames[index]);
                            }

                            var mirrorName = va.GetMirrorBlendShape(mg.renderer, mg.blendShapeNames[index]);
                            if (!string.IsNullOrEmpty(mirrorName))
                            {
                                if (GUILayout.Button(new GUIContent("", string.Format("Mirror: '{0}'", mirrorName)), vaw.guiStyleMirrorButton, GUILayout.Width(vaw.mirrorTex.width), GUILayout.Height(vaw.mirrorTex.height)))
                                {
                                    SelectBlendShapeName(mirrorName);
                                }
                            }
                            else
                            {
                                GUILayout.Space(FoldoutSpace);
                            }

                            Action<float> SetValue = (value) =>
                            {
                                Undo.RecordObject(vae, "Change BlendShape");
                                blendPoseValues[valueIndex] = value;
                                SetBlendShapeValue(mg.renderer, mg.blendShapeNames[index], value);
                                if (va.mirrorEnable && !string.IsNullOrEmpty(mirrorName))
                                {
                                    var mindex = ArrayUtility.IndexOf(mg.blendShapeNames, mirrorName);
                                    var mvalueIndex = blendPoseTreeTable[mg] + 1 + mindex;
                                    blendPoseValues[mvalueIndex] = value;
                                    SetBlendShapeValue(mg.renderer, mirrorName, value);
                                }
                            };
                            {
                                EditorGUI.BeginChangeCheck();
                                var value = GUILayout.HorizontalSlider(blendPoseValues[valueIndex], 0f, 1f);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    SetValue(value);
                                }
                            }
                            {
                                var width = FloatFieldWidth + IndentWidth * (brotherMaxLevel - 1);
                                EditorGUI.BeginChangeCheck();
                                var value = EditorGUILayout.FloatField(blendPoseValues[valueIndex], GUILayout.Width(width));
                                if (EditorGUI.EndChangeCheck())
                                {
                                    SetValue(value);
                                }
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                if (mg.children != null && mg.children.Length > 0)
                {
                    int maxLevel = 0;
                    foreach (var child in mg.children)
                    {
                        maxLevel = Math.Max(GetTreeLevel(child, 0), maxLevel);
                    }
                    foreach (var child in mg.children)
                    {
                        BlendShapeTreeNodeGUI(child as BlendShapeNode, level + 1, maxLevel);
                    }
                }
            }
        }
        private int GetTreeLevel(BaseNode node, int level)
        {
            if (node.foldout)
            {
                if (node.children != null && node.children.Length > 0)
                {
                    int tmp = level;
                    foreach (var child in node.children)
                    {
                        tmp = Math.Max(tmp, GetTreeLevel(child, level + 1));
                    }
                    level = tmp;
                }
                else
                {
                    if (node is HumanoidNode)
                    {
                        var hnode = node as HumanoidNode;
                        if (hnode.humanoidIndexes != null && hnode.humanoidIndexes.Length > 0)
                            level++;
                    }
                    else if (node is GenericNode)
                    {
                    }
                    else if (node is BlendShapeNode)
                    {
                        var bnode = node as BlendShapeNode;
                        if (bnode.blendShapeNames != null && bnode.blendShapeNames.Length > 0)
                            level++;
                    }
                }
            }
            return level;
        }
        private HumanoidNode GetMirrorHumanoidNode(HumanoidNode mg)
        {
            if (string.IsNullOrEmpty(mg.mirrorName))
                return null;
            var splits = mg.mirrorName.Split('/');
            BaseNode mirrorNode = humanoidNode;
            for (int i = 0; i < splits.Length; i++)
            {
                var index = ArrayUtility.FindIndex(mirrorNode.children, (node) => node.name == splits[i]);
                mirrorNode = mirrorNode.children[index];
            }
            Assert.IsTrue(mirrorNode.name == Path.GetFileName(mg.mirrorName));
            return mirrorNode as HumanoidNode;
        }
        private GenericNode GetMirrorGenericNode(GenericNode mg)
        {
            if (va.mirrorBoneIndexes[mg.boneIndex] < 0)
                return null;
            Func<GenericNode, int, GenericNode> FindNode = null;
            FindNode = (node, mBoneIndex) =>
            {
                if (node.boneIndex == mBoneIndex)
                    return node;
                if(node.children != null && node.children.Length > 0)
                {
                    foreach (var cn in node.children)
                    {
                        var result = FindNode(cn as GenericNode, mBoneIndex);
                        if (result != null)
                            return result;
                    }
                }
                return null;
            };
            return FindNode(genericNode, va.mirrorBoneIndexes[mg.boneIndex]);
        }
        private void SetChildrenFoldout(BaseNode node, bool foldout)
        {
            node.foldout = foldout;
            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    SetChildrenFoldout(child, foldout);
                }
            }
        }
        private void SetHumanoidValue(HumanBodyBones humanoidIndex, float value)
        {
            if (poseL.isHuman && poseR.isHuman)
            {
                if (humanoidIndex < 0)
                {
                    #region Root
                    if (humanoidEnablePosition && poseL.haveRootT && poseR.haveRootT)
                    {
                        var blendValue = Vector3.Lerp(poseL.rootT, poseR.rootT, value);
                        va.SetAnimationValueAnimatorRootTIfNotOriginal(blendValue);
                    }
                    if (humanoidEnableRotation && poseL.haveRootQ && poseR.haveRootQ)
                    {
                        var blendValue = Quaternion.Slerp(poseL.rootQ, poseR.rootQ, value);
                        va.SetAnimationValueAnimatorRootQIfNotOriginal(blendValue);
                    }
                    #endregion
                }
                else
                {
                    #region Bone
                    if (humanoidEnableRotation && poseIndexTableL.muscleIndexes != null && poseIndexTableR.muscleIndexes != null)
                    {
                        for (int dof = 0; dof < 3; dof++)
                        {
                            var muscleIndex = HumanTrait.MuscleFromBone((int)humanoidIndex, dof);
                            if (muscleIndex < 0) continue;
                            float blendValue = 0f;
                            {
                                var miL = poseIndexTableL.muscleIndexes[muscleIndex];
                                var miR = poseIndexTableR.muscleIndexes[muscleIndex];
                                if (miL < 0 || miR < 0) continue;
                                blendValue = Mathf.Lerp(poseL.muscleValues[miL], poseR.muscleValues[miR], value);
                            }
                            va.SetAnimationValueAnimatorMuscleIfNotOriginal(muscleIndex, blendValue);
                        }
                    }
                    if (humanoidEnablePosition && poseL.tdofIndices != null && poseR.tdofIndices != null)
                    {
                        if (VeryAnimation.HumanBonesAnimatorTDOFIndex[(int)humanoidIndex] != null)
                        {
                            var tdofIndex = VeryAnimation.HumanBonesAnimatorTDOFIndex[(int)humanoidIndex].index;
                            if (tdofIndex >= 0 && (int)tdofIndex < poseL.tdofValues.Length && (int)tdofIndex < poseR.tdofValues.Length)
                            {
                                var blendValue = Vector3.Lerp(poseL.tdofValues[(int)tdofIndex], poseR.tdofValues[(int)tdofIndex], value);
                                va.SetAnimationValueAnimatorTDOFIfNotOriginal(tdofIndex, blendValue);
                            }
                        }
                    }
                    #endregion
                }
            }
        }
        private void SetGenericValue(int boneIndex, float value)
        {
            if (boneIndex < 0) return;
            if (va.isHuman && va.humanoidConflict[boneIndex]) return;
            if (va.rootMotionBoneIndex >= 0 && boneIndex == 0) return;
            if (poseIndexTableL.transformIndexes == null || poseIndexTableR.transformIndexes == null) return;
            var miL = poseIndexTableL.transformIndexes[boneIndex];
            var miR = poseIndexTableR.transformIndexes[boneIndex];
            if (miL < 0 || miR < 0) return;

            #region Transform
            if (genericEnablePosition)
            {
                var blendValue = Vector3.Lerp(poseL.transformValues[miL].position, poseR.transformValues[miR].position, value);
                va.SetAnimationValueTransformPositionIfNotOriginal(boneIndex, blendValue);
            }
            if (genericEnableRotation)
            {
                var blendValue = Quaternion.Slerp(poseL.transformValues[miL].rotation, poseR.transformValues[miR].rotation, value);
                va.SetAnimationValueTransformRotationIfNotOriginal(boneIndex, blendValue);
            }
            if (genericEnableScale)
            {
                var blendValue = Vector3.Lerp(poseL.transformValues[miL].scale, poseR.transformValues[miR].scale, value);
                va.SetAnimationValueTransformScaleIfNotOriginal(boneIndex, blendValue);
            }
            #endregion
        }
        private void SetBlendShapeValue(SkinnedMeshRenderer renderer, string blendShapeName, float value)
        {
            if (blendShapeEnable && poseL.blendShapePaths != null && poseR.blendShapePaths != null)
            {
                var path = AnimationUtility.CalculateTransformPath(renderer.transform, va.editGameObject.transform);
                var indexL = EditorCommon.ArrayIndexOf(poseL.blendShapePaths, path);
                var indexR = EditorCommon.ArrayIndexOf(poseR.blendShapePaths, path);
                if (indexL >= 0 && indexR >= 0)
                {
                    var nameIndexL = EditorCommon.ArrayIndexOf(poseL.blendShapeValues[indexL].names, blendShapeName);
                    var nameIndexR = EditorCommon.ArrayIndexOf(poseR.blendShapeValues[indexR].names, blendShapeName);
                    if (nameIndexL >= 0 && nameIndexR >= 0)
                    {
                        var blendValue = Mathf.Lerp(poseL.blendShapeValues[indexL].weights[nameIndexL], poseR.blendShapeValues[indexR].weights[nameIndexR], value);
                        va.SetAnimationValueBlendShapeIfNotOriginal(renderer, blendShapeName, blendValue);
                    }
                }
            }
        }
        private void SetChildrenValue(BaseNode node, float value)
        {
            var valueIndex = blendPoseTreeTable[node];
            blendPoseValues[valueIndex] = value;
            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    SetChildrenValue(child, value);
                }
            }

            if (node is HumanoidNode)
            {
                var hnode = node as HumanoidNode;
                if (hnode.humanoidIndexes != null && hnode.humanoidIndexes.Length > 0)
                {
                    int rootIndex = -1;
                    for (int i = 0; i < hnode.humanoidIndexes.Length; i++)
                    {
                        blendPoseValues[valueIndex + 1 + i] = value;
                        if (hnode.humanoidIndexes[i] < 0)
                        {
                            rootIndex = i;
                        }
                        else
                        {
                            SetHumanoidValue(hnode.humanoidIndexes[i], value);
                        }
                    }
                    if (rootIndex >= 0)
                    {
                        SetHumanoidValue(hnode.humanoidIndexes[rootIndex], value);
                    }
                }
            }
            else if (node is GenericNode)
            {
                var gnode = node as GenericNode;
                if (gnode.boneIndex > 0)
                {
                    SetGenericValue(gnode.boneIndex, value);
                }
            }
            else if (node is BlendShapeNode)
            {
                var bnode = node as BlendShapeNode;
                if (bnode.blendShapeNames != null && bnode.blendShapeNames.Length > 0)
                {
                    for (int i = 0; i < bnode.blendShapeNames.Length; i++)
                    {
                        blendPoseValues[valueIndex + 1 + i] = value;
                        SetBlendShapeValue(bnode.renderer, bnode.blendShapeNames[i], value);
                    }
                }
            }
            va.SetPoseAfter();
        }
        private void SetSelectionHumanoidValue(float value)
        {
            if (humanoidNode == null) return;
            blendPoseValues[blendPoseTreeTable[humanoidNode]] = value;
            foreach (var humanoidIndex in va.SelectionGameObjectsHumanoidIndex())
            {
                SetHumanoidValue(humanoidIndex, value);
            }
            if (va.SelectionGameObjectsIndexOf(vaw.gameObject) >= 0)
            {
                SetHumanoidValue((HumanBodyBones)(-1), value);
            }
            va.SetPoseAfter();
        }
        private void SetSelectionGenericValue(float value)
        {
            if (genericNode == null) return;
            blendPoseValues[blendPoseTreeTable[genericNode]] = value;
            foreach (var boneIndex in va.selectionBones)
            {
                SetGenericValue(boneIndex, value);
            }
            va.SetPoseAfter();
        }
        private void SetSelectionBlendShapeValue(float value)
        {
            if (blendShapeNode == null) return;
            blendPoseValues[blendPoseTreeTable[blendShapeNode]] = value;
            foreach (var boneIndex in va.selectionBones)
            {
                var renderer = va.editBones[boneIndex].GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount <= 0) continue;
                foreach (var child in blendShapeNode.children)
                {
                    if (!(child is BlendShapeNode)) continue;
                    var bnode = child as BlendShapeNode;
                    if (bnode.renderer != renderer) continue;
                    foreach (var name in bnode.blendShapeNames)
                    {
                        SetBlendShapeValue(renderer, name, value);
                    }
                }
            }
            va.SetPoseAfter();
        }
        #endregion

        private void UpdateNode()
        {
            #region Humanoid
            humanoidNode = null;
            if (IsPoseReady && va.isHuman)
            {
                var children = new List<HumanoidNode>();
                Func<HumanBodyBones[], HumanBodyBones[]> GetContainsList = (src) =>
                {
                    Func<PoseTemplate, HumanBodyBones, bool> IsHumanoidIndexContains = (pose, hi) =>
                    {
                        if (pose.isHuman)
                        {
                            if (hi < 0)
                            {
                                if (pose.haveRootT || pose.haveRootQ)
                                    return true;
                            }
                            else
                            {
                                for (int dof = 0; dof < 3; dof++)
                                {
                                    var muscleIndex = HumanTrait.MuscleFromBone((int)hi, dof);
                                    if (muscleIndex < 0) continue;
                                    if (EditorCommon.ArrayContains(pose.musclePropertyNames, va.musclePropertyName.PropertyNames[muscleIndex]))
                                        return true;
                                }
                                if (VeryAnimation.HumanBonesAnimatorTDOFIndex[(int)hi] != null)
                                {
                                    var tdofIndex = VeryAnimation.HumanBonesAnimatorTDOFIndex[(int)hi].index;
                                    if (tdofIndex >= 0)
                                    {
                                        if (EditorCommon.ArrayContains(pose.tdofIndices, tdofIndex))
                                            return true;
                                    }
                                }
                            }
                        }
                        return false;
                    };

                    var dst = new List<HumanBodyBones>();
                    foreach (var hi in src)
                    {
                        if (hi >= 0 && va.humanoidBones[(int)hi] == null && VeryAnimation.HumanVirtualBones[(int)hi] == null)
                            continue;
                        if (!IsHumanoidIndexContains(poseL, hi) || !IsHumanoidIndexContains(poseR, hi))
                            continue;
                        dst.Add(hi);
                    }
                    return dst.ToArray();
                };

                #region Head
                {
                    var node = new HumanoidNode()
                    {
                        name = "Head",
                        humanoidIndexes = GetContainsList(new HumanBodyBones[]
                        {
                            HumanBodyBones.LeftEye,
                            HumanBodyBones.RightEye,
                            HumanBodyBones.Jaw,
                            HumanBodyBones.Head,
                            HumanBodyBones.Neck,
                        }),
                    };
                    if (node.humanoidIndexes.Length > 0)
                        children.Add(node);
                }
                #endregion
                #region Body
                {
                    var node = new HumanoidNode()
                    {
                        name = "Body",
                        humanoidIndexes = GetContainsList(new HumanBodyBones[]
                        {
                            HumanBodyBones.UpperChest,
                            HumanBodyBones.Chest,
                            HumanBodyBones.Spine,
                        }),
                    };
                    if (node.humanoidIndexes.Length > 0)
                        children.Add(node);
                }
                #endregion
                #region Left Arm
                {
                    var node = new HumanoidNode()
                    {
                        name = "Left Arm",
                        mirrorName = "Right Arm",
                        humanoidIndexes = GetContainsList(new HumanBodyBones[]
                        {
                            HumanBodyBones.LeftShoulder,
                            HumanBodyBones.LeftUpperArm,
                            HumanBodyBones.LeftLowerArm,
                            HumanBodyBones.LeftHand,
                        }),
                    };
                    if (node.humanoidIndexes.Length > 0)
                        children.Add(node);
                }
                #endregion
                #region Right Arm
                {
                    var node = new HumanoidNode()
                    {
                        name = "Right Arm",
                        mirrorName = "Left Arm",
                        humanoidIndexes = GetContainsList(new HumanBodyBones[]
                        {
                            HumanBodyBones.RightShoulder,
                            HumanBodyBones.RightUpperArm,
                            HumanBodyBones.RightLowerArm,
                            HumanBodyBones.RightHand,
                        }),
                    };
                    if (node.humanoidIndexes.Length > 0)
                        children.Add(node);
                }
                #endregion
                #region Left Leg
                {
                    var node = new HumanoidNode()
                    {
                        name = "Left Leg",
                        mirrorName = "Right Leg",
                        humanoidIndexes = GetContainsList(new HumanBodyBones[]
                        {
                            HumanBodyBones.LeftUpperLeg,
                            HumanBodyBones.LeftLowerLeg,
                            HumanBodyBones.LeftFoot,
                            HumanBodyBones.LeftToes,
                        }),
                    };
                    if (node.humanoidIndexes.Length > 0)
                        children.Add(node);
                }
                #endregion
                #region Right Leg
                {
                    var node = new HumanoidNode()
                    {
                        name = "Right Leg",
                        mirrorName = "Left Leg",
                        humanoidIndexes = GetContainsList(new HumanBodyBones[]
                        {
                            HumanBodyBones.RightUpperLeg,
                            HumanBodyBones.RightLowerLeg,
                            HumanBodyBones.RightFoot,
                            HumanBodyBones.RightToes,
                        }),
                    };
                    if (node.humanoidIndexes.Length > 0)
                        children.Add(node);
                }
                #endregion
                #region Left Finger
                if (va.humanoidHasLeftHand)
                {
                    var node = new HumanoidNode()
                    {
                        name = "Left Finger",
                        mirrorName = "Right Finger",
                        children = new HumanoidNode[]
                        {
                            new HumanoidNode()
                            {
                                name = "Left Thumb",
                                mirrorName = "Right Finger/Right Thumb",
                                humanoidIndexes = GetContainsList(new HumanBodyBones[]
                                {
                                    HumanBodyBones.LeftThumbProximal,
                                    HumanBodyBones.LeftThumbIntermediate,
                                    HumanBodyBones.LeftThumbDistal,
                                }),
                            },
                            new HumanoidNode()
                            {
                                name = "Left Index",
                                mirrorName = "Right Finger/Right Index",
                                humanoidIndexes = GetContainsList(new HumanBodyBones[]
                                {
                                    HumanBodyBones.LeftIndexProximal,
                                    HumanBodyBones.LeftIndexIntermediate,
                                    HumanBodyBones.LeftIndexDistal,
                                }),
                            },
                            new HumanoidNode()
                            {
                                name = "Left Middle",
                                mirrorName = "Right Finger/Right Middle",
                                humanoidIndexes = GetContainsList(new HumanBodyBones[]
                                {
                                    HumanBodyBones.LeftMiddleProximal,
                                    HumanBodyBones.LeftMiddleIntermediate,
                                    HumanBodyBones.LeftMiddleDistal,
                                }),
                            },
                            new HumanoidNode()
                            {
                                name = "Left Ring",
                                mirrorName = "Right Finger/Right Ring",
                                humanoidIndexes = GetContainsList(new HumanBodyBones[]
                                {
                                    HumanBodyBones.LeftRingProximal,
                                    HumanBodyBones.LeftRingIntermediate,
                                    HumanBodyBones.LeftRingDistal,
                                }),
                            },
                            new HumanoidNode()
                            {
                                name = "Left Little",
                                mirrorName = "Right Finger/Right Little",
                                humanoidIndexes = GetContainsList(new HumanBodyBones[]
                                {
                                    HumanBodyBones.LeftLittleProximal,
                                    HumanBodyBones.LeftLittleIntermediate,
                                    HumanBodyBones.LeftLittleDistal,
                                }),
                            },
                        },
                    };
                    children.Add(node);
                }
                #endregion
                #region Right Finger
                if (va.humanoidHasRightHand)
                {
                    var node = new HumanoidNode()
                    {
                        name = "Right Finger",
                        mirrorName = "Left Finger",
                        children = new HumanoidNode[]
                        {
                            new HumanoidNode()
                            {
                                name = "Right Thumb",
                                mirrorName = "Left Finger/Left Thumb",
                                humanoidIndexes = GetContainsList(new HumanBodyBones[]
                                {
                                    HumanBodyBones.RightThumbProximal,
                                    HumanBodyBones.RightThumbIntermediate,
                                    HumanBodyBones.RightThumbDistal,
                                }),
                            },
                            new HumanoidNode()
                            {
                                name = "Right Index",
                                mirrorName = "Left Finger/Left Index",
                                humanoidIndexes = GetContainsList(new HumanBodyBones[]
                                {
                                    HumanBodyBones.RightIndexProximal,
                                    HumanBodyBones.RightIndexIntermediate,
                                    HumanBodyBones.RightIndexDistal,
                                }),
                            },
                            new HumanoidNode()
                            {
                                name = "Right Middle",
                                mirrorName = "Left Finger/Left Middle",
                                humanoidIndexes = GetContainsList(new HumanBodyBones[]
                                {
                                    HumanBodyBones.RightMiddleProximal,
                                    HumanBodyBones.RightMiddleIntermediate,
                                    HumanBodyBones.RightMiddleDistal,
                                }),
                            },
                            new HumanoidNode()
                            {
                                name = "Right Ring",
                                mirrorName = "Left Finger/Left Ring",
                                humanoidIndexes = GetContainsList(new HumanBodyBones[]
                                {
                                    HumanBodyBones.RightRingProximal,
                                    HumanBodyBones.RightRingIntermediate,
                                    HumanBodyBones.RightRingDistal,
                                }),
                            },
                            new HumanoidNode()
                            {
                                name = "Right Little",
                                mirrorName = "Left Finger/Left Little",
                                humanoidIndexes = GetContainsList(new HumanBodyBones[]
                                {
                                    HumanBodyBones.RightLittleProximal,
                                    HumanBodyBones.RightLittleIntermediate,
                                    HumanBodyBones.RightLittleDistal,
                                }),
                            },
                        },
                    };
                    children.Add(node);
                }
                #endregion

                humanoidNode = new HumanoidNode()
                {
                    name = "Humanoid",
                    children = children.ToArray(),
                    humanoidIndexes = GetContainsList(new HumanBodyBones[]
                    {
                        (HumanBodyBones)(-1),
                    }),
                };
            }
            #endregion

            #region Generic
            genericNode = null;
            if (IsPoseReady)
            {
                Func<Transform, GenericNode> AddBone = null;
                AddBone = (t) =>
                {
                    var boneIndex = va.BonesIndexOf(t.gameObject);
                    if (boneIndex < 0)
                        return null;
                    if (!EditorCommon.ArrayContains(poseL.transformPaths, va.bonePaths[boneIndex]))
                        return null;
                    if (!EditorCommon.ArrayContains(poseR.transformPaths, va.bonePaths[boneIndex]))
                        return null;

                    var children = new List<GenericNode>();
                    for (int i = 0; i < t.childCount; i++)
                    {
                        var child = AddBone(t.GetChild(i));
                        if (child != null)
                            children.Add(child);
                    }
                    var node = new GenericNode()
                    {
                        name = t.name,
                        children = children.ToArray(),
                        boneIndex = boneIndex,
                    };
                    return node;
                };

                {
                    var children = new List<GenericNode>();
                    for (int i = 0; i < vaw.gameObject.transform.childCount; i++)
                    {
                        var child = AddBone(vaw.gameObject.transform.GetChild(i));
                        if (child != null)
                            children.Add(child);
                    }
                    if (children.Count > 0)
                    {
                        genericNode = new GenericNode()
                        {
                            name = "Generic",
                            children = children.ToArray(),
                            boneIndex = va.BonesIndexOf(vaw.gameObject),
                        };
                    }
                }
            }
            #endregion

            #region BlendShape
            blendShapeNode = null;
            if (IsPoseReady)
            {
                var children = new List<BlendShapeNode>();
                if (poseL.blendShapePaths != null && poseR.blendShapePaths != null)
                {
                    foreach (var renderer in vaw.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount <= 0) continue;
                        var path = AnimationUtility.CalculateTransformPath(renderer.transform, va.editGameObject.transform);
                        var indexL = EditorCommon.ArrayIndexOf(poseL.blendShapePaths, path);
                        var indexR = EditorCommon.ArrayIndexOf(poseR.blendShapePaths, path);
                        if (indexL < 0 || indexR < 0) continue;
                        var names = new List<string>();
                        for (int i = 0; i < poseL.blendShapeValues[indexL].names.Length; i++)
                        {
                            if (!EditorCommon.ArrayContains(poseR.blendShapeValues[indexR].names, poseL.blendShapeValues[indexL].names[i]))
                                continue;
                            if (!va.blendShapeWeightSave.IsHaveOriginalWeight(renderer, poseL.blendShapeValues[indexL].names[i]))
                                continue;
                            names.Add(poseL.blendShapeValues[indexL].names[i]);
                        }
                        if (names.Count > 0)
                        {
                            children.Add(new BlendShapeNode()
                            {
                                name = renderer.name,
                                renderer = renderer,
                                blendShapeNames = names.ToArray(),
                            });
                        }
                    }
                }
                if (children.Count > 0)
                {
                    blendShapeNode = new BlendShapeNode()
                    {
                        name = "Blend Shape",
                        children = children.ToArray(),
                    };
                }
            }
            #endregion

            #region Root
            {
                var rootChildren = new List<BaseNode>();
                if (humanoidNode != null) rootChildren.Add(humanoidNode);
                if (genericNode != null) rootChildren.Add(genericNode);
                if (blendShapeNode != null) rootChildren.Add(blendShapeNode);
                rootNode = new BaseNode()
                {
                    name = "Root",
                    children = rootChildren.ToArray(),
                };
            }
            #endregion

            #region Values
            {
                blendPoseTreeTable = new Dictionary<BaseNode, int>();
                int counter = 0;
                Action<BaseNode> AddTable = null;
                AddTable = (node) =>
                {
                    blendPoseTreeTable.Add(node, counter++);
                    if (node is HumanoidNode)
                    {
                        var hnode = node as HumanoidNode;
                        if (hnode.humanoidIndexes != null)
                        {
                            counter += hnode.humanoidIndexes.Length;
                        }
                    }
                    else if (node is GenericNode)
                    {
                    }
                    else if (node is BlendShapeNode)
                    {
                        var bnode = node as BlendShapeNode;
                        if (bnode.blendShapeNames != null)
                        {
                            counter += bnode.blendShapeNames.Length;
                        }
                    }
                    if (node.children != null)
                    {
                        foreach (var child in node.children)
                        {
                            AddTable(child);
                        }
                    }
                };
                AddTable(rootNode);

                blendPoseValues = new float[counter];
            }
            #endregion

            #region PoseIndexTable
            poseIndexTableL = poseIndexTableR = null;
            if (IsPoseReady)
            {
                Func<PoseTemplate, PoseIndexTable> CreatePoseIndexTable = (pose) =>
                {
                    var indexTable = new PoseIndexTable();

                    if (pose.musclePropertyNames != null && pose.muscleValues!= null)
                    {
                        indexTable.muscleIndexes = new int[va.musclePropertyName.PropertyNames.Length];
                        for (int i = 0; i < va.musclePropertyName.PropertyNames.Length; i++)
                            indexTable.muscleIndexes[i] = EditorCommon.ArrayIndexOf(pose.musclePropertyNames, va.musclePropertyName.PropertyNames[i]);
                    }
                    if (pose.transformPaths != null && pose.transformValues != null)
                    {
                        indexTable.transformIndexes = new int[va.bonePaths.Length];
                        for (int i = 0; i < va.bonePaths.Length; i++)
                            indexTable.transformIndexes[i] = EditorCommon.ArrayIndexOf(pose.transformPaths, va.bonePaths[i]);
                    }
                    return indexTable;
                };
                poseIndexTableL = CreatePoseIndexTable(poseL);
                poseIndexTableR = CreatePoseIndexTable(poseR);
            }
            #endregion
        }

        private void UpdateEditModeString()
        {
            for (int i = 0; i < (int)EditMode.Total; i++)
            {
                EditModeString[i] = new GUIContent(Language.GetContent(Language.Help.BlendPoseModeTree + i));
            }
        }
    }
}
