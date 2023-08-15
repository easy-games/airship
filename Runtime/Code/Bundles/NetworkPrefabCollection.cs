using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = "NetworkPrefabCollection", menuName = "Airship/Create Network Prefab Collection")]
public class NetworkPrefabCollection : ScriptableObject {
    public List<Object> networkPrefabs = new();

    private void OnValidate() {
        this.ScanForPrefabs();
    }

    public void ScanForPrefabs() {
        // Debug.Log("scanning...");
        // string[] guids = AssetDatabase.FindAssets("t:" + typeof(NetworkObject).Name);
        // foreach (var guid in guids) {
        //     var path = AssetDatabase.GUIDToAssetPath(guid);
        //     Debug.Log("found path: " + path);
        // }
    }
}