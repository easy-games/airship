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
            NetworkClient.OnTransportExceptionEvent += NetworkClient_OnTransportException;

            var transferData = CrossSceneState.ServerTransferData;

#if UNITY_EDITOR
            var tags = CurrentPlayer.ReadOnlyTags();
            foreach (var tag in tags) {
                if (tag.ToLower().StartsWith("latejoin:")) {
                    var split = tag.ToLower().Split("latejoin:");
                    if (split.Length == 2) {
                        var num = int.Parse(split[1]);
                        Debug.Log($"[Airship]: Delaying join by {num} seconds. This is due to having the {tag} MPPM tag.");
                        var uri = new Uri("kcp://" + transferData.address + ":" + transferData.port);
                        StartCoroutine(LateJoin(num, uri));
                        return;
                    }
                }
            }
#endif

            if (!RunCore.IsServer()) {
                Debug.Log($"Connecting to server {transferData.address}:{transferData.port}");
                var uri = new Uri("kcp://" + transferData.address + ":" + transferData.port);
                // NetworkManager.singleton.StartClient(uri);
                StartCoroutine(LateJoin(1, uri));
            }
        }
    }

    private IEnumerator LateJoin(int delaySeconds, Uri uri) {
        yield return new WaitForSecondsRealtime(delaySeconds);
        NetworkManager.singleton.StartClient(uri);
    }
    
    private void OnDisable() {
        if (RunCore.IsClient()) {
            NetworkClient.OnConnectedEvent -= NetworkClient_OnConnected;
            NetworkClient.OnDisconnectedEvent -= NetworkClient_OnDisconnected;
            NetworkClient.OnTransportExceptionEvent -= NetworkClient_OnTransportException;
        }
    }

    private void NetworkClient_OnConnected() {
        this.reconnectAttempt = 0;
    }

    private void NetworkClient_OnDisconnected() {
        print("OnDisconnected");
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

    void NetworkClient_OnTransportException(Exception e) {
        Debug.LogWarning("Transport exception:");
        Debug.LogException(e);
    }

    private IEnumerator Reconnect() {
        float delay = 1f;
        yield return new WaitForSecondsRealtime(delay);

        Debug.Log("Reconnecting... (" + reconnectAttempt + ")");
        var transferData = CrossSceneState.ServerTransferData;
        NetworkClient.Connect(transferData.address + ":" + transferData.port);
    }
}