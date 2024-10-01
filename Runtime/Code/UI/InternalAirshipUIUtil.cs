using System;
using UnityEngine;

namespace Code.UI {
    public class InternalAirshipUtil : MonoBehaviour {
        private static bool didWindowResize = false;

        public static void HandleWindowSize() {
            if (didWindowResize) return;
            didWindowResize = true;
            if (Application.platform == RuntimePlatform.WindowsPlayer) {
                // In Unity 6 this should work.
                // Thread: https://discussions.unity.com/t/maximized-window-mode-launches-in-fullscreen/770423/32
                
                // Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
            }
        }
    }
}