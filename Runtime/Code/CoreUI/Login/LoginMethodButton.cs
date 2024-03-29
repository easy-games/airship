using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Code.CoreUI.Login {
    public class LoginMethodButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
        [SerializeField] public Image overlay;

        private void OnEnable() {
            this.overlay.enabled = false;
        }

        public void OnPointerDown(PointerEventData eventData) {
            this.overlay.enabled = true;
        }

        public void OnPointerUp(PointerEventData eventData) {
            this.overlay.enabled = false;
        }
    }
}