using Assets.Luau;
using UnityEngine;

[LuauAPI(LuauContext.Protected)]
public class ServerTransferData
{
    public string address;
    public ushort port;
    public string gameId;
    public string loadingImageUrl;
    public BinaryBlob lastTransferData;
}

[LuauAPI(LuauContext.Protected)]
public static class CrossSceneState
{
    /// <summary>
    /// Individual properties are updated within this by TS.
    /// Do not reassign the object reference to something new.
    /// </summary>
    public static ServerTransferData ServerTransferData;
    public static bool UseLocalBundles = false;
    public static string kickMessage = "";
    public static bool kickForceLogout = false;
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