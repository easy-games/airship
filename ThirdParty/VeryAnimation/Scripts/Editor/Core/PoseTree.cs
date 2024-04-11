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
    public class PoseTree
    {
        private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
        private VeryAnimation va { get { return VeryAnimation.instance; } }
        private VeryAnimationEditorWindow vae { get { return VeryAnimationEditorWindow.instance; } }

        public enum EditMode
        {
            All,
            Selection,
            Total
        }
        private EditMode editMode;

        private GUIContent[] EditModeString = new GUIContent[(int)EditMode.Total];

        private const int QuickSaveSize = 3;
        private PoseTemplate[] quickSaves;

        public PoseTree()
        {
            UpdateEditModeString();
            Language.OnLanguageChanged += UpdateEditModeString;
        }

        public void LoadEditorPref()
        {
            editMode = (EditMode)EditorPrefs.GetInt("VeryAnimation_Editor_Pose_EditMode", 0);
        }
        public void SaveEditorPref()
        {
            EditorPrefs.SetInt("VeryAnimation_Editor_Pose_EditMode", (int)editMode);
        }

        public void PoseTreeToolbarGUI()
        {
            EditorGUI.BeginChangeCheck();
            var mode = (EditMode)GUILayout.Toolbar((int)editMode, EditModeString, EditorStyles.miniButton);
            if (EditorGUI.EndChangeCheck())
            {
                editMode = mode;
            }
        }
        public void PoseTreeGUI()
        {
            EditorGUILayout.BeginVertical(vaw.guiStyleSkinBox);
            {
                EditorGUILayout.BeginHorizontal();
                #region Set
                if (GUILayout.Button("Reset", vaw.guiStyleDropDown))
                {
                    GenericMenu menu = new GenericMenu();
                    {
                        if (va.isHuman)
                        {
                            menu.AddItem(Language.GetContent(Language.Help.EditorPoseHumanoidReset), false, () =>
                            {
                                Undo.RecordObject(vae, "Humanoid Pose");
                                switch (editMode)
                                {
                                    case EditMode.All:
                                        va.SetPoseHumanoidDefault();
                                        break;
                                    case EditMode.Selection:
                                        va.SetSelectionHumanoidDefault(true, true, true);
                                        break;
                                }
                            });
                        }
                        if (va.transformPoseSave.IsEnableHumanDescriptionTransforms())
                        {
                            menu.AddItem(Language.GetContent(Language.Help.EditorPoseAvatarConfiguration), false, () =>
                            {
                                Undo.RecordObject(vae, "Avatar Configuration Pose");
                                switch (editMode)
                                {
                                    case EditMode.All:
                                        va.SetPoseHumanoidAvatarConfiguration();
                                        break;
                                    case EditMode.Selection:
                                        va.SetSelectionHumanoidAvatarConfiguration(true, true, true);
                                        break;
                                }
                            });
                        }
                        if (va.transformPoseSave.IsEnableTPoseTransform())
                        {
                            menu.AddItem(Language.GetContent(Language.Help.EditorPoseTPose), false, () =>
                            {
                                Undo.RecordObject(vae, "T Pose");
                                switch (editMode)
                                {
                                    case EditMode.All:
                                        va.SetPoseHumanoidTPose();
                                        break;
                                    case EditMode.Selection:
                                        va.SetSelectionHumanoidTPose(true, true, true);
                                        break;
                                }
                            });
                        }
                        if (va.transformPoseSave.IsEnableBindTransform())
                        {
                            menu.AddItem(Language.GetContent(Language.Help.EditorPoseBind), false, () =>
                            {
                                Undo.RecordObject(vae, "Bind Pose");
                                switch (editMode)
                                {
                                    case EditMode.All:
                                        va.SetPoseBind();
                                        break;
                                    case EditMode.Selection:
                                        va.SetSelectionBindPose(true, true, true);
                                        break;
                                }
                            });
                        }
                        if (va.transformPoseSave.IsEnablePrefabTransform())
                        {
                            menu.AddItem(Language.GetContent(Language.Help.EditorPosePrefab), false, () =>
                            {
                                Undo.RecordObject(vae, "Prefab Pose");
                                switch (editMode)
                                {
                                    case EditMode.All:
                                        va.SetPosePrefab();
                                        break;
                                    case EditMode.Selection:
                                        va.SetSelectionPrefabPose(true, true, true);
                                        break;
                                }
                            });
                        }
                        {
                            menu.AddItem(Language.GetContent(Language.Help.EditorPoseStart), false, () =>
                            {
                                Undo.RecordObject(vae, "Edit Start Pose");
                                switch (editMode)
                                {
                                    case EditMode.All:
                                        va.SetPoseEditStart();
                                        break;
                                    case EditMode.Selection:
                                        va.SetSelectionEditStart(true, true, true);
                                        break;
                                }
                            });
                        }
                    }
                    menu.ShowAsContext();
                }
                #endregion
                #region Mirror
                if (GUILayout.Button(Language.GetContent(Language.Help.EditorPoseMirror)))
                {
                    Undo.RecordObject(vae, "Mirror Pose");
                    switch (editMode)
                    {
                        case EditMode.All:
                            va.SetPoseMirror();
                            break;
                        case EditMode.Selection:
                            va.SetSelectionMirror();
                            break;
                    }
                }
                #endregion
                #region Template
                if (GUILayout.Button(Language.GetContent(Language.Help.EditorPoseTemplate), vaw.guiStyleDropDown))
                {
                    Dictionary<string, string> poseTemplates = new Dictionary<string, string>();
                    {
                        var guids = AssetDatabase.FindAssets("t:posetemplate");
                        for (int i = 0; i < guids.Length; i++)
                        {
                            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                            var name = path.Remove(0, "Assets/".Length);
                            poseTemplates.Add(name, path);
                        }
                    }

                    GenericMenu menu = new GenericMenu();
                    {
                        var enu = poseTemplates.GetEnumerator();
                        while (enu.MoveNext())
                        {
                            var value = enu.Current.Value;
                            menu.AddItem(new GUIContent(enu.Current.Key), false, () =>
                            {
                                var poseTemplate = AssetDatabase.LoadAssetAtPath<PoseTemplate>(value);
                                if (poseTemplate != null)
                                {
                                    Undo.RecordObject(vae, "Template Pose");
                                    Undo.RegisterCompleteObjectUndo(va.currentClip, "Template Pose");
                                    switch (editMode)
                                    {
                                        case EditMode.All:
                                            va.LoadPoseTemplate(poseTemplate);
                                            break;
                                        case EditMode.Selection:
                                            va.LoadSelectionPoseTemplate(poseTemplate, true);
                                            break;
                                    }
                                }
                                else
                                {
                                    Debug.LogErrorFormat(Language.GetText(Language.Help.LogFailedLoadPoseError), value);
                                }
                            });
                        }
                    }
                    menu.ShowAsContext();
                }
                #endregion
                EditorGUILayout.Space();
                #region Save as
                if (GUILayout.Button(Language.GetContent(Language.Help.EditorPoseSaveAs)))
                {
                    string path = EditorUtility.SaveFilePanel("Save as Pose Template", vae.templateSaveDefaultDirectory, string.Format("{0}.asset", va.currentClip.name), "asset");
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
                            var poseTemplate = ScriptableObject.CreateInstance<PoseTemplate>();
                            va.SavePoseTemplate(poseTemplate);
                            try
                            {
                                VeryAnimationWindow.CustomAssetModificationProcessor.Pause();
                                AssetDatabase.CreateAsset(poseTemplate, path);
                            }
                            finally
                            {
                                VeryAnimationWindow.CustomAssetModificationProcessor.Resume();
                            }
                            vae.Focus();
                        }
                    }
                }
                #endregion
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Quick Load", GUILayout.Width(70));
                    Action<int> QuickLoad = (index) =>
                    {
                        EditorGUI.BeginDisabledGroup(quickSaves == null || index >= quickSaves.Length || quickSaves[index] == null);
                        if (GUILayout.Button((index + 1).ToString()))
                        {
                            Undo.RecordObject(vae, "Quick Load");
                            Undo.RegisterCompleteObjectUndo(va.currentClip, "Quick Load");
                            switch (editMode)
                            {
                                case EditMode.All:
                                    va.LoadPoseTemplate(quickSaves[index]);
                                    break;
                                case EditMode.Selection:
                                    va.LoadSelectionPoseTemplate(quickSaves[index], true);
                                    break;
                            }
                        }
                        EditorGUI.EndDisabledGroup();
                    };
                    for (int i = 0; i < QuickSaveSize; i++)
                    {
                        QuickLoad(i);
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Quick Save", GUILayout.Width(70));
                    Action<int> QuickSave = (index) =>
                    {
                        if (GUILayout.Button((index + 1).ToString()))
                        {
                            Undo.RecordObject(vae, "Quick Save");
                            if (quickSaves == null || quickSaves.Length != QuickSaveSize)
                                quickSaves = new PoseTemplate[QuickSaveSize];
                            {
                                quickSaves[index] = ScriptableObject.CreateInstance<PoseTemplate>();
                                va.SavePoseTemplate(quickSaves[index]);
                            }
                        }
                    };
                    for (int i = 0; i < QuickSaveSize; i++)
                    {
                        QuickSave(i);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.Space(3);
            EditorGUILayout.EndVertical();
        }

        private void UpdateEditModeString()
        {
            for (int i = 0; i < (int)EditMode.Total; i++)
            {
                EditModeString[i] = new GUIContent(Language.GetContent(Language.Help.EditorPoseModeAll + i));
            }
        }
    }
}
