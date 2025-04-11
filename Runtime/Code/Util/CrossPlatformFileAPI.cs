using System.Runtime.InteropServices;
using System.IO;
using UnityEngine;

public static class CrossPlatformFileAPI
{
#if UNITY_STANDALONE_OSX
    [DllImport("libc")]
    private static extern int system(string command);
#endif

    public static void OpenPath(string path) {
        if (!Directory.Exists(path) && !File.Exists(path)) {
            Debug.LogWarning("Directory or file doesn't exist: " + path);
            return;
        }

#if UNITY_STANDALONE_WIN
        path = path.Replace("/", "\\");
        System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");

#elif UNITY_STANDALONE_OSX
        // Uses macOS 'open' command via native system call (works in IL2CPP)
        string openCommand = $"open \"{path}\"";
        system(openCommand);
#else
        Debug.Log("Folder open not supported on this platform.");
#endif
    }
}