#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Airship.Editor;
using Easy.Airship.Editor.EditorInternal;
using Easy.Airship.Editor.Util;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;
using Object = UnityEngine.Object;

internal class DoCreateScriptableObject : EndNameEditAction {
    public AirshipScript script;
    public override void Action(int instanceId, string pathName, string resourceFile) {
        var scriptableObject = ScriptableObject.CreateInstance<AirshipScriptableObject>();
        scriptableObject.script = script;
        AssetDatabase.CreateAsset(scriptableObject, pathName);
        ProjectWindowUtil.ShowCreatedAsset(scriptableObject);
    }
}

[CustomEditor(typeof(AirshipScriptableObject))]
public class AirshipScriptableObjectEditor : UnityEditor.Editor {
    internal static string TemplatePath => "Packages/gg.easy.airship/Editor/Templates";
    internal static string AirshipScriptableObjectTemplate => PosixPath.Join(TemplatePath, "AirshipScriptableObject.asset.txt");
    
    private AirshipEditor editor;

    public const string CreateScriptableObjectMenu = "Assets/Create/Airship/Airship Scriptable Object Asset...";
    
    [InitializeOnLoadMethod]
    internal static void Init() {
#if AIRSHIP_INTERNAL
        EditorApplication.delayCall += () => {
            if (!AirshipEditorInternals.HasUnityMenuItem(CreateScriptableObjectMenu)) {
                AirshipEditorInternals.AddUnityMenuItem(
                    CreateScriptableObjectMenu, 
                    "", false, 50, 
                    CreateScriptableObjectPrompt, () => true);
            }
        };
#endif
    }
    
    internal static void CreateScriptableObjectPrompt() {
        var context = new AirshipScriptSelectionContext(AirshipScriptType.ScriptableObject, null, null, false);
        AirshipScriptSelectorWindow.Show(context, null, (script) => {
            if (script == null) return;
            CreateAirshipScriptableObject(script, Path.GetFileNameWithoutExtension(script.assetPath) + ".asset");
        });
    }

    internal static void CreateAirshipScriptableObject(AirshipScript script, string defaultFileName) {
        var scriptable = ScriptableObject.CreateInstance<AirshipScriptableObject>();
        scriptable.script = script;
        
        var createInstance = ScriptableObject.CreateInstance<DoCreateScriptableObject>();
        createInstance.script = script;
            
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, 
            createInstance, defaultFileName, null, "");
    }

    private void OnEnable() {
        AirshipScriptableObject binding = (AirshipScriptableObject)target;
        if (binding.script != null && binding.metadata != null) {
            var customEditorType = AirshipCustomEditors.GetEditorTypeForTypeName(binding.metadata.name, AirshipDeclarationType.AirshipScriptableObject);
            
            if (customEditorType != null && AirshipCustomEditors.TryGetEditorForScriptableObject(binding, customEditorType, out var editor)) {
                editor.OnEnable();
                this.editor = editor;
            }
        }
    }

    private void OnDisable() {
        AirshipScriptableObject binding = (AirshipScriptableObject)target;
        if (binding.script != null && binding.metadata != null) {
            var customEditorType = AirshipCustomEditors.GetEditorTypeForTypeName(binding.metadata.name, AirshipDeclarationType.AirshipScriptableObject);
            if (customEditorType != null) {
                var editor = AirshipCustomEditors.GetEditorForScriptableObject(binding, customEditorType, serializedObject);
                editor.OnDisable();
            }

            this.editor = null;
        }
    }

    private const string LuaIconOk = "Packages/gg.easy.airship/Editor/AirshipScriptableObject.png";
    
    protected override void OnHeaderGUI() {
        var scriptableObject = target as AirshipScriptableObject;
        var script = scriptableObject != null ? scriptableObject.script : null;
        if (script == null) {
            base.OnHeaderGUI();
            return;
        }
        

        
        GUILayout.BeginHorizontal("In BigTitle", GUILayout.ExpandWidth(true));
        
        var rect = EditorGUILayout.GetControlRect(false, 40, "IN BigTitle");
        
        var textureImage = new Rect(rect);
        textureImage.y += 0;
        textureImage.x += 0;
        textureImage.width = 38;
        textureImage.height = 38;


        rect.x += 40;
        
        GUI.Label(rect, ObjectNames.NicifyVariableName(target.name), "IN TitleText");
        GUI.Label(new RectOffset(2, 0, -10, 0).Add(rect), ObjectNames.NicifyVariableName(script.m_metadata.name));

        var icon = script.m_metadata.displayIcon != null ? script.m_metadata.displayIcon : AssetDatabase.LoadAssetAtPath<Texture2D>(LuaIconOk);
        GUI.Label(textureImage, icon);
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal(GUILayout.Height(30));
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reimport", GUILayout.MaxWidth(100))) {
                AssetDatabase.StartAssetEditing();
                AssetDatabase.ImportAsset(script.assetPath, ImportAssetOptions.Default);
                AssetDatabase.StopAssetEditing();
                return;
            }
            
            if (GUILayout.Button("Edit", GUILayout.MaxWidth(100))) {
                TypescriptProjectsService.OpenFileInEditor(script.assetPath);
            }
            
            GUILayout.Space(5);
        }
        EditorGUILayout.EndHorizontal();
    }

    public override void OnInspectorGUI() {
        AirshipScriptableObject binding = (AirshipScriptableObject)target;
        
        binding.ReconcileMetadata(ReconcileSource.Inspector);
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
        
        Type customEditorType = null;
        if (binding.script != null && binding.metadata != null) {
            customEditorType = AirshipCustomEditors.GetEditorTypeForTypeName(binding.metadata.name, AirshipDeclarationType.AirshipScriptableObject);
        }

        var script = binding.script;
        
        if (!AirshipCustomEditors.UseNewInspector) {
            EditorGUILayout.HelpBox("AirshipScriptableObject requires the new Editor API to be enabled", MessageType.Warning);
            return;
        }

        if (script == null) {
            EditorGUILayout.Space(5);
            // var newScript = EditorGUILayout.ObjectField(new GUIContent("Script"), script, typeof(AirshipScript), true);

            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, EditorStyles.label);
            AirshipEditorGUI.AirshipScriptField(rect, new GUIContent("Script"), script, (script) => {
                    binding.script = script;
                },
                AirshipEditorGUI.ScriptExportType.ScriptableObject, false);
            
            EditorGUILayout.Space(5);
            return;
        }
        
        if (script.scriptType != AirshipScriptType.ScriptableObject) {
            EditorGUILayout.HelpBox("Script is not a ScriptableObject", MessageType.Warning);
            return;
        }
        
        if (customEditorType != null && binding.script != null) {
            var componentEditor = AirshipCustomEditors.GetEditorForScriptableObject(binding, customEditorType, serializedObject);
            if (this.editor == null) this.editor = componentEditor;
            componentEditor.script = binding.script;
            componentEditor.target = binding;
            componentEditor.OnInspectorGUI();

            if (serializedObject.hasModifiedProperties) {
                EditorUtility.SetDirty(binding);
            }
            
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        } else {
            EditorGUILayout.HelpBox("Could not find custom inspector", MessageType.Warning);
        }
    }


}
#endif