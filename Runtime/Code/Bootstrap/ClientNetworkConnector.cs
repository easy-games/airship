using System.Collections;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;

#if UNITY_EDITOR
using ParrelSync;
#endif 
using UnityEngine;
using UnityEngine.SceneManagement;

[LuauAPI]
public class ClientNetworkConnector : MonoBehaviour {
    private bool expectingDisconnect = false;
    private ushort reconnectAttempt = 1;
    
    private void Start() {
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager == null) {
            Debug.LogError("Failed to find NetworkManager.");
            return;
        }

        if (RunCore.IsClient())
        {
            InstanceFinder.NetworkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            
            var transferData = CrossSceneState.ServerTransferData;
            Debug.Log($"Connecting to server {transferData.address}:{transferData.port}");
            InstanceFinder.NetworkManager.ClientManager.StartConnection(transferData.address, transferData.port);
        }
    }
    
    private void OnDisable()
    {
        if (RunCore.IsClient())
        {
            InstanceFinder.NetworkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }
    }

    private IEnumerator DisconnectAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();

        this.expectingDisconnect = true;
        Debug.Log("Disconnect.1");
        LuauCore.ResetInstance();

        Debug.Log("Disconnect.2");
        var clientBundleLoader = FindObjectOfType<ClientBundleLoader>();
        if (clientBundleLoader)
        {
            Debug.Log("Disconnect.2.1");
            clientBundleLoader.UnloadGameSceneServerRpc();
            clientBundleLoader.DisconnectServerRpc();
        }

        Debug.Log("Disconnect.3");
        var players = GameObject.Find("Players");
        Object.Destroy(players);

        Debug.Log("Disconnect.4");
        var network = GameObject.Find("Network");
        Object.Destroy(network);

        Debug.Log("Disconnect.5");
        SystemRoot.Instance.UnloadBundles();

        Debug.Log("Disconnect.6");
        Object.Destroy(this.gameObject);
        Debug.Log("Disconnect.7");
    }

    public void Disconnect()
    {
        StartCoroutine(DisconnectAtEndOfFrame());
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        Debug.Log("Connection state changed: " + args.ConnectionState);
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
                    SceneManager.LoadScene("MainMenu");
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