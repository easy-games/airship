using Code.Bundles;
using TMPro;
using UnityEngine;

namespace Code.CoreUI.Components {
    public class AirshipVersionOverlay : MonoBehaviour {
        public TMP_Text versionText;

        private void Start() {
#if !AIRSHIP_PLAYER
            if (Application.isEditor) {
                this.versionText.gameObject.SetActive(false);
                return;
            }
#endif

            var hash = AirshipVersion.GetVersionHash();
            this.versionText.text = Application.version + "-" + hash;
        }
    }
}