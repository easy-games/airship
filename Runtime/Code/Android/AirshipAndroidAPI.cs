using System;
using UnityEngine;

public static class AirshipAndroidAPI {
    private const string WINDOW_METHOD_ADD_FLAGS = "addFlags";
    private const string WINDOW_METHOD_CLEAR_FLAGS = "clearFlags";

    private const string UNITY_PLAYER_CLASS = "com.unity3d.player.UnityPlayer";
    private const string AIRSHIP_ANDROID_PLAYER_CLASS = "gg.easy.airship.AirshipAndroidPlayer";

    public enum AndroidPlayerContext {
        [Obsolete]
        Startup,
        Menu,
        Game,
    }
    
    internal class AndroidPluginContext {
        private AndroidJavaObject _androidPlayer;

        internal AndroidPluginContext() {
            _androidPlayer = new AndroidJavaClass(AIRSHIP_ANDROID_PLAYER_CLASS);
        }

        private AndroidJavaObject androidPlayer {
            get {
                if (_androidPlayer == null) _androidPlayer = new AndroidJavaClass(AIRSHIP_ANDROID_PLAYER_CLASS);
                return _androidPlayer;
            }
        }
        
        public void SetContext(AndroidPlayerContext context) {
            switch (context) {
                case AndroidPlayerContext.Game: {
                    Screen.fullScreen = true;
                    break;
                }
                case AndroidPlayerContext.Menu: {
                    Screen.fullScreen = false;
                    break;
                }
            }
        }

        public void ShowToast(string message) {
            androidPlayer.Call("showToast", message);
        }

        private static uint ToARGB(Color color) {
            Color32 c = color;
            byte[] b = { c.b, c.g, c.r, c.a };
            return BitConverter.ToUInt32(b, 0);
        }

        public void SetThemeColor(Color navigationColor, Color statusbarColor) {
            SetThemeColor(ToARGB(navigationColor), ToARGB(statusbarColor));
        }

        private void SetThemeColor(uint navigationColorARGB, uint statusbarColorARGB) {
            androidPlayer.Call("setThemeColor", navigationColorARGB, statusbarColorARGB);
        }
    }

    private static AndroidPluginContext _pluginPluginContext;

    internal static AndroidPluginContext Plugin {
        get {
#if UNITY_ANDROID
            if (_pluginPluginContext == null) _pluginPluginContext = new AndroidPluginContext();
#endif
            return _pluginPluginContext;
        }
    }
}
