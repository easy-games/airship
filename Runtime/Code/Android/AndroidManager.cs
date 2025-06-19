
    using System;
    using UnityEngine;

    public class AndroidManager : MonoBehaviour {
        public bool fullscreen;
        public Color32 primaryColor;
        public Color32 darkColor;
        
        private void Start() {
#if UNITY_ANDROID
            AirshipAndroidAPI.SetFullscreen(fullscreen);
            if (!fullscreen) {
                AirshipAndroidAPI.SetAndroidTheme(primaryColor, darkColor);
            }
#endif
        }
    }
