using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class LuauPluginUpdateCheck {
    private const string BasePath = "Packages/gg.easy.airship/Runtime/Plugins";
    private const string SessionKey = "LuauPluginHash";
    
#if UNITY_EDITOR_WIN
    private const string PluginPath = BasePath + "/Windows/x64/LuauPlugin.dll";
#elif UNITY_EDITOR_OSX
    private const string PluginPath = BasePath + "/Mac/LuauPlugin.bundle/Contents/MacOS/LuauPlugin";
#endif
    
#if UNITY_EDITOR_WIN || UNITY_EDITOR_OSX
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void CheckHash() {
        if (!EditorIntegrationsConfig.instance.promptIfLuauPluginChanged) {
            return;
        }
        
        if (!File.Exists(PluginPath)) {
            Debug.LogWarning($"Could not compare Luau plugin hash; plugin at path not found: {PluginPath}");
            return;
        }
        
        var pluginBytes = File.ReadAllBytes(PluginPath);
        
        // Create MD5 hash of the plugin:
        using var md5 = MD5.Create();
        md5.Initialize();
        md5.ComputeHash(pluginBytes);
        var hash = Convert.ToBase64String(md5.Hash);

        // Get the last hash saved in session state, if any:
        var lastHash = SessionState.GetString(SessionKey, "");
        if (hash == lastHash) return;
        
        SessionState.SetString(SessionKey, hash);
        
        // Hash didn't match and last hash in session state isn't empty, so the plugin file changed: 
        if (lastHash != "") {
            EditorUtility.DisplayDialog("Luau Plugin Updated",
                "The Luau plugin has updated. Restart Unity to apply changes.", "Ok");
        }
    }
#endif
}
