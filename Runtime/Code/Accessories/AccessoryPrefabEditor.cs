using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[SelectionBase]
public class AccessoryPrefabEditor : MonoBehaviour {
#if UNITY_EDITOR
    private bool _isInPrefab = false;
    
    private void Start() {
        if (Application.isPlaying) return;
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (!stage) return;

        _isInPrefab = stage.prefabContentsRoot == gameObject;
        if (!_isInPrefab) return;
        
        
    }

    private void Update() {
        if (!_isInPrefab) return;
    }
#endif
}
