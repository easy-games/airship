using System;
using System.Collections;
using Airship.DevConsole;
using Code.Authentication;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

[LuauAPI(LuauContext.Protected)]
public class TransferManager : Singleton<TransferManager> {

    private void Awake() {
        DontDestroyOnLoad(this);
    }

    public bool ConnectToServer(string address, ushort port) {
        StartCoroutine(this.StartTransfer(address, port));
        return true;
    }

    private IEnumerator StartTransfer(string address, ushort port) {
        yield return null;
        CrossSceneState.ServerTransferData.address = address;
        CrossSceneState.ServerTransferData.port = port;

        LuauCore.ResetContext(LuauContext.Game);
        LuauCore.ResetContext(LuauContext.Protected);

        if (NetworkClient.isConnected || NetworkClient.isConnecting) {
            NetworkManager.singleton.StopClient();
        }

        if (SceneManager.GetSceneByName("CoreScene").isLoaded) {
            var unload = SceneManager.UnloadSceneAsync("CoreScene");
            yield return new WaitUntil(() => unload.isDone);
        }
        
        var loadReq = SceneManager.LoadSceneAsync("CoreScene", LoadSceneMode.Single);
        yield return new WaitUntil(() => loadReq.isDone);
    }

    public void KickClient(int connectionId, string kickMessage) {
        var conn = NetworkServer.connections[connectionId];
        conn.Send(new KickMessage() {
            reason = kickMessage,
        });
        conn.Disconnect();
    }

    public void Disconnect(bool kicked, string kickMessage) {
        this.Disconnect(kicked, kickMessage);
    }

    public void Disconnect() {
        var clientNetworkConnector = FindObjectOfType<ClientNetworkConnector>();
        if (clientNetworkConnector) {
            clientNetworkConnector.expectingDisconnect = true;
        }

        StartCoroutine(this.StartDisconnect());
    }

    private IEnumerator StartDisconnect(bool kicked = false, string kickMessage = "") {
        yield return null;
        LuauCore.ResetContext(LuauContext.Game);
        LuauCore.ResetContext(LuauContext.Protected);
        
        ResetClientUnityState();

        NetworkClient.Disconnect();

        CrossSceneState.disconnectKicked = kicked;
        if (kicked) {
            CrossSceneState.kickMessage = kickMessage;
            SceneManager.LoadScene("Disconnected");
        } else {
            SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        }
    }

    /// <summary>
    /// Reset global client properties back to the Unity defaults
    /// </summary>
    private void ResetClientUnityState() {
        Time.timeScale = 1; // Reset time scale
        
        Physics.reuseCollisionCallbacks = true;
        Physics2D.reuseCollisionCallbacks = true;
    }
}