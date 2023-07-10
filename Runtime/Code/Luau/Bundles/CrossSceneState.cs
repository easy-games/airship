using UnityEngine;

public class ServerTransferData
{
    public string address;
    public ushort port;
}

public static class CrossSceneState
{
    public static string Username;
    public static ServerTransferData ServerTransferData;
    public static bool UseLocalBundles = false;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnStartup()
    {
        Username = "Player";
        ServerTransferData = new ServerTransferData()
        {
            address = "localhost",
            port = 7770,
        };
        UseLocalBundles = false;
    }

    public static bool IsLocalServer()
    {
        return ServerTransferData.address == "localhost";
    }
}