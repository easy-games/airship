
using Unity.Multiplayer.Playmode;
using UnityEngine;
#if UNITY_EDITOR
using ParrelSync;
#endif

[LuauAPI]
public class RunCore {
#if UNITY_EDITOR
    private static readonly bool isServer = CurrentPlayer.ReadOnlyTag() == "server" || (ClonesManager.IsClone() && ClonesManager.GetArgument() != "client");
    private static readonly bool isClone = CurrentPlayer.ReadOnlyTag() == "server" || ClonesManager.IsClone();
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
