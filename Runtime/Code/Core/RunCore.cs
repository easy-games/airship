
using System.Linq;
using FishNet;
using UnityEngine;
#if UNITY_EDITOR
using ParrelSync;
using Unity.Multiplayer.Playmode;
#endif
[LuauAPI]
public class RunCore {
#if UNITY_EDITOR
    private static readonly bool isServer = CurrentPlayer.ReadOnlyTags().Contains("Server") || ClonesManager.GetArgument() == "server";
    private static readonly bool isClone = CurrentPlayer.ReadOnlyTags().Count > 0 || ClonesManager.IsClone();
    // private static readonly bool isServer = true;
#elif UNITY_SERVER
    private static readonly bool isServer = true;
    private static readonly bool isClone = false;
#else
    private static readonly bool isServer = false;
    private static readonly bool isClone = false;
#endif
    
    public static bool IsServer() {
        return true;
        // return InstanceFinder.IsHost;
        // return isServer;
    }

    public static bool IsClient() {
        return true;
        // return !isServer;
    }

    public static bool IsEditor()
    {
        return Application.isEditor;
    }

    public static bool IsClone() {
        return isClone;
    }
}
