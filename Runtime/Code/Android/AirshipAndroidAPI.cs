using UnityEngine;

internal static class AirshipAndroidAPI {
    private const string WINDOW_METHOD_ADD_FLAGS = "addFlags";
    private const string WINDOW_METHOD_CLEAR_FLAGS = "clearFlags";

    /// <summary>
    /// Sets the fullscreen state of the android app
    /// </summary>
    /// <param name="fullscreen">Fullscreen</param>
    public static void SetFullscreen(bool fullscreen) {
        Screen.fullScreen = fullscreen;
        
#if UNITY_ANDROID
        var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            var layoutParamsClass = new AndroidJavaClass("android.view.WindowManager$LayoutParams");
            var windowObject = activity.Call<AndroidJavaObject>("getWindow");
            
            var flagFullscreen = layoutParamsClass.GetStatic<int>("FLAG_FULLSCREEN");
            var flagNotFullscreen = layoutParamsClass.GetStatic<int>("FLAG_FORCE_NOT_FULLSCREEN");
            var flagDrawsSystemBarBackgrounds = layoutParamsClass.GetStatic<int>("FLAG_DRAWS_SYSTEM_BAR_BACKGROUNDS");
            
            if (fullscreen) {
                windowObject.Call(WINDOW_METHOD_CLEAR_FLAGS, flagNotFullscreen);
                windowObject.Call(WINDOW_METHOD_CLEAR_FLAGS, flagDrawsSystemBarBackgrounds);
                windowObject.Call(WINDOW_METHOD_ADD_FLAGS, flagFullscreen);
            }
            else {
                windowObject.Call(WINDOW_METHOD_ADD_FLAGS, flagNotFullscreen);
                windowObject.Call(WINDOW_METHOD_ADD_FLAGS, flagDrawsSystemBarBackgrounds);
                windowObject.Call(WINDOW_METHOD_CLEAR_FLAGS, flagFullscreen);
            }
        }));
#endif
    }
}
