using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AirshipScrollRect : ScrollRect, IBeginDragHandler, IDragHandler, IEndDragHandler {
    public bool disableScrollWheel;
    public ScrollRect redirectScrollWheelInput;

    public override void OnScroll(PointerEventData data) {
        if (redirectScrollWheelInput != null && !Input.GetKeyDown(KeyCode.LeftShift)) {
            redirectScrollWheelInput.OnScroll(data);
            return;
        }
        if (disableScrollWheel) {
            return;
        }
        base.OnScroll(data);
    }
}