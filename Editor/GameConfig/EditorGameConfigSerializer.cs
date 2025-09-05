using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public class EditorGameConfigSerializer {
    static EditorGameConfigSerializer() {
        #if !AIRSHIP_PLAYER
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        #endif
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange stateChange) {
        if (stateChange == PlayModeStateChange.ExitingEditMode) {
            try {
                var gameConfig = GameConfig.Load();
                
                // Only Dirty & Save GameConfig if the JSON changes. On my device takes less than 10ms if no changes
                // were found. If we do have to save this takes around 80ms.
                var prevHash = JsonUtility.ToJson(gameConfig).GetHashCode();
                gameConfig.SerializeSettings();
                if (prevHash != JsonUtility.ToJson(gameConfig).GetHashCode()) {
                    EditorUtility.SetDirty(gameConfig);
                    AssetDatabase.SaveAssetIfDirty(gameConfig);   
                }
            } catch (Exception ex) {
                Debug.LogError("Error when copying Unity properties to GameConfig: " + ex);
            }
        }
    }
}