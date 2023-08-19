using UnityEngine.SceneManagement;

[LuauAPI]
public class TransferManager {
    public static bool ConnectToServer(string address, ushort port) {
        CrossSceneState.ServerTransferData.address = address;
        CrossSceneState.ServerTransferData.port = port;
        SceneManager.LoadScene("CoreScene");
        return true;
    }
}