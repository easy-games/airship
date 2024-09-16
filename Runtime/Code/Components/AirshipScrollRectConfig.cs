using UnityEngine;
using UnityEngine.UI;

namespace Code.Components {
    public class AirshipScrollRectConfig : MonoBehaviour {
        private void Awake() {
            var scrollRect = GetComponent<ScrollRect>();
            if (!scrollRect) return;

            if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor) {
                scrollRect.scrollSensitivity = 12f;
            } else if (Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.LinuxPlayer) {
                scrollRect.scrollSensitivity = 16f;
            }
        }
    }
}