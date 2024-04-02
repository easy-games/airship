using System.Runtime.InteropServices;
using UnityEngine;

public class MouseUtil {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    /// <summary>
    /// Represents a Windows POINT struct.
    /// <a href="https://learn.microsoft.com/en-us/windows/win32/api/windef/ns-windef-point">Windows Docs</a>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsPoint {
        private int x;
        private int y;

        public static implicit operator Vector2Int(WindowsPoint point) {
            return new Vector2Int(point.x, point.y);
        }
    }
    
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out WindowsPoint lpPoint);
#endif
    
    /// <summary>
    /// Get the native mouse position.
    /// </summary>
    public static Vector2Int GetNativeMousePosition() {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        GetCursorPos(out var point);
        return point;
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        // TODO
#endif
        return Vector2Int.zero;
    }

    /// <summary>
    /// Set the native mouse position.
    /// </summary>
    public static void SetNativeMousePosition(Vector2Int position) {
        SetNativeMousePosition(position.x, position.y);
    }

    /// <summary>
    /// Set the native mouse position.
    /// </summary>
    public static void SetNativeMousePosition(Vector2 position) {
        SetNativeMousePosition((int)position.x, (int)position.y);
    }

    /// <summary>
    /// Set the native mouse position.
    /// </summary>
    public static void SetNativeMousePosition(int x, int y) {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        SetCursorPos(x, y);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        // TODO
#endif
    }
}
