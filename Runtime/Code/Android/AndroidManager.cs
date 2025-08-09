using System;
using UnityEngine;
using UnityEngine.Serialization;

public class AndroidManager : MonoBehaviour {
    public AirshipAndroidAPI.AndroidPlayerContext context;

#if UNITY_ANDROID
    private void Start() {
        AirshipAndroidAPI.Plugin.SetContext(context);
    }
#endif
}
