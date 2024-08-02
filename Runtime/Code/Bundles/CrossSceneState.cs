using UnityEngine;

public class ServerTransferData
{
    public string address;
    public ushort port;
}

[LuauAPI]
public static class CrossSceneState
{
    public static ServerTransferData ServerTransferData;
    public static bool UseLocalBundles = false;
    public static string kickMessage = "";
    public static bool disconnectKicked = false;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnStartup() {
        ushort port = 7770;
        #if UNITY_EDITOR
        port = AirshipEditorNetworkConfig.instance.portOverride;
        #endif
        ServerTransferData = new ServerTransferData()
        {
            address = "localhost",
            port = port,
        };
        UseLocalBundles = false;
    }

    public static bool IsLocalServer()
    {
        return ServerTransferData.address == "localhost";
    }
}