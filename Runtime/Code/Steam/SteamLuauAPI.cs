using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
#if STEAMWORKS_NET
using Steamworks;
#endif
using UnityEngine;


[LuauAPI(LuauContext.Protected)]
public class SteamLuauAPI : Singleton<SteamLuauAPI> {
    private static List<(object, object)> joinPacketQueue = new();
    public static event Action<object, object> OnRichPresenceGameJoinRequest;
    
    private static int k_cchMaxRichPresenceValueLength = 256;
    private static bool initialized = false;

    public string steamToken = "";
    public bool steamTokenLoaded = false;

#if STEAMWORKS_NET
    private static Callback<GameRichPresenceJoinRequested_t> gameRichPresenceJoinRequested;
#endif

#if STEAMWORKS_NET
    private void Awake() {
        this.gameObject.hideFlags = HideFlags.None;
        GameObject.DontDestroyOnLoad(this);
        if (!SteamManager.Initialized) return;
        
        // Don't initialized multiple times
        if (SteamLuauAPI.initialized) return;
        SteamLuauAPI.initialized = true;

        // Check for launch by "Join game"
        // This might cause weird data to be processed. Seems like commandLineStr is of the format:
        // {"serverId":"c97976f8-e57d-43ac-90c8-9f0058abd094","gameId":"6536ee084c9987573c3a3c03"}
        SteamApps.GetLaunchCommandLine(out var commandLineStr, 260);
        if (commandLineStr.Length > 0) {
            joinPacketQueue.Add((commandLineStr, null));
        }
        
        gameRichPresenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnGameRichPresenceRequest);

        Callback<GetTicketForWebApiResponse_t>.Create(OnGetTicketForWebApiResponse);
        SteamUser.GetAuthTicketForWebApi("airship");
    }
#endif

    /** Returns true if status was updated. Sets rich presence to "{Game Name} - {status}" */
    public static bool SetGameRichPresence(string gameName, string status) {
#if STEAMWORKS_NET
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
#else
        return true;
#endif
    }
    
    /** Directly set rich presence tag (this is a specific value used by Steamworks) */
    public static bool SetRichPresence(string key, string tag) {
#if STEAMWORKS_NET
        if (!SteamManager.Initialized) return false;
        SteamFriends.SetRichPresence(key, tag);
#endif
        return true;
    }

    public static void ProcessPendingJoinRequests() {
        foreach (var (connectData, steamId) in joinPacketQueue) {
            OnRichPresenceGameJoinRequest?.Invoke(connectData, steamId);
        }
        joinPacketQueue.Clear();
    }

#if STEAMWORKS_NET
    private void OnGameRichPresenceRequest(GameRichPresenceJoinRequested_t data) {
        Debug.Log("[Steam Join] Rich presence request");
        if (OnRichPresenceGameJoinRequest == null || OnRichPresenceGameJoinRequest.GetInvocationList().Length == 0) {
            Debug.Log("[Steam Join] Queue join request");
            joinPacketQueue.Add((data.m_rgchConnect, data.m_steamIDFriend.m_SteamID));
            return;
        }
        OnRichPresenceGameJoinRequest.Invoke(data.m_rgchConnect, data.m_steamIDFriend.m_SteamID);
    }
    
    private void OnGetTicketForWebApiResponse(GetTicketForWebApiResponse_t data) {
        if (data.m_eResult != EResult.k_EResultOK) {
            Debug.LogError("[Steam] Failed to get auth ticket. Error: " + data.m_eResult);
            return;
        }
        
        // Convert auth token to hex string
        StringBuilder hexString = new StringBuilder(data.m_cubTicket * 2);
        foreach (var b in data.m_rgubTicket) {
            hexString.AppendFormat("{0:x2}", b);
        }

        var hexTicket = hexString.ToString();
        this.steamToken = hexTicket;
        this.steamTokenLoaded = true;
    }
#endif

    public async Task<string> GetSteamTokenAsync() {
        while (!this.steamTokenLoaded) {
            await Awaitable.NextFrameAsync();
        }
        return this.steamToken;
    }
}