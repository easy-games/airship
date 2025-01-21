
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EasyGridAlign))]
public class EasyGridAlignEditor : UnityEditor.Editor {

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EasyGridAlign self = (EasyGridAlign)target;
        DrawDefaultInspector();
        if (GUILayout.Button("Rebuild")) {
            self.Rebuild();
        }
        serializedObject.ApplyModifiedProperties();
    }
}
#endif