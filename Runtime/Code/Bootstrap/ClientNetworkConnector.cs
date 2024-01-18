using System.Collections;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;

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
        var networkManager = FindAnyObjectByType<NetworkManager>();
        if (networkManager == null) {
            Debug.LogError("Failed to find NetworkManager.");
            return;
        }

        if (RunCore.IsClient()) {
            InstanceFinder.NetworkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            
            var transferData = CrossSceneState.ServerTransferData;
            if (!RunCore.IsEditor()) {
                Debug.Log($"Connecting to server {transferData.address}:{transferData.port}");
            }

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

            InstanceFinder.NetworkManager.ClientManager.StartConnection(transferData.address, transferData.port);
            // InstanceFinder.NetworkManager.ClientManager.StartConnection();
        }
    }

    private IEnumerator LateJoin(int delaySeconds) {
        yield return new WaitForSeconds(delaySeconds);
        InstanceFinder.NetworkManager.ClientManager.StartConnection();
    }
    
    private void OnDisable()
    {
        if (RunCore.IsClient())
        {
            InstanceFinder.NetworkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        // Debug.Log("Connection state changed: " + args.ConnectionState);
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            this.reconnectAttempt = 0;
            return;
        }

        if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            if (!this.expectingDisconnect)
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.name == "CoreScene")
                {
                    this.reconnectAttempt++;
                    StartCoroutine(Reconnect());   
                } else
                {
                    TransferManager.Instance.Disconnect();
                }
            }   
        }
    }

    private IEnumerator Reconnect()
    {
        float delay = 1f;
        yield return new WaitForSeconds(delay);

        Debug.Log("Reconnecting... (" + reconnectAttempt + ")");
        var transferData = CrossSceneState.ServerTransferData;
        InstanceFinder.NetworkManager.ClientManager.StartConnection(transferData.address, transferData.port);
    }
}