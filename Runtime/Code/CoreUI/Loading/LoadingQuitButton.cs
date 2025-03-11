using UnityEngine;

public class LoadingQuitButton : MonoBehaviour {
    public Transform visuals;

    public void Button_OnClick() {
        Application.Quit();
    }

    private void Update() {
        #if UNITY_IOS || UNITY_ANDROID
        return;
        #endif
        if (Screen.fullScreen) {
            this.visuals.gameObject.SetActive(true);
        } else {
            this.visuals.gameObject.SetActive(false);
        }
    }
}