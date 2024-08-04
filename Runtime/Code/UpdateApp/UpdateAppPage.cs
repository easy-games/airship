using UnityEngine;

public class UpdateAppPage : MonoBehaviour {

    public void Button_ClickAppStoreLink() {
#if UNITY_IOS
        Application.OpenURL("itms://itunes.apple.com/app/id6480534389");
#endif
    }
}