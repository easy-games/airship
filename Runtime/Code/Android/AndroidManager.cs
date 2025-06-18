
    using System;
    using UnityEngine;

    public class AndroidManager : MonoBehaviour {
        public bool fullscreen;
        
        private void Start() {
#if UNITY_ANDROID
            AirshipAndroidAPI.SetFullscreen(fullscreen);
#endif
        }
    }
