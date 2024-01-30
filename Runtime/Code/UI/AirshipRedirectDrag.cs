using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Code.UI {
    public class AirshipRedirectDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
        public bool isDragging = false;
        public ScrollRect redirectTarget;

        public void OnBeginDrag(PointerEventData eventData) {
            this.isDragging = true;
            if (redirectTarget) {
                redirectTarget.OnBeginDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData) {
            if (redirectTarget) {
                redirectTarget.OnDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData) {
            this.isDragging = false;
            if (redirectTarget) {
                redirectTarget.OnEndDrag(eventData);
            }
        }
    }
}