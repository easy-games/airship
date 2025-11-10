#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Editor.EditorInternal;
using Editor.Util;
using Luau;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

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
            var customEditorType = AirshipCustomEditors.GetEditorTypeForTypeName(binding.metadata.name);
            
            if (customEditorType != null && AirshipCustomEditors.TryGetEditorForScriptableObject(binding, customEditorType, out var editor)) {
                editor.OnEnable();
                this.editor = editor;
            }
        }
    }

    private void OnDisable() {
        AirshipScriptableObject binding = (AirshipScriptableObject)target;
        if (binding.script != null && binding.metadata != null) {
            var customEditorType = AirshipCustomEditors.GetEditorTypeForTypeName(binding.metadata.name);
            if (customEditorType != null) {
                var editor = AirshipCustomEditors.GetEditorForScriptableObject(binding, customEditorType, serializedObject);
                editor.OnDisable();
            }

            this.editor = null;
        }
    }

    public override void OnInspectorGUI() {
        AirshipScriptableObject binding = (AirshipScriptableObject)target;
        
        binding.ReconcileMetadata(ReconcileSource.Inspector);
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
        
        Type customEditorType = null;
        if (binding.script != null && binding.metadata != null) {
            customEditorType = AirshipCustomEditors.GetEditorTypeForTypeName(binding.metadata.name);
        }

        var script = binding.script;
        EditorGUILayout.Space(5);
        
        // var newScript = (AirshipScript) EditorGUILayout.ObjectField(new GUIContent("Script"), script, typeof(AirshipScript), true);

        if (script != null) {
            GUI.enabled = script == null;
            EditorGUILayout.ObjectField(new GUIContent("Script"), script, typeof(AirshipScript), false);
            GUI.enabled = true;
        } else {
            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            AirshipEditorGUI.AirshipScriptField(rect, new GUIContent("Script"), script, OnScriptSelectionChanged, AirshipEditorGUI.ScriptExportType.ScriptableObject);
        }

        
        EditorGUILayout.Space(5);
        
 
        
        if (!AirshipCustomEditors.UseNewInspector) {
            EditorGUILayout.HelpBox("AirshipScriptableObject requires the new Editor API to be enabled", MessageType.Warning);
            return;
        }

        if (script == null) return;
        
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
            
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        } else {
            EditorGUILayout.HelpBox("Could not find custom inspector", MessageType.Warning);
        }
    }

    private void OnScriptSelectionChanged(AirshipScript targetScript) {
        serializedObject.FindProperty("script").objectReferenceValue = targetScript;
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
    }
}
#endif