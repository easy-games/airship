#if UNITY_EDITOR
using Luau;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BinaryFile))]
public class BinaryFileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var binFile = (BinaryFile)target;
        var metadata = binFile.m_metadata;

        EditorGUILayout.Space();

        if (metadata == null)
        {
            GUILayout.Label("No associated metadata");
            return;
        }

        GUILayout.Label("Properties");
        if (metadata.properties == null)
        {
            GUILayout.Label("Properties is null!");
            return;
        }
        foreach (var item in metadata.properties)
        {
            GUILayout.Label(string.IsNullOrEmpty(item.modifiers)
                ? $"{item.name}: {item.type}"
                : $"{item.name}: {item.type} ({item.modifiers})");
        }
    }
}

#endif
