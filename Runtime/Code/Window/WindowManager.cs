using System;
using System.Runtime.InteropServices;
using UnityEngine;
 
[DisallowMultipleComponent]
public class WindowManager : MonoBehaviour {
    [DllImport("libTitlebarLib")]
    private static extern int ShowTitleBar();
 
    [DllImport("libTitlebarLib")]
    private static extern int HideTitleBar();

    private void Awake() {
#if !UNITY_EDITOR
        OnHideTitleBar();
#endif
    }

    public void OnShowTitleBar() {
        int ret = ShowTitleBar();
    }
 
    public void OnHideTitleBar() {
        Debug.Log("[WindowManager] Hiding the title bar");
        int ret = HideTitleBar();
    }
}