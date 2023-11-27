using System;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class SpawnLocalPlayer : NetworkBehaviour {
    public NetworkObject characterPrefab;
    public Transform spawnPosition;

    private void Start() {
        InstanceFinder.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
    }

    private void OnDestroy() {
        if (InstanceFinder.SceneManager) {
            InstanceFinder.SceneManager.OnClientLoadedStartScenes -= SceneManager_OnClientLoadedStartScenes;
        }
    }

    private void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
    {
        if (!asServer)
            return;
        if (characterPrefab == null)
        {
            Debug.LogWarning($"Player prefab is empty and cannot be spawned for connection {conn.ClientId}.");
            return;
        }

        Vector3 position = this.spawnPosition.position;
        Quaternion rotation = this.spawnPosition.rotation;

        NetworkObject nob = InstanceFinder.NetworkManager.GetPooledInstantiated(characterPrefab, position, rotation, true);
        InstanceFinder.NetworkManager.ServerManager.Spawn(nob, conn);
        nob.gameObject.AddComponent<EditorCharacterMovementControls>();

        //If there are no global scenes
        InstanceFinder.NetworkManager.SceneManager.AddOwnerToDefaultScene(nob);
    }
}