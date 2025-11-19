#if UNITY_EDITOR
using System;
using UnityEditor;

[CustomEditor(typeof(AirshipSerializedLuauObject))]
public class AirshipSerializedLuauObjectEditor : UnityEditor.Editor {
    private AirshipEditor editor;
    public override void OnInspectorGUI() {
        AirshipSerializedLuauObject binding = (AirshipSerializedLuauObject)target;
        
        Type customEditorType = null;
        if (binding.metadata != null) {
            customEditorType = AirshipCustomEditors.GetEditorTypeForTypeName(binding.metadata.name);
        }
        
        if (customEditorType != null) {
            var componentEditor = AirshipCustomEditors.GetEditorForScriptableClass(binding, customEditorType, serializedObject);
            if (this.editor == null) this.editor = componentEditor;
            componentEditor.script = binding.GetAirshipType().Script;
            componentEditor.target = binding;
            componentEditor.OnInspectorGUI();
            
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }
    }
}
#endif