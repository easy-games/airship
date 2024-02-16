using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

[LuauAPI]
public class SteamLuauAPI : Singleton<SteamLuauAPI> {
    private static List<(object, object)> joinPacketQueue = new();
    public static event Action<object, object> OnRichPresenceGameJoinRequest = delegate(object a, object b) {
        joinPacketQueue.Add((a, b));
    };
    
    private static int k_cchMaxRichPresenceValueLength = 256;
    private bool steamInitialized = false;
    private Callback<GameRichPresenceJoinRequested_t> gameRichPresenceJoinRequested;

    private void Awake() {
        if (!SteamManager.Initialized) return;

        gameRichPresenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnGameRichPresenceRequest);
    }

    /** Returns true if status was updated. Sets rich presence to "{Game Name} - {status}" */
    public static bool SetGameRichPresence(string gameName, string status) {
        if (!SteamManager.Initialized) return false;
        
        var inEditor = false;
#if UNITY_EDITOR
        inEditor = true;
#endif
        
        var display = $"{gameName}";
        if (inEditor) {
            display = $"{display} (Editor)";
        }
        if (status.Length > 0) display = $"{display} - {status}";
        
        // Crop display to max length (defined by Steam)
        if (display.Length > k_cchMaxRichPresenceValueLength) display = display.Substring(0, k_cchMaxRichPresenceValueLength);
        // "#Status_Custom" and "status" come from our steam localization file -- it is required
        SteamFriends.SetRichPresence("status", display);
        SteamFriends.SetRichPresence("steam_display", "#Status_Custom");
        return true;
    }
    
    /** Directly set rich presence tag (this is a specific value used by Steamworks) */
    public static bool SetRichPresence(string key, string tag) {
        if (!SteamManager.Initialized) return false;
        SteamFriends.SetRichPresence(key, tag);
        return true;
    }

    public static void ProcessPendingJoinRequests() {
        foreach (var (connectData, steamId) in joinPacketQueue) {
            OnRichPresenceGameJoinRequest(connectData, steamId);
        }
        joinPacketQueue.Clear();
    }

    private void OnGameRichPresenceRequest(GameRichPresenceJoinRequested_t data) {
        OnRichPresenceGameJoinRequest(data.m_rgchConnect, data.m_steamIDFriend.m_SteamID);
    }
}