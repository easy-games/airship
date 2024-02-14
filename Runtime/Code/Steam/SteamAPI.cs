using Steamworks;
using UnityEngine;

public class SteamAPI : Singleton<SteamAPI> {
    private static int k_cchMaxRichPresenceValueLength = 256;
    private bool steamInitialized = false;
    
    private void Awake() {
#if !UNITY_EDITOR
        StartupSteamClient();
#endif
    }

    private void StartupSteamClient() {
        try {
            Steamworks.SteamClient.Init(2381730, true);
            steamInitialized = true;
            Debug.Log("Steam client initialized");
        }
        catch (System.Exception e) {
            Debug.LogError("Steamworks init failed: " + e);
        }
    }

    /** Returns true if status was updated. Sets rich presence to "{Game Name} - {status}" */
    public static bool SetRichPresence(string gameName, string status) {
        if (!Instance.steamInitialized) return false;

        var display = $"{gameName}";
        if (status.Length > 0) display = $"{display} - ${status}";
        
        // Crop display to max length (defined by Steam)
        if (display.Length > k_cchMaxRichPresenceValueLength) display = display.Substring(0, k_cchMaxRichPresenceValueLength);
        SteamFriends.SetRichPresence("steam_display", display);
        
        return true;
    }
    
    /** Returns true if status was updated */
    // public static bool ClearRichPresence(string status) {
    //     if (!Instance.steamInitialized) return false;
    //
    //     var gameName = "BedWars";
    //     SteamFriends.SetRichPresence($"{gameName} - ${status}");
    //     
    //     return true;
    // }
}