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
         
        if (GUILayout.Button("Load Map")) {
            mapLoader.LoadMap();
        }
    }
}
#endif
    
