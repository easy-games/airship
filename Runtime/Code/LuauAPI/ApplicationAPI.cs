using System;
using System.Collections.Generic;
using System.Security;
using UnityEngine;

[LuauAPI(ContextOverrideList = new [] {
    // Protected methods
    "RequestUserAuthorization",
    "RequestAdvertisingIdentifierAsync",
    "CaptureScreenshot",
    "Unload",
    
    // Protected file path members
    "persistentDataPath",
    "dataPath",
    "temporaryCachePath",
    "consoleLogPath",
}, ContextOverrideMask = (int) LuauContext.Protected)]
public class ApplicationAPI : BaseLuaAPIClass {
    private static HashSet<string> ValidUrlHosts = new (StringComparer.OrdinalIgnoreCase) {
        "discord.gg",
        
        // Social
        "youtube.com",
        "youtu.be",
        "x.com",
        "twitter.com",
        "bsky.app",
        "mastodon.social",
        "tiktok.com",
        
        // Easy
        "airship.gg",
        "bedwars.com",
        "easy.gg",
    };
    private static double lastGameOpenedUrlTime = Time.unscaledTime; 
    
    public override Type GetAPIType() {
        return typeof(Application);
    }

    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters,
        Span<int> parameterDataPODTypes, Span<IntPtr> parameterDataPtrs, Span<int> parameterDataSizes) {
        // Only allow opening safe URLs in game access
        if (methodName == "OpenURL" && context != LuauContext.Protected) {
            var urlStr = LuauCore.NewStringFromPointer(parameterDataPtrs[0], parameterDataSizes[0]);
            
            // Add https if not included
            if (!urlStr.Contains("://")) {
                urlStr = $"https://{urlStr}";
            }
            
            if (!Uri.TryCreate(urlStr, UriKind.Absolute, out var uri)) {
                throw new ArgumentException($"[Airship] Invalid URL: {urlStr}");
            }
            if (uri.Scheme != Uri.UriSchemeHttps) throw new SecurityException($"[Airship] Disallowed scheme: {uri.Scheme}. Can only open HTTPS.");

            var host = uri.Host;
            if (host.StartsWith("www.")) host = host.Substring(4);
            if (!ValidUrlHosts.Contains(host)) throw new SecurityException($@"[Airship] URL ""{host}"" is not whitelisted.");
            
            // Make sure game isn't spamming URL requests (max 1 per second)
            // (don't error here, just warn that URL couldn't be opened and continue)
            if ((Time.unscaledTime - lastGameOpenedUrlTime) < 1) {
                Debug.LogWarning("[Airship] OpenURL rate limit exceeded (1 call per second).");
                return 0;
            }
            lastGameOpenedUrlTime = Time.unscaledTime;
            
            Application.OpenURL(urlStr);
            return 0;
        }
        return -1;
    }
}