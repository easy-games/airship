
using System.Linq;
using FishNet;
using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
using ParrelSync;
using Unity.Multiplayer.Playmode;
#endif
[LuauAPI]
public class RunCore {
    // Launch params
    public static bool launchInDedicatedServerMode = SessionState.GetBool("AirshipDedicatedServerMode", false);

    private static bool isServer = false;
    private static bool isClient = false;
    private static bool isClone = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void OnLoad() {
        launchInDedicatedServerMode = SessionState.GetBool("AirshipDedicatedServerMode", false);
#if UNITY_EDITOR && !AIRSHIP_PLAYER
        isClone = CurrentPlayer.ReadOnlyTags().Count > 0 || ClonesManager.IsClone();
        if (launchInDedicatedServerMode || isClone) {
            isServer = CurrentPlayer.ReadOnlyTags().Contains("Server") || ClonesManager.GetArgument() == "server";
            isClient = !isServer;
        } else {
            isServer = true;
            isClient = true;
        }
#elif UNITY_SERVER
        isServer = true;
        isClient = false;
        isClone = false;
#else
        isServer = false;
        isClient = true;
        isClone = false;
#endif
    }

    public static bool IsServer() {
        return isServer;
    }

    public static bool IsClient() {
        return isClient;
    }

    public static bool IsEditor()
    {
        return Application.isEditor;
    }

    public static bool IsClone() {
        return isClone;
    }
}
