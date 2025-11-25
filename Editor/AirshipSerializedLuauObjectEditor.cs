#if UNITY_EDITOR
using System;
using UnityEditor;
#if AIRSHIPEX_CLASS_OBJECT
[CustomEditor(typeof(AirshipSerializableClassObject))]
public class AirshipSerializedLuauObjectEditor : UnityEditor.Editor {
    private AirshipEditor editor;
    public override void OnInspectorGUI() {
        AirshipSerializableClassObject binding = (AirshipSerializableClassObject)target;
        
        Type customEditorType = null;
        if (binding.metadata != null) {
            customEditorType = AirshipCustomEditors.GetEditorTypeForTypeName(binding.metadata.name);
        }
        
        if (customEditorType != null) {
            var componentEditor = AirshipCustomEditors.GetEditorForClass(binding, customEditorType, serializedObject);
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
#endif