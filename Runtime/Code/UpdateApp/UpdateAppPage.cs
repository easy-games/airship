using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Application = UnityEngine.Device.Application;

public class UpdateAppPage : MonoBehaviour {
    public Button linkButton;
    public TMP_Text linkButtonText;
    public TMP_Text subtitleText;

    private void Start() {
        if (Application.isMobilePlatform) {

        } else {
            this.linkButton.gameObject.SetActive(false);
            this.subtitleText.text = "UPDATE AIRSHIP IN STEAM";
        }
    }

    public void Button_ClickAppStoreLink() {
#if UNITY_IOS
        Application.OpenURL("itms://itunes.apple.com/app/id6480534389");
#endif
    }
}