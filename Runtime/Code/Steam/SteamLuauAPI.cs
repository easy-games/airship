using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
#if STEAMWORKS_NET
using Steamworks;
#endif
using UnityEngine;
using UnityEngine.Assertions;

public struct AirshipSteamFriendInfo {
    public bool playingAirship;
    public bool playingOtherGame;
    public ulong steamId;
    public string steamName;
    public bool online;
}


[LuauAPI(LuauContext.Protected)]
public class SteamLuauAPI : Singleton<SteamLuauAPI> {
    private static List<(object, object)> commandLineQueue = new();
    private static (object, object, object) paramsQueue = (null, null, null);
    public static event Action<object, object> OnRichPresenceGameJoinRequest;
    public static event Action<object, object, object> OnNewLaunchParams;
    
    private static int k_cchMaxRichPresenceValueLength = 256;
    private static bool initialized = false;

    public string steamToken = "";
    public bool steamTokenLoaded = false;

#if STEAMWORKS_NET
    private static Callback<GameRichPresenceJoinRequested_t> gameRichPresenceJoinRequested;
    private static Callback<NewUrlLaunchParameters_t> newUrlLaunchParameters;
#endif

#if STEAMWORKS_NET
    private void Awake() {
        // Don't initialized multiple times
        if (initialized) {
            Destroy(this.gameObject);
            return;
        }
        initialized = true;

        if (!SteamManager.Initialized) {
            return;
        }

        this.gameObject.hideFlags = HideFlags.None;
        DontDestroyOnLoad(this);

        // Check for launch by "Join game"
        // This might cause weird data to be processed. Seems like commandLineStr is of the format:
        // {"serverId":"c97976f8-e57d-43ac-90c8-9f0058abd094","gameId":"6536ee084c9987573c3a3c03"}
        SteamApps.GetLaunchCommandLine(out var commandLineStr, 260);
        if (commandLineStr.Length > 0) {
            commandLineQueue.Add((commandLineStr, null));
        }
        
        OnNewUrlLaunchParameters();
        
        gameRichPresenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnGameRichPresenceRequest);
        newUrlLaunchParameters = Callback<NewUrlLaunchParameters_t>.Create((data) => OnNewUrlLaunchParameters());

        Callback<GetTicketForWebApiResponse_t>.Create(OnGetTicketForWebApiResponse);
        SteamUser.GetAuthTicketForWebApi("airship");
        Debug.Log("Invoked steam auth request.");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnReload() {
        initialized = false;
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

    public static bool IsSteamInitialized() {
        return SteamManager.Initialized;
    }

    public static void ProcessPendingJoinRequests() {
        foreach (var (connectData, steamId) in commandLineQueue) {
            OnRichPresenceGameJoinRequest?.Invoke(connectData, steamId);
        }

        if (paramsQueue.Item1 != null) {
            OnNewLaunchParams?.Invoke(paramsQueue.Item1, paramsQueue.Item2, paramsQueue.Item3);
        }

        commandLineQueue.Clear();
        paramsQueue = (null, null, null);
    }

#if STEAMWORKS_NET
    private void OnGameRichPresenceRequest(GameRichPresenceJoinRequested_t data) {
        Debug.Log("[Steam Join] Rich presence request");
        if (OnRichPresenceGameJoinRequest == null || OnRichPresenceGameJoinRequest.GetInvocationList().Length == 0) {
            Debug.Log("[Steam Join] Queue join request");
            commandLineQueue.Add((data.m_rgchConnect, data.m_steamIDFriend.m_SteamID));
            return;
        }
        OnRichPresenceGameJoinRequest.Invoke(data.m_rgchConnect, data.m_steamIDFriend.m_SteamID);
    }
    
    private void OnNewUrlLaunchParameters() {
        var gameId = SteamApps.GetLaunchQueryParam("gameId");
        var serverId = SteamApps.GetLaunchQueryParam("serverId");
        var customData = SteamApps.GetLaunchQueryParam("custom");
        
        if (OnNewLaunchParams == null || OnNewLaunchParams.GetInvocationList().Length == 0) {
            paramsQueue = (gameId, serverId, customData);
            return;
        }
        
        OnNewLaunchParams.Invoke(gameId, serverId, customData);
    }
    
    private void OnGetTicketForWebApiResponse(GetTicketForWebApiResponse_t data) {
        print("OnGetTicketForWebApiResponse");
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


    public static bool InviteUserToGame(string steamId, string connectString) {
        CSteamID friendSteamID = new CSteamID(ulong.Parse(steamId));
        bool success = SteamFriends.InviteUserToGame(friendSteamID, connectString);
        return success;
    }

        public static AirshipSteamFriendInfo[] GetSteamFriends() {
        Assert.IsTrue(SteamManager.Initialized, "Can't fetch friends: steam is not initialized.");

        #if !STEAMWORKS_NET
            return Array.Empty<AirshipSteamFriendInfo>();
        #else
            var friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            var friendInfos = new AirshipSteamFriendInfo[friendCount];
            for (var i = 0; i < friendCount; i++) {
                var friendId = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                var friendName = SteamFriends.GetFriendPersonaName(friendId);
                var personaState = SteamFriends.GetFriendPersonaState(friendId);
                SteamFriends.GetFriendGamePlayed(friendId, out var friendGameInfo);

                var friendInfoStruct = new AirshipSteamFriendInfo {
                    steamId = friendId.m_SteamID,
                    steamName = friendName
                };

                // Is friend playing Airship?
                if (friendGameInfo.m_gameID.m_GameID == 2381730) {
                    friendInfoStruct.playingAirship = true;
                } else if (friendGameInfo.m_gameID.IsValid()) {
                    friendInfoStruct.playingOtherGame = true;
                }

                // Check if online
                if (personaState != EPersonaState.k_EPersonaStateOffline) {
                    friendInfoStruct.online = true;
                }

                friendInfos[i] = friendInfoStruct;
            }
            return friendInfos;
        #endif
    }

    /// <summary>
    /// Can return null.
    /// </summary>
    /// <param name="steamId"></param>
    /// <returns></returns>
    public static async Task<Texture2D> GetSteamProfilePictureYielding(string steamId) {
        CSteamID friendSteamID = new CSteamID(ulong.Parse(steamId));
        int avatarInt = SteamFriends.GetLargeFriendAvatar(friendSteamID);
        while (avatarInt == -1) {
            await Awaitable.NextFrameAsync();
            avatarInt = SteamFriends.GetLargeFriendAvatar(friendSteamID);
        }

        if (SteamUtils.GetImageSize(avatarInt, out uint width, out uint height)) {
            byte[] image = new byte[width * height * 4];
            if (SteamUtils.GetImageRGBA(avatarInt, image, image.Length)) {
                Texture2D tex = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(image);
                tex.Apply();
                FlipTextureVertically(tex);
                return tex;
            } else {
                Debug.LogWarning("Failed to get avatar RGBA data.");
            }
        } else {
            Debug.LogWarning("Failed to get avatar or image size.");
        }

        return null;
    }
#endif

    public async Task<string> GetSteamTokenAsync() {
        while (!this.steamTokenLoaded) {
            await Awaitable.NextFrameAsync();
        }
        return this.steamToken;
    }

    private static void FlipTextureVertically(Texture2D texture) {
        int width = texture.width;
        int height = texture.height;

        Color[] pixels = texture.GetPixels();

        for (int y = 0; y < height / 2; y++) {
            int topRow = y * width;
            int bottomRow = (height - 1 - y) * width;

            for (int x = 0; x < width; x++) {
                Color temp = pixels[topRow + x];
                pixels[topRow + x] = pixels[bottomRow + x];
                pixels[bottomRow + x] = temp;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
    }
}