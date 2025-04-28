using System.Runtime.InteropServices;
using System.IO;
using UnityEngine;

public static class CrossPlatformFileAPI {
#if UNITY_STANDALONE_WIN
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ShellExecuteW(
        System.IntPtr hwnd,
        string lpOperation,
        string lpFile,
        string lpParameters,
        string lpDirectory,
        int nShowCmd
    );
#endif

#if UNITY_STANDALONE_OSX
    [DllImport("libc")]
    private static extern int system(string command);
#endif

    public static void OpenPath(string path) {
        if (!Directory.Exists(path) && !File.Exists(path)) {
            Debug.LogWarning($"Directory does not exist: {path}");
            return;
        }

#if UNITY_STANDALONE_WIN
        // Use ShellExecute (works in IL2CPP too)
        ShellExecuteW(System.IntPtr.Zero, "open", path, null, null, 1);

#elif UNITY_STANDALONE_OSX
        string openCommand = $"open \"{path}\"";
        system(openCommand);

#else
        Debug.LogWarning("OpenFolder is not supported on this platform.");
#endif
    }
}