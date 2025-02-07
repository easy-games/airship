
using System;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using ParrelSync;
using Unity.Multiplayer.Playmode;
#endif
[LuauAPI]
public class RunCore {
    // Launch params
    public static bool launchInDedicatedServerMode = true;

    private static bool isServer = false;
    private static bool isClient = false;
    private static bool isClone = false;
    private static bool isInteral = false;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void OnLoad() {
#if UNITY_EDITOR
        launchInDedicatedServerMode = EditorPrefs.GetBool("AirshipDedicatedServerMode", false);
#endif
#if UNITY_EDITOR && !AIRSHIP_PLAYER
        isClone = ClonesManager.IsClone() || IsMPPMClone();
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

#if AIRSHIP_INTERNAL
        isInteral = true;
#endif
    }

    public static bool IsServer() {
        return isServer;
    }

    public static bool IsClient() {
        return isClient;
    }

    public static bool IsEditor() {
        return Application.isEditor;
    }

    public static bool IsInternal() {
        return isInteral;
    }

    public static bool IsClone() {
        return isClone;
    }

    private static bool IsMPPMClone() {
#if !AIRSHIP_PLAYER
        if (CurrentPlayer.IsMainEditor) return false;
#endif
        return Environment.GetCommandLineArgs().Contains("--virtual-project-clone");
    }
}
