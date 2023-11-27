using System;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

public class AutoStartServer : MonoBehaviour {
    private void Start() {
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager) {
            networkManager.ServerManager.StartConnection();
        }


    }
}