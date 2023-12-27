
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using ParrelSync;
using Unity.Multiplayer.Playmode;
#endif
[LuauAPI]
public class RunCore {
#if UNITY_EDITOR
    private static readonly bool isServer = CurrentPlayer.ReadOnlyTags().Contains("Server");
    private static readonly bool isClone = CurrentPlayer.ReadOnlyTags().Count > 0;
    // private static readonly bool isServer = true;
#elif UNITY_SERVER
    private static readonly bool isServer = true;
    private static readonly bool isClone = false;
#else
    private static readonly bool isServer = false;
    private static readonly bool isClone = false;
#endif
    
    public static bool IsServer() {
        return isServer;
    }

    public static bool IsClient() {
        return !isServer;
    }

    public static bool IsEditor()
    {
        return Application.isEditor;
    }

    public static bool IsClone() {
        return isClone;
    }
}
