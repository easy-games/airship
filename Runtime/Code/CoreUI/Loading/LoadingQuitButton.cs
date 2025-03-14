using System;
using UnityEngine;

public class LoadingQuitButton : MonoBehaviour {
    public Transform visuals;

    private void Awake() {
#if UNITY_IOS || UNITY_ANDROID
        this.gameObject.SetActive(false);
#endif
    }

    public void Button_OnClick() {
        Application.Quit();
    }

    private void Update() {
        if (Screen.fullScreen) {
            this.visuals.gameObject.SetActive(true);
        } else {
            this.visuals.gameObject.SetActive(false);
        }
    }
}