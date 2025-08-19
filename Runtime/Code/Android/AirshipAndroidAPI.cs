using System;
using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public static class AirshipAndroidAPI {
    private const string WINDOW_METHOD_ADD_FLAGS = "addFlags";
    private const string WINDOW_METHOD_CLEAR_FLAGS = "clearFlags";

    private const string UNITY_PLAYER_CLASS = "com.unity3d.player.UnityPlayer";
    private const string AIRSHIP_ANDROID_PLAYER_CLASS = "gg.easy.airship.AirshipAndroidPlayer";

    public enum AndroidPlayerContext {
        Init,
        Menu,
        Game,
    }
    
    internal class AndroidPluginContext {
        private AndroidJavaObject _androidPlayer;

        internal AndroidPluginContext() {
            _androidPlayer = new AndroidJavaClass(AIRSHIP_ANDROID_PLAYER_CLASS);
        }

        private AndroidJavaObject currentActivity {
            get {
#if UNITY_ANDROID
                return AndroidApplication.currentActivity;
#else
                return null;
#endif
            }
        }
        
        public void SetContext(AndroidPlayerContext context) {
            
            
            switch (context) {
                case AndroidPlayerContext.Game: {
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    break;
                }
                case AndroidPlayerContext.Menu: {
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                    break;
                }
            }

            var contextInteger = (int) context;
            currentActivity.Call("setContext", contextInteger);
        }

        public void ShowToast(string message) {
            currentActivity.Call("showToast", message);
        }

        // private static uint ToARGB(Color color) {
        //     Color32 c = color;
        //     byte[] b = { c.b, c.g, c.r, c.a };
        //     return BitConverter.ToUInt32(b, 0);
        // }

        // public void SetThemeColor(Color navigationColor, Color statusbarColor) {
        //     SetThemeColor(ToARGB(navigationColor), ToARGB(statusbarColor));
        // }
        //
        // private void SetThemeColor(uint navigationColorARGB, uint statusbarColorARGB) {
        //     currentActivity.Call("setThemeColor", navigationColorARGB, statusbarColorARGB);
        // }
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
