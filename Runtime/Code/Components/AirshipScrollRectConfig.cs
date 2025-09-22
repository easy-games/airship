using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Code.Components {
    public class AirshipScrollRectConfig : MonoBehaviour, IBeginDragHandler, IEndDragHandler {
        private ScrollRect scrollRect;

        private void Start() {
            scrollRect = GetComponent<ScrollRect>();
            if (!scrollRect) return;

            if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor) {
                scrollRect.scrollSensitivity = 15f;
            } else if (Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor or RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor) {
                scrollRect.scrollSensitivity = 36f;
            } else if (Application.platform is RuntimePlatform.IPhonePlayer) {
                scrollRect.decelerationRate = 0.135f;
                scrollRect.elasticity = 0.25f;
            }
        }

        public void OnBeginDrag(PointerEventData eventData) {

        }

        public void OnEndDrag(PointerEventData eventData) {
            if (!scrollRect) return;

            if (Application.platform is RuntimePlatform.IPhonePlayer) {
                scrollRect.velocity *= 1.3f;
            }
        }
    }
}