using System;
using System.Collections;
using kcp2k;
using Mirror;

#if UNITY_EDITOR
using ParrelSync;
using Unity.Multiplayer.Playmode;
#endif 
using UnityEngine;
using UnityEngine.SceneManagement;

[LuauAPI]
public class ClientNetworkConnector : MonoBehaviour {
    public bool expectingDisconnect = false;
    private ushort reconnectAttempt = 1;
    
    private void Start() {
        if (RunCore.IsClient()) {
            NetworkClient.OnConnectedEvent += NetworkClient_OnConnected;
            NetworkClient.OnDisconnectedEvent += NetworkClient_OnDisconnected;
            
            var transferData = CrossSceneState.ServerTransferData;

#if UNITY_EDITOR
            var tags = CurrentPlayer.ReadOnlyTags();
            foreach (var tag in tags) {
                if (tag.ToLower().StartsWith("latejoin:")) {
                    var split = tag.ToLower().Split("latejoin:");
                    if (split.Length == 2) {
                        var num = int.Parse(split[1]);
                        Debug.Log($"[Airship]: Delaying join by {num} seconds. This is due to having the {tag} MPPM tag.");
                        StartCoroutine(LateJoin(num));
                        return;
                    }
                }
            }
#endif

            if (!RunCore.IsServer()) {
                Debug.Log($"Connecting to server {transferData.address}:{transferData.port}");
                var uri = new Uri("kcp://" + transferData.address + ":" + transferData.port);
                NetworkManager.singleton.StartClient(uri);
            }
        }
    }

    private IEnumerator LateJoin(int delaySeconds) {
        throw new Exception("Airship LateJoin is not implemented.");
        // yield return new WaitForSecondsRealtime(delaySeconds);
        // InstanceFinder.NetworkManager.ClientManager.StartConnection();
    }
    
    private void OnDisable() {
        if (RunCore.IsClient()) {
            NetworkClient.OnConnectedEvent -= NetworkClient_OnConnected;
            NetworkClient.OnDisconnectedEvent -= NetworkClient_OnDisconnected;
        }
    }

    private void NetworkClient_OnConnected() {
        this.reconnectAttempt = 0;
    }

    private void NetworkClient_OnDisconnected() {
        if (!this.expectingDisconnect) {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == "CoreScene") {
                this.reconnectAttempt++;
                StartCoroutine(Reconnect());
            } else {
                TransferManager.Instance.Disconnect();
            }
        }
    }

    private IEnumerator Reconnect() {
        float delay = 1f;
        yield return new WaitForSecondsRealtime(delay);

        Debug.Log("Reconnecting... (" + reconnectAttempt + ")");
        var transferData = CrossSceneState.ServerTransferData;
        NetworkClient.Connect(transferData.address + ":" + transferData.port);
    }
}