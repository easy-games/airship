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
    [SerializeField]
    private GameObject[] backdrops;
    
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

    public void SetBackdrop(int index){
        for(int i=0; i<backdrops.Length; i++){
            backdrops[i].SetActive(i == index);
        }
    }
#endif
}
