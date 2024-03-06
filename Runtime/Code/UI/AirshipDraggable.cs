using UnityEngine;
using UnityEngine.EventSystems;

namespace Code.UI {
    public class AirshipDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler  {
        public void OnBeginDrag(PointerEventData eventData) {
            print("C# begin drag");
        }

        public void OnDrag(PointerEventData eventData) {
            print("C# drag");
        }

        public void OnEndDrag(PointerEventData eventData) {
            print("C# end drag");
        }

        // public void OnPointerClick(PointerEventData eventData) {
        //     print("C# click");
        // }
    }
}