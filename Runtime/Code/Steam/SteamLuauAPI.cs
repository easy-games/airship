using Steamworks;
using UnityEngine;

[LuauAPI]
public class SteamLuauAPI : Singleton<SteamLuauAPI> {
    private static int k_cchMaxRichPresenceValueLength = 256;
    private bool steamInitialized = false;

    /** Returns true if status was updated. Sets rich presence to "{Game Name} - {status}" */
    public static bool SetGameRichPresence(string gameName, string status) {
        if (!SteamManager.Initialized) return false;
        
        var display = $"{gameName}";
        if (status.Length > 0) display = $"{display} - ${status}";
        
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