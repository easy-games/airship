using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class ManageFullscreenSwitch : MonoBehaviour
{
    private int _fullscreenWidth = 0;
    private int _fullscreenHeight = 0;
    private int _fullscreenAspectRatio = 0;

    private bool _fullscreen = false;

    private void Start() {
#if !UNITY_IOS && !UNITY_ANDROID
        _fullscreen = Screen.fullScreen;
        SetFullScreenValues();
#endif
    }
    private void Update() {
#if !UNITY_IOS && !UNITY_ANDROID
        if (_fullscreen != Screen.fullScreen) {
            if (Screen.fullScreen) {
                RestoreFullscreenResolution();
            }

            _fullscreen = Screen.fullScreen;
        }
#endif
    }

    private void RestoreFullscreenResolution() {
        SetFullScreenValues();
        Screen.SetResolution(_fullscreenWidth, _fullscreenHeight, true, _fullscreenAspectRatio);
    }

    private void SetFullScreenValues()
    {
        // Set the screen width and height
        int systemWidth = Display.main.systemWidth;
        int systemHeight = Display.main.systemHeight;

        // Get a list of all supported resolutions
        Resolution[] supportedResolutions = Screen.resolutions;

        // Find the closest supported resolution to the native resolution
        Resolution closestResolution = supportedResolutions[0];
        int smallestGapInResolution = int.MaxValue;

        foreach (Resolution resolution in supportedResolutions)
        {
            int gap = Mathf.Abs(resolution.width - systemWidth) + Mathf.Abs(resolution.height - systemHeight);

            if (gap < smallestGapInResolution)
            {
                smallestGapInResolution = gap;
                closestResolution = resolution;
            }
        }

        _fullscreenWidth = closestResolution.width;
        _fullscreenHeight = closestResolution.height;
        _fullscreenAspectRatio = _fullscreenWidth / _fullscreenHeight;
    }
}