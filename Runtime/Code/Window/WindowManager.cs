using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
 
#if !UNITY_IOS

[DisallowMultipleComponent]
public class WindowManager : MonoBehaviour {
    [DllImport("libTitlebarLib")]
    private static extern int ShowTitleBar();
 
    [DllImport("libTitlebarLib")]
    private static extern int HideTitleBar();

    private bool isFullScreen = false;

    private void Awake() {
        // Only hide titlebar on mac
        if (Application.platform != RuntimePlatform.OSXPlayer) {
            enabled = false;
            return;
        }
        
        isFullScreen = Screen.fullScreen;
#if !UNITY_EDITOR
        ProcessFullScreenUpdate();
#endif
    }

    private IEnumerator ShowWindow() {
        // Needs to wait for Unity to snap into full screen..
        yield return new WaitForSecondsRealtime(0);
        if (!Screen.fullScreen) yield break;
        ShowTitleBar();
    }

    private void Update() {
        // If full screen mode changes update whether titlebar is shown
        if (isFullScreen != Screen.fullScreen) {
            isFullScreen = !isFullScreen;
            ProcessFullScreenUpdate();
        }
    }

    private void ProcessFullScreenUpdate() {
        if (isFullScreen) StartCoroutine(ShowWindow());
        else HideTitleBar();
    }
}

#endif