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
            
            if (fullscreen) {
                windowObject.Call(WINDOW_METHOD_CLEAR_FLAGS, flagNotFullscreen);
                windowObject.Call(WINDOW_METHOD_ADD_FLAGS, flagFullscreen);
            }
            else {
                windowObject.Call(WINDOW_METHOD_ADD_FLAGS, flagNotFullscreen);
                windowObject.Call(WINDOW_METHOD_CLEAR_FLAGS, flagFullscreen);
            }
        }));
#endif
    }

    public static int ToARGB(Color color)
    {
        Color32 c = (Color32)color;
        byte[] b = new byte[] { c.b, c.g, c.r, c.a };
        return System.BitConverter.ToInt32(b, 0);
    }
    
    public static void SetAndroidTheme(Color32 primaryColor, Color32 darkColor, string label = null) {
#if UNITY_ANDROID // && !UNITY_EDITOR
    label = label ?? Application.productName;
    var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
    activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
    {
        var layoutParamsClass = new AndroidJavaClass("android.view.WindowManager$LayoutParams");
        var flagDrawsSystemBarBackgrounds = layoutParamsClass.GetStatic<int>("FLAG_DRAWS_SYSTEM_BAR_BACKGROUNDS");
        var windowObject = activity.Call<AndroidJavaObject>("getWindow");
        windowObject.Call(WINDOW_METHOD_ADD_FLAGS, flagDrawsSystemBarBackgrounds);
        
        var sdkInt = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
        const int lollipop = 21;
        if (sdkInt <= lollipop) return;
        
        windowObject.Call("setStatusBarColor", ToARGB(darkColor));
        var myName = activity.Call<string>("getPackageName");
        var packageManager = activity.Call<AndroidJavaObject>("getPackageManager");
        var drawable = packageManager.Call<AndroidJavaObject>("getApplicationIcon", myName);
        var taskDescription = new AndroidJavaObject("android.app.ActivityManager$TaskDescription", label, drawable.Call<AndroidJavaObject>("getBitmap"), ToARGB(primaryColor));
        activity.Call("setTaskDescription", taskDescription);
    }));
#endif
    }
}
