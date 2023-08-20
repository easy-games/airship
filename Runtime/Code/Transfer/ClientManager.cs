using System.Collections;
using UnityEngine.SceneManagement;

[LuauAPI]
public class TransferManager : Singleton<TransferManager> {
    public bool ConnectToServer(string address, ushort port) {
        StartCoroutine(address, port);
        return true;
    }

    private IEnumerator StartTransfer(string address, ushort port) {
        CrossSceneState.ServerTransferData.address = address;
        CrossSceneState.ServerTransferData.port = port;
        SceneManager.LoadScene("CoreScene");
        yield break;
    }
}