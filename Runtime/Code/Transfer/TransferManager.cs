using System.Collections;
using FishNet;
using UnityEngine;
using UnityEngine.SceneManagement;

[LuauAPI]
public class TransferManager : Singleton<TransferManager> {
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
}