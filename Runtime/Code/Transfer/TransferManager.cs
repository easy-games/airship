using System;
using System.Collections;
using FishNet;
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

        LuauCore.ResetInstance();

        if (InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Connection.IsActive) {
            InstanceFinder.ClientManager.Connection.Disconnect(true);
        }

        SystemRoot.Instance.UnloadBundles();

        if (SceneManager.GetSceneByName("CoreScene").isLoaded) {
            var unload = SceneManager.UnloadSceneAsync("CoreScene");
            yield return new WaitUntil(() => unload.isDone);
        }

        var loadReq = SceneManager.LoadSceneAsync("CoreScene");
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
        yield return null;
        LuauCore.ResetInstance();

        if (InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Connection.IsActive) {
            InstanceFinder.ClientManager.Connection.Disconnect(true);
        }

        SystemRoot.Instance.UnloadBundles();
        SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
    }
}