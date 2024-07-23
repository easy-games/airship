using System;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Components {
    public class AirshipConstantPixelScaler : MonoBehaviour {
        private void Start() {
            var canvasScaler = GetComponent<CanvasScaler>();
            if (!canvasScaler) return;

            float scale = 1;
            var deviceType = DeviceBridge.GetDeviceType();
            if (deviceType == AirshipDeviceType.Tablet) {
                scale = Screen.dpi / 180;
            } else if (deviceType == AirshipDeviceType.Phone) {
                scale = Mathf.Max(2.5555f, Screen.dpi / 180f);
            } else if (deviceType is AirshipDeviceType.Desktop && Screen.dpi >= 255) {
                scale = 1.75f;
            }

            canvasScaler.scaleFactor = scale;
        }
    }
}