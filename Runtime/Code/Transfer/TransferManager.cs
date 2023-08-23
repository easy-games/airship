using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[LuauAPI]
public class TransferManager : Singleton<TransferManager> {
    public bool ConnectToServer(string address, ushort port) {
        StartCoroutine(this.StartTransfer(address, port));
        return true;
    }

    private IEnumerator StartTransfer(string address, ushort port) {
        CrossSceneState.ServerTransferData.address = address;
        CrossSceneState.ServerTransferData.port = port;

        LuauCore.ResetInstance();
        SystemRoot.Instance.UnloadBundles();

        if (SceneManager.GetSceneByName("CoreScene").isLoaded) {
            var unload = SceneManager.UnloadSceneAsync("CoreScene");
            yield return new WaitUntil(() => unload.isDone);
        }

        var loadReq = SceneManager.LoadSceneAsync("CoreScene");
        yield return new WaitUntil(() => loadReq.isDone);
    }
}