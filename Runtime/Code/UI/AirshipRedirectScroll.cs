using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Code.UI {
    [LuauAPI]
    public class AirshipRedirectScroll : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler {
        [NonSerialized] public bool isDragging = false;
        [Tooltip("Mousewheel scrolling and click-and-drag events will be redirected to the RedirectTarget.")]
        public ScrollRect redirectTarget;

        private static string mouseScrollWheelAxis = "Mouse ScrollWheel";
        private bool swallowMouseWheelScrolls = true;
        private bool isMouseOver = false;

        public bool ignoreDrag = false;

        public void OnBeginDrag(PointerEventData eventData) {
            if (this.ignoreDrag) return;
            
            this.isDragging = true;
            if (redirectTarget) {
                redirectTarget.OnBeginDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData) {
            if (this.ignoreDrag) return;
            
            if (redirectTarget) {
                redirectTarget.OnDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData) {
            if (this.ignoreDrag) return;
            
            this.isDragging = false;
            if (redirectTarget) {
                redirectTarget.OnEndDrag(eventData);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isMouseOver = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isMouseOver = false;
        }

        private void Update()
        {
            // Detect the mouse wheel and generate a scroll. This fixes the issue where Unity will prevent our ScrollRect
            // from receiving any mouse wheel messages if the mouse is over a raycast target (such as a button).
            if (isMouseOver && IsMouseWheelRolling() && this.redirectTarget) {
                var delta = UnityEngine.Input.GetAxis(mouseScrollWheelAxis);

                PointerEventData data = new PointerEventData(EventSystem.current);
                data.scrollDelta = new Vector2(0f, delta * this.redirectTarget.scrollSensitivity);

                // Amplify the mousewheel so that it matches the scroll sensitivity.
                // if (data.scrollDelta.y < -Mathf.Epsilon)
                //     data.scrollDelta = new Vector2(0f, -this.redirectTarget.scrollSensitivity);
                // else if (data.scrollDelta.y > Mathf.Epsilon)
                //     data.scrollDelta = new Vector2(0f, this.redirectTarget.scrollSensitivity);

                // swallowMouseWheelScrolls = false;
                this.redirectTarget.OnScroll(data);
                // swallowMouseWheelScrolls = true;
            }
        }

        // public override void OnScroll(PointerEventData data)
        // {
        //     if (IsMouseWheelRolling() && swallowMouseWheelScrolls)
        //     {
        //         // Eat the scroll so that we don't get a double scroll when the mouse is over an image
        //     }
        //     else
        //     {
        //         // Amplify the mousewheel so that it matches the scroll sensitivity.
        //         if (data.scrollDelta.y < -Mathf.Epsilon)
        //             data.scrollDelta = new Vector2(0f, -scrollSensitivity);
        //         else if (data.scrollDelta.y > Mathf.Epsilon)
        //             data.scrollDelta = new Vector2(0f, scrollSensitivity);
        //
        //         // base.OnScroll(data);
        //     }
        // }

        private static bool IsMouseWheelRolling()
        {
            return UnityEngine.Input.GetAxis(mouseScrollWheelAxis) != 0;
        }
    }
}