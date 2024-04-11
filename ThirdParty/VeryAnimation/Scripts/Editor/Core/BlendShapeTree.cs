using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace VeryAnimation
{
    [Serializable]
    public class BlendShapeTree
    {
        private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
        private VeryAnimation va { get { return VeryAnimation.instance; } }
        private VeryAnimationEditorWindow vae { get { return VeryAnimationEditorWindow.instance; } }

        private enum BlendShapeMode
        {
            Slider,
            List,
            Icon,
            Total,
        }
        private static readonly string[] BlendShapeModeString =
        {
            BlendShapeMode.Slider.ToString(),
            BlendShapeMode.List.ToString(),
            BlendShapeMode.Icon.ToString(),
        };
        private BlendShapeMode blendShapeMode;

        #region Tree
        [System.Diagnostics.DebuggerDisplay("{blendShapeName}")]
        private class BlendShapeInfo
        {
            public string blendShapeName;
        }
        private class BlendShapeNode
        {
            public string name;
            public bool foldout;
            public BlendShapeInfo[] infoList;
        }
        private class BlendShapeRootNode : BlendShapeNode
        {
            public SkinnedMeshRenderer renderer;
            public Mesh mesh;
            public string[] blendShapeNames;
        }
        private List<BlendShapeRootNode> blendShapeNodes;
        private Dictionary<BlendShapeNode, int> blendShapeGroupTreeTable;

        [SerializeField]
        private float[] blendShapeGroupValues;

        private bool blendShapeMirrorName;
        #endregion

        #region List
        private ReorderableList blendShapeSetListReorderableList;
        #endregion

        #region Icon
        private const int IconTextureSize = 256;
        private bool iconUpdate;
        private bool iconShowName;
        private float iconSize;

        private enum IconCameraMode
        {
            forward,
            back,
            up,
            down,
            right,
            left,
        }
        private IconCameraMode iconCameraMode;
        #endregion

        #region GUIStyle
        private GUIStyle guiStyleIconButton;
        private GUIStyle guiStyleNameLabelCenter;
        private GUIStyle guiStyleNameLabelRight;

        private void GUIStyleReady()
        {
            #region GUIStyle
            if (guiStyleIconButton == null)
                guiStyleIconButton = new GUIStyle(GUI.skin.button);
            guiStyleIconButton.margin = new RectOffset(0, 0, 0, 0);
            guiStyleIconButton.overflow = new RectOffset(0, 0, 0, 0);
            guiStyleIconButton.padding = new RectOffset(0, 0, 0, 0);
            if (guiStyleNameLabelCenter == null)
                guiStyleNameLabelCenter = new GUIStyle(EditorStyles.whiteLargeLabel);
            guiStyleNameLabelCenter.alignment = TextAnchor.LowerCenter;
            if (guiStyleNameLabelRight == null)
                guiStyleNameLabelRight = new GUIStyle(EditorStyles.whiteLargeLabel);
            guiStyleNameLabelRight.alignment = TextAnchor.LowerRight;
            #endregion
        }
        #endregion

        public BlendShapeTree()
        {
            if (vaw == null || vaw.gameObject == null)
                return;
            
            #region BlendShapeNode
            {
                blendShapeNodes = new List<BlendShapeRootNode>();
                foreach (var renderer in vaw.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount <= 0)
                        continue;
                    BlendShapeRootNode root = new BlendShapeRootNode()
                    {
                        renderer = renderer,
                        mesh = renderer.sharedMesh,
                        name = renderer.gameObject.name,
                        infoList = new BlendShapeInfo[renderer.sharedMesh.blendShapeCount],
                    };
                    root.blendShapeNames = new string[renderer.sharedMesh.blendShapeCount + 1];
                    root.blendShapeNames[0] = "[none]";
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    {
                        root.infoList[i] = new BlendShapeInfo()
                        {
                            blendShapeName = renderer.sharedMesh.GetBlendShapeName(i),
                        };
                        root.blendShapeNames[i + 1] = renderer.sharedMesh.GetBlendShapeName(i);
                    }
                    blendShapeNodes.Add(root);
                }

                {
                    blendShapeGroupTreeTable = new Dictionary<BlendShapeNode, int>();
                    int counter = 0;
                    Action<BlendShapeNode> AddTable = null;
                    AddTable = (mg) =>
                    {
                        blendShapeGroupTreeTable.Add(mg, counter++);
                    };
                    foreach (var node in blendShapeNodes)
                    {
                        AddTable(node);
                    }

                    blendShapeGroupValues = new float[blendShapeGroupTreeTable.Count];
                }
            }
            #endregion

            iconUpdate = true;
        }
        
        public void LoadEditorPref()
        {
            blendShapeMode = (BlendShapeMode)EditorPrefs.GetInt("VeryAnimation_BlendShapeMode", 0);
            blendShapeMirrorName = EditorPrefs.GetBool("VeryAnimation_Control_BlendShapeMirrorName", false);
            iconShowName = EditorPrefs.GetBool("VeryAnimation_Control_BlendShapeSetIconShowName", true);
            iconSize = EditorPrefs.GetFloat("VeryAnimation_Control_BlendShapeSetIconSize", 100f);
            iconCameraMode = (IconCameraMode)EditorPrefs.GetInt("VeryAnimation_BlendShapeSetIconCameraMode", 0);
        }
        public void SaveEditorPref()
        {
            EditorPrefs.SetInt("VeryAnimation_BlendShapeMode", (int)blendShapeMode);
            EditorPrefs.SetBool("VeryAnimation_Control_BlendShapeMirrorName", blendShapeMirrorName);
            EditorPrefs.SetBool("VeryAnimation_Control_BlendShapeSetIconShowName", iconShowName);
            EditorPrefs.SetFloat("VeryAnimation_Control_BlendShapeSetIconSize", iconSize);
            EditorPrefs.SetInt("VeryAnimation_BlendShapeSetIconCameraMode", (int)iconCameraMode);
        }

        public void BlendShapeTreeToolbarGUI()
        {
            EditorGUI.BeginChangeCheck();
            var m = (BlendShapeMode)GUILayout.Toolbar((int)blendShapeMode, BlendShapeModeString, EditorStyles.miniButton);
            if (EditorGUI.EndChangeCheck())
            {
                blendShapeMode = m;
            }
        }

        public void BlendShapeTreeSettingsMesh()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(Language.GetContent(Language.Help.BlendShapeMirrorName), blendShapeMirrorName, () =>
            {
                blendShapeMirrorName = !blendShapeMirrorName;
            });
            menu.AddItem(Language.GetContent(Language.Help.BlendShapeMirrorAutomap), false, () =>
            {
                va.BlendShapeMirrorAutomap();
                InternalEditorUtility.RepaintAllViews();
            });
            menu.AddItem(Language.GetContent(Language.Help.BlendShapeMirrorClear), false, () =>
            {
                va.BlendShapeMirrorInitialize();
                InternalEditorUtility.RepaintAllViews();
            });
            menu.ShowAsContext();
        }

        public bool IsHaveBlendShapeNodes()
        {
            return blendShapeNodes != null  && blendShapeNodes.Count > 0;
        }

        public void BlendShapeTreeGUI()
        {
            var e = Event.current;

            GUIStyleReady();

            EditorGUILayout.BeginVertical(vaw.guiStyleSkinBox);
            if (blendShapeMode == BlendShapeMode.Slider)
            {
                #region Slider
                const int IndentWidth = 15;

                #region SetBlendShapeFoldout
                Action<BlendShapeNode, bool> SetBlendShapeFoldout = null;
                SetBlendShapeFoldout = (mg, foldout) =>
                {
                    mg.foldout = foldout;
                };
                #endregion

                var mgRoot = blendShapeNodes;

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
                            var combineBindings = new HashSet<EditorCurveBinding>(va.uAw.GetCurveSelection());
                            foreach (var root in mgRoot)
                            {
                                if (root.renderer != null && root.renderer.gameObject != null)
                                    combineGoList.Add(root.renderer.gameObject);
                                if (root.infoList != null && root.infoList.Length > 0)
                                {
                                    foreach (var info in root.infoList)
                                        combineBindings.Add(va.AnimationCurveBindingBlendShape(root.renderer, info.blendShapeName));
                                }
                            }
                            va.SelectGameObjects(combineGoList.ToArray(), combineVirtualList.ToArray());
                            va.SetAnimationWindowSynchroSelection(combineBindings.ToArray());
                        }
                        else
                        {
                            var combineGoList = new List<GameObject>();
                            var combineBindings = new List<EditorCurveBinding>();
                            foreach (var root in mgRoot)
                            {
                                if (root.renderer != null && root.renderer.gameObject != null)
                                    combineGoList.Add(root.renderer.gameObject);
                                if (root.infoList != null && root.infoList.Length > 0)
                                {
                                    foreach (var info in root.infoList)
                                        combineBindings.Add(va.AnimationCurveBindingBlendShape(root.renderer, info.blendShapeName));
                                }
                            }
                            va.SelectGameObjects(combineGoList.ToArray());
                            va.SetAnimationWindowSynchroSelection(combineBindings.ToArray());
                        }
                    }
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Reset All", vaw.guiStyleDropDown, GUILayout.Width(100)))
                    {
                        GenericMenu menu = new GenericMenu();
                        {
                            if (va.blendShapeWeightSave.IsEnablePrefabWeight())
                            {
                                menu.AddItem(Language.GetContent(Language.Help.EditorPosePrefab), false, () =>
                                {
                                    Undo.RecordObject(vae, "Prefab Pose");
                                    for (int i = 0; i < blendShapeGroupValues.Length; i++)
                                        blendShapeGroupValues[i] = 0f;
                                    foreach (var root in mgRoot)
                                    {
                                        if (root.infoList != null && root.infoList.Length > 0)
                                        {
                                            foreach (var info in root.infoList)
                                                va.SetAnimationValueBlendShapeIfNotOriginal(root.renderer, info.blendShapeName, va.blendShapeWeightSave.GetPrefabWeight(root.renderer, info.blendShapeName));
                                        }
                                    }
                                });
                            }
                            {
                                menu.AddItem(Language.GetContent(Language.Help.EditorPoseStart), false, () =>
                                {
                                    Undo.RecordObject(vae, "Edit Start Pose");
                                    for (int i = 0; i < blendShapeGroupValues.Length; i++)
                                        blendShapeGroupValues[i] = 0f;
                                    foreach (var root in mgRoot)
                                    {
                                        if (root.infoList != null && root.infoList.Length > 0)
                                        {
                                            foreach (var info in root.infoList)
                                                va.SetAnimationValueBlendShapeIfNotOriginal(root.renderer, info.blendShapeName, va.blendShapeWeightSave.GetOriginalWeight(root.renderer, info.blendShapeName));
                                        }
                                    }
                                });
                            }
                        }
                        menu.ShowAsContext();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                #endregion

                EditorGUILayout.Space();

                #region BlendShape
                BlendShapeRootNode rootNode = null;
                int RowCount = 0;
                Action<BlendShapeNode, int, int> BlendShapeTreeNodeGUI = null;
                BlendShapeTreeNodeGUI = (mg, level, brotherMaxLevel) =>
                {
                    const int FoldoutWidth = 22;
                    const int FoldoutSpace = 17;
                    const int FloatFieldWidth = 44;
                    var indentSpace = IndentWidth * level;
                    EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                    {
                        {
                            var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(FoldoutWidth));
                            EditorGUI.BeginChangeCheck();
                            mg.foldout = EditorGUI.Foldout(rect, mg.foldout, "", true);
                            if (EditorGUI.EndChangeCheck())
                            {
                                if (e.alt)
                                    SetBlendShapeFoldout(mg, mg.foldout);
                            }
                        }
                        if (GUILayout.Button(new GUIContent(mg.name, mg.name), GUILayout.Width(vaw.editorSettings.settingEditorNameFieldWidth)))
                        {
                            if (Shortcuts.IsKeyControl(e) || e.shift)
                            {
                                var combineGoList = new HashSet<GameObject>(va.selectionGameObjects);
                                var combineVirtualList = new HashSet<HumanBodyBones>();
                                if (va.selectionHumanVirtualBones != null)
                                    combineVirtualList.UnionWith(va.selectionHumanVirtualBones);
                                var combineBindings = new HashSet<EditorCurveBinding>(va.uAw.GetCurveSelection());
                                if (rootNode.renderer != null && rootNode.renderer.gameObject != null)
                                    combineGoList.Add(rootNode.renderer.gameObject);
                                if (rootNode.infoList != null && rootNode.infoList.Length > 0)
                                {
                                    foreach (var info in rootNode.infoList)
                                        combineBindings.Add(va.AnimationCurveBindingBlendShape(rootNode.renderer, info.blendShapeName));
                                }
                                va.SelectGameObjects(combineGoList.ToArray(), combineVirtualList.ToArray());
                                va.SetAnimationWindowSynchroSelection(combineBindings.ToArray());
                            }
                            else
                            {
                                var combineGoList = new List<GameObject>();
                                var combineBindings = new List<EditorCurveBinding>();
                                if (rootNode.renderer != null && rootNode.renderer.gameObject != null)
                                    combineGoList.Add(rootNode.renderer.gameObject);
                                if (rootNode.infoList != null && rootNode.infoList.Length > 0)
                                {
                                    foreach (var info in rootNode.infoList)
                                        combineBindings.Add(va.AnimationCurveBindingBlendShape(rootNode.renderer, info.blendShapeName));
                                }
                                va.SelectGameObjects(combineGoList.ToArray());
                                va.SetAnimationWindowSynchroSelection(combineBindings.ToArray());
                            }
                        }
                        {
                            GUILayout.Space(FoldoutSpace);
                        }
                        {
                            EditorGUI.BeginChangeCheck();
                            var value = GUILayout.HorizontalSlider(blendShapeGroupValues[blendShapeGroupTreeTable[mg]], 0f, 100f);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vae, "Change BlendShape Group");
                                blendShapeGroupValues[blendShapeGroupTreeTable[mg]] = value;
                                if (mg.infoList != null && mg.infoList.Length > 0)
                                {
                                    foreach (var info in mg.infoList)
                                    {
                                        va.SetAnimationValueBlendShape(rootNode.renderer, info.blendShapeName, value);
                                    }
                                }
                            }
                        }
                        {
                            var width = FloatFieldWidth + IndentWidth * Math.Max(0, brotherMaxLevel);
                            EditorGUI.BeginChangeCheck();
                            var value = EditorGUILayout.FloatField(blendShapeGroupValues[blendShapeGroupTreeTable[mg]], GUILayout.Width(width));
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(vae, "Change BlendShape Group");
                                blendShapeGroupValues[blendShapeGroupTreeTable[mg]] = value;
                                if (mg.infoList != null && mg.infoList.Length > 0)
                                {
                                    foreach (var info in mg.infoList)
                                    {
                                        va.SetAnimationValueBlendShape(rootNode.renderer, info.blendShapeName, value);
                                    }
                                }
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    if (mg.foldout)
                    {
                        if (mg.infoList != null && mg.infoList.Length > 0)
                        {
                            #region BlendShape
                            foreach (var info in mg.infoList)
                            {
                                var blendShapeValue = va.GetAnimationValueBlendShape(rootNode.renderer, info.blendShapeName);
                                EditorGUILayout.BeginHorizontal(RowCount++ % 2 == 0 ? vaw.guiStyleAnimationRowEvenStyle : vaw.guiStyleAnimationRowOddStyle);
                                {
                                    EditorGUILayout.GetControlRect(false, GUILayout.Width(indentSpace + FoldoutWidth));
                                    if (GUILayout.Button(new GUIContent(info.blendShapeName, info.blendShapeName), GUILayout.Width(vaw.editorSettings.settingEditorNameFieldWidth)))
                                    {
                                        if (Shortcuts.IsKeyControl(e) || e.shift)
                                        {
                                            var combineGoList = new HashSet<GameObject>(va.selectionGameObjects);
                                            var combineVirtualList = new HashSet<HumanBodyBones>();
                                            if (va.selectionHumanVirtualBones != null)
                                                combineVirtualList.UnionWith(va.selectionHumanVirtualBones);
                                            var combineBindings = new HashSet<EditorCurveBinding>(va.uAw.GetCurveSelection());
                                            if (rootNode.renderer != null && rootNode.renderer.gameObject != null)
                                                combineGoList.Add(rootNode.renderer.gameObject);
                                            combineBindings.Add(va.AnimationCurveBindingBlendShape(rootNode.renderer, info.blendShapeName));
                                            va.SelectGameObjects(combineGoList.ToArray(), combineVirtualList.ToArray());
                                            va.SetAnimationWindowSynchroSelection(combineBindings.ToArray());
                                        }
                                        else
                                        {
                                            if (rootNode.renderer != null && rootNode.renderer.gameObject != null)
                                                va.SelectGameObject(rootNode.renderer.gameObject);
                                            va.SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { va.AnimationCurveBindingBlendShape(rootNode.renderer, info.blendShapeName) });
                                        }
                                    }
                                }
                                {
                                    var mirrorName = va.GetMirrorBlendShape(rootNode.renderer, info.blendShapeName);
                                    if (!string.IsNullOrEmpty(mirrorName))
                                    {
                                        if (GUILayout.Button(new GUIContent("", string.Format("Mirror: '{0}'", mirrorName)), vaw.guiStyleMirrorButton, GUILayout.Width(vaw.mirrorTex.width), GUILayout.Height(vaw.mirrorTex.height)))
                                        {
                                            if (rootNode.renderer != null && rootNode.renderer.gameObject != null)
                                                va.SelectGameObject(rootNode.renderer.gameObject);
                                            va.SetAnimationWindowSynchroSelection(new EditorCurveBinding[] { va.AnimationCurveBindingBlendShape(rootNode.renderer, mirrorName) });
                                        }
                                    }
                                    else
                                    {
                                        GUILayout.Space(FoldoutSpace);
                                    }
                                    if (blendShapeMirrorName)
                                    {
                                        var mirrorIndex = EditorCommon.ArrayIndexOf(rootNode.blendShapeNames, mirrorName);
                                        EditorGUI.BeginChangeCheck();
                                        mirrorIndex = EditorGUILayout.Popup(mirrorIndex, rootNode.blendShapeNames);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            string newMirrorName = mirrorIndex > 0 ? rootNode.blendShapeNames[mirrorIndex] : null;
                                            if (info.blendShapeName == newMirrorName)
                                                newMirrorName = null;
                                            va.ChangeBlendShapeMirror(rootNode.renderer, info.blendShapeName, newMirrorName);
                                            if (!string.IsNullOrEmpty(newMirrorName))
                                                va.ChangeBlendShapeMirror(rootNode.renderer, newMirrorName, info.blendShapeName);
                                        }
                                    }
                                }
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var value2 = GUILayout.HorizontalSlider(blendShapeValue, 0f, 100f);
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        va.SetAnimationValueBlendShape(rootNode.renderer, info.blendShapeName, value2);
                                    }
                                }
                                {
                                    EditorGUI.BeginChangeCheck();
                                    var value2 = EditorGUILayout.FloatField(blendShapeValue, GUILayout.Width(FloatFieldWidth));
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        va.SetAnimationValueBlendShape(rootNode.renderer, info.blendShapeName, value2);
                                    }
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                            #endregion
                        }
                    }
                };
                {
                    int maxLevel = 0;
                    foreach (var root in mgRoot)
                    {
                        if (root.renderer != null && root.mesh != null && root.renderer.sharedMesh == root.mesh)
                        {
                            if (root.foldout)
                                maxLevel = Math.Max(maxLevel, 1);
                        }
                    }
                    foreach (var root in mgRoot)
                    {
                        if (root.renderer != null && root.mesh != null && root.renderer.sharedMesh == root.mesh)
                        {
                            rootNode = root;
                            BlendShapeTreeNodeGUI(root, 1, maxLevel);
                        }
                    }
                }
                #endregion
                #endregion
            }
            else if (blendShapeMode == BlendShapeMode.List)
            {
                #region List
                if (e.type == EventType.Layout)
                {
                    UpdateBlendShapeSetListReorderableList();
                }
                if (blendShapeSetListReorderableList != null)
                {
                    blendShapeSetListReorderableList.DoLayoutList();
                }
                #endregion
            }
            else if (blendShapeMode == BlendShapeMode.Icon)
            {
                #region Icon
                if (e.type == EventType.Layout)
                {
                    UpdateBlendShapeSetIcon();
                }
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                    {
                        EditorGUI.BeginChangeCheck();
                        iconCameraMode = (IconCameraMode)EditorGUILayout.EnumPopup(iconCameraMode, EditorStyles.toolbarDropDown, GUILayout.Width(80f));
                        if (EditorGUI.EndChangeCheck())
                        {
                            iconUpdate = true;
                        }
                    }
                    EditorGUILayout.Space();
                    iconShowName = GUILayout.Toggle(iconShowName, "Show Name", EditorStyles.toolbarButton);
                    EditorGUILayout.Space();
                    iconSize = EditorGUILayout.Slider(iconSize, 32f, IconTextureSize);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.Space();
                if (va.blendShapeSetList.Count > 0)
                {
                    float areaWidth = vae.position.width - 16f;
                    int countX = Math.Max(1, Mathf.FloorToInt(areaWidth / iconSize));
                    int countY = Mathf.CeilToInt(va.blendShapeSetList.Count / (float)countX);
                    for (int i = 0; i < countY; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        for (int j = 0; j < countX; j++)
                        {
                            var index = i * countX + j;
                            if (index >= va.blendShapeSetList.Count) break;
                            var rect = EditorGUILayout.GetControlRect(false, iconSize, guiStyleIconButton, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
                            if (GUI.Button(rect, va.blendShapeSetList[index].icon, guiStyleIconButton))
                            {
                                var poseTemplate = va.blendShapeSetList[index].poseTemplate;
                                va.LoadPoseTemplate(poseTemplate, false, VeryAnimation.PoseFlags.BlendShape);
                            }
                            if (iconShowName)
                            {
                                var size = guiStyleNameLabelCenter.CalcSize(new GUIContent(va.blendShapeSetList[index].poseTemplate.name));
                                if (size.x < rect.width)
                                    EditorGUI.DropShadowLabel(rect, va.blendShapeSetList[index].poseTemplate.name, guiStyleNameLabelCenter);
                                else
                                    EditorGUI.DropShadowLabel(rect, va.blendShapeSetList[index].poseTemplate.name, guiStyleNameLabelRight);
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("List is Empty", EditorStyles.centeredGreyMiniLabel);
                }
                #endregion
            }
            EditorGUILayout.EndVertical();
        }
        
        private void UpdateBlendShapeSetListReorderableList()
        {
            if (blendShapeSetListReorderableList != null)
                return;

            blendShapeSetListReorderableList = new ReorderableList(va.blendShapeSetList, typeof(PoseTemplate), true, true, true, true);
            blendShapeSetListReorderableList.drawHeaderCallback = (Rect rect) =>
            {
                float x = rect.x;
                {
                    const float ButtonWidth = 100f;
                    #region Add
                    {
                        var r = rect;
                        r.width = ButtonWidth;
                        if (GUI.Button(r, Language.GetContent(Language.Help.BlendShapeTemplate), EditorStyles.toolbarDropDown))
                        {
                            Dictionary<string, string> blendShapeTemplates = new Dictionary<string, string>();
                            {
                                var guids = AssetDatabase.FindAssets("t:blendshapetemplate");
                                for (int i = 0; i < guids.Length; i++)
                                {
                                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                                    var name = path.Remove(0, "Assets/".Length);
                                    blendShapeTemplates.Add(name, path);
                                }
                            }

                            GenericMenu menu = new GenericMenu();
                            {
                                menu.AddItem(new GUIContent("All"), false, () =>
                                {
                                    Undo.RecordObject(vaw, "Template BlendShape");
                                    {
                                        var basePoseTemplate = ScriptableObject.CreateInstance<PoseTemplate>();
                                        va.SavePoseTemplate(basePoseTemplate, VeryAnimation.PoseFlags.BlendShape);
                                        for (int i = 0; i < basePoseTemplate.blendShapeValues.Length; i++)
                                        {
                                            for (int j = 0; j < basePoseTemplate.blendShapeValues[i].weights.Length; j++)
                                                basePoseTemplate.blendShapeValues[i].weights[j] = 0f;
                                        }
                                        for (int i = 0; i < basePoseTemplate.blendShapeValues.Length; i++)
                                        {
                                            var renderer = vaw.gameObject.transform.Find(basePoseTemplate.blendShapePaths[i]);
                                            if (renderer == null)
                                                continue;
                                            for (int j = 0; j < basePoseTemplate.blendShapeValues[i].weights.Length; j++)
                                            {
                                                var poseTemplate = ScriptableObject.Instantiate(basePoseTemplate);
                                                poseTemplate.name = renderer.name + "/" + basePoseTemplate.blendShapeValues[i].names[j];
                                                poseTemplate.blendShapeValues[i].weights[j] = 100f;
                                                va.blendShapeSetList.Add(new VeryAnimation.BlendShapeSet()
                                                {
                                                    poseTemplate = poseTemplate,
                                                });
                                            }
                                        }
                                        ScriptableObject.DestroyImmediate(basePoseTemplate);
                                    }
                                    iconUpdate = true;
                                });
                                menu.AddSeparator("");
                                {
                                    var enu = blendShapeTemplates.GetEnumerator();
                                    while (enu.MoveNext())
                                    {
                                        var value = enu.Current.Value;
                                        menu.AddItem(new GUIContent("Template/" + enu.Current.Key), false, () =>
                                        {
                                            var blendShapeTemplate = AssetDatabase.LoadAssetAtPath<BlendShapeTemplate>(value);
                                            if (blendShapeTemplate != null)
                                            {
                                                Undo.RecordObject(vaw, "Template BlendShape");
                                                foreach (var template in blendShapeTemplate.list)
                                                {
                                                    var set = new VeryAnimation.BlendShapeSet();
                                                    set.poseTemplate = template.GetPoseTemplate();
                                                    va.blendShapeSetList.Add(set);
                                                }
                                                iconUpdate = true;
                                            }
                                        });
                                    }
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
                            Undo.RecordObject(vaw, "Clear BlendShape");
                            va.blendShapeSetList.Clear();
                        }
                    }
                    #endregion
                    #region Save as
                    {
                        var r = rect;
                        r.width = ButtonWidth;
                        r.x = rect.xMax - r.width;
                        if (GUI.Button(r, Language.GetContent(Language.Help.BlendShapeSaveAs), EditorStyles.toolbarButton))
                        {
                            string path = EditorUtility.SaveFilePanel("Save as BlendShape Template", vae.templateSaveDefaultDirectory, string.Format("{0}_BlendShape.asset", vaw.gameObject.name), "asset");
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
                                    var blendShapeTemplate = ScriptableObject.CreateInstance<BlendShapeTemplate>();
                                    {
                                        foreach (var set in va.blendShapeSetList)
                                        {
                                            blendShapeTemplate.Add(set.poseTemplate);
                                        }
                                    }
                                    try
                                    {
                                        VeryAnimationWindow.CustomAssetModificationProcessor.Pause();
                                        AssetDatabase.CreateAsset(blendShapeTemplate, path);
                                    }
                                    finally
                                    {
                                        VeryAnimationWindow.CustomAssetModificationProcessor.Resume();
                                    }
                                    vae.Focus();
                                }
                            }
                        }
                    }
                    #endregion
                }
            };
            blendShapeSetListReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (index >= va.blendShapeSetList.Count)
                    return;
                
                float x = rect.x;
                {
                    const float Rate = 0.75f;
                    var r = rect;
                    r.x = x;
                    r.y += 2;
                    r.height -= 4;
                    r.width = rect.width * Rate;
                    x += r.width;
                    if (index == blendShapeSetListReorderableList.index)
                    {
                        EditorGUI.BeginChangeCheck();
                        var text = EditorGUI.TextField(r, va.blendShapeSetList[index].poseTemplate.name);
                        if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(text))
                        {
                            Undo.RecordObject(va.blendShapeSetList[index].poseTemplate, "Change set name");
                            va.blendShapeSetList[index].poseTemplate.name = text;
                        }
                    }
                    else
                    {
                        EditorGUI.LabelField(r, va.blendShapeSetList[index].poseTemplate.name);
                    }
                }
                {
                    const float Rate = 0.25f;
                    var r = rect;
                    r.x = x;
                    r.y += 2;
                    r.height -= 4;
                    r.width = rect.width * Rate;
                    x += r.width;
                    if (GUI.Button(r, "Set"))
                    {
                        var poseTemplate = va.blendShapeSetList[index].poseTemplate;
                        va.LoadPoseTemplate(poseTemplate, false, VeryAnimation.PoseFlags.BlendShape);
                    }
                }
            };
            blendShapeSetListReorderableList.onAddCallback = (ReorderableList list) =>
            {
                Undo.RecordObject(vaw, "Add BlendShape Set");

                var poseTemplate = ScriptableObject.CreateInstance<PoseTemplate>();
                va.SavePoseTemplate(poseTemplate, VeryAnimation.PoseFlags.BlendShape);
                {
                    var name = string.Format("Set {0}", va.blendShapeSetList.Count);
                    float max = 0f;
                    for (int i = 0; i < poseTemplate.blendShapeValues.Length; i++)
                    {
                        for (int j = 0; j < poseTemplate.blendShapeValues[i].weights.Length; j++)
                        {
                            if (poseTemplate.blendShapeValues[i].weights[j] > max)
                            {
                                var renderer = vaw.gameObject.transform.Find(poseTemplate.blendShapePaths[i]);
                                if (renderer != null)
                                {
                                    name = renderer.name + "/" + poseTemplate.blendShapeValues[i].names[j];
                                    max = poseTemplate.blendShapeValues[i].weights[j];
                                }
                            }
                        }
                    }
                    poseTemplate.name = name;
                }
                va.blendShapeSetList.Add(new VeryAnimation.BlendShapeSet()
                {
                    poseTemplate = poseTemplate,
                });
                iconUpdate = true;
                EditorApplication.delayCall += () =>
                {
                    blendShapeSetListReorderableList.index = va.blendShapeSetList.Count - 1;
                    vae.Repaint();
                };
            };
            blendShapeSetListReorderableList.onRemoveCallback = (ReorderableList list) =>
            {
                Undo.RecordObject(vaw, "Remove BlendShape Set");
                va.blendShapeSetList.RemoveAt(list.index);
                if (list.index >= list.count)
                    list.index = list.count - 1;
            };
        }

        private void UpdateBlendShapeSetIcon()
        {
            if (!iconUpdate)
                return;
            iconUpdate = false;

            if (va.blendShapeSetList == null || va.blendShapeSetList.Count <= 0)
                return;

            TransformPoseSave beforePose = new TransformPoseSave(va.editGameObject);
            BlendShapeWeightSave beforeBlendShape = new BlendShapeWeightSave(va.editGameObject);

            va.transformPoseSave.ResetDefaultTransform();
            va.blendShapeWeightSave.ResetDefaultWeight();

            var gameObject = GameObject.Instantiate<GameObject>(vaw.gameObject);
            gameObject.hideFlags |= HideFlags.HideAndDontSave;
            gameObject.transform.rotation = Quaternion.identity;
            EditorCommon.DisableOtherBehaviors(gameObject);

            var animator = gameObject.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.enabled = false;
            }

            int blankLayer;
            {
                for (blankLayer = 31; blankLayer > 0; blankLayer--)
                {
                    if (string.IsNullOrEmpty(LayerMask.LayerToName(blankLayer)))
                        break;
                }
                if (blankLayer < 0)
                    blankLayer = 31;
            }
            foreach (var renderer in gameObject.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;
                renderer.gameObject.layer = blankLayer;
            }
            var renderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(renderer => renderer != null && renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0).ToArray();
            foreach (var renderer in renderers)
            {
                renderer.updateWhenOffscreen = true;
#if UNITY_2018_3_OR_NEWER
                renderer.forceMatrixRecalculationPerRender = true;
#endif
            }

            {
                RenderTexture iconTexture = new RenderTexture(IconTextureSize, IconTextureSize, 16, RenderTextureFormat.ARGB32);
                iconTexture.hideFlags |= HideFlags.HideAndDontSave;
                iconTexture.Create();
                var cameraObject = new GameObject();
                cameraObject.hideFlags |= HideFlags.HideAndDontSave;
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = iconTexture;
                camera.clearFlags = CameraClearFlags.Color;
                camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << blankLayer;
                {
                    Bounds bounds = new Bounds();
                    foreach (var renderer in renderers)
                    {
                        if (Mathf.Approximately(bounds.size.sqrMagnitude, 0f))
                            bounds = renderer.bounds;
                        else
                            bounds.Encapsulate(renderer.bounds);
                    }
                    var transform = camera.transform;
                    var sizeMax = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                    switch (iconCameraMode)
                    {
                    case IconCameraMode.forward:
                        {
                            var rot = Quaternion.AngleAxis(180f, Vector3.up);
                            transform.localRotation = rot;
                            sizeMax = Mathf.Max(bounds.size.x, bounds.size.y);
                            transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z) - transform.forward;
                        }
                        break;
                    case IconCameraMode.back:
                        {
                            transform.localRotation = Quaternion.identity;
                            sizeMax = Mathf.Max(bounds.size.x, bounds.size.y);
                            transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z) - transform.forward;
                        }
                        break;
                    case IconCameraMode.up:
                        {
                            var rot = Quaternion.AngleAxis(90f, Vector3.right);
                            transform.localRotation = rot;
                            sizeMax = Mathf.Max(bounds.size.x, bounds.size.z);
                            transform.localPosition = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) - transform.forward;
                        }
                        break;
                    case IconCameraMode.down:
                        {
                            var rot = Quaternion.AngleAxis(-90f, Vector3.right);
                            transform.localRotation = rot;
                            sizeMax = Mathf.Max(bounds.size.x, bounds.size.z);
                            transform.localPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - transform.forward;
                        }
                        break;
                    case IconCameraMode.right:
                        {
                            var rot = Quaternion.AngleAxis(-90f, Vector3.up);
                            transform.localRotation = rot;
                            sizeMax = Mathf.Max(bounds.size.y, bounds.size.z);
                            transform.localPosition = new Vector3(bounds.max.x, bounds.center.y, bounds.center.z) - transform.forward;
                        }
                        break;
                    case IconCameraMode.left:
                        {
                            var rot = Quaternion.AngleAxis(90f, Vector3.up);
                            transform.localRotation = rot;
                            sizeMax = Mathf.Max(bounds.size.y, bounds.size.z);
                            transform.localPosition = new Vector3(bounds.min.x, bounds.center.y, bounds.center.z) - transform.forward;
                        }
                        break;
                    }
                    camera.orthographic = true;
                    camera.orthographicSize = sizeMax * 0.6f;
                    camera.nearClipPlane = 0.0001f;
                    camera.farClipPlane = 1f + sizeMax * 10f;
                }
                cameraObject.transform.SetParent(gameObject.transform);
                gameObject.transform.rotation = va.transformPoseSave.startRotation;

                foreach (var set in va.blendShapeSetList)
                {
                    if (set.poseTemplate.blendShapePaths != null && set.poseTemplate.blendShapeValues != null)
                    {
                        foreach (var renderer in renderers)
                        {
                            var path = AnimationUtility.CalculateTransformPath(renderer.transform, gameObject.transform);
                            var index = EditorCommon.ArrayIndexOf(set.poseTemplate.blendShapePaths, path);
                            if (index < 0) continue;
                            for (int i = 0; i < set.poseTemplate.blendShapeValues[index].names.Length; i++)
                            {
                                var sindex = renderer.sharedMesh.GetBlendShapeIndex(set.poseTemplate.blendShapeValues[index].names[i]);
                                if (sindex < 0 || sindex >= renderer.sharedMesh.blendShapeCount) continue;
                                renderer.SetBlendShapeWeight(sindex, set.poseTemplate.blendShapeValues[index].weights[i]);
                            }
                        }
                    }

#if !UNITY_2018_3_OR_NEWER
                    foreach (var renderer in renderers)
                    {
                        renderer.enabled = !renderer.enabled;
                        renderer.enabled = !renderer.enabled;
                    }
#endif
                    camera.Render();
                    {
                        RenderTexture save = RenderTexture.active;
                        RenderTexture.active = iconTexture;
                        if (set.icon == null)
                        {
                            set.icon = new Texture2D(iconTexture.width, iconTexture.height, TextureFormat.ARGB32, iconTexture.useMipMap);
                            set.icon.hideFlags |= HideFlags.HideAndDontSave;
                        }
                        set.icon.ReadPixels(new Rect(0, 0, iconTexture.width, iconTexture.height), 0, 0);
                        set.icon.Apply();
                        RenderTexture.active = save;
                    }
                }

                GameObject.DestroyImmediate(cameraObject);
                iconTexture.Release();
                RenderTexture.DestroyImmediate(iconTexture);
            }

            GameObject.DestroyImmediate(gameObject);

            beforePose.ResetDefaultTransform();
            beforeBlendShape.ResetDefaultWeight();
            {
                va.editGameObject.SetActive(false);
                va.editGameObject.SetActive(true);
            }
            va.SetUpdateSampleAnimation();
        }
    }
}
