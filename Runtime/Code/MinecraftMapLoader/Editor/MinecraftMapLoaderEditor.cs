#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = System.Object;

[CustomEditor(typeof(MinecraftMapLoader))]
public class MinecraftMapLoaderEditor : UnityEditor.Editor {
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        MinecraftMapLoader mapLoader = (MinecraftMapLoader) target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("This will load a Minecraft schematic json into the current world. A new world file will not be created.", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("Load Minecraft Map")) {
            mapLoader.LoadMap();
        }
    }
}
#endif
    
