using UnityEngine;
using UnityEngine.UI;

namespace Code.Components {
    public class AirshipScrollRectConfig : MonoBehaviour {
        private void Start() {
            var scrollRect = GetComponent<ScrollRect>();
            if (!scrollRect) return;

            if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor) {
                scrollRect.scrollSensitivity = 15f;
            } else if (Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor or RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor) {
                scrollRect.scrollSensitivity = 30f;
            }
        }
    }
}