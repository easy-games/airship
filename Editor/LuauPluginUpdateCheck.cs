using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LuauPluginUpdateCheck {
    private const string BasePath = "Packages/gg.easy.airship/Runtime/Plugins";
    private const string SessionKeyWriteTime = "LuauPluginWriteTime";
    private const string SessionKeyHash = "LuauPluginHash";
    
#if UNITY_EDITOR_WIN
    private const string PluginPath = BasePath + "/Windows/x64/LuauPlugin.dll";
#elif UNITY_EDITOR_OSX
    private const string PluginPath = BasePath + "/Mac/LuauPlugin.bundle/Contents/MacOS/LuauPlugin";
#endif
    
#if UNITY_EDITOR_WIN || UNITY_EDITOR_OSX
    [InitializeOnLoadMethod]
    private static void SetLuauTimeout() {
        LuauPlugin.LuauSetScriptTimeoutDuration(EditorIntegrationsConfig.instance.luauScriptTimeout);
    }
    
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void CheckHash() {
        if (Application.isPlaying || !EditorIntegrationsConfig.instance.promptIfLuauPluginChanged) {
            return;
        }
        
        if (!File.Exists(PluginPath)) {
            Debug.LogWarning($"Could not compare Luau plugin hash; plugin at path not found: {PluginPath}");
            return;
        }

        // Check last write time:
        var pluginLastWriteTime = File.GetLastWriteTime(PluginPath).ToString("O");
        var lastWriteTime = SessionState.GetString(SessionKeyWriteTime, "");
        if (pluginLastWriteTime == lastWriteTime) return;
        
        SessionState.SetString(SessionKeyWriteTime, pluginLastWriteTime);
        
        // Create MD5 hash of the plugin:
        var pluginBytes = File.ReadAllBytes(PluginPath);
        using var md5 = MD5.Create();
        md5.Initialize();
        md5.ComputeHash(pluginBytes);
        var hash = Convert.ToBase64String(md5.Hash);

        // Get the last hash saved in session state, if any:
        var lastHash = SessionState.GetString(SessionKeyHash, "");
        if (hash == lastHash) return;
        
        SessionState.SetString(SessionKeyHash, hash);
        
        // Hash didn't match and last hash in session state isn't empty, so the plugin file changed: 
        if (lastHash != "") {
            // Check if user wants to restart
            var acceptsRestart = EditorUtility.DisplayDialog("Luau Plugin Updated",
                "The Luau plugin has updated. Restart Unity to apply changes. We're sorry for this..", "Quit", "Cancel");
            if (acceptsRestart) {
                // Verify any unsaved changes are saved
                var confirmedSaveState = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                if (confirmedSaveState) {
                    EditorApplication.Exit(0);
                }
            }
        }
    }
#endif
}
