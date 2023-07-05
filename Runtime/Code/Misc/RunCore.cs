
using UnityEngine;
#if UNITY_EDITOR
using ParrelSync;
#endif

[LuauAPI]
public class RunCore {
#if UNITY_EDITOR
    private static readonly bool isServer = ClonesManager.IsClone() && ClonesManager.GetArgument() != "client";
#elif UNITY_SERVER
    private static readonly bool isServer = true;
#else
    private static readonly bool isServer = false;
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
}
