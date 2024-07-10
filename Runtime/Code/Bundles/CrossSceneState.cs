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
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnStartup() {
        ushort port = 7770;
        #if UNITY_EDITOR
        port = AirshipEditorNetworkConfig.instance.portOverride;
        #endif
        ServerTransferData = new ServerTransferData()
        {
            address = "127.0.0.1",
            port = port,
        };
        UseLocalBundles = false;
    }

    public static bool IsLocalServer()
    {
        return ServerTransferData.address == "127.0.0.1";
    }
}