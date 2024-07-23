using System;
using System.Collections;
using Airship.DevConsole;
using FishNet;
using Luau;
using UnityEngine;
using UnityEngine.SceneManagement;

[LuauAPI]
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

        if (InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Connection.IsActive) {
            InstanceFinder.ClientManager.Connection.Disconnect(true);
        }

        // if (SceneManager.GetSceneByName("CoreScene").isLoaded) {
        //     var unload = SceneManager.UnloadSceneAsync("CoreScene");
        //     yield return new WaitUntil(() => unload.isDone);
        // }
        
        var loadReq = SceneManager.LoadSceneAsync("CoreScene", LoadSceneMode.Single);
        yield return new WaitUntil(() => loadReq.isDone);
    }

    public void Disconnect() {
        var clientNetworkConnector = FindObjectOfType<ClientNetworkConnector>();
        if (clientNetworkConnector) {
            clientNetworkConnector.expectingDisconnect = true;
        }

        StartCoroutine(this.StartDisconnect());
    }

    private IEnumerator StartDisconnect() {
        Debug.Log("Disconnect.1");
        yield return null;
        LuauCore.ResetContext(LuauContext.Game);
        Debug.Log("Disconnect.2");
        LuauCore.ResetContext(LuauContext.Protected);
        Debug.Log("Disconnect.3");

        if (InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Connection.IsActive) {
            InstanceFinder.ClientManager.Connection.Disconnect(true);
        }
        Debug.Log("Disconnect.4");

        SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        Debug.Log("Disconnect.5");
    }
}